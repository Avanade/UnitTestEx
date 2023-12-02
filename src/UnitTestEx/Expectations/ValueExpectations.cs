// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using UnitTestEx.Abstractions;
using UnitTestEx.Json;

namespace UnitTestEx.Expectations
{
    /// <summary>
    /// Provides value expectations.
    /// </summary>
    /// <param name="owner">The owning <see cref="TesterBase"/>.</param>
    /// <param name="tester">The initiating tester.</param>
    public class ValueExpectations<TTester>(TesterBase owner, TTester tester) : ExpectationsBase<TTester>(owner, tester)
    {
        private const string NullJson = "null";
        private Func<TTester, string>? _json;
        private bool _expectNull;

        /// <summary>
        /// Expects that the result JSON compares to the expected <paramref name="json"/>.
        /// </summary>
        /// <param name="json">The expected JSON function.</param>
        public void SetExpectJson(Func<TTester, string> json) => _json = json ?? throw new ArgumentNullException(nameof(json));

        /// <summary>
        /// Expects the the result JSON is <c>null</c>.
        /// </summary>
        public void SetExpectNull() => _expectNull = true;

        /// <summary>
        /// Indicates whether the expected value was matched.
        /// </summary>
        /// <remarks>This must be set to <c>true</c> once matched; otherwise, the <see cref="OnLastAssertAsync(AssertArgs)"/> will assert a failure.</remarks>
        public bool ValueMatched { get; private set; }

        /// <inheritdoc/>
        protected async override Task OnAssertAsync(AssertArgs args)
        {
            if (_json is null)
                return; // No value configured to compare.

            JsonElementComparerResult? jcr;
            var jc = new JsonElementComparer(new JsonElementComparerOptions { JsonSerializer = args.Tester.JsonSerializer });

            string expectedJson = _expectNull ? NullJson : (_json?.Invoke(Tester) ?? NullJson);

            // Where there is an explicit value, serialize and compare.
            if (args.HasValue)
                jcr = jc.Compare(expectedJson, args.Tester.JsonSerializer.Serialize(args.HasValue ? args.Value : null), args.PathsToIgnore.ToArray());
            else
            {
                // Where there is no explicit value, see if there is an HTTP response.
                if (!args.TryGetExtra<HttpResponseMessage>(out var result) || result is null)
                    return;

                if (!result.IsSuccessStatusCode)
                {
                    args.Tester.Implementor.AssertFail($"Expected value; however, the {nameof(HttpResponseMessage)} has an unsuccessful {nameof(HttpResponseMessage.StatusCode)} of {result.StatusCode} ({(int)result.StatusCode}).");
                    return;
                }

                jcr = jc.Compare(expectedJson, result.Content.Headers.ContentLength == 0 ? NullJson : await result.Content.ReadAsStringAsync().ConfigureAwait(false), args.PathsToIgnore.ToArray());
            }

            // Assert the differences.
            if (jcr.HasDifferences)
                args.Tester.Implementor.AssertFail($"Expected and Actual values are not equal:{Environment.NewLine}{jcr}");
            else
                ValueMatched = true;
        }

        /// <inheritdoc/>
        protected override Task OnLastAssertAsync(AssertArgs args)
        {
            if (!ValueMatched)
                args.Tester.Implementor.AssertFail($"Expected value; however, none was returned.");

            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public override void Reset()
        {
            base.Reset();
            ValueMatched = false;
        }
    }
}