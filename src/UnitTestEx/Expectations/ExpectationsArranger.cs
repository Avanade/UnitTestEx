// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnitTestEx.Abstractions;
using UnitTestEx.Json;

namespace UnitTestEx.Expectations
{
    /// <summary>
    /// Enables <b>Expectations</b> assertions to be arranged and then finally asserted.
    /// </summary>
    /// <typeparam name="TTester">The <see cref="Tester"/> type.</typeparam>
    /// <remarks>An <see cref="ExpectationsArranger{TTester}.AssertAsync(AssertArgs)"/> will <see cref="Reset"/> after execution; as such, the configuration is not intended to be reused more than once.</remarks>
    /// <param name="owner">The owning <see cref="TesterBase"/>.</param>
    /// <param name="tester">The initiating tester.</param>
    public class ExpectationsArranger<TTester>(TesterBase owner, TTester tester)
    {
        private readonly Dictionary<Type, ExpectationsBase<TTester>> _expectations = [];

        /// <summary>
        /// Gets the owning <see cref="TesterBase"/>.
        /// </summary>
        public TesterBase Owner { get; } = owner ?? throw new ArgumentNullException(nameof(owner));

        /// <summary>
        /// Gets the initiating tester.
        /// </summary>
        public TTester Tester { get; } = tester ?? throw new ArgumentNullException(nameof(tester));

        /// <summary>
        /// Gets the collection of JSON paths to ignore where comparing a result value (where applicable).
        /// </summary>
        /// <remarks>See <see cref="JsonElementComparer"/>.</remarks>
        public List<string> PathsToIgnore { get; } = [];

        /// <summary>
        /// Gets or adds the <typeparamref name="TExpectation"/>.
        /// </summary>
        /// <typeparam name="TExpectation">The <see cref="ExpectationsBase{TTester}"/> <see cref="Type"/>.</typeparam>
        /// <returns>The <see cref="ExpectationsBase{TTester}"/> instance.</returns>
        public TExpectation GetOrAdd<TExpectation>(Func<TExpectation> factory) where TExpectation : ExpectationsBase<TTester>
        {
            if (_expectations.TryGetValue(typeof(TExpectation), out var expectation))
                return (TExpectation)expectation;

            expectation = factory() ?? throw new InvalidCastException("The factory must return an instance.");
            _expectations.Add(typeof(TExpectation), expectation);
            return (TExpectation)expectation;
        }

        /// <summary>
        /// Gets the <typeparamref name="TExpectation"/> if it exists.
        /// </summary>
        /// <typeparam name="TExpectation">The <see cref="ExpectationsBase{TTester}"/> <see cref="Type"/>.</typeparam>
        /// <param name="expectation">The <see cref="ExpectationsBase{TTester}"/> where it exists; otherwise, <c>null</c>.</param>
        /// <returns><c>true</c> where it exists; otherwise, <c>false</c>.</returns>
        public bool TryGet<TExpectation>(out TExpectation? expectation) where TExpectation : ExpectationsBase<TTester>
        {
            if (_expectations.TryGetValue(typeof(TExpectation), out var value))
            {
                expectation = (TExpectation)value;
                return true;
            }

            expectation = null;
            return false;
        }

        /// <summary>
        /// Performs the expectations assertion(s) with the specified <paramref name="logs"/> and <paramref name="exception"/>.
        /// </summary>
        /// <param name="logs">The logs captured.</param>
        /// <param name="exception">The <see cref="Exception"/>.</param>
        public Task AssertAsync(IEnumerable<string?>? logs, Exception? exception = null) => AssertAsync(CreateArgs(logs, exception));

        /// <summary>
        /// Performs the expectations assertion(s) with the specified <paramref name="logs"/>, <paramref name="value"/> and <paramref name="exception"/>.
        /// </summary>
        /// <param name="logs">The logs captured.</param>
        /// <param name="value">The resulting value.</param>
        /// <param name="exception">The <see cref="Exception"/>.</param>
        public Task AssertValueAsync(IEnumerable<string?>? logs, object? value, Exception? exception = null) => AssertAsync(CreateValueArgs(logs, value, exception));

        /// <summary>
        /// Performs the expectations assertion(s) for the specified <paramref name="args"/> and then does a <see cref="Reset"/> (regardless of outcome).
        /// </summary>
        public async Task AssertAsync(AssertArgs args)
        {
            try
            {
                foreach (var assert in _expectations.Values.OrderBy(x => x.Order))
                {
                    await assert.AssertAsync(args).ConfigureAwait(false);
                }
            }
            finally { Reset(); }
        }

        /// <summary>
        /// Resets any existing expectations back to their orginating assert state to allow for a re-execution.
        /// </summary>
        public void Reset()
        {
            foreach (var assert in _expectations.Values.OrderBy(x => x.Order))
            {
                assert.Reset();
            }
        }

        /// <summary>
        /// Clears (removes) any existing expectations.
        /// </summary>
        public void Clear() => _expectations.Clear();

        /// <summary>
        /// Creates a new <see cref="AssertArgs"/>.
        /// </summary>, 
        /// <param name="logs">The logs captured.</param>
        /// <param name="exception">The <see cref="Exception"/>.</param>
        /// <returns>The corresponding <see cref="AssertArgs"/>.</returns>
        public AssertArgs CreateArgs(IEnumerable<string?>? logs, Exception? exception = null) => new(Owner, PathsToIgnore, exception, logs);

        /// <summary>
        /// Creates a new <see cref="AssertArgs"/> with the specified <paramref name="value"/>.
        /// </summary>
        /// <param name="logs">The logs captured.</param>
        /// <param name="value">The resulting value.</param>
        /// <param name="exception">The <see cref="Exception"/>.</param>
        /// <returns>The corresponding <see cref="AssertArgs"/>.</returns>
        public AssertArgs CreateValueArgs(IEnumerable<string?>? logs, object? value, Exception? exception = null) => new(Owner, PathsToIgnore, exception, logs, value);
    }
}