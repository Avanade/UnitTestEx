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
    private readonly TesterBase _owner;
    private DurableTaskTestHost? _host;

    /// <summary>Initializes a scheduled test run and takes ownership of its SDK host.</summary>
    /// <param name="owner">The FunctionTester owning the assertion framework.</param>
    /// <param name="host">The in-memory SDK host.</param>
    /// <param name="instanceId">The scheduled orchestration instance identifier.</param>
    internal DurableTaskTestRun(TesterBase owner, DurableTaskTestHost host, string instanceId)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _host = host;
        InstanceId = instanceId;
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
    /// Gets the expectations arranger.
    /// </summary>
    public ExpectationsArranger<DurableTaskTestRun<TOutput>> ExpectationsArranger { get; }

    /// <summary>
    /// Waits for orchestration completion up to the specified timeout.
    /// </summary>
    /// <remarks>When expectations are arranged, non-completed orchestration metadata is represented by <see cref="DurableTaskOrchestrationException"/>. Host and wait errors propagate directly.</remarks>
    /// <param name="timeout">The maximum amount of time to wait; must be positive.</param>
    /// <returns>An assertor exposing the SDK orchestration metadata and fluent assertions.</returns>
    public async Task<DurableTaskTestAssertor<TOutput>> WaitForCompletionAsync(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "The completion timeout must be positive.");

        var metadata = await Host.Client.WaitForInstanceCompletionAsync(InstanceId, getInputsAndOutputs: true).WaitAsync(timeout).ConfigureAwait(false);
        if (metadata is null)
            throw new InvalidOperationException($"The test host returned no metadata for orchestration instance '{InstanceId}'.");

        if (ExpectationsArranger.HasExpectations)
            await ExpectationsArranger.AssertValueAsync(_owner.SharedState.GetLoggerMessages(), metadata, GetExpectationException(metadata)).ConfigureAwait(false);

        return new DurableTaskTestAssertor<TOutput>(_owner, metadata);
    }

    /// <summary>
    /// Creates the exception value supplied to expectations for a non-completed orchestration.
    /// </summary>
    /// <param name="metadata">The orchestration metadata.</param>
    /// <returns>The expectation exception, or <c>null</c> for a completed orchestration.</returns>
    private static Exception? GetExpectationException(OrchestrationMetadata metadata)
        => metadata.RuntimeStatus == OrchestrationRuntimeStatus.Completed ? null : new DurableTaskOrchestrationException(metadata);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        DurableTaskTestHost? host = Interlocked.Exchange(ref _host, null);
        if (host is not null)
            await host.DisposeAsync().ConfigureAwait(false);
    }
}
