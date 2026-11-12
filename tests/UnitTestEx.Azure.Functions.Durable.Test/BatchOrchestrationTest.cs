using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using UnitTestEx.DurableFunction;

namespace UnitTestEx.Azure.Functions.Durable.Test;

/// <summary>
/// Verifies batch orchestration behavior with controllable activity and child calls.
/// </summary>
[TestFixture]
public sealed class BatchOrchestrationTest
{
    /// <summary>Verifies that results retain input order when activities complete out of order.</summary>
    [Test]
    public async Task ProcessBatchPreservesInputOrderWhenActivitiesCompleteOutOfOrder()
    {
        var context = new ControllableOrchestrationContext(nameof(BatchOrchestration.ProcessBatch), new BatchRequest(" batch-7 ", [new WorkItem("A", 10), new WorkItem("B", 20), new WorkItem("C", 30)]));

        Task<BatchResult> execution = BatchOrchestration.ProcessBatch(context);
        var normalize = await context.WaitForActivityCallAsync<string>(nameof(ActivityTransforms.NormalizeReference));
        normalize.Complete("BATCH-7");

        var processCalls = await context.WaitForActivityCallsAsync<ItemResult>(nameof(WorkflowActivities.ProcessItem), 3);
        Assert.That(processCalls.Select(call => ((WorkItem)call.Input!).Id), Is.EqualTo(new[] { "A", "B", "C" }));
        foreach (var call in processCalls)
        {
            var retryPolicy = call.Options?.Retry?.Policy
                ?? throw new AssertionException("Each item activity call should carry its retry policy.");
            Assert.That(retryPolicy.MaxNumberOfAttempts, Is.EqualTo(3));
            Assert.That(retryPolicy.FirstRetryInterval, Is.EqualTo(TimeSpan.FromSeconds(1)));
        }

        processCalls[2].Complete(new ItemResult("C", 30));
        processCalls[1].Complete(new ItemResult("B", 20));
        Assert.That(execution.IsCompleted, Is.False);
        processCalls[0].Complete(new ItemResult("A", 10));

        var total = await context.WaitForActivityCallAsync<decimal>(nameof(ActivityTransforms.TotalItems));
        var orderedItems = (ItemResult[])total.Input!;
        Assert.That(orderedItems.Select(item => item.Id), Is.EqualTo(new[] { "A", "B", "C" }));
        total.Complete(60);

        var finalize = await context.WaitForSubOrchestrationCallAsync<BatchResult>(nameof(BatchOrchestration.FinalizeBatch));
        var finalizationInput = (BatchResult)finalize.Input!;
        Assert.That(finalizationInput.Reference, Is.EqualTo("BATCH-7"));
        Assert.That(finalizationInput.Total, Is.EqualTo(60));
        Assert.That(finalizationInput.Items.Select(item => item.Id), Is.EqualTo(new[] { "A", "B", "C" }));
        Assert.That(finalize.Options, Is.TypeOf<SubOrchestrationOptions>());
        Assert.That(((SubOrchestrationOptions)finalize.Options!).InstanceId, Is.EqualTo("00000000-0000-0000-0000-000000000001"));
        finalize.Complete(finalizationInput);

        BatchResult result = await execution.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(result, Is.EqualTo(finalizationInput));
    }

    /// <summary>Verifies that the orchestration handles SDK failure details from an activity call.</summary>
    [Test]
    public async Task RecoverWorkflowHandlesDurableFailureDetailsFromActivityCall()
    {
        var context = new ControllableOrchestrationContext(nameof(FailureOrchestration.RecoverWorkflow), "batch-9");
        Task<string> execution = FailureOrchestration.RecoverWorkflow(context);

        var reject = await context.WaitForActivityCallAsync<string>(nameof(ActivityTransforms.RejectWorkflow));
        reject.Fail(new WorkflowRejectedException("Workflow batch-9 was declined."));

        Assert.That(reject.FailureDetails, Is.Not.Null);
        Assert.That(reject.FailureDetails!.ErrorType, Is.EqualTo(typeof(WorkflowRejectedException).FullName));
        Assert.That(reject.FailureDetails.ErrorMessage, Is.EqualTo("Workflow batch-9 was declined."));
        Assert.That(reject.FailureDetails.IsCausedBy<WorkflowRejectedException>(), Is.True);
        Assert.That(await execution.WaitAsync(TimeSpan.FromSeconds(5)), Is.EqualTo("Declined"));
        Assert.That(context.CustomStatus, Is.EqualTo("Declined"));
    }
}

/// <summary>
/// Captures orchestration calls and lets a test complete them in a controlled order.
/// </summary>
/// <param name="name">The orchestration name.</param>
/// <param name="input">The orchestration input.</param>
internal sealed class ControllableOrchestrationContext(string name, object input) : TaskOrchestrationContext
{
    private readonly object _gate = new();
    private readonly List<RecordedCall> _calls = [];
    private TaskCompletionSource _callsChanged = NewSignal();
    private int _nextTaskId;

