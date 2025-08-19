// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using UnitTestEx.Abstractions;
using UnitTestEx.Assertors;

namespace UnitTestEx.Expectations
{
    /// <summary>
    /// Provides error expectations.
    /// </summary>
    /// <param name="owner">The owning <see cref="TesterBase"/>.</param>
    /// <param name="tester">The initiating tester.</param>
    public class ErrorExpectations<TTester>(TesterBase owner, TTester tester) : ExpectationsBase<TTester>(owner, tester)
    {
        /// <inheritdoc/>
        public override string Title => "Error expectations";

        /// <summary>
        /// Gets or sets the expected error (contains) message (as distinct from the <see cref="Errors"/>).
        /// </summary>
        public string? Error { get; set; }

        /// <summary>
        /// Gets the underlying expected errors (as distinct from the <see cref="Error"/>).
        /// </summary>
        public List<ApiError> Errors { get; } = [];

        /// <summary>
        /// Indicates whether the <see cref="Error"/> and <see cref="Errors"/> were matched.
        /// </summary>
        /// <remarks>This must be set to <c>true</c> once matched; otherwise, the <see cref="OnLastAssertAsync(AssertArgs)"/> will assert a failure.</remarks>
        public bool ErrorsMatched { get; set; }

        /// <inheritdoc/>
        protected async override Task OnAssertAsync(AssertArgs args)
        {
            // Only continue where the HTTP response is not null and not successful.
            if (!args.TryGetExtra<HttpResponseMessage>(out var result) || result is null || result.IsSuccessStatusCode)
                return;

            // Get the underlying response content.
            var content = result.Content.Headers.ContentLength == 0 ? null : await result.Content.ReadAsStringAsync().ConfigureAwait(false);

            // Assert error message expectation.
            if (Error is not null)
            {
                if (string.IsNullOrEmpty(content) || !content.Contains(Error, StringComparison.InvariantCultureIgnoreCase))
                {
                    args.Tester.Implementor.AssertFail($"Expected error message to contain '{Error}'; actual error message '{content}'.");
                    return;
                }
            }

            // Assert errors expectation.
            if (Errors.Count > 0 && content is not null)
            {
                ApiError[] actual = [];

                try
                {
                    var errors = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string[]>>(content);
                    actual = Assertor.ConvertToApiErrors(errors);
                }
                catch { } // Swallow any deserialization errors.

                if (actual.Length > 0)
                {
                    if (!Assertor.TryAreErrorsMatched(Errors, actual, out var errorMessage))
                        args.Tester.Implementor.AssertFail(errorMessage);

                    ErrorsMatched = true;
                }
            }
            else
                ErrorsMatched = Error is not null;
        }

        /// <inheritdoc/>
        protected override Task OnLastAssertAsync(AssertArgs args)
        {
            // Check error vs exception message as an alternate.
            if (!ErrorsMatched && Error is not null && args.Exception is not null)
            {
                if (!args.Exception.Message.Contains(Error, StringComparison.InvariantCultureIgnoreCase))
                    args.Tester.Implementor.AssertFail($"Expected error message to contain '{Error}'; actual error message '{args.Exception.Message}'.");

                ErrorsMatched = true;
            }

            // Where no matching occured (allows for extensions) and there were extensions then we have not met the expectations.
            if (!ErrorsMatched && (Error is not null || Errors.Count > 0))
                args.Tester.Implementor.AssertFail($"Expected one or more errors; however, none were returned.");

            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public override void Reset()
        {
            base.Reset();
            ErrorsMatched = false;
        }
    }
}