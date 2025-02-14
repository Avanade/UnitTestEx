// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System;
using UnitTestEx.Abstractions;
using UnitTestEx.Xunit.Internal;
using Xunit.Abstractions;

namespace UnitTestEx
{
    /// <summary>
    /// Provides the base <b>Xunit</b> capabilities.
    /// </summary>
    /// <remarks>Primarily configures the required <see cref="TestFrameworkImplementor.SetLocalCreateFactory(Func{TestFrameworkImplementor})"/> for the <see cref="XunitTestImplementor"/>.</remarks>
    public abstract class UnitTestBase : IDisposable
    {
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="UnitTestBase"/> class.
        /// </summary>
        /// <param name="output">The <see cref="ITestOutputHelper"/>.</param>
        protected UnitTestBase(ITestOutputHelper output)
        {
            Output = output ?? throw new ArgumentNullException(nameof(output));
            TestFrameworkImplementor.SetLocalCreateFactory(() => new XunitTestImplementor(output));
        }

        /// <summary>
        /// Gets the <see cref="ITestOutputHelper"/>.
        /// </summary>
        protected ITestOutputHelper Output { get; }

        /// <summary>
        /// Dispose of all resources.
        /// </summary>
        /// <param name="disposing">Indicates whether from the <see cref="Dispose()"/>.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                    TestFrameworkImplementor.ResetLocalCreateFactory();

                _disposed = true;
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