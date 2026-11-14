// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System;
using UnitTestEx.Abstractions;

namespace UnitTestEx
{
    /// <summary>
    /// Provides a generic tester for use with framework-managed fixture lifetimes.
    /// </summary>
    /// <typeparam name="TEntryPoint">The generic startup type.</typeparam>
    /// <param name="output">An optional framework output helper or callback.</param>
    /// <param name="createFactory">An optional framework implementor factory.</param>
    public abstract class WithGenericTester<TEntryPoint>(object? output = null, Func<TestFrameworkImplementor>? createFactory = null) : UnitTestBase(output, createFactory) where TEntryPoint : class
    {
        private bool _configured;
        private UnitTestEx.Generic.GenericTester<TEntryPoint>? _genericTester = GenericTester.Create<TEntryPoint>();

        /// <summary>
        /// Gets the generic tester.
        /// </summary>
        public UnitTestEx.Generic.GenericTester<TEntryPoint> Test
        {
            get
            {
                var tester = _genericTester ?? throw new ObjectDisposedException(nameof(Test));
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

        /// <inheritdoc/>
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
                    var tester = _genericTester;
                    _genericTester = null;
                    tester?.Dispose();
                }
            }
        }
    }
}
