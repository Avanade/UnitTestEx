// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.Extensions.Logging;
using System;
using WireMock.Admin.Requests;
using WireMock.Logging;

namespace UnitTestEx.Aspire.HttpMock
{
    /// <summary>
    /// A <see cref="IWireMockLogger"/> that logs each non-admin (i.e. genuine stubbed traffic, not the admin API calls used to configure/query mappings) request/response pair to the
    /// console, giving Tier 2/3 parity with Tier 1's <c>MockHttpClientHandler</c>, which logs the same "sending request"/"received response" pair for every mocked <see cref="System.Net.Http.HttpClient"/>
    /// call it intercepts.
    /// </summary>
    /// <remarks>Registered against <see cref="WireMock.Settings.WireMockServerSettings.Logger"/> by <see cref="WireMockConsole.RunAsync"/>, holding a live reference to the
    /// <see cref="AspireWireMockServerSettings"/> it was constructed against so that <see cref="AspireWireMockServerSettings.RequestResponseLogLevel"/> is read fresh at the time of each
    /// request/response pair, rather than a value copied once up-front. Since the self-hosted WireMock.Net server is a genuinely separate OS process (see the README's "Aspire multi-host
    /// testing" section), there is no in-process <see cref="ILogger"/> to write through directly - instead, each entry is written to the console formatted to match the standard ASP.NET
    /// Core console-logger header/message convention (e.g. <c>"info: WireMockRequestResponseLogger[0]"</c> followed by an indented message line) so that
    /// <see cref="Aspire.AspireTesterBase{TAppHost, TSelf}"/>'s resource log capture - which already parses that exact convention to recover a real <see cref="Microsoft.Extensions.Logging.LogLevel"/>
    /// per entry for <see cref="Aspire.AspireTesterBase{TAppHost, TSelf}.ErrorWhenLogContains(LogLevel, string[], string[])"/> - attributes the configured level correctly rather than falling
    /// back to a generic, unattributed passthrough line.
    /// <para>The general-purpose <see cref="IWireMockLogger.Debug(string, object[])"/>/<see cref="IWireMockLogger.Info(string, object[])"/>/<see cref="IWireMockLogger.Warn(string, object[])"/>/
    /// <c>Error</c> members are WireMock.Net's own internal diagnostic logging (server start-up, admin API activity, etc.) - deliberately left as no-ops here to keep this focused solely on
    /// request/response traffic and avoid otherwise-unactionable noise; assign a different <see cref="WireMock.Settings.WireMockServerSettings.Logger"/> instead where that diagnostic output is
    /// also wanted.</para></remarks>
    public sealed class WireMockRequestResponseLogger(AspireWireMockServerSettings settings) : IWireMockLogger
    {
        private readonly AspireWireMockServerSettings _settings = settings ?? throw new ArgumentNullException(nameof(settings));

        /// <inheritdoc/>
        void IWireMockLogger.Debug(string formatString, params object[] args) { /* WireMock.Net's own internal diagnostics; deliberately not surfaced - see remarks. */ }

        /// <inheritdoc/>
        void IWireMockLogger.Info(string formatString, params object[] args) { /* WireMock.Net's own internal diagnostics; deliberately not surfaced - see remarks. */ }

        /// <inheritdoc/>
        void IWireMockLogger.Warn(string formatString, params object[] args) { /* WireMock.Net's own internal diagnostics; deliberately not surfaced - see remarks. */ }

        /// <inheritdoc/>
        void IWireMockLogger.Error(string formatString, params object[] args) { /* WireMock.Net's own internal diagnostics; deliberately not surfaced - see remarks. */ }

        /// <inheritdoc/>
        void IWireMockLogger.Error(string message, Exception exception) { /* WireMock.Net's own internal diagnostics; deliberately not surfaced - see remarks. */ }

        /// <inheritdoc/>
        /// <remarks>Ignores <paramref name="isAdminRequest"/> entries (mapping registration/reset/verification calls issued by <see cref="AspireHttpMockClient"/> itself) - only genuine stubbed
        /// traffic (an application's real request being matched/responded to) is logged.</remarks>
        void IWireMockLogger.DebugRequestResponse(LogEntryModel logEntryModel, bool isAdminRequest)
        {
            var logLevel = _settings.RequestResponseLogLevel;
            if (isAdminRequest || logLevel == LogLevel.None)
                return;

            ArgumentNullException.ThrowIfNull(logEntryModel);

            if (logEntryModel.Request is not null)
                Log(logLevel, $"UnitTestEx > Sending HTTP request {logEntryModel.Request.Method} {logEntryModel.Request.Path} {LogContent(logEntryModel.Request.Body)}");

            if (logEntryModel.Response is not null)
                Log(logLevel, $"UnitTestEx > Received HTTP response {logEntryModel.Response.StatusCode} {LogContent(logEntryModel.Response.Body)}");
        }

        /// <summary>
        /// Writes <paramref name="message"/> to the console as a standard ASP.NET Core console-logger header/message pair at <paramref name="logLevel"/>.
        /// </summary>
        private static void Log(LogLevel logLevel, string message)
        {
            Console.WriteLine($"{Abbreviate(logLevel)}: {nameof(WireMockRequestResponseLogger)}[0]");
            Console.WriteLine($"      {message}");
        }

        /// <summary>
        /// Maps a <see cref="LogLevel"/> to the standard ASP.NET Core console-logger's own abbreviation convention (the exact convention <see cref="Aspire.AspireTesterBase{TAppHost, TSelf}"/>'s
        /// resource log capture parses back into a <see cref="LogLevel"/>).
        /// </summary>
        private static string Abbreviate(LogLevel level) => level switch
        {
            LogLevel.Trace => "trce",
            LogLevel.Debug => "dbug",
            LogLevel.Warning => "warn",
            LogLevel.Error => "fail",
            LogLevel.Critical => "crit",
            _ => "info"
        };

        /// <summary>
        /// Logs (and formats) the content.
        /// </summary>
        private static string LogContent(string? body) => string.IsNullOrEmpty(body) ? "No content." : body;
    }
}
