// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System;
using UnitTestEx.AspNetCore;
using UnitTestEx.Xunit.Internal;

namespace UnitTestEx
{
    /// <summary>
    /// Provides a shared <see cref="Test"/> <see cref="ApiTester{TEntryPoint}"/> to enable usage of the same underlying <see cref="ApiTesterBase{TEntryPoint, TSelf}.GetTestServer"/> instance across multiple tests.
    /// </summary>
    /// <remarks>Be aware that using may result in cross-test contamination.</remarks>
    public class ApiTestFixture<TEntryPoint> : IDisposable where TEntryPoint : class
    {
        private ApiTester<TEntryPoint>? _apiTester = ApiTester.Create<TEntryPoint>(() => new XunitLocalTestImplementor());
        private bool _disposed;

        /// <summary>
        /// Gets the shared <see cref="ApiTester{TEntryPoint}"/> for testing.
        /// </summary>
        public ApiTester<TEntryPoint> Test => _apiTester ?? throw new ObjectDisposedException(nameof(Test));

        /// <summary>
        /// Releases the unmanaged resources and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
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