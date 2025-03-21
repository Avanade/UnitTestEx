﻿// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Mime;
using System.Reflection;
using System.Text.Json;
using UnitTestEx.Abstractions;

namespace UnitTestEx.Assertors
{
    /// <summary>
    /// Represents the <see cref="IActionResult"/> test assert helper.
    /// </summary>
    /// <param name="owner">The owning <see cref="TesterBase"/>.</param>
    /// <param name="result">The <see cref="IActionResult"/>.</param>
    /// <param name="exception">The <see cref="Exception"/> (if any).</param>
    public class ActionResultAssertor(TesterBase owner, IActionResult result, Exception? exception) : AssertorBase<ActionResultAssertor>(owner, exception)
    {
        /// <summary>
        /// Gets the <see cref="IActionResult"/>.
        /// </summary>
        public IActionResult Result { get; } = result;

        /// <summary>
        /// Assert the <see cref="Result"/> <see cref="Type"/>.
        /// </summary>
        /// <typeparam name="TResult">The expected <see cref="IActionResult"/> <see cref="Type"/>.</typeparam>
        /// <returns>The <see cref="ActionResultAssertor"/> to support fluent-style method-chaining.</returns>
        public ActionResultAssertor AssertResultType<TResult>() where TResult : IActionResult
        {
            AssertSuccess();
            if (Result is not TResult)
                Implementor.AssertFail($"Result Type '{Result.GetType().Name}' is not expected '{typeof(TResult).Name}'.");

            return this;
        }

        /// <summary>
        /// Asserts that the <see cref="IStatusCodeActionResult.StatusCode"/> has a successful value between 200 and 299.
        /// </summary>
        /// <returns>The <see cref="ActionResultAssertor"/> to support fluent-style method-chaining.</returns>
        public ActionResultAssertor AssertSuccessStatusCode()
        {
            AssertResultType<IStatusCodeActionResult>();
            var scar = (IStatusCodeActionResult)Result;
            if (scar.StatusCode == null)
                return this;

            if (scar.StatusCode < 200 || scar.StatusCode > 299)
                Implementor.AssertFail($"Result Status Code '{scar.StatusCode}' must be between 200 and 299 to be considered successful.");

            return this;
        }

        /// <summary>
        /// Asserts that the <see cref="Result"/> has the specified <paramref name="statusCode"/>.
        /// </summary>
        /// <param name="statusCode">The <see cref="HttpStatusCode"/>.</param>
        /// <returns>The <see cref="ActionResultAssertor"/> to support fluent-style method-chaining.</returns>
        public ActionResultAssertor Assert(HttpStatusCode statusCode)
        {
            AssertResultType<IStatusCodeActionResult>();
            var scar = (IStatusCodeActionResult)Result;
            Implementor.AssertAreEqual((int)statusCode, scar.StatusCode ?? (int)HttpStatusCode.OK, $"Result StatusCode '{scar.StatusCode}' is not expected '{(int)statusCode}'.");
            return this;
        }

        /// <summary>
        /// Asserts that the <see cref="Result"/> has the specified <paramref name="expectedContent"/>.
        /// </summary>
        /// <param name="expectedContent">The expected content.</param>
        /// <returns>The <see cref="ActionResultAssertor"/> to support fluent-style method-chaining.</returns>
        /// <remarks>The <see cref="Result"/> must be an <see cref="ObjectResult"/> or <see cref="ContentResult"/> or an assertion error will occur.</remarks>
        public ActionResultAssertor AssertContent(string? expectedContent)
        {
            if (Result is ObjectResult)
                return AssertObjectResult(expectedContent);
            else if (Result is ContentResult)
                return AssertContentResult(expectedContent);

            Implementor.AssertFail($"Result IActionResult Type '{Result.GetType().Name}' must be either '{nameof(ObjectResult)}' or '{nameof(ContentResult)}' to assert its value.");
            return this;
        }

