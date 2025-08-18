using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using UnitTestEx.Abstractions;
using UnitTestEx.Assertors;
using UnitTestEx.Expectations;
using UnitTestEx.Json;

namespace UnitTestEx.Hosting;

/// <summary>
/// Provides a pre-<i>scoped</i> <typeparamref name="TService"/> unit-testing capabilities from a parent/owning host (see <see cref="TesterBase"/>).
/// </summary>
/// <typeparam name="TService">The service <see cref="Type"/> (must be a <c>class</c>).</typeparam>
/// <remarks>The scoped <see cref="Service"/> instance lifetime is managed outside of <see cref="ScopedTypeTester{TService}"/> lifetime.</remarks>
public class ScopedTypeTester<TService> : HostTesterBase<TService, ScopedTypeTester<TService>>, IExpectations<ScopedTypeTester<TService>> where TService : class
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ScopedTypeTester{TService}"/> class.
    /// </summary>
    /// <param name="owner">The owning <see cref="TesterBase"/>.</param>
    /// <param name="serviceProvider">The <see cref="IServiceProvider"/>.</param>
    /// <param name="service">The <typeparamref name="TService"/> instance.</param>
    public ScopedTypeTester(TesterBase owner, IServiceProvider serviceProvider, TService service) : base(owner, serviceProvider)
    {
        Service = service ?? throw new ArgumentNullException(nameof(service));
        ExpectationsArranger = new ExpectationsArranger<ScopedTypeTester<TService>>(owner, this);
    }

    /// <summary>
    /// Gets the <typeparamref name="TService"/> instance being tested.
    /// </summary>
    public TService Service { get; }

    /// <summary>
    /// Gets the <see cref="ExpectationsArranger{TSelf}"/>.
    /// </summary>
    public ExpectationsArranger<ScopedTypeTester<TService>> ExpectationsArranger { get; }

    /// <summary>
    /// Runs the synchronous method with no result.
    /// </summary>
    /// <param name="function">The function execution.</param>
    /// <returns>A <see cref="VoidAssertor"/>.</returns>
    public VoidAssertor Run(Action<TService> function) => RunAsync(x => { function(x); return Task.CompletedTask; }).GetAwaiter().GetResult();

    /// <summary>
    /// Runs the synchronous method with a result.
    /// </summary>
    /// <typeparam name="TValue">The result value <see cref="Type"/>.</typeparam>
    /// <param name="function">The function execution.</param>
    /// <returns>A <see cref="ValueAssertor{TValue}"/>.</returns>
    public ValueAssertor<TValue> Run<TValue>(Func<TService, TValue> function) => RunAsync(x => Task.FromResult(function(x))).GetAwaiter().GetResult();

    /// <summary>
    /// Runs the asynchronous method with no result.
    /// </summary>
    /// <param name="function">The function execution.</param>
    /// <returns>A <see cref="VoidAssertor"/>.</returns>
#if NET9_0_OR_GREATER
    [OverloadResolutionPriority(1)]
#endif
    public VoidAssertor Run(Func<TService, Task> function) => RunAsync(function).GetAwaiter().GetResult();

#if NET9_0_OR_GREATER
    /// <summary>
    /// Runs the asynchronous method with no result.
    /// </summary>
    /// <param name="function">The function execution.</param>
    /// <returns>A <see cref="VoidAssertor"/>.</returns>
    [OverloadResolutionPriority(2)]
    public VoidAssertor Run(Func<TService, ValueTask> function) => RunAsync(v => function(v).AsTask()).GetAwaiter().GetResult();

#endif
    /// <summary>
    /// Runs the asynchronous method with no result.
    /// </summary>
    /// <param name="function">The function execution.</param>
    /// <returns>A <see cref="VoidAssertor"/>.</returns>
#if NET9_0_OR_GREATER
    [OverloadResolutionPriority(1)]
#endif
    public async Task<VoidAssertor> RunAsync(Func<TService, Task> function)
    {
        TestSetUp.LogAutoSetUpOutputs(Implementor);

        Exception? ex = null;
        var sw = Stopwatch.StartNew();
        LogHeader();

        try
        {
            await (function ?? throw new ArgumentNullException(nameof(function)))(Service).ConfigureAwait(false);
        }
        catch (AggregateException aex)
        {
            ex = aex.InnerException ?? aex;
        }
        catch (Exception uex)
        {
            ex = uex;
        }
        finally
        {
            sw.Stop();
        }

        await Task.Delay(TestSetUp.TaskDelayMilliseconds).ConfigureAwait(false);
        var logs = Owner.SharedState.GetLoggerMessages();
        LogResult(ex, sw.Elapsed.TotalMilliseconds, logs);

        await ExpectationsArranger.AssertAsync(logs, ex).ConfigureAwait(false);

        return new VoidAssertor(Owner, ex);
    }

#if NET9_0_OR_GREATER
    /// <summary>
    /// Runs the asynchronous method with no result.
    /// </summary>
    /// <param name="function">The function execution.</param>
    /// <returns>A <see cref="VoidAssertor"/>.</returns>
    [OverloadResolutionPriority(2)]
    public async Task<VoidAssertor> RunAsync(Func<TService, ValueTask> function) => await RunAsync(v => function(v).AsTask()).ConfigureAwait(false);

