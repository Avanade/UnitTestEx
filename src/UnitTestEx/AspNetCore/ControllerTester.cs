// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using CoreEx.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.TestHost;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Net.Mime;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnitTestEx.Abstractions;
using UnitTestEx.Assertors;
using Ceh = CoreEx.Http;

namespace UnitTestEx.AspNetCore
{
    /// <summary>
    /// Enables the testing of a <see cref="ControllerBase"/> operation.
    /// </summary>
    /// <typeparam name="TController">The <see cref="ControllerBase"/> <see cref="Type"/>.</typeparam>
    public class ControllerTester<TController> where TController : ControllerBase
    {
        private readonly TestServer _testServer;
        private readonly IJsonSerializer _jsonSerializer;
        private readonly TestFrameworkImplementor _implementor;

        /// <summary>
        /// Initializes a new <see cref="ControllerTester{TController}"/> class.
        /// </summary>
        /// <param name="testServer">The <see cref="TestServer"/>.</param>
        /// <param name="implementor">The <see cref="TestFrameworkImplementor"/>.</param>
        /// <param name="jsonSerializer">The <see cref="IJsonSerializer"/>.</param>
        internal ControllerTester(TestServer testServer, TestFrameworkImplementor implementor, IJsonSerializer jsonSerializer)
        {
            _testServer = testServer;
            _implementor = implementor;
            _jsonSerializer = jsonSerializer;
        }

        /// <summary>
        /// Runs the controller using an <see cref="HttpRequestMessage"/>.
        /// </summary>
        /// <param name="httpMethod">The <see cref="HttpMethod"/></param>
        /// <param name="requestUri">The string that represents the request <see cref="Uri"/>.</param>
        /// <param name="value">The optional request body value.</param>
        /// <param name="requestOptions">The optional <see cref="Ceh.HttpRequestOptions"/>.</param>
        /// <param name="args">Zero or more <see cref="Ceh.IHttpArg"/> objects for <paramref name="requestUri"/> templating, query string additions, and content body specification.</param>
        /// <returns>An <see cref="HttpResponseMessageAssertor"/>.</returns>
        public HttpResponseMessageAssertor RunWithRequest(HttpMethod httpMethod, string? requestUri, object? value = null, Ceh.HttpRequestOptions? requestOptions = null, params Ceh.IHttpArg[] args)
            => RunWithRequestAsync(httpMethod, requestUri, value, requestOptions, args).GetAwaiter().GetResult();

        /// <summary>
        /// Runs the controller using an <see cref="HttpRequestMessage"/>.
        /// </summary>
        /// <param name="httpMethod">The <see cref="HttpMethod"/></param>
        /// <param name="requestUri">The string that represents the request <see cref="Uri"/>.</param>
        /// <param name="value">The optional request body value.</param>
        /// <param name="requestOptions">The optional <see cref="Ceh.HttpRequestOptions"/>.</param>
        /// <param name="args">Zero or more <see cref="Ceh.IHttpArg"/> objects for <paramref name="requestUri"/> templating, query string additions, and content body specification.</param>
        /// <returns>An <see cref="HttpResponseMessageAssertor"/>.</returns>
        public async Task<HttpResponseMessageAssertor> RunWithRequestAsync(HttpMethod httpMethod, string? requestUri, object? value = null, Ceh.HttpRequestOptions? requestOptions = null, params Ceh.IHttpArg[] args)
        {
            var tc = new TypedHttpClient(_testServer.CreateClient(), _jsonSerializer);
            var sw = Stopwatch.StartNew();
            var res = await tc.SendAsync(httpMethod, requestUri, value, requestOptions, args).ConfigureAwait(false);

            sw.Stop();
            LogOutput(res, sw);

            return new HttpResponseMessageAssertor(res, _implementor, _jsonSerializer);
        }

