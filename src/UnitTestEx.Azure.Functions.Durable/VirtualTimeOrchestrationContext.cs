using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace UnitTestEx.Azure.Functions.Durable;

/// <summary>
/// Provides a narrow virtual-time context for method-level orchestration tests.
/// </summary>
public sealed class VirtualTimeOrchestrationContext : TaskOrchestrationContext, IDisposable
{
#if NET8_0
    private readonly object _lock = new();
#else
    private readonly Lock _lock = new();
#endif
    private readonly List<TimerWait> _timers = [];
    private readonly List<EventWait> _eventWaits = [];
    private readonly TaskName _name;
    private readonly object? _input;
    private readonly string _instanceId;
    private DateTime _currentUtcDateTime;

    /// <summary>
    /// Initializes a virtual-time context with the specified orchestration identity and input.
    /// </summary>
    /// <param name="name">The orchestration name.</param>
    /// <param name="input">The orchestration input.</param>
    /// <param name="initialUtcDateTime">The initial UTC time, or null to use the default probe time.</param>
    /// <param name="instanceId">The orchestration instance identifier.</param>
    public VirtualTimeOrchestrationContext(TaskName name, object? input, DateTime? initialUtcDateTime = null, string instanceId = "virtual-time-probe")
    {
        _name = name;
        _input = input;
        _instanceId = instanceId;
        _currentUtcDateTime = initialUtcDateTime ?? new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        if (_currentUtcDateTime.Kind != DateTimeKind.Utc)
            throw new ArgumentException("The initial orchestration time must be UTC.", nameof(initialUtcDateTime));
    }

    /// <summary>Gets the orchestration name.</summary>
    public override TaskName Name => _name;

    /// <summary>Gets the orchestration instance identifier.</summary>
    public override string InstanceId => _instanceId;

    /// <summary>Gets the parent orchestration, which is always null in this context.</summary>
    public override ParentOrchestrationInstance? Parent => null;

    /// <summary>Gets the current virtual UTC time.</summary>
    public override DateTime CurrentUtcDateTime
    {
        get
        {
            lock (_lock)
                return _currentUtcDateTime;
        }
    }

    /// <summary>Gets whether the orchestration is being replayed; this context never replays.</summary>
    public override bool IsReplaying => false;

    /// <summary>Gets the logger factory used by the orchestration context.</summary>
    protected override ILoggerFactory LoggerFactory => NullLoggerFactory.Instance;

    /// <summary>Gets a snapshot of the timers created by this context.</summary>
    public IReadOnlyList<TimerWait> Timers
    {
        get
        {
            lock (_lock)
                return [.. _timers];
        }
    }

    /// <summary>Gets a snapshot of external-event waits created by this context.</summary>
    public IReadOnlyList<EventWaitState> EventWaits
    {
        get
        {
            lock (_lock)
                return [.. _eventWaits.Select(wait => new EventWaitState(wait.Name, wait.IsCompleted, wait.IsCanceled))];
        }
    }

    /// <summary>
    /// Gets the orchestration input when it is compatible with the requested type.
    /// </summary>
    /// <typeparam name="T">The expected input type.</typeparam>
    /// <returns>The orchestration input.</returns>
    public override T GetInput<T>()
        => _input is T input
            ? input
            : _input is null && default(T) is null
                ? default!
                : throw new InvalidCastException($"Orchestration input is not {typeof(T).FullName}.");

    /// <summary>Throws because activity calls are outside this context's supported scope.</summary>
    /// <typeparam name="TResult">The activity result type.</typeparam>
    /// <param name="name">The activity name.</param>
    /// <param name="input">The activity input.</param>
    /// <param name="options">The activity options.</param>
    /// <returns>This method does not return.</returns>
    public override Task<TResult> CallActivityAsync<TResult>(TaskName name, object? input = null, TaskOptions? options = null)
        => throw new NotSupportedException("Activity calls are not supported by this virtual-time context.");

    /// <summary>Throws because sub-orchestration calls are outside this context's supported scope.</summary>
    /// <typeparam name="TResult">The sub-orchestration result type.</typeparam>
    /// <param name="orchestratorName">The sub-orchestration name.</param>
    /// <param name="input">The sub-orchestration input.</param>
    /// <param name="options">The sub-orchestration options.</param>
    /// <returns>This method does not return.</returns>
    public override Task<TResult> CallSubOrchestratorAsync<TResult>(TaskName orchestratorName, object? input = null, TaskOptions? options = null)
        => throw new NotSupportedException("Sub-orchestration calls are not supported by this virtual-time context.");

