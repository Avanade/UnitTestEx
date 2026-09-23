// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Mime;
using System.Threading.Tasks;
using UnitTestEx.Expectations;
using UnitTestEx.Json;
using UnitTestEx.Logging;

namespace UnitTestEx.Abstractions
{
    /// <summary>
    /// Provides the host-agnostic unit-testing capabilities that are common to <i>every</i> tester, irrespective of whether the underlying test target is a single in-process host
    /// (see <see cref="TesterBase"/>) or a multi-host distributed application (e.g. .NET Aspire).
    /// </summary>
    /// <remarks>This intentionally does <b>not</b> expose any dependency injection (DI) related capabilities (e.g. <c>Services</c>, <c>Configuration</c>, <c>ConfigureServices</c>) as these are meaningful only
    /// where there is a single host with a single DI container; see <see cref="TesterBase"/> for these capabilities. Where a capability is common in spirit, but cannot be expressed here without a DI 
    /// dependency, it should be exposed via an interface (e.g. <see cref="IHttpClientSource"/>) that each tier can implement appropriately.</remarks>
    public abstract class TesterBaseCore
    {
        private readonly List<Action> _hostStart = [];

        /// <summary>
        /// Static constructor.
        /// </summary>
        static TesterBaseCore()
        {
            TestSetUp.Force();

            try
            {
                var fi = new FileInfo(Path.Combine(Environment.CurrentDirectory, "appsettings.unittest.json"));
                if (!fi.Exists)
                    return;

                var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(fi.FullName));
                if (json.RootElement.TryGetProperty("DefaultJsonSerializer", out var je) && je.ValueKind == System.Text.Json.JsonValueKind.String)
                    TestSetUp.Default.JsonSerializer = (IJsonSerializer)Activator.CreateInstance(Type.GetType(je.GetString()!)!)!;
            }
            catch (Exception ex)
            {
                // Swallow and carry on; none of this logic should impact execution.
                System.Diagnostics.Debug.WriteLine($"UnitTestEx attempted to read, then load (if specified) the 'DefaultJsonSerializer' from, 'appsettings.unittest.json': {ex}.");
            }
        }

        /// <summary>
        /// Gets the default JSON media type names used for JSON serialization/deserialization.
        /// </summary>
        public static string[] JsonMediaTypeNames { get; set; } = [MediaTypeNames.Application.Json, "application/json-patch+json", "application/problem+json", "application/merge-patch+json"];

        /// <summary>
        /// Initializes a new instance of the <see cref="TesterBaseCore"/> class.
        /// </summary>
        /// <param name="implementor">The <see cref="TestFrameworkImplementor"/>.</param>
        protected TesterBaseCore(TestFrameworkImplementor implementor)
        {
            Implementor = implementor ?? throw new ArgumentNullException(nameof(implementor));
            LoggerProvider = new SharedStateLoggerProvider(SharedState);
            SetUp = TestSetUp.Default.Clone();
            JsonSerializer = SetUp.JsonSerializer;
            JsonComparerOptions = SetUp.JsonComparerOptions;
        }

        /// <summary>
        /// Gets the <see cref="TestFrameworkImplementor"/>.
        /// </summary>
        public TestFrameworkImplementor Implementor { get; private set; }

        /// <summary>
        /// Gets the <see cref="SharedStateLoggerProvider"/> <see cref="ILoggerProvider"/>.
        /// </summary>
        public SharedStateLoggerProvider LoggerProvider { get; }

        /// <summary>
        /// Gets the <see cref="TestSharedState"/>.
        /// </summary>
        public TestSharedState SharedState { get; } = new TestSharedState();

        /// <summary>
        /// Gets the configured <see cref="TestSetUp"/>. 
        /// </summary>
        /// <remarks>Defaults to <see cref="TestSetUp.Default"/>.</remarks>
        public TestSetUp SetUp { get; internal set; }