        /// <summary>
        /// Log the request/response to the output.
        /// </summary>
        private void LogOutput(HttpResponseMessage res, Stopwatch sw)
        {
            if (res.RequestMessage == null)
                return;

            _implementor.WriteLine("");
            _implementor.WriteLine("API TESTER...");
            _implementor.WriteLine("");
            _implementor.WriteLine("REQUEST >");
            _implementor.WriteLine($"Request: {res.RequestMessage.Method} {res.RequestMessage.RequestUri}");
            _implementor.WriteLine($"Headers: {(res.RequestMessage.Headers == null || !res.RequestMessage.Headers.Any() ? "none" : "")}");
            if (res.RequestMessage.Headers != null && res.RequestMessage.Headers.Any())
            {
                foreach (var hdr in res.RequestMessage.Headers)
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

            object? jo = null;
            if (res.RequestMessage.Content != null)
            {
                // HACK: The Request Content is a forward only stream that is already read; we need to reset this private variable back to the start.
                if (res.RequestMessage.Content is StreamContent)
                {
                    var fi = typeof(StreamContent).GetField("_content", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    var ms = (MemoryStream)fi!.GetValue(res.RequestMessage.Content)!;
                    ms.Position = 0;
                }

                // Parse out the content.
                try
                {
                    jo = _jsonSerializer.Deserialize(res.RequestMessage.Content.ReadAsStringAsync().Result);
                }
                catch (Exception) { /* This is being swallowed by design. */ }

                _implementor.WriteLine($"Content: [{res.RequestMessage.Content?.Headers?.ContentType?.MediaType ?? "None"}]");
                _implementor.WriteLine(jo == null ? res.RequestMessage.Content?.ToString() : _jsonSerializer.Serialize(jo, JsonWriteFormat.Indented));
            }

            _implementor.WriteLine("");
            _implementor.WriteLine($"RESPONSE >");
            _implementor.WriteLine($"HttpStatusCode: {res.StatusCode} ({(int)res.StatusCode})");
            _implementor.WriteLine($"Elapsed (ms): {(sw == null ? "none" : sw.ElapsedMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture))}");

            var hdrs = res.Headers?.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            _implementor.WriteLine($"Headers: {(hdrs == null || !hdrs.Any() ? "none" : "")}");
            if (hdrs != null && hdrs.Any())
            {
                foreach (var hdr in hdrs)
                {
                    _implementor.WriteLine($"  {hdr}");
                }
            }

            jo = null;
            var content = res.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (!string.IsNullOrEmpty(content) && res.Content?.Headers?.ContentType?.MediaType == MediaTypeNames.Application.Json)
            {
                try
                {
                    jo = _jsonSerializer.Deserialize(content);
                }
                catch (Exception) { /* This is being swallowed by design. */ }
            }

            var txt = $"Content: [{res.Content?.Headers?.ContentType?.MediaType ?? "none"}]";
            if (jo != null)
            {
                _implementor.WriteLine(txt);
                _implementor.WriteLine(_jsonSerializer.Serialize(jo, JsonWriteFormat.Indented));
            }
            else
                _implementor.WriteLine($"{txt} {(string.IsNullOrEmpty(content) ? "none" : content)}");

            _implementor.WriteLine("");
            _implementor.WriteLine(new string('=', 80));
            _implementor.WriteLine("");
        }

        /// <summary>
        /// Runs the controller using an <see cref="HttpRequestMessage"/> inferring the <see cref="HttpMethod"/>, operation name and request from the <paramref name="expression"/>.
        /// </summary>
        /// <typeparam name="TResult">The result <see cref="Type"/>.</typeparam>
        /// <param name="expression">The controller operation invocation expression.</param>
        /// <param name="requestOptions">The optional <see cref="Ceh.HttpRequestOptions"/>.</param>
        /// <param name="value">The optional body value where not explicitly passed via the <paramref name="expression"/>.</param>
        /// <param name="args">Zero or more <see cref="Ceh.IHttpArg"/> objects for <see cref="Uri"/> templating, query string additions, and content body specification.</param>
        /// <returns>A <see cref="HttpResponseMessageAssertor"/>.</returns>
        public HttpResponseMessageAssertor Run<TResult>(Expression<Func<TController, TResult>> expression, object? value = null, Ceh.HttpRequestOptions? requestOptions = null, params Ceh.IHttpArg[] args)
            => RunAsync(expression, value, requestOptions, args).GetAwaiter().GetResult();

        /// <summary>
        /// Runs the controller using an <see cref="HttpRequestMessage"/> inferring the <see cref="HttpMethod"/>, operation name and request from the <paramref name="expression"/>.
        /// </summary>
        /// <typeparam name="TResult">The result <see cref="Type"/>.</typeparam>
        /// <param name="expression">The controller operation invocation expression.</param>
        /// <param name="value">The optional body value where not explicitly passed via the <paramref name="expression"/>.</param>
        /// <param name="requestOptions">The optional <see cref="Ceh.HttpRequestOptions"/>.</param>
        /// <param name="args">Zero or more <see cref="Ceh.IHttpArg"/> objects for <see cref="Uri"/> templating, query string additions, and content body specification.</param>
        /// <returns>A <see cref="HttpResponseMessageAssertor"/>.</returns>
        public async Task<HttpResponseMessageAssertor> RunAsync<TResult>(Expression<Func<TController, TResult>> expression, object? value = null, Ceh.HttpRequestOptions? requestOptions = null, params Ceh.IHttpArg[] args)
        {
            if (expression.Body.NodeType != ExpressionType.Call)
                throw new ArgumentException("Expression must be a method invocation.", nameof(expression));

            if (expression.Body is not MethodCallExpression mce)
                throw new ArgumentException($"Expression must be of Type '{nameof(MethodCallExpression)}'.", nameof(expression));

            // HttpRequestOptions is not *really* valid as a value; move as likely a placement error on behalf of the consuming developer.
            if (value is Ceh.HttpRequestOptions ro && requestOptions == null)
            {
                value = null;
                requestOptions = ro;
            }

            // Attempts similar logic to: https://docs.microsoft.com/en-us/aspnet/web-api/overview/formats-and-model-binding/parameter-binding-in-aspnet-web-api
            var pis = mce.Method.GetParameters();
            var @params = new List<(string Name, object? Value)>();
            object? body = null;

            for (int i = 0; i < mce.Arguments.Count; i++)
            {
                var arg = mce.Arguments[i];
                var par = pis[i];
                if (par.Name == null)
                    continue;

                var ue = Expression.Convert(mce.Arguments[i], typeof(object));
                var le = Expression.Lambda<Func<object>>(ue);
                var val = le.Compile().Invoke();

                if (par.GetCustomAttribute<FromBodyAttribute>() != null)
                    body = val;
                else
                {
                    if (par.GetCustomAttribute<System.Web.Http.FromUriAttribute>() == null && par.GetCustomAttribute<FromQueryAttribute>() == null && par.ParameterType.IsClass && par.ParameterType != typeof(string))
                        body = val;
                    else
                        @params.Add((par.Name, val));
                }
            }

            if (value != null)
            {
                if (body != null)
                    throw new ArgumentException("A body value can not be explicity specified where the expression already contains a body value.", nameof(value));

                body = value;
            }

            var att = mce.Method.GetCustomAttributes<HttpMethodAttribute>(true)?.FirstOrDefault();
            if (att == null)
                throw new InvalidOperationException($"Operation {mce.Method.Name} does not have an {nameof(HttpMethodAttribute)} specified.");

            var uri = GetRequestUri(att.Template, @params);
            return await RunWithRequestAsync(new HttpMethod(att.HttpMethods.First()), uri, body, requestOptions, args).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets (infers) the Request URI.
        /// </summary>
        private static string GetRequestUri(string? template, List<(string Name, object? Value)> @params)
        {
            var type = typeof(TController);
            var atts = type.GetCustomAttributes(typeof(RouteAttribute), false);
            if (atts == null || atts.Length != 1)
                throw new InvalidOperationException($"Controller {type.Name} must have a single RouteAttribute specified.");

            var name = type.Name.EndsWith("Controller", StringComparison.OrdinalIgnoreCase) ? type.Name[0..^"Controller".Length] : type.Name;
            var route = (atts[0] as RouteAttribute)?.Template.Replace("[controller]", name) ?? "";
            if (!string.IsNullOrEmpty(template))
                route = $"{route}/{template}";

            var query = new List<KeyValuePair<string, string>>();

            foreach (var (Name, Value) in @params)
            {
                string? value = null;
                List<string>? list = null;
                if (Value != null && Value is not string && Value is IEnumerable el)
                {
                    list = new List<string>();
                    foreach (var item in el)
                    {
                        var iv = GetParamValueAsString(item);
                        if (iv != null)
                            list.Add(iv);
                    }
                }
                else
                    value = GetParamValueAsString(Value);

                var n = $"{{{Name}}}";
                if (route.Contains(n))
                    route = route.Replace(n, list == null ? value : list.FirstOrDefault());
                else
                {
                    if (list == null)
                    {
                        if (value != null)
                            query.Add(new KeyValuePair<string, string>(Name!, value));
                    }
                    else
                    {
                        foreach (var item in list)
                        {
                            query.Add(new KeyValuePair<string, string>(Name!, item));
                        }
                    }
                }
            }

            if (query.Count == 0)
                return route;

            var qs = new FormUrlEncodedContent(query);
            return route + "?" + qs.ReadAsStringAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Converts the value into its string representation.
        /// </summary>
        private static string? GetParamValueAsString(object? param)
        {
            string? val = null;
            if (param != null)
            {
                if (param is string str)
                    val = str;
                else if (param is DateTime dt)
                    val = dt.ToString("o", System.Globalization.CultureInfo.InvariantCulture);
                else if (param is IConvertible cv)
                    val = cv.ToString(System.Globalization.CultureInfo.InvariantCulture);
                else
                    val = param.ToString();
            }

            return val;
        }

        private class TypedHttpClient : Ceh.TypedHttpClientBase
        {
            /// <summary>
            /// Initialize a new instance of the class.
            /// </summary>
            public TypedHttpClient(HttpClient hc, IJsonSerializer js) : base(hc, js) { }

            /// <summary>
            /// Provides the primay send capability.
            /// </summary>
            public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string? requestUri, object? value, Ceh.HttpRequestOptions? requestOptions, Ceh.IHttpArg[] args)
                => (value == null)
                    ? await SendAsync(await CreateRequestAsync(method, requestUri ?? "", requestOptions, args), default).ConfigureAwait(false)
                    : await SendAsync(await CreateJsonRequestAsync(method, requestUri ?? "", value, requestOptions, args), default).ConfigureAwait(false);

            /// <inheritdoc/>
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Client.SendAsync(request, cancellationToken);
        }
    }
}