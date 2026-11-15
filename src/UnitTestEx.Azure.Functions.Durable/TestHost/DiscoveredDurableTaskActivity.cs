using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.DependencyInjection;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace UnitTestEx.Azure.Functions.Durable.TestHost;

/// <summary>Adapts an explicitly discovered Functions activity to the SDK host without running worker bindings or middleware.</summary>
internal sealed class DiscoveredDurableTaskActivity : ITaskActivity
{
    private readonly MethodInfo _method;
    private readonly ParameterInfo[] _parameters;
    private readonly Func<object?, object?[], object?> _invoke;
    private readonly Func<object?, Task<object?>> _readResult;
    private readonly int _inputIndex;
    private readonly IServiceProvider? _services;

    /// <summary>Initializes and validates an activity method before registering the host.</summary>
    /// <param name="method">The discovered worker activity method.</param>
    internal DiscoveredDurableTaskActivity(MethodInfo method)
    {
        _method = method;
        string name = method.GetCustomAttribute<FunctionAttribute>()!.Name;
        _parameters = method.GetParameters();
        int[] inputs = _parameters.Select((parameter, index) => (parameter, index)).Where(item => IsActivityInput(item.parameter)).Select(item => item.index).ToArray();
        if (method.ContainsGenericParameters || method.DeclaringType is null || (!method.IsStatic && method.DeclaringType.IsAbstract)
            || inputs.Length != 1 || _parameters.Any(parameter => parameter.ParameterType.IsByRef || parameter.ParameterType.IsPointer || parameter.ParameterType.IsByRefLike)
            || _parameters.Where((_, index) => index != inputs[0]).Any(parameter => parameter.ParameterType != typeof(FunctionContext) && parameter.ParameterType != typeof(CancellationToken)))
            throw new NotSupportedException($"Discovered activity '{name}' ({method.DeclaringType?.FullName}.{method.Name}) must have one ActivityTrigger input and only optional FunctionContext/CancellationToken parameters, and cannot be generic or use ref/out parameters.");

        _inputIndex = inputs[0];
        InputType = _parameters[_inputIndex].ParameterType;
        Type resultType = method.ReturnType;
        bool typedTask = resultType.IsGenericType && resultType.GetGenericTypeDefinition() == typeof(Task<>);
        if (resultType.IsByRef || resultType.IsPointer || resultType.IsByRefLike || (resultType == typeof(void) && method.IsDefined(typeof(AsyncStateMachineAttribute)))
            || (resultType != typeof(Task) && !typedTask && typeof(Task).IsAssignableFrom(resultType)))
            throw new NotSupportedException($"Discovered activity '{name}' ({method.DeclaringType.FullName}.{method.Name}) has unsupported return type '{resultType}'.");

        OutputType = resultType == typeof(void) || resultType == typeof(Task) || resultType == typeof(ValueTask) ? typeof(object)
            : resultType.IsGenericType && (resultType.GetGenericTypeDefinition() == typeof(Task<>) || resultType.GetGenericTypeDefinition() == typeof(ValueTask<>)) ? resultType.GetGenericArguments()[0] : resultType;
        
        var instance = Expression.Parameter(typeof(object), "instance");
        var arguments = Expression.Parameter(typeof(object[]), "arguments");
        var call = Expression.Call(method.IsStatic ? null : Expression.Convert(instance, method.DeclaringType), method,
            _parameters.Select((parameter, index) => Expression.Convert(Expression.ArrayIndex(arguments, Expression.Constant(index)), parameter.ParameterType)));
        
        Expression body = resultType == typeof(void) ? Expression.Block(call, Expression.Constant(null, typeof(object))) : Expression.Convert(call, typeof(object));
        _invoke = Expression.Lambda<Func<object?, object?[], object?>>(body, instance, arguments).Compile();
        _readResult = CreateResultReader(resultType);
    }

    /// <summary>Creates an invocation adapter bound to the application's service provider.</summary>
    private DiscoveredDurableTaskActivity(DiscoveredDurableTaskActivity activity, IServiceProvider services)
    {
        _method = activity._method;
        _parameters = activity._parameters;
        _invoke = activity._invoke;
        _readResult = activity._readResult;
        _inputIndex = activity._inputIndex;
        InputType = activity.InputType;
        OutputType = activity.OutputType;
        _services = services;
    }

