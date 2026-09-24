// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Moq;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using UnitTestEx.Mocking;
using WireMock.Admin.Mappings;

namespace UnitTestEx.Aspire.HttpMock
{
    /// <summary>
    /// Represents the request-matching configuration for a stubbed <see cref="AspireHttpMockClient"/> mapping.
    /// </summary>
    /// <remarks>Implements the shared <see cref="IHttpMockRequest"/> abstraction (see that type's remarks) so that test-authoring code can be written once against the interface and
    /// reused identically regardless of which tier applied it.</remarks>
    public sealed class AspireHttpMockRequest : IHttpMockRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AspireHttpMockRequest"/> class.
        /// </summary>
        /// <param name="client">The owning <see cref="AspireHttpMockClient"/>.</param>
        /// <param name="method">The <see cref="HttpMethod"/> to match; where not specified any method will match.</param>
        /// <param name="requestUri">The relative request URI (path) to match (exact match); where not specified any path will match.</param>
        internal AspireHttpMockRequest(AspireHttpMockClient client, HttpMethod? method, string? requestUri)
        {
            Client = client ?? throw new ArgumentNullException(nameof(client));
            Rule = new RequestModel
            {
                Methods = method is null ? null : [method.Method],
                Path = requestUri
            };
        }

        /// <summary>
        /// Gets the owning <see cref="AspireHttpMockClient"/>.
        /// </summary>
        internal AspireHttpMockClient Client { get; }

        /// <summary>
        /// Gets the underlying <see cref="RequestModel"/> being built up.
        /// </summary>
        internal RequestModel Rule { get; }

        /// <summary>
        /// Gets the number of <see cref="Times"/> the request is expected to be invoked; used as the default for <see cref="AspireHttpMockedRequest.VerifyAsync"/> where not overridden there.
        /// </summary>
        internal Times? ExpectedTimes { get; private set; }

        /// <summary>
        /// Sets the number of <paramref name="times"/> that the request is expected to be invoked.
        /// </summary>
        /// <param name="times">The expected <see cref="Moq.Times"/>.</param>
        /// <returns>The <see cref="AspireHttpMockRequest"/> to support fluent-style method-chaining.</returns>
        /// <remarks>Where not set, <see cref="AspireHttpMockedRequest.VerifyAsync"/> will default to verifying at least one invocation (see <see cref="Moq.Times.AtLeastOnce()"/>).
        /// <para>Each time this is invoked it will override the previously set value.</para></remarks>
        public AspireHttpMockRequest Times(Times times)
        {
            ExpectedTimes = times;
            return this;
        }

        /// <summary>
        /// Indicates that the request will match regardless of the body content.
        /// </summary>
        /// <returns>The <see cref="AspireHttpMockRequestBody"/> to continue the fluent-style configuration.</returns>
        public AspireHttpMockRequestBody WithAnyBody() => new(this);

        /// <summary>
        /// Indicates that the request body must match the specified <paramref name="text"/> exactly.
        /// </summary>
        /// <param name="text">The exact body text to match.</param>
        /// <returns>The <see cref="AspireHttpMockRequestBody"/> to continue the fluent-style configuration.</returns>
        public AspireHttpMockRequestBody WithBody(string text)
        {
            Rule.Body = new BodyModel { Matcher = new MatcherModel { Name = "ExactMatcher", Pattern = text ?? throw new ArgumentNullException(nameof(text)) } };
            return new(this);
        }

