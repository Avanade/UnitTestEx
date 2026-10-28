// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Net.Http.Headers;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net.Http;
using UnitTestEx.Abstractions;
using UnitTestEx.Azure.Functions;

namespace UnitTestEx
{
    /// <summary>
    /// Provides the <b>UnitTestEx</b> extension methods.
    /// </summary>
    public static class ExtensionMethods
    {
        internal const string HttpMethodCheckName = "HttpTriggerTester_MethodCheck";
        internal const string HttpRouteCheckOptionName = "HttpTriggerTester_" + nameof(RouteCheckOption);
        internal const string HttpRouteComparisonName = "HttpTriggerTester_" + nameof(StringComparison);

        /// <summary>
        /// Creates a <see cref="WorkerServiceBusMessageActionsAssertor"/> as the <see cref="Microsoft.Azure.Functions.Worker.ServiceBusMessageActions"/> instance to enable test mock and assert verification.
        /// </summary>
        /// <returns>The <see cref="WorkerServiceBusMessageActionsAssertor"/>.</returns>
        /// <param name="tester">The <see cref="TesterBase"/>.</param>
        public static WorkerServiceBusMessageActionsAssertor CreateWorkerServiceBusMessageActions(this TesterBase tester) => new(tester.Implementor);

        /// <summary>
        /// Creates a new <see cref="HttpRequestData"/> (isolated worker) with <i>optional</i> <paramref name="body"/>; see <see cref="TesterBase.CreateHttpRequest(HttpMethod, string?, string?, string?, Action{HttpRequest}?)"/> for further details.
        /// </summary>
        /// <param name="tester">The <see cref="TesterBase"/>.</param>
        /// <param name="httpMethod">The <see cref="HttpMethod"/>.</param>
        /// <param name="requestUri">The request uri.</param>
        /// <param name="body">The optional body content.</param>
        /// <param name="contentType">The content type. Defaults to <see cref="System.Net.Mime.MediaTypeNames.Text.Plain"/>.</param>
        /// <param name="requestModifier">The optional underlying <see cref="HttpRequest"/> modifier.</param>
        /// <returns>The <see cref="HttpRequestData"/>.</returns>
        public static HttpRequestData CreateHttpRequestData(this TesterBase tester, HttpMethod httpMethod, [StringSyntax(StringSyntaxAttribute.Uri)] string? requestUri = null, string? body = null, string? contentType = null, Action<HttpRequest>? requestModifier = null)
            => tester.CreateHttpRequest(httpMethod, requestUri, body, contentType, requestModifier).ToHttpRequestData();

        /// <summary>
        /// Creates a new <see cref="HttpRequestData"/> (isolated worker) with the <paramref name="value"/> JSON serialized as content type of <see cref="System.Net.Mime.MediaTypeNames.Application.Json"/>.
        /// </summary>
        /// <param name="tester">The <see cref="TesterBase"/>.</param>
        /// <param name="httpMethod">The <see cref="HttpMethod"/>.</param>
        /// <param name="requestUri">The request uri.</param>
        /// <param name="value">The value to JSON serialize.</param>
        /// <param name="requestModifier">The optional underlying <see cref="HttpRequest"/> modifier.</param>
        /// <returns>The <see cref="HttpRequestData"/>.</returns>
        public static HttpRequestData CreateJsonHttpRequestData(this TesterBase tester, HttpMethod httpMethod, [StringSyntax(StringSyntaxAttribute.Uri)] string? requestUri, object? value, Action<HttpRequest>? requestModifier = null)
            => tester.CreateJsonHttpRequest(httpMethod, requestUri, value, requestModifier).ToHttpRequestData();

