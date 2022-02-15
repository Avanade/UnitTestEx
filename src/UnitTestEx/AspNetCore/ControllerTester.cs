// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.TestHost;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnitTestEx.Abstractions;
using UnitTestEx.Assertors;

namespace UnitTestEx.AspNetCore
{
    /// <summary>
    /// Enables the testing of a <see cref="ControllerBase"/> operation.
    /// </summary>
    /// <typeparam name="TController">The <see cref="ControllerBase"/> <see cref="Type"/>.</typeparam>
    public class ControllerTester<TController> where TController : ControllerBase
    {
        private readonly TestServer _testServer;
        private readonly TestFrameworkImplementor _implementor;

        /// <summary>
        /// Initializes a new <see cref="ControllerTester{TController}"/> class.
        /// </summary>
        /// <param name="testServer">The <see cref="TestServer"/>.</param>
        /// <param name="implementor">The <see cref="TestFrameworkImplementor"/>.</param>
        internal ControllerTester(TestServer testServer, TestFrameworkImplementor implementor)
        {
            _testServer = testServer;
            _implementor = implementor;
        }

        /// <summary>
        /// Runs the controller using an <see cref="HttpRequestMessage"/>.
        /// </summary>
        /// <param name="httpMethod">The <see cref="HttpMethod"/></param>
        /// <param name="requestUri">The string that represents the request <see cref="Uri"/>.</param>
        /// <param name="value">The optional request body value.</param>
        /// <returns>An <see cref="HttpResponseMessageAssertor"/>.</returns>
        public HttpResponseMessageAssertor Run(HttpMethod httpMethod, string? requestUri, object? value = null) => RunAsync(httpMethod, requestUri, value).GetAwaiter().GetResult();

        /// <summary>
        /// Runs the controller using an <see cref="HttpRequestMessage"/>.
        /// </summary>
        /// <param name="httpMethod">The <see cref="HttpMethod"/></param>
        /// <param name="requestUri">The string that represents the request <see cref="Uri"/>.</param>
        /// <param name="value">The optional request body value.</param>
        /// <returns>An <see cref="HttpResponseMessageAssertor"/>.</returns>
        public async Task<HttpResponseMessageAssertor> RunAsync(HttpMethod httpMethod, string? requestUri, object? value = null)
        {
            var req = new HttpRequestMessage(httpMethod, requestUri);
            if (value != null)
                req.Content = CreateJsonContentFromValue(value);

            var hc = _testServer.CreateClient();

            var sw = Stopwatch.StartNew();
            var res = await hc.SendAsync(req).ConfigureAwait(false);

            sw.Stop();
            LogOutput(res, sw);

            return new HttpResponseMessageAssertor(res, _implementor);
        }

        /// <summary>
        /// Log the request/response to the output.
        /// </summary>
        private void LogOutput(HttpResponseMessage res, Stopwatch sw)
        {
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

            JToken? json = null;
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
                    json = JToken.Parse(res.RequestMessage.Content.ReadAsStringAsync().Result);
                }
                catch (Exception) { /* This is being swallowed by design. */ }

                _implementor.WriteLine($"Content: [{res.RequestMessage.Content?.Headers?.ContentType?.MediaType ?? "None"}]");
                _implementor.WriteLine(json == null ? res.RequestMessage.Content?.ToString() : json.ToString());
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

            json = null;
            var content = res.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (!string.IsNullOrEmpty(content) && res.Content?.Headers?.ContentType?.MediaType == MediaTypeNames.Application.Json)
            {
                try
                {
                    json = JToken.Parse(content);
                }
                catch (Exception) { /* This is being swallowed by design. */ }
            }