        /// <summary>
        /// Indicates that the request body must match the specified <paramref name="text"/> exactly, with the specified <paramref name="mediaType"/> (via a Content-Type header match).
        /// </summary>
        /// <param name="text">The exact body text to match.</param>
        /// <param name="mediaType">The media type of the request.</param>
        /// <returns>The <see cref="AspireHttpMockRequestBody"/> to continue the fluent-style configuration.</returns>
        /// <remarks>Unlike <see cref="WithBody(string)"/>, which is media-type agnostic, this additionally adds a Content-Type header matcher (a wildcard match on <paramref name="mediaType"/>);
        /// this is the member used to satisfy the shared <see cref="IHttpMockRequest.WithBody(string, string)"/> abstraction, mirroring Tier 1's stricter (media-type checked) behaviour there.</remarks>
        public AspireHttpMockRequestBody WithBody(string text, string mediaType)
        {
            Rule.Body = new BodyModel { Matcher = new MatcherModel { Name = "ExactMatcher", Pattern = text ?? throw new ArgumentNullException(nameof(text)) } };
            Rule.Headers ??= [];
            Rule.Headers.Add(new HeaderModel
            {
                Name = "Content-Type",
                Matchers = [new MatcherModel { Name = "WildcardMatcher", Pattern = $"{mediaType ?? throw new ArgumentNullException(nameof(mediaType))}*" }]
            });

            return new(this);
        }

        /// <summary>
        /// Indicates that the request body must match the specified <paramref name="json"/> (using WireMock.Net's JSON comparison semantics).
        /// </summary>
        /// <param name="json">The JSON body to match.</param>
        /// <param name="pathsToIgnore">The simple (dot-separated, non-<see cref="Json.JsonElementComparer">JSONPath</see>) property paths to exclude from the match pattern - e.g. a
        /// generated <c>eTag</c> whose runtime value is unknown/unimportant.</param>
        /// <returns>The <see cref="AspireHttpMockRequestBody"/> to continue the fluent-style configuration.</returns>
        /// <remarks>See the <see cref="AspireHttpMockClient"/> remarks regarding WireMock.Net's JSON comparison semantics versus <see cref="Json.JsonElementComparer"/> (as used by, for
        /// example, <see cref="Assertors.HttpResponseMessageAssertor.AssertValue{TValue}(TValue, string[])"/>): they are <b>not</b> equivalent, and there is no semantic value coercion
        /// (e.g. dates, GUIDs) here. Where that parity matters, target a self-hosted WireMock.Net project resource (see <see cref="AspireTesterBase{TAppHost, TSelf}.HttpMock"/> remarks)
        /// and use <see cref="WithJsonBodyUsingUnitTestExComparer(string, string[])"/> instead.
        /// <para>Where <paramref name="pathsToIgnore"/> is specified, the named properties are removed from the match pattern <i>and</i> the underlying matcher switches from an exact,
        /// bidirectional <c>JsonMatcher</c> to WireMock.Net's own <c>JsonPartialMatcher</c> (a subset match): properties present in the pattern must still match exactly, but the
        /// ignored (removed) properties - and any other property not present in the pattern - are not checked at all, regardless of their value or even presence in the actual
        /// request body. This is looser than Tier 1's <c>pathsToIgnore</c> (which still requires an exact 1:1 property correspondence apart from the ignored paths) but is the
        /// pragmatic native tool WireMock.Net provides for this, since request matching is evaluated by the out-of-process WireMock.Net server itself - it has no access to
        /// <see cref="Json.JsonElementComparer"/> to delegate the decision back to.</para></remarks>
        public AspireHttpMockRequestBody WithJsonBody([StringSyntax(StringSyntaxAttribute.Json)] string json, params string[] pathsToIgnore)
        {
            ArgumentNullException.ThrowIfNull(json);

            if (pathsToIgnore is { Length: > 0 })
            {
                var node = JsonNode.Parse(json) ?? throw new ArgumentException("The json could not be parsed as a JSON object.", nameof(json));
                foreach (var path in pathsToIgnore)
                    RemovePath(node, path);

                Rule.Body = new BodyModel { Matcher = new MatcherModel { Name = "JsonPartialMatcher", Pattern = node.ToJsonString() } };
            }
            else
                Rule.Body = new BodyModel { Matcher = new MatcherModel { Name = "JsonMatcher", Pattern = json } };

            return new(this);
        }

        /// <summary>
        /// Removes the (simple, dot-separated) <paramref name="path"/> from the <paramref name="node"/>; silently does nothing where the path cannot be resolved (nothing to ignore).
        /// </summary>
        private static void RemovePath(JsonNode node, string path)
        {
            var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
                return;

            var current = node;
            for (var i = 0; i < segments.Length - 1; i++)
            {
                if (current is not JsonObject obj || !obj.TryGetPropertyValue(segments[i], out var next) || next is null)
                    return;

                current = next;
            }

            if (current is JsonObject parent)
                parent.Remove(segments[^1]);
        }