        /// <summary>
        /// Converts (wraps) the <see cref="HttpRequest"/> as an <see cref="HttpRequestData"/> (isolated worker); see <see cref="TestHttpRequestData"/>.
        /// </summary>
        /// <param name="httpRequest">The <see cref="HttpRequest"/>.</param>
        /// <param name="functionContext">The optional <see cref="FunctionContext"/>; defaults to a <see cref="TestFunctionContext"/> using the <see cref="HttpContext.RequestServices"/>.</param>
        /// <returns>The <see cref="HttpRequestData"/>.</returns>
        public static HttpRequestData ToHttpRequestData(this HttpRequest httpRequest, FunctionContext? functionContext = null)
        {
            ArgumentNullException.ThrowIfNull(httpRequest);
            return new TestHttpRequestData(httpRequest, functionContext ?? new TestFunctionContext(httpRequest.HttpContext.RequestServices ?? throw new ArgumentException($"The {nameof(HttpContext)}.{nameof(HttpContext.RequestServices)} must not be null.", nameof(httpRequest))));
        }

        /// <summary>
        /// Converts the <see cref="HttpResponseData"/> (isolated worker) to an equivalent <see cref="HttpResponseMessage"/> (status code, headers, cookies and body content) to enable assertions.
        /// </summary>
        /// <param name="response">The <see cref="HttpResponseData"/>.</param>
        /// <returns>The <see cref="HttpResponseMessage"/>.</returns>
        /// <remarks>The <see cref="HttpResponseData.Body"/> bytes are copied as-is (not re-serialized); where seekable the body is read from the start and its position is restored, otherwise it is read from its current position. The body stream is not disposed.
        /// <para>The <see cref="HttpResponseData.Cookies"/> are only available where using the <see cref="TestHttpCookies"/>; these are converted to corresponding '<c>Set-Cookie</c>' headers.</para></remarks>
        public static HttpResponseMessage ToHttpResponseMessage(this HttpResponseData response)
        {
            ArgumentNullException.ThrowIfNull(response);

            var hr = new HttpResponseMessage(response.StatusCode);

            byte[] bytes = [];
            var body = response.Body;
            if (body is not null && body.CanRead)
            {
                long? position = body.CanSeek ? body.Position : null;
                if (position.HasValue)
                    body.Position = 0;

                using var ms = new MemoryStream();
                body.CopyTo(ms);
                bytes = ms.ToArray();

                if (position.HasValue)
                    body.Position = position.Value;
            }

            hr.Content = new ByteArrayContent(bytes);

            if (response.Headers is not null)
            {
                foreach (var h in response.Headers)
                {
                    if (!hr.Headers.TryAddWithoutValidation(h.Key, h.Value))
                        hr.Content.Headers.TryAddWithoutValidation(h.Key, h.Value);
                }
            }

            if (response.Cookies is TestHttpCookies cookies)
            {
                foreach (var c in cookies.Items)
                    hr.Headers.TryAddWithoutValidation(HeaderNames.SetCookie, ToSetCookieHeaderValue(c).ToString());
            }

            return hr;
        }

        /// <summary>
        /// Converts the <see cref="IHttpCookie"/> to a <see cref="SetCookieHeaderValue"/>.
        /// </summary>
        private static SetCookieHeaderValue ToSetCookieHeaderValue(IHttpCookie cookie) => new(cookie.Name, cookie.Value)
        {
            Domain = cookie.Domain,
            Path = cookie.Path,
            Expires = cookie.Expires,
            MaxAge = cookie.MaxAge.HasValue ? TimeSpan.FromSeconds(cookie.MaxAge.Value) : null,
            Secure = cookie.Secure ?? false,
            HttpOnly = cookie.HttpOnly ?? false,
            SameSite = cookie.SameSite switch
            {
                SameSite.Lax => Microsoft.Net.Http.Headers.SameSiteMode.Lax,
                SameSite.Strict => Microsoft.Net.Http.Headers.SameSiteMode.Strict,
                SameSite.ExplicitNone => Microsoft.Net.Http.Headers.SameSiteMode.None,
                _ => Microsoft.Net.Http.Headers.SameSiteMode.Unspecified
            }
        };

        /// <summary>
        /// Sets the default that <i>no</i> check is performed to ensure that the <see cref="Microsoft.Azure.Functions.Worker.HttpTriggerAttribute.Methods"/> contains the <see cref="HttpRequest.Method"/> for the <see cref="HttpTriggerTester{TFunction}.WithNoMethodCheck"/>.
        /// </summary>
        /// <param name="setup">The <see cref="TestSetUp"/>.</param>
        public static TestSetUp WithNoMethodCheck(this TestSetUp setup)
        {
            setup.Properties[HttpMethodCheckName] = false;
            return setup;
        }