    /// <summary>Creates a timer that completes when virtual time reaches its deadline or cancellation is requested.</summary>
    /// <param name="fireAt">The timer deadline in UTC.</param>
    /// <param name="cancellationToken">The token used to cancel the timer.</param>
    /// <returns>The task representing the timer.</returns>
    public override Task CreateTimer(DateTime fireAt, CancellationToken cancellationToken)
    {
        if (fireAt.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Timer deadlines must be UTC.", nameof(fireAt));

        var timer = new TimerWait(fireAt);
        lock (_lock)
            _timers.Add(timer);

        timer.RegisterCancellation(cancellationToken);
        if (fireAt <= CurrentUtcDateTime)
            timer.Fire();

        return timer.Task;
    }

    /// <summary>Waits for an external event with the specified name.</summary>
    /// <typeparam name="T">The expected event payload type.</typeparam>
    /// <param name="eventName">The event name.</param>
    /// <param name="cancellationToken">The token used to cancel the wait.</param>
    /// <returns>The task representing the event wait.</returns>
    public override Task<T> WaitForExternalEvent<T>(string eventName, CancellationToken cancellationToken = default)
    {
        var wait = new EventWait<T>(eventName);
        lock (_lock)
            _eventWaits.Add(wait);

        wait.RegisterCancellation(cancellationToken);
        return wait.Task;
    }

    /// <summary>Advances the virtual clock and completes timers whose deadlines are now due.</summary>
    /// <param name="duration">The non-negative amount of virtual time to advance.</param>
    public void AdvanceBy(TimeSpan duration)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(duration, TimeSpan.Zero);

        TimerWait[] dueTimers;
        lock (_lock)
        {
            _currentUtcDateTime = _currentUtcDateTime.Add(duration);
            dueTimers = [.. _timers.Where(timer => timer.Deadline <= _currentUtcDateTime).OrderBy(timer => timer.Deadline)];
        }

        foreach (TimerWait timer in dueTimers)
            timer.Fire();
    }

    /// <summary>Delivers an event payload to the most recent pending wait with the specified name.</summary>
    /// <typeparam name="T">The event payload type.</typeparam>
    /// <param name="eventName">The event name.</param>
    /// <param name="payload">The event payload.</param>
    public void RaiseEvent<T>(string eventName, T payload)
    {
        EventWait? wait;
        lock (_lock)
        {
            wait = _eventWaits.LastOrDefault(candidate => !candidate.IsCompleted && string.Equals(candidate.Name, eventName, StringComparison.OrdinalIgnoreCase));
        }

        if (wait is null || !wait.TrySet(payload))
            throw new InvalidOperationException($"No pending event wait named '{eventName}' accepts this payload.");
    }

    /// <summary>Delivers an event to this orchestration instance.</summary>
    /// <param name="instanceId">The target orchestration instance identifier.</param>
    /// <param name="eventName">The event name.</param>
    /// <param name="payload">The event payload.</param>
    public override void SendEvent(string instanceId, string eventName, object payload)
    {
        if (!string.Equals(instanceId, InstanceId, StringComparison.Ordinal))
            throw new ArgumentException("The event targets a different orchestration instance.", nameof(instanceId));

        RaiseEvent(eventName, payload);
    }

    /// <summary>Throws because custom status is outside this context's supported scope.</summary>
    /// <param name="customStatus">The custom status value.</param>
    public override void SetCustomStatus(object? customStatus) => throw new NotSupportedException("Custom status is not supported by this virtual-time context.");

    /// <summary>Throws because continuing as new is outside this context's supported scope.</summary>
    /// <param name="newInput">The input for the new orchestration generation.</param>
    /// <param name="preserveUnprocessedEvents">Whether to preserve unprocessed events.</param>
    public override void ContinueAsNew(object? newInput = null, bool preserveUnprocessedEvents = true) => throw new NotSupportedException("ContinueAsNew is not supported by this virtual-time context.");

    /// <summary>Throws because deterministic GUID generation is outside this context's supported scope.</summary>
    /// <returns>This method does not return.</returns>
    public override Guid NewGuid() => throw new NotSupportedException("Deterministic GUID generation is not supported by this virtual-time context.");

    /// <summary>Releases the cancellation registrations held by timers and event waits.</summary>
    public void Dispose()
    {
        TimerWait[] timers;
        EventWait[] waits;
        lock (_lock)
        {
            timers = [.. _timers];
            waits = [.. _eventWaits];
        }

        foreach (TimerWait timer in timers)
            timer.Dispose();
        foreach (EventWait wait in waits)
            wait.Dispose();
    }