    /// <summary>Gets the orchestration name.</summary>
    public override TaskName Name => name;

    /// <summary>Gets the orchestration instance identifier.</summary>
    public override string InstanceId => "batch-test";

    /// <summary>Gets the parent orchestration, which is always null in this context.</summary>
    public override ParentOrchestrationInstance? Parent => null;

    /// <summary>Gets the fixed orchestration time used by this test context.</summary>
    public override DateTime CurrentUtcDateTime => new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>Gets whether the orchestration is being replayed; this context never replays.</summary>
    public override bool IsReplaying => false;

    /// <summary>Gets the logger factory used by the orchestration context.</summary>
    protected override ILoggerFactory LoggerFactory => NullLoggerFactory.Instance;

    /// <summary>Gets the most recently assigned custom status.</summary>
    public object? CustomStatus { get; private set; }

    /// <summary>Gets the orchestration input.</summary>
    /// <typeparam name="T">The expected input type.</typeparam>
    /// <returns>The orchestration input.</returns>
    public override T GetInput<T>()
        => input is T typed
            ? typed
            : throw new InvalidCastException($"Orchestration input is not {typeof(T).FullName}.");

    /// <summary>Records an activity call and returns a task completed by the test.</summary>
    /// <typeparam name="TResult">The activity result type.</typeparam>
    /// <param name="name">The activity name.</param>
    /// <param name="input">The activity input.</param>
    /// <param name="options">The activity options.</param>
    /// <returns>The task completed by the test.</returns>
    public override Task<TResult> CallActivityAsync<TResult>(TaskName name, object? input = null, TaskOptions? options = null)
    {
        var call = new ActivityCall<TResult>(name, input, options, Interlocked.Increment(ref _nextTaskId));
        Record(call);
        return call.Task;
    }

    /// <summary>Records a sub-orchestration call and returns a task completed by the test.</summary>
    /// <typeparam name="TResult">The sub-orchestration result type.</typeparam>
    /// <param name="orchestratorName">The sub-orchestration name.</param>
    /// <param name="input">The sub-orchestration input.</param>
    /// <param name="options">The sub-orchestration options.</param>
    /// <returns>The task completed by the test.</returns>
    public override Task<TResult> CallSubOrchestratorAsync<TResult>(TaskName orchestratorName, object? input = null, TaskOptions? options = null)
    {
        var call = new SubOrchestrationCall<TResult>(orchestratorName, input, options);
        Record(call);
        return call.Task;
    }

    /// <summary>Waits for the first recorded activity call with the specified name.</summary>
    /// <typeparam name="TResult">The activity result type.</typeparam>
    /// <param name="name">The activity name.</param>
    /// <returns>The recorded activity call.</returns>
    public async Task<ActivityCall<TResult>> WaitForActivityCallAsync<TResult>(string name)
        => (await WaitForCallsAsync<ActivityCall<TResult>>(name, 1)).Single();

    /// <summary>Waits until the requested number of activity calls has been recorded.</summary>
    /// <typeparam name="TResult">The activity result type.</typeparam>
    /// <param name="name">The activity name.</param>
    /// <param name="count">The minimum number of matching calls.</param>
    /// <returns>The matching activity calls.</returns>
    public Task<IReadOnlyList<ActivityCall<TResult>>> WaitForActivityCallsAsync<TResult>(string name, int count)
        => WaitForCallsAsync<ActivityCall<TResult>>(name, count);

    /// <summary>Waits for the first recorded sub-orchestration call with the specified name.</summary>
    /// <typeparam name="TResult">The sub-orchestration result type.</typeparam>
    /// <param name="name">The sub-orchestration name.</param>
    /// <returns>The recorded sub-orchestration call.</returns>
    public async Task<SubOrchestrationCall<TResult>> WaitForSubOrchestrationCallAsync<TResult>(string name)
        => (await WaitForCallsAsync<SubOrchestrationCall<TResult>>(name, 1)).Single();

