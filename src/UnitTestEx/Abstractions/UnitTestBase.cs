// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System;

namespace UnitTestEx.Abstractions
{
    /// <summary>
    /// Provides a framework-neutral base for supplying per-test output to UnitTestEx.
    /// </summary>
    public abstract class UnitTestBase : IDisposable
    {
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="UnitTestBase"/> class.
        /// </summary>
        /// <param name="output">An optional framework output helper, <see cref="Action{T}"/>, or writer object.</param>
        /// <param name="createFactory">An optional implementor factory that overrides automatic framework detection.</param>
        protected UnitTestBase(object? output = null, Func<TestFrameworkImplementor>? createFactory = null)
        {
            Output = output;
            TestFrameworkImplementor.SetLocalImplementor(createFactory is null ? TestFrameworkImplementor.CreateForCurrentFramework(output) : createFactory() ?? throw new InvalidOperationException($"The {nameof(createFactory)} has returned null."));
        }

        /// <summary>
        /// Gets the optional framework output helper or callback supplied to the constructor.
        /// </summary>
        protected object? Output { get; }

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
                    try
                    {
                        OnDispose();
                    }
                    finally
                    {
                        TestFrameworkImplementor.ResetLocalImplementor();
                    }
                }
            }
        }

        /// <summary>
        /// Releases resources owned by a derived test fixture.
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
