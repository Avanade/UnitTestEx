// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.AspNetCore.Mvc;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Reflection;
using UnitTestEx.Abstractions;

namespace UnitTestEx.Assertors
{
    /// <summary>
    /// Represents the test result assert helper.
    /// </summary>
    /// <typeparam name="TValue">The result value <see cref="Type"/>.</typeparam>
    /// <param name="owner">The owning <see cref="TesterBase"/>.</param>
    /// <param name="result">The result value.</param>
    /// <param name="exception">The <see cref="Exception"/> (if any).</param>
    public class ValueAssertor<TValue>(TesterBase owner, TValue result, Exception? exception) : AssertorBase<ValueAssertor<TValue>>(owner, exception)
    {
        /// <summary>
        /// Gets the result.
        /// </summary>
        public TValue Result { get; } = result;

        /// <summary>
        /// Asserts that the <see cref="Result"/> matches the <paramref name="expectedValue"/>.
        /// </summary>
        /// <param name="expectedValue">The expected value.</param>
        /// <param name="pathsToIgnore">The JSON paths to ignore from the comparison.</param>
        /// <returns>The <see cref="ValueAssertor{TValue}"/> to support fluent-style method-chaining.</returns>
        public ValueAssertor<TValue> AssertValue(TValue expectedValue, params string[] pathsToIgnore)
        {
            AssertSuccess();
            if (expectedValue is IComparable)
                Implementor.AssertAreEqual(expectedValue, Result);
            else
            {
                var cr = Owner.CreateJsonComparer().CompareValue(expectedValue, Result, pathsToIgnore);
                if (cr.HasDifferences)
                    Implementor.AssertFail($"Expected and Actual values are not equal:{Environment.NewLine}{cr}");
            }

            return this;
        }

        /// <summary>
        ///  Asserts that the <see cref="Result"/> matches the <paramref name="json"/> serialized value.
        /// </summary>
        /// <param name="json">The JSON <see cref="string"/>.</param>
        /// <param name="pathsToIgnore">The JSON paths to ignore from the comparison.</param>
        /// <returns>The <see cref="ValueAssertor{TValue}"/> to support fluent-style method-chaining.</returns>
#if NET7_0_OR_GREATER
        public ValueAssertor<TValue> AssertJson([StringSyntax(StringSyntaxAttribute.Json)] string json, params string[] pathsToIgnore) => AssertValue(JsonSerializer.Deserialize<TValue>(json)!, pathsToIgnore);
#else
        public ValueAssertor<TValue> AssertJson(string json, params string[] pathsToIgnore) => AssertValue(JsonSerializer.Deserialize<TValue>(json)!, pathsToIgnore);
#endif

        /// <summary>
        /// Asserts that the <see cref="Result"/> matches the JSON serialized value from the named embedded resource.
        /// </summary>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualifed resource name) that contains the expected value as serialized JSON.</param>
        /// <param name="pathsToIgnore">The JSON paths to ignore from the comparison.</param>
        /// <returns>The <see cref="ValueAssertor{TValue}"/> to support fluent-style method-chaining.</returns>
        public ValueAssertor<TValue> AssertJsonFromResource(string resourceName, params string[] pathsToIgnore)
            => AssertValue(Resource.GetJsonValue<TValue>(resourceName, Assembly.GetCallingAssembly(), JsonSerializer), pathsToIgnore);

        /// <summary>
        /// Asserts that the <see cref="Result"/> matches the JSON serialized value from the named embedded resource.
        /// </summary>
        /// <typeparam name="TAssembly">The <see cref="Type"/> to infer the <see cref="Assembly"/> that contains the embedded resource.</typeparam>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualifed resource name) that contains the expected value as serialized JSON.</param>
        /// <param name="pathsToIgnore">The JSON paths to ignore from the comparison.</param>
        /// <returns>The <see cref="ValueAssertor{TValue}"/> to support fluent-style method-chaining.</returns>
        public ValueAssertor<TValue> AssertJsonFromResource<TAssembly>(string resourceName, params string[] pathsToIgnore)
            => AssertValue(Resource.GetJsonValue<TValue>(resourceName, typeof(TAssembly).Assembly, JsonSerializer), pathsToIgnore);

        /// <summary>
        /// Converts the <see cref="ValueAssertor{TValue}"/> to an <see cref="ActionResultAssertor"/>.
        /// </summary>
        /// <returns>The corresponding <see cref="ActionResultAssertor"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown where the <see cref="Result"/> <see cref="Type"/> is not assignable from <see cref="IActionResult"/>.</exception>
        public ActionResultAssertor ToActionResultAssertor()
        {
            if (typeof(IActionResult).IsAssignableFrom(typeof(TValue)))
                return new ActionResultAssertor(Owner, (IActionResult)Result!, Exception);

            throw new InvalidOperationException($"Result Type '{typeof(TValue).Name}' must be assignable from '{nameof(IActionResult)}'");
        }

        /// <summary>
        /// Converts the <see cref="ValueAssertor{TValue}"/> to an <see cref="HttpResponseMessageAssertor"/>.
        /// </summary>
        /// <returns>The corresponding <see cref="HttpResponseMessageAssertor"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown where the <see cref="Result"/> <see cref="Type"/> is not <see cref="HttpResponseMessage"/>.</exception>
        public HttpResponseMessageAssertor ToHttpResponseMessageAssertor()
        {
            if (Result != null)
            {
                if (Result is HttpResponseMessage hrm)
                    return new HttpResponseMessageAssertor(Owner, hrm);

                if (Result is ActionResult ar)
                    return ActionResultAssertor.ToHttpResponseMessageAssertor(Owner, ar);
            }

            throw new InvalidOperationException($"Result Type '{typeof(TValue).Name}' must be either a '{nameof(HttpResponseMessage)}' or '{nameof(IActionResult)}', and the value must not be null.");
        }
    }
}