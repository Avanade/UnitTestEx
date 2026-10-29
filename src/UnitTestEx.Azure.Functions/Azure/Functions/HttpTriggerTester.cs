// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnitTestEx.Abstractions;
using UnitTestEx.Assertors;
using UnitTestEx.Expectations;
using UnitTestEx.Hosting;
using UnitTestEx.Json;

namespace UnitTestEx.Azure.Functions
{
    /// <summary>
    /// Provides Azure Functions isolated-worker HTTP trigger unit-testing capabilities.
    /// </summary>
    /// <typeparam name="TFunction">The Azure Function <see cref="Type"/>.</typeparam>
    /// <remarks>To aid with the testing the following checks are automatically performed during execution with an <see cref="InvalidOperationException"/> thrown when:
    /// <list type="bullet">
    /// <item><description>The <see cref="Microsoft.Azure.Functions.Worker.HttpTriggerAttribute.Methods"/> does not contain the <see cref="HttpRequest.Method"/>.
    /// This check can be further configured using the <see cref="WithNoMethodCheck"/> and <see cref="WithMethodCheck"/> methods prior to the <see cref="RunAsync"/> execution.</description></item>
    /// <item><description>The <see cref="Microsoft.Azure.Functions.Worker.HttpTriggerAttribute.Route"/> does not match the <see cref="HttpRequest.Path"/> and <see cref="HttpRequest.QueryString"/> combination.
    /// This check can be further configured using the <see cref="WithNoRouteCheck"/> and <see cref="WithRouteCheck(RouteCheckOption, StringComparison?)"/> methods prior to the <see cref="RunAsync"/> execution.</description></item>
    /// </list>
    /// The above checks are generally neccessary to assist in ensuring that the function is being invoked correctly given the parameters have to be explicitly passed in separately.
    /// </remarks>
    public class HttpTriggerTester<TFunction> : HostTesterBase<TFunction, HttpTriggerTester<TFunction>>, IExpectations<HttpTriggerTester<TFunction>> where TFunction : class
    {
        private bool _methodCheck = true;
        private RouteCheckOption _routeCheckOption = RouteCheckOption.PathAndQuery;
        private StringComparison _routeComparison = StringComparison.OrdinalIgnoreCase;

        /// <summary>
        /// Initializes a new <see cref="HttpTriggerTester{TFunction}"/> class.
        /// </summary>
        /// <param name="owner">The owning <see cref="TesterBase"/>.</param>
        /// <param name="serviceScope">The <see cref="IServiceScope"/>.</param>
        public HttpTriggerTester(TesterBase owner, IServiceScope serviceScope) : base(owner, serviceScope.ServiceProvider)
        { 
            ExpectationsArranger = new ExpectationsArranger<HttpTriggerTester<TFunction>>(owner, this);
            this.SetHttpMethodCheck(owner.SetUp);
            this.SetHttpRouteCheck(owner.SetUp);
        }

        /// <summary>
        /// Gets the <see cref="ExpectationsArranger{TSelf}"/>.
        /// </summary>
        public ExpectationsArranger<HttpTriggerTester<TFunction>> ExpectationsArranger { get; }
        
        /// <inheritdoc/>
        internal override Task<TValue> InvokeAsync<TValue>(System.Reflection.MethodInfo method, object?[] arguments, Func<Task<TValue>> invoke) => FunctionPipeline.InvokeAsync(Owner, Services, method, arguments, invoke);

        /// <summary>
        /// Indicates that <i>no</i> check is performed to ensure that the <see cref="Microsoft.Azure.Functions.Worker.HttpTriggerAttribute.Methods"/> contain the <see cref="HttpRequest.Method"/>.
        /// </summary>
        /// <remarks>Defaults to <see cref="TestSetUp"/> configuration values where configured; otherwise, <see cref="WithMethodCheck"/>.</remarks>
        public HttpTriggerTester<TFunction> WithNoMethodCheck()
        {
            _methodCheck = false;
            return this;
        }

        /// <summary>
        /// Indicates that a check is performed to ensure that the <see cref="Microsoft.Azure.Functions.Worker.HttpTriggerAttribute.Methods"/> contain the <see cref="HttpRequest.Method"/>.
        /// </summary>
        /// <remarks>Defaults to <see cref="TestSetUp"/> configuration values where configured; otherwise, this is the default.</remarks>
        public HttpTriggerTester<TFunction> WithMethodCheck()
        {
            _methodCheck = true;
            return this;
        }

