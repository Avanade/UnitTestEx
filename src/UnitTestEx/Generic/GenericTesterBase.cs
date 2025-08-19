// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using UnitTestEx.Abstractions;
using UnitTestEx.Assertors;
using UnitTestEx.Hosting;
using UnitTestEx.Json;

namespace UnitTestEx.Generic
{
    /// <summary>
    /// Provides generic testing capabilities.
    /// </summary>
    /// <typeparam name="TEntryPoint">The <see cref="EntryPoint"/> <see cref="Type"/>.</typeparam>
    /// <typeparam name="TSelf">The <see cref="GenericTesterBase{TEntryPoint, TSelf}"/> to support inheriting fluent-style method-chaining.</typeparam>
    /// <param name="implementor">The <see cref="TestFrameworkImplementor"/>.</param>
    public abstract class GenericTesterBase<TEntryPoint, TSelf>(TestFrameworkImplementor implementor)
        : GenericTesterCore<TEntryPoint, GenericTesterBase<TEntryPoint, TSelf>>(implementor) where TEntryPoint : class where TSelf : GenericTesterBase<TEntryPoint, TSelf>
    {
        /// <summary>
        /// Executes the <paramref name="action"/> that performs the logic.
        /// </summary>
        /// <param name="action">The function performing the logic.</param>
        /// <returns>The resulting <see cref="VoidAssertor"/>.</returns>
        public VoidAssertor Run(Action action) => RunAsync(() =>
        {
            (action ?? throw new ArgumentNullException(nameof(action))).Invoke();
            return Task.CompletedTask;
        }).GetAwaiter().GetResult();

        /// <summary>
        /// Executes the <paramref name="action"/> that performs the logic.
        /// </summary>
        /// <typeparam name="TService">The configured service <see cref="Type"/> to instantiate.</typeparam>
        /// <param name="action">The function performing the logic.</param>
        /// <param name="serviceKey">The optional keyed service key.</param>
        /// <returns>The resulting <see cref="VoidAssertor"/>.</returns>
        public VoidAssertor Run<TService>(Action<TService> action, object? serviceKey = null) where TService : class => RunAsync(() =>
        {
            var service = serviceKey is null ? Services.GetRequiredService<TService>() : Services.GetRequiredKeyedService<TService>(serviceKey);
            (action ?? throw new ArgumentNullException(nameof(action))).Invoke(service);
            return Task.CompletedTask;
        }).GetAwaiter().GetResult();

        /// <summary>
        /// Executes the <paramref name="action"/> that performs the logic.
        /// </summary>
        /// <typeparam name="TService">The configured service <see cref="Type"/> to instantiate.</typeparam>
        /// <param name="action">The function performing the logic.</param>
        /// <param name="serviceFactory">The factory to create the <typeparamref name="TService"/> instance.</param>
        /// <returns>The resulting <see cref="VoidAssertor"/>.</returns>
        public VoidAssertor Run<TService>(Action<TService> action, Func<IServiceProvider, TService> serviceFactory) where TService : class => RunAsync(() =>
        {
            var service = serviceFactory(Services);
            (action ?? throw new ArgumentNullException(nameof(action))).Invoke(service);
            return Task.CompletedTask;
        }).GetAwaiter().GetResult();

        /// <summary>
        /// Executes the <paramref name="function"/> that performs the logic.
        /// </summary>
        /// <param name="function">The function performing the logic.</param>
        /// <returns>The resulting <see cref="VoidAssertor"/>.</returns>
#if NET9_0_OR_GREATER
        [OverloadResolutionPriority(1)]
#endif
        public VoidAssertor Run(Func<Task> function) => RunAsync(function).GetAwaiter().GetResult();

#if NET9_0_OR_GREATER
        /// <summary>
        /// Executes the <paramref name="function"/> that performs the logic.
        /// </summary>
        /// <param name="function">The function performing the logic.</param>
        /// <returns>The resulting <see cref="VoidAssertor"/>.</returns>
        [OverloadResolutionPriority(2)]
        public VoidAssertor Run(Func<ValueTask> function) => RunAsync(() => function().AsTask()).GetAwaiter().GetResult();

#endif
        /// <summary>
        /// Executes the <paramref name="function"/> that performs the logic on the specified <typeparamref name="TService"/>.
        /// </summary>
        /// <typeparam name="TService">The configured service <see cref="Type"/> to instantiate.</typeparam>
        /// <param name="function">The function performing the logic.</param>
        /// <param name="serviceKey">The optional keyed service key.</param>
        /// <returns>The resulting <see cref="VoidAssertor"/>.</returns>
#if NET9_0_OR_GREATER
        [OverloadResolutionPriority(1)]
#endif
        public VoidAssertor Run<TService>(Func<TService, Task> function, object? serviceKey = null) where TService : class
        {
            var service = serviceKey is null ? Services.GetRequiredService<TService>() : Services.GetRequiredKeyedService<TService>(serviceKey);
            return RunAsync(() => function(service)).GetAwaiter().GetResult();
        }

#if NET9_0_OR_GREATER
        /// <summary>
        /// Executes the <paramref name="function"/> that performs the logic on the specified <typeparamref name="TService"/>.
        /// </summary>
        /// <typeparam name="TService">The configured service <see cref="Type"/> to instantiate.</typeparam>
        /// <param name="function">The function performing the logic.</param>
        /// <param name="serviceKey">The optional keyed service key.</param>
        /// <returns>The resulting <see cref="VoidAssertor"/>.</returns>
        [OverloadResolutionPriority(2)]
        public VoidAssertor Run<TService>(Func<TService, ValueTask> function, object? serviceKey = null) where TService : class
        {
            var service = serviceKey is null ? Services.GetRequiredService<TService>() : Services.GetRequiredKeyedService<TService>(serviceKey);
            return RunAsync(() => function(service).AsTask()).GetAwaiter().GetResult();
        }

#endif
        /// <summary>
        /// Executes the <paramref name="function"/> that performs the logic on the specified <typeparamref name="TService"/>.
        /// </summary>
        /// <typeparam name="TService">The configured service <see cref="Type"/> to instantiate.</typeparam>
        /// <param name="function">The function performing the logic.</param>
        /// <param name="serviceFactory">The factory to create the <typeparamref name="TService"/> instance.</param>
        /// <returns>The resulting <see cref="VoidAssertor"/>.</returns>
#if NET9_0_OR_GREATER
        [OverloadResolutionPriority(1)]
#endif
        public VoidAssertor Run<TService>(Func<TService, Task> function, Func<IServiceProvider, TService> serviceFactory) where TService : class
        {
            var service = serviceFactory(Services);
            return RunAsync(() => function(service)).GetAwaiter().GetResult();
        }

#if NET9_0_OR_GREATER
        /// <summary>
        /// Executes the <paramref name="function"/> that performs the logic on the specified <typeparamref name="TService"/>.
        /// </summary>
        /// <typeparam name="TService">The configured service <see cref="Type"/> to instantiate.</typeparam>
        /// <param name="function">The function performing the logic.</param>
        /// <param name="serviceFactory">The factory to create the <typeparamref name="TService"/> instance.</param>
        /// <returns>The resulting <see cref="VoidAssertor"/>.</returns>
        [OverloadResolutionPriority(2)]
        public VoidAssertor Run<TService>(Func<TService, ValueTask> function, Func<IServiceProvider, TService> serviceFactory) where TService : class
        {
            var service = serviceFactory(Services);
            return RunAsync(() => function(service).AsTask()).GetAwaiter().GetResult();
        }

#endif
        /// <summary>
        /// Executes the <paramref name="function"/> that performs the logic on the specified <typeparamref name="TService"/>.
        /// </summary>
        /// <typeparam name="TService">The configured service <see cref="Type"/> to instantiate.</typeparam>
        /// <param name="function">The function performing the logic.</param>
        /// <param name="serviceKey">The optional keyed service key.</param>
        /// <returns>The resulting <see cref="VoidAssertor"/>.</returns>
#if NET9_0_OR_GREATER
        [OverloadResolutionPriority(1)]
#endif
        public async Task<VoidAssertor> RunAsync<TService>(Func<TService, Task> function, object? serviceKey = null) where TService : class
        {
            var service = serviceKey is null ? Services.GetRequiredService<TService>() : Services.GetRequiredKeyedService<TService>(serviceKey);
            return await RunAsync(() => function(service)).ConfigureAwait(false);
        }

#if NET9_0_OR_GREATER
        /// <summary>
        /// Executes the <paramref name="function"/> that performs the logic on the specified <typeparamref name="TService"/>.
        /// </summary>
        /// <typeparam name="TService">The configured service <see cref="Type"/> to instantiate.</typeparam>
        /// <param name="function">The function performing the logic.</param>
        /// <param name="serviceKey">The optional keyed service key.</param>
        /// <returns>The resulting <see cref="VoidAssertor"/>.</returns>
        [OverloadResolutionPriority(2)]
        public async Task<VoidAssertor> RunAsync<TService>(Func<TService, ValueTask> function, object? serviceKey = null) where TService : class
        {
            var service = serviceKey is null ? Services.GetRequiredService<TService>() : Services.GetRequiredKeyedService<TService>(serviceKey);
            return await RunAsync(() => function(service).AsTask()).ConfigureAwait(false);
        }

#endif
        /// <summary>
        /// Executes the <paramref name="function"/> that performs the logic on the specified <typeparamref name="TService"/>.
        /// </summary>
        /// <typeparam name="TService">The configured service <see cref="Type"/> to instantiate.</typeparam>
        /// <param name="function">The function performing the logic.</param>
        /// <param name="serviceFactory">The factory to create the <typeparamref name="TService"/> instance.</param>
        /// <returns>The resulting <see cref="VoidAssertor"/>.</returns>
#if NET9_0_OR_GREATER
        [OverloadResolutionPriority(1)]
#endif
        public async Task<VoidAssertor> RunAsync<TService>(Func<TService, Task> function, Func<IServiceProvider, TService> serviceFactory) where TService : class
        {
            var service = serviceFactory(Services);
            return await RunAsync(() => function(service)).ConfigureAwait(false);
        }

#if NET9_0_OR_GREATER
        /// <summary>
        /// Executes the <paramref name="function"/> that performs the logic on the specified <typeparamref name="TService"/>.
        /// </summary>
        /// <typeparam name="TService">The configured service <see cref="Type"/> to instantiate.</typeparam>
        /// <param name="function">The function performing the logic.</param>
        /// <param name="serviceFactory">The factory to create the <typeparamref name="TService"/> instance.</param>
        /// <returns>The resulting <see cref="VoidAssertor"/>.</returns>
        [OverloadResolutionPriority(2)]
        public async Task<VoidAssertor> RunAsync<TService>(Func<TService, ValueTask> function, Func<IServiceProvider, TService> serviceFactory) where TService : class
        {
            var service = serviceFactory(Services);
            return await RunAsync(() => function(service).AsTask()).ConfigureAwait(false);
        }

#endif

        /// <summary>
        /// Executes the <paramref name="function"/> that performs the logic.
        /// </summary>
        /// <param name="function">The function performing the logic.</param>
        /// <returns>The resulting <see cref="VoidAssertor"/>.</returns>
#if NET9_0_OR_GREATER
        [OverloadResolutionPriority(1)]
#endif
        public async Task<VoidAssertor> RunAsync(Func<Task> function)
        {
            ArgumentNullException.ThrowIfNull(function);

            TestSetUp.LogAutoSetUpOutputs(Implementor);

            Implementor.WriteLine("");
            Implementor.WriteLine("GENERIC TESTER...");
            Implementor.WriteLine("");

            Exception? exception = null;
            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                await function().ConfigureAwait(false);
            }
            catch (AggregateException aex)
            {
                exception = aex.InnerException ?? aex;
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            sw.Stop();

            Implementor.WriteLine("LOGGING >");
            var messages = SharedState.GetLoggerMessages();
            if (messages.Any())
            {
                foreach (var msg in messages)
                {
                    Implementor.WriteLine(msg);
                }
            }
            else
                Implementor.WriteLine("None.");

            Implementor.WriteLine("");
            Implementor.WriteLine("RESULT >");
            Implementor.WriteLine($"Elapsed (ms): {sw.Elapsed.TotalMilliseconds}");
            if (exception != null)
            {
                Implementor.WriteLine($"Exception: {exception.Message} [{exception.GetType().Name}]");
                Implementor.WriteLine(exception.ToString());
            }
            else
                Implementor.WriteLine($"Result: Success");

            await ExpectationsArranger.AssertAsync(messages, exception).ConfigureAwait(false);

            return new VoidAssertor(this, exception);
        }

#if NET9_0_OR_GREATER
        /// <summary>
        /// Executes the <paramref name="function"/> that performs the logic.
        /// </summary>
        /// <param name="function">The function performing the logic.</param>
        /// <returns>The resulting <see cref="VoidAssertor"/>.</returns>
        [OverloadResolutionPriority(2)]
        public async Task<VoidAssertor> RunAsync(Func<ValueTask> function)
            => await RunAsync(() => function().AsTask()).ConfigureAwait(false);

#endif
        /// <summary>
        /// Executes the <paramref name="function"/> that performs the logic.
        /// </summary>
        /// <typeparam name="TValue">The result value <see cref="Type"/>.</typeparam>
        /// <param name="function">The function performing the logic.</param>
        /// <returns>The resulting <see cref="ValueAssertor{TValue}"/>.</returns>
        public ValueAssertor<TValue> Run<TValue>(Func<TValue> function) => RunAsync(() =>
        {
            TValue value = (function ?? throw new ArgumentNullException(nameof(function))).Invoke();
            return Task.FromResult(value);
        }).GetAwaiter().GetResult();

        /// <summary>
        /// Executes the <paramref name="function"/> that performs the logic.
        /// </summary>
        /// <typeparam name="TService">The configured service <see cref="Type"/> to instantiate.</typeparam>
        /// <typeparam name="TValue">The result value <see cref="Type"/>.</typeparam>
        /// <param name="function">The function performing the logic.</param>
        /// <param name="serviceKey">The optional keyed service key.</param>
        /// <returns>The resulting <see cref="ValueAssertor{TValue}"/>.</returns>
        public ValueAssertor<TValue> Run<TService, TValue>(Func<TService, TValue> function, object? serviceKey = null) where TService : class => RunAsync(() =>
        {
            var service = serviceKey is null ? Services.GetRequiredService<TService>() : Services.GetRequiredKeyedService<TService>(serviceKey);
            TValue value = (function ?? throw new ArgumentNullException(nameof(function))).Invoke(service);
            return Task.FromResult(value);
        }).GetAwaiter().GetResult();

        /// <summary>
        /// Executes the <paramref name="function"/> that performs the logic.
        /// </summary>
        /// <typeparam name="TService">The configured service <see cref="Type"/> to instantiate.</typeparam>
        /// <typeparam name="TValue">The result value <see cref="Type"/>.</typeparam>
        /// <param name="function">The function performing the logic.</param>
        /// <param name="serviceFactory">The factory to create the <typeparamref name="TService"/> instance.</param>
        /// <returns>The resulting <see cref="ValueAssertor{TValue}"/>.</returns>
        public ValueAssertor<TValue> Run<TService, TValue>(Func<TService, TValue> function, Func<IServiceProvider, TService> serviceFactory) where TService : class => RunAsync(() =>
        {
            var service = serviceFactory(Services);
            TValue value = (function ?? throw new ArgumentNullException(nameof(function))).Invoke(service);
            return Task.FromResult(value);
        }).GetAwaiter().GetResult();

        /// <summary>
        /// Executes the <paramref name="function"/> that performs the logic.
        /// </summary>
        /// <typeparam name="TValue">The result value <see cref="Type"/>.</typeparam>
        /// <param name="function">The function performing the logic.</param>
        /// <returns>The resulting <see cref="ValueAssertor{TValue}"/>.</returns>
#if NET9_0_OR_GREATER
        [OverloadResolutionPriority(1)]
#endif
        public ValueAssertor<TValue> Run<TValue>(Func<Task<TValue>> function) => RunAsync(function).GetAwaiter().GetResult();

#if NET9_0_OR_GREATER
        /// <summary>
        /// Executes the <paramref name="function"/> that performs the logic.
        /// </summary>
        /// <typeparam name="TValue">The result value <see cref="Type"/>.</typeparam>
        /// <param name="function">The function performing the logic.</param>
        /// <returns>The resulting <see cref="ValueAssertor{TValue}"/>.</returns>
        [OverloadResolutionPriority(2)]
        public ValueAssertor<TValue> Run<TValue>(Func<ValueTask<TValue>> function) => RunAsync(() => function().AsTask()).GetAwaiter().GetResult();

#endif
        /// <summary>
        /// Executes the <paramref name="function"/> that performs the logic on the specified <typeparamref name="TService"/>.
        /// </summary>
        /// <typeparam name="TService">The configured service <see cref="Type"/> to instantiate.</typeparam>
        /// <typeparam name="TValue">The result value <see cref="Type"/>.</typeparam>
        /// <param name="function">The function performing the logic.</param>
        /// <param name="serviceKey">The optional keyed service key.</param>
        /// <returns>The resulting <see cref="VoidAssertor"/>.</returns>
#if NET9_0_OR_GREATER
        [OverloadResolutionPriority(1)]
#endif        
        public ValueAssertor<TValue> Run<TService, TValue>(Func<TService, Task<TValue>> function, object? serviceKey = null) where TService : class
        {
            var service = serviceKey is null ? Services.GetRequiredService<TService>() : Services.GetRequiredKeyedService<TService>(serviceKey);
            return RunAsync(() => function(service)).GetAwaiter().GetResult();
        }

#if NET9_0_OR_GREATER

        /// <summary>
        /// Executes the <paramref name="function"/> that performs the logic on the specified <typeparamref name="TService"/>.
        /// </summary>
        /// <typeparam name="TService">The configured service <see cref="Type"/> to instantiate.</typeparam>
        /// <typeparam name="TValue">The result value <see cref="Type"/>.</typeparam>
        /// <param name="function">The function performing the logic.</param>
        /// <param name="serviceKey">The optional keyed service key.</param>
        /// <returns>The resulting <see cref="VoidAssertor"/>.</returns>
        [OverloadResolutionPriority(2)]
        public ValueAssertor<TValue> Run<TService, TValue>(Func<TService, ValueTask<TValue>> function, object? serviceKey = null) where TService : class
        {
            var service = serviceKey is null ? Services.GetRequiredService<TService>() : Services.GetRequiredKeyedService<TService>(serviceKey);
            return RunAsync(() => function(service).AsTask()).GetAwaiter().GetResult();
        }

#endif
        /// <summary>
        /// Executes the <paramref name="function"/> that performs the logic on the specified <typeparamref name="TService"/>.
        /// </summary>
        /// <typeparam name="TService">The configured service <see cref="Type"/> to instantiate.</typeparam>
        /// <typeparam name="TValue">The result value <see cref="Type"/>.</typeparam>
        /// <param name="function">The function performing the logic.</param>
        /// <param name="serviceFactory">The factory to create the <typeparamref name="TService"/> instance.</param>
        /// <returns>The resulting <see cref="VoidAssertor"/>.</returns>
#if NET9_0_OR_GREATER
        [OverloadResolutionPriority(1)]
#endif        
        public ValueAssertor<TValue> Run<TService, TValue>(Func<TService, Task<TValue>> function, Func<IServiceProvider, TService> serviceFactory) where TService : class
        {
            var service = serviceFactory(Services);
            return RunAsync(() => function(service)).GetAwaiter().GetResult();
        }

#if NET9_0_OR_GREATER

        /// <summary>
        /// Executes the <paramref name="function"/> that performs the logic on the specified <typeparamref name="TService"/>.
        /// </summary>
        /// <typeparam name="TService">The configured service <see cref="Type"/> to instantiate.</typeparam>
        /// <typeparam name="TValue">The result value <see cref="Type"/>.</typeparam>
        /// <param name="function">The function performing the logic.</param>
        /// <param name="serviceFactory">The factory to create the <typeparamref name="TService"/> instance.</param>
        /// <returns>The resulting <see cref="VoidAssertor"/>.</returns>
        [OverloadResolutionPriority(2)]
        public ValueAssertor<TValue> Run<TService, TValue>(Func<TService, ValueTask<TValue>> function, Func<IServiceProvider, TService> serviceFactory) where TService : class
        {
            var service = serviceFactory(Services);
            return RunAsync(() => function(service).AsTask()).GetAwaiter().GetResult();
        }

#endif

        /// <summary>
        /// Executes the <paramref name="function"/> that performs the logic on the specified <typeparamref name="TService"/>.
        /// </summary>
        /// <typeparam name="TService">The configured service <see cref="Type"/> to instantiate.</typeparam>
        /// <typeparam name="TValue">The result value <see cref="Type"/>.</typeparam>
        /// <param name="function">The function performing the logic.</param>
        /// <param name="serviceKey">The optional keyed service key.</param>
        /// <returns>The resulting <see cref="VoidAssertor"/>.</returns>
#if NET9_0_OR_GREATER
        [OverloadResolutionPriority(1)]
#endif     
        public async Task<ValueAssertor<TValue>> RunAsync<TService, TValue>(Func<TService, Task<TValue>> function, object? serviceKey = null) where TService : class
        {
            var service = serviceKey is null ? Services.GetRequiredService<TService>() : Services.GetRequiredKeyedService<TService>(serviceKey);
            return await RunAsync(() => function(service)).ConfigureAwait(false);
        }

#if NET9_0_OR_GREATER
        /// <summary>
        /// Executes the <paramref name="function"/> that performs the logic on the specified <typeparamref name="TService"/>.
        /// </summary>
        /// <typeparam name="TService">The configured service <see cref="Type"/> to instantiate.</typeparam>
        /// <typeparam name="TValue">The result value <see cref="Type"/>.</typeparam>
        /// <param name="function">The function performing the logic.</param>
        /// <param name="serviceKey">The optional keyed service key.</param>
        /// <returns>The resulting <see cref="VoidAssertor"/>.</returns>
        [OverloadResolutionPriority(2)]
        public async Task<ValueAssertor<TValue>> RunAsync<TService, TValue>(Func<TService, ValueTask<TValue>> function, object? serviceKey = null) where TService : class
        {
            var service = serviceKey is null ? Services.GetRequiredService<TService>() : Services.GetRequiredKeyedService<TService>(serviceKey);
            return await RunAsync(() => function(service).AsTask()).ConfigureAwait(false);
        }

#endif

        /// <summary>
        /// Executes the <paramref name="function"/> that performs the logic on the specified <typeparamref name="TService"/>.
        /// </summary>
        /// <typeparam name="TService">The configured service <see cref="Type"/> to instantiate.</typeparam>
        /// <typeparam name="TValue">The result value <see cref="Type"/>.</typeparam>
        /// <param name="function">The function performing the logic.</param>
        /// <param name="serviceFactory">The factory to create the <typeparamref name="TService"/> instance.</param>
        /// <returns>The resulting <see cref="VoidAssertor"/>.</returns>
#if NET9_0_OR_GREATER
        [OverloadResolutionPriority(1)]
#endif     
        public async Task<ValueAssertor<TValue>> RunAsync<TService, TValue>(Func<TService, Task<TValue>> function, Func<IServiceProvider, TService> serviceFactory) where TService : class
        {
            var service = serviceFactory(Services);
            return await RunAsync(() => function(service)).ConfigureAwait(false);
        }

#if NET9_0_OR_GREATER
        /// <summary>
        /// Executes the <paramref name="function"/> that performs the logic on the specified <typeparamref name="TService"/>.
        /// </summary>
        /// <typeparam name="TService">The configured service <see cref="Type"/> to instantiate.</typeparam>
        /// <typeparam name="TValue">The result value <see cref="Type"/>.</typeparam>
        /// <param name="function">The function performing the logic.</param>
        /// <param name="serviceFactory">The factory to create the <typeparamref name="TService"/> instance.</param>
        /// <returns>The resulting <see cref="VoidAssertor"/>.</returns>
        [OverloadResolutionPriority(2)]
        public async Task<ValueAssertor<TValue>> RunAsync<TService, TValue>(Func<TService, ValueTask<TValue>> function, Func<IServiceProvider, TService> serviceFactory) where TService : class
        {
            var service = serviceFactory(Services);
            return await RunAsync(() => function(service).AsTask()).ConfigureAwait(false);
        }

#endif

        /// <summary>
        /// Executes the <paramref name="function"/> that performs the logic.
        /// </summary>
        /// <typeparam name="TValue">The result value <see cref="Type"/>.</typeparam>
        /// <param name="function">The function performing the logic.</param>
        /// <returns>The resulting <see cref="ValueAssertor{TValue}"/>.</returns>
#if NET9_0_OR_GREATER
        [OverloadResolutionPriority(1)]
#endif     
        public async Task<ValueAssertor<TValue>> RunAsync<TValue>(Func<Task<TValue>> function)
        {
            ArgumentNullException.ThrowIfNull(function);

            TestSetUp.LogAutoSetUpOutputs(Implementor);

            Implementor.WriteLine("");
            Implementor.WriteLine("GENERIC TESTER...");
            Implementor.WriteLine("");

            Exception? exception = null;

            var sw = System.Diagnostics.Stopwatch.StartNew();
            TValue value = default!;

            try
            {
                value = await function().ConfigureAwait(false);
            }
            catch (AggregateException aex)
            {
                exception = aex.InnerException ?? aex;
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            sw.Stop();

            Implementor.WriteLine("LOGGING >");
            var messages = SharedState.GetLoggerMessages();
            if (messages.Any())
            {
                foreach (var msg in messages)
                {
                    Implementor.WriteLine(msg);
                }
            }
            else
                Implementor.WriteLine("None.");

            Implementor.WriteLine("");
            Implementor.WriteLine("RESULT >");
            Implementor.WriteLine($"Elapsed (ms): {sw.Elapsed.TotalMilliseconds}");
            if (exception != null)
            {
                Implementor.WriteLine($"Exception: {exception.Message} [{exception.GetType().Name}]");
                Implementor.WriteLine(exception.ToString());
            }
            else
            {
                Implementor.WriteLine($"Result: Success");
                try
                {
                    Implementor.WriteLine($"Value: {JsonSerializer.Serialize(value, JsonWriteFormat.Indented)}");
                }
                catch (Exception ex)
                {
                    Implementor.WriteLine($"Value serialization error: {ex.Message}");
                }
            }

            await ExpectationsArranger.AssertAsync(messages, exception).ConfigureAwait(false);

            return new ValueAssertor<TValue>(this, value, exception);
        }

#if NET9_0_OR_GREATER
        /// <summary>
        /// Executes the <paramref name="function"/> that performs the logic.
        /// </summary>
        /// <typeparam name="TValue">The result value <see cref="Type"/>.</typeparam>
        /// <param name="function">The function performing the logic.</param>
        /// <returns>The resulting <see cref="ValueAssertor{TValue}"/>.</returns>
        [OverloadResolutionPriority(2)]
        public async Task<ValueAssertor<TValue>> RunAsync<TValue>(Func<ValueTask<TValue>> function)
            => await RunAsync(() => function().AsTask()).ConfigureAwait(false);
#endif
    }
}