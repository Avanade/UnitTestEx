// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Reflection;
using UnitTestEx.Abstractions;

namespace UnitTestEx.Assertors
{
    /// <summary>
    /// Represents the test result assert helper.
    /// </summary>
    public class ResultAssertor<TResult> : AssertorBase<ResultAssertor<TResult>>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ResultAssertor{TResult}"/> class.
        /// </summary>
        /// <param name="result">The result value.</param>
        /// <param name="exception">The <see cref="Exception"/> (if any).</param>
        /// <param name="implementor">The <see cref="TestFrameworkImplementor"/>.</param>
        internal ResultAssertor(TResult result, Exception? exception, TestFrameworkImplementor implementor) : base(exception, implementor) => Result = result;

        /// <summary>
        /// Gets the result.
        /// </summary>
        public TResult Result { get; }

        /// <summary>
        /// Asserts that the <see cref="Result"/> matches the <paramref name="expectedValue"/>.
        /// </summary>
        /// <param name="expectedValue">The expected value.</param>
        /// <param name="membersToIgnore">The members to ignore from the comparison.</param>
        /// <returns>The <see cref="ResultAssertor{TResult}"/> to support fluent-style method-chaining.</returns>
        public ResultAssertor<TResult> Assert(TResult expectedValue, params string[] membersToIgnore)
        {
            AssertSuccess();
            if (expectedValue is IComparable)
                Implementor.AssertAreEqual(expectedValue, Result);
            else
            {
                var cr = ObjectComparer.Compare(expectedValue, Result, membersToIgnore);
                if (!cr.AreEqual)
                    Implementor.AssertFail($"Expected and Actual values are not equal: {cr.DifferencesString}");
            }

            return this;
        }

        /// <summary>
        ///  Asserts that the <see cref="Result"/> matches the <paramref name="json"/> serialized value.
        /// </summary>
        /// <param name="json">The JSON <see cref="string"/>.</param>
        /// <param name="membersToIgnore">The members to ignore from the comparison.</param>
        /// <returns>The <see cref="ResultAssertor{TResult}"/> to support fluent-style method-chaining.</returns>
        public ResultAssertor<TResult> AssertFromJson(string json, params string[] membersToIgnore) => Assert(JsonConvert.DeserializeObject<TResult>(json)!, membersToIgnore);

        /// <summary>
        /// Asserts that the <see cref="Result"/> matches the JSON serialized value from the named embedded resource.
        /// </summary>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualifed resource name) that contains the expected value as serialized JSON.</param>
        /// <param name="membersToIgnore">The members to ignore from the comparison.</param>
        /// <returns>The <see cref="ResultAssertor{TResult}"/> to support fluent-style method-chaining.</returns>
        public ResultAssertor<TResult> AssertFromJsonResource(string resourceName, params string[] membersToIgnore)
            => Assert(Resource.GetJsonValue<TResult>(resourceName, Assembly.GetCallingAssembly()), membersToIgnore);

        /// <summary>
        /// Asserts that the <see cref="Result"/> matches the JSON serialized value from the named embedded resource.
        /// </summary>
        /// <typeparam name="TAssembly">The <see cref="Type"/> to infer the <see cref="Assembly"/> that contains the embedded resource.</typeparam>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualifed resource name) that contains the expected value as serialized JSON.</param>
        /// <param name="membersToIgnore">The members to ignore from the comparison.</param>
        /// <returns>The <see cref="ResultAssertor{TResult}"/> to support fluent-style method-chaining.</returns>
        public ResultAssertor<TResult> AssertFromJsonResource<TAssembly>(string resourceName, params string[] membersToIgnore)
            => Assert(Resource.GetJsonValue<TResult>(resourceName, typeof(TAssembly).Assembly), membersToIgnore);

        /// <summary>
        /// Converts the <see cref="ResultAssertor{TResult}"/> to an <see cref="ActionResultAssertor"/>.
        /// </summary>
        /// <returns>The corresponding <see cref="ActionResultAssertor"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown where the <see cref="Result"/> <see cref="Type"/> is not assignable from <see cref="IActionResult"/>.</exception>
        public ActionResultAssertor ToActionResultAssertor()
        {
            if (typeof(IActionResult).IsAssignableFrom(typeof(TResult)))
                return new ActionResultAssertor((IActionResult)Result!, Exception, Implementor);

            throw new InvalidOperationException($"Result Type '{typeof(TResult).Name}' must be assignable from '{nameof(IActionResult)}'");
        }

        /// <summary>
        /// Converts the <see cref="ResultAssertor{TResult}"/> to an <see cref="HttpResponseMessageAssertor"/>.
        /// </summary>
        /// <returns>The corresponding <see cref="HttpResponseMessageAssertor"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown where the <see cref="Result"/> <see cref="Type"/> is not <see cref="HttpResponseMessage"/>.</exception>
        public HttpResponseMessageAssertor ToHttpResponseMessageAssertor()
        {
            if (Result != null && Result is HttpResponseMessage hrm)
                return new HttpResponseMessageAssertor(hrm, Implementor);

            throw new InvalidOperationException($"Result Type '{typeof(TResult).Name}' must be '{nameof(HttpResponseMessage)}' and the value must not be null.");
        }
    }
}