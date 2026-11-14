// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System;
using UnitTestEx.Abstractions;

namespace UnitTestEx
{
    /// <summary>
    /// Provides a shared API tester for framework-managed test fixtures.
    /// </summary>
    /// <typeparam name="TEntryPoint">The API startup type.</typeparam>
    public class ApiTestFixture<TEntryPoint> : IDisposable where TEntryPoint : class
    {
        private UnitTestEx.AspNetCore.ApiTester<TEntryPoint>? _apiTester;
        private bool _configured;
        private bool _disposed;

        /// <summary>Initializes a shared fixture using the current per-test implementor.</summary>
        public ApiTestFixture() : this(null) { }

        /// <summary>Initializes a shared fixture using an optional framework implementor factory.</summary>
        /// <param name="createFactory">The optional framework implementor factory.</param>
        protected ApiTestFixture(Func<TestFrameworkImplementor>? createFactory)
            => _apiTester = ApiTester.Create<TEntryPoint>(createFactory ?? (() => new LocalTestFrameworkImplementor()));

        /// <summary>
        /// Provides an opportunity to configure the shared tester before use.
        /// </summary>
        protected virtual void OnConfiguration() { }

        /// <summary>
        /// Gets the shared API tester.
        /// </summary>
        public UnitTestEx.AspNetCore.ApiTester<TEntryPoint> Test
        {
            get
            {
                var tester = _apiTester ?? throw new ObjectDisposedException(nameof(Test));
                if (!_configured)
                {
                    _configured = true;
                    OnConfiguration();
                }

                return tester;
            }
        }

        /// <summary>
        /// Releases managed resources.
        /// </summary>
        /// <param name="disposing">Indicates whether managed resources should be released.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;
                if (disposing)
                {
                    var tester = _apiTester;

                    try
                    {
                        OnDispose();
                    }
                    finally
                    {
                        _apiTester = null;
                        tester?.Dispose();
                    }
                }
                else
                    _apiTester = null;
            }
        }

        /// <summary>
        /// Releases resources owned by a derived fixture.
        /// </summary>
        protected virtual void OnDispose() { }

        /// <inheritdoc/>
        public void Dispose()
        {
            try
            {
                Dispose(disposing: true);
            }
            finally
            {
                GC.SuppressFinalize(this);
            }
        }
    }
}
