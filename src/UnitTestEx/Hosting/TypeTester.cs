// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using CoreEx.Json;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;
using UnitTestEx.Abstractions;
using UnitTestEx.Assertors;

namespace UnitTestEx.Hosting
{
    /// <summary>
    /// Provides the generic <see cref="Type"/> unit-testing capabilities.
    /// </summary>
    /// <typeparam name="T">The <see cref="Type"/> (must be a <c>class</c>).</typeparam>
    public class TypeTester<T> : HostTesterBase<T> where T : class
    {
        /// <summary>
        /// Initializes a new <see cref="TypeTester{TFunction}"/> class.
        /// </summary>
        /// <param name="serviceScope">The <see cref="IServiceScope"/>.</param>
        /// <param name="implementor">The <see cref="TestFrameworkImplementor"/>.</param>
        /// <param name="jsonSerializer">The <see cref="IJsonSerializer"/>.</param>
        internal TypeTester(IServiceScope serviceScope, TestFrameworkImplementor implementor, IJsonSerializer jsonSerializer) : base(serviceScope, implementor, jsonSerializer) { }

        /// <summary>
        /// Runs the synchronous method with no result.
        /// </summary>
        /// <param name="function">The function execution.</param>
        /// <returns>A <see cref="VoidAssertor"/>.</returns>
        public VoidAssertor Run(Action<T> function) => RunAsync(x => { function(x); return Task.CompletedTask; }).GetAwaiter().GetResult();

        /// <summary>
        ///  Runs the synchronous method with a result.
        /// </summary>
        /// <typeparam name="TResult">The result <see cref="Type"/>.</typeparam>
        /// <param name="function">The function execution.</param>
        /// <returns>A <see cref="ResultAssertor{TResult}"/>.</returns>
        public ResultAssertor<TResult> Run<TResult>(Func<T, TResult> function) => RunAsync(x => Task.FromResult(function(x))).GetAwaiter().GetResult();

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
            catch (Exception uex)
            {
                ex = uex;
            }
            finally
            {
                sw.Stop();
            }

            await Task.Delay(0).ConfigureAwait(false);
            LogResult(ex, sw.ElapsedMilliseconds);
            LogTrailer();
            return new VoidAssertor(ex, Implementor, JsonSerializer);
        }

        /// <summary>
        /// Runs the asynchronous method with a result.
        /// </summary>
        /// <typeparam name="TResult">The result <see cref="Type"/>.</typeparam>
        /// <param name="function">The function execution.</param>
        /// <returns>A <see cref="ResultAssertor{TResult}"/>.</returns>
        public ResultAssertor<TResult> Run<TResult>(Func<T, Task<TResult>> function) => RunAsync(function).GetAwaiter().GetResult();

        /// <summary>
        /// Runs the asynchronous method with a result.
        /// </summary>
        /// <typeparam name="TResult">The result <see cref="Type"/>.</typeparam>
        /// <param name="function">The function execution.</param>
        /// <returns>A <see cref="ResultAssertor{TResult}"/>.</returns>
        public async Task<ResultAssertor<TResult>> RunAsync<TResult>(Func<T, Task<TResult>> function)
        {
            TResult result = default!;
            Exception? ex = null;
            var sw = Stopwatch.StartNew();
            try
            {
                LogHeader();
                var f = ServiceScope.ServiceProvider.CreateInstance<T>();
                result = await (function ?? throw new ArgumentNullException(nameof(function)))(f).ConfigureAwait(false);
            }
            catch (Exception uex)
            {
                ex = uex;
            }
            finally
            {
                sw.Stop();
            }

            await Task.Delay(0).ConfigureAwait(false);
            LogResult(ex, sw.ElapsedMilliseconds);

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
            return new ResultAssertor<TResult>(result, ex, Implementor, JsonSerializer);
        }

        /// <summary>
        /// Logs the header.
        /// </summary>
        private void LogHeader()
        {
            Implementor.WriteLine("");
            Implementor.WriteLine("GENERIC TYPE TESTER...");
            Implementor.WriteLine("");
            Implementor.WriteLine("LOGGING >");
        }

        /// <summary>
        /// Log the elapsed execution time.
        /// </summary>
        private void LogResult(Exception? ex, long ms)
        {
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