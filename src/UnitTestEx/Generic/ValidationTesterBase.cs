﻿// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using CoreEx;
using CoreEx.Validation;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using UnitTestEx.Abstractions;
using UnitTestEx.Assertors;

namespace UnitTestEx.Generic
{
    /// <summary>
    /// Provides the <see cref="IValidator"/> testing capabilities.
    /// </summary>
    public abstract class ValidationTesterBase<TSelf> : GenericTesterCore<ValidationTesterBase<TSelf>>
    {
        private OperationType _operationType = CoreEx.OperationType.Unspecified;

        /// <summary>
        /// Initializes a new instance of the <see cref="ValidationTesterBase{TSelf}"/> class.
        /// </summary>
        /// <param name="implementor">The <see cref="TestFrameworkImplementor"/>.</param>
        /// <param name="username">The username (<c>null</c> indicates to use the existing <see cref="CoreEx.ExecutionContext.Current"/> <see cref="CoreEx.ExecutionContext.Username"/> where configured).</param>
        protected ValidationTesterBase(TestFrameworkImplementor implementor, string? username) : base(implementor, username) { }

        /// <summary>
        /// Sets the <see cref="ExecutionContext.OperationType"/> to the specified <paramref name="operationType"/>.
        /// </summary>
        /// <param name="operationType">The <see cref="OperationType"/>.</param>
        /// <returns>The <see cref="ValidationTesterBase{TSelf}"/> instance to support fluent/chaining usage.</returns>
        public ValidationTesterBase<TSelf> OperationType(OperationType operationType)
        {
            _operationType = operationType;
            return this;
        }

        /// <summary>
        /// Creates (instantiates) the <typeparamref name="TValidator"/> using Dependency Injection (DI) and validates the <typeparamref name="TValue"/> <paramref name="value"/>.
        /// </summary>
        /// <typeparam name="TValue">The entity <see cref="Type"/>.</typeparam>
        /// <typeparam name="TValidator">The validator <see cref="Type"/> to instantiate.</typeparam>
        /// <param name="value">The <typeparamref name="TValue"/> value.</param>
        /// <returns>The resulting <see cref="IValidationResult"/>.</returns>
        public ValueAssertor<IValidationResult> Run<TValidator, TValue>(TValue value) where TValue : class where TValidator : class, IValidator<TValue> => RunAsync<TValidator, TValue>(value).GetAwaiter().GetResult();

        /// <summary>
        /// Validates the <typeparamref name="TValue"/> <paramref name="value"/> using the <paramref name="validator"/>.
        /// </summary>
        /// <typeparam name="TValue">The entity <see cref="Type"/>.</typeparam>
        /// <typeparam name="TValidator">The validator <see cref="Type"/> to instantiate.</typeparam>
        /// <param name="validator">The <see cref="IValidator{T}"/>.</param>
        /// <param name="value">The <typeparamref name="TValue"/> value.</param>
        /// <returns>The resulting <see cref="IValidationResult"/>.</returns>
        public ValueAssertor<IValidationResult> Run<TValidator, TValue>(TValidator validator, TValue value) where TValue : class where TValidator : class, IValidator<TValue> => RunAsync(validator, value).GetAwaiter().GetResult();

        /// <summary>
        /// Creates (instantiates) the <typeparamref name="TValidator"/> using Dependency Injection (DI) and validates asynchronously the <typeparamref name="TValue"/> <paramref name="value"/>.
        /// </summary>
        /// <typeparam name="TValue">The entity <see cref="Type"/>.</typeparam>
        /// <typeparam name="TValidator">The validator <see cref="Type"/> to instantiate.</typeparam>
        /// <param name="value">The <typeparamref name="TValue"/> value.</param>
        /// <returns>The resulting <see cref="IValidationResult"/>.</returns>
        public Task<ValueAssertor<IValidationResult>> RunAsync<TValidator, TValue>(TValue value) where TValue : class where TValidator : class, IValidator<TValue>
            => RunAsync(Services.GetService<TValidator>() ?? throw new InvalidOperationException($"Validator '{typeof(TValidator).FullName}' not configured using Dependency Injection (DI) and therefore unable to be instantiated for testing."), value);

