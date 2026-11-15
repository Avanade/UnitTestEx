using Microsoft.DurableTask;
using Microsoft.DurableTask.Testing;
using Microsoft.DurableTask.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Runtime.ExceptionServices;

namespace UnitTestEx.Azure.Functions.Durable.TestHost;

/// <summary>Holds validated discovery results shared by immutable builder copies.</summary>
internal sealed class DurableTaskDiscovery
{
    /// <summary>Gets discovered activities.</summary>
    internal Dictionary<string, DiscoveredDurableTaskActivity> Activities { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets discovered orchestration methods.</summary>
    internal Dictionary<string, DiscoveredDurableTaskOrchestrator> Orchestrators { get; } = new(StringComparer.Ordinal);

    /// <summary>Creates a copy for a further assembly scan.</summary>
    internal DurableTaskDiscovery Copy()
    {
        var result = new DurableTaskDiscovery();
        foreach (var item in Activities)
            result.Activities.Add(item.Key, item.Value);
        foreach (var item in Orchestrators)
            result.Orchestrators.Add(item.Key, item.Value);
        return result;
    }
}

/// <summary>Owns discovered orchestration instances and their application scope until the SDK host is stopped.</summary>
internal sealed class DurableTaskDiscoveryRun(IServiceProvider applicationServices) : IAsyncDisposable
{
    private readonly Dictionary<(Type Type, string InstanceId), (object? Instance, AsyncServiceScope Scope, bool Owned)> _instances = [];
    private readonly object _sync = new();
    private DataConverter? _converter;

    /// <summary>Captures the actual configured worker converter before scheduling any orchestration.</summary>
    internal void SetHost(DurableTaskTestHost host) => _converter = host.Services.GetRequiredService<IOptions<DurableTaskWorkerOptions>>().Value.DataConverter;

    /// <summary>Registers discoveries except those already supplied explicitly.</summary>
    internal void Register(DurableTaskRegistry registry, DurableTaskDiscovery? discovery)
    {
        if (discovery is null)
            return;

        DiscoveredDurableTaskActivity.Register(registry, discovery.Activities, applicationServices);
        var explicitNames = registry.GetOrchestrators().Select(item => item.Key.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var item in discovery.Orchestrators)
        {
            if (explicitNames.Contains(item.Key))
                continue;
            var adapter = item.Value.Bind(context => GetInstance(item.Value, context.InstanceId), () => _converter ?? throw new InvalidOperationException("The Durable Task host converter is not initialized."));
            registry.AddOrchestrator(item.Key, adapter);
        }
    }

    /// <summary>Resolves each discovered instance lazily and keeps it alive for all replays until run disposal.</summary>
    private object? GetInstance(DiscoveredDurableTaskOrchestrator orchestration, string instanceId)
    {
        if (orchestration.Method.IsStatic)
            return null;
        lock (_sync)
        {
            Type type = orchestration.Method.DeclaringType!;
            var key = (type, instanceId);
            if (_instances.TryGetValue(key, out var existing))
                return existing.Instance ?? throw new InvalidOperationException($"Could not create discovered orchestration '{type.FullName}' for '{instanceId}'.");
            var scope = applicationServices.CreateAsyncScope();
            _instances.Add(key, (null, scope, false));
            object? instance = scope.ServiceProvider.GetService(type);
            bool owned = instance is null;
            instance ??= ActivatorUtilities.CreateInstance(scope.ServiceProvider, type);
            _instances[key] = (instance, scope, owned);
            return instance;
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        List<Exception>? errors = null;
        foreach (var item in _instances.Values.Reverse())
        {
            try
            {
                if (item.Owned && item.Instance is IAsyncDisposable asyncDisposable)
                    await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                else if (item.Owned && item.Instance is IDisposable disposable)
                    disposable.Dispose();
            }
            catch (Exception error) { (errors ??= []).Add(error); }
            try { await item.Scope.DisposeAsync().ConfigureAwait(false); }
            catch (Exception error) { (errors ??= []).Add(error); }
        }

        if (errors?.Count == 1)
            ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors?.Count > 1)
            throw new AggregateException("Disposing discovered orchestration instances or scopes failed.", errors);
    }
}