#endif
    /// <summary>
    /// Runs the asynchronous method with a result.
    /// </summary>
    /// <typeparam name="TValue">The result value <see cref="Type"/>.</typeparam>
    /// <param name="function">The function execution.</param>
    /// <returns>A <see cref="ValueAssertor{TValue}"/>.</returns>
#if NET9_0_OR_GREATER
    [OverloadResolutionPriority(1)]
#endif
    public ValueAssertor<TValue> Run<TValue>(Func<TService, Task<TValue>> function) => RunAsync(function).GetAwaiter().GetResult();

#if NET9_0_OR_GREATER
    /// <summary>
    /// Runs the asynchronous method with a result.
    /// </summary>
    /// <typeparam name="TValue">The result value <see cref="Type"/>.</typeparam>
    /// <param name="function">The function execution.</param>
    /// <returns>A <see cref="ValueAssertor{TValue}"/>.</returns>
    [OverloadResolutionPriority(2)]
    public ValueAssertor<TValue> Run<TValue>(Func<TService, ValueTask<TValue>> function) => RunAsync(v => function(v).AsTask()).GetAwaiter().GetResult();

#endif
    /// <summary>
    /// Runs the asynchronous method with a result.
    /// </summary>
    /// <typeparam name="TValue">The result value <see cref="Type"/>.</typeparam>
    /// <param name="function">The function execution.</param>
    /// <returns>A <see cref="ValueAssertor{TValue}"/>.</returns>
#if NET9_0_OR_GREATER
    [OverloadResolutionPriority(1)]
#endif
    public async Task<ValueAssertor<TValue>> RunAsync<TValue>(Func<TService, Task<TValue>> function)
    {
        TestSetUp.LogAutoSetUpOutputs(Implementor);

        TValue result = default!;
        Exception? ex = null;
        var sw = Stopwatch.StartNew();
        LogHeader();

        try
        {
            result = await (function ?? throw new ArgumentNullException(nameof(function)))(Service).ConfigureAwait(false);
        }
        catch (AggregateException aex)
        {
            ex = aex.InnerException ?? aex;
        }
        catch (Exception uex)
        {
            ex = uex;
        }
        finally
        {
            sw.Stop();
        }

        await Task.Delay(TestSetUp.TaskDelayMilliseconds).ConfigureAwait(false);
        var logs = Owner.SharedState.GetLoggerMessages();
        LogResult(ex, sw.Elapsed.TotalMilliseconds, logs);

        if (ex == null)
        {
            if (result is string str)
                Implementor.WriteLine($"Result: {str}");
            else if (result is IFormattable ifm)
                Implementor.WriteLine($"Result: {ifm.ToString(null, CultureInfo.CurrentCulture)}");
            else
            {
                Implementor.WriteLine($"Result: {(result == null ? "<null>" : result.GetType().Name)}");
                if (result != null)
                    Implementor.WriteLine(JsonSerializer.Serialize<dynamic>(result, JsonWriteFormat.Indented));
            }
        }

        await ExpectationsArranger.AssertValueAsync(logs, result, ex).ConfigureAwait(false);

        return new ValueAssertor<TValue>(Owner, result, ex);
    }

#if NET9_0_OR_GREATER
    /// <summary>
    /// Runs the asynchronous method with a result.
    /// </summary>
    /// <typeparam name="TValue">The result value <see cref="Type"/>.</typeparam>
    /// <param name="function">The function execution.</param>
    /// <returns>A <see cref="ValueAssertor{TValue}"/>.</returns>
    [OverloadResolutionPriority(2)]
    public async Task<ValueAssertor<TValue>> RunAsync<TValue>(Func<TService, ValueTask<TValue>> function) => await RunAsync(v => function(v).AsTask()).ConfigureAwait(false);

#endif
    /// <summary>
    /// Logs the header.
    /// </summary>
    private void LogHeader()
    {
        Implementor.WriteLine("");
        Implementor.WriteLine("TYPE TESTER...");
        Implementor.WriteLine($"Type: {typeof(TService).Name} [{typeof(TService).FullName}]");
    }

    /// <summary>
    /// Log the elapsed execution time.
    /// </summary>
    private void LogResult(Exception? ex, double ms, IEnumerable<string?>? logs)
    {
        Implementor.WriteLine("");
        Implementor.WriteLine("LOGGING >");
        if (logs is not null && logs.Any())
        {
            foreach (var msg in logs)
            {
                Implementor.WriteLine(msg);
            }
        }
        else
            Implementor.WriteLine("None.");

        Implementor.WriteLine("");
        Implementor.WriteLine("RESULT >");
        Implementor.WriteLine($"Elapsed (ms): {ms}");
        if (ex != null)
        {
            Implementor.WriteLine($"Exception: {ex.Message} [{ex.GetType().Name}]");
            Implementor.WriteLine(ex.ToString());
        }
    }
}