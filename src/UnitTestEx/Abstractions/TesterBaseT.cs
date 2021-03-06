// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using CoreEx;
using CoreEx.Events;
using CoreEx.Json;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System;
using UnitTestEx.Expectations;
using UnitTestEx.Mocking;

namespace UnitTestEx.Abstractions
{
    /// <summary>
    /// Provides the common/core base unit-testing capabilities.
    /// </summary>
    /// <typeparam name="TSelf">The <see cref="TesterBase{TSelf}"/> to support inheriting fluent-style method-chaining.</typeparam>
    public abstract class TesterBase<TSelf> : TesterBase where TSelf : TesterBase<TSelf>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TesterBase{TSelf}"/> class.
        /// </summary>
        /// <param name="implementor">The <see cref="TestFrameworkImplementor"/>.</param>
        /// <param name="username">The username (<c>null</c> indicates to use the existing <see cref="ExecutionContext.Current"/> <see cref="ExecutionContext.Username"/> where configured).</param>
        protected TesterBase(TestFrameworkImplementor implementor, string? username) : base(implementor, username) { }

        /// <summary>
        /// Updates (replaces) the <see cref="TesterBase.SetUp"/>.
        /// </summary>
        /// <param name="setUp">The <see cref="TestSetUp"/>.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        /// <remarks>Also executes the <see cref="TestSetUp.ConfigureServices"/>.</remarks>
        public TSelf UseSetUp(TestSetUp setUp)
        {
            SetUp = setUp ?? throw new ArgumentNullException(nameof(setUp));
            if (SetUp.ConfigureServices != null)
                ConfigureServices(SetUp.ConfigureServices);

            if (SetUp.ExpectedEventsEnabled)
                UseExpectedEvents();

            return (TSelf)this;
        }

        /// <summary>
        /// Updates the <see cref="JsonSerializer"/> used by the <see cref="TesterBase{TSelf}"/> itself, not the underlying executing host which should be configured separately.
        /// </summary>
        /// <param name="jsonSerializer">The <see cref="JsonSerializer"/>.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        public TSelf UseJsonSerializer(IJsonSerializer jsonSerializer)
        {
            JsonSerializer = jsonSerializer ?? throw new ArgumentNullException(nameof(jsonSerializer));
            return (TSelf)this;
        }

        /// <summary>
        /// Replaces the <see cref="IEventPublisher"/> with the <see cref="ExpectedEventPublisher"/> to enable the likes of <see cref="Expectations.ExpectationsExtensions.ExpectEvent{TSelf}(IEventExpectations{TSelf}, EventData, string[])"/>, etc.
        /// </summary>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        /// <remarks>This can also be set using <see cref="TestSetUp.ExpectedEventsEnabled"/> either via <see cref="TestSetUp.Default"/> or <see cref="UseSetUp(TestSetUp)"/>.</remarks>
        public TSelf UseExpectedEvents()
        {
            ConfigureServices(ReplaceExpectedEventPublisher);
            return (TSelf)this;
        }

        /// <summary>
        /// Performs the <see cref="IEventPublisher"/> replacement with <see cref="ExpectedEventPublisher"/>.
        /// </summary>
        /// <param name="sc">The <see cref="IServiceCollection"/>.</param>
        internal void ReplaceExpectedEventPublisher(IServiceCollection sc)
        {
            if (IsExpectedEventPublisherEnabled)
                return;

            sc.ReplaceScoped<IEventPublisher, ExpectedEventPublisher>();
            IsExpectedEventPublisherEnabled = true;
        }

        /// <summary>
        /// Provides an opportunity to further configure the services. This can be called multiple times. 
        /// </summary>
        /// <param name="configureServices">A delegate for configuring <see cref="IServiceCollection"/>.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        public abstract TSelf ConfigureServices(Action<IServiceCollection> configureServices);

        /// <summary>
        /// Replaces (where existing), or adds, a singleton service with the <paramref name="mockHttpClientFactory"/>.
        /// </summary>
        /// <param name="mockHttpClientFactory">The <see cref="Mocking.MockHttpClientFactory"/>.</param>
        /// <returns></returns>
        public TSelf ReplaceHttpClientFactory(MockHttpClientFactory mockHttpClientFactory) => ConfigureServices(sc => (mockHttpClientFactory ?? throw new ArgumentNullException(nameof(mockHttpClientFactory))).Replace(sc));

        /// <summary>
        /// Replaces (where existing), or adds, a singleton service with a mock object.
        /// </summary>
        /// <typeparam name="TService">The service <see cref="Type"/> being mocked.</typeparam>
        /// <param name="mock">The <see cref="Mock{T}"/>.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        public TSelf MockSingletonService<TService>(Mock<TService> mock) where TService : class => ConfigureServices(sc => sc.ReplaceSingleton(_ => mock.Object));

        /// <summary>
        /// Replaces (where existing), or adds, a scoped service with a mock object.
        /// </summary>
        /// <typeparam name="TService">The service <see cref="Type"/> being mocked.</typeparam>
        /// <param name="mock">The <see cref="Mock{T}"/>.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        public TSelf MockScopedService<TService>(Mock<TService> mock) where TService : class => ConfigureServices(sc => sc.ReplaceScoped(_ => mock.Object));

