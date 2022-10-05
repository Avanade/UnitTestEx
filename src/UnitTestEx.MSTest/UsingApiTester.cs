// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System;

namespace UnitTestEx.MSTest
{
    /// <summary>
    /// Provides a shared <see cref="ApiTester"/> instance (automatically disposed) for all tests.
    /// </summary>
    /// <typeparam name="TEntryPoint">The API startup <see cref="Type"/>.</typeparam>
    public abstract class UsingApiTester<TEntryPoint> : IDisposable where TEntryPoint : class
    {
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="UsingApiTester{TEntryPoint}"/> class.
        /// </summary>
        protected UsingApiTester() => ApiTester = MSTest.ApiTester.Create<TEntryPoint>();

        /// <summary>
        /// Gets the <see cref="Internal.ApiTester{TEntryPoint}"/>.
        /// </summary>
        public Internal.ApiTester<TEntryPoint> ApiTester { get; }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases the unmanaged resources and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            ApiTester.Dispose();
            _disposed = true;
        }
    }
}