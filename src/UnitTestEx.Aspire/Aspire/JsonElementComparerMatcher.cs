// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System;
using AnyOfTypes;
using WireMock.Admin.Mappings;
using WireMock.Matchers;
using WireMock.Models;
using JsonSerializer = System.Text.Json.JsonSerializer;
using UnitTestExJsonElementComparer = UnitTestEx.Json.JsonElementComparer;

namespace UnitTestEx.Aspire
{
    /// <summary>
    /// A custom WireMock.Net <see cref="IStringMatcher"/> that delegates JSON body matching to UnitTestEx's own <see cref="UnitTestEx.Json.JsonElementComparer"/>, giving a
    /// self-hosted WireMock.Net project resource (see <see cref="AspireTesterBase{TAppHost, TSelf}"/> remarks and the README's "Aspire multi-host testing" section) genuine JSON
    /// comparison-semantics parity with Tier 1's in-process <c>MockHttpClient</c> - notably semantic value coercion for dates, GUIDs and numbers (e.g. <c>"2024-01-01T00:00:00Z"</c>
    /// and <c>"2024-01-01T00:00:00+00:00"</c> are considered equal) - which WireMock.Net's own <c>JsonMatcher</c>/<c>JsonPartialMatcher</c> (a textual/structural comparison) does
    /// not provide.
    /// </summary>
    /// <remarks>Register this against the well-known matcher name <see cref="MatcherName"/> via <see cref="WireMock.Settings.WireMockServerSettings.CustomMatcherMappings"/> in the
    /// self-hosted WireMock.Net process (see <c>UnitTestEx.Aspire.MockHost</c>'s <c>Program.cs</c> template); the corresponding client-side mapping is produced by
    /// <see cref="HttpMock.AspireHttpMockRequest.WithJsonBodyUsingUnitTestExComparer(string, string[])"/>, which encodes the JSON pattern, any <c>pathsToIgnore</c> and the calling
    /// test's <see cref="Json.JsonElementComparerOptions"/> (the comparison-affecting subset of it - see <see cref="Envelope"/>) as a small <see cref="Envelope"/> carried in
    /// <see cref="MatcherModel.Pattern"/> (WireMock.Net's admin API has no other extensibility point for passing matcher-specific configuration through to a
    /// <c>CustomMatcherMappings</c> factory). This matcher instance runs in the self-hosted WireMock.Net process - a genuinely separate OS process from the test - so it has no
    /// access to the test process's <see cref="Json.JsonElementComparer.Default"/>/<see cref="Abstractions.TesterBaseCore.JsonComparerOptions"/>; the carried subset of the latter
    /// is therefore used to build a dedicated <see cref="Json.JsonElementComparer"/> per mapping, rather than falling back to <see cref="Json.JsonElementComparer.Default"/>.
    /// <para>On a non-match caused by an unparseable request body (invalid JSON), a <see cref="JsonElementComparerMatcherException"/> is attached to the returned
    /// <see cref="WireMock.Matchers.MatchResult.Exception"/> - consistent with WireMock.Net's own built-in matchers' convention for a genuine evaluation failure. This is
    /// deliberately NOT done for an ordinary semantic mismatch (a completed comparison that simply scored zero): WireMock.Net's internal <c>MappingMatcher.FindBestMatch</c>
    /// treats ANY non-null <see cref="WireMock.Matchers.MatchResult.Exception"/> as "this mapping failed to evaluate" and excludes it entirely from partial/closest-match
    /// tracking, so attaching a per-mismatch diagnostic there would silently break WireMock.Net's own "closest match" debugging (visible via its admin API/dashboard) rather
    /// than improve it. See <see cref="IsMatch"/> for details.</para></remarks>
    public sealed class JsonElementComparerMatcher : IStringMatcher
    {
        /// <summary>
        /// Gets the well-known matcher name used for both the <see cref="WireMock.Settings.WireMockServerSettings.CustomMatcherMappings"/> registration key (host-side) and the
        /// <see cref="MatcherModel.Name"/> value (client-side).
        /// </summary>
        public const string MatcherName = nameof(JsonElementComparerMatcher);

        private readonly Envelope _envelope;
        private readonly UnitTestExJsonElementComparer _comparer;

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonElementComparerMatcher"/> class from the <paramref name="matcherModel"/> supplied by WireMock.Net's
        /// <c>CustomMatcherMappings</c> factory callback.
        /// </summary>
        /// <param name="matcherModel">The <see cref="MatcherModel"/> received via the admin API mapping request.</param>
        public JsonElementComparerMatcher(MatcherModel matcherModel)
        {
            ArgumentNullException.ThrowIfNull(matcherModel);
            var pattern = matcherModel.Pattern?.ToString() ?? throw new ArgumentException($"The {MatcherName} matcher requires a Pattern containing the encoded {nameof(Envelope)}.", nameof(matcherModel));
            _envelope = JsonSerializer.Deserialize<Envelope>(pattern) ?? throw new ArgumentException($"The {MatcherName} matcher's Pattern could not be deserialized as a {nameof(Envelope)}.", nameof(matcherModel));
            MatchBehaviour = matcherModel.RejectOnMatch == true ? MatchBehaviour.RejectOnMatch : MatchBehaviour.AcceptOnMatch;

            // Rebuild (rather than use UnitTestExJsonElementComparer.Default) as this matcher runs in a separate OS process from the test that configured it - see remarks above.
            _comparer = new UnitTestExJsonElementComparer(new UnitTestEx.Json.JsonElementComparerOptions
            {
                ValueComparison = _envelope.ValueComparison,
                NullComparison = _envelope.NullComparison,
                MaxDifferences = _envelope.MaxDifferences
            });
        }