        /// <summary>
        /// Indicates that the request body must match the JSON serialized representation of the specified <paramref name="value"/> (using WireMock.Net's JSON comparison semantics).
        /// </summary>
        /// <typeparam name="T">The value <see cref="Type"/>.</typeparam>
        /// <param name="value">The value to serialize and match.</param>
        /// <param name="pathsToIgnore">The simple property paths to exclude from the match pattern; see <see cref="WithJsonBody(string, string[])"/> remarks.</param>
        /// <returns>The <see cref="AspireHttpMockRequestBody"/> to continue the fluent-style configuration.</returns>
        public AspireHttpMockRequestBody WithJsonBody<T>(T value, params string[] pathsToIgnore) => WithJsonBody(JsonSerializer.Serialize(value), pathsToIgnore);

        /// <summary>
        /// Indicates that the request body must match the specified <paramref name="json"/> using UnitTestEx's own <see cref="Json.JsonElementComparer"/> - i.e. the <b>same</b>
        /// comparison semantics as Tier 1 (including semantic value coercion for dates, GUIDs and numbers) - rather than WireMock.Net's own JSON comparison (see <see cref="WithJsonBody(string, string[])"/>).
        /// </summary>
        /// <param name="json">The JSON body to match.</param>
        /// <param name="pathsToIgnore">The simple (dot-separated) property paths to exclude from the match - see <see cref="Json.JsonElementComparer.Compare(string, string, string[])"/>;
        /// unlike <see cref="WithJsonBody(string, string[])"/>, this still requires an exact 1:1 property correspondence apart from the ignored paths (true parity with Tier 1), rather
        /// than switching to a looser subset/partial match.</param>
        /// <returns>The <see cref="AspireHttpMockRequestBody"/> to continue the fluent-style configuration.</returns>
        /// <remarks>Requires the target <see cref="AspireHttpMockClient"/> to have been resolved against a <i>self-hosted</i> WireMock.Net project resource (via
        /// <see cref="AspireTesterBase{TAppHost, TSelf}.HttpMock"/>, targeting a small sample project the consumer copies into their own solution - see UnitTestEx's README
        /// "Aspire multi-host testing" section) that has registered <see cref="JsonElementComparerMatcher"/> - the official <c>WireMock.Net.Aspire</c> package's container resource has
        /// no way to load this custom matcher type and will reject the mapping with a "Matcher 'JsonElementComparerMatcher' is not supported" error.
        /// <para>The tester's current <see cref="Abstractions.TesterBaseCore.JsonComparerOptions"/> - specifically <see cref="Json.JsonElementComparerOptions.ValueComparison"/>,
        /// <see cref="Json.JsonElementComparerOptions.NullComparison"/> and <see cref="Json.JsonElementComparerOptions.MaxDifferences"/> - is captured (as data, into the matcher's
        /// payload) at the point this method is called, since the matcher itself runs in a separate OS process (the self-hosted WireMock.Net server) with no access to this test
        /// process's live state; mutating <see cref="Json.JsonElementComparer.Default"/> or this tester's <see cref="AspireTesterBase{TAppHost, TSelf}.UseJsonComparerOptions"/>
        /// <b>after</b> calling this method has no effect on an already-configured mapping. Where strict, WireMock-style textual matching is instead wanted against a self-hosted
        /// resource, call <see cref="AspireTesterBase{TAppHost, TSelf}.UseJsonComparerOptions"/> with <see cref="Json.JsonElementComparerOptions.ValueComparison"/> set to
        /// <see cref="Json.JsonElementComparison.Exact"/> before calling this method, rather than falling back to <see cref="WithJsonBody(string, string[])"/>.</para></remarks>
        public AspireHttpMockRequestBody WithJsonBodyUsingUnitTestExComparer([StringSyntax(StringSyntaxAttribute.Json)] string json, params string[] pathsToIgnore)
        {
            ArgumentNullException.ThrowIfNull(json);

            var options = Client.JsonComparerOptions;
            var envelope = JsonSerializer.Serialize(new
            {
                Json = json,
                PathsToIgnore = pathsToIgnore,
                options.ValueComparison,
                options.NullComparison,
                options.MaxDifferences
            });
            Rule.Body = new BodyModel { Matcher = new MatcherModel { Name = JsonElementComparerMatcher.MatcherName, Pattern = envelope } };
            return new(this);
        }

