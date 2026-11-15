// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System;
using UnitTestEx.Abstractions;

namespace UnitTestEx
{
    /// <summary>
    /// Provides an API tester and optional shared fixture without a test-framework dependency.
    /// </summary>
    /// <typeparam name="TEntryPoint">The API startup type.</typeparam>
    public abstract class WithApiTester<TEntryPoint> : UnitTestBase where TEntryPoint : class
    {
        private readonly bool _ownsTester;
        private readonly ApiTestFixture<TEntryPoint>? _fixture;
        private bool _configured;
        private UnitTestEx.AspNetCore.ApiTester<TEntryPoint>? _apiTester;

        /// <summary>
        /// Initializes a new instance of the <see cref="WithApiTester{TEntryPoint}"/> class.
        /// </summary>
        /// <param name="output">An optional framework output helper or callback.</param>
        /// <param name="fixture">An optional framework-managed shared fixture.</param>
        /// <param name="createFactory">An optional framework implementor factory.</param>
        protected WithApiTester(object? output = null, ApiTestFixture<TEntryPoint>? fixture = null, Func<TestFrameworkImplementor>? createFactory = null)
            : base(output, createFactory)
        {
            _fixture = fixture;
            _ownsTester = fixture is null;
            if (_ownsTester)
                _apiTester = ApiTester.Create<TEntryPoint>();
        }

        /// <summary>
        /// Gets the API tester.
        /// </summary>
        public UnitTestEx.AspNetCore.ApiTester<TEntryPoint> Test
        {
            get
            {
                var tester = _fixture?.Test ?? _apiTester ?? throw new ObjectDisposedException(nameof(Test));
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
                if (disposing && _ownsTester)
                {
                    var tester = _apiTester;
                    _apiTester = null;
                    tester?.Dispose();
                }
            }
        }
    }
}
