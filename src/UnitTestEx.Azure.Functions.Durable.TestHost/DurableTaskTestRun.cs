using System.Diagnostics;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Testing;
using UnitTestEx.Abstractions;
using UnitTestEx.Expectations;

namespace UnitTestEx.Azure.Functions.Durable.TestHost;

/// <summary>
/// Owns a scheduled orchestration test run and its in-memory SDK host.
/// </summary>
/// <typeparam name="TOutput">The orchestration output type.</typeparam>
public sealed class DurableTaskTestRun<TOutput> : IAsyncDisposable, IExpectations<DurableTaskTestRun<TOutput>>
{
    private static readonly TimeSpan DefaultWaitTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan StatusPollingInterval = TimeSpan.FromMilliseconds(20);
    private readonly TesterBase _owner;
    private readonly DurableTaskActivityVerifier _activityVerifier;
    private DurableTaskTestHost? _host;
    private bool _completionObserved;
    private readonly Stopwatch _elapsed = Stopwatch.StartNew();
    private int _resultWritten;
    private readonly List<string?> _logs = [];
    private readonly object _logsSync = new();

    /// <summary>Initializes a scheduled test run and takes ownership of its SDK host.</summary>
    /// <param name="owner">The FunctionTester owning the assertion framework.</param>
    /// <param name="host">The in-memory SDK host.</param>
    /// <param name="instanceId">The scheduled orchestration instance identifier.</param>
    /// <param name="activityVerifier">The per-run activity invocation verifier.</param>
    internal DurableTaskTestRun(TesterBase owner, DurableTaskTestHost host, string instanceId, DurableTaskActivityVerifier activityVerifier)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _host = host;
        InstanceId = instanceId;
        _activityVerifier = activityVerifier ?? throw new ArgumentNullException(nameof(activityVerifier));
        ExpectationsArranger = new ExpectationsArranger<DurableTaskTestRun<TOutput>>(_owner, this);
    }

    /// <summary>
    /// Gets the SDK host for advanced access to its client and other SDK APIs.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The test run has been disposed.</exception>
    public DurableTaskTestHost Host => _host ?? throw new ObjectDisposedException(nameof(DurableTaskTestRun<TOutput>));

    /// <summary>
    /// Gets the scheduled orchestration instance identifier.
    /// </summary>
    public string InstanceId { get; }

    /// <summary>
    /// Waits for the orchestration to start up to the specified timeout, defaulting to five seconds.
    /// </summary>
    /// <param name="timeout">The maximum amount of time to wait; defaults to five seconds and otherwise must be positive.</param>
    public async Task WaitForStartAsync(TimeSpan? timeout = null)
    {
        TimeSpan waitTimeout = timeout ?? DefaultWaitTimeout;
        if (waitTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout), waitTimeout, "The start timeout must be positive.");

        _ = await Host.Client.WaitForInstanceStartAsync(InstanceId).WaitAsync(waitTimeout).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"The test host returned no metadata for orchestration instance '{InstanceId}'.");
    }

    /// <summary>
    /// Raises an external event on this orchestration instance.
    /// </summary>
    /// <typeparam name="TEvent">The event payload type.</typeparam>
    /// <param name="eventName">The event name.</param>
    /// <param name="eventData">The event payload.</param>
    /// <returns>A task that completes when the SDK client has raised the event.</returns>
    public Task RaiseEventAsync<TEvent>(string eventName, TEvent eventData)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        return Host.Client.RaiseEventAsync(InstanceId, eventName, eventData);
    }

    /// <summary>
    /// Waits until a custom-status predicate matches the latest orchestration metadata.
    /// </summary>
    /// <param name="predicate">A predicate that returns true when the desired custom status is visible.</param>
    /// <param name="timeout">The maximum amount of time to wait; defaults to five seconds and otherwise must be positive.</param>
    /// <returns>The orchestration metadata snapshot with the matching custom status.</returns>
    /// <exception cref="TimeoutException">The custom status was not observed before the timeout.</exception>
    public async Task<OrchestrationMetadata> WaitForCustomStatusAsync(Func<OrchestrationMetadata, bool> predicate, TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        TimeSpan waitTimeout = timeout ?? DefaultWaitTimeout;
        if (waitTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout), waitTimeout, "The custom-status timeout must be positive.");

        Stopwatch stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < waitTimeout)
        {
            TimeSpan remaining = waitTimeout - stopwatch.Elapsed;
            OrchestrationMetadata? metadata = await Host.Client.GetInstanceAsync(InstanceId, getInputsAndOutputs: true).WaitAsync(remaining).ConfigureAwait(false);
            if (metadata is not null && predicate(metadata))
                return metadata;

            remaining = waitTimeout - stopwatch.Elapsed;
            if (remaining <= TimeSpan.Zero)
                break;

            await Task.Delay(remaining < StatusPollingInterval ? remaining : StatusPollingInterval).ConfigureAwait(false);
        }

        throw new TimeoutException($"The custom status was not observed for orchestration instance '{InstanceId}' within {waitTimeout}.");
    }

    /// <summary>
    /// Gets the expectations arranger.
    /// </summary>
    public ExpectationsArranger<DurableTaskTestRun<TOutput>> ExpectationsArranger { get; }

    /// <summary>
    /// Waits for orchestration completion up to the specified timeout, defaulting to five seconds.
    /// </summary>
    /// <remarks>When expectations are arranged, non-completed orchestration metadata is represented by <see cref="DurableTaskOrchestrationException"/>. The assertion callback runs after expectations are checked. Host, wait, and assertion errors propagate directly.</remarks>
    /// <param name="timeout">The maximum amount of time to wait; defaults to five seconds and otherwise must be positive.</param>
    /// <param name="assert">An optional callback to assert the completion result.</param>
    /// <returns>An assertor exposing the SDK orchestration metadata and fluent assertions.</returns>
    public async Task<DurableTaskTestAssertor<TOutput>> WaitForCompletionAsync(TimeSpan? timeout = null, Action<DurableTaskTestAssertor<TOutput>>? assert = null)
    {
        TimeSpan waitTimeout = timeout ?? DefaultWaitTimeout;
        if (waitTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout), waitTimeout, "The completion timeout must be positive.");

        OrchestrationMetadata metadata;
        try
        {
            metadata = await Host.Client.WaitForInstanceCompletionAsync(InstanceId, getInputsAndOutputs: true).WaitAsync(waitTimeout).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"The test host returned no metadata for orchestration instance '{InstanceId}'.");
        }
        catch (TimeoutException)
        {
            await WriteTimeoutAsync(waitTimeout).ConfigureAwait(false);
            throw;
        }

        _completionObserved = true;
        string?[] logs = CaptureLogs();
        if (Interlocked.Exchange(ref _resultWritten, 1) == 0)
            WriteResult(metadata, logs);
        if (ExpectationsArranger.HasExpectations)
            await ExpectationsArranger.AssertValueAsync(logs, metadata, GetExpectationException(metadata)).ConfigureAwait(false);

        var result = new DurableTaskTestAssertor<TOutput>(_owner, metadata);
        assert?.Invoke(result);
        return result;
    }

    /// <summary>Writes bookended managed diagnostics for a completed run or a timeout snapshot.</summary>
    /// <param name="metadata">The orchestration metadata, if available.</param>
    /// <param name="logs">The captured log snapshot shared with expectations.</param>
    /// <param name="timeout">The completion timeout, if the wait expired.</param>
    /// <param name="metadataError">The error preventing timeout metadata retrieval, if any.</param>
    private void WriteResult(OrchestrationMetadata? metadata, string?[] logs, TimeSpan? timeout = null, Exception? metadataError = null)
    {
        var output = _owner.Implementor;
        output.WriteLine("");
        output.WriteLine("=== BEGIN DURABLE ORCHESTRATION TESTER ===");
        output.WriteLine("");
        output.WriteLine($"Orchestration: {metadata?.Name.ToString() ?? "<unavailable>"}");
        output.WriteLine($"Instance: {InstanceId}");
        output.WriteLine("");
        output.WriteLine("INPUT >");
        output.WriteLine(metadata is null ? "<unavailable>" : metadata.SerializedInput ?? "<none>");
        output.WriteLine("");
        output.WriteLine("LOGGING >");
        if (logs.Length == 0)
            output.WriteLine("None.");
        else
            foreach (string? log in logs)
                output.WriteLine(log);

        output.WriteLine("");
        output.WriteLine("RESULT >");
        output.WriteLine($"Elapsed (ms): {_elapsed.Elapsed.TotalMilliseconds:F3}");
        if (timeout is not null)
            output.WriteLine($"Completion wait timed out after {timeout}; this is not an orchestration failure.");
        output.WriteLine($"{(timeout is null ? "Status" : "Last observed status")}: {metadata?.RuntimeStatus.ToString() ?? "<unavailable>"}");
        if (metadataError is not null)
            output.WriteLine($"Metadata retrieval failed: {metadataError.Message} [{metadataError.GetType().Name}]");
        if (metadata is not null)
        {
            output.WriteLine($"Custom status: {metadata.SerializedCustomStatus ?? "<none>"}");
            output.WriteLine($"Output: {metadata.SerializedOutput ?? "<none>"}");
            int depth = 0;
            for (TaskFailureDetails? failure = metadata.FailureDetails; failure is not null; failure = failure.InnerFailure)
            {
                string indent = new(' ', depth++ * 2);
                output.WriteLine($"{indent}Failure: {failure.ErrorMessage} [{failure.ErrorType}]");
                if (!string.IsNullOrEmpty(failure.StackTrace))
                    output.WriteLine($"{indent}{failure.StackTrace}");
            }
        }

        output.WriteLine("");
        output.WriteLine("=== END DURABLE ORCHESTRATION TESTER ===");
        output.WriteLine("");
    }

    /// <summary>Obtains a bounded status snapshot for a timed-out completion wait.</summary>
    /// <param name="timeout">The timeout that expired.</param>
    /// <returns>A task that completes after diagnostics have been written.</returns>
    private async Task WriteTimeoutAsync(TimeSpan timeout)
    {
        OrchestrationMetadata? metadata = null;
        Exception? metadataError = null;
        try
        {
            metadata = await Host.Client.GetInstanceAsync(InstanceId, getInputsAndOutputs: true)
                .WaitAsync(TimeSpan.FromMilliseconds(250)).ConfigureAwait(false);
        }
        catch (Exception error)
        {
            // Diagnostic retrieval must not replace the original completion timeout.
            metadataError = error;
        }

        WriteResult(metadata, CaptureLogs(), timeout, metadataError);
    }

    /// <summary>Drains shared log messages while preserving earlier timeout snapshots for expectations.</summary>
    /// <returns>All logs captured by this run so far.</returns>
    private string?[] CaptureLogs()
    {
        lock (_logsSync)
        {
            _logs.AddRange(_owner.SharedState.GetLoggerMessages());
            return [.. _logs];
        }
    }

    /// <summary>
    /// Verifies the configured invocation counts for all registered activity handlers.
    /// </summary>
    /// <remarks>Each activity registration defaults to at least one invocation; retries count as separate handler invocations.</remarks>
    /// <exception cref="InvalidOperationException">Completion has not been observed, or an activity count does not meet its expectation.</exception>
    public void Verify()
    {
        if (!_completionObserved)
            throw new InvalidOperationException("WaitForCompletionAsync must complete before activity verification.");

        _activityVerifier.Verify();
    }

    /// <summary>
    /// Creates the exception value supplied to expectations for a non-completed orchestration.
    /// </summary>
    /// <param name="metadata">The orchestration metadata.</param>
    /// <returns>The expectation exception, or <c>null</c> for a completed orchestration.</returns>
    private static DurableTaskOrchestrationException? GetExpectationException(OrchestrationMetadata metadata)
        => metadata.RuntimeStatus == OrchestrationRuntimeStatus.Completed ? null : new DurableTaskOrchestrationException(metadata);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        DurableTaskTestHost? host = Interlocked.Exchange(ref _host, null);
        if (host is not null)
            await host.DisposeAsync().ConfigureAwait(false);
    }
}
