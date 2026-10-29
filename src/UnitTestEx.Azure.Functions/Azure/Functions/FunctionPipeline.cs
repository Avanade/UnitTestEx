// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Abstractions;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Invocation;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using UnitTestEx.Abstractions;

namespace UnitTestEx.Azure.Functions
{
    /// <summary>
    /// Provides the internal function tester options.
    /// </summary>
    internal interface IFunctionTesterOptions
    {
        /// <summary>
        /// Indicates whether the function is executed within the application's isolated worker middleware pipeline.
        /// </summary>
        bool UseWorkerMiddleware { get; }
    }

    /// <summary>
    /// Executes a function method within the application's own isolated worker middleware pipeline (as registered via <c>UseMiddleware</c>, <c>UseWhen</c>, etc.).
    /// </summary>
    /// <remarks>The pipeline is the application's <see cref="FunctionExecutionDelegate"/>; the terminal <c>FunctionExecutionMiddleware</c> is redirected (via the <see cref="IFunctionExecutor"/> feature) to the test's method invocation.
    /// <para>Internal worker contracts that have no public equivalent (<c>IFunctionBindingsFeature</c> and the ASP.NET Core integration <c>IHttpCoordinator</c>) are implemented using a <see cref="DispatchProxy"/>; i.e. no private state is accessed.</para></remarks>
    internal static class FunctionPipeline
    {
        internal const string HttpContextItemKey = "UnitTestEx.HttpContext";
        private const string HttpCoordinatorTypeName = "Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore.IHttpCoordinator";
        private const string BindingsFeatureTypeName = "Microsoft.Azure.Functions.Worker.Context.Features.IFunctionBindingsFeature";
        private const string OutputBindingsInfoProviderTypeName = "Microsoft.Azure.Functions.Worker.OutputBindings.IOutputBindingsInfoProvider";

        /// <summary>
        /// Replaces the ASP.NET Core integration <c>IHttpCoordinator</c> (where registered) as there is no HTTP proxy request to coordinate with; the test <see cref="HttpContext"/> is provided directly.
        /// </summary>
        public static void ConfigureServices(IServiceCollection services)
        {
            var descriptors = services.Where(x => x.ServiceType.FullName == HttpCoordinatorTypeName).ToArray();
            if (descriptors.Length == 0)
                return;

            foreach (var sd in descriptors)
                services.Remove(sd);

            var type = descriptors[0].ServiceType;
            services.AddSingleton(type, _ => DispatchProxy.Create(type, typeof(HttpCoordinatorProxy)));
        }

        /// <summary>
        /// Invokes the function method within the worker middleware pipeline.
        /// </summary>
        public static async Task<TValue> InvokeAsync<TValue>(TesterBase owner, IServiceProvider services, MethodInfo method, object?[] arguments, Func<Task<TValue>> invoke)
        {
            var pipeline = owner is IFunctionTesterOptions { UseWorkerMiddleware: false } ? null : services.GetService<FunctionExecutionDelegate>();
            if (pipeline is null)
                return await invoke().ConfigureAwait(false);

            var context = arguments.OfType<TestFunctionContext>().FirstOrDefault()
                ?? arguments.OfType<HttpRequestData>().Select(x => x.FunctionContext).OfType<TestFunctionContext>().FirstOrDefault()
                ?? new TestFunctionContext(services);

            context.InstanceServices = services;
            context.TestFunctionDefinition ??= TestFunctionDefinition.Create(method);

            var bindings = CreateBindingsFeature(services, context);
            var executor = new TestFunctionExecutor<TValue>(invoke, TestFunctionDefinition.HasReturnValue(method), bindings);
            context.Features.Set<IFunctionExecutor>(executor);

            var requestData = arguments.OfType<HttpRequestData>().FirstOrDefault();
            if (requestData is not null)
                context.Features.Set<IHttpRequestDataFeature>(new HttpRequestDataFeature(requestData));

            var httpContext = arguments.OfType<HttpRequest>().FirstOrDefault()?.HttpContext ?? (requestData as TestHttpRequestData)?.HttpRequest.HttpContext;
            object? priorHttpContext = null;
            if (httpContext is not null)
            {
                httpContext.RequestServices = services;
                if (httpContext.Response.Body == Stream.Null)
                    httpContext.Response.Body = new MemoryStream();

                // The ASP.NET Core integration middleware adds (not sets) the HttpContext item; remove to avoid a duplicate key, restoring after where not re-added.
                context.Items[HttpContextItemKey] = httpContext;
                if (context.Items.Remove(TestHttpRequestData.HttpContextKey, out priorHttpContext) is false)
                    priorHttpContext = null;
            }

            try
            {
                await pipeline(context).ConfigureAwait(false);
            }
            finally
            {
                context.Items.Remove(HttpContextItemKey);
                if (priorHttpContext is not null)
                    context.Items.TryAdd(TestHttpRequestData.HttpContextKey, priorHttpContext);
            }

            if (bindings.InvocationResult is TValue value)
                return value;

            // The ASP.NET Core integration clears an HttpResponseData invocation result once written to the HttpContext response.
            if (bindings.LastHttpResponseData is TValue hrd)
                return hrd;

            if (executor.Completed)
                return executor.Result!;

            if (httpContext is not null && typeof(TValue).IsAssignableFrom(typeof(HttpResponseReplayResult)))
                return (TValue)(object)HttpResponseReplayResult.Create(httpContext.Response);

            if (executor.Exception is not null)
                return default!;

            throw new InvalidOperationException($"Function method '{method.DeclaringType?.Name}.{method.Name}' was not executed; the worker middleware pipeline short-circuited the invocation without setting a compatible invocation result.");
        }