        /// <summary>
        /// Sets the <see cref="RouteCheckOption"/> to be <see cref="RouteCheckOption.None"/>.
        /// </summary>
        /// <remarks>Defaults to <see cref="TestSetUp"/> configuration values where configured; otherwise, the default is <see cref="RouteCheckOption.PathAndQuery"/> and <see cref="StringComparer.OrdinalIgnoreCase"/></remarks>
        public HttpTriggerTester<TFunction> WithNoRouteCheck() => WithRouteCheck(RouteCheckOption.None);

        /// <summary>
        /// Sets the <see cref="RouteCheckOption"/> to be checked during execution.
        /// </summary>
        /// <param name="option">The <see cref="RouteCheckOption"/>.</param>
        /// <param name="comparison">The <see cref="StringComparison"/>.</param>
        /// <remarks>Defaults to <see cref="TestSetUp"/> configuration values where configured; otherwise, the default is <see cref="RouteCheckOption.PathAndQuery"/> and <see cref="StringComparer.OrdinalIgnoreCase"/></remarks>
        public HttpTriggerTester<TFunction> WithRouteCheck(RouteCheckOption option, StringComparison? comparison = StringComparison.OrdinalIgnoreCase)
        {
            _routeCheckOption = option;
            _routeComparison = comparison ?? StringComparison.OrdinalIgnoreCase;
            return this;
        }

        private static readonly Type[] _httpTriggerAttributeTypes = [typeof(Microsoft.Azure.Functions.Worker.HttpTriggerAttribute)];

        /// <summary>
        /// Runs the HTTP-triggered function returning an <see cref="IActionResult"/>.
        /// </summary>
        /// <param name="expression">The function operation invocation expression.</param>
        /// <returns>An <see cref="ActionResultAssertor"/>.</returns>
        public ActionResultAssertor Run(Expression<Func<TFunction, Task<IActionResult>>> expression) => RunAsync(expression).GetAwaiter().GetResult();

        /// <summary>
        /// Runs the HTTP-triggered function returning an <see cref="IActionResult"/>.
        /// </summary>
        /// <param name="expression">The function operation invocation expression.</param>
        /// <returns>An <see cref="ActionResultAssertor"/>.</returns>
        public async Task<ActionResultAssertor> RunAsync(Expression<Func<TFunction, Task<IActionResult>>> expression)
        {
            var (result, ex, logs) = await CompleteAsync(RunAsync(expression, _httpTriggerAttributeTypes, ValidateHttpRequest)).ConfigureAwait(false);
            return new ActionResultAssertor(Owner, result, logs, ex);
        }

        /// <summary>
        /// Runs the HTTP Triggered (see <see cref="Microsoft.Azure.Functions.Worker.HttpTriggerAttribute"/>) synchronous function returning an <see cref="IActionResult"/>.
        /// </summary>
        /// <param name="expression">The function operation invocation expression.</param>
        /// <returns>An <see cref="ActionResultAssertor"/>.</returns>
        public ActionResultAssertor Run(Expression<Func<TFunction, IActionResult>> expression) => RunAsync(expression).GetAwaiter().GetResult();

        /// <summary>
        /// Runs the HTTP Triggered (see <see cref="Microsoft.Azure.Functions.Worker.HttpTriggerAttribute"/>) synchronous function returning an <see cref="IActionResult"/>.
        /// </summary>
        /// <param name="expression">The function operation invocation expression.</param>
        /// <returns>An <see cref="ActionResultAssertor"/>.</returns>
        public async Task<ActionResultAssertor> RunAsync(Expression<Func<TFunction, IActionResult>> expression)
        {
            var (result, ex, logs) = await CompleteAsync(RunSyncAsync(expression, _httpTriggerAttributeTypes, ValidateHttpRequest)).ConfigureAwait(false);
            return new ActionResultAssertor(Owner, result, logs, ex);
        }

        /// <summary>
        /// Runs the HTTP Triggered (see <see cref="Microsoft.Azure.Functions.Worker.HttpTriggerAttribute"/>) function returning an <see cref="IResult"/>.
        /// </summary>
        /// <param name="expression">The function operation invocation expression.</param>
        /// <returns>An <see cref="HttpResultAssertor"/>.</returns>
        public HttpResultAssertor Run(Expression<Func<TFunction, Task<IResult>>> expression) => RunAsync(expression).GetAwaiter().GetResult();

