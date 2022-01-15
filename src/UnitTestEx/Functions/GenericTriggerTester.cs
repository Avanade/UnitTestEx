// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System;
using System.Globalization;
using System.Linq.Expressions;
using System.Threading.Tasks;
using UnitTestEx.Abstractions;

namespace UnitTestEx.Functions
{
    /// <summary>
    /// Provides the Azure Function <see cref="HttpTriggerTester{TFunction}"/> unit-testing capabilities.
    /// </summary>
    /// <typeparam name="TFunction">The Azure Function <see cref="Type"/>.</typeparam>
    public class GenericTriggerTester<TFunction> : TriggerTesterBase<TFunction> where TFunction : class
    {
        /// <summary>
        /// Initializes a new <see cref="GenericTriggerTester{TFunction}"/> class.
        /// </summary>
        /// <param name="serviceScope">The <see cref="IServiceScope"/>.</param>
        /// <param name="implementor">The <see cref="TestFrameworkImplementor"/>.</param>
        internal GenericTriggerTester(IServiceScope serviceScope, TestFrameworkImplementor implementor) : base(serviceScope, implementor) { }

        /// <summary>
        /// Runs the asynchronous function method with no result.
        /// </summary>
        /// <param name="expression">The function execution expression.</param>
        public VoidAssertor Run(Expression<Func<TFunction, Task>> expression)
        {
            (Exception? ex, long ms) = RunFunction(expression, null, null);
            LogElapsed(ex, ms);
            LogTrailer();
            return new VoidAssertor(ex, Implementor);
        }

        /// <summary>
        /// Runs the asynchronous function method with a result.
        /// </summary>
        /// <param name="expression">The function execution expression.</param>
        /// <returns>The resulting value.</returns>
        public ResultAssertor<TResult> Run<TResult>(Expression<Func<TFunction, Task<TResult>>> expression)
        {
            (TResult result, Exception? ex, long ms) = RunFunction(expression, null, null);
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
            Implementor.WriteLine("FUNCTION GENERIC-TRIGGER TESTER...");
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