        /// <summary>
        /// Creates the <c>IFunctionBindingsFeature</c> and adds to the <paramref name="context"/>.
        /// </summary>
        private static FunctionBindingsProxy CreateBindingsFeature(IServiceProvider services, TestFunctionContext context)
        {
            var coreAssembly = typeof(FunctionExecutionDelegate).Assembly;
            var featureType = coreAssembly.GetType(BindingsFeatureTypeName, throwOnError: true)!;
            var proxy = (FunctionBindingsProxy)DispatchProxy.Create(featureType, typeof(FunctionBindingsProxy));

            var providerType = coreAssembly.GetType(OutputBindingsInfoProviderTypeName, throwOnError: true)!;
            var provider = services.GetService(providerType);
            proxy.OutputBindingsInfo = provider is null ? null : providerType.GetMethod("GetBindingsInfo")!.Invoke(provider, [context.FunctionDefinition]);

            context.SetFeature(featureType, proxy);
            return proxy;
        }

        /// <summary>
        /// Provides the <see cref="IFunctionExecutor"/> that invokes the test method.
        /// </summary>
        private sealed class TestFunctionExecutor<TValue>(Func<Task<TValue>> invoke, bool hasReturnValue, FunctionBindingsProxy bindings) : IFunctionExecutor
        {
            public bool Completed { get; private set; }

            public TValue? Result { get; private set; }

            public Exception? Exception { get; private set; }

            public async ValueTask ExecuteAsync(FunctionContext context)
            {
                try
                {
                    Result = await invoke().ConfigureAwait(false);
                    Completed = true;
                    if (hasReturnValue)
                        bindings.InvocationResult = Result;
                }
                catch (TargetInvocationException tie) when (tie.InnerException is not null)
                {
                    Exception = tie.InnerException;
                    ExceptionDispatchInfo.Capture(tie.InnerException).Throw();
                }
                catch (Exception ex)
                {
                    Exception = ex;
                    throw;
                }
            }
        }

        /// <summary>
        /// Provides the <see cref="IHttpRequestDataFeature"/> for the test <see cref="HttpRequestData"/>.
        /// </summary>
        private sealed class HttpRequestDataFeature(HttpRequestData requestData) : IHttpRequestDataFeature
        {
            public ValueTask<HttpRequestData?> GetHttpRequestDataAsync(FunctionContext context) => ValueTask.FromResult<HttpRequestData?>(requestData);
        }
    }

    /// <summary>
    /// Provides the <c>IFunctionBindingsFeature</c> (internal worker contract) implementation.
    /// </summary>
    internal class FunctionBindingsProxy : DispatchProxy
    {
        private static readonly IReadOnlyDictionary<string, object?> _empty = new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>());

        private object? _invocationResult;

        public object? InvocationResult
        {
            get => _invocationResult;
            set
            {
                _invocationResult = value;
                if (value is HttpResponseData hrd)
                    LastHttpResponseData = hrd;
            }
        }

        public HttpResponseData? LastHttpResponseData { get; private set; }

