using Microsoft.DurableTask;
using Microsoft.DurableTask.Testing;
using Microsoft.Extensions.DependencyInjection;
using UnitTestEx.Azure.Functions;

namespace UnitTestEx.Azure.Functions.Durable.TestHost;

/// <summary>
/// Builds an in-memory Durable Task orchestration test using the registered SDK host.
/// </summary>
/// <typeparam name="TEntryPoint">A type from the isolated-worker application assembly.</typeparam>
/// <typeparam name="TSelf">The concrete FunctionTester type.</typeparam>
public sealed class DurableTaskTestBuilder<TEntryPoint, TSelf>
    where TEntryPoint : class
    where TSelf : FunctionTesterBase<TEntryPoint, TSelf>
{
    private readonly FunctionTesterBase<TEntryPoint, TSelf> _functionTester;
    private readonly Action<DurableTaskRegistry> _configureRegistry;
    private readonly Action<IServiceCollection>? _configureServices;

    /// <summary>Initializes a builder for a FunctionTester and optional host configuration.</summary>
    /// <param name="functionTester">The owning FunctionTester.</param>
    /// <param name="configureRegistry">The initial SDK registry configuration.</param>
    /// <param name="configureServices">The optional SDK host service configuration.</param>
    internal DurableTaskTestBuilder(FunctionTesterBase<TEntryPoint, TSelf> functionTester, Action<DurableTaskRegistry>? configureRegistry = null, Action<IServiceCollection>? configureServices = null)
    {
        _functionTester = functionTester ?? throw new ArgumentNullException(nameof(functionTester));
        _configureRegistry = configureRegistry ?? (static _ => { });
        _configureServices = configureServices;
    }

    /// <summary>
    /// Adds services to the in-memory Durable Task host's service collection.
    /// </summary>
    /// <param name="configureServices">The host service configuration callback.</param>
    /// <returns>A new builder with the additional host service configuration.</returns>
    public DurableTaskTestBuilder<TEntryPoint, TSelf> ConfigureHostServices(Action<IServiceCollection> configureServices)
    {
        ArgumentNullException.ThrowIfNull(configureServices);
        return new DurableTaskTestBuilder<TEntryPoint, TSelf>(_functionTester, _configureRegistry, Combine(_configureServices, configureServices));
    }

    /// <summary>
    /// Registers a function-based orchestration delegate for this test.
    /// </summary>
    /// <typeparam name="TInput">The orchestration input type.</typeparam>
    /// <typeparam name="TOutput">The orchestration output type.</typeparam>
    /// <param name="name">The orchestration name.</param>
    /// <param name="orchestrator">The orchestration delegate, typically calling the real function method.</param>
    /// <returns>A typed builder for registering handlers and starting the orchestration.</returns>
    public DurableTaskOrchestrationTestBuilder<TEntryPoint, TSelf, TInput, TOutput> Orchestrator<TInput, TOutput>(string name, Func<TaskOrchestrationContext, TInput, Task<TOutput>> orchestrator)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(orchestrator);

        return new DurableTaskOrchestrationTestBuilder<TEntryPoint, TSelf, TInput, TOutput>(_functionTester, name,
            Append(_configureRegistry, registry => registry.AddOrchestratorFuncWithTimerDiagnostics(name, orchestrator)), _configureServices);
    }

    /// <summary>Returns a registry callback that applies an additional registration after existing registrations.</summary>
    private static Action<DurableTaskRegistry> Append(Action<DurableTaskRegistry> existing, Action<DurableTaskRegistry> next)
        => registry =>
        {
            existing(registry);
            next(registry);
        };

    /// <summary>Returns a service callback that applies an additional configuration after existing configurations.</summary>
    private static Action<IServiceCollection> Combine(Action<IServiceCollection>? existing, Action<IServiceCollection> next)
        => services =>
        {
            existing?.Invoke(services);
            next(services);
        };
}

