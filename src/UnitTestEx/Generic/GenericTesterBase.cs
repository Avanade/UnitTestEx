// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using CoreEx;
using CoreEx.Hosting;
using Microsoft.Azure.Amqp.Framing;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading.Tasks;
using UnitTestEx.Abstractions;
using UnitTestEx.Assertors;

namespace UnitTestEx.Generic
{
    /// <summary>
    /// Provides generic testing capabilities.
    /// </summary>
    /// <typeparam name="TEntryPoint">The <see cref="IHostStartup"/> <see cref="Type"/>.</typeparam>
    /// <typeparam name="TSelf">The <see cref="GenericTesterBase{TEntryPoint, TSelf}"/> to support inheriting fluent-style method-chaining.</typeparam>
    public abstract class GenericTesterBase<TEntryPoint, TSelf> : GenericTesterCore<TEntryPoint, GenericTesterBase<TEntryPoint, TSelf>> where TEntryPoint : IHostStartup, new() where TSelf : GenericTesterBase<TEntryPoint, TSelf>
    {
        private OperationType _operationType = CoreEx.OperationType.Unspecified;

        /// <summary>
        /// Initializes a new instance of the <see cref="GenericTesterBase{TEntryPoint, TSelf}"/> class.
        /// </summary>
        /// <param name="implementor">The <see cref="TestFrameworkImplementor"/>.</param>
        protected GenericTesterBase(TestFrameworkImplementor implementor) : base(implementor) { }

        /// <summary>
        /// Sets the <see cref="ExecutionContext.OperationType"/> to the specified <paramref name="operationType"/>.
        /// </summary>
        /// <param name="operationType">The <see cref="OperationType"/>.</param>
        /// <returns>The <see cref="GenericTesterBase{TEntryPoint, TSelf}"/> instance to support fluent/chaining usage.</returns>
        public TSelf OperationType(OperationType operationType)
        {
            _operationType = operationType;
            return (TSelf)this;
        }

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
        /// <returns>The resulting <see cref="VoidAssertor"/>.</returns>
        public VoidAssertor Run<TService>(Action<TService> action) where TService : class => RunAsync(() =>
        {
            var service = Services.GetRequiredService<TService>();
            (action ?? throw new ArgumentNullException(nameof(action))).Invoke(service);
            return Task.CompletedTask;
        }).GetAwaiter().GetResult();

        /// <summary>
        /// Executes the <paramref name="function"/> that performs the logic.
        /// </summary>
        /// <param name="function">The function performing the logic.</param>
        /// <returns>The resulting <see cref="VoidAssertor"/>.</returns>
        public VoidAssertor Run(Func<Task> function) => RunAsync(function).GetAwaiter().GetResult();