        /// <summary>
        /// Asserts that the <see cref="Result"/> has the specified <c>Content</c> <paramref name="expectedValue"/>.
        /// </summary>
        /// <typeparam name="TValue">The value <see cref="Type"/>.</typeparam>
        /// <param name="expectedValue">The expected value.</param>
        /// <param name="pathsToIgnore">The JSON paths to ignore from the comparison.</param>
        /// <returns>The <see cref="ActionResultAssertor"/> to support fluent-style method-chaining.</returns>
        /// <remarks>The <see cref="Result"/> must be an <see cref="ObjectResult"/>, <see cref="JsonResult"/> or <see cref="ContentResult"/> or an assertion error will occur.</remarks>
        public ActionResultAssertor AssertValue<TValue>(TValue expectedValue, params string[] pathsToIgnore)
        {
            if (Result is ObjectResult)
                return AssertObjectResult(expectedValue, pathsToIgnore);
            else if (Result is JsonResult)
                return AssertJsonResult(expectedValue, pathsToIgnore);
            else if (Result is ContentResult)
                return AssertContentResult(expectedValue, pathsToIgnore);

            Implementor.AssertFail($"Result IActionResult Type '{Result.GetType().Name}' must be either '{nameof(JsonResult)}', '{nameof(ObjectResult)}' or '{nameof(ContentResult)}' to assert its value.");
            return this;
        }

        /// <summary>
        /// Asserts that the <see cref="Result"/> has the specified <c>Content</c> matches the JSON serialized value.
        /// </summary>
        /// <typeparam name="TValue">The value <see cref="Type"/>.</typeparam>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualifed resource name) that contains the expected value as serialized JSON.</param>
        /// <param name="pathsToIgnore">The JSON paths to ignore from the comparison.</param>
        /// <returns>The <see cref="ActionResultAssertor"/> to support fluent-style method-chaining.</returns>
        public ActionResultAssertor AssertValueFromJsonResource<TValue>(string resourceName, params string[] pathsToIgnore)
            => AssertValue(Resource.GetJsonValue<TValue>(resourceName, Assembly.GetCallingAssembly(), JsonSerializer), pathsToIgnore);

        /// <summary>
        /// Asserts that the <see cref="Result"/> has the specified <c>Content</c> matches the JSON serialized value.
        /// </summary>
        /// <typeparam name="TAssembly">The <see cref="Type"/> to infer the <see cref="Assembly"/> that contains the embedded resource.</typeparam>
        /// <typeparam name="TValue">The value <see cref="Type"/>.</typeparam>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualifed resource name) that contains the expected value as serialized JSON.</param>
        /// <param name="pathsToIgnore">The JSON paths to ignore from the comparison.</param>
        /// <returns>The <see cref="ActionResultAssertor"/> to support fluent-style method-chaining.</returns>
        public ActionResultAssertor AssertValueFromJsonResource<TAssembly, TValue>(string resourceName, params string[] pathsToIgnore) 
            => AssertValue(Resource.GetJsonValue<TValue>(resourceName, typeof(TAssembly).Assembly, JsonSerializer), pathsToIgnore);

        /// <summary>
        /// Asserts that the <see cref="Result"/> is an <see cref="HttpStatusCode.NotFound"/>.
        /// </summary>
        /// <returns>The <see cref="ActionResultAssertor"/> to support fluent-style method-chaining.</returns>
        public ActionResultAssertor AssertNotFound() => Assert(HttpStatusCode.NotFound);

        /// <summary>
        /// Asserts that the <see cref="Result"/> is a <see cref="HttpStatusCode.NoContent"/>.
        /// </summary>
        /// <returns>The <see cref="ActionResultAssertor"/> to support fluent-style method-chaining.</returns>
        public ActionResultAssertor AssertNoContent() => Assert(HttpStatusCode.NoContent);

        /// <summary>
        /// Asserts that the <see cref="Result"/> is an <see cref="IStatusCodeActionResult"/> with a <see cref="IStatusCodeActionResult.StatusCode"/> of <see cref="HttpStatusCode.NotModified"/>.
        /// </summary>
        /// <returns>The <see cref="ActionResultAssertor"/> to support fluent-style method-chaining.</returns>
        public ActionResultAssertor AssertNotModified() => Assert(HttpStatusCode.NotModified);

        /// <summary>
        /// Asserts that the <see cref="Result"/> is an <see cref="HttpStatusCode.OK"/>.
        /// </summary>
        /// <returns>The <see cref="ActionResultAssertor"/> to support fluent-style method-chaining.</returns>
        public ActionResultAssertor AssertOK() => Assert(HttpStatusCode.OK);

