using Microsoft.DurableTask;
using Microsoft.DurableTask.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Azure.Functions.Worker;
using System.Reflection;
using UnitTestEx.Azure.Functions;

namespace UnitTestEx.Azure.Functions.Durable.TestHost;

/// <summary>
/// Configures handlers for a no-input orchestration and starts its in-memory SDK-host test.
/// </summary>
/// <typeparam name="TEntryPoint">A type from the isolated-worker application assembly.</typeparam>
/// <typeparam name="TSelf">The concrete FunctionTester type.</typeparam>
/// <typeparam name="TOutput">The orchestration output type.</typeparam>
public sealed class DurableTaskNoInputOrchestrationTestBuilder<TEntryPoint, TSelf, TOutput> where TEntryPoint : class where TSelf : FunctionTesterBase<TEntryPoint, TSelf>
{
    private readonly FunctionTesterBase<TEntryPoint, TSelf> _functionTester;
    private readonly string _orchestrationName;
    private readonly Action<DurableTaskRegistry> _configureRegistry;
    private readonly Action<IServiceCollection>? _configureServices;
    private readonly IReadOnlyList<Action<DurableTaskRegistry, DurableTaskActivityVerifier>> _activityRegistrations;
    private readonly DurableTaskDiscovery? _discoveredActivities;

    /// <summary>Initializes a no-input orchestration builder.</summary>
    /// <param name="functionTester">The owning FunctionTester.</param>
    /// <param name="orchestrationName">The orchestration name to start.</param>
    /// <param name="configureRegistry">The SDK registry configuration.</param>
    /// <param name="configureServices">The optional SDK host service configuration.</param>
    /// <param name="activityRegistrations">The activity registrations with their per-run invocation expectations.</param>
    /// <param name="discoveredActivities">The discovered handlers used only when no explicit handler has the same name.</param>
    internal DurableTaskNoInputOrchestrationTestBuilder(FunctionTesterBase<TEntryPoint, TSelf> functionTester, string orchestrationName, Action<DurableTaskRegistry> configureRegistry,
        Action<IServiceCollection>? configureServices, IReadOnlyList<Action<DurableTaskRegistry, DurableTaskActivityVerifier>>? activityRegistrations = null,
        DurableTaskDiscovery? discoveredActivities = null)
    {
        _functionTester = functionTester;
        _orchestrationName = orchestrationName;
        _configureRegistry = configureRegistry;
        _configureServices = configureServices;
        _activityRegistrations = activityRegistrations ?? [];
        _discoveredActivities = discoveredActivities;
    }

    /// <summary>
    /// Registers an asynchronous activity handler.
    /// </summary>
    /// <typeparam name="TActivityInput">The activity input type.</typeparam>
    /// <typeparam name="TActivityOutput">The activity output type.</typeparam>
    /// <param name="name">The activity name.</param>
    /// <param name="activity">The handler, which may invoke a real activity method.</param>
    /// <param name="configure">Optional invocation verification configuration for this activity handler.</param>
    /// <returns>A new builder with the activity handler registered.</returns>
    public DurableTaskNoInputOrchestrationTestBuilder<TEntryPoint, TSelf, TOutput> Activity<TActivityInput, TActivityOutput>(string name,
        Func<TaskActivityContext, TActivityInput, Task<TActivityOutput>> activity, Action<DurableTaskActivityOptions>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(activity);

        var options = new DurableTaskActivityOptions();
        configure?.Invoke(options);

        return AppendActivity((registry, verifier) =>
        {
            Action recordInvocation = verifier.Register(name, options.ExpectedTimes);
            registry.AddActivityFunc<TActivityInput, TActivityOutput>(name, async (context, input) =>
            {
                recordInvocation();
                return await activity(context, input).ConfigureAwait(false);
            });
        });
    }

    /// <summary>
    /// Registers a synchronous activity handler.
    /// </summary>
    /// <typeparam name="TActivityInput">The activity input type.</typeparam>
    /// <typeparam name="TActivityOutput">The activity output type.</typeparam>
    /// <param name="name">The activity name.</param>
    /// <param name="activity">The handler, which may invoke a real activity method.</param>
    /// <param name="configure">Optional invocation verification configuration for this activity handler.</param>
    /// <returns>A new builder with the activity handler registered.</returns>
    public DurableTaskNoInputOrchestrationTestBuilder<TEntryPoint, TSelf, TOutput> Activity<TActivityInput, TActivityOutput>(string name,
        Func<TaskActivityContext, TActivityInput, TActivityOutput> activity, Action<DurableTaskActivityOptions>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(activity);

        var options = new DurableTaskActivityOptions();
        configure?.Invoke(options);

        return AppendActivity((registry, verifier) =>
        {
            Action recordInvocation = verifier.Register(name, options.ExpectedTimes);
            registry.AddActivityFunc<TActivityInput, TActivityOutput>(name, (context, input) =>
            {
                recordInvocation();
                return activity(context, input);
            });
        });
    }

    /// <summary>
    /// Registers a child orchestration delegate.
    /// </summary>
    /// <typeparam name="TChildInput">The child orchestration input type.</typeparam>
    /// <typeparam name="TChildOutput">The child orchestration output type.</typeparam>
    /// <param name="name">The child orchestration name.</param>
    /// <param name="orchestrator">The child orchestration delegate.</param>
    /// <returns>A new builder with the child orchestration registered.</returns>
    public DurableTaskNoInputOrchestrationTestBuilder<TEntryPoint, TSelf, TOutput> SubOrchestrator<TChildInput, TChildOutput>(string name, Func<TaskOrchestrationContext, TChildInput, Task<TChildOutput>> orchestrator)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(orchestrator);

