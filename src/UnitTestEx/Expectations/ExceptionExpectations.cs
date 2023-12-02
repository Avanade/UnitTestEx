// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System;
using System.Net.Http;
using System.Threading.Tasks;
using UnitTestEx.Abstractions;

namespace UnitTestEx.Expectations
{
    /// <summary>
    /// Provides <see cref="Exception"/> expectations.
    /// </summary>
    /// <param name="owner">The owning <see cref="TesterBase"/>.</param>
    /// <param name="tester">The initiating tester.</param>
    public class ExceptionExpectations<TTester>(TesterBase owner, TTester tester) : ExpectationsBase<TTester>(owner, tester)
    {
        /// <inheritdoc/>
        public override int Order => int.MinValue;

        /// <summary>
        /// Indicates that the expectation is that the execution is successful.
        /// </summary>
        public bool ExpectSuccess { get; set; }

        /// <summary>
        /// Indicates that the expectation is that an <see cref="Exception"/> will be thrown during execution.
        /// </summary>
        public bool ExpectException { get; private set; }

        /// <summary>
        /// Gets the optional <see cref="Exception"/> <see cref="Type"/>.
        /// </summary>
        public Type? ExceptionType { get; private set; }

        /// <summary>
        /// Gets the optional expected message to match (contains).
        /// </summary>
        public string? ExceptionMessage { get; private set; }

        /// <summary>
        /// Expects that an <see cref="Exception"/> will be thrown during execution.
        /// </summary>
        /// <param name="exceptionType">The optional <see cref="Exception"/> <see cref="Type"/>.</param>
        /// <param name="expectedMessage">The optional expected message to match (contains).</param>
        public void SetExpectException(Type? exceptionType, string? expectedMessage)
        {
            ExpectException = true;
            ExceptionType = exceptionType;
            ExceptionMessage = expectedMessage;
        }

        /// <inheritdoc/>
        protected override Task OnAssertAsync(AssertArgs args)
        {
            if (ExpectException)
            {
                if (args.Exception is null)
                    args.Tester.Implementor.AssertFail("Expected an exception; however, the execution was successful.");
                else
                {
                    if (ExceptionType is not null && ExceptionType != args.Exception.GetType())
                        args.Tester.Implementor.AssertFail($"Expected Exception type '{ExceptionType.Name}' not equal to actual '{args.Exception.GetType().Name}'.");

                    if (ExceptionMessage is not null && !args.Exception.Message.Contains(ExceptionMessage, StringComparison.InvariantCultureIgnoreCase))
                        args.Tester.Implementor.AssertFail($"Expected Exception message '{ExceptionMessage}' is not contained within '{args.Exception.Message}'.");
                }
            }

            if (ExpectSuccess)
            {
                if (args.Exception is not null)
                    args.Tester.Implementor.AssertFail($"Expected success; however, '{args.Exception.GetType().Name}' was thrown: {args.Exception.Message}");

                if (args.TryGetExtra<HttpResponseMessage>(out var result) && result is not null && !result.IsSuccessStatusCode)
                    args.Tester.Implementor.AssertFail($"Expected success; however, the {nameof(HttpResponseMessage)} has an unsuccessful {nameof(HttpResponseMessage.StatusCode)} of {result.StatusCode} ({(int)result.StatusCode}).");
            }

            return Task.CompletedTask;
        }
    }
}