        /// <summary>
        /// Asserts that the <see cref="Result"/> is <see cref="HttpStatusCode.Accepted"/>.
        /// </summary>
        /// <returns>The <see cref="ActionResultAssertor"/> to support fluent-style method-chaining.</returns>
        /// <remarks>The corresponding location header is not verified.</remarks>
        public ActionResultAssertor AssertAccepted() => Assert(HttpStatusCode.Accepted);

        /// <summary>
        /// Asserts that the <see cref="Result"/> is <see cref="HttpStatusCode.Created"/>.
        /// </summary>
        /// <returns>The <see cref="ActionResultAssertor"/> to support fluent-style method-chaining.</returns>
        /// <remarks>The corresponding location header is not verified.</remarks>
        public ActionResultAssertor AssertCreated() => Assert(HttpStatusCode.Created);

        /// <summary>
        /// Asserts that the <see cref="Result"/> is a <see cref="HttpStatusCode.BadRequest"/>.
        /// </summary>
        /// <returns>The <see cref="ActionResultAssertor"/> to support fluent-style method-chaining.</returns>
        public ActionResultAssertor AssertBadRequest() => Assert(HttpStatusCode.BadRequest);

        /// <summary>
        /// Asserts that the <see cref="Result"/> is a <see cref="HttpStatusCode.PreconditionFailed"/>.
        /// </summary>
        /// <returns>The <see cref="ActionResultAssertor"/> to support fluent-style method-chaining.</returns>
        public ActionResultAssertor AssertPreconditionFailed() => Assert(HttpStatusCode.PreconditionFailed);

        /// <summary>
        /// Asserts that the <see cref="Result"/> is a <see cref="HttpStatusCode.Conflict"/>.
        /// </summary>
        /// <returns>The <see cref="ActionResultAssertor"/> to support fluent-style method-chaining.</returns>
        public ActionResultAssertor AssertConflict() => Assert(HttpStatusCode.Conflict);

        /// <summary>
        /// Asserts that the <see cref="Result"/> is a <see cref="HttpStatusCode.Unauthorized"/>.
        /// </summary>
        /// <returns>The <see cref="ActionResultAssertor"/> to support fluent-style method-chaining.</returns>
        public ActionResultAssertor AssertUnauthorized() => Assert(HttpStatusCode.Unauthorized);

        /// <summary>
        /// Asserts that the <see cref="Result"/> is a <see cref="HttpStatusCode.Forbidden"/>.
        /// </summary>
        /// <returns>The <see cref="ActionResultAssertor"/> to support fluent-style method-chaining.</returns>
        public ActionResultAssertor AssertForbidden() => Assert(HttpStatusCode.Forbidden);

        /// <summary>
        /// Asserts that the <see cref="Result"/> contains the specified <paramref name="errors"/>.
        /// </summary>
        /// <param name="errors">The expected errors.</param>
        /// <returns>The <see cref="ActionResultAssertor"/> to support fluent-style method-chaining.</returns>
        public override ActionResultAssertor AssertErrors(params ApiError[] errors)
        {
            object? eval = null;
            if (Result is ObjectResult or)
                eval = or.Value;
            else if (Result is JsonResult jr)
                eval = jr.Value;
            else if (Result is ContentResult cr)
                eval = cr.Content;

            var val = new Dictionary<string, string[]>();
            if (eval is string str)
                val.Add(string.Empty, [str]);
            if (eval is Dictionary<string, string[]> dis)
                val = dis;
            else if (eval is Dictionary<string, object> dio)
            {
                foreach (var i in dio)
                {
                    if (i.Value is string[] msgs)
                        val.Add(i.Key, msgs);
                }
            }

            var act = new List<ApiError>();
            foreach (var err in val)
            {
                foreach (var msg in err.Value)
                {
                    act.Add(new ApiError(err.Key, msg));
                }
            }

            if (!Assertor.TryAreErrorsMatched(errors, act, out var errorMessage))
                Implementor.AssertFail(errorMessage);

            return this;
        }

        /// <summary>
        /// Asserts that the <see cref="Result"/> content type is <see cref="MediaTypeNames.Application.Json"/>.
        /// </summary>
        /// <returns>The <see cref="ActionResultAssertor"/> to support fluent-style method-chaining.</returns>
        public ActionResultAssertor AssertContentTypeJson() => AssertContentType(MediaTypeNames.Application.Json);

