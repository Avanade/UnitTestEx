// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.Extensions.DependencyInjection;
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

namespace UnitTestEx.Hosting
{
    /// <summary>
    /// Provides the generic <typeparamref name="TService"/> <see cref="Type"/> unit-testing capabilities.
    /// </summary>
    /// <typeparam name="TService">The service <see cref="Type"/> (must be a <c>class</c>).</typeparam>
    /// <remarks>Note that the <typeparamref name="TService"/> service instance is created on first use and then reused (see <see cref="GetOrCreateService"/>).</remarks>
    public class TypeTester<TService> : HostTesterBase<TService>, IExpectations<TypeTester<TService>> where TService : class
    {
        private readonly object? _serviceKey;
        private readonly Func<IServiceProvider, TService>? _serviceFactory;
        private TService? _service;
        private bool _runAsScoped;
        private Func<IServiceScope, Task>? _onRunScopeFuncAsync;

        /// <summary>
        /// Initializes a new <see cref="TypeTester{TFunction}"/> class.
        /// </summary>
        /// <param name="owner">The owning <see cref="TesterBase"/>.</param>
        /// <param name="serviceScope">The <see cref="IServiceScope"/>.</param>
        /// <param name="serviceKey">The optional key for a keyed service.</param>
        public TypeTester(TesterBase owner, IServiceScope serviceScope, object? serviceKey = null) : base(owner, serviceScope)
        {
            _serviceKey = serviceKey;
            ExpectationsArranger = new ExpectationsArranger<TypeTester<TService>>(owner, this);
        }

        /// <summary>
        /// Initializes a new <see cref="TypeTester{TFunction}"/> class with a factory for creating the <typeparamref name="TService"/> instance.
        /// </summary>
        /// <param name="owner">The owning <see cref="TesterBase"/>.</param>
        /// <param name="serviceScope">The <see cref="IServiceScope"/>.</param>
        /// <param name="serviceFactory">The factory to create the <typeparamref name="TService"/> instance.</param>
        /// <exception cref="ArgumentNullException"></exception>
        public TypeTester(TesterBase owner, IServiceScope serviceScope, Func<IServiceProvider, TService> serviceFactory) : base(owner, serviceScope)
        {
            _serviceFactory = serviceFactory ?? throw new ArgumentNullException(nameof(serviceFactory));
            ExpectationsArranger = new ExpectationsArranger<TypeTester<TService>>(owner, this);
        }

        /// <summary>
        /// Indicates that the underlying <b>Run</b> methods should be scoped (i.e. <see cref="ServiceProviderServiceExtensions.CreateScope(IServiceProvider)"/>.
        /// </summary>
        /// <param name="onRunAsScoped">The optional function to execute before the primary <b>Run*</b> methods when running as scoped.</param>
        /// <returns>The tester to support fluent-style method-chaining.</returns>
        /// <remarks>By default the <b>Run</b> methods are not scoped.</remarks>
        public TypeTester<TService> UseRunAsScoped(Func<IServiceScope, Task>? onRunAsScoped = null)
        {
            _runAsScoped = true;
            _onRunScopeFuncAsync = onRunAsScoped;
            return this;
        }

        /// <summary>
        /// Indicates that the underlying <b>Run</b> methods should be scoped (i.e. <see cref="ServiceProviderServiceExtensions.CreateScope(IServiceProvider)"/>.
        /// </summary>
        /// <param name="runAsScoped"><see langword="true"/> indicates scoped; otherwise, <see langword="false"/>.</param>
        /// <returns>The tester to support fluent-style method-chaining.</returns>
        /// <remarks>By default the <b>Run</b> methods are not scoped.</remarks>
        public TypeTester<TService> UseRunAsScoped(bool runAsScoped)
        {
            _runAsScoped = runAsScoped;
            _onRunScopeFuncAsync = null;
            return this;
        }

        /// <summary>
        /// Gets the <see cref="ExpectationsArranger{TSelf}"/>.
        /// </summary>
        public ExpectationsArranger<TypeTester<TService>> ExpectationsArranger { get; }

        /// <summary>
        /// Gets or creates the <typeparamref name="TService"/> service instance.
        /// </summary>
        /// <remarks>This is intended for advanced scenarios; for the most part the <c>Run</c> or <c>RunAsync</c> methods should be used for testing as these encapsulate logging, expectations and assertions.</remarks>
        public TService GetOrCreateService() => _service ??= _serviceFactory is null
            ? ServiceScope.ServiceProvider.CreateInstance<TService>(_serviceKey)
            : _serviceFactory(ServiceScope.ServiceProvider);

        /// <summary>
        /// Resets the <typeparamref name="TService"/> service instance.
        /// </summary>
        /// <returns>The tester to support fluent-style method-chaining.</returns>
        public TypeTester<TService> ResetService()
        {
            _service = default;
            return this;
        }

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

            IServiceScope? scope = null;
            Exception? ex = null;
            var sw = Stopwatch.StartNew();
            try
            {
                LogHeader();
                await OnBeforeRunAsync().ConfigureAwait(false);
                if (OnBeforeRunFuncAsync is not null)
                    await OnBeforeRunFuncAsync().ConfigureAwait(false);

                scope = _runAsScoped ? ServiceScope.ServiceProvider.CreateScope() : null;
                if (scope is not null)
                {
                    await OnRunScopeAsync(scope).ConfigureAwait(false);
                    if (_onRunScopeFuncAsync is not null)
                        await _onRunScopeFuncAsync(scope).ConfigureAwait(false);
                }

                var f = GetOrCreateService();
                await (function ?? throw new ArgumentNullException(nameof(function)))(f).ConfigureAwait(false);
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
                scope?.Dispose();
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

            IServiceScope? scope = null;
            TValue result = default!;
            Exception? ex = null;
            var sw = Stopwatch.StartNew();
            try
            {
                LogHeader();
                await OnBeforeRunAsync().ConfigureAwait(false);
                if (OnBeforeRunFuncAsync is not null)
                    await OnBeforeRunFuncAsync().ConfigureAwait(false);

                scope = _runAsScoped ? ServiceScope.ServiceProvider.CreateScope() : null;
                if (scope is not null)
                {
                    await OnRunScopeAsync(scope).ConfigureAwait(false);
                    if (_onRunScopeFuncAsync is not null)
                        await _onRunScopeFuncAsync(scope).ConfigureAwait(false);
                }

                var f = GetOrCreateService();
                result = await (function ?? throw new ArgumentNullException(nameof(function)))(f).ConfigureAwait(false);
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
                scope?.Dispose();
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

        /// <summary>
        /// Provides an opportunity to perform any pre-run logic.
        /// </summary>
        protected virtual Task OnBeforeRunAsync() => Task.CompletedTask;

        /// <summary>
        /// Gets or sets the function to perform any pre-run logic.
        /// </summary>
        public Func<Task>? OnBeforeRunFuncAsync { get; set; }

        /// <summary>
        /// Provides an opportunity to perform any logic as a result of the <see cref="UseRunAsScoped"/>.
        /// </summary>
        /// <remarks>This is invoked after the <see cref="OnBeforeRunAsync"/>, but before the <b>Run</b> logic.</remarks>
        protected virtual Task OnRunScopeAsync(IServiceScope scope) => Task.CompletedTask;
    }
}