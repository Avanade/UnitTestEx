// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.Extensions.Logging;
using WireMock.Settings;

namespace UnitTestEx.Aspire.HttpMock
{
    /// <summary>
    /// Extends <see cref="WireMockServerSettings"/> (deliberately left unsealed by WireMock.Net) with UnitTestEx-specific configuration for the self-hosted Aspire resource
    /// (see <see cref="Aspire.WireMockConsole.RunAsync"/>) - a single, strongly-typed extension point for settings that <c>WireMock.Net</c> itself has no concept of, rather than
    /// growing <see cref="Aspire.WireMockConsole.RunAsync"/>'s parameter list every time a further such setting is needed.
    /// </summary>
    public class AspireWireMockServerSettings : WireMockServerSettings
    {
        /// <summary>
        /// Gets or sets the <see cref="LogLevel"/> that <see cref="WireMockRequestResponseLogger"/> logs each stubbed request/response pair at - giving Tier 2/3 parity with Tier 1's
        /// <c>MockHttpClientHandler</c> request/response logging.
        /// </summary>
        /// <remarks>Defaults to <see cref="LogLevel.Information"/> - unlike Tier 1's default of <see cref="LogLevel.Debug"/> - since Tier 2/3 resource logging does not typically have
        /// Debug-level output enabled, and this is otherwise silently never seen. Set to <see cref="LogLevel.None"/> to disable this logging entirely. This is read live at the time each
        /// request/response pair is logged, so it may be changed at any point after <see cref="Aspire.WireMockConsole.RunAsync"/> constructs the settings - including from within the
        /// caller-supplied server factory itself (e.g. <c>settings =&gt; { settings.RequestResponseLogLevel = LogLevel.Debug; return WireMockServer.Start(settings); }</c>).</remarks>
        public LogLevel RequestResponseLogLevel { get; set; } = LogLevel.Information;
    }
}
