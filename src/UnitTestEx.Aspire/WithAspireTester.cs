// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Aspire.Hosting;
using System;
using System.Threading.Tasks;
using UnitTestEx.Abstractions;
using UnitTestEx.Aspire;

namespace UnitTestEx
{
    /// <summary>
    /// Provides a shared <see cref="AspireTester{TAppHost}"/> for tests that reuse the same underlying Aspire distributed application.
    /// </summary>
    /// <typeparam name="TAppHost">The AppHost <see cref="Type"/> (a <c>Projects.*</c> type generated for the AppHost's <c>ProjectReference</c>).</typeparam>
    /// <remarks>Implements <see cref="IAsyncDisposable"/> to dispose the tester and its distributed application. Sharing an AppHost can reduce startup cost, but resources and captured logs
    /// persist across tests; reset or isolate mutable external state where required.</remarks>
    public abstract class WithAspireTester<TAppHost> : IAsyncDisposable where TAppHost : class
    {
        private AspireTester<TAppHost>? _aspireTester;

        /// <summary>
        /// Initializes a new instance of the <see cref="WithAspireTester{TAppHost}"/> class.
        /// </summary>
        /// <param name="createFactory">The optional function to create the <see cref="TestFrameworkImplementor"/> instance.</param>
        protected WithAspireTester(Func<TestFrameworkImplementor>? createFactory = null)
        {
            _aspireTester = AspireTester.Create<TAppHost>(createFactory)
                .BeforeStart(OnBeforeStartAsync)
                .AfterStart(OnAfterStartAsync);
        }

        /// <summary>
        /// Gets the underlying <see cref="AspireTester{TAppHost}"/> for testing.
        /// </summary>
        /// <exception cref="ObjectDisposedException">The fixture has been disposed.</exception>
        public AspireTester<TAppHost> Test => _aspireTester ?? throw new ObjectDisposedException(nameof(Test));

        /// <summary>
        /// Provides an opportunity to perform setup before the <see cref="DistributedApplication"/> is started.
        /// </summary>
        /// <param name="app">The <see cref="DistributedApplication"/> being started.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        protected virtual Task OnBeforeStartAsync(DistributedApplication app) => Task.CompletedTask;

        /// <summary>
        /// Provides an opportunity to perform setup after the <see cref="DistributedApplication"/> has started.
        /// </summary>
        /// <param name="app">The started <see cref="DistributedApplication"/>.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        protected virtual Task OnAfterStartAsync(DistributedApplication app) => Task.CompletedTask;

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            var tester = _aspireTester;
            if (tester is null)
                return;

            _aspireTester = null;
            await tester.DisposeAsync().ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }
    }
}