        /// <summary>
        /// Indicates whether the underlying host has been instantiated.
        /// </summary>
        /// <remarks>The host can be reset by invoking <see cref="ResetHost"/> (or, where supported, <see cref="TesterBase{TSelf}.ResetHost(bool)"/>).</remarks>
        public bool IsHostInstantiated { get; internal set; }

        /// <summary>
        /// Gets the synchronization object where synchronized access is required.
        /// </summary>
        protected object SyncRoot { get; } = new object();

        /// <summary>
        /// Gets the test user name.
        /// </summary>
        /// <remarks>Defaults to <see cref="SetUp"/> <see cref="TestSetUp.DefaultUserName"/>.</remarks>
        public string UserName
        {
            get => _userName ?? SetUp.DefaultUserName;
            protected set => _userName = value;
        }

        private string? _userName;

        /// <summary>
        /// Gets the <see cref="IJsonSerializer"/> <i>not</i> from the underlying host.
        /// </summary>
        /// <remarks>Defaults to <see cref="TestSetUp.JsonSerializer"/>. This does <i>not</i> use the instance from the underlying host as a different serializer may be required or may not have been configured.</remarks>
        public IJsonSerializer JsonSerializer { get; internal set; }

        /// <summary>
        /// Gets the <see cref="JsonElementComparerOptions"/> <i>not</i> from the underlying host.
        /// </summary>
        /// <remarks>Defaults to <see cref="TestSetUp.JsonSerializer"/>. This does <i>not</i> use the instance from the underlying host as a different serializer may be required or may not have been configured.</remarks>
        public JsonElementComparerOptions JsonComparerOptions { get; internal set; }

        /// <summary>
        /// Creates a <see cref="JsonElementComparer"/> using the configured <see cref="JsonComparerOptions"/> and <see cref="JsonSerializer"/>.
        /// </summary>
        /// <returns>A new <see cref="JsonElementComparer"/> instance.</returns>
        public JsonElementComparer CreateJsonComparer()
        {
            var options = JsonComparerOptions.Clone();
            options.JsonSerializer ??= JsonSerializer;
            return new JsonElementComparer(options);
        }

        /// <summary>
        /// Resets the underlying host to instantiate a new instance.
        /// </summary>
        public void ResetHost()
        {
            lock (SyncRoot)
            {
                IsHostInstantiated = false;
                OnResetHost();
            }
        }

        /// <summary>
        /// Resets the underlying host to instantiate a new instance.
        /// </summary>
        protected abstract void OnResetHost();

        /// <summary>
        /// Enables opportunity to execute logic immediately after the underlying host has been started. 
        /// </summary>
        /// <remarks>Where overriding ensure the base is invoked first to avoid unintended side-effects as <see cref="TesterBaseCore"/> will invoke the registered <see cref="OnHostStart(Action, bool)"/>.
        /// <para><i>Note:</i> a host lifetime can span one or more tests so this should not be used for per-test set-up/configuration. Equally, a <see cref="ResetHost"/> will result in a new host instantiation on first access.</para></remarks>
        protected virtual void OnHostStartUp()
        {
            foreach (var start in _hostStart)
            {
                start();
            }
        }

        /// <summary>
        /// Provides an opportunity to execute logic immediately after the underlying host has been started.
        /// </summary>
        /// <param name="start">A start <see cref="Action"/>.</param>
        /// <param name="autoResetHost">Indicates whether to automatically <see cref="ResetHost"/> when configuring the services.</param>
        /// <remarks>This can be called multiple times prior to the underlying host being instantiated.
        /// See <see cref="OnHostStartUp"/>.</remarks>
        protected void OnHostStart(Action start, bool autoResetHost = true)
        {
            lock (SyncRoot)
            {
                if (autoResetHost)
                    ResetHost();

                _hostStart.Add(start);
            }
        }

        /// <summary>
        /// Gets the list of pre-run actions to be executed before the underlying test <b>Run</b> occurs.
        /// </summary>
        protected List<Action<IExpectations>> PreRunActions { get; } = [];

