// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System;
using System.Globalization;
using System.Linq.Expressions;
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
        internal TypeTester(IServiceScope serviceScope, TestFrameworkImplementor implementor) : base(serviceScope, implementor) { }

        /// <summary>
        /// Runs the asynchronous method with no result.
        /// </summary>
        /// <param name="expression">The function execution expression.</param>
        /// <returns>A <see cref="VoidAssertor"/>.</returns>
        public VoidAssertor Run(Expression<Func<T, Task>> expression) => RunAsync(expression).GetAwaiter().GetResult();

        /// <summary>
        /// Runs the asynchronous method with no result.
        /// </summary>
        /// <param name="expression">The function execution expression.</param>
        /// <returns>A <see cref="VoidAssertor"/>.</returns>
        public async Task<VoidAssertor> RunAsync(Expression<Func<T, Task>> expression)
        {
            (Exception? ex, long ms) = await RunAsync(expression, null, null);
            LogElapsed(ex, ms);
            LogTrailer();
            return new VoidAssertor(ex, Implementor);
        }

        /// <summary>
        /// Runs the asynchronous method with a result.
        /// </summary>
        /// <param name="expression">The function execution expression.</param>
        /// <returns>A <see cref="ResultAssertor{TResult}"/>.</returns>
        public ResultAssertor<TResult> Run<TResult>(Expression<Func<T, Task<TResult>>> expression) => RunAsync(expression).GetAwaiter().GetResult();

        /// <summary>
        /// Runs the asynchronous method with a result.
        /// </summary>
        /// <param name="expression">The function execution expression.</param>
        /// <returns>A <see cref="ResultAssertor{TResult}"/>.</returns>
        public async Task<ResultAssertor<TResult>> RunAsync<TResult>(Expression<Func<T, Task<TResult>>> expression)
        {
            (TResult result, Exception? ex, long ms) = await RunAsync(expression, null, null).ConfigureAwait(false);
            LogElapsed(ex, ms);

            if (ex == null)
            {
                if (result is string str)
                    Implementor.WriteLine($"Result: {str}");
                else if (result is IFormattable ifm)
                    Implementor.WriteLine($"Result: {ifm.ToString(null, CultureInfo.CurrentCulture)}");
                else
                {
                    Implementor.WriteLine($"Result: {(result == null ? "<null>" : "")}");
                    if (result != null)
                        Implementor.WriteLine(JsonConvert.SerializeObject(result, Formatting.Indented));
                }
            }

            LogTrailer();
            return new ResultAssertor<TResult>(result, ex, Implementor);
        }

        /// <summary>
        /// Log the elapsed execution time.
        /// </summary>
        private void LogElapsed(Exception? ex, long ms)
        {
            Implementor.WriteLine("");
            Implementor.WriteLine("GENERIC TYPE TESTER...");
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