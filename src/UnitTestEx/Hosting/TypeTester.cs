// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using UnitTestEx.Abstractions;
using UnitTestEx.Assertors;
using UnitTestEx.Expectations;
using UnitTestEx.Json;

namespace UnitTestEx.Hosting
{
    /// <summary>
    /// Provides the generic <see cref="Type"/> unit-testing capabilities.
    /// </summary>
    /// <typeparam name="T">The <see cref="Type"/> (must be a <c>class</c>).</typeparam>
    public class TypeTester<T> : HostTesterBase<T>, IExpectations<TypeTester<T>> where T : class
    {
        /// <summary>
        /// Initializes a new <see cref="TypeTester{TFunction}"/> class.
        /// </summary>
        /// <param name="owner">The owning <see cref="TesterBase"/>.</param>
        /// <param name="serviceScope">The <see cref="IServiceScope"/>.</param>
        public TypeTester(TesterBase owner, IServiceScope serviceScope) : base(owner, serviceScope) => ExpectationsArranger = new ExpectationsArranger<TypeTester<T>>(owner, this);

        /// <summary>
        /// Gets the <see cref="ExpectationsArranger{TSelf}"/>.
        /// </summary>
        public ExpectationsArranger<TypeTester<T>> ExpectationsArranger { get; }

        /// <summary>
        /// Runs the synchronous method with no result.
        /// </summary>
        /// <param name="function">The function execution.</param>
        /// <returns>A <see cref="VoidAssertor"/>.</returns>
        public VoidAssertor Run(Action<T> function) => RunAsync(x => { function(x); return Task.CompletedTask; }).GetAwaiter().GetResult();

        /// <summary>
        /// Runs the synchronous method with a result.
        /// </summary>
        /// <typeparam name="TValue">The result value <see cref="Type"/>.</typeparam>
        /// <param name="function">The function execution.</param>
        /// <returns>A <see cref="ValueAssertor{TValue}"/>.</returns>
        public ValueAssertor<TValue> Run<TValue>(Func<T, TValue> function) => RunAsync(x => Task.FromResult(function(x))).GetAwaiter().GetResult();

        /// <summary>
        /// Runs the asynchronous method with no result.
        /// </summary>
        /// <param name="function">The function execution.</param>
        /// <returns>A <see cref="VoidAssertor"/>.</returns>
        public VoidAssertor Run(Func<T, Task> function) => RunAsync(function).GetAwaiter().GetResult();

        /// <summary>
        /// Runs the asynchronous method with no result.
        /// </summary>
        /// <param name="function">The function execution.</param>
        /// <returns>A <see cref="VoidAssertor"/>.</returns>
        public async Task<VoidAssertor> RunAsync(Func<T, Task> function)
        {
            Exception? ex = null;
            var sw = Stopwatch.StartNew();
            try
            {
                LogHeader();
                var f = ServiceScope.ServiceProvider.CreateInstance<T>();
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
                sw.Stop();
            }

            await Task.Delay(TestSetUp.TaskDelayMilliseconds).ConfigureAwait(false);
            var logs = Owner.SharedState.GetLoggerMessages();
            LogResult(ex, sw.Elapsed.TotalMilliseconds, logs);
            LogTrailer();

            await ExpectationsArranger.AssertAsync(logs, ex).ConfigureAwait(false);

            return new VoidAssertor(Owner, ex);
        }

        /// <summary>
        /// Runs the asynchronous method with a result.
        /// </summary>
        /// <typeparam name="TValue">The result value <see cref="Type"/>.</typeparam>
        /// <param name="function">The function execution.</param>
        /// <returns>A <see cref="ValueAssertor{TValue}"/>.</returns>
        public ValueAssertor<TValue> Run<TValue>(Func<T, Task<TValue>> function) => RunAsync(function).GetAwaiter().GetResult();

        /// <summary>
        /// Runs the asynchronous method with a result.
        /// </summary>
        /// <typeparam name="TValue">The result value <see cref="Type"/>.</typeparam>
        /// <param name="function">The function execution.</param>
        /// <returns>A <see cref="ValueAssertor{TValue}"/>.</returns>
        public async Task<ValueAssertor<TValue>> RunAsync<TValue>(Func<T, Task<TValue>> function)
        {
            TValue result = default!;
            Exception? ex = null;
            var sw = Stopwatch.StartNew();
            try
            {
                LogHeader();
                var f = ServiceScope.ServiceProvider.CreateInstance<T>();
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

            LogTrailer();

            await ExpectationsArranger.AssertValueAsync(logs, result, ex).ConfigureAwait(false);

            return new ValueAssertor<TValue>(Owner, result, ex);
        }

        /// <summary>
        /// Logs the header.
        /// </summary>
        private void LogHeader()
        {
            Implementor.WriteLine("");
            Implementor.WriteLine("TYPE TESTER...");
            Implementor.WriteLine($"Type: {typeof(T).Name} [{typeof(T).FullName}]");
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
        /// Log the trailer.
        /// </summary>
        private void LogTrailer()
        {
            Implementor.WriteLine("");
            Implementor.WriteLine(new string('=', 80));
            Implementor.WriteLine("");
        }
    }
}