/// <summary>
/// Configures handlers for a typed orchestration and starts its in-memory SDK-host test.
/// </summary>
/// <typeparam name="TEntryPoint">A type from the isolated-worker application assembly.</typeparam>
/// <typeparam name="TSelf">The concrete FunctionTester type.</typeparam>
/// <typeparam name="TInput">The orchestration input type.</typeparam>
/// <typeparam name="TOutput">The orchestration output type.</typeparam>
public sealed class DurableTaskOrchestrationTestBuilder<TEntryPoint, TSelf, TInput, TOutput>
    where TEntryPoint : class
    where TSelf : FunctionTesterBase<TEntryPoint, TSelf>
{
    private readonly FunctionTesterBase<TEntryPoint, TSelf> _functionTester;
    private readonly string _orchestrationName;
    private readonly Action<DurableTaskRegistry> _configureRegistry;
    private readonly Action<IServiceCollection>? _configureServices;

    /// <summary>Initializes a typed orchestration builder.</summary>
    /// <param name="functionTester">The owning FunctionTester.</param>
    /// <param name="orchestrationName">The orchestration name to start.</param>
    /// <param name="configureRegistry">The SDK registry configuration.</param>
    /// <param name="configureServices">The optional SDK host service configuration.</param>
    internal DurableTaskOrchestrationTestBuilder(
        FunctionTesterBase<TEntryPoint, TSelf> functionTester,
        string orchestrationName,
        Action<DurableTaskRegistry> configureRegistry,
        Action<IServiceCollection>? configureServices)
    {
        _functionTester = functionTester;
        _orchestrationName = orchestrationName;
        _configureRegistry = configureRegistry;
        _configureServices = configureServices;
    }

    /// <summary>
    /// Registers an asynchronous activity handler.
    /// </summary>
    /// <typeparam name="TActivityInput">The activity input type.</typeparam>
    /// <typeparam name="TActivityOutput">The activity output type.</typeparam>
    /// <param name="name">The activity name.</param>
    /// <param name="activity">The handler, which may invoke a real activity method.</param>
    /// <returns>A new builder with the activity handler registered.</returns>
    public DurableTaskOrchestrationTestBuilder<TEntryPoint, TSelf, TInput, TOutput> Activity<TActivityInput, TActivityOutput>(string name, Func<TaskActivityContext, TActivityInput, Task<TActivityOutput>> activity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(activity);

        return Append(registry => registry.AddActivityFunc(name, activity));
    }

    /// <summary>
    /// Registers a synchronous activity handler.
    /// </summary>
    /// <typeparam name="TActivityInput">The activity input type.</typeparam>
    /// <typeparam name="TActivityOutput">The activity output type.</typeparam>
    /// <param name="name">The activity name.</param>
    /// <param name="activity">The handler, which may invoke a real activity method.</param>
    /// <returns>A new builder with the activity handler registered.</returns>
    public DurableTaskOrchestrationTestBuilder<TEntryPoint, TSelf, TInput, TOutput> Activity<TActivityInput, TActivityOutput>(string name, Func<TaskActivityContext, TActivityInput, TActivityOutput> activity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(activity);

        return Append(registry => registry.AddActivityFunc(name, activity));
    }

    /// <summary>
    /// Registers a child orchestration delegate.
    /// </summary>
    /// <typeparam name="TChildInput">The child orchestration input type.</typeparam>
    /// <typeparam name="TChildOutput">The child orchestration output type.</typeparam>
    /// <param name="name">The child orchestration name.</param>
    /// <param name="orchestrator">The child orchestration delegate.</param>
    /// <returns>A new builder with the child orchestration registered.</returns>
    public DurableTaskOrchestrationTestBuilder<TEntryPoint, TSelf, TInput, TOutput> SubOrchestrator<TChildInput, TChildOutput>(string name, Func<TaskOrchestrationContext, TChildInput, Task<TChildOutput>> orchestrator)
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
    public DurableTaskOrchestrationTestBuilder<TEntryPoint, TSelf, TInput, TOutput> ConfigureHostServices(Action<IServiceCollection> configureServices)
    {
        ArgumentNullException.ThrowIfNull(configureServices);
        return new DurableTaskOrchestrationTestBuilder<TEntryPoint, TSelf, TInput, TOutput>(_functionTester, _orchestrationName, _configureRegistry, Combine(_configureServices, configureServices));
    }

    /// <summary>
    /// Starts the orchestration with the specified input.
    /// </summary>
    /// <param name="input">The orchestration input.</param>
    /// <returns>A disposable test run exposing the SDK host and bounded completion wait.</returns>
    public async Task<DurableTaskTestRun<TOutput>> StartAsync(TInput input)
    {
        DurableTaskTestHost host = await _functionTester.StartDurableTaskTestHostAsync(_configureRegistry, _configureServices).ConfigureAwait(false);
        try
        {
            string instanceId = await host.Client.ScheduleNewOrchestrationInstanceAsync(_orchestrationName, input).ConfigureAwait(false);
            return new DurableTaskTestRun<TOutput>(_functionTester, host, instanceId);
        }
        catch
        {
            await host.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Returns a new builder with the specified registry registration appended.</summary>
    private DurableTaskOrchestrationTestBuilder<TEntryPoint, TSelf, TInput, TOutput> Append(Action<DurableTaskRegistry> registration)
        => new(_functionTester, _orchestrationName,
            registry =>
            {
                _configureRegistry(registry);
                registration(registry);
            },
            _configureServices);

    /// <summary>Returns a service callback that applies an additional configuration after existing configurations.</summary>
    private static Action<IServiceCollection> Combine(Action<IServiceCollection>? existing, Action<IServiceCollection> next)
        => services =>
        {
            existing?.Invoke(services);
            next(services);
        };
}
