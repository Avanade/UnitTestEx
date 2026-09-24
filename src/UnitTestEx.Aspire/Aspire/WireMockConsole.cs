// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WireMock.Admin.Mappings;
using WireMock.Matchers;
using WireMock.Server;
using WireMock.Settings;

namespace UnitTestEx.Aspire
{
    /// <summary>
    /// Encapsulates the standard boilerplate for UnitTestEx's recommended self-hosted WireMock.Net Aspire project resource (see the README's "Aspire multi-host testing" section
    /// and <c>UnitTestEx.Aspire.MockHost</c>'s <c>Program.cs</c>) - reading the <c>PORT</c> environment variable Aspire assigns via <c>WithHttpEndpoint(env: "PORT")</c>, registering
    /// <see cref="JsonElementComparerMatcher"/> as a custom matcher, invoking the caller-supplied factory to start the actual WireMock.Net server, then blocking gracefully until
    /// Aspire stops the process (Ctrl+C locally, SIGTERM in orchestration) before disposing it. This turns the console app's <c>Program.cs</c> into essentially a one-liner:
    /// <code>await WireMockConsole.RunAsync(settings =&gt; WireMockServer.Start(settings));</code>
    /// </summary>
    public static class WireMockConsole
    {
        /// <summary>
        /// Reads the <c>PORT</c> environment variable, builds a <see cref="WireMockServerSettings"/> with <see cref="JsonElementComparerMatcher"/> registered against
        /// <see cref="JsonElementComparerMatcher.MatcherName"/>, invokes <paramref name="server"/> to start the actual WireMock.Net server, then blocks until the process is asked
        /// to shut down (Ctrl+C or SIGTERM), disposing the returned <see cref="IWireMockServer"/> before returning.
        /// </summary>
        /// <param name="server">A factory that starts and returns the <see cref="IWireMockServer"/> - typically <c>settings =&gt; WireMockServer.Start(settings)</c>. The caller's
        /// own project must reference the full <c>WireMock.Net</c> (or <c>WireMock.Net.StandAlone</c>) package for <c>WireMock.Server.WireMockServer</c> itself; this method
        /// only depends on <c>WireMock.Net.Abstractions</c>' <see cref="IWireMockServer"/>/<see cref="WireMockServerSettings"/>, which <c>UnitTestEx.Aspire</c> already brings in.</param>
        /// <param name="cancellationToken">An optional additional <see cref="CancellationToken"/> that also triggers shutdown (e.g. to host this in-process for a test); shutdown
        /// otherwise occurs on Ctrl+C or process exit.</param>
        public static async Task RunAsync(Func<WireMockServerSettings, IWireMockServer> server, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(server);

            var portText = Environment.GetEnvironmentVariable("PORT") ?? throw new InvalidOperationException("The 'PORT' environment variable must be set (expected to be supplied by Aspire's WithHttpEndpoint(env: \"PORT\")).");
            if (!int.TryParse(portText, out var port))
                throw new InvalidOperationException($"The 'PORT' environment variable value '{portText}' is not a valid port number.");

            var settings = new WireMockServerSettings
            {
                Port = port,
                StartAdminInterface = true,
                CustomMatcherMappings = new Dictionary<string, Func<MatcherModel, IMatcher>>
                {
                    [JsonElementComparerMatcher.MatcherName] = model => new JsonElementComparerMatcher(model)
                }
            };

            using var wireMockServer = server(settings);

            Console.WriteLine($"{nameof(WireMockConsole)}: WireMock.Net server listening on port {port} (custom matcher '{JsonElementComparerMatcher.MatcherName}' registered).");

            // Block until Aspire stops the process (Ctrl+C locally, SIGTERM in orchestration) rather than a bare Thread.Sleep(Timeout.Infinite), so shutdown is graceful (the
            // 'using' above still disposes/stops the WireMock.Net server) rather than relying purely on the OS killing the process.
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
            AppDomain.CurrentDomain.ProcessExit += (_, _) => cts.Cancel();

            try
            {
                await Task.Delay(Timeout.Infinite, cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown.
            }
        }
    }
}
