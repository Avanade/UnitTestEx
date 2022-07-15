// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using CoreEx.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.TestHost;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Net.Mime;
using System.Reflection;
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
    public class ControllerTester<TController> : HttpTesterBase<ControllerTester<TController>> where TController : ControllerBase
    {
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
        /// <param name="owner">The owning <see cref="TesterBase"/>.</param>
        /// <param name="testServer">The <see cref="TestServer"/>.</param>
        internal ControllerTester(TesterBase owner, TestServer testServer) : base(owner, testServer) { }

        /// <summary>
        /// Runs the controller using an <see cref="HttpRequestMessage"/> inferring the <see cref="HttpMethod"/>, operation name and request from the <paramref name="expression"/>.
        /// </summary>
        /// <typeparam name="TResult">The result value <see cref="Type"/>.</typeparam>
        /// <param name="expression">The controller operation invocation expression.</param>
        /// <param name="requestOptions">The optional <see cref="Ceh.HttpRequestOptions"/>.</param>
        /// <returns>A <see cref="HttpResponseMessageAssertor"/>.</returns>
        public HttpResponseMessageAssertor Run<TResult>(Expression<Func<TController, TResult>> expression, Ceh.HttpRequestOptions? requestOptions = null)
            => RunAsync(expression, requestOptions).GetAwaiter().GetResult();

        /// <summary>
        /// Runs the controller using an <see cref="HttpRequestMessage"/> inferring the <see cref="HttpMethod"/>, operation name and request from the <paramref name="expression"/>.
        /// </summary>
        /// <typeparam name="TResult">The result value <see cref="Type"/>.</typeparam>
        /// <param name="expression">The controller operation invocation expression.</param>
        /// <param name="requestOptions">The optional <see cref="Ceh.HttpRequestOptions"/>.</param>
        /// <param name="value">The optional body value where not explicitly passed via the <paramref name="expression"/>.</param>
        /// <returns>A <see cref="HttpResponseMessageAssertor"/>.</returns>
        public HttpResponseMessageAssertor Run<TResult>(Expression<Func<TController, TResult>> expression, object? value, Ceh.HttpRequestOptions? requestOptions = null)
            => RunAsync(expression, value, requestOptions).GetAwaiter().GetResult();

        /// <summary>
        /// Runs the controller using an <see cref="HttpRequestMessage"/> inferring the <see cref="HttpMethod"/>, operation name and request from the <paramref name="expression"/>.
        /// </summary>
        /// <typeparam name="TResult">The result value <see cref="Type"/>.</typeparam>
        /// <param name="expression">The controller operation invocation expression.</param>
        /// <param name="requestOptions">The optional <see cref="Ceh.HttpRequestOptions"/>.</param>
        /// <returns>A <see cref="HttpResponseMessageAssertor"/>.</returns>
        public Task<HttpResponseMessageAssertor> RunAsync<TResult>(Expression<Func<TController, TResult>> expression, Ceh.HttpRequestOptions? requestOptions = null)
            => RunInternalAsync(expression, BodyOption.None, null, MediaTypeNames.Application.Json, requestOptions);

        /// <summary>
        /// Runs the controller using an <see cref="HttpRequestMessage"/> inferring the <see cref="HttpMethod"/>, operation name and request from the <paramref name="expression"/>.
        /// </summary>
        /// <typeparam name="TResult">The result value <see cref="Type"/>.</typeparam>
        /// <param name="expression">The controller operation invocation expression.</param>
        /// <param name="value">The body value to serialized as JSON.</param>
        /// <param name="requestOptions">The optional <see cref="Ceh.HttpRequestOptions"/>.</param>
        /// <returns>A <see cref="HttpResponseMessageAssertor"/>.</returns>
        public Task<HttpResponseMessageAssertor> RunAsync<TResult>(Expression<Func<TController, TResult>> expression, object? value, Ceh.HttpRequestOptions? requestOptions = null)
            => RunInternalAsync(expression, BodyOption.Value, value, MediaTypeNames.Application.Json, requestOptions);

        /// <summary>
        /// Runs the controller using an <see cref="HttpRequestMessage"/> inferring the <see cref="HttpMethod"/>, operation name and request from the <paramref name="expression"/>.
        /// </summary>
        /// <typeparam name="TResult">The result value <see cref="Type"/>.</typeparam>
        /// <param name="expression">The controller operation invocation expression.</param>
        /// <param name="content">The body content.</param>
        /// <param name="contentType">The body content type. Defaults to <see cref="MediaTypeNames.Text.Plain"/>.</param>
        /// <returns>A <see cref="HttpResponseMessageAssertor"/>.</returns>
        public HttpResponseMessageAssertor RunContent<TResult>(Expression<Func<TController, TResult>> expression, string? content, string? contentType = MediaTypeNames.Text.Plain)
            => RunContentAsync(expression, content, contentType).GetAwaiter().GetResult();

        /// <summary>
        /// Runs the controller using an <see cref="HttpRequestMessage"/> inferring the <see cref="HttpMethod"/>, operation name and request from the <paramref name="expression"/>.
        /// </summary>
        /// <typeparam name="TResult">The result value <see cref="Type"/>.</typeparam>
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
        /// <typeparam name="TResult">The result value <see cref="Type"/>.</typeparam>
        /// <param name="expression">The controller operation invocation expression.</param>
        /// <param name="content">The body content.</param>
        /// <param name="contentType">The body content type. Defaults to <see cref="MediaTypeNames.Text.Plain"/>.</param>
        /// <returns>A <see cref="HttpResponseMessageAssertor"/>.</returns>
        public Task<HttpResponseMessageAssertor> RunContentAsync<TResult>(Expression<Func<TController, TResult>> expression, string? content, string? contentType = MediaTypeNames.Text.Plain)
            => RunInternalAsync(expression, BodyOption.Content, content, contentType ?? MediaTypeNames.Text.Plain, null);

        /// <summary>
        /// Runs the controller using an <see cref="HttpRequestMessage"/> inferring the <see cref="HttpMethod"/>, operation name and request from the <paramref name="expression"/>.
        /// </summary>
        /// <typeparam name="TResult">The result value <see cref="Type"/>.</typeparam>
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
            var mce = Hosting.HostTesterBase<object>.MethodCallExpressionValidate(expression);

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
                BodyOption.Content => await SendAsync(new HttpMethod(att.HttpMethods.First()), uri, (string?)body, contentType, requestOptions).ConfigureAwait(false),
                BodyOption.Value => await SendAsync(new HttpMethod(att.HttpMethods.First()), uri, body, requestOptions).ConfigureAwait(false),
                _ => await SendAsync(new HttpMethod(att.HttpMethods.First()), uri, requestOptions).ConfigureAwait(false)
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
    }
}