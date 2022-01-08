// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnitTestEx.Abstractions;

namespace UnitTestEx.Functions
{
    /// <summary>
    /// Provides the Azure Function <see cref="HttpTriggerTester{TFunction}"/> unit-testing capabilities.
    /// </summary>
    /// <typeparam name="TFunction">The Azure Function <see cref="Type"/>.</typeparam>
    public class HttpTriggerTester<TFunction> where TFunction : class
    {
        private readonly IServiceScope _serviceScope;
        private readonly TestFrameworkImplementor _implementor;

        /// <summary>
        /// Initializes a new <see cref="HttpTriggerTester{TFunction}"/> class.
        /// </summary>
        /// <param name="serviceScope">The <see cref="IServiceScope"/>.</param>
        /// <param name="implementor">The <see cref="TestFrameworkImplementor"/>.</param>
        internal HttpTriggerTester(IServiceScope serviceScope, TestFrameworkImplementor implementor)
        {
            _serviceScope = serviceScope;
            _implementor = implementor;
        }

        /// <summary>
        /// Create the function. Note: cannot instantiate directly as DI not setup for functions so we have rolled our own.
        /// </summary>
        private TFunction CreateFunction()
        {
            // Try instantiating using service provider and use if successful.
            var val = _serviceScope.ServiceProvider.GetService<TFunction>();
            if (val != null)
                return val;

            // Simulate the creation of a request scope.
            var type = typeof(TFunction);
            var ctor = type.GetConstructors().FirstOrDefault();
            if (ctor == null)
                return (TFunction)(Activator.CreateInstance(type) ?? throw new InvalidOperationException($"Unable to instantiate Function Type '{type.Name}'"));

            // Simulate dependency injection for each parameter.
            var pis = ctor.GetParameters();
            var args = new object[pis.Length];
            for (int i = 0; i < pis.Length; i++)
            {
                args[i] = _serviceScope.ServiceProvider.GetRequiredService(pis[i].ParameterType);
            }

            return (TFunction)(Activator.CreateInstance(type, args) ?? throw new InvalidOperationException($"Unable to instantiate Function Type '{type.Name}'"));
        }

        /// <summary>
        /// Runs the HTTP Triggered method.
        /// </summary>
        /// <param name="expression">The funtion execution expression.</param>
        /// <returns>The <see cref="ActionResultAssertor"/>.</returns>
        public ActionResultAssertor Run(Expression<Func<TFunction, IActionResult>> expression)
        {
            if (expression == null)
                throw new ArgumentNullException(nameof(expression));

            if (expression.Body.NodeType != ExpressionType.Call)
                throw new ArgumentException("Expression must be a method invocation.", nameof(expression));

            if (!(expression.Body is MethodCallExpression mce))
                throw new ArgumentException($"Expression must be of Type '{nameof(MethodCallExpression)}'.", nameof(expression));

            var pis = mce.Method.GetParameters();
            var @params = new object?[pis.Length];
            HttpRequest? httpRequest = null;
            object? requestVal = null;
            HttpTriggerAttribute? httpTriggerAttribute = null;

            for (int i = 0; i < mce.Arguments.Count; i++)
            {
                var ue = Expression.Convert(mce.Arguments[i], typeof(object));
                var le = Expression.Lambda<Func<object>>(ue);
                @params[i] = le.Compile().Invoke();

                if (httpRequest == null && @params[i] is HttpRequest hr)
                    httpRequest = hr;

                if (httpTriggerAttribute == null)
                {
                    httpTriggerAttribute = pis[i].GetCustomAttribute<HttpTriggerAttribute>();
                    requestVal = @params[i];
                }
            }

            if (httpTriggerAttribute == null)
                throw new InvalidOperationException($"The function method must have a parameter using the {nameof(HttpTriggerAttribute)}.");

            if (httpRequest != null && !httpTriggerAttribute.Methods.Contains(httpRequest.Method, StringComparer.OrdinalIgnoreCase))
                throw new InvalidOperationException($"The function {nameof(HttpTriggerAttribute)} supports {nameof(HttpTriggerAttribute.Methods)} of {string.Join(" or ", httpTriggerAttribute.Methods.Select(x => $"'{x.ToUpperInvariant()}'"))}; however, invoked using '{httpRequest.Method.ToUpperInvariant()}' which is not valid.");

            var f = CreateFunction();
            var sw = Stopwatch.StartNew();
            var r = mce.Method.Invoke(f, @params);
            if (!(r is IActionResult tar))
                throw new InvalidOperationException($"The function method must return a result of Type {nameof(IActionResult)}.");

            sw.Stop();
            LogOutput(httpRequest, requestVal, tar, sw);
            return new ActionResultAssertor(tar, _implementor);
        }