    /// <summary>Describes the completion state of an external-event wait.</summary>
    /// <param name="Name">The event name.</param>
    /// <param name="IsCompleted">Whether the wait has completed.</param>
    /// <param name="IsCanceled">Whether the wait was canceled.</param>
    public sealed record EventWaitState(string Name, bool IsCompleted, bool IsCanceled);

    /// <summary>Describes the outcome of a virtual timer.</summary>
    public enum TimerOutcome
    {
        /// <summary>The timer has not completed.</summary>
        Pending,
        /// <summary>The virtual clock reached the timer deadline.</summary>
        Fired,
        /// <summary>The timer was canceled.</summary>
        Canceled,
    }

    /// <summary>Represents a virtual timer and its completion state.</summary>
    /// <param name="deadline">The timer deadline.</param>
    public sealed class TimerWait(DateTime deadline) : IDisposable
    {
        private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private CancellationTokenRegistration _registration;

        /// <summary>Gets the timer deadline.</summary>
        public DateTime Deadline { get; } = deadline;

        /// <summary>Gets the task that completes when the timer fires or is canceled.</summary>
        public Task Task => _completion.Task;

        /// <summary>Gets the timer outcome.</summary>
        public TimerOutcome Outcome => _completion.Task.Status switch
        {
            TaskStatus.RanToCompletion => TimerOutcome.Fired,
            TaskStatus.Canceled => TimerOutcome.Canceled,
            _ => TimerOutcome.Pending,
        };

        /// <summary>Registers cancellation for the timer.</summary>
        /// <param name="cancellationToken">The token used to cancel the timer.</param>
        internal void RegisterCancellation(CancellationToken cancellationToken)
        {
            if (cancellationToken.CanBeCanceled)
                _registration = cancellationToken.Register(() => _completion.TrySetCanceled(cancellationToken));
        }

        /// <summary>Completes the timer if it has not already completed.</summary>
        internal void Fire() => _completion.TrySetResult();

        /// <summary>Releases the timer's cancellation registration.</summary>
        public void Dispose() => _registration.Dispose();
    }

    /// <summary>Represents a pending external-event wait.</summary>
    /// <param name="name">The event name.</param>
    private abstract class EventWait(string name) : IDisposable
    {
        /// <summary>Gets the event name.</summary>
        public string Name { get; } = name;

        /// <summary>Gets whether the wait has completed.</summary>
        public abstract bool IsCompleted { get; }

        /// <summary>Gets whether the wait was canceled.</summary>
        public abstract bool IsCanceled { get; }

        /// <summary>Completes the wait with the specified payload when its type matches.</summary>
        /// <param name="payload">The event payload.</param>
        /// <returns>True when the payload completed the wait.</returns>
        public abstract bool TrySet(object? payload);

        /// <summary>Releases the wait's cancellation registration.</summary>
        public abstract void Dispose();
    }

    /// <summary>Represents an external-event wait for a specific payload type.</summary>
    /// <param name="name">The event name.</param>
    private sealed class EventWait<T>(string name) : EventWait(name)
    {
        private readonly TaskCompletionSource<T> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private CancellationTokenRegistration _registration;

        /// <summary>Gets the task that completes when the event arrives or is canceled.</summary>
        public Task<T> Task => _completion.Task;

        /// <summary>Gets whether the wait has completed.</summary>
        public override bool IsCompleted => _completion.Task.IsCompleted;

        /// <summary>Gets whether the wait was canceled.</summary>
        public override bool IsCanceled => _completion.Task.IsCanceled;

        /// <summary>Registers cancellation for the event wait.</summary>
        /// <param name="cancellationToken">The token used to cancel the wait.</param>
        public void RegisterCancellation(CancellationToken cancellationToken)
        {
            if (cancellationToken.CanBeCanceled)
                _registration = cancellationToken.Register(() => _completion.TrySetCanceled(cancellationToken));
        }

        /// <summary>Completes the wait with a payload of the expected type.</summary>
        /// <param name="payload">The event payload.</param>
        /// <returns>True when the payload completed the wait.</returns>
        public override bool TrySet(object? payload)
        {
            if (payload is not T typed)
                throw new InvalidCastException($"Event payload is not {typeof(T).FullName}.");
            return _completion.TrySetResult(typed);
        }

        /// <summary>Releases the event wait's cancellation registration.</summary>
        public override void Dispose() => _registration.Dispose();
    }
}
