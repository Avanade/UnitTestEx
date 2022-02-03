// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Mime;
using System.Reflection;
using UnitTestEx.Abstractions;

namespace UnitTestEx.Assertors
{
    /// <summary>
    /// Represents the <see cref="IActionResult"/> test assert helper.
    /// </summary>
    public class ActionResultAssertor : AssertorBase<ActionResultAssertor>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ActionResultAssertor"/> class.
        /// </summary>
        /// <param name="result">The <see cref="IActionResult"/>.</param>
        /// <param name="exception">The <see cref="Exception"/> (if any).</param>
        /// <param name="implementor">The <see cref="TestFrameworkImplementor"/>.</param>
        internal ActionResultAssertor(IActionResult result, Exception? exception, TestFrameworkImplementor implementor) : base(exception, implementor) => Result = result;

        /// <summary>
        /// Gets the <see cref="IActionResult"/>.
        /// </summary>
        public IActionResult Result { get; }

        /// <summary>
        /// Assert the <see cref="Result"/> <see cref="Type"/>.
        /// </summary>
        /// <typeparam name="T">The expected <see cref="IActionResult"/> <see cref="Type"/>.</typeparam>
        /// <returns>The <see cref="ActionResultAssertor"/> to support fluent-style method-chaining.</returns>
        public ActionResultAssertor AssertResultType<T>() where T : IActionResult
        {
            AssertSuccess();
            Implementor.AssertIsType<IStatusCodeActionResult>(Result, $"Result Type '{Result.GetType().Name}' is not expected '{typeof(T).Name}'.");
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
        /// Asserts that the <see cref="Result"/> has the specified <c>Content</c> <paramref name="expectedValue"/>.
        /// </summary>
        /// <param name="expectedValue">The expected value.</param>
        /// <param name="membersToIgnore">The members to ignore from the comparison.</param>
        /// <returns>The <see cref="ActionResultAssertor"/> to support fluent-style method-chaining.</returns>
        /// <remarks>The <see cref="Result"/> must be an <see cref="ObjectResult"/>, <see cref="JsonResult"/> or <see cref="ContentResult"/> or an assertion error will occur.</remarks>
        public ActionResultAssertor Assert(object? expectedValue, params string[] membersToIgnore)
        {
            if (Result is ObjectResult)
                return AssertObjectResult(expectedValue, membersToIgnore);
            else if (Result is JsonResult)
                return AssertJsonResult(expectedValue, membersToIgnore);
            else if (Result is ContentResult)
                return AssertContentResult(expectedValue?.ToString());

            Implementor.AssertFail($"Result IActionResult Type '{Result.GetType().Name}' must be either '{nameof(JsonResult)}', '{nameof(ObjectResult)}' or {nameof(ContentResult)} to assert its value.");
            return this;
        }

        /// <summary>
        /// AAsserts that the <see cref="Result"/> has the specified <c>Content</c> matches the JSON serialized value.
        /// </summary>
        /// <typeparam name="TResult">The result <see cref="Type"/>.</typeparam>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualifed resource name) that contains the expected value as serialized JSON.</param>
        /// <param name="membersToIgnore">The members to ignore from the comparison.</param>
        /// <returns>The <see cref="ActionResultAssertor"/> to support fluent-style method-chaining.</returns>
        public ActionResultAssertor AssertFromJsonResource<TResult>(string resourceName, params string[] membersToIgnore)
            => Assert(Resource.GetJsonValue<TResult>(resourceName, Assembly.GetCallingAssembly()),membersToIgnore);

        /// <summary>
        /// Asserts that the <see cref="Result"/> has the specified <c>Content</c> matches the JSON serialized value.
        /// </summary>
        /// <typeparam name="TAssembly">The <see cref="Type"/> to infer the <see cref="Assembly"/> that contains the embedded resource.</typeparam>
        /// <typeparam name="TResult">The result <see cref="Type"/>.</typeparam>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualifed resource name) that contains the expected value as serialized JSON.</param>
        /// <param name="membersToIgnore">The members to ignore from the comparison.</param>
        /// <returns>The <see cref="ActionResultAssertor"/> to support fluent-style method-chaining.</returns>
        public ActionResultAssertor AssertFromJsonResource<TAssembly, TResult>(string resourceName, params string[] membersToIgnore) 
            => Assert(Resource.GetJsonValue<TResult>(resourceName, typeof(TAssembly).Assembly), membersToIgnore);

        /// <summary>
        /// AAsserts that the <see cref="Result"/> has the specified <c>Content</c> matches the JSON serialized value.
        /// </summary>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualifed resource name) that contains the expected value as serialized JSON.</param>
        /// <param name="membersToIgnore">The members to ignore from the comparison.</param>
        /// <returns>The <see cref="ActionResultAssertor"/> to support fluent-style method-chaining.</returns>
        public ActionResultAssertor AssertFromJsonResource(string resourceName, params string[] membersToIgnore)
            => Assert(Resource.GetJson(resourceName, Assembly.GetCallingAssembly()), membersToIgnore);

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
        public ActionResultAssertor AssertAccepted()
        {
            Assert(HttpStatusCode.Accepted);
            return this;
        }

        /// <summary>
        /// Asserts that the <see cref="Result"/> is <see cref="HttpStatusCode.Created"/>.
        /// </summary>
        /// <returns>The <see cref="ActionResultAssertor"/> to support fluent-style method-chaining.</returns>
        /// <remarks>The corresponding location header is not verified.</remarks>
        public ActionResultAssertor AssertCreated()
        {
            Assert(HttpStatusCode.Created);
            return this;
        }

        /// <summary>
        /// Asserts that the <see cref="Result"/> is a <see cref="HttpStatusCode.BadRequest"/>.
        /// </summary>
        /// <returns>The <see cref="ActionResultAssertor"/> to support fluent-style method-chaining.</returns>
        public ActionResultAssertor AssertBadRequest() => Assert(HttpStatusCode.BadRequest);

        /// <summary>
        /// Asserts that the <see cref="Result"/> contains the expected error <paramref name="messages"/>.
        /// </summary>
        /// <param name="messages">The expected error messages.</param>
        /// <returns>The <see cref="ActionResultAssertor"/> to support fluent-style method-chaining.</returns>
        /// <remarks>The field (key) is not validated; only the error message texts.</remarks>
        public ActionResultAssertor AssertErrors(params string[] messages)
        {
            var expected = new List<ApiError>();
            foreach (var m in messages)
            {
                expected.Add(new ApiError(null, m));
            }

            AssertBadRequestErrors(expected, false);
            return this;
        }

        /// <summary>
        /// Asserts that the <see cref="Result"/> contains the <paramref name="expected"/> errors.
        /// </summary>
        /// <param name="expected">The expected errors.</param>
        /// <returns>The <see cref="ActionResultAssertor"/> to support fluent-style method-chaining.</returns>
        public ActionResultAssertor AssertErrors(params ApiError[] expected)
        {
            AssertBadRequestErrors(expected, true);
            return this;
        }

        /// <summary>
        /// Asserts the expected vs actual errors.
        /// </summary>
        private void AssertBadRequestErrors(IEnumerable<ApiError> expected, bool includeField)
        {
            AssertSuccess();

            object? eval = null;
            if (Result is ObjectResult or)
                eval = or.Value;
            else if (Result is JsonResult jr)
                eval = jr.Value;

            var val = new Dictionary<string, string[]>();
            if (eval is string str)
                val.Add(string.Empty, new string[] { str });
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
                    act.Add(new ApiError(includeField ? err.Key : null, msg));
                }
            }

