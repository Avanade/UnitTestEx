// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System;
using System.Collections.Generic;
using UnitTestEx.Abstractions;
using UnitTestEx.Json;

namespace UnitTestEx.Assertors
{
    /// <summary>
    /// Represents the base test assert helper.
    /// </summary>
    /// <param name="owner">The owning <see cref="TesterBase"/>.</param>
    /// <param name="exception">The <see cref="Exception"/> (if any).</param>
    public abstract class AssertorBase(TesterBase owner, Exception? exception)
    {
        private static List<Func<AssertorBase, ApiError[], bool>>? _assertErrorsExtentions;

        /// <summary>
        /// Adds an <c>AssertErrors</c> extension to support custom error assertions.
        /// </summary>
        /// <param name="assertErrorsExtension">The assertion extension.</param>
        /// <remarks>The extension functions return a <c>bool</c>; where <c>true</c> indicates that the function asserted the errors and no further assertions need to be performed.</remarks>
        public static void AddAssertErrorsExtension(Func<AssertorBase, ApiError[], bool> assertErrorsExtension) => (_assertErrorsExtentions ??= []).Add(assertErrorsExtension);

        /// <summary>
        /// Gets the owning <see cref="TesterBase"/>.
        /// </summary>
        public TesterBase Owner { get; } = owner ?? throw new ArgumentNullException(nameof(owner));

        /// <summary>
        /// Gets the <see cref="System.Exception"/>.
        /// </summary>
        public Exception? Exception { get; } = exception;

        /// <summary>
        /// Gets the <see cref="TestFrameworkImplementor"/>.
        /// </summary>
        protected TestFrameworkImplementor Implementor => Owner.Implementor;

        /// <summary>
        /// Gets the <see cref="IJsonSerializer"/>.
        /// </summary>
        protected IJsonSerializer JsonSerializer => Owner.JsonSerializer;

        /// <summary>
        /// Asserts the errors using the extensions.
        /// </summary>
        internal void AssertErrorsUsingExtensions(ApiError[] errors)
        {
            if (_assertErrorsExtentions is null)
                throw new NotImplementedException($"AssertErrors is unable to be performed as there are no extensions to perform the assertion; see {nameof(AddAssertErrorsExtension)}.");
            else
            {
                foreach (var ae in _assertErrorsExtentions)
                {
                    if (ae(this, errors))
                        return;
                }

                if (!Assertor.TryAreErrorsMatched(errors, Array.Empty<ApiError>(), out var errorMessage))
                    Implementor.AssertFail(errorMessage);
            }
        }
    }
}