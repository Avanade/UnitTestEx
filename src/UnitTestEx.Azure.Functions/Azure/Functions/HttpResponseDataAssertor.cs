// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.Azure.Functions.Worker.Http;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using UnitTestEx.Abstractions;
using UnitTestEx.Assertors;

namespace UnitTestEx.Azure.Functions
{
    /// <summary>
    /// Represents the <see cref="HttpResponseData"/> (isolated worker) test assert helper; see <see cref="ToHttpResponseMessageAssertor"/> to perform the HTTP response assertions.
    /// </summary>
    /// <param name="owner">The owning <see cref="TesterBase"/>.</param>
    /// <param name="result">The <see cref="HttpResponseData"/>.</param>
    /// <param name="logs">The logs captured during execution.</param>
    /// <param name="exception">The <see cref="Exception"/> (if any).</param>
    public class HttpResponseDataAssertor(TesterBase owner, HttpResponseData result, IEnumerable<string?>? logs, Exception? exception) : AssertorBase<HttpResponseDataAssertor>(owner, logs, exception)
    {
        /// <summary>
        /// Gets the <see cref="HttpResponseData"/>.
        /// </summary>
        public HttpResponseData Result { get; } = result;

        /// <summary>
        /// Converts the <see cref="Result"/> to an <see cref="HttpResponseMessageAssertor"/> (see <see cref="ExtensionMethods.ToHttpResponseMessage(HttpResponseData)"/>).
        /// </summary>
        /// <returns>The corresponding <see cref="HttpResponseMessageAssertor"/>.</returns>
        public HttpResponseMessageAssertor ToHttpResponseMessageAssertor()
        {
            AssertSuccess();
            var sw = Stopwatch.StartNew();
            var hr = Result.ToHttpResponseMessage();
            sw.Stop();
            Owner.LogHttpResponseMessage(hr, sw);
            return new HttpResponseMessageAssertor(Owner, LogMessages, hr);
        }
    }
}