    /// <inheritdoc/>
    public Type InputType { get; }

    /// <inheritdoc/>
    public Type OutputType { get; }

    /// <summary>Gets the discovered method for duplicate-name validation.</summary>
    internal MethodInfo Method => _method;

    /// <summary>Recognizes the worker Durable trigger without requiring its runtime binding extension as a library dependency.</summary>
    /// <param name="parameter">The candidate activity input parameter.</param>
    /// <returns>Whether the parameter has the isolated-worker ActivityTrigger attribute.</returns>
    internal static bool IsActivityInput(ParameterInfo parameter)
        => parameter.GetCustomAttributesData().Any(attribute => attribute.AttributeType.FullName == "Microsoft.Azure.Functions.Worker.ActivityTriggerAttribute");

    /// <summary>Registers discovered activities that have not been explicitly replaced.</summary>
    /// <param name="registry">The SDK host registry containing explicit registrations.</param>
    /// <param name="activities">The validated discovered methods, if discovery was enabled.</param>
    /// <param name="services">The application's root provider used to create invocation scopes.</param>
    internal static void Register(DurableTaskRegistry registry, IReadOnlyDictionary<string, DiscoveredDurableTaskActivity>? activities, IServiceProvider services)
    {
        if (activities is null)
            return;

        var explicitNames = registry.GetActivities().Select(activity => activity.Key.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var activity in activities)
        {
            if (!explicitNames.Contains(activity.Key))
                registry.AddActivity(activity.Key, new DiscoveredDurableTaskActivity(activity.Value, services));
        }
    }

    /// <inheritdoc/>
    public async Task<object?> RunAsync(TaskActivityContext context, object? input)
    {
        await using var scope = (_services ?? throw new InvalidOperationException("The discovered activity has not been bound to application services.")).CreateAsyncScope();
        object? instance = null;
        bool ownsInstance = false;
        try
        {
            if (!_method.IsStatic)
            {
                instance = scope.ServiceProvider.GetService(_method.DeclaringType!);
                if (instance is null)
                {
                    instance = ActivatorUtilities.CreateInstance(scope.ServiceProvider, _method.DeclaringType!);
                    ownsInstance = true;
                }
            }

            var functionContext = new TestFunctionContext(scope.ServiceProvider, context.Name.Name);
            var arguments = new object?[_parameters.Length];
            for (int index = 0; index < arguments.Length; index++)
                arguments[index] = index == _inputIndex ? input : _parameters[index].ParameterType == typeof(FunctionContext) ? functionContext : functionContext.CancellationToken;

            return await _readResult(_invoke(instance, arguments)).ConfigureAwait(false);
        }
        finally
        {
            if (ownsInstance && instance is IAsyncDisposable asyncDisposable)
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            else if (ownsInstance && instance is IDisposable disposable)
                disposable.Dispose();
        }
    }

    /// <summary>Builds a typed asynchronous result reader once during discovery.</summary>
    private static Func<object?, Task<object?>> CreateResultReader(Type resultType)
    {
        if (resultType == typeof(Task))
            return async result => { await ((Task)result!).ConfigureAwait(false); return null; };

        if (resultType == typeof(ValueTask))
            return async result => { await ((ValueTask)result!).ConfigureAwait(false); return null; };

        if (resultType.IsGenericType && (resultType.GetGenericTypeDefinition() == typeof(Task<>) || resultType.GetGenericTypeDefinition() == typeof(ValueTask<>)))
        {
            string name = resultType.GetGenericTypeDefinition() == typeof(Task<>) ? nameof(ReadTaskResult) : nameof(ReadValueTaskResult);
            return typeof(DiscoveredDurableTaskActivity).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!.MakeGenericMethod(resultType.GetGenericArguments()[0])
                .CreateDelegate<Func<object?, Task<object?>>>();
        }

        return Task.FromResult;
    }

    /// <summary>Awaits a typed task and boxes its result without reflection during invocation.</summary>
    private static async Task<object?> ReadTaskResult<T>(object? result) => await ((Task<T>)result!).ConfigureAwait(false);

    /// <summary>Awaits a typed value task and boxes its result without reflection during invocation.</summary>
    private static async Task<object?> ReadValueTaskResult<T>(object? result) => await ((ValueTask<T>)result!).ConfigureAwait(false);
}