        /// <summary>
        /// Asserts that the <see cref="Result"/> content type is <see cref="MediaTypeNames.Text.Plain"/>.
        /// </summary>
        /// <returns>The <see cref="ActionResultAssertor"/> to support fluent-style method-chaining.</returns>
        public ActionResultAssertor AssertContentTypePlainText() => AssertContentType(MediaTypeNames.Text.Plain);

        /// <summary>
        /// Asserts that the <see cref="Result"/> content type matches the <paramref name="expectedContentType"/>.
        /// </summary>
        /// <param name="expectedContentType">The expected content type.</param>
        /// <returns>The <see cref="ActionResultAssertor"/> to support fluent-style method-chaining.</returns>
        public ActionResultAssertor AssertContentType(string expectedContentType)
        {
            if (Result is ObjectResult or)
            {
                if (!or.ContentTypes.Contains(expectedContentType))
                    Implementor.AssertFail($"Result ContentType '{string.Join(", ", or.ContentTypes)}' does not contain '{expectedContentType}'.");

                return this;
            }
            else if (Result is JsonResult jr)
                return AssertContentType(expectedContentType, jr.ContentType);
            else if (Result is ContentResult cr)
                return AssertContentType(expectedContentType, cr.ContentType);

            Implementor.AssertFail($"Result IActionResult Type '{Result.GetType().Name}' must be either '{nameof(JsonResult)}', '{nameof(ObjectResult)}' or {nameof(ContentResult)} to assert its value.");
            return this;
        }

        /// <summary>
        /// Asserts the content type.
        /// </summary>
        private ActionResultAssertor AssertContentType(string? expectedContentType, string? actualContentType)
        {
            Implementor.AssertAreEqual(expectedContentType, actualContentType, $"Result ContentType '{actualContentType}' is not expected '{expectedContentType}'.");
            return this;
        }

        /// <summary>
        /// Asserts that the <see cref="Result"/> is an <see cref="ObjectResult"/> that matches the <paramref name="expectedValue"/>.
        /// </summary>
        /// <typeparam name="TValue">The value <see cref="Type"/>.</typeparam>
        /// <param name="expectedValue">The expected value.</param>
        /// <param name="pathsToIgnore">The JSON paths to ignore from the comparison.</param>
        /// <returns>The <see cref="ActionResultAssertor"/> to support fluent-style method-chaining.</returns>
        internal ActionResultAssertor AssertObjectResult<TValue>(TValue expectedValue, params string[] pathsToIgnore)
        {
            AssertResultType<ObjectResult>();

            var or = (ObjectResult)Result;
            return AssertValue(expectedValue, or.Value, pathsToIgnore);
        }

        /// <summary>
        /// Asserts that the <see cref="Result"/> is a <see cref="JsonResult"/> that matches the <paramref name="expectedValue"/>.
        /// </summary>
        /// <typeparam name="TValue">The value <see cref="Type"/>.</typeparam>
        /// <param name="expectedValue">The expected value.</param>
        /// <param name="pathsToIgnore">The JSON paths to ignore from the comparison.</param>
        /// <returns>The <see cref="ActionResultAssertor"/> to support fluent-style method-chaining.</returns>
        internal ActionResultAssertor AssertJsonResult<TValue>(TValue expectedValue, params string[] pathsToIgnore)
        {
            AssertResultType<JsonResult>();

            var jr = (JsonResult)Result;
            return AssertValue(expectedValue, jr.Value, pathsToIgnore);
        }

        /// <summary>
        /// Asserts that the <see cref="Result"/> is a <see cref="ContentResult"/> that matches the <paramref name="expectedValue"/>.
        /// </summary>
        /// <typeparam name="TValue">The value <see cref="Type"/>.</typeparam>
        /// <param name="expectedValue">The expected value.</param>
        /// <param name="pathsToIgnore">The JSON paths to ignore from the comparison.</param>
        /// <returns>The <see cref="ActionResultAssertor"/> to support fluent-style method-chaining.</returns>
        internal ActionResultAssertor AssertContentResult<TValue>(TValue expectedValue, params string[] pathsToIgnore)
        {
            AssertResultType<ContentResult>();

            var cr = (ContentResult)Result;
            if (expectedValue != null && cr.Content != null && TesterBase.JsonMediaTypeNames.Contains(cr.ContentType))
                return AssertValue(expectedValue, JsonSerializer.Deserialize<TValue>(cr.Content)!, pathsToIgnore);
            else
                return AssertValue(expectedValue, cr.Content!, pathsToIgnore);
        }