            if (!ApiError.TryAreMatched(expected, act, includeField, out var message))
                Implementor.AssertFail(message);
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
        /// <param name="expectedValue">The expected value.</param>
        /// <param name="membersToIgnore">The members to ignore from the comparison.</param>
        /// <returns>The <see cref="ActionResultAssertor"/> to support fluent-style method-chaining.</returns>
        internal ActionResultAssertor AssertObjectResult(object? expectedValue, params string[] membersToIgnore)
        {
            AssertResultType<ObjectResult>();

            var or = (ObjectResult)Result;
            return AssertValue(expectedValue, or.Value, membersToIgnore);
        }

        /// <summary>
        /// Asserts that the <see cref="Result"/> is a <see cref="JsonResult"/> that matches the <paramref name="expectedValue"/>.
        /// </summary>
        /// <param name="expectedValue">The expected value.</param>
        /// <param name="membersToIgnore">The members to ignore from the comparison.</param>
        /// <returns>The <see cref="ActionResultAssertor"/> to support fluent-style method-chaining.</returns>
        internal ActionResultAssertor AssertJsonResult(object? expectedValue, params string[] membersToIgnore)
        {
            AssertResultType<JsonResult>();

            var jr = (JsonResult)Result;
            return AssertValue(expectedValue, jr.Value, membersToIgnore);
        }

        /// <summary>
        /// Asserts that the <see cref="Result"/> is a <see cref="ContentResult"/> that matches the <paramref name="expectedValue"/>.
        /// </summary>
        /// <param name="expectedValue">The expected value.</param>
        /// <returns>The <see cref="ActionResultAssertor"/> to support fluent-style method-chaining.</returns>
        internal ActionResultAssertor AssertContentResult(string? expectedValue)
        {
            AssertResultType<ContentResult>();

            var cr = (ContentResult)Result;
            return AssertValue(expectedValue, cr.Content, Array.Empty<string>());
        }

        /// <summary>
        /// Assert the value.
        /// </summary>
        private ActionResultAssertor AssertValue(object? expectedValue, object actualValue, string[] membersToIgnore)
        {
            if (expectedValue is JToken jte)
            {
                var jta = JToken.FromObject(actualValue);
                if (!JToken.DeepEquals(jte, jta))
                    Implementor.AssertFail($"Expected and Actual JSON are not equal: {Environment.NewLine}Expected =>{Environment.NewLine}{jte?.ToString(Formatting.Indented)}{Environment.NewLine}Actual =>{Environment.NewLine}{jta?.ToString(Formatting.Indented)}");
            }
            else if (expectedValue is IComparable)
                Implementor.AssertAreEqual(expectedValue, actualValue);
            else
            {
                var cr = ObjectComparer.Compare(expectedValue, actualValue, membersToIgnore);
                if (!cr.AreEqual)
                    Implementor.AssertFail($"Expected and Actual values are not equal: {cr.DifferencesString}");
            }

            return this;
        }
    }
}