        /// <summary>
        /// Indicates that the request body must match the JSON serialized representation of the specified <paramref name="value"/> using UnitTestEx's own <see cref="Json.JsonElementComparer"/>;
        /// see <see cref="WithJsonBodyUsingUnitTestExComparer(string, string[])"/> remarks.
        /// </summary>
        /// <typeparam name="T">The value <see cref="Type"/>.</typeparam>
        /// <param name="value">The value to serialize and match.</param>
        /// <param name="pathsToIgnore">The simple property paths to exclude from the match pattern; see <see cref="WithJsonBodyUsingUnitTestExComparer(string, string[])"/> remarks.</param>
        /// <returns>The <see cref="AspireHttpMockRequestBody"/> to continue the fluent-style configuration.</returns>
        public AspireHttpMockRequestBody WithJsonBodyUsingUnitTestExComparer<T>(T value, params string[] pathsToIgnore) => WithJsonBodyUsingUnitTestExComparer(JsonSerializer.Serialize(value), pathsToIgnore);

        /// <summary>
        /// Indicates that the request body must match the JSON formatted embedded resource content (using WireMock.Net's JSON comparison semantics).
        /// </summary>
        /// <typeparam name="TAssembly">The <see cref="Type"/> used to infer <see cref="Assembly"/> that contains the embedded resource.</typeparam>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualified resource name).</param>
        /// <param name="pathsToIgnore">The simple property paths to exclude from the match pattern; see <see cref="WithJsonBody(string, string[])"/> remarks.</param>
        /// <returns>The <see cref="AspireHttpMockRequestBody"/> to continue the fluent-style configuration.</returns>
        public AspireHttpMockRequestBody WithJsonResourceBody<TAssembly>(string resourceName, params string[] pathsToIgnore) => WithJsonResourceBody(resourceName, typeof(TAssembly).Assembly, pathsToIgnore);

        /// <summary>
        /// Indicates that the request body must match the JSON formatted embedded resource content (using WireMock.Net's JSON comparison semantics).
        /// </summary>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualified resource name).</param>
        /// <param name="assembly">The <see cref="Assembly"/> that contains the embedded resource; defaults to <see cref="Assembly.GetCallingAssembly"/>.</param>
        /// <param name="pathsToIgnore">The simple property paths to exclude from the match pattern; see <see cref="WithJsonBody(string, string[])"/> remarks.</param>
        /// <returns>The <see cref="AspireHttpMockRequestBody"/> to continue the fluent-style configuration.</returns>
        public AspireHttpMockRequestBody WithJsonResourceBody(string resourceName, Assembly? assembly = null, params string[] pathsToIgnore) => WithJsonBody(Resource.GetJson(resourceName, assembly ?? Assembly.GetCallingAssembly()), pathsToIgnore);

        /// <inheritdoc/>
        IHttpMockRequest IHttpMockRequest.Times(Times times) => Times(times);

        /// <inheritdoc/>
        IHttpMockRequestBody IHttpMockRequest.WithAnyBody() => WithAnyBody();

        /// <inheritdoc/>
        IHttpMockRequestBody IHttpMockRequest.WithBody(string body, string mediaType) => WithBody(body, mediaType);

        /// <inheritdoc/>
        IHttpMockRequestBody IHttpMockRequest.WithJsonBody(string json, params string[] pathsToIgnore) => WithJsonBody(json, pathsToIgnore);

        /// <inheritdoc/>
        IHttpMockRequestBody IHttpMockRequest.WithJsonBody<T>(T value, params string[] pathsToIgnore) => WithJsonBody(value, pathsToIgnore);
    }
}