        /// <summary>
        /// Assert the value.
        /// </summary>
        private ActionResultAssertor AssertValue<TValue>(TValue? expectedValue, object? actualValue, string[] pathsToIgnore)
        {
            if (expectedValue == null && actualValue == null)
                return this;

            if (expectedValue is IComparable)
                Implementor.AssertAreEqual(expectedValue, actualValue);
            else
            {
                var cr = Owner.CreateJsonComparer().CompareValue(expectedValue, actualValue, pathsToIgnore);
                if (cr.HasDifferences)
                    Implementor.AssertFail($"Expected and Actual values are not equal:{Environment.NewLine}{cr}");
            }

            return this;
        }

        /// <summary>
        /// Asserts that the <see cref="Result"/> has the specified <c>Content</c> matches the JSON value.
        /// </summary>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualifed resource name) that contains the expected JSON.</param>
        /// <param name="pathsToIgnore">The JSON paths to ignore from the comparison.</param>
        /// <returns>The <see cref="ActionResultAssertor"/> to support fluent-style method-chaining.</returns>
        public ActionResultAssertor AssertJsonFromResource(string resourceName, params string[] pathsToIgnore)
            => AssertValue(Resource.GetJson(resourceName, Assembly.GetCallingAssembly()), pathsToIgnore);

        /// <summary>
        /// Asserts that the <see cref="Result"/> JSON content matches the specified <paramref name="json"/>.
        /// </summary>
        /// <param name="json">The expected JSON.</param>
        /// <param name="pathsToIgnore">The JSON paths to ignore from the comparison.</param>
        /// <returns>The <see cref="ActionResultAssertor"/> to support fluent-style method-chaining.</returns>
#if NET7_0_OR_GREATER
        public ActionResultAssertor AssertJson([StringSyntax(StringSyntaxAttribute.Json)] string json, params string[] pathsToIgnore)
#else
        public ActionResultAssertor AssertJson(string json, params string[] pathsToIgnore)
#endif
        {
            if (string.IsNullOrEmpty(json))
                throw new ArgumentNullException(nameof(json));

            var actj = GetValueAsJson();
            if (actj == null)
            {
                Implementor.AssertAreEqual(json, null, "Expected and Actual (no content) JSON values are not equal");
                return this;
            }

            var exp = new Utf8JsonReader(new BinaryData(json));
            if (!JsonElement.TryParseValue(ref exp, out JsonElement? eje))
                throw new ArgumentException("Expected JSON is not considered valid.", nameof(json));

            var act = new Utf8JsonReader(new BinaryData(actj));
            if (!JsonElement.TryParseValue(ref act, out JsonElement? aje))
                Implementor.AssertFail("Actual value is not considered valid JSON.");

            var jecr = Owner.CreateJsonComparer().Compare(eje!.Value, aje!.Value, pathsToIgnore);
            if (jecr.HasDifferences)
                Implementor.AssertFail($"Expected and Actual JSON values are not equal:{Environment.NewLine}{jecr}");

            return this;
        }

        /// <summary>
        /// Gets the value as a JSON <see cref="string"/>.
        /// </summary>
        /// <returns>The JSON <see cref="string"/>.</returns>
        public string? GetValueAsJson()
        {
            if (Result == null)
                return default;
            else if (Result is ObjectResult or)
                return or.Value == null ? default : JsonSerializer.Serialize(or.Value);
            else if (Result is JsonResult jr)
                return jr.Value == null ? default : JsonSerializer.Serialize(jr.Value);
            else if (Result is ContentResult cr)
            {
                AssertContentTypeJson();
                return cr.Content;
            }
            else
                return null;
        }

        /// <summary>
        /// Gets the response value.
        /// </summary>
        /// <typeparam name="TValue">The resulting value <see cref="Type"/>.</typeparam>
        /// <returns>The result value.</returns>
        public TValue? GetValue<TValue>()
        {
            var value = GetValueInternal<TValue>();
            foreach (var ext in TestSetUp.Extensions)
                ext.UpdateValueFromActionResult(Owner, Result, ref value);

            return value;
        }

