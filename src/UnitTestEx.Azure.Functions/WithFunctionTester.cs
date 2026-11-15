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
    public abstract class WithFunctionTester<TEntryPoint> : UnitTestBase where TEntryPoint : class
    {
        private bool _configured;
        private FunctionTester<TEntryPoint>? _functionTester;

        /// <summary>
        /// Initializes a new instance of the <see cref="WithFunctionTester{TEntryPoint}"/> class using an optional test output helper.
        /// </summary>
        /// <param name="output">A framework output helper, callback, or writer.</param>
        /// <param name="createFactory">The optional function to create the <see cref="TestFrameworkImplementor"/> instance.</param>
        protected WithFunctionTester(object? output = null, Func<TestFrameworkImplementor>? createFactory = null)
            : base(output, createFactory)
        {
            _functionTester = FunctionTester.Create<TEntryPoint>();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WithFunctionTester{TEntryPoint}"/> class using an optional implementor factory.
        /// </summary>
        /// <param name="factory">The optional function to create the <see cref="TestFrameworkImplementor"/> instance.</param>
        protected WithFunctionTester(Func<TestFrameworkImplementor>? factory)
            : this(output: null, createFactory: factory) { }

        /// <summary>
        /// Gets the underlying <see cref="FunctionTester{TEntryPoint}"/> for testing.
        /// </summary>
        /// <exception cref="ObjectDisposedException">The fixture has been disposed.</exception>
        public FunctionTester<TEntryPoint> Test
        {
            get
            {
                var tester = _functionTester ?? throw new ObjectDisposedException(nameof(Test));
                if (!_configured)
                {
                    _configured = true;
                    OnConfiguration();
                }

                return tester;
            }
        }

        /// <summary>
        /// Configures the tester once, immediately before it is first exposed through <see cref="Test"/>.
        /// </summary>
        protected virtual void OnConfiguration() { }

        /// <summary>
        /// Releases the managed resources when disposing.
        /// </summary>
        /// <param name="disposing">Indicates whether to release managed resources.</param>
        protected override void Dispose(bool disposing)
        {
            try
            {
                base.Dispose(disposing);
            }
            finally
            {
                if (disposing)
                {
                    var tester = _functionTester;
                    _functionTester = null;
                    tester?.Dispose();
                }
            }
        }
    }
}
