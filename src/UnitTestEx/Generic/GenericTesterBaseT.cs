// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading.Tasks;
using UnitTestEx.Abstractions;
using UnitTestEx.Assertors;
using UnitTestEx.Expectations;
using UnitTestEx.Hosting;
using UnitTestEx.Json;

namespace UnitTestEx.Generic
{
    /// <summary>
    /// Provides generic testing capabilities.
    /// </summary>
    /// <typeparam name="TEntryPoint">The <see cref="EntryPoint"/> <see cref="Type"/>.</typeparam>
    /// <typeparam name="TValue">The result value <see cref="Type"/>.</typeparam>
    /// <typeparam name="TSelf">The <see cref="GenericTesterBase{TEntryPoint, TSelf}"/> to support inheriting fluent-style method-chaining.</typeparam>
    /// <param name="implementor">The <see cref="TestFrameworkImplementor"/>.</param>
    public abstract class GenericTesterBase<TEntryPoint, TValue, TSelf>(TestFrameworkImplementor implementor) : GenericTesterCore<TEntryPoint, TSelf>(implementor), IValueExpectations<TValue, TSelf> where TEntryPoint : class where TSelf : GenericTesterBase<TEntryPoint, TValue, TSelf>
    {
        /// <summary>
        /// Executes the <paramref name="function"/> that performs the logic.
        /// </summary>
        /// <param name="function">The function performing the logic.</param>
        /// <returns>The resulting <see cref="ValueAssertor{TValue}"/>.</returns>
        public ValueAssertor<TValue> Run(Func<TValue> function) => RunAsync(() =>
        {
            TValue value = (function ?? throw new ArgumentNullException(nameof(function))).Invoke();
            return Task.FromResult(value);
        }).GetAwaiter().GetResult();

        /// <summary>
        /// Executes the <paramref name="function"/> that performs the logic.
        /// </summary>
        /// <typeparam name="TService">The configured service <see cref="Type"/> to instantiate.</typeparam>
        /// <param name="function">The function performing the logic.</param>
        /// <returns>The resulting <see cref="ValueAssertor{TValue}"/>.</returns>
        public ValueAssertor<TValue> Run<TService>(Func<TService, TValue> function) where TService : class => RunAsync(() =>
        {
            var service = Services.GetRequiredService<TService>();
            TValue value = (function ?? throw new ArgumentNullException(nameof(function))).Invoke(service);
            return Task.FromResult(value);
        }).GetAwaiter().GetResult();

        /// <summary>
        /// Executes the <paramref name="function"/> that performs the logic.
        /// </summary>
        /// <param name="function">The function performing the logic.</param>
        /// <returns>The resulting <see cref="ValueAssertor{TValue}"/>.</returns>
        public ValueAssertor<TValue> Run(Func<Task<TValue>> function) => RunAsync(function).GetAwaiter().GetResult();

        /// <summary>
        /// Executes the <paramref name="function"/> that performs the logic on the specified <typeparamref name="TService"/>.
        /// </summary>
        /// <typeparam name="TService">The configured service <see cref="Type"/> to instantiate.</typeparam>
        /// <param name="function">The function performing the logic.</param>
        /// <returns>The resulting <see cref="VoidAssertor"/>.</returns>
        public ValueAssertor<TValue> Run<TService>(Func<TService, Task<TValue>> function) where TService : class
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
        public Task<ValueAssertor<TValue>> RunAsync<TService>(Func<TService, Task<TValue>> function) where TService : class
        {
            var service = Services.GetRequiredService<TService>();
            return RunAsync(() => function(service));
        }

        /// <summary>
        /// Executes the <paramref name="function"/> that performs the logic.
        /// </summary>
        /// <param name="function">The function performing the logic.</param>
        /// <returns>The resulting <see cref="ValueAssertor{TValue}"/>.</returns>
        public async Task<ValueAssertor<TValue>> RunAsync(Func<Task<TValue>> function)
        {
            ArgumentNullException.ThrowIfNull(function, nameof(function));

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
    }
}