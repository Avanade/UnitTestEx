// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System;
using UnitTestEx.Generic;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace UnitTestEx
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    /// <summary>
    /// Provides a shared <see cref="Test"/> <see cref="GenericTester{TEntryPoint}"/> to enable usage of the same underlying instance across multiple tests.
    /// </summary>
    /// <typeparam name="TEntryPoint">The generic startup <see cref="Type"/>.</typeparam>
    /// <remarks>Implements <see cref="IDisposable"/> to automatically dispose of the <see cref="Test"/> instance to release all resources.
    /// <para>Be aware that using may result in cross-test contamination.</para></remarks>
    public abstract class WithGenericTester<TEntryPoint> : IDisposable where TEntryPoint : class
    {
        private bool _disposed;
        private GenericTester<TEntryPoint>? _genericTester = GenericTester.Create<TEntryPoint>();

        /// <summary>
        /// Gets the underlying <see cref="GenericTester{TEntryPoint}"/> for testing.
        /// </summary>
        public GenericTester<TEntryPoint> Test => _genericTester ?? throw new ObjectDisposedException(nameof(Test));

        /// <summary>
        /// Dispose of all resources.
        /// </summary>
        /// <param name="disposing">Indicates whether from the <see cref="Dispose()"/>.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _genericTester?.Dispose();
                    _genericTester = null;
                }

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