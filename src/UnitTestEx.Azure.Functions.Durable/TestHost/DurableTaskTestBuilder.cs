using Microsoft.DurableTask;
using Microsoft.DurableTask.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Azure.Functions.Worker;
using System.Reflection;
using UnitTestEx.Azure.Functions;

namespace UnitTestEx.Azure.Functions.Durable.TestHost;

/// <summary>
/// Builds an in-memory Durable Task orchestration test using the registered SDK host.
/// </summary>
/// <typeparam name="TEntryPoint">A type from the isolated-worker application assembly.</typeparam>
/// <typeparam name="TSelf">The concrete FunctionTester type.</typeparam>
public sealed class DurableTaskTestBuilder<TEntryPoint, TSelf> where TEntryPoint : class where TSelf : FunctionTesterBase<TEntryPoint, TSelf>
{
    private readonly FunctionTesterBase<TEntryPoint, TSelf> _functionTester;
    private readonly Action<DurableTaskRegistry> _configureRegistry;
    private readonly Action<IServiceCollection>? _configureServices;
    private readonly DurableTaskDiscovery? _discoveredActivities;

    /// <summary>Initializes a builder for a FunctionTester and optional host configuration.</summary>
    /// <param name="functionTester">The owning FunctionTester.</param>
    /// <param name="configureRegistry">The initial SDK registry configuration.</param>
    /// <param name="configureServices">The optional SDK host service configuration.</param>
    /// <param name="discoveredActivities">The validated activity methods from explicitly selected assemblies.</param>
    internal DurableTaskTestBuilder(FunctionTesterBase<TEntryPoint, TSelf> functionTester, Action<DurableTaskRegistry>? configureRegistry = null, Action<IServiceCollection>? configureServices = null,
        DurableTaskDiscovery? discoveredActivities = null)
    {
        _functionTester = functionTester ?? throw new ArgumentNullException(nameof(functionTester));
        _configureRegistry = configureRegistry ?? (static _ => { });
        _configureServices = configureServices;
        _discoveredActivities = discoveredActivities;
    }

    /// <summary>Discovers Functions activities and orchestrations only in the parent FunctionTester's entry-point assembly.</summary>
    /// <returns>A builder with discovered handlers; explicit registrations take precedence.</returns>
    public DurableTaskTestBuilder<TEntryPoint, TSelf> WithSelfDiscovery() => WithSelfDiscovery<TEntryPoint>();