        /// <summary>
        /// Gets the list of post-run actions to be executed after the underlying test <b>Run</b> occurs (before <see cref="Expectations.ExpectationsArranger{TTester}.AssertAsync(Expectations.AssertArgs)"/>).
        /// </summary>
        protected List<Action<IExpectations>> PostRunBeforeExpectationsActions { get; } = [];

        /// <summary>
        /// Gets the list of post-run actions to be executed after the underlying test <b>Run</b> occurs (after <see cref="Expectations.ExpectationsArranger{TTester}.AssertAsync(Expectations.AssertArgs)"/>).
        /// </summary>
        protected List<Action<IExpectations>> PostRunAfterExpectationsActions { get; } = [];

        /// <summary>
        /// Gets the list of post-run actions to be executed after the underlying test <b>Run</b> occurs (always executed regardless of result to enable the likes of clean-up etc.).
        /// </summary>
        protected List<Action<IExpectations>> PostRunActions { get; } = [];

        /// <summary>
        /// Executes the pre-run actions before the underlying test <b>Run</b> occurs.
        /// </summary>
        /// <param name="tester">The <see cref="IExpectations"/> tester instance.</param>
        internal void ExecutePreRunActions(IExpectations tester)
        {
            foreach (var action in PreRunActions)
                action(tester);
        }

        /// <summary>
        /// Executes the post-run actions after the underlying test <b>Run</b> occurs (before <see cref="Expectations.ExpectationsArranger{TTester}.AssertAsync(Expectations.AssertArgs)"/>).
        /// </summary>
        /// <param name="tester">The <see cref="IExpectations"/> tester instance.</param>
        internal void ExecutePostRunBeforeExpectationsActions(IExpectations tester)
        {
            foreach (var action in PostRunBeforeExpectationsActions)
                action(tester);
        }

        /// <summary>
        /// Executes the post-run actions after the underlying test <b>Run</b> occurs (before <see cref="Expectations.ExpectationsArranger{TTester}.AssertAsync(Expectations.AssertArgs)"/>).
        /// </summary>
        /// <param name="tester">The <see cref="IExpectations"/> tester instance.</param>
        internal void ExecutePostRunAfterExpectationsActions(IExpectations tester)
        {
            foreach (var action in PostRunAfterExpectationsActions)
                action(tester);
        }

        /// <summary>
        /// Executes the post-run actions after the underlying test <b>Run</b> occurs (always executed regardless of result to enable the likes of clean-up etc.).
        /// </summary>
        /// <param name="tester">The <see cref="IExpectations"/> tester instance.</param>
        internal void ExecutePostRunActions(IExpectations tester)
        {
            foreach (var action in PostRunActions)
                action(tester);
        }

        /// <summary>
        /// Replaces the <see cref="TestFrameworkImplementor"/> with the specified <paramref name="implementor"/>.
        /// </summary>
        /// <param name="implementor">The new <see cref="TestFrameworkImplementor"/>.</param>
        public void ReplaceTestFrameworkImplementor(TestFrameworkImplementor implementor)
        {
            Implementor = implementor ?? throw new ArgumentNullException(nameof(implementor));
        }

        /// <summary>
        /// Gets the log messages accumulated since the last time this was invoked (draining/resetting the starting point for the next invocation).
        /// </summary>
        /// <returns>The accumulated log messages; <c>null</c>/empty where none.</returns>
        /// <remarks>Used by <see cref="WriteWaitAndLog(string, TimeSpan)"/> to correlate output that occurs during a wait period rather than a specific HTTP request/response (see
        /// <see cref="AspNetCore.HttpTesterBase"/> for the latter, request-scoped, correlation). The default (Tier 1, single in-process host) implementation surfaces any <see cref="SharedState"/>
        /// logging that was not attributed to a specific HTTP request (see <see cref="TestSharedState.GetLoggerMessages(string?)"/>) - e.g. from a background/hosted service. A Tier 2 (e.g. multi-host
        /// Aspire) tester should override this to surface whatever is applicable to its own hosting model.</remarks>
        protected virtual IEnumerable<string?>? DrainElapsedLogMessages() => SharedState.GetLoggerMessages();

