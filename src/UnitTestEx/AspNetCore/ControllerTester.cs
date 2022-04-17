// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using CoreEx.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging;
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
        /// Provides the HTTP request body option.
        /// </summary>
        private enum BodyOption
        {
            None,
            Content,
            Value
        }

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
        /// Runs the controller using an <see cref="HttpRequestMessage"/> with no body.
        /// </summary>
        /// <param name="httpMethod">The <see cref="HttpMethod"/></param>
        /// <param name="requestUri">The string that represents the request <see cref="Uri"/>.</param>
        /// <param name="requestOptions">The optional <see cref="Ceh.HttpRequestOptions"/>.</param>
        /// <returns>An <see cref="HttpResponseMessageAssertor"/>.</returns>
        public HttpResponseMessageAssertor RunHttpRequest(HttpMethod httpMethod, string? requestUri, Ceh.HttpRequestOptions? requestOptions = null)
            => RunJsonHttpRequestAsync(httpMethod, requestUri, requestOptions).GetAwaiter().GetResult();

        /// <summary>
        /// Runs the controller using an <see cref="HttpRequestMessage"/> with <i>optional</i> <paramref name="body"/> (defaults <c>Content-Type</c> to <see cref="MediaTypeNames.Text.Plain"/>).
        /// </summary>
        /// <param name="httpMethod">The <see cref="HttpMethod"/></param>
        /// <param name="requestUri">The string that represents the request <see cref="Uri"/>.</param>
        /// <param name="body">The optional body content.</param>
        /// <param name="contentType">The content type. Defaults to <see cref="MediaTypeNames.Text.Plain"/>.</param>
        /// <returns>An <see cref="HttpResponseMessageAssertor"/>.</returns>
        public HttpResponseMessageAssertor RunHttpRequest(HttpMethod httpMethod, string? requestUri, string? body = null, string? contentType = MediaTypeNames.Text.Plain)
            => RunHttpRequestAsync(httpMethod, requestUri, body, null, contentType).GetAwaiter().GetResult();

        /// <summary>
        /// Runs the controller using an <see cref="HttpRequestMessage"/> with <i>optional</i> <paramref name="body"/> (defaults <c>Content-Type</c> to <see cref="MediaTypeNames.Text.Plain"/>).
        /// </summary>
        /// <param name="httpMethod">The <see cref="HttpMethod"/></param>
        /// <param name="requestUri">The string that represents the request <see cref="Uri"/>.</param>
        /// <param name="body">The optional body content.</param>
        /// <param name="requestOptions">The optional <see cref="Ceh.HttpRequestOptions"/>.</param>
        /// <param name="contentType">The content type. Defaults to <see cref="MediaTypeNames.Text.Plain"/>.</param>
        /// <returns>An <see cref="HttpResponseMessageAssertor"/>.</returns>
        public HttpResponseMessageAssertor RunHttpRequest(HttpMethod httpMethod, string? requestUri, string? body = null, Ceh.HttpRequestOptions? requestOptions = null, string? contentType = MediaTypeNames.Text.Plain)
            => RunHttpRequestAsync(httpMethod, requestUri, body, requestOptions, contentType).GetAwaiter().GetResult();

        /// <summary>
        /// Runs the controller using an <see cref="HttpRequestMessage"/> with no body.
        /// </summary>
        /// <param name="httpMethod">The <see cref="HttpMethod"/></param>
        /// <param name="requestUri">The string that represents the request <see cref="Uri"/>.</param>
        /// <param name="requestOptions">The optional <see cref="Ceh.HttpRequestOptions"/>.</param>
        /// <returns>An <see cref="HttpResponseMessageAssertor"/>.</returns>
        public Task<HttpResponseMessageAssertor> RunHttpRequestAsync(HttpMethod httpMethod, string? requestUri, Ceh.HttpRequestOptions? requestOptions = null)
            => RunHttpRequestInternalAsync(httpMethod, requestUri, false, null, requestOptions);

        /// <summary>
        /// Runs the controller using an <see cref="HttpRequestMessage"/> with <i>optional</i> <paramref name="body"/> (defaults <c>Content-Type</c> to <see cref="MediaTypeNames.Text.Plain"/>).
        /// </summary>
        /// <param name="httpMethod">The <see cref="HttpMethod"/></param>
        /// <param name="requestUri">The string that represents the request <see cref="Uri"/>.</param>
        /// <param name="body">The optional body content.</param>
        /// <param name="contentType">The content type. Defaults to <see cref="MediaTypeNames.Text.Plain"/>.</param>
        /// <returns>An <see cref="HttpResponseMessageAssertor"/>.</returns>
        public Task<HttpResponseMessageAssertor> RunHttpRequestAsync(HttpMethod httpMethod, string? requestUri, string? body = null, string? contentType = MediaTypeNames.Text.Plain)
            => RunHttpRequestInternalAsync(httpMethod, requestUri, true, body, null, contentType);

        /// <summary>
        /// Runs the controller using an <see cref="HttpRequestMessage"/> with <i>optional</i> <paramref name="body"/> (defaults <c>Content-Type</c> to <see cref="MediaTypeNames.Text.Plain"/>).
        /// </summary>
        /// <param name="httpMethod">The <see cref="HttpMethod"/></param>
        /// <param name="requestUri">The string that represents the request <see cref="Uri"/>.</param>
        /// <param name="body">The optional body content.</param>
        /// <param name="requestOptions">The optional <see cref="Ceh.HttpRequestOptions"/>.</param>
        /// <param name="contentType">The content type. Defaults to <see cref="MediaTypeNames.Text.Plain"/>.</param>
        /// <returns>An <see cref="HttpResponseMessageAssertor"/>.</returns>
        public Task<HttpResponseMessageAssertor> RunHttpRequestAsync(HttpMethod httpMethod, string? requestUri, string? body = null, Ceh.HttpRequestOptions? requestOptions = null, string? contentType = MediaTypeNames.Text.Plain)
            => RunHttpRequestInternalAsync(httpMethod, requestUri, true, body, requestOptions, contentType);

        /// <summary>
        /// Runs the controller using the passed parameters.
        /// </summary>
        private async Task<HttpResponseMessageAssertor> RunHttpRequestInternalAsync(HttpMethod httpMethod, string? requestUri, bool hasBody, string? body = null, Ceh.HttpRequestOptions? requestOptions = null, string? contentType = MediaTypeNames.Text.Plain)
        {
            if (body != null && httpMethod == HttpMethod.Get)
                 _implementor.CreateLogger("ControllerTester").LogWarning("A payload within a GET request message has no defined semantics; sending a payload body on a GET request might cause some existing implementations to reject the request (see https://www.rfc-editor.org/rfc/rfc7231).");

            var tc = new TypedHttpClient(_testServer.CreateClient(), _jsonSerializer, req => LogRequest(req));
            var sw = Stopwatch.StartNew();
            var res = await tc.SendContentAsync(httpMethod, requestUri, hasBody, body, contentType, requestOptions).ConfigureAwait(false);

            sw.Stop();
            LogResponse(res, sw);

            return new HttpResponseMessageAssertor(res, _implementor, _jsonSerializer);
        }

        /// <summary>
        /// Runs the controller using an <see cref="HttpRequestMessage"/>.
        /// </summary>
        /// <param name="httpMethod">The <see cref="HttpMethod"/></param>
        /// <param name="requestUri">The string that represents the request <see cref="Uri"/>.</param>
        /// <param name="value">The request body value.</param>
        /// <param name="requestOptions">The optional <see cref="Ceh.HttpRequestOptions"/>.</param>
        /// <returns>An <see cref="HttpResponseMessageAssertor"/>.</returns>
        public async Task<HttpResponseMessageAssertor> RunJsonHttpRequestAsync(HttpMethod httpMethod, string? requestUri, object? value, Ceh.HttpRequestOptions? requestOptions = null)
        {
            if (value != null && httpMethod == HttpMethod.Get)
                _implementor.CreateLogger("ControllerTester").LogWarning("A payload within a GET request message has no defined semantics; sending a payload body on a GET request might cause some existing implementations to reject the request (see https://www.rfc-editor.org/rfc/rfc7231).");

            var tc = new TypedHttpClient(_testServer.CreateClient(), _jsonSerializer, req => LogRequest(req));
            var sw = Stopwatch.StartNew();
            var res = await tc.SendAsync(httpMethod, requestUri, value, requestOptions).ConfigureAwait(false);

            sw.Stop();
            await Task.Delay(0).ConfigureAwait(false);
            LogResponse(res, sw);

            return new HttpResponseMessageAssertor(res, _implementor, _jsonSerializer);
        }

        /// <summary>
        /// Log the request to the output.
        /// </summary>
        private void LogRequest(HttpRequestMessage req)
        {
            _implementor.WriteLine("");
            _implementor.WriteLine("API TESTER...");
            _implementor.WriteLine("");
            _implementor.WriteLine("REQUEST >");
            _implementor.WriteLine($"Request: {req.Method} {req.RequestUri}");
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

            object? jo = null;
            if (req.Content != null)
            {
                string? content = null;

                // Parse out the content.
                try
                {
                    content = req.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    if (!string.IsNullOrEmpty(content))
                        jo = _jsonSerializer.Deserialize(content);
                }
                catch (Exception) { /* This is being swallowed by design. */ }

                _implementor.WriteLine($"Content: [{req.Content?.Headers?.ContentType?.MediaType ?? "None"}]");
                _implementor.WriteLine(jo == null ? content : _jsonSerializer.Serialize(jo, JsonWriteFormat.Indented));
            }

            _implementor.WriteLine("");
            _implementor.WriteLine("LOGGING >");
        }

        /// <summary>
        /// Log the response to the output.
        /// </summary>
        private void LogResponse(HttpResponseMessage res, Stopwatch sw)
        {
            if (res.RequestMessage == null)
                return;

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

            object? jo = null;
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
        /// <returns>A <see cref="HttpResponseMessageAssertor"/>.</returns>
        public HttpResponseMessageAssertor Run<TResult>(Expression<Func<TController, TResult>> expression, Ceh.HttpRequestOptions? requestOptions = null)
            => RunAsync(expression, requestOptions).GetAwaiter().GetResult();

        /// <summary>
        /// Runs the controller using an <see cref="HttpRequestMessage"/> inferring the <see cref="HttpMethod"/>, operation name and request from the <paramref name="expression"/>.
        /// </summary>
        /// <typeparam name="TResult">The result <see cref="Type"/>.</typeparam>
        /// <param name="expression">The controller operation invocation expression.</param>
        /// <param name="requestOptions">The optional <see cref="Ceh.HttpRequestOptions"/>.</param>
        /// <param name="value">The optional body value where not explicitly passed via the <paramref name="expression"/>.</param>
        /// <returns>A <see cref="HttpResponseMessageAssertor"/>.</returns>
        public HttpResponseMessageAssertor Run<TResult>(Expression<Func<TController, TResult>> expression, object? value, Ceh.HttpRequestOptions? requestOptions = null)
            => RunAsync(expression, value, requestOptions).GetAwaiter().GetResult();

        /// <summary>
        /// Runs the controller using an <see cref="HttpRequestMessage"/> inferring the <see cref="HttpMethod"/>, operation name and request from the <paramref name="expression"/>.
        /// </summary>
        /// <typeparam name="TResult">The result <see cref="Type"/>.</typeparam>
        /// <param name="expression">The controller operation invocation expression.</param>
        /// <param name="requestOptions">The optional <see cref="Ceh.HttpRequestOptions"/>.</param>
        /// <returns>A <see cref="HttpResponseMessageAssertor"/>.</returns>
        public Task<HttpResponseMessageAssertor> RunAsync<TResult>(Expression<Func<TController, TResult>> expression, Ceh.HttpRequestOptions? requestOptions = null)
            => RunInternalAsync(expression, BodyOption.None, null, MediaTypeNames.Application.Json, requestOptions);

        /// <summary>
        /// Runs the controller using an <see cref="HttpRequestMessage"/> inferring the <see cref="HttpMethod"/>, operation name and request from the <paramref name="expression"/>.
        /// </summary>
        /// <typeparam name="TResult">The result <see cref="Type"/>.</typeparam>
        /// <param name="expression">The controller operation invocation expression.</param>
        /// <param name="value">The body value to serialized as JSON.</param>
        /// <param name="requestOptions">The optional <see cref="Ceh.HttpRequestOptions"/>.</param>
        /// <returns>A <see cref="HttpResponseMessageAssertor"/>.</returns>
        public Task<HttpResponseMessageAssertor> RunAsync<TResult>(Expression<Func<TController, TResult>> expression, object? value, Ceh.HttpRequestOptions? requestOptions = null)
            => RunInternalAsync(expression, BodyOption.Value, value, MediaTypeNames.Application.Json, requestOptions);

        /// <summary>
        /// Runs the controller using an <see cref="HttpRequestMessage"/> inferring the <see cref="HttpMethod"/>, operation name and request from the <paramref name="expression"/>.
        /// </summary>
        /// <typeparam name="TResult">The result <see cref="Type"/>.</typeparam>
        /// <param name="expression">The controller operation invocation expression.</param>
        /// <param name="content">The body content.</param>
        /// <param name="contentType">The body content type. Defaults to <see cref="MediaTypeNames.Text.Plain"/>.</param>
        /// <returns>A <see cref="HttpResponseMessageAssertor"/>.</returns>
        public HttpResponseMessageAssertor RunContent<TResult>(Expression<Func<TController, TResult>> expression, string? content, string? contentType = MediaTypeNames.Text.Plain)
            => RunContentAsync(expression, content, contentType).GetAwaiter().GetResult();

        /// <summary>
        /// Runs the controller using an <see cref="HttpRequestMessage"/> inferring the <see cref="HttpMethod"/>, operation name and request from the <paramref name="expression"/>.
        /// </summary>
        /// <typeparam name="TResult">The result <see cref="Type"/>.</typeparam>
        /// <param name="expression">The controller operation invocation expression.</param>
        /// <param name="content">The body content.</param>
        /// <param name="contentType">The body content type. Defaults to <see cref="MediaTypeNames.Text.Plain"/>.</param>
        /// <param name="requestOptions">The optional <see cref="Ceh.HttpRequestOptions"/>.</param>
        /// <returns>A <see cref="HttpResponseMessageAssertor"/>.</returns>
        public HttpResponseMessageAssertor RunContent<TResult>(Expression<Func<TController, TResult>> expression, string? content, Ceh.HttpRequestOptions? requestOptions = null, string? contentType = MediaTypeNames.Text.Plain)
            => RunContentAsync(expression, content, requestOptions, contentType).GetAwaiter().GetResult();

        /// <summary>
        /// Runs the controller using an <see cref="HttpRequestMessage"/> inferring the <see cref="HttpMethod"/>, operation name and request from the <paramref name="expression"/>.
        /// </summary>
        /// <typeparam name="TResult">The result <see cref="Type"/>.</typeparam>
        /// <param name="expression">The controller operation invocation expression.</param>
        /// <param name="content">The body content.</param>
        /// <param name="contentType">The body content type. Defaults to <see cref="MediaTypeNames.Text.Plain"/>.</param>
        /// <returns>A <see cref="HttpResponseMessageAssertor"/>.</returns>
        public Task<HttpResponseMessageAssertor> RunContentAsync<TResult>(Expression<Func<TController, TResult>> expression, string? content, string? contentType = MediaTypeNames.Text.Plain)
            => RunInternalAsync(expression, BodyOption.Content, content, contentType ?? MediaTypeNames.Text.Plain, null);

        /// <summary>
        /// Runs the controller using an <see cref="HttpRequestMessage"/> inferring the <see cref="HttpMethod"/>, operation name and request from the <paramref name="expression"/>.
        /// </summary>
        /// <typeparam name="TResult">The result <see cref="Type"/>.</typeparam>
        /// <param name="expression">The controller operation invocation expression.</param>
        /// <param name="content">The body content.</param>
        /// <param name="contentType">The body content type. Defaults to <see cref="MediaTypeNames.Text.Plain"/>.</param>
        /// <param name="requestOptions">The optional <see cref="Ceh.HttpRequestOptions"/>.</param>
        /// <returns>A <see cref="HttpResponseMessageAssertor"/>.</returns>
        public Task<HttpResponseMessageAssertor> RunContentAsync<TResult>(Expression<Func<TController, TResult>> expression, string? content, Ceh.HttpRequestOptions? requestOptions = null, string? contentType = MediaTypeNames.Text.Plain)
            => RunInternalAsync(expression, BodyOption.Content, content, contentType ?? MediaTypeNames.Text.Plain, requestOptions);

        /// <summary>
        /// Runs the controller using the passed parameters.
        /// </summary>
        private async Task<HttpResponseMessageAssertor> RunInternalAsync<TResult>(Expression<Func<TController, TResult>> expression, BodyOption bodyOption, object? value, string? contentType, Ceh.HttpRequestOptions? requestOptions = null)
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

            if (bodyOption != BodyOption.None)
            {
                if (body != null)
                    throw new ArgumentException("A Body can not be explicity specified where the expression already contains a body value.", nameof(value));

                body = value;
            }
            else if (body != null && bodyOption == BodyOption.None)
                bodyOption = BodyOption.Value;

            var att = mce.Method.GetCustomAttributes<HttpMethodAttribute>(true)?.FirstOrDefault();
            if (att == null)
                throw new InvalidOperationException($"Operation {mce.Method.Name} does not have an {nameof(HttpMethodAttribute)} specified.");

            var uri = GetRequestUri(att.Template, @params);
            return bodyOption switch
            {
                BodyOption.Content => await RunHttpRequestAsync(new HttpMethod(att.HttpMethods.First()), uri, (string?)body, requestOptions, contentType).ConfigureAwait(false),
                BodyOption.Value => await RunJsonHttpRequestAsync(new HttpMethod(att.HttpMethods.First()), uri, body, requestOptions).ConfigureAwait(false),
                _ => await RunHttpRequestAsync(new HttpMethod(att.HttpMethods.First()), uri, requestOptions).ConfigureAwait(false)
            };
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

        /// <summary>
        /// Provides the requisite HttpClient sending capabilities.
        /// </summary>
        private class TypedHttpClient : Ceh.TypedHttpClientBase
        {
            private readonly Action<HttpRequestMessage> _onBeforeRequest;

            /// <summary>
            /// Initialize a new instance of the class.
            /// </summary>
            public TypedHttpClient(HttpClient hc, IJsonSerializer js, Action<HttpRequestMessage> onBeforeRequest) : base(hc, js) => _onBeforeRequest = onBeforeRequest;

            /// <summary>
            /// Provides the content send capability.
            /// </summary>
            public async Task<HttpResponseMessage> SendContentAsync(HttpMethod method, string? requestUri, bool hasContent, string? content, string? contentType, Ceh.HttpRequestOptions? requestOptions, params Ceh.IHttpArg[] args)
                => hasContent
                    ? await SendAsync(await CreateContentRequestAsync(method, requestUri ?? "", new StringContent(content ?? string.Empty, Encoding.UTF8, contentType ?? MediaTypeNames.Text.Plain), requestOptions, args).ConfigureAwait(false), default).ConfigureAwait(false)
                    : await SendAsync(await CreateRequestAsync(method, requestUri ?? "", requestOptions, args).ConfigureAwait(false), default).ConfigureAwait(false);

            /// <summary>
            /// Provides the serialized JSON send capability.
            /// </summary>
            public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string? requestUri, object? value, Ceh.HttpRequestOptions? requestOptions, params Ceh.IHttpArg[] args)
                => await SendAsync(await CreateJsonRequestAsync(method, requestUri ?? "", value, requestOptions, args).ConfigureAwait(false), default).ConfigureAwait(false);

            /// <inheritdoc/>
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                _onBeforeRequest(request);
                return Client.SendAsync(request, cancellationToken);
            }
        }
    }
}