        /// <summary>
        /// Executes the <paramref name="function"/> that performs the logic on the specified <typeparamref name="TService"/>.
        /// </summary>
        /// <typeparam name="TService">The configured service <see cref="Type"/> to instantiate.</typeparam>
        /// <param name="function">The function performing the logic.</param>
        /// <returns>The resulting <see cref="VoidAssertor"/>.</returns>
        public VoidAssertor Run<TService>(Func<TService, Task> function) where TService : class
        {
            var service = Services.GetRequiredService<TService>();
            return RunAsync(() => function(service)).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Executes the <paramref name="function"/> that performs the logic on the specified <typeparamref name="TService"/>.
        /// </summary>
        /// <typeparam name="TService">The configured service <see cref="Type"/> to instantiate.</typeparam>
        /// <param name="function">The function performing the logic.</param>
        /// <returns>The resulting <see cref="VoidAssertor"/>.</returns>
        public Task<VoidAssertor> RunAsync<TService>(Func<TService, Task> function) where TService : class
        {
            var service = Services.GetRequiredService<TService>();
            return RunAsync(() => function(service));
        }

        /// <summary>
        /// Executes the <paramref name="function"/> that performs the logic.
        /// </summary>
        /// <param name="function">The function performing the logic.</param>
        /// <returns>The resulting <see cref="VoidAssertor"/>.</returns>
        public async Task<VoidAssertor> RunAsync(Func<Task> function)
        {
            if (function == null)
                throw new ArgumentNullException(nameof(function));

            TestSetUp.LogAutoSetUpOutputs(Implementor);

            Implementor.WriteLine("");
            Implementor.WriteLine("GENERIC TESTER...");
            Implementor.WriteLine("");

            var ec = Services.GetRequiredService<ExecutionContext>();
            ec.OperationType = _operationType;

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

            Implementor.WriteLine("");
            Implementor.WriteLine(new string('=', 80));
            Implementor.WriteLine("");

            ExceptionSuccessExpectations.Assert(exception);
            EventExpectations.Assert();
            LoggerExpectations.Assert(messages);
            return new VoidAssertor(exception, Implementor, JsonSerializer);
        }

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
        /// <returns>The resulting <see cref="ValueAssertor{TValue}"/>.</returns>
        public ValueAssertor<TValue> Run<TService, TValue>(Func<TService, TValue> function) where TService : class => RunAsync(() =>
        {
            var service = Services.GetRequiredService<TService>();
            TValue value = (function ?? throw new ArgumentNullException(nameof(function))).Invoke(service);
            return Task.FromResult(value);
        }).GetAwaiter().GetResult();

        /// <summary>
        /// Executes the <paramref name="function"/> that performs the logic.
        /// </summary>
        /// <typeparam name="TValue">The result value <see cref="Type"/>.</typeparam>
        /// <param name="function">The function performing the logic.</param>
        /// <returns>The resulting <see cref="ValueAssertor{TValue}"/>.</returns>
        public ValueAssertor<TValue> Run<TValue>(Func<Task<TValue>> function) => RunAsync(function).GetAwaiter().GetResult();

        /// <summary>
        /// Executes the <paramref name="function"/> that performs the logic on the specified <typeparamref name="TService"/>.
        /// </summary>
        /// <typeparam name="TService">The configured service <see cref="Type"/> to instantiate.</typeparam>
        /// <typeparam name="TValue">The result value <see cref="Type"/>.</typeparam>
        /// <param name="function">The function performing the logic.</param>
        /// <returns>The resulting <see cref="VoidAssertor"/>.</returns>
        public ValueAssertor<TValue> Run<TService, TValue>(Func<TService, Task<TValue>> function) where TService : class
        {
            var service = Services.GetRequiredService<TService>();
            return RunAsync(() => function(service)).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Executes the <paramref name="function"/> that performs the logic on the specified <typeparamref name="TService"/>.
        /// </summary>
        /// <typeparam name="TService">The configured service <see cref="Type"/> to instantiate.</typeparam>
        /// <typeparam name="TValue">The result value <see cref="Type"/>.</typeparam>
        /// <param name="function">The function performing the logic.</param>
        /// <returns>The resulting <see cref="VoidAssertor"/>.</returns>
        public Task<ValueAssertor<TValue>> RunAsync<TService, TValue>(Func<TService, Task<TValue>> function) where TService : class
        {
            var service = Services.GetRequiredService<TService>();
            return RunAsync(() => function(service));
        }

        /// <summary>
        /// Executes the <paramref name="function"/> that performs the logic.
        /// </summary>
        /// <typeparam name="TValue">The result value <see cref="Type"/>.</typeparam>
        /// <param name="function">The function performing the logic.</param>
        /// <returns>The resulting <see cref="ValueAssertor{TValue}"/>.</returns>
        public async Task<ValueAssertor<TValue>> RunAsync<TValue>(Func<Task<TValue>> function)
        {
            if (function == null)
                throw new ArgumentNullException(nameof(function));

            TestSetUp.LogAutoSetUpOutputs(Implementor);

            Implementor.WriteLine("");
            Implementor.WriteLine("GENERIC TESTER...");
            Implementor.WriteLine("");

            var ec = Services.GetRequiredService<ExecutionContext>();
            ec.OperationType = _operationType;

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
                    Implementor.WriteLine($"Value: {JsonSerializer.Serialize(value, CoreEx.Json.JsonWriteFormat.Indented)}");
                }
                catch (Exception ex)
                {
                    Implementor.WriteLine($"Value serialization error: {ex.Message}");
                }
            }

            Implementor.WriteLine("");
            Implementor.WriteLine(new string('=', 80));
            Implementor.WriteLine("");

            ExceptionSuccessExpectations.Assert(exception);
            EventExpectations.Assert();
            LoggerExpectations.Assert(messages);
            return new ValueAssertor<TValue>(value, exception, Implementor, JsonSerializer);
        }
    }
}