        /// <summary>
        /// Runs the HTTP Triggered (see <see cref="Microsoft.Azure.Functions.Worker.HttpTriggerAttribute"/>) function returning an <see cref="IResult"/>.
        /// </summary>
        /// <param name="expression">The function operation invocation expression.</param>
        /// <returns>An <see cref="HttpResultAssertor"/>.</returns>
        public async Task<HttpResultAssertor> RunAsync(Expression<Func<TFunction, Task<IResult>>> expression)
        {
            var (result, ex, logs) = await CompleteAsync(RunAsync(expression, _httpTriggerAttributeTypes, ValidateHttpRequest)).ConfigureAwait(false);
            return new HttpResultAssertor(Owner, result, logs, ex);
        }

        /// <summary>
        /// Runs the HTTP Triggered (see <see cref="Microsoft.Azure.Functions.Worker.HttpTriggerAttribute"/>) synchronous function returning an <see cref="IResult"/>.
        /// </summary>
        /// <param name="expression">The function operation invocation expression.</param>
        /// <returns>An <see cref="HttpResultAssertor"/>.</returns>
        public HttpResultAssertor Run(Expression<Func<TFunction, IResult>> expression) => RunAsync(expression).GetAwaiter().GetResult();

        /// <summary>
        /// Runs the HTTP Triggered (see <see cref="Microsoft.Azure.Functions.Worker.HttpTriggerAttribute"/>) synchronous function returning an <see cref="IResult"/>.
        /// </summary>
        /// <param name="expression">The function operation invocation expression.</param>
        /// <returns>An <see cref="HttpResultAssertor"/>.</returns>
        public async Task<HttpResultAssertor> RunAsync(Expression<Func<TFunction, IResult>> expression)
        {
            var (result, ex, logs) = await CompleteAsync(RunSyncAsync(expression, _httpTriggerAttributeTypes, ValidateHttpRequest)).ConfigureAwait(false);
            return new HttpResultAssertor(Owner, result, logs, ex);
        }

        /// <summary>
        /// Runs the HTTP Triggered (see <see cref="Microsoft.Azure.Functions.Worker.HttpTriggerAttribute"/>) function returning an <see cref="HttpResponseData"/>.
        /// </summary>
        /// <param name="expression">The function operation invocation expression.</param>
        /// <returns>An <see cref="HttpResponseDataAssertor"/>.</returns>
        public HttpResponseDataAssertor Run(Expression<Func<TFunction, Task<HttpResponseData>>> expression) => RunAsync(expression).GetAwaiter().GetResult();

        /// <summary>
        /// Runs the HTTP Triggered (see <see cref="Microsoft.Azure.Functions.Worker.HttpTriggerAttribute"/>) function returning an <see cref="HttpResponseData"/>.
        /// </summary>
        /// <param name="expression">The function operation invocation expression.</param>
        /// <returns>An <see cref="HttpResponseDataAssertor"/>.</returns>
        public async Task<HttpResponseDataAssertor> RunAsync(Expression<Func<TFunction, Task<HttpResponseData>>> expression)
        {
            var (result, ex, logs) = await CompleteAsync(RunAsync(expression, _httpTriggerAttributeTypes, ValidateHttpRequest)).ConfigureAwait(false);
            return new HttpResponseDataAssertor(Owner, result, logs, ex);
        }

        /// <summary>
        /// Runs the HTTP Triggered (see <see cref="Microsoft.Azure.Functions.Worker.HttpTriggerAttribute"/>) synchronous function returning an <see cref="HttpResponseData"/>.
        /// </summary>
        /// <param name="expression">The function operation invocation expression.</param>
        /// <returns>An <see cref="HttpResponseDataAssertor"/>.</returns>
        public HttpResponseDataAssertor Run(Expression<Func<TFunction, HttpResponseData>> expression) => RunAsync(expression).GetAwaiter().GetResult();

