// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System;
using UnitTestEx.AspNetCore;

namespace UnitTestEx
{
    /// <summary>
    /// Provides a shared <see cref="Test"/> <see cref="ApiTester{TEntryPoint}"/> to enable usage of the same underlying <see cref="ApiTesterBase{TEntryPoint, TSelf}.GetTestServer"/> instance across multiple tests.
    /// </summary>
    /// <typeparam name="TEntryPoint">The API startup <see cref="Type"/>.</typeparam>
    /// <remarks>Implements <see cref="IDisposable"/> to automatically dispose of the <see cref="Test"/> instance to release all resources.
    /// <para>Be aware that using may result in cross-test contamination.</para></remarks>
    public abstract class WithApiTester<TEntryPoint> : IDisposable where TEntryPoint : class
    {
        private bool _disposed;
        private ApiTester<TEntryPoint>? _apiTester = ApiTester.Create<TEntryPoint>();

        /// <summary>
        /// Gets the shared <see cref="ApiTester{TEntryPoint}"/> for testing.
        /// </summary>
        public ApiTester<TEntryPoint> Test => _apiTester ?? throw new ObjectDisposedException(nameof(Test));

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
                    _apiTester?.Dispose();
                    _apiTester = null;
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