// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using CoreEx;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using UnitTestEx.Abstractions;
using UnitTestEx.Assertors;

namespace UnitTestEx.Generic
{
    /// <summary>
    /// Provides generic testing capabilities.
    /// </summary>
    public abstract class GenericTesterBase<TSelf> : GenericTesterCore<GenericTesterBase<TSelf>>
    {
        private OperationType _operationType = CoreEx.OperationType.Unspecified;

        /// <summary>
        /// Initializes a new instance of the <see cref="GenericTesterBase{TSelf}"/> class.
        /// </summary>
        /// <param name="implementor">The <see cref="TestFrameworkImplementor"/>.</param>
        protected GenericTesterBase(TestFrameworkImplementor implementor) : base(implementor) { }

        /// <summary>
        /// Sets the <see cref="ExecutionContext.OperationType"/> to the specified <paramref name="operationType"/>.
        /// </summary>
        /// <param name="operationType">The <see cref="OperationType"/>.</param>
        /// <returns>The <see cref="GenericTesterBase{TSelf}"/> instance to support fluent/chaining usage.</returns>
        public GenericTesterBase<TSelf> OperationType(OperationType operationType)
        {
            _operationType = operationType;
            return this;
        }

        /// <summary>
        /// Executes the <paramref name="action"/> that performs the validation.
        /// </summary>
        /// <param name="action">The function performing the validation.</param>
        /// <returns>The resulting <see cref="VoidAssertor"/>.</returns>
        public VoidAssertor Run(Action action) => RunAsync(() =>
        {
            (action ?? throw new ArgumentNullException(nameof(action))).Invoke();
            return Task.CompletedTask;
        }).GetAwaiter().GetResult();

        /// <summary>
        /// Executes the <paramref name="function"/> that performs the validation.
        /// </summary>
        /// <param name="function">The function performing the validation.</param>
        /// <returns>The resulting <see cref="VoidAssertor"/>.</returns>
        public VoidAssertor Run(Func<Task> function) => RunAsync(function).GetAwaiter().GetResult();

        /// <summary>
        /// Executes the <paramref name="function"/> that performs the validation.
        /// </summary>
        /// <param name="function">The function performing the validation.</param>
        /// <returns>The resulting <see cref="VoidAssertor"/>.</returns>
        public async Task<VoidAssertor> RunAsync(Func<Task> function)
        {
            if (function == null)
                throw new ArgumentNullException(nameof(function));

            Implementor.WriteLine("");
            Implementor.WriteLine("VALIDATION TESTER...");
            Implementor.WriteLine("");

            var ec = Services.GetRequiredService<ExecutionContext>();
            ec.OperationType = _operationType;

            Exception? exception = null;

            Implementor.WriteLine("VALIDATE >");
            Implementor.WriteLine("Validator: <function>");
            Implementor.WriteLine("");
            Implementor.WriteLine("LOGGING >");

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
            return new VoidAssertor(exception, Implementor, JsonSerializer);
        }

        /// <summary>
        /// Executes the <paramref name="function"/> that performs the validation.
        /// </summary>
        /// <param name="function">The function performing the validation.</param>
        /// <returns>The resulting <see cref="ValueAssertor{TValue}"/>.</returns>
        public ValueAssertor<T> Run<T>(Func<T> function) => RunAsync(() =>
        {
            T value = (function ?? throw new ArgumentNullException(nameof(function))).Invoke();
            return Task.FromResult(value);
        }).GetAwaiter().GetResult();

        /// <summary>
        /// Executes the <paramref name="function"/> that performs the validation.
        /// </summary>
        /// <param name="function">The function performing the validation.</param>
        /// <returns>The resulting <see cref="ValueAssertor{TValue}"/>.</returns>
        public ValueAssertor<T> Run<T>(Func<Task<T>> function) => RunAsync(function).GetAwaiter().GetResult();

        /// <summary>
        /// Executes the <paramref name="function"/> that performs the validation.
        /// </summary>
        /// <param name="function">The function performing the validation.</param>
        /// <returns>The resulting <see cref="ValueAssertor{TValue}"/>.</returns>
        public async Task<ValueAssertor<T>> RunAsync<T>(Func<Task<T>> function)
        {
            if (function == null)
                throw new ArgumentNullException(nameof(function));

            Implementor.WriteLine("");
            Implementor.WriteLine("VALIDATION TESTER...");
            Implementor.WriteLine("");

            var ec = Services.GetRequiredService<ExecutionContext>();
            ec.OperationType = _operationType;

            Exception? exception = null;

            Implementor.WriteLine("VALIDATE >");
            Implementor.WriteLine("Validator: <function>");
            Implementor.WriteLine("");
            Implementor.WriteLine("LOGGING >");

            var sw = System.Diagnostics.Stopwatch.StartNew();
            T value = default!;

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
            return new ValueAssertor<T>(value, exception, Implementor, JsonSerializer);
        }
    }
}