        /// <summary>
        /// Runs the function using an <see cref="HttpRequestMessage"/> within the <paramref name="expression"/>.
        /// </summary>
        /// <param name="expression">The function operation invocation expression.</param>
        /// <returns>A <see cref="ActionResultAssertor"/>.</returns>
        public ActionResultAssertor Run(Expression<Func<TFunction, Task<IActionResult>>> expression)
        {
            if (expression == null)
                throw new ArgumentNullException(nameof(expression));

            if (expression.Body.NodeType != ExpressionType.Call)
                throw new ArgumentException("Expression must be a method invocation.", nameof(expression));

            if (!(expression.Body is MethodCallExpression mce))
                throw new ArgumentException($"Expression must be of Type '{nameof(MethodCallExpression)}'.", nameof(expression));

            var pis = mce.Method.GetParameters();
            var @params = new object?[pis.Length];
            HttpRequest? httpRequest = null;
            object? requestVal = null;
            HttpTriggerAttribute? httpTriggerAttribute = null;

            for (int i = 0; i < mce.Arguments.Count; i++)
            {
                var ue = Expression.Convert(mce.Arguments[i], typeof(object));
                var le = Expression.Lambda<Func<object>>(ue);
                @params[i] = le.Compile().Invoke();

                if (httpRequest == null && @params[i] is HttpRequest hr)
                    httpRequest = hr;

                if (httpTriggerAttribute == null)
                {
                    httpTriggerAttribute = pis[i].GetCustomAttribute<HttpTriggerAttribute>();
                    requestVal = @params[i];
                }
            }

            if (httpTriggerAttribute == null)
                throw new InvalidOperationException($"The function method must have a parameter using the {nameof(HttpTriggerAttribute)}.");

            if (httpRequest != null && !httpTriggerAttribute.Methods.Contains(httpRequest.Method, StringComparer.OrdinalIgnoreCase))
                throw new InvalidOperationException($"The function {nameof(HttpTriggerAttribute)} supports {nameof(HttpTriggerAttribute.Methods)} of {string.Join(" or ", httpTriggerAttribute.Methods.Select(x => $"'{x.ToUpperInvariant()}'"))}; however, invoked using '{httpRequest.Method.ToUpperInvariant()}' which is not valid.");

            var f = CreateFunction();
            var sw = Stopwatch.StartNew();
            var r = mce.Method.Invoke(f, @params);
            if (!(r is Task<IActionResult> tar))
                throw new InvalidOperationException($"The function method must return a result of Type {nameof(Task<IActionResult>)}.");

            var ar = tar.GetAwaiter().GetResult();
            sw.Stop();
            LogOutput(httpRequest, requestVal, ar, sw);
            return new ActionResultAssertor(ar, _implementor);
        }

        /// <summary>
        /// Log the request/response to the output.
        /// </summary>
        private void LogOutput(HttpRequest? req, object? reqVal, IActionResult res, Stopwatch sw)
        {
            _implementor.WriteLine("");
            _implementor.WriteLine("FUNCTION HTTP-TRIGGER TESTER...");

            if (req != null)
            {
                _implementor.WriteLine("");
                _implementor.WriteLine("REQUEST >");
                _implementor.WriteLine($"Request: {req.Method} {req.GetEncodedUrl()}");
                _implementor.WriteLine($"Headers: {(req.Headers == null || !req.Headers.Any() ? "none" : "")}");
                if (req.Headers != null && req.Headers.Any())
                {
                    foreach (var hdr in req.Headers)
                    {
                        var sb = new StringBuilder();
                        foreach (var v in hdr.Value)
                        {
                            if (sb.Length > 0)
                                sb.Append(", ");

                            sb.Append(v);
                        }

                        _implementor.WriteLine($"  {hdr.Key}: {sb}");
                    }

                    _implementor.WriteLine("");
                }

                JToken? json = null;
                if (req.Body != null)
                {
                    req.Body.Position = 0;
                    using var sr = new StreamReader(req.Body);
                    var body = sr.ReadToEnd();

                    // Parse out the content.
                    if (body.Length > 0)
                    {
                        try
                        {
                            json = JToken.Parse(body);
                        }
                        catch (Exception) { /* This is being swallowed by design. */ }
                    }

                    _implementor.WriteLine($"Content: [{req.ContentType ?? "None"}]");
                    if (json != null || !string.IsNullOrEmpty(body))
                        _implementor.WriteLine(json == null ? body : json.ToString());
                }
            }

            if (req == null && reqVal != null)
            {
                _implementor.WriteLine("");
                _implementor.WriteLine("REQUEST >");
                _implementor.WriteLine($"Type: {reqVal.GetType()}");
                if (reqVal is string str)
                    _implementor.WriteLine($"Content: {str}");
                else if (reqVal is IFormattable ifm)
                    _implementor.WriteLine($"Content: {ifm.ToString(null, CultureInfo.CurrentCulture)}");
                else
                    _implementor.WriteLine(JsonConvert.SerializeObject(reqVal, Formatting.Indented));
            }

            _implementor.WriteLine("");
            _implementor.WriteLine($"RESPONSE >");
            _implementor.WriteLine($"IActionResult: {res.GetType().Name}");
            if (res is IStatusCodeActionResult scar && scar.StatusCode != null)
                _implementor.WriteLine($"HttpStatusCode: {(HttpStatusCode)scar.StatusCode} ({scar.StatusCode})");

            _implementor.WriteLine($"Elapsed (ms): {(sw == null ? "none" : sw.ElapsedMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture))}");
            if (res is ObjectResult or)
            {
                var ct = or.ContentTypes == null || or.ContentTypes.Count == 0 ? "None" : string.Join(",", or.ContentTypes);

                if (or.Value is string str)
                    _implementor.WriteLine($"Content: [{ct}] {str}");
                else if (or.Value is IFormattable ifm)
                    _implementor.WriteLine($"Content: [{ct}] {ifm.ToString(null, CultureInfo.CurrentCulture)}");
                else
                {
                    _implementor.WriteLine($"Content: [{ct}] {(or.Value == null ? "<none>" : $"Type: {or.Value.GetType()}")}");
                    _implementor.WriteLine(JsonConvert.SerializeObject(or.Value, Formatting.Indented));
                }
            }
            else if (res is JsonResult jr)
            {
                _implementor.WriteLine($"Content: [{jr.ContentType ?? "None"}]");
                if (jr.Value != null)
                    _implementor.WriteLine(JsonConvert.SerializeObject(jr.Value, Formatting.Indented));
            }

            _implementor.WriteLine("");
            _implementor.WriteLine(new string('=', 80));
            _implementor.WriteLine("");
        }
    }
}