        /// <summary>
        /// Runs the HTTP Triggered (see <see cref="Microsoft.Azure.Functions.Worker.HttpTriggerAttribute"/>) synchronous function returning an <see cref="HttpResponseData"/>.
        /// </summary>
        /// <param name="expression">The function operation invocation expression.</param>
        /// <returns>An <see cref="HttpResponseDataAssertor"/>.</returns>
        public async Task<HttpResponseDataAssertor> RunAsync(Expression<Func<TFunction, HttpResponseData>> expression)
        {
            var (result, ex, logs) = await CompleteAsync(RunSyncAsync(expression, _httpTriggerAttributeTypes, ValidateHttpRequest)).ConfigureAwait(false);
            return new HttpResponseDataAssertor(Owner, result, logs, ex);
        }

        /// <summary>
        /// Runs an HTTP-triggered function returning an isolated-worker multi-output value.
        /// </summary>
        public FunctionOutputAssertor<TOutput> Run<TOutput>(Expression<Func<TFunction, Task<TOutput>>> expression) => RunAsync(expression).GetAwaiter().GetResult();

        /// <summary>
        /// Runs an HTTP-triggered function returning an isolated-worker multi-output value.
        /// </summary>
        public async Task<FunctionOutputAssertor<TOutput>> RunAsync<TOutput>(Expression<Func<TFunction, Task<TOutput>>> expression)
        {
            var (result, ex, logs) = await CompleteAsync(RunAsync(expression, _httpTriggerAttributeTypes, ValidateHttpRequest)).ConfigureAwait(false);
            return new FunctionOutputAssertor<TOutput>(Owner, result, logs, ex);
        }

        /// <summary>
        /// Runs an HTTP-triggered synchronous function returning an isolated-worker multi-output value.
        /// </summary>
        public FunctionOutputAssertor<TOutput> Run<TOutput>(Expression<Func<TFunction, TOutput>> expression) => RunAsync(expression).GetAwaiter().GetResult();

        /// <summary>
        /// Runs an HTTP-triggered synchronous function returning an isolated-worker multi-output value.
        /// </summary>
        public async Task<FunctionOutputAssertor<TOutput>> RunAsync<TOutput>(Expression<Func<TFunction, TOutput>> expression)
        {
            var (result, ex, logs) = await CompleteAsync(RunSyncAsync(expression, _httpTriggerAttributeTypes, ValidateHttpRequest)).ConfigureAwait(false);
            return new FunctionOutputAssertor<TOutput>(Owner, result, logs, ex);
        }

        /// <summary>
        /// Completes the run: logs the response and asserts the expectations.
        /// </summary>
        private async Task<(TResult Result, Exception? Exception, IEnumerable<string?> Logs)> CompleteAsync<TResult>(Task<(TResult Result, Exception? Exception, double ElapsedMilliseconds)> run)
        {
            var (result, ex, ms) = await run.ConfigureAwait(false);

            await Task.Delay(UnitTestEx.TestSetUp.TaskDelayMilliseconds).ConfigureAwait(false);
            var logs = Owner.SharedState.GetLoggerMessages();
            LogResponse(result, ex, ms, logs);

            await ExpectationsArranger.AssertAsync(logs, ex).ConfigureAwait(false);
            return (result, ex, logs);
        }