    /// <summary>Waits until the requested number of matching calls has been recorded.</summary>
    /// <typeparam name="TCall">The recorded call type.</typeparam>
    /// <param name="name">The call name.</param>
    /// <param name="count">The minimum number of matching calls.</param>
    /// <returns>The matching calls.</returns>
    private async Task<IReadOnlyList<TCall>> WaitForCallsAsync<TCall>(string name, int count)
        where TCall : RecordedCall
    {
        while (true)
        {
            Task wait;
            lock (_gate)
            {
                var matching = _calls.OfType<TCall>()
                    .Where(call => string.Equals(call.Name.ToString(), name, StringComparison.Ordinal))
                    .ToArray();
                if (matching.Length >= count)
                    return matching;

                wait = _callsChanged.Task;
            }

            await wait.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    /// <summary>Adds a call to the recorded call list and signals waiting tests.</summary>
    /// <param name="call">The call to record.</param>
    private void Record(RecordedCall call)
    {
        TaskCompletionSource changed;
        lock (_gate)
        {
            _calls.Add(call);
            changed = _callsChanged;
            _callsChanged = NewSignal();
        }

        changed.TrySetResult();
    }

    /// <summary>Throws because timers are outside this batch-call test context.</summary>
    /// <param name="fireAt">The timer deadline.</param>
    /// <param name="cancellationToken">The token used to cancel the timer.</param>
    /// <returns>This method does not return.</returns>
    public override Task CreateTimer(DateTime fireAt, CancellationToken cancellationToken)
        => throw new NotSupportedException("Timers are outside this batch-call test context.");

    /// <summary>Throws because external events are outside this batch-call test context.</summary>
    /// <typeparam name="T">The event payload type.</typeparam>
    /// <param name="eventName">The event name.</param>
    /// <param name="cancellationToken">The token used to cancel the wait.</param>
    /// <returns>This method does not return.</returns>
    public override Task<T> WaitForExternalEvent<T>(string eventName, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("External events are outside this batch-call test context.");

    /// <summary>Throws because event sending is outside this batch-call test context.</summary>
    /// <param name="instanceId">The target instance identifier.</param>
    /// <param name="eventName">The event name.</param>
    /// <param name="payload">The event payload.</param>
    public override void SendEvent(string instanceId, string eventName, object payload)
        => throw new NotSupportedException("External events are outside this batch-call test context.");

    /// <summary>Stores the orchestration's custom status.</summary>
    /// <param name="customStatus">The custom status value.</param>
    public override void SetCustomStatus(object? customStatus) => CustomStatus = customStatus;

    /// <summary>Throws because continuing as new is outside this batch-call test context.</summary>
    /// <param name="newInput">The new orchestration input.</param>
    /// <param name="preserveUnprocessedEvents">Whether to preserve unprocessed events.</param>
    public override void ContinueAsNew(object? newInput = null, bool preserveUnprocessedEvents = true)
        => throw new NotSupportedException("ContinueAsNew is outside this batch-call test context.");

    /// <summary>Returns the fixed GUID used by this test context.</summary>
    /// <returns>The fixed test-context GUID.</returns>
    public override Guid NewGuid() => Guid.Parse("00000000-0000-0000-0000-000000000001");

    /// <summary>Creates a task completion source used to signal recorded-call changes.</summary>
    /// <returns>A task completion source that runs continuations asynchronously.</returns>
    private static TaskCompletionSource NewSignal()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Stores the name and input for a recorded orchestration call.</summary>
    /// <param name="name">The call name.</param>
    /// <param name="input">The call input.</param>
    internal abstract class RecordedCall(TaskName name, object? input)
    {
        /// <summary>Gets the call name.</summary>
        public TaskName Name { get; } = name;

        /// <summary>Gets the call input.</summary>
        public object? Input { get; } = input;
    }

    /// <summary>Represents a recorded activity call that a test can complete or fail.</summary>
    /// <param name="name">The activity name.</param>
    /// <param name="input">The activity input.</param>
    /// <param name="options">The activity options.</param>
    /// <param name="taskId">The activity task identifier.</param>
    internal sealed class ActivityCall<TResult>(TaskName name, object? input, TaskOptions? options, int taskId)
        : RecordedCall(name, input)
    {
        private readonly TaskCompletionSource<TResult> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Gets the activity options.</summary>
        public TaskOptions? Options { get; } = options;

        /// <summary>Gets the task completed by the test.</summary>
        public Task<TResult> Task => _completion.Task;

        /// <summary>Gets the failure details supplied when the activity call fails.</summary>
        public TaskFailureDetails? FailureDetails { get; private set; }

        /// <summary>Completes the activity call successfully.</summary>
        /// <param name="result">The activity result.</param>
        public void Complete(TResult result) => _completion.TrySetResult(result);

        /// <summary>Fails the activity call with the specified exception.</summary>
        /// <param name="exception">The activity exception.</param>
        public void Fail(Exception exception)
        {
            FailureDetails = TaskFailureDetails.FromException(exception);
            _completion.TrySetException(new TaskFailedException(Name.ToString(), taskId, FailureDetails));
        }
    }

    /// <summary>Represents a recorded sub-orchestration call that a test can complete.</summary>
    /// <param name="name">The sub-orchestration name.</param>
    /// <param name="input">The sub-orchestration input.</param>
    /// <param name="options">The sub-orchestration options.</param>
    internal sealed class SubOrchestrationCall<TResult>(TaskName name, object? input, TaskOptions? options)
        : RecordedCall(name, input)
    {
        private readonly TaskCompletionSource<TResult> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Gets the sub-orchestration options.</summary>
        public TaskOptions? Options { get; } = options;

        /// <summary>Gets the task completed by the test.</summary>
        public Task<TResult> Task => _completion.Task;

        /// <summary>Completes the sub-orchestration call successfully.</summary>
        /// <param name="result">The sub-orchestration result.</param>
        public void Complete(TResult result) => _completion.TrySetResult(result);
    }
}