    /// <summary>Discovers Functions activities and orchestrations only in the specified marker type's assembly, without scanning referenced assemblies.</summary>
    /// <typeparam name="T">A type identifying the assembly to scan.</typeparam>
    /// <returns>A builder with the additional handlers; repeated discovery is idempotent and explicit registrations take precedence.</returns>
    /// <exception cref="InvalidOperationException">Two different discovered methods of the same kind have the same Function name.</exception>
    /// <exception cref="NotSupportedException">A discovered method has an unsupported signature.</exception>
    public DurableTaskTestBuilder<TEntryPoint, TSelf> WithSelfDiscovery<T>()
    {
        var discovery = _discoveredActivities?.Copy() ?? new DurableTaskDiscovery();
        foreach (MethodInfo method in typeof(T).Assembly.GetTypes().SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)))
        {
            FunctionAttribute? function = method.GetCustomAttribute<FunctionAttribute>();
            if (function is null)
                continue;

            bool activity = method.GetParameters().Any(DiscoveredDurableTaskActivity.IsActivityInput);
            bool orchestration = method.GetParameters().Any(DiscoveredDurableTaskOrchestrator.IsOrchestrationInput);
            if (!activity && !orchestration)
                continue;
            if (activity && orchestration)
                throw new NotSupportedException($"Discovered function '{function.Name}' cannot be both an activity and an orchestration.");
            ArgumentException.ThrowIfNullOrWhiteSpace(function.Name);
            if (activity)
            {
                if (discovery.Activities.TryGetValue(function.Name, out var existing))
                {
                    if (existing.Method == method)
                        continue;
                    throw new InvalidOperationException($"Discovered activity name '{function.Name}' is used by both '{existing.Method.DeclaringType?.FullName}.{existing.Method.Name}' and '{method.DeclaringType?.FullName}.{method.Name}'.");
                }
                discovery.Activities.Add(function.Name, new DiscoveredDurableTaskActivity(method));
            }
            else
            {
                if (discovery.Orchestrators.TryGetValue(function.Name, out var existing))
                {
                    if (existing.Method == method)
                        continue;
                    throw new InvalidOperationException($"Discovered orchestration name '{function.Name}' is used by both '{existing.Method.DeclaringType?.FullName}.{existing.Method.Name}' and '{method.DeclaringType?.FullName}.{method.Name}'.");
                }
                discovery.Orchestrators.Add(function.Name, new DiscoveredDurableTaskOrchestrator(method));
            }
        }

        return new DurableTaskTestBuilder<TEntryPoint, TSelf>(_functionTester, _configureRegistry, _configureServices, discovery);
    }

    /// <summary>
    /// Adds services to the in-memory Durable Task host's service collection.
    /// </summary>
    /// <param name="configureServices">The host service configuration callback.</param>
    /// <returns>A new builder with the additional host service configuration.</returns>
    public DurableTaskTestBuilder<TEntryPoint, TSelf> ConfigureHostServices(Action<IServiceCollection> configureServices)
    {
        ArgumentNullException.ThrowIfNull(configureServices);
        return new DurableTaskTestBuilder<TEntryPoint, TSelf>(_functionTester, _configureRegistry, Combine(_configureServices, configureServices), _discoveredActivities);
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
            Append(_configureRegistry, registry => registry.AddOrchestratorFuncWithTimerDiagnostics(name, orchestrator)), _configureServices, discoveredActivities: _discoveredActivities);
    }

    /// <summary>
    /// Registers a typed orchestration instance using the SDK host and managed timer diagnostics.
    /// </summary>
    /// <remarks>The caller constructs and owns the instance and its dependencies, which must remain alive until the test run is disposed. The same instance is used during replay and continuation.</remarks>
    /// <typeparam name="TInput">The orchestration input type.</typeparam>
    /// <typeparam name="TOutput">The orchestration output type.</typeparam>
    /// <param name="name">The orchestration name.</param>
    /// <param name="orchestrator">The typed orchestration instance, optionally constructed using application services.</param>
    /// <returns>A typed builder for registering handlers and starting the orchestration.</returns>
    public DurableTaskOrchestrationTestBuilder<TEntryPoint, TSelf, TInput, TOutput> Orchestrator<TInput, TOutput>(string name, TaskOrchestrator<TInput, TOutput> orchestrator)
    {
        ArgumentNullException.ThrowIfNull(orchestrator);
        return Orchestrator<TInput, TOutput>(name, orchestrator.RunAsync);
    }

    /// <summary>
    /// Registers a no-input orchestration delegate for this test.
    /// </summary>
    /// <typeparam name="TOutput">The orchestration output type.</typeparam>
    /// <param name="name">The orchestration name.</param>
    /// <param name="orchestrator">The orchestration delegate, typically calling the real function method.</param>
    /// <returns>A builder for registering handlers and starting the orchestration without input.</returns>
    public DurableTaskNoInputOrchestrationTestBuilder<TEntryPoint, TSelf, TOutput> Orchestrator<TOutput>(string name, Func<TaskOrchestrationContext, Task<TOutput>> orchestrator)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(orchestrator);

        return new DurableTaskNoInputOrchestrationTestBuilder<TEntryPoint, TSelf, TOutput>(_functionTester, name,
            Append(_configureRegistry, registry => registry.AddOrchestratorFuncWithTimerDiagnostics(name, orchestrator)), _configureServices, discoveredActivities: _discoveredActivities);
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
