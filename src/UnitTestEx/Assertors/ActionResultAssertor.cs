// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net;
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
        /// Asserts that the <see cref="Result"/> has the specified <paramref name="statusCode"/>.
        /// </summary>
        /// <param name="expectedValue">The expected value.</param>
        /// <param name="statusCode">The <see cref="HttpStatusCode"/>.</param>
        /// <param name="membersToIgnore">The members to ignore from the comparison.</param>
        /// <returns>The <see cref="ActionResultAssertor"/> to support fluent-style method-chaining.</returns>
        public ActionResultAssertor Assert(object? expectedValue, HttpStatusCode statusCode, params string[] membersToIgnore)
        {
            Assert(statusCode);
            var scar = (IStatusCodeActionResult)Result;
            if (scar.StatusCode < 200 || scar.StatusCode > 299)
                Implementor.AssertFail($"Result Status Code '{scar.StatusCode}' must be between 200 and 299 to assert its value.");

            if (Result is ObjectResult)
                return AssertObjectResult(expectedValue, statusCode, membersToIgnore);
            else if (Result is JsonResult)
                return AssertJsonResult(expectedValue, statusCode, membersToIgnore);

            Implementor.AssertFail($"Result IActionResult Type '{Result.GetType().Name}' must be either '{nameof(JsonResult)}' or '{nameof(ObjectResult)}' to assert its value.");
            return this;
        }

        /// <summary>
        /// Asserts that the <see cref="Result"/> is an <see cref="NotFoundResult"/>.
        /// </summary>
        /// <returns>The <see cref="ActionResultAssertor"/> to support fluent-style method-chaining.</returns>
        public ActionResultAssertor AssertNotFound() => Assert(HttpStatusCode.NotFound);

        /// <summary>
        /// Asserts that the <see cref="Result"/> is a <see cref="NoContentResult"/>.
        /// </summary>
        /// <returns>The <see cref="ActionResultAssertor"/> to support fluent-style method-chaining.</returns>
        public ActionResultAssertor AssertNoContent() => Assert(HttpStatusCode.NoContent);

        /// <summary>
        /// Asserts that the <see cref="Result"/> is an <see cref="OkResult"/>.
        /// </summary>
        /// <returns>The <see cref="ActionResultAssertor"/> to support fluent-style method-chaining.</returns>
        public ActionResultAssertor AssertOK() => Assert(HttpStatusCode.OK);

        /// <summary>
        /// Asserts that the <see cref="Result"/> is an <see cref="IStatusCodeActionResult"/> with a <see cref="IStatusCodeActionResult.StatusCode"/> of <see cref="HttpStatusCode.NotModified"/>.
        /// </summary>
        /// <returns>The <see cref="ActionResultAssertor"/> to support fluent-style method-chaining.</returns>
        public ActionResultAssertor AssertNotModified() => Assert(HttpStatusCode.NotModified);

        /// <summary>
        /// Asserts that the <see cref="Result"/> is <see cref="HttpStatusCode.OK"/> and matches the <paramref name="expectedValue"/>.
        /// </summary>
        /// <param name="expectedValue">The expected value.</param>
        /// <param name="membersToIgnore">The members to ignore from the comparison.</param>
        /// <returns>The <see cref="ActionResultAssertor"/> to support fluent-style method-chaining.</returns>
        public ActionResultAssertor AssertOK(object? expectedValue, params string[] membersToIgnore) => Assert(expectedValue, HttpStatusCode.OK, membersToIgnore);

        /// <summary>
        /// Asserts that the <see cref="Result"/> is <see cref="HttpStatusCode.OK"/> and matches the JSON serialized value.
        /// </summary>
        /// <typeparam name="TResult">The result <see cref="Type"/>.</typeparam>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualifed resource name) that contains the expected value as serialized JSON.</param>
        /// <param name="membersToIgnore">The members to ignore from the comparison.</param>
        /// <returns>The <see cref="ActionResultAssertor"/> to support fluent-style method-chaining.</returns>
        public ActionResultAssertor AssertOKFromJsonResource<TResult>(string resourceName, params string[] membersToIgnore) => Assert(Resource.GetJsonValue<TResult>(resourceName, Assembly.GetCallingAssembly()), HttpStatusCode.OK, membersToIgnore);

        /// <summary>
        /// Asserts that the <see cref="Result"/> is <see cref="HttpStatusCode.OK"/> and matches the JSON serialized value.
        /// </summary>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualifed resource name) that contains the expected value as serialized JSON.</param>
        /// <param name="membersToIgnore">The members to ignore from the comparison.</param>
        /// <returns>The <see cref="ActionResultAssertor"/> to support fluent-style method-chaining.</returns>
        public ActionResultAssertor AssertOKFromJsonResource(string resourceName, params string[] membersToIgnore) => Assert(Resource.GetJson(resourceName, Assembly.GetCallingAssembly()), HttpStatusCode.OK, membersToIgnore);

        /// <summary>
        /// Asserts that the <see cref="Result"/> is <see cref="HttpStatusCode.OK"/> and matches the <paramref name="expectedValue"/>.
        /// </summary>
        /// <param name="expectedValue">The expected value.</param>
        /// <param name="membersToIgnore">The members to ignore from the comparison.</param>
        /// <returns>The <see cref="ActionResultAssertor"/> to support fluent-style method-chaining.</returns>
        public ActionResultAssertor AssertCreated(object? expectedValue, params string[] membersToIgnore) => Assert(expectedValue, HttpStatusCode.OK, membersToIgnore);

        /// <summary>
        /// Asserts that the <see cref="Result"/> is <see cref="HttpStatusCode.Created"/> and matches the JSON serialized value.
        /// </summary>
        /// <typeparam name="TResult">The result <see cref="Type"/>.</typeparam>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualifed resource name) that contains the expected value as serialized JSON.</param>
        /// <param name="membersToIgnore">The members to ignore from the comparison.</param>
        /// <returns>The <see cref="ActionResultAssertor"/> to support fluent-style method-chaining.</returns>
        public ActionResultAssertor AssertCreatedFromJsonResource<TResult>(string resourceName, params string[] membersToIgnore) => Assert(Resource.GetJsonValue<TResult>(resourceName, Assembly.GetCallingAssembly()), HttpStatusCode.Created, membersToIgnore);

        /// <summary>
        /// Asserts that the <see cref="Result"/> is <see cref="HttpStatusCode.Created"/> and matches the JSON serialized value.
        /// </summary>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualifed resource name) that contains the expected value as serialized JSON.</param>
        /// <param name="membersToIgnore">The members to ignore from the comparison.</param>
        /// <returns>The <see cref="ActionResultAssertor"/> to support fluent-style method-chaining.</returns>
        public ActionResultAssertor AssertCreatedFromJsonResource(string resourceName, params string[] membersToIgnore) => Assert(Resource.GetJson(resourceName, Assembly.GetCallingAssembly()), HttpStatusCode.Created, membersToIgnore);

        /// <summary>
        /// Asserts that the <see cref="Result"/> is <see cref="HttpStatusCode.Accepted"/> and matches the <paramref name="expectedValue"/>.
        /// </summary>
        /// <param name="expectedValue">The expected value.</param>
        /// <param name="membersToIgnore">The members to ignore from the comparison.</param>
        /// <returns>The <see cref="ActionResultAssertor"/> to support fluent-style method-chaining.</returns>
        public ActionResultAssertor AssertAccepted(object? expectedValue, params string[] membersToIgnore) => Assert(expectedValue, HttpStatusCode.Accepted, membersToIgnore);

        /// <summary>
        /// Asserts that the <see cref="Result"/> is <see cref="HttpStatusCode.Accepted"/> and matches the JSON serialized value.
        /// </summary>
        /// <typeparam name="TResult">The result <see cref="Type"/>.</typeparam>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualifed resource name) that contains the expected value as serialized JSON.</param>
        /// <param name="membersToIgnore">The members to ignore from the comparison.</param>
        /// <returns>The <see cref="ActionResultAssertor"/> to support fluent-style method-chaining.</returns>
        public ActionResultAssertor AssertAcceptedFromJsonResource<TResult>(string resourceName, params string[] membersToIgnore) => Assert(Resource.GetJsonValue<TResult>(resourceName, Assembly.GetCallingAssembly()), HttpStatusCode.Accepted, membersToIgnore);

        /// <summary>
        /// Asserts that the <see cref="Result"/> is <see cref="HttpStatusCode.Accepted"/> and matches the JSON serialized value.
        /// </summary>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualifed resource name) that contains the expected value as serialized JSON.</param>
        /// <param name="membersToIgnore">The members to ignore from the comparison.</param>
        /// <returns>The <see cref="ActionResultAssertor"/> to support fluent-style method-chaining.</returns>
        public ActionResultAssertor AssertAcceptedFromJsonResource(string resourceName, params string[] membersToIgnore) => Assert(Resource.GetJson(resourceName, Assembly.GetCallingAssembly()), HttpStatusCode.Accepted, membersToIgnore);

        /// <summary>
        /// Asserts that the <see cref="Result"/> is a <see cref="HttpStatusCode.BadRequest"/>.
        /// </summary>
        /// <returns>The <see cref="ActionResultAssertor"/> to support fluent-style method-chaining.</returns>
        public ActionResultAssertor AssertBadRequest() => Assert(HttpStatusCode.BadRequest);

        /// <summary>
        /// Asserts that the <see cref="Result"/> is a <see cref="HttpStatusCode.BadRequest"/> and contains the expected error <paramref name="messages"/>.
        /// </summary>
        /// <param name="messages">The expected error messages.</param>
        /// <returns>The <see cref="ActionResultAssertor"/> to support fluent-style method-chaining.</returns>
        /// <remarks>The field (key) is not validated; only the error message texts.</remarks>
        public ActionResultAssertor AssertBadRequest(params string[] messages)
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
        /// Asserts that the <see cref="Result"/> is a <see cref="HttpStatusCode.BadRequest"/> and contains the <paramref name="expected"/> errors.
        /// </summary>
        /// <param name="expected">The expected errors.</param>
        /// <returns>The <see cref="ActionResultAssertor"/> to support fluent-style method-chaining.</returns>
        public ActionResultAssertor AssertBadRequest(params ApiError[] expected)
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
        /// Asserts that the <see cref="Result"/> is an <see cref="ObjectResult"/> that matches the <paramref name="expectedValue"/>.
        /// </summary>
        /// <param name="expectedValue">The expected value.</param>
        /// <param name="statusCode">The <see cref="HttpStatusCode"/>.</param>
        /// <param name="membersToIgnore">The members to ignore from the comparison.</param>
        /// <returns>The <see cref="ActionResultAssertor"/> to support fluent-style method-chaining.</returns>
        internal ActionResultAssertor AssertObjectResult(object? expectedValue, HttpStatusCode statusCode = HttpStatusCode.OK, params string[] membersToIgnore)
        {
            AssertResultType<ObjectResult>();
            Assert(statusCode);

            var or = (ObjectResult)Result;
            return AssertValue(expectedValue, or.Value, membersToIgnore);
        }

        /// <summary>
        /// Asserts that the <see cref="Result"/> is a <see cref="JsonResult"/> that matches the <paramref name="expectedValue"/> and <paramref name="statusCode"/>.
        /// </summary>
        /// <param name="expectedValue">The expected value.</param>
        /// <param name="statusCode">The <see cref="HttpStatusCode"/>.</param>
        /// <param name="membersToIgnore">The members to ignore from the comparison.</param>
        internal ActionResultAssertor AssertJsonResult(object? expectedValue, HttpStatusCode statusCode = HttpStatusCode.OK, params string[] membersToIgnore)
        {
            AssertResultType<JsonResult>();
            Assert(statusCode);

            var jr = (JsonResult)Result;
            return AssertValue(expectedValue, jr.Value, membersToIgnore);
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