using Microsoft.DurableTask;
using Microsoft.DurableTask.Entities;
using Microsoft.DurableTask.Testing;
using Microsoft.Extensions.Logging;

namespace UnitTestEx.Azure.Functions.Durable.TestHost;

/// <summary>
/// Extensions for registering orchestrations with the in-memory Durable Task test host.
/// </summary>
public static class DurableTaskRegistryExtensions
{
    /// <summary>
    /// Registers a no-input orchestration delegate that emits a replay-safe warning when it schedules a future, wall-clock timer.
    /// </summary>
    /// <typeparam name="TOutput">The orchestration output type.</typeparam>
    /// <param name="registry">The Durable Task registry.</param>
    /// <param name="name">The orchestration name.</param>
    /// <param name="orchestrator">The orchestration delegate.</param>
    public static void AddOrchestratorFuncWithTimerDiagnostics<TOutput>(this DurableTaskRegistry registry, string name, Func<TaskOrchestrationContext, Task<TOutput>> orchestrator)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(orchestrator);

        registry.AddOrchestratorFunc<TOutput>(name, context => orchestrator(new WallClockTimerDiagnosticContext(context)));
    }

    /// <summary>
    /// Registers an orchestration delegate that emits a replay-safe warning when it schedules a future, wall-clock timer in the in-memory test host.
    /// </summary>
    /// <typeparam name="TInput">The orchestration input type.</typeparam>
    /// <typeparam name="TOutput">The orchestration output type.</typeparam>
    /// <param name="registry">The Durable Task registry.</param>
    /// <param name="name">The orchestration name.</param>
    /// <param name="orchestrator">The orchestration delegate.</param>
    public static void AddOrchestratorFuncWithTimerDiagnostics<TInput, TOutput>(this DurableTaskRegistry registry, string name, Func<TaskOrchestrationContext, TInput, Task<TOutput>> orchestrator)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(orchestrator);

        registry.AddOrchestratorFunc<TInput, TOutput>(name, (context, input) => orchestrator(new WallClockTimerDiagnosticContext(context), input));
    }

    /// <summary>
    /// Forwards orchestration operations to the host context and reports future wall-clock timers.
    /// </summary>
    /// <param name="inner">The host-provided orchestration context.</param>
    private sealed class WallClockTimerDiagnosticContext(TaskOrchestrationContext inner) : TaskOrchestrationContext
    {
        private readonly TaskOrchestrationContext _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        private readonly ILogger _logger = inner.CreateReplaySafeLogger<WallClockTimerDiagnosticContext>();

        /// <summary>Gets the orchestration name.</summary>
        public override TaskName Name => _inner.Name;

        /// <summary>Gets the orchestration instance identifier.</summary>
        public override string InstanceId => _inner.InstanceId;

        /// <summary>Gets the parent orchestration instance.</summary>
        public override ParentOrchestrationInstance? Parent => _inner.Parent;

        /// <summary>Gets the host's current orchestration time.</summary>
        public override DateTime CurrentUtcDateTime => _inner.CurrentUtcDateTime;

        /// <summary>Gets whether the orchestration is being replayed.</summary>
        public override bool IsReplaying => _inner.IsReplaying;

        /// <summary>Gets the orchestration version.</summary>
        public override string Version => _inner.Version;

        /// <summary>Gets the orchestration properties.</summary>
        public override IReadOnlyDictionary<string, object?> Properties => _inner.Properties;

        /// <summary>Gets the host entity feature.</summary>
        public override TaskOrchestrationEntityFeature Entities => _inner.Entities;

        /// <summary>Gets the host's replay-safe logger factory.</summary>
        protected override ILoggerFactory LoggerFactory => _inner.ReplaySafeLoggerFactory;

        /// <summary>Gets the orchestration input.</summary>
        /// <typeparam name="T">The expected input type.</typeparam>
        /// <returns>The orchestration input.</returns>
        public override T GetInput<T>() => _inner.GetInput<T>()!;

        /// <summary>Schedules an activity through the host context.</summary>
        /// <typeparam name="TResult">The activity result type.</typeparam>
        /// <param name="name">The activity name.</param>
        /// <param name="input">The activity input.</param>
        /// <param name="options">The activity options.</param>
        /// <returns>The activity result task.</returns>
        public override Task<TResult> CallActivityAsync<TResult>(TaskName name, object? input = null, TaskOptions? options = null) => _inner.CallActivityAsync<TResult>(name, input, options);

        /// <summary>Creates a host timer and reports when it uses future wall-clock time.</summary>
        /// <param name="delay">The timer delay.</param>
        /// <param name="cancellationToken">The token used to cancel the timer.</param>
        /// <returns>The host timer task.</returns>
        public override Task CreateTimer(TimeSpan delay, CancellationToken cancellationToken)
        {
            DateTime fireAt = _inner.CurrentUtcDateTime.Add(delay);
            Task timer = _inner.CreateTimer(delay, cancellationToken);
            ReportWallClockTimer(delay, fireAt);
            return timer;
        }

        /// <summary>Creates a host timer and reports when its deadline is in the future.</summary>
        /// <param name="fireAt">The timer deadline.</param>
        /// <param name="cancellationToken">The token used to cancel the timer.</param>
        /// <returns>The host timer task.</returns>
        public override Task CreateTimer(DateTime fireAt, CancellationToken cancellationToken)
        {
            TimeSpan delay = fireAt - _inner.CurrentUtcDateTime;
            Task timer = _inner.CreateTimer(fireAt, cancellationToken);
            ReportWallClockTimer(delay, fireAt);
            return timer;
        }

        /// <summary>Waits for an external event through the host context.</summary>
        /// <typeparam name="T">The event payload type.</typeparam>
        /// <param name="eventName">The event name.</param>
        /// <param name="cancellationToken">The token used to cancel the wait.</param>
        /// <returns>The host event-wait task.</returns>
        public override Task<T> WaitForExternalEvent<T>(string eventName, CancellationToken cancellationToken = default) => _inner.WaitForExternalEvent<T>(eventName, cancellationToken);

        /// <summary>Sends an event through the host context.</summary>
        /// <param name="instanceId">The target instance identifier.</param>
        /// <param name="eventName">The event name.</param>
        /// <param name="payload">The event payload.</param>
        public override void SendEvent(string instanceId, string eventName, object payload) => _inner.SendEvent(instanceId, eventName, payload);

        /// <summary>Sets custom status through the host context.</summary>
        /// <param name="customStatus">The custom status value.</param>
        public override void SetCustomStatus(object? customStatus) => _inner.SetCustomStatus(customStatus);

        /// <summary>Schedules a sub-orchestration through the host context.</summary>
        /// <typeparam name="TResult">The sub-orchestration result type.</typeparam>
        /// <param name="orchestratorName">The sub-orchestration name.</param>
        /// <param name="input">The sub-orchestration input.</param>
        /// <param name="options">The sub-orchestration options.</param>
        /// <returns>The sub-orchestration result task.</returns>
        public override Task<TResult> CallSubOrchestratorAsync<TResult>(TaskName orchestratorName, object? input = null, TaskOptions? options = null) => _inner.CallSubOrchestratorAsync<TResult>(orchestratorName, input, options);

        /// <summary>Continues the orchestration with new input through the host context.</summary>
        /// <param name="newInput">The input for the new orchestration generation.</param>
        /// <param name="preserveUnprocessedEvents">Whether to preserve unprocessed events.</param>
        public override void ContinueAsNew(object? newInput = null, bool preserveUnprocessedEvents = true) => _inner.ContinueAsNew(newInput, preserveUnprocessedEvents);

        /// <summary>Continues the orchestration using the specified options.</summary>
        /// <param name="options">The continuation options.</param>
        public override void ContinueAsNew(ContinueAsNewOptions options)
            => _inner.ContinueAsNew(options);

        /// <summary>Creates a deterministic GUID through the host context.</summary>
        /// <returns>The generated GUID.</returns>
        public override Guid NewGuid() => _inner.NewGuid();

        /// <summary>Writes a warning for future timers outside replay.</summary>
        /// <param name="delay">The delay until the timer fires.</param>
        /// <param name="fireAt">The timer deadline.</param>
        private void ReportWallClockTimer(TimeSpan delay, DateTime fireAt)
        {
            if (delay <= TimeSpan.Zero || IsReplaying)
                return;

            _logger.LogWarning("Durable timer for orchestration {OrchestrationName} ({InstanceId}) uses the host's real wall clock; virtual time is unavailable and the timer may wait {Delay} until {FireAtUtc}.",
                Name, InstanceId, delay, fireAt);
        }
    }
}