        /// <summary>
        /// Sets the default that a check is performed to ensure that the <see cref="Microsoft.Azure.Functions.Worker.HttpTriggerAttribute.Methods"/> contains the <see cref="HttpRequest.Method"/> for the <see cref="HttpTriggerTester{TFunction}.WithMethodCheck"/>.
        /// </summary>
        /// <param name="setup">The <see cref="TestSetUp"/>.</param>
        public static TestSetUp WithMethodCheck(this TestSetUp setup)
        {
            setup.Properties[HttpMethodCheckName] = true;
            return setup;
        }

        /// <summary>
        /// Sets the default <see cref="RouteCheckOption"/> to be <see cref="RouteCheckOption.None"/> for the <see cref="HttpTriggerTester{TFunction}.WithNoRouteCheck"/>.
        /// </summary>
        /// <param name="setup">The <see cref="TestSetUp"/>.</param>
        public static TestSetUp WithNoHttpRouteCheck(this TestSetUp setup) => WithHttpRouteCheck(setup, RouteCheckOption.None);

        /// <summary>
        /// Sets the default <see cref="RouteCheckOption"/> to be checked during execution for the <see cref="HttpTriggerTester{TFunction}.WithRouteCheck(RouteCheckOption, StringComparison?)"/>.
        /// </summary>
        /// <param name="setup">The <see cref="TestSetUp"/>.</param>
        /// <param name="option">The <see cref="RouteCheckOption"/>.</param>
        /// <param name="comparison">The <see cref="StringComparison"/>.</param>
        public static TestSetUp WithHttpRouteCheck(this TestSetUp setup, RouteCheckOption option, StringComparison? comparison = StringComparison.OrdinalIgnoreCase)
        {
            setup.Properties[HttpRouteCheckOptionName] = option;
            setup.Properties[HttpRouteComparisonName] = comparison;
            return setup;
        }

        /// <summary>
        /// Invokes the <see cref="HttpTriggerTester{TFunction}.WithNoMethodCheck"/> or <see cref="HttpTriggerTester{TFunction}.WithMethodCheck"/> method based on the <see cref="TestSetUp"/> <see cref="HttpMethodCheckName"/> property.
        /// </summary>
        /// <typeparam name="TFunction">The Azure Function <see cref="System.Type"/>.</typeparam>
        /// <param name="tester">The <see cref="HttpTriggerTester{TFunction}"/>.</param>
        /// <param name="setup">The <see cref="TestSetUp"/>.</param>
        internal static void SetHttpMethodCheck<TFunction>(this HttpTriggerTester<TFunction> tester, TestSetUp setup) where TFunction : class
        {
            if (!setup.Properties.TryGetValue(HttpMethodCheckName, out var check) || (bool)check! == true)
                tester.WithMethodCheck();
            else
                tester.WithNoMethodCheck();
        }

        /// <summary>
        /// Invokes the <see cref="HttpTriggerTester{TFunction}.WithRouteCheck"/> method to set the <see cref="RouteCheckOption"/> and <see cref="StringComparison"/> from the <see cref="TestSetUp"/>.
        /// </summary>
        /// <typeparam name="TFunction">The Azure Function <see cref="System.Type"/>.</typeparam>
        /// <param name="tester">The <see cref="HttpTriggerTester{TFunction}"/>.</param>
        /// <param name="setup">The <see cref="TestSetUp"/>.</param>
        internal static void SetHttpRouteCheck<TFunction>(this HttpTriggerTester<TFunction> tester, TestSetUp setup) where TFunction : class
            => tester.WithRouteCheck(
                setup.Properties.TryGetValue(HttpRouteCheckOptionName, out var option) ? (RouteCheckOption)option! : RouteCheckOption.PathAndQuery,
                setup.Properties.TryGetValue(HttpRouteComparisonName, out var comparison) ? (StringComparison)comparison! : StringComparison.OrdinalIgnoreCase);
    }
}