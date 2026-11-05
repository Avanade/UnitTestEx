// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System;
using UnitTestEx.Abstractions;
using UnitTestEx.Azure.Functions;

namespace UnitTestEx
{
    /// <summary>
    /// Provides a shared <see cref="FunctionTester{TEntryPoint}"/> for tests that reuse the same underlying Azure Functions application host.
    /// </summary>
    /// <typeparam name="TEntryPoint">Any <see cref="Type"/> within the Azure Functions isolated-worker application assembly.</typeparam>
    /// <remarks>Implements <see cref="IDisposable"/> to dispose the tester and its host. The host is built lazily and never started.
    /// Sharing depends on the test framework's fixture lifetime; isolate mutable state and do not use the shared tester concurrently.</remarks>
    public abstract class WithFunctionTester<TEntryPoint> : IDisposable where TEntryPoint : class
    {
        private FunctionTester<TEntryPoint>? _functionTester;

        /// <summary>
        /// Initializes a new instance of the <see cref="WithFunctionTester{TEntryPoint}"/> class.
        /// </summary>
        /// <param name="createFactory">The optional function to create the <see cref="TestFrameworkImplementor"/> instance.</param>
        protected WithFunctionTester(Func<TestFrameworkImplementor>? createFactory = null)
        {
            _functionTester = FunctionTester.Create<TEntryPoint>(createFactory: createFactory);
        }

        /// <summary>
        /// Gets the underlying <see cref="FunctionTester{TEntryPoint}"/> for testing.
        /// </summary>
        /// <exception cref="ObjectDisposedException">The fixture has been disposed.</exception>
        public FunctionTester<TEntryPoint> Test => _functionTester ?? throw new ObjectDisposedException(nameof(Test));

        /// <summary>
        /// Releases the managed resources when disposing.
        /// </summary>
        /// <param name="disposing">Indicates whether to release managed resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                var tester = _functionTester;
                _functionTester = null;
                tester?.Dispose();
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