        /// <summary>
        /// Replaces (where existing), or adds, a transient service with a mock object.
        /// </summary>
        /// <typeparam name="TService">The service <see cref="Type"/> being mocked.</typeparam>
        /// <param name="mock">The <see cref="Mock{T}"/>.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        public TSelf MockTransientService<TService>(Mock<TService> mock) where TService : class => ConfigureServices(sc => sc.ReplaceTransient(_ => mock.Object));

        /// <summary>
        /// Replaces (where existing), or adds, a singleton service <paramref name="instance"/>. 
        /// </summary>
        /// <typeparam name="TService">The service <see cref="Type"/>.</typeparam>
        /// <param name="instance">The instance value.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        public TSelf ReplaceSingleton<TService>(TService instance) where TService : class => ConfigureServices(sc => sc.ReplaceSingleton(_ => instance));

        /// <summary>
        /// Replaces (where existing), or adds, a singleton service using an <paramref name="implementationFactory"/>.
        /// </summary>
        /// <typeparam name="TService">The service <see cref="Type"/>.</typeparam>
        /// <param name="implementationFactory">The implementation factory.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        public TSelf ReplaceSingleton<TService>(Func<IServiceProvider, TService> implementationFactory) where TService : class => ConfigureServices(sc => sc.ReplaceSingleton(implementationFactory));

        /// <summary>
        /// Replaces (where existing), or adds, a singleton service. 
        /// </summary>
        /// <typeparam name="TService">The service <see cref="Type"/>.</typeparam>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        public TSelf ReplaceSingleton<TService>() where TService : class => ConfigureServices(sc => sc.ReplaceSingleton<TService>());

        /// <summary>
        /// Replaces (where existing), or adds, a singleton service. 
        /// </summary>
        /// <typeparam name="TService">The service <see cref="Type"/>.</typeparam>
        /// <typeparam name="TImplementation">The implementation <see cref="Type"/>.</typeparam>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        public TSelf ReplaceSingleton<TService, TImplementation>() where TService : class where TImplementation : class, TService => ConfigureServices(sc => sc.ReplaceSingleton<TService, TImplementation>());

        /// <summary>
        /// Replaces (where existing), or adds, a scoped service using an <paramref name="implementationFactory"/>.
        /// </summary>
        /// <typeparam name="TService">The service <see cref="Type"/>.</typeparam>
        /// <param name="implementationFactory">The implementation factory.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        public TSelf ReplaceScoped<TService>(Func<IServiceProvider, TService> implementationFactory) where TService : class => ConfigureServices(sc => sc.ReplaceScoped(implementationFactory));

        /// <summary>
        /// Replaces (where existing), or adds, a scoped service. 
        /// </summary>
        /// <typeparam name="TService">The service <see cref="Type"/>.</typeparam>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        public TSelf ReplaceScoped<TService>() where TService : class => ConfigureServices(sc => sc.ReplaceScoped<TService>());

        /// <summary>
        /// Replaces (where existing), or adds, a scoped service. 
        /// </summary>
        /// <typeparam name="TService">The service <see cref="Type"/>.</typeparam>
        /// <typeparam name="TImplementation">The implementation <see cref="Type"/>.</typeparam>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        public TSelf ReplaceScoped<TService, TImplementation>() where TService : class where TImplementation : class, TService => ConfigureServices(sc => sc.ReplaceScoped<TService, TImplementation>());

        /// <summary>
        /// Replaces (where existing), or adds, a transient service using an <paramref name="implementationFactory"/>.
        /// </summary>
        /// <typeparam name="TService">The service <see cref="Type"/>.</typeparam>
        /// <param name="implementationFactory">The implementation factory.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        public TSelf ReplaceTransient<TService>(Func<IServiceProvider, TService> implementationFactory) where TService : class => ConfigureServices(sc => sc.ReplaceTransient(implementationFactory));

        /// <summary>
        /// Replaces (where existing), or adds, a transient service. 
        /// </summary>
        /// <typeparam name="TService">The service <see cref="Type"/>.</typeparam>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        public TSelf ReplaceTransient<TService>() where TService : class => ConfigureServices(sc => sc.ReplaceTransient<TService>());

        /// <summary>
        /// Replaces (where existing), or adds, a transient service. 
        /// </summary>
        /// <typeparam name="TService">The service <see cref="Type"/>.</typeparam>
        /// <typeparam name="TImplementation">The implementation <see cref="Type"/>.</typeparam>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        public TSelf ReplaceTransient<TService, TImplementation>() where TService : class where TImplementation : class, TService => ConfigureServices(sc => sc.ReplaceTransient<TService, TImplementation>());

        /// <summary>
        /// Wraps the host execution to perform required start-up style activities; specifically resetting the <see cref="TestSharedState"/>.
        /// </summary>
        /// <typeparam name="T">The result <see cref="Type"/>.</typeparam>
        /// <param name="result">The function to create the result.</param>
        /// <returns>The <paramref name="result"/>.</returns>
        internal T HostExecutionWrapper<T>(Func<T> result)
        {
            SharedState.Reset();
            return result();
        }
    }
}