            var txt = $"Content: [{res.Content?.Headers?.ContentType?.MediaType ?? "none"}]";
            if (json != null)
            {
                _implementor.WriteLine(txt);
                _implementor.WriteLine(json.ToString());
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
        /// <returns>A <see cref="HttpResponseMessageAssertor"/>.</returns>
        public HttpResponseMessageAssertor Run<TResult>(Expression<Func<TController, TResult>> expression) => RunAsync(expression).GetAwaiter().GetResult();

        /// <summary>
        /// Runs the controller using an <see cref="HttpRequestMessage"/> inferring the <see cref="HttpMethod"/>, operation name and request from the <paramref name="expression"/>.
        /// </summary>
        /// <typeparam name="TResult">The result <see cref="Type"/>.</typeparam>
        /// <param name="expression">The controller operation invocation expression.</param>
        /// <returns>A <see cref="HttpResponseMessageAssertor"/>.</returns>
        public async Task<HttpResponseMessageAssertor> RunAsync<TResult>(Expression<Func<TController, TResult>> expression)
        {
            if (expression.Body.NodeType != ExpressionType.Call)
                throw new ArgumentException("Expression must be a method invocation.", nameof(expression));

            if (!(expression.Body is MethodCallExpression mce))
                throw new ArgumentException($"Expression must be of Type '{nameof(MethodCallExpression)}'.", nameof(expression));

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
                    @params.Add((par.Name, val));
            }

            var att = mce.Method.GetCustomAttributes<HttpMethodAttribute>(true)?.FirstOrDefault();
            if (att == null)
                throw new InvalidOperationException($"Operation {mce.Method.Name} does not have an {nameof(HttpMethodAttribute)} specified.");

            var uri = GetRequestUri(new HttpMethod(att.HttpMethods.First()), att.Template, @params);
            return await RunAsync(new HttpMethod(att.HttpMethods.First()), uri, body).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets (infers) the Request URI.
        /// </summary>
        private static string GetRequestUri(HttpMethod httpMethod, string? template, List<(string Name, object? Value)> @params)
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
                if (Value != null && !(Value is string) && Value is IEnumerable el)
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
        /// Gets (infers) the Request URI.
        /// </summary>
        private string GetRequestUri(HttpMethod httpMethod, string operationName, object? request = null)
        {
            var type = typeof(TController);
            var atts = type.GetCustomAttributes(typeof(RouteAttribute), false);
            if (atts == null || atts.Length != 1)
                throw new InvalidOperationException($"Controller {type.Name} must have a single RouteAttribute specified.");

            var name = type.Name.EndsWith("Controller", StringComparison.OrdinalIgnoreCase) ? type.Name[0..^"Controller".Length] : type.Name;
            var route = (atts[0] as RouteAttribute)?.Template.Replace("[controller]", name) + "/" + operationName;

            if (httpMethod != HttpMethod.Get || request == null)
                return route;

            var dict = new Dictionary<string, string>();
            GetPathsAndValues(dict, JToken.FromObject(request));
            var query = new System.Net.Http.FormUrlEncodedContent(dict);

            return route + "?" + query.ReadAsStringAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Recursively get all the paths and values.
        /// </summary>
        private void GetPathsAndValues(IDictionary<string, string> dict, JToken json)
        {
            if (json.HasValues)
            {
                foreach (var ct in json.Children().ToList())
                {
                    GetPathsAndValues(dict, ct);
                }
            }
            else
            {
                if (json is JValue jv)
                {
                    dict.Add(jv.Path, jv.Type == JTokenType.Date
                        ? jv.ToString("o", System.Globalization.CultureInfo.InvariantCulture)
                        : jv.ToString(System.Globalization.CultureInfo.InvariantCulture));
                }
            }
        }

        /// <summary>
        /// Create the content by JSON serializing the request value.
        /// </summary>
        private static StringContent? CreateJsonContentFromValue(object value)
        {
            if (value == null)
                return null;

            var content = new StringContent(JsonConvert.SerializeObject(value));
            content.Headers.ContentType = MediaTypeHeaderValue.Parse(MediaTypeNames.Application.Json);
            return content;
        }
    }
}