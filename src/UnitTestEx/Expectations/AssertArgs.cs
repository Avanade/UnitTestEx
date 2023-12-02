// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System;
using System.Collections.Generic;
using UnitTestEx.Abstractions;

namespace UnitTestEx.Expectations
{
    /// <summary>
    /// Provides the assertion arguments.
    /// </summary>
    public class AssertArgs
    {
        private readonly bool _hasValue;
        private readonly object? _value;
        private Dictionary<string, object?>? _extras;

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertArgs"/> class.
        /// </summary>
        /// <param name="tester">The owning <see cref="TesterBase"/>.</param>
        /// <param name="pathsToIgnore">The JSON paths to ignore where comparing the result value.</param>
        /// <param name="exception">The <see cref="Exception"/>.</param>
        /// <param name="logs">The logs captured.</param>
        internal AssertArgs(TesterBase tester, IEnumerable<string> pathsToIgnore, Exception? exception, IEnumerable<string?>? logs)
        {
            Tester = tester ?? throw new ArgumentNullException(nameof(tester));
            PathsToIgnore = pathsToIgnore;
            Exception = exception;
            Logs = logs;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertArgs"/> class with a <paramref name="value"/>.
        /// </summary>
        /// <param name="tester">The owning <see cref="TesterBase"/>.</param>
        /// <param name="pathsToIgnore">The JSON paths to ignore where comparing the result value.</param>
        /// <param name="exception">The <see cref="Exception"/>.</param>
        /// <param name="logs">The logs captured.</param>
        /// <param name="value">The resulting value.</param>
        internal AssertArgs(TesterBase tester, IEnumerable<string> pathsToIgnore, Exception? exception, IEnumerable<string?>? logs, object? value) : this(tester, pathsToIgnore, exception, logs)
        {
            _hasValue = true;
            _value = value;
        }

        /// <summary>
        /// Gets the owning <see cref="TesterBase"/>.
        /// </summary>
        public TesterBase Tester { get; }

        /// <summary>
        /// Gets the <see cref="Exception"/> that was the result of the test.
        /// </summary>
        public Exception? Exception { get; }

        /// <summary>
        /// Gets the logs captured during the test.
        /// </summary>
        public IEnumerable<string?>? Logs { get; }

        /// <summary>
        /// Gets the JSON paths to ignore where comparing the result value.
        /// </summary>
        public IEnumerable<string> PathsToIgnore { get; set; }

        /// <summary>
        /// Indicates whether there is a <see cref="Value"/>; which may be <c>null</c>
        /// </summary>
        public bool HasValue => _hasValue;

        /// <summary>
        /// Gets the resulting value (where <see cref="HasValue"/>).
        /// </summary>
        public object? Value
        {
            get
            {
                if (_hasValue)
                    return _value;
                
                Tester.Implementor.AssertFail($"Expected a value; no value resulted.");
                throw new InvalidOperationException();
            }
        }

        /// <summary>
        /// Gets the <see cref="Value"/> (where <see cref="HasValue"/>) as <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The <see cref="Value"/> <see cref="Type"/>.</typeparam>
        /// <returns>The typed value.</returns>
        public T GetValue<T>() => (T)Value!;
        
        /// <summary>
        /// Trys to get the specified extra value.
        /// </summary>
        /// <typeparam name="T">The <paramref name="value"/> <see cref="Type"/>.</typeparam>
        /// <param name="key">The key.</param>
        /// <param name="value">The value where found.</param>
        /// <returns><c>true</c> where found; otherwise, <c>false</c>.</returns>
        public bool TryGetExtra<T>(string key, out T value)
        {
            if (_extras is null || !_extras.TryGetValue(key, out var result))
            {
                value = default!;
                return false;
            }

            value = (T)result!;
            return true;
        }

        /// <summary>
        ///  Trys to get the specified extra value using the <typeparamref name="T"/> <see cref="Type"/> name as the key.
        /// </summary>
        /// <typeparam name="T">The <paramref name="value"/> <see cref="Type"/>.</typeparam>
        /// <param name="value">The value where found.</param>
        /// <returns><c>true</c> where found; otherwise, <c>false</c>.</returns>
        public bool TryGetExtra<T>(out T? value) => TryGetExtra(typeof(T).Name!, out value);

        /// <summary>
        /// Adds an extra value.
        /// </summary>
        /// <typeparam name="T">The <paramref name="value"/> <see cref="Type"/>.</typeparam>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <returns>The <see cref="AssertArgs"/> instance to support fluent-style method-chaining.</returns>
        public AssertArgs AddExtra<T>(string key, T value)
        {
            _extras ??= [];
            _extras.Add(key, value);
            return this;
        }

        /// <summary>
        /// Adds an extra value using the <typeparamref name="T"/> <see cref="Type"/> name as the key.
        /// </summary>
        /// <typeparam name="T">The <paramref name="value"/> <see cref="Type"/>.</typeparam>
        /// <param name="value">The value.</param>
        /// <returns>The <see cref="AssertArgs"/> instance to support fluent-style method-chaining.</returns>
        public AssertArgs AddExtra<T>(T value) => AddExtra(typeof(T).Name!, value);
    }
}