        /// <inheritdoc/>
        public string Name => MatcherName;

        /// <inheritdoc/>
        public MatchBehaviour MatchBehaviour { get; }

        /// <inheritdoc/>
        public MatchOperator MatchOperator => MatchOperator.Or;

        /// <inheritdoc/>
        public MatchResult IsMatch(string? input)
        {
            bool isMatch;
            try
            {
                var result = _comparer.Compare(_envelope.Json, input ?? "null", _envelope.PathsToIgnore ?? []);
                isMatch = result.AreEqual;
            }
            catch (ArgumentException ex)
            {
                // The input (or, less likely, the configured pattern) is not valid JSON. Unlike an ordinary non-match (a completed comparison that simply scored zero), this is a
                // genuine evaluation failure - consistent with WireMock.Net's own built-in matchers (e.g. JsonMatcher), attach it as the MatchResult's Exception. Note this is
                // deliberately NOT done for an ordinary semantic mismatch: WireMock.Net's MappingMatcher.FindBestMatch treats ANY non-null MatchResult.Exception as "this mapping
                // failed to evaluate" - it excludes the mapping entirely from partial/closest-match tracking (so it never appears via the admin API's request log) and instead
                // only logs the exception via WireMock.Net's own internal error logger. Attaching a diagnostic exception on every ordinary mismatch would therefore silently
                // break WireMock.Net's built-in "closest match" debugging experience rather than improve it.
                return MatchResult.From(Name, new JsonElementComparerMatcherException("The request body is not valid JSON.", ex));
            }

            return MatchResult.From(Name, MatchBehaviour, isMatch);
        }

        /// <inheritdoc/>
        public AnyOf<string, StringPattern>[] GetPatterns() => [new(_envelope.Json)];

        /// <inheritdoc/>
        public string GetCSharpCodeArguments() => $"new {typeof(JsonElementComparerMatcher).FullName}(/* not supported for this matcher */)";

        /// <summary>
        /// The small payload carried in <see cref="MatcherModel.Pattern"/> (as a serialized JSON string) that lets the client pass the JSON pattern to match, the <c>pathsToIgnore</c>,
        /// and the comparison-affecting subset of the calling test's <see cref="Json.JsonElementComparerOptions"/> through WireMock.Net's admin API, which otherwise has no dedicated
        /// field for custom matcher configuration.
        /// </summary>
        /// <param name="Json">The JSON pattern to match against.</param>
        /// <param name="PathsToIgnore">The simple (dot-separated) property paths to exclude from the comparison; see <see cref="UnitTestEx.Json.JsonElementComparer.Compare(string, string, string[])"/>.</param>
        /// <param name="ValueComparison">The <see cref="Json.JsonElementComparerOptions.ValueComparison"/> at the point the mapping was configured.</param>
        /// <param name="NullComparison">The <see cref="Json.JsonElementComparerOptions.NullComparison"/> at the point the mapping was configured.</param>
        /// <param name="MaxDifferences">The <see cref="Json.JsonElementComparerOptions.MaxDifferences"/> at the point the mapping was configured.</param>
        public sealed record Envelope(string Json, string[]? PathsToIgnore, Json.JsonElementComparison ValueComparison, Json.JsonElementComparison NullComparison, int MaxDifferences);
    }

    /// <summary>
    /// Represents a <see cref="JsonElementComparerMatcher"/> evaluation failure - specifically, that the request body could not be parsed as JSON. This is attached to
    /// <see cref="WireMock.Matchers.MatchResult.Exception"/> only for that genuine failure case (never for an ordinary semantic mismatch - see <see cref="JsonElementComparerMatcher.IsMatch"/>
    /// remarks), consistent with how WireMock.Net's own built-in matchers use <see cref="WireMock.Matchers.MatchResult.Exception"/>.
    /// </summary>
    public class JsonElementComparerMatcherException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="JsonElementComparerMatcherException"/> class with a specified message.
        /// </summary>
        /// <param name="message">The message text.</param>
        public JsonElementComparerMatcherException(string? message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonElementComparerMatcherException"/> class with a specified message and inner exception.
        /// </summary>
        /// <param name="message">The message text.</param>
        /// <param name="innerException">The inner <see cref="Exception"/>.</param>
        public JsonElementComparerMatcherException(string? message, Exception innerException) : base(message, innerException) { }
    }
}