        /// <summary>
        /// Validates (and logs) the <see cref="HttpRequest"/> or <see cref="HttpRequestData"/> prior to execution.
        /// </summary>
        private void ValidateHttpRequest((string? Name, object? Value)[] p, Attribute? a, object? v)
        {
            string? method = null;
            Uri? url = null;

            if (v is HttpRequest httpRequest)
            {
                LogRequest(httpRequest, v);
                method = httpRequest.Method;
                url = new Uri(httpRequest.GetDisplayUrl());
            }
            else if (v is HttpRequestData httpRequestData)
            {
                LogRequest(httpRequestData, v);
                method = httpRequestData.Method;
                url = httpRequestData.Url;
            }
            else
                LogRequest((HttpRequest?)null, v);

            if (method is null || url is null)
                return;

            if (a is Microsoft.Azure.Functions.Worker.HttpTriggerAttribute httpTriggerAttribute2)
            {
                if (_methodCheck && httpTriggerAttribute2.Methods is not null && !httpTriggerAttribute2.Methods!.Contains(method, StringComparer.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"The function {nameof(Microsoft.Azure.Functions.Worker.HttpTriggerAttribute)} supports {nameof(Microsoft.Azure.Functions.Worker.HttpTriggerAttribute.Methods)} of {string.Join(" or ", httpTriggerAttribute2.Methods.Select(x => $"'{x.ToUpperInvariant()}'"))}; however, invoked using '{method.ToUpperInvariant()}' which is not valid. Use '{nameof(WithNoMethodCheck)}' to change this behavior.");

                CheckRoute(url, httpTriggerAttribute2.Route, p);
            }
        }

        private void CheckRoute(Uri reqUri, string? route, (string? Name, object? Value)[] @params)
        {
            if (_routeCheckOption == RouteCheckOption.None)
                return;

            var rouUri = new Uri(new Uri($"{reqUri.Scheme}://{reqUri.Host}"), FormatReplacement(route ?? string.Empty, @params));

            switch (_routeCheckOption)
            {
                case RouteCheckOption.Path:
                    if (!string.Equals(reqUri.AbsolutePath, rouUri.AbsolutePath, _routeComparison))
                        throw new InvalidOperationException($"The function route path '{rouUri.AbsolutePath}' does not match the request path '{reqUri.AbsolutePath}'. Use '{nameof(WithRouteCheck)}' to change this behavior.");

                    break;

                case RouteCheckOption.PathAndQuery:
                    if (!string.Equals(reqUri.PathAndQuery, rouUri.PathAndQuery, _routeComparison))
                        throw new InvalidOperationException($"The function route path and query '{rouUri.PathAndQuery}' does not match the request path and query '{reqUri.PathAndQuery}'. Use '{nameof(WithRouteCheck)}' to change this behavior.");

                    break;

                case RouteCheckOption.PathAndQueryStartsWith:
                    if (!reqUri.PathAndQuery.StartsWith(rouUri.PathAndQuery, _routeComparison))
                        throw new InvalidOperationException($"The function route path and query '{rouUri.PathAndQuery}' does not start with the request path and query '{reqUri.PathAndQuery}'. Use '{nameof(WithRouteCheck)}' to change this behavior.");

                    break;

                case RouteCheckOption.Query:
                    if (!string.Equals(reqUri.Query, rouUri.Query, _routeComparison))
                        throw new InvalidOperationException($"The function route query '{rouUri.Query}' does not match the request query '{reqUri.Query}'. Use '{nameof(WithRouteCheck)}' to change this behavior.");

                    break;

                case RouteCheckOption.QueryStartsWith:
                    if (!reqUri.Query.StartsWith(rouUri.Query, _routeComparison))
                        throw new InvalidOperationException($"The function route query '{rouUri.PathAndQuery}' does not start with the request query '{reqUri.PathAndQuery}'. Use '{nameof(WithRouteCheck)}' to change this behavior.");

                    break;
            }
        }

        /// <summary>
        /// Format replacement inspired by: https://github.com/dotnet/runtime/blob/main/src/libraries/Microsoft.Extensions.Logging.Abstractions/src/LogValuesFormatter.cs
        /// </summary>
        private static string FormatReplacement(string route, (string? Name, object? Value)[] @params)
        {
            var sb = new StringBuilder();
            var scanIndex = 0;
            var endIndex = route.Length;

            while (scanIndex < endIndex)
            {
                var openBraceIndex = FindBraceIndex(route, '{', scanIndex, endIndex);
                if (scanIndex == 0 && openBraceIndex == endIndex)
                    return route;  // No holes found.

                var closeBraceIndex = FindBraceIndex(route, '}', openBraceIndex, endIndex);
                if (closeBraceIndex == endIndex)
                {
                    sb.Append(route, scanIndex, endIndex - scanIndex);
                    scanIndex = endIndex;
                }
                else
                {
                    sb.Append(route, scanIndex, openBraceIndex - scanIndex);

                    if (@params is not null)
                    {
                        var pval = @params.Where(x => x.Name != null && MemoryExtensions.Equals(route.AsSpan(openBraceIndex + 1, closeBraceIndex - openBraceIndex - 1), x.Name, StringComparison.Ordinal)).Select(x => x.Value).FirstOrDefault();
                        if (pval is not null)
                            sb.Append(FormatValue(pval));
                    }

                    scanIndex = closeBraceIndex + 1;
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Find the brace index within specified range.
        /// </summary>
        private static int FindBraceIndex(string format, char brace, int startIndex, int endIndex)
        {
            // Example: {{prefix{{{Argument}}}suffix}}.
            var braceIndex = endIndex;
            var scanIndex = startIndex;
            var braceOccurenceCount = 0;

            while (scanIndex < endIndex)
            {
                if (braceOccurenceCount > 0 && format[scanIndex] != brace)
                {
                    if (braceOccurenceCount % 2 == 0)
                    {
                        // Even number of '{' or '}' found. Proceed search with next occurence of '{' or '}'.
                        braceOccurenceCount = 0;
                        braceIndex = endIndex;
                    }
                    else
                    {
                        // An unescaped '{' or '}' found.
                        break;
                    }
                }
                else if (format[scanIndex] == brace)
                {
                    if (brace == '}')
                    {
                        if (braceOccurenceCount == 0)
                        {
                            // For '}' pick the first occurence.
                            braceIndex = scanIndex;
                        }
                    }
                    else
                    {
                        // For '{' pick the last occurence.
                        braceIndex = scanIndex;
                    }

                    braceOccurenceCount++;
                }

                scanIndex++;
            }

            return braceIndex;
        }

        /// <summary>
        /// Formats the value.
        /// </summary>
        private static string FormatValue(object value)
        {
            if (value is DateTime dt)
                return dt.ToString("o", CultureInfo.InvariantCulture);
            else if (value is DateTimeOffset dto)
                return dto.ToString("o", CultureInfo.InvariantCulture);
            else if (value is bool b)
                return b.ToString().ToLowerInvariant();
            else if (value is IFormattable fmt)
                return fmt.ToString(null, CultureInfo.InvariantCulture);
            else
                return value.ToString() ?? string.Empty;
        }

        /// <summary>
        /// Log the <see cref="HttpRequestData"/> request to the output.
        /// </summary>
        private void LogRequest(HttpRequestData req, object? reqVal)
        {
            if (req is TestHttpRequestData thrd)
            {
                LogRequest(thrd.HttpRequest, reqVal);
                return;
            }

            Implementor.WriteLine("");
            Implementor.WriteLine("FUNCTION HTTP-TRIGGER TESTER...");
            Implementor.WriteLine("");
            Implementor.WriteLine("REQUEST >");
            Implementor.WriteLine($"Request: {req.Method} {req.Url}");
            Implementor.WriteLine($"Headers: {(!req.Headers.Any() ? "none" : "")}");
            foreach (var hdr in req.Headers)
            {
                Implementor.WriteLine($"  {hdr.Key}: {string.Join(", ", hdr.Value)}");
            }
        }

        /// <summary>
        /// Log the request to the output.
        /// </summary>
        private void LogRequest(HttpRequest? req, object? reqVal)
        {
            Implementor.WriteLine("");
            Implementor.WriteLine("FUNCTION HTTP-TRIGGER TESTER...");

            if (req != null)
            {
                Implementor.WriteLine("");
                Implementor.WriteLine("REQUEST >");
                Implementor.WriteLine($"Request: {req.Method} {req.GetEncodedUrl()}");
                Implementor.WriteLine($"Headers: {(req.Headers == null || !req.Headers.Any() ? "none" : "")}");
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

                        Implementor.WriteLine($"  {hdr.Key}: {sb}");
                    }

                    Implementor.WriteLine("");
                }

                object? jo = null;
                if (req.Body != null)
                {
                    if (req.Body.CanRead)
                    {
                        req.Body.Position = 0;
                        using var sr = new StreamReader(req.Body, leaveOpen: true);
                        var body = sr.ReadToEnd();

                        // Parse out the content.
                        if (body.Length > 0)
                        {
                            try
                            {
                                jo = JsonSerializer.Deserialize(body);
                            }
                            catch (Exception) { /* This is being swallowed by design. */ }
                        }

                        // Reset the request body position back to start.
                        req.Body.Position = 0;

                        // Continue logging.
                        Implementor.WriteLine($"Content: [{req.ContentType ?? "None"}]");
                        if (jo != null || !string.IsNullOrEmpty(body))
                            Implementor.WriteLine(jo == null ? body : JsonSerializer.Serialize(jo, JsonWriteFormat.Indented));
                    }
                    else
                        Implementor.WriteLine($"Content: [{req.ContentType ?? "None"}] => Response.Body is not in a read state.");
                }
            }

            if (req == null && reqVal != null)
            {
                Implementor.WriteLine("");
                Implementor.WriteLine("REQUEST >");
                Implementor.WriteLine($"Type: {reqVal.GetType()}");
                if (reqVal is string str)
                    Implementor.WriteLine($"Content: {str}");
                else if (reqVal is IFormattable ifm)
                    Implementor.WriteLine($"Content: {ifm.ToString(null, CultureInfo.CurrentCulture)}");
                else
                    Implementor.WriteLine(JsonSerializer.Serialize(reqVal, JsonWriteFormat.Indented));
            }
        }

        /// <summary>
        /// Log the response to the output.
        /// </summary>
        private void LogResponse(object? res, Exception? ex, double ms, IEnumerable<string?>? logs)
        {
            Implementor.WriteLine("");
            Implementor.WriteLine("LOGGING >");
            if (logs is not null && logs.Any())
            {
                foreach (var msg in logs)
                {
                    Implementor.WriteLine(msg);
                }
            }
            else
                Implementor.WriteLine("None.");

            Implementor.WriteLine("");
            Implementor.WriteLine($"RESPONSE >");
            Implementor.WriteLine($"Elapsed (ms): {ms}");
            if (ex != null)
            {
                Implementor.WriteLine($"Exception: {ex.Message} [{ex.GetType().Name}]");
                Implementor.WriteLine(ex.ToString());
            }
            else if (res is IResult ir)
            {
                Implementor.WriteLine($"IResult: {ir.GetType().Name}");
                if (ir is IStatusCodeHttpResult schr && schr.StatusCode != null)
                    Implementor.WriteLine($"HttpStatusCode: {(HttpStatusCode)schr.StatusCode} ({schr.StatusCode})");

                Implementor.WriteLine("Content: use 'ToHttpResponseMessageAssertor' to execute and log the response content.");
            }
            else if (res is HttpResponseData hrd)
            {
                Implementor.WriteLine($"HttpResponseData: {hrd.GetType().Name}");
                Implementor.WriteLine($"HttpStatusCode: {hrd.StatusCode} ({(int)hrd.StatusCode})");
                Implementor.WriteLine("Content: use 'ToHttpResponseMessageAssertor' to convert and log the response content.");
            }
            else if (res is IActionResult)
            {
                Implementor.WriteLine($"IActionResult: {res.GetType().Name}");
                if (res is IStatusCodeActionResult scar && scar.StatusCode != null)
                    Implementor.WriteLine($"HttpStatusCode: {(HttpStatusCode)scar.StatusCode} ({scar.StatusCode})");

                if (res is ObjectResult or)
                {
                    var ct = or.ContentTypes == null || or.ContentTypes.Count == 0 ? "None" : string.Join(",", or.ContentTypes);

                    if (or.Value is string str)
                        Implementor.WriteLine($"Content: [{ct}] {str}");
                    else if (or.Value is IFormattable ifm)
                        Implementor.WriteLine($"Content: [{ct}] {ifm.ToString(null, CultureInfo.CurrentCulture)}");
                    else
                    {
                        Implementor.WriteLine($"Content: [{ct}] {(or.Value == null ? "<none>" : $"Type: {or.Value.GetType()}")}");
                        Implementor.WriteLine(JsonSerializer.Serialize(or.Value, JsonWriteFormat.Indented));
                    }
                }
                else if (res is JsonResult jr)
                {
                    Implementor.WriteLine($"Content: [{jr.ContentType ?? "None"}]");
                    if (jr.Value != null)
                        Implementor.WriteLine(JsonSerializer.Serialize(jr.Value, JsonWriteFormat.Indented));
                }
                else if (res is ContentResult cr)
                {
                    Implementor.WriteLine($"Content: [{cr.ContentType ?? "None"}]");
                    if (cr.Content == null)
                        Implementor.WriteLine("<null>");
                    else
                    {
                        try
                        {
                            var jo = JsonSerializer.Deserialize(cr.Content);
                            Implementor.WriteLine(JsonSerializer.Serialize(jo, JsonWriteFormat.Indented));
                        }
                        catch
                        {
                            Implementor.WriteLine(cr.Content);
                        }
                    }
                }
            }

            //Implementor.WriteLine("");
            //Implementor.WriteLine(new string('=', 80));
            //Implementor.WriteLine("");
        }
    }
}