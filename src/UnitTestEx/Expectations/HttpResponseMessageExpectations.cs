// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using UnitTestEx.Abstractions;

namespace UnitTestEx.Expectations
{
    /// <summary>
    /// Provides <see cref="HttpResponseMessage"/> expectations.
    /// </summary>
    /// <param name="owner">The owning <see cref="TesterBase"/>.</param>
    /// <param name="tester">The initiating tester.</param>
    public class HttpResponseMessageExpectations<TTester>(TesterBase owner, TTester tester) : ExpectationsBase<TTester>(owner, tester)
    {
        private HttpStatusCode? _httpStatusCode;

        /// <inheritdoc/>
        public override string Title => "HTTP Response Message expectations";

        /// <summary>
        /// Expects that the <see cref="HttpResponseMessage.StatusCode"/> is equal to the <paramref name="httpStatusCode"/>.
        /// </summary>
        /// <param name="httpStatusCode">The <see cref="HttpStatusCode"/>.</param>
        public void SetExpectStatusCode(HttpStatusCode httpStatusCode) => _httpStatusCode = httpStatusCode;

        /// <inheritdoc/>
        protected override Task OnAssertAsync(AssertArgs args)
        {
            if (!args.TryGetExtra<HttpResponseMessage>(out var result))
                throw new InvalidOperationException($"The '{nameof(HttpResponseMessage)}' Extra value must be set for this expectation assertion to function.");

            if (_httpStatusCode.HasValue && result!.StatusCode != _httpStatusCode)
                args.Tester.Implementor.AssertFail($"Expected StatusCode value of '{_httpStatusCode.Value} ({(int)_httpStatusCode.Value})'; actual was '{result.StatusCode} ({(int)result.StatusCode})'.");

            return Task.CompletedTask;
        }
    }
}