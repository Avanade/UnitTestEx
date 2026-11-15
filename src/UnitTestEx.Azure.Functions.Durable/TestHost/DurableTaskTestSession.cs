using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Testing;
using UnitTestEx.Abstractions;

namespace UnitTestEx.Azure.Functions.Durable.TestHost;

/// <summary>Owns an SDK test host for orchestrations scheduled by application starter functions.</summary>
public sealed class DurableTaskTestSession : IAsyncDisposable
{
    private readonly TesterBase _owner;
    private readonly DurableTaskDiscoveryRun _discoveryRun;
    private DurableTaskTestHost? _host;

    /// <summary>Initializes a session that owns its host and discovery lifetime.</summary>
    /// <param name="owner">The FunctionTester providing assertions and diagnostics.</param>
    /// <param name="host">The SDK host owned by the session.</param>
    /// <param name="discoveryRun">The discovered orchestration lifetime owned by the session.</param>
    internal DurableTaskTestSession(TesterBase owner, DurableTaskTestHost host, DurableTaskDiscoveryRun discoveryRun)
    {
        _owner = owner;
        _host = host;
        _discoveryRun = discoveryRun;
    }

    /// <summary>Gets the SDK host; the session owns its lifetime.</summary>
    /// <exception cref="ObjectDisposedException">The session has been disposed.</exception>
    public DurableTaskTestHost Host => _host ?? throw new ObjectDisposedException(nameof(DurableTaskTestSession));

    /// <summary>Gets the SDK-host client to pass explicitly to a starter function.</summary>
    public DurableTaskClient Client => Host.Client;

    /// <summary>Observes an orchestration by its known instance identifier without scheduling it.</summary>
    /// <typeparam name="TOutput">The expected orchestration output type.</typeparam>
    /// <param name="instanceId">The identifier returned or chosen by the starter.</param>
    /// <returns>A run with the existing fluent wait, assertion and diagnostic APIs.</returns>
    /// <remarks>The session retains ownership of the host and must outlive the returned run.</remarks>
    public DurableTaskTestRun<TOutput> Observe<TOutput>(string instanceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        _ = Host;
        return new DurableTaskTestRun<TOutput>(_owner, this, instanceId);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        DurableTaskTestHost? host = Interlocked.Exchange(ref _host, null);
        if (host is not null)
        {
            try { await host.DisposeAsync().ConfigureAwait(false); }
            finally { await _discoveryRun.DisposeAsync().ConfigureAwait(false); }
        }
    }
}