        /// <summary>
        /// Writes the specified <paramref name="reason"/> to the test output to provide additional context (e.g. why a particular action, or wait, is being performed).
        /// </summary>
        /// <param name="reason">The reason text.</param>
        protected void WriteReason(string reason)
        {
            if (string.IsNullOrEmpty(reason))
                return;

            Implementor.WriteLine("");
            Implementor.WriteLine("REASON >");
            Implementor.WriteLine(reason);
        }

        /// <summary>
        /// Waits for the specified <paramref name="duration"/>, then writes any log messages captured during that time (e.g. from a background process) to the test output (see
        /// <see cref="DrainElapsedLogMessages"/>).
        /// </summary>
        /// <param name="reason">The reason for waiting (written to the test output for context).</param>
        /// <param name="duration">The duration to wait.</param>
        protected async Task WriteWaitAndLog(string reason, TimeSpan duration)
        {
            // Drain any pre-existing/stale backlog first so only messages logged during the wait window itself are reported.
            DrainElapsedLogMessages();

            Implementor.WriteLine("");
            Implementor.WriteLine(new string('=', 80));
            Implementor.WriteLine($"WAIT > {reason}");
            Implementor.WriteLine($"Timeout: {duration}");
            Implementor.WriteLine(new string('=', 80));

            await Task.Delay(duration).ConfigureAwait(false);

            Implementor.WriteLine("");
            Implementor.WriteLine("LOGGING >");
            var logs = DrainElapsedLogMessages();
            if (logs is not null && logs.Any())
            {
                foreach (var msg in logs)
                {
                    Implementor.WriteLine(msg);
                }
            }
            else
                Implementor.WriteLine("None.");
        }

        /// <summary>
        /// Logs the <see cref="HttpResponseMessage"/>.
        /// </summary>
        /// <param name="res">The <see cref="HttpResponseMessage"/>.</param>
        /// <param name="sw">The optional <see cref="Stopwatch"/>.</param>
        internal void LogHttpResponseMessage(HttpResponseMessage res, Stopwatch? sw)
        {
            Implementor.WriteLine("");
            Implementor.WriteLine($"RESPONSE >");
            Implementor.WriteLine($"HttpStatusCode: {res.StatusCode} ({(int)res.StatusCode})");
            Implementor.WriteLine($"Elapsed (ms): {(sw == null ? "none" : sw.Elapsed.TotalMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture))}");

            var hdrs = res.Headers?.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            Implementor.WriteLine($"Headers: {(hdrs == null || hdrs.Length == 0 ? "none" : "")}");
            if (hdrs != null && hdrs.Length > 0)
            {
                foreach (var hdr in hdrs)
                {
                    Implementor.WriteLine($"  {hdr}");
                }
            }

            object? jo = null;
            var content = res.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (!string.IsNullOrEmpty(content) && !string.IsNullOrEmpty(res.Content?.Headers?.ContentType?.MediaType) && JsonMediaTypeNames.Contains(res.Content.Headers.ContentType.MediaType))
            {
                try
                {
                    jo = JsonSerializer.Deserialize(content);
                }
                catch (Exception) { /* This is being swallowed by design. */ }
            }

            var txt = $"Content: [{res.Content?.Headers?.ContentType?.MediaType ?? "none"}]";
            if (jo != null)
            {
                Implementor.WriteLine(txt);
                Implementor.WriteLine(JsonSerializer.Serialize(jo, JsonWriteFormat.Indented));
            }
            else
                Implementor.WriteLine($"{txt} {(string.IsNullOrEmpty(content) ? "none" : content)}");
        }
    }
}
