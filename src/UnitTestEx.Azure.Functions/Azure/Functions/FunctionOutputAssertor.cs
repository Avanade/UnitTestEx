using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker.Http;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Reflection;
using UnitTestEx.Abstractions;
using UnitTestEx.Assertors;

namespace UnitTestEx.Azure.Functions
{
    /// <summary>
    /// Provides assertions for a function's multi-output return value.
    /// </summary>
    public sealed class FunctionOutputAssertor<TOutput>(TesterBase owner, TOutput result, IEnumerable<string?>? logs, Exception? exception)
        : AssertorBase<FunctionOutputAssertor<TOutput>>(owner, logs, exception)
    {
        /// <summary>
        /// Gets the complete function output.
        /// </summary>
        public TOutput Result { get; } = result;

        /// <summary>
        /// Gets a non-HTTP output property as a value assertor.
        /// </summary>
        public ValueAssertor<TValue> Output<TValue>(Expression<Func<TOutput, TValue>> selector)
        {
            AssertSuccess();
            ArgumentNullException.ThrowIfNull(selector);
            if (Result is null)
                throw new InvalidOperationException("The function returned a null multi-output result.");

            return new ValueAssertor<TValue>(Owner, selector.Compile()(Result), LogMessages, null);
        }

        /// <summary>
        /// Executes or converts the property marked with the worker <c>HttpResult</c> attribute for HTTP assertions.
        /// </summary>
        public HttpResponseMessageAssertor ToHttpResponseMessageAssertor(HttpRequest? httpRequest = null)
        {
            AssertSuccess();
            if (Result is null)
                throw new InvalidOperationException("The function returned a null multi-output result.");

            var properties = Result.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(p => p.GetCustomAttributesData().Any(a => a.AttributeType.Name == "HttpResultAttribute"))
                .ToArray();
            if (properties.Length != 1)
                throw new InvalidOperationException($"Expected exactly one public output property marked with the worker HttpResult attribute, found {properties.Length}.");

            var value = properties[0].GetValue(Result) ?? throw new InvalidOperationException("The function's HttpResult output is null.");
            if (value is HttpResponseData responseData)
            {
                var stopwatch = Stopwatch.StartNew();
                var response = responseData.ToHttpResponseMessage();
                stopwatch.Stop();
                Owner.LogHttpResponseMessage(response, stopwatch);
                return new HttpResponseMessageAssertor(Owner, LogMessages, response);
            }

            if (value is IActionResult actionResult)
                return ActionResultAssertor.ToHttpResponseMessageAssertor(Owner, actionResult, LogMessages, httpRequest);

            if (value is IResult result)
                return HttpResultAssertor.ToHttpResponseMessageAssertor(Owner, result, LogMessages, httpRequest);

            if (value is HttpResponseMessage responseMessage)
                return new HttpResponseMessageAssertor(Owner, LogMessages, responseMessage);

            throw new InvalidOperationException($"The function's HttpResult output type '{value.GetType().FullName}' is not supported.");
        }
    }
}
