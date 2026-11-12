using Microsoft.DurableTask;
using Microsoft.DurableTask.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UnitTestEx.Azure.Functions;

namespace UnitTestEx.Azure.Functions.Durable.TestHost;

/// <summary>
/// Provides integration between the in-memory Durable Task host and FunctionTester.
/// </summary>
public static class DurableTaskTestHostExtensions
{
    /// <summary>
    /// Creates a fluent in-memory Durable Task test builder.
    /// </summary>
    /// <typeparam name="TEntryPoint">A type from the isolated-worker application assembly.</typeparam>
    /// <typeparam name="TSelf">The concrete FunctionTester type.</typeparam>
    /// <param name="functionTester">The FunctionTester supplying captured logging.</param>
    /// <returns>A builder for registering an orchestration and its handlers.</returns>
    public static DurableTaskTestBuilder<TEntryPoint, TSelf> DurableTask<TEntryPoint, TSelf>(this FunctionTesterBase<TEntryPoint, TSelf> functionTester)
        where TEntryPoint : class
        where TSelf : FunctionTesterBase<TEntryPoint, TSelf>
    {
        ArgumentNullException.ThrowIfNull(functionTester);
        return new DurableTaskTestBuilder<TEntryPoint, TSelf>(functionTester);
    }

    /// <summary>
    /// Starts an in-memory Durable Task host using the FunctionTester's captured logger factory.
    /// </summary>
    /// <typeparam name="TEntryPoint">A type from the isolated-worker application assembly.</typeparam>
    /// <typeparam name="TSelf">The concrete FunctionTester type.</typeparam>
    /// <param name="functionTester">The FunctionTester supplying captured logging.</param>
    /// <param name="configure">The Durable Task registry configuration.</param>
    /// <param name="configureServices">Optional configuration for the Durable Task host's services.</param>
    /// <returns>The started in-memory Durable Task host.</returns>
    public static Task<DurableTaskTestHost> StartDurableTaskTestHostAsync<TEntryPoint, TSelf>(this FunctionTesterBase<TEntryPoint, TSelf> functionTester, Action<DurableTaskRegistry> configure, Action<IServiceCollection>? configureServices = null)
        where TEntryPoint : class
        where TSelf : FunctionTesterBase<TEntryPoint, TSelf>
    {
        ArgumentNullException.ThrowIfNull(functionTester);
        ArgumentNullException.ThrowIfNull(configure);

        return DurableTaskTestHost.StartAsync(configure, new DurableTaskTestHostOptions
        {
            LoggerFactory = functionTester.Services.GetRequiredService<ILoggerFactory>(),
            ConfigureServices = configureServices,
        });
    }
}