        /// <summary>
        /// Gets the response value.
        /// </summary>
        private TValue? GetValueInternal<TValue>()
        {
            if (Result == null)
                return default;
            else if (Result is ObjectResult or)
                return or.Value == null ? default : (or.Value is TValue tv ? tv : GetValueFail<TValue>(or.Value));
            else if (Result is JsonResult jr)
                return jr.Value == null ? default : (jr.Value is TValue tv ? tv : GetValueFail<TValue>(jr.Value));
            else if (Result is ContentResult cr)
            {
                if (cr == null || string.IsNullOrEmpty(cr.Content))
                    return default;

                try
                {
                    var val = JsonSerializer.Deserialize<TValue>(cr.Content);
                    return val;
                }
                catch (Exception ex)
                {
                    Implementor.AssertFail($"Unable to deserialize the JSON content to Type {typeof(TValue).FullName}: {ex.Message}");
                    throw new InvalidOperationException($"{nameof(TestFrameworkImplementor)}.{nameof(TestFrameworkImplementor.AssertFail)} has not worked correctly; this code should never execute?!"); // Will never reach here; needed to compile.
                }
            }
            else
                return default;
        }

        /// <summary>
        /// Get the response value failure.
        /// </summary>
        private TValue? GetValueFail<TValue>(object result)
        {
            Implementor.AssertFail($"Value Type '{typeof(TValue).FullName}' is not same as ObjectResult.Value Type '{result.GetType().FullName}'.");
            throw new InvalidOperationException($"{nameof(TestFrameworkImplementor)}.{nameof(TestFrameworkImplementor.AssertFail)} has not worked correctly; this code should never execute?!"); // Will never reach here; needed to compile.
        }

        /// <summary>
        /// Converts the <see cref="IActionResult"/> to an <see cref="HttpResponseMessageAssertor"/>.
        /// </summary>
        /// <param name="httpRequest">The optional requesting <see cref="HttpRequest"/> with <see cref="HttpContext"/>; otherwise, will default.</param>
        /// <returns>The corresponding <see cref="HttpResponseMessageAssertor"/>.</returns>
        public HttpResponseMessageAssertor ToHttpResponseMessageAssertor(HttpRequest? httpRequest = null) => ToHttpResponseMessageAssertor(Owner, Result, httpRequest);

        /// <summary>
        /// Converts the <see cref="ValueAssertor{TValue}"/> to an <see cref="HttpResponseMessageAssertor"/>.
        /// </summary>
        /// <param name="owner">The owning <see cref="TesterBase"/>.</param>
        /// <param name="result">The <see cref="IActionResult"/> to convert.</param>
        /// <param name="httpRequest">The optional requesting <see cref="HttpRequest"/>; otherwise, will default.</param>
        /// <returns>The corresponding <see cref="HttpResponseMessageAssertor"/>.</returns>
        internal static HttpResponseMessageAssertor ToHttpResponseMessageAssertor(TesterBase owner, IActionResult result, HttpRequest? httpRequest)
        {
            var sw = Stopwatch.StartNew();
            using var ms = new MemoryStream();
            var context = httpRequest?.HttpContext ?? new DefaultHttpContext { RequestServices = owner.Services };
            context.Response.Body = ms;

            result.ExecuteResultAsync(new ActionContext(context, new Microsoft.AspNetCore.Routing.RouteData(), new ActionDescriptor())).GetAwaiter().GetResult();

            var hr = new HttpResponseMessage((System.Net.HttpStatusCode)context.Response.StatusCode);
            foreach (var h in context.Response.Headers)
                hr.Headers.TryAddWithoutValidation(h.Key, [.. h.Value]);

            ms.Position = 0;
            hr.Content = new ByteArrayContent(ms.ToArray());

            hr.Content.Headers.ContentLength = context.Response.ContentLength;
            if (context.Response.ContentType is not null && System.Net.Http.Headers.MediaTypeHeaderValue.TryParse(context.Response.ContentType, out var ct))
                hr.Content.Headers.ContentType = ct;

            sw.Stop();
            owner.LogHttpResponseMessage(hr, sw);

            return new HttpResponseMessageAssertor(owner, hr);
        }
    }
}