        return Append(registry => registry.AddOrchestratorFuncWithTimerDiagnostics(name, orchestrator));
    }

    /// <summary>
    /// Registers a typed child orchestration instance using the SDK host and managed timer diagnostics.
    /// </summary>
    /// <remarks>The caller owns the instance and its dependencies for the lifetime of the run. The same instance is used during replay and continuation.</remarks>
    /// <typeparam name="TChildInput">The child orchestration input type.</typeparam>
    /// <typeparam name="TChildOutput">The child orchestration output type.</typeparam>
    /// <param name="name">The child orchestration name.</param>
    /// <param name="orchestrator">The typed child orchestration instance.</param>
    /// <returns>A new builder with the child orchestration registered.</returns>
    public DurableTaskNoInputOrchestrationTestBuilder<TEntryPoint, TSelf, TOutput> SubOrchestrator<TChildInput, TChildOutput>(string name, TaskOrchestrator<TChildInput, TChildOutput> orchestrator)
    {
        ArgumentNullException.ThrowIfNull(orchestrator);
        return SubOrchestrator<TChildInput, TChildOutput>(name, orchestrator.RunAsync);
    }

    /// <summary>
    /// Registers a no-input child orchestration delegate.
    /// </summary>
    /// <typeparam name="TChildOutput">The child orchestration output type.</typeparam>
    /// <param name="name">The child orchestration name.</param>
    /// <param name="orchestrator">The child orchestration delegate.</param>
    /// <returns>A new builder with the child orchestration registered.</returns>
    public DurableTaskNoInputOrchestrationTestBuilder<TEntryPoint, TSelf, TOutput> SubOrchestrator<TChildOutput>(string name, Func<TaskOrchestrationContext, Task<TChildOutput>> orchestrator)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(orchestrator);

        return Append(registry => registry.AddOrchestratorFuncWithTimerDiagnostics(name, orchestrator));
    }

    /// <summary>
    /// Adds services to the in-memory Durable Task host's service collection.
    /// </summary>
    /// <param name="configureServices">The host service configuration callback.</param>
    /// <returns>A new builder with the additional host service configuration.</returns>
    public DurableTaskNoInputOrchestrationTestBuilder<TEntryPoint, TSelf, TOutput> ConfigureHostServices(Action<IServiceCollection> configureServices)
    {
        ArgumentNullException.ThrowIfNull(configureServices);
        return new DurableTaskNoInputOrchestrationTestBuilder<TEntryPoint, TSelf, TOutput>(
            _functionTester,
            _orchestrationName,
            _configureRegistry,
            Combine(_configureServices, configureServices),
            _activityRegistrations,
            _discoveredActivities);
    }

    /// <summary>
    /// Starts the orchestration without input.
    /// </summary>
    /// <returns>A disposable test run exposing the SDK host and bounded completion wait.</returns>
    public async Task<DurableTaskTestRun<TOutput>> StartAsync()
    {
        var verifier = new DurableTaskActivityVerifier();
        var discoveryRun = new DurableTaskDiscoveryRun(_functionTester.Services);
        Action<DurableTaskRegistry> configureRegistry = registry =>
        {
            _configureRegistry(registry);
            foreach (Action<DurableTaskRegistry, DurableTaskActivityVerifier> registration in _activityRegistrations)
                registration(registry, verifier);
            discoveryRun.Register(registry, _discoveredActivities);
        };

        DurableTaskTestHost? host = null;
        try
        {
            host = await _functionTester.StartDurableTaskTestHostAsync(configureRegistry, _configureServices).ConfigureAwait(false);
            discoveryRun.SetHost(host);
            string instanceId = await host.Client.ScheduleNewOrchestrationInstanceAsync(_orchestrationName).ConfigureAwait(false);
            return new DurableTaskTestRun<TOutput>(_functionTester, host, instanceId, verifier, discoveryRun);
        }
        catch
        {
            try { if (host is not null) await host.DisposeAsync().ConfigureAwait(false); }
            finally { await discoveryRun.DisposeAsync().ConfigureAwait(false); }
            throw;
        }
    }

    /// <summary>Returns a new builder with the specified registry registration appended.</summary>
    private DurableTaskNoInputOrchestrationTestBuilder<TEntryPoint, TSelf, TOutput> Append(Action<DurableTaskRegistry> registration)
        => new(_functionTester, _orchestrationName,
            registry =>
            {
                _configureRegistry(registry);
                registration(registry);
            },
            _configureServices, _activityRegistrations, _discoveredActivities);

    /// <summary>Returns a builder with an additional activity registration.</summary>
    /// <param name="registration">The activity registration callback.</param>
    /// <returns>A new builder containing the additional activity registration.</returns>
    private DurableTaskNoInputOrchestrationTestBuilder<TEntryPoint, TSelf, TOutput> AppendActivity(Action<DurableTaskRegistry, DurableTaskActivityVerifier> registration)
        => new(_functionTester, _orchestrationName, _configureRegistry, _configureServices, _activityRegistrations.Append(registration).ToArray(), _discoveredActivities);

    /// <summary>Returns a service callback that applies an additional configuration after existing configurations.</summary>
    private static Action<IServiceCollection> Combine(Action<IServiceCollection>? existing, Action<IServiceCollection> next)
        => services =>
        {
            existing?.Invoke(services);
            next(services);
        };
}