        public object? OutputBindingsInfo { get; set; }

        public IDictionary<string, object?> OutputBindingData { get; } = new Dictionary<string, object?>();

        /// <inheritdoc/>
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => targetMethod?.Name switch
        {
            "get_InvocationResult" => InvocationResult,
            "set_InvocationResult" => InvocationResult = args![0],
            "get_OutputBindingData" => OutputBindingData,
            "get_OutputBindingsInfo" => OutputBindingsInfo,
            "get_TriggerMetadata" or "get_InputData" => _empty,
            _ => throw new NotSupportedException($"UnitTestEx does not support the worker '{targetMethod?.DeclaringType?.Name}.{targetMethod?.Name}' member.")
        };
    }

    /// <summary>
    /// Provides the ASP.NET Core integration <c>IHttpCoordinator</c> (internal worker contract) implementation; returns the test <see cref="HttpContext"/> directly.
    /// </summary>
    internal class HttpCoordinatorProxy : DispatchProxy
    {
        /// <inheritdoc/>
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            switch (targetMethod?.Name)
            {
                case "SetFunctionContextAsync":
                    var context = (FunctionContext)args![1]!;
                    var httpContext = context.Items.TryGetValue(FunctionPipeline.HttpContextItemKey, out var hc) && hc is HttpContext h ? h : new DefaultHttpContext { RequestServices = context.InstanceServices };
                    if (httpContext.Response.Body == Stream.Null)
                        httpContext.Response.Body = new MemoryStream();

                    return Task.FromResult(httpContext);

                case "CompleteFunctionInvocation":
                    return null;

                default:
                    throw new NotSupportedException($"UnitTestEx does not support the worker '{targetMethod?.DeclaringType?.Name}.{targetMethod?.Name}' member.");
            }
        }
    }

    /// <summary>
    /// Provides a <see cref="FunctionDefinition"/> inferred from the function method and its binding attributes.
    /// </summary>
    internal sealed class TestFunctionDefinition : FunctionDefinition
    {
        public override ImmutableArray<FunctionParameter> Parameters { get; } = [];

        public override string PathToAssembly { get; } = string.Empty;

        public override string EntryPoint { get; } = string.Empty;

        public override string Id { get; } = string.Empty;

        public override string Name { get; } = string.Empty;

        public override IImmutableDictionary<string, BindingMetadata> InputBindings { get; } = ImmutableDictionary<string, BindingMetadata>.Empty;

        public override IImmutableDictionary<string, BindingMetadata> OutputBindings { get; } = ImmutableDictionary<string, BindingMetadata>.Empty;

        private TestFunctionDefinition(string name, string entryPoint, string pathToAssembly, ImmutableArray<FunctionParameter> parameters, IImmutableDictionary<string, BindingMetadata> inputs, IImmutableDictionary<string, BindingMetadata> outputs)
        {
            Name = name;
            Id = name;
            EntryPoint = entryPoint;
            PathToAssembly = pathToAssembly;
            Parameters = parameters;
            InputBindings = inputs;
            OutputBindings = outputs;
        }

        /// <summary>
        /// Creates the <see cref="FunctionDefinition"/> from the <paramref name="method"/>.
        /// </summary>
        public static FunctionDefinition Create(MethodInfo method)
        {
            var inputs = ImmutableDictionary.CreateBuilder<string, BindingMetadata>();
            foreach (var p in method.GetParameters())
            {
                var ba = p.GetCustomAttributes().FirstOrDefault(a => a is InputBindingAttribute or TriggerBindingAttribute);
                if (ba is not null && p.Name is not null)
                    inputs[p.Name] = new TestBindingMetadata(p.Name, GetBindingType(ba.GetType()), BindingDirection.In);
            }

            var outputs = ImmutableDictionary.CreateBuilder<string, BindingMetadata>();
            var returnType = GetReturnType(method);
            var returnBinding = method.ReturnParameter.GetCustomAttributes().Concat(method.GetCustomAttributes()).FirstOrDefault(a => a is OutputBindingAttribute);
            if (returnBinding is not null)
                outputs["$return"] = new TestBindingMetadata("$return", GetBindingType(returnBinding.GetType()), BindingDirection.Out);
            else if (returnType is not null && IsHttpResponseType(returnType))
                outputs["$return"] = new TestBindingMetadata("$return", "http", BindingDirection.Out);
            else if (returnType is not null)
            {
                foreach (var pi in returnType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    var pa = pi.GetCustomAttributes().FirstOrDefault(a => a is OutputBindingAttribute || a.GetType().Name == "HttpResultAttribute");
                    if (pa is not null)
                        outputs[pi.Name] = new TestBindingMetadata(pi.Name, GetBindingType(pa.GetType()), BindingDirection.Out);
                    else if (typeof(HttpResponseData).IsAssignableFrom(pi.PropertyType))
                        outputs[pi.Name] = new TestBindingMetadata(pi.Name, "http", BindingDirection.Out);
                }
            }

            var name = method.GetCustomAttribute<FunctionAttribute>()?.Name ?? method.Name;
            var parameters = method.GetParameters().Select(p => new FunctionParameter(p.Name ?? string.Empty, p.ParameterType)).ToImmutableArray();
            return new TestFunctionDefinition(name, $"{method.DeclaringType?.FullName}.{method.Name}", method.DeclaringType?.Assembly.Location ?? string.Empty, parameters, inputs.ToImmutable(), outputs.ToImmutable());
        }

        /// <summary>
        /// Indicates whether the <paramref name="method"/> returns a value (i.e. not <c>void</c> or <see cref="Task"/>).
        /// </summary>
        public static bool HasReturnValue(MethodInfo method) => GetReturnType(method) is not null;

        private static Type? GetReturnType(MethodInfo method)
        {
            var type = method.ReturnType;
            if (type == typeof(void) || type == typeof(Task) || type == typeof(ValueTask))
                return null;

            if (type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(Task<>) || type.GetGenericTypeDefinition() == typeof(ValueTask<>)))
                return type.GetGenericArguments()[0];

            return type;
        }

        private static bool IsHttpResponseType(Type type) => typeof(HttpResponseData).IsAssignableFrom(type) || typeof(IActionResult).IsAssignableFrom(type) || typeof(IResult).IsAssignableFrom(type);

        /// <summary>
        /// Gets the binding type (as would be generated within the function metadata) from the attribute type name; e.g. '<c>HttpTriggerAttribute</c>' is '<c>httpTrigger</c>', and '<c>BlobInputAttribute</c>' is '<c>blob</c>'.
        /// </summary>
        private static string GetBindingType(Type attributeType)
        {
            var name = attributeType.Name;
            if (name.EndsWith("Attribute", StringComparison.Ordinal))
                name = name[..^"Attribute".Length];

            if (name == "HttpResult")
                return "http";

            if (name.EndsWith("Input", StringComparison.Ordinal))
                name = name[..^"Input".Length];
            else if (name.EndsWith("Output", StringComparison.Ordinal))
                name = name[..^"Output".Length];

            return name.Length == 0 ? name : char.ToLowerInvariant(name[0]) + name[1..];
        }

        private sealed class TestBindingMetadata(string name, string type, BindingDirection direction) : BindingMetadata
        {
            public override string Name { get; } = name;

            public override string Type { get; } = type;

            public override BindingDirection Direction { get; } = direction;
        }
    }

    /// <summary>
    /// Provides an <see cref="IActionResult"/> and <see cref="IResult"/> that replays a captured <see cref="HttpResponse"/>; i.e. where worker middleware writes the response directly without invoking the function.
    /// </summary>
    internal sealed class HttpResponseReplayResult : IActionResult, IResult
    {
        private int _statusCode;
        private readonly List<KeyValuePair<string, Microsoft.Extensions.Primitives.StringValues>> _headers = [];
        private byte[] _body = [];

        /// <summary>
        /// Creates the result from the <paramref name="response"/>.
        /// </summary>
        public static HttpResponseReplayResult Create(HttpResponse response)
        {
            var r = new HttpResponseReplayResult { _statusCode = response.StatusCode };
            r._headers.AddRange(response.Headers);
            if (response.Body is MemoryStream ms)
                r._body = ms.ToArray();

            return r;
        }

        /// <inheritdoc/>
        public Task ExecuteResultAsync(ActionContext context) => ExecuteAsync(context.HttpContext);

        /// <inheritdoc/>
        public async Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.StatusCode = _statusCode;
            foreach (var h in _headers)
                httpContext.Response.Headers[h.Key] = h.Value;

            if (_body.Length > 0)
                await httpContext.Response.Body.WriteAsync(_body).ConfigureAwait(false);
        }
    }
}