        /// <summary>
        /// Validates asynchronously the <typeparamref name="TValue"/> <paramref name="value"/> using the <paramref name="validator"/>.
        /// </summary>
        /// <typeparam name="TValue">The entity <see cref="Type"/>.</typeparam>
        /// <typeparam name="TValidator">The validator <see cref="Type"/>.</typeparam>
        /// <param name="validator">The <see cref="IValidator{T}"/>.</param>
        /// <param name="value">The <typeparamref name="TValue"/> value.</param>
        /// <returns>The resulting <see cref="IValidationResult"/>.</returns>
        public Task<ValueAssertor<IValidationResult>> RunAsync<TValidator, TValue>(TValidator validator, TValue value) where TValue : class where TValidator : class, IValidator<TValue>
            => RunInternalAsync(async () => await validator.ValidateAsync(value).ConfigureAwait(false), (validator ?? throw new ArgumentNullException(nameof(validator))).GetType().FullName);

        /// <summary>
        /// Executes the <paramref name="validation"/> function.
        /// </summary>
        /// <param name="validation">The function performing the validation.</param>
        /// <returns>The resulting <see cref="IValidationResult"/>.</returns>
        public ValueAssertor<IValidationResult> Run(Func<Task<IValidationResult>> validation) => RunInternalAsync(validation, null).GetAwaiter().GetResult();

        /// <summary>
        /// Executes the <paramref name="validation"/> function.
        /// </summary>
        /// <param name="validation">The function performing the validation.</param>
        /// <returns>The resulting <see cref="IValidationResult"/>.</returns>
        public ValueAssertor<IValidationResult> Run(Func<IValidationResult> validation) => RunInternalAsync(() => Task.FromResult((validation ?? throw new ArgumentNullException(nameof(validation))).Invoke()), null).GetAwaiter().GetResult();

        /// <summary>
        /// Executes the <paramref name="validation"/> function.
        /// </summary>
        /// <param name="validation">The function performing the validation.</param>
        /// <returns>The resulting <see cref="IValidationResult"/>.</returns>
        public Task<ValueAssertor<IValidationResult>> RunAsync(Func<Task<IValidationResult>> validation) => RunInternalAsync(validation, null);

        /// <summary>
        /// Performs the internal validation orchestration.
        /// </summary>
        private async Task<ValueAssertor<IValidationResult>> RunInternalAsync(Func<Task<IValidationResult>> validation, string? validatorType)
        {
            if (validation == null)
                throw new ArgumentNullException(nameof(validation));

            Implementor.WriteLine("");
            Implementor.WriteLine("VALIDATION TESTER...");
            Implementor.WriteLine("");

            var ec = Services.GetRequiredService<ExecutionContext>();
            ec.OperationType = _operationType;

            IValidationResult? validationResult = null;
            Exception? exception = null;

            Implementor.WriteLine("VALIDATE >");
            Implementor.WriteLine($"Validator: {(validatorType == null ? "<function>" : validatorType)}");
            Implementor.WriteLine("");
            Implementor.WriteLine("LOGGING >");

            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                validationResult = await validation().ConfigureAwait(false);
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
            {
                Implementor.WriteLine($"ValidationResult:");
                Implementor.WriteLine(new CoreEx.Text.Json.JsonSerializer().Serialize(validationResult, CoreEx.Json.JsonWriteFormat.Indented));
            }

            Implementor.WriteLine("");
            Implementor.WriteLine(new string('=', 80));
            Implementor.WriteLine("");

            exception ??= validationResult?.ToValidationException();
            ExceptionSuccessExpectations.Assert(exception);
            return new ValueAssertor<IValidationResult>(validationResult!, exception, Implementor, JsonSerializer);
        }
    }
}