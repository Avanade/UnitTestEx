// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using CoreEx.Events;
using CoreEx.Json;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System;
using System.Collections.Generic;
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
        private readonly List<Action<IServiceCollection>> _configureServices = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="TesterBase{TSelf}"/> class.
        /// </summary>
        /// <param name="implementor">The <see cref="TestFrameworkImplementor"/>.</param>
        protected TesterBase(TestFrameworkImplementor implementor) : base(implementor) { }

        /// <summary>
        /// Gets the synchronization object where syncronized access is required.
        /// </summary>
        protected object SyncRoot { get; } = new object();

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
        /// Updates (replaces) the default test <see cref="TesterBase.UserName"/>.
        /// </summary>
        /// <param name="userName">The test user name (a <c>null</c> value will reset to <see cref="TesterBase.SetUp"/> <see cref="TestSetUp.DefaultUserName"/>).</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        public TSelf UseUser(string? userName)
        {
            UserName = userName ?? SetUp.DefaultUserName;
            return (TSelf)this;
        }

        /// <summary>
        /// Updates (replaces) the default test <see cref="TesterBase.UserName"/>.
        /// </summary>
        /// <param name="userIdentifier">The test user identifier (a <c>null</c> value will reset to <see cref="TesterBase.SetUp"/> <see cref="TestSetUp.DefaultUserName"/>).</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        /// <remarks>The <see cref="TestSetUp.UserNameConverter"/> is required for the conversion to take place.</remarks>
        public TSelf UseUser(object? userIdentifier)
        {
            if (userIdentifier == null)
                return UseUser(null);

            if (SetUp.UserNameConverter == null)
                throw new InvalidOperationException($"The {nameof(TestSetUp)}.{nameof(TestSetUp.UserNameConverter)} must be defined to support user identifier conversion.");

            return UseUser(SetUp.UserNameConverter(userIdentifier));
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
        /// Resets the underlying host to instantiate a new instance.
        /// </summary>
        /// <param name="resetConfiguredServices">Indicates whether to reset the previously configured services.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        public TSelf ResetHost(bool resetConfiguredServices = false)
        {
            lock (SyncRoot)
            {
                IsHostInstantiated = false;
                if (resetConfiguredServices)
                {
                    _configureServices.Clear();
                    IsExpectedEventPublisherEnabled = false;
                }

                ResetHost();
                return (TSelf)this;
            }
        }

        /// <summary>
        /// Resets the underlying host to instantiate a new instance.
        /// </summary>
        protected abstract void ResetHost();

        /// <summary>
        /// Adds the previously <see cref="ConfigureServices(Action{IServiceCollection}, bool)"/> to the <paramref name="services"/>.
        /// </summary>
        /// <remarks>It is recommended that this is performed within a <see cref="SyncRoot"/> to ensure thread-safety.</remarks>
        protected void AddConfiguredServices(IServiceCollection services)
        {
            if (IsHostInstantiated)
                throw new InvalidOperationException($"Underlying host has been instantiated and as such the {nameof(ConfigureServices)} operations can no longer be used.");

            foreach (var configureService in _configureServices)
            {
                configureService(services);
            }

            IsHostInstantiated = true;
        }

        /// <summary>
        /// Provides an opportunity to further configure the services before the underlying host is instantiated.
        /// </summary>
        /// <param name="configureServices">A delegate for configuring <see cref="IServiceCollection"/>.</param>
        /// <param name="autoResetHost">Indicates whether to automatically <see cref="ResetHost(bool)"/> (passing <c>false</c>) when configuring the services.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        /// <remarks>This can be called multiple times prior to the underlying host being instantiated. Internally, the <paramref name="configureServices"/> is queued and then played in order when the host is initially instantiated.
        /// Once instantiated, further calls will result in a <see cref="InvalidOperationException"/> unless a <see cref="ResetHost(bool)"/> is performed.</remarks>
        public TSelf ConfigureServices(Action<IServiceCollection> configureServices, bool autoResetHost = true)
        {
            lock (SyncRoot)
            {
                if (autoResetHost)
                    ResetHost(false);

                _configureServices.Add(configureServices);
            }

            return (TSelf)this;
        }

        /// <summary>
        /// Replaces (where existing), or adds, a singleton service with the <paramref name="mockHttpClientFactory"/>.
        /// </summary>
        /// <param name="mockHttpClientFactory">The <see cref="Mocking.MockHttpClientFactory"/>.</param>
        /// <param name="autoResetHost">Indicates whether to automatically <see cref="ResetHost(bool)"/> (passing <c>false</c>) when configuring the service.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        public TSelf ReplaceHttpClientFactory(MockHttpClientFactory mockHttpClientFactory, bool autoResetHost = true) => ConfigureServices(sc => (mockHttpClientFactory ?? throw new ArgumentNullException(nameof(mockHttpClientFactory))).Replace(sc), autoResetHost);

        /// <summary>
        /// Replaces (where existing), or adds, a singleton service with a mock object.
        /// </summary>
        /// <typeparam name="TService">The service <see cref="Type"/> being mocked.</typeparam>
        /// <param name="mock">The <see cref="Mock{T}"/>.</param>
        /// <param name="autoResetHost">Indicates whether to automatically <see cref="ResetHost(bool)"/> (passing <c>false</c>) when configuring the service.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        public TSelf MockSingleton<TService>(Mock<TService> mock, bool autoResetHost = true) where TService : class => ConfigureServices(sc => sc.ReplaceSingleton(_ => mock.Object), autoResetHost);

        /// <summary>
        /// Replaces (where existing), or adds, a scoped service with a mock object.
        /// </summary>
        /// <typeparam name="TService">The service <see cref="Type"/> being mocked.</typeparam>
        /// <param name="mock">The <see cref="Mock{T}"/>.</param>
        /// <param name="autoResetHost">Indicates whether to automatically <see cref="ResetHost(bool)"/> (passing <c>false</c>) when configuring the service.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        public TSelf MockScoped<TService>(Mock<TService> mock, bool autoResetHost = true) where TService : class => ConfigureServices(sc => sc.ReplaceScoped(_ => mock.Object), autoResetHost);

        /// <summary>
        /// Replaces (where existing), or adds, a transient service with a mock object.
        /// </summary>
        /// <typeparam name="TService">The service <see cref="Type"/> being mocked.</typeparam>
        /// <param name="mock">The <see cref="Mock{T}"/>.</param>
        /// <param name="autoResetHost">Indicates whether to automatically <see cref="ResetHost(bool)"/> (passing <c>false</c>) when configuring the service.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        public TSelf MockTransient<TService>(Mock<TService> mock, bool autoResetHost = true) where TService : class => ConfigureServices(sc => sc.ReplaceTransient(_ => mock.Object), autoResetHost);

        /// <summary>
        /// Replaces (where existing), or adds, a singleton service <paramref name="instance"/>. 
        /// </summary>
        /// <typeparam name="TService">The service <see cref="Type"/>.</typeparam>
        /// <param name="instance">The instance value.</param>
        /// <param name="autoResetHost">Indicates whether to automatically <see cref="ResetHost(bool)"/> (passing <c>false</c>) when configuring the service.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        public TSelf ReplaceSingleton<TService>(TService instance, bool autoResetHost = true) where TService : class => ConfigureServices(sc => sc.ReplaceSingleton(_ => instance), autoResetHost);

        /// <summary>
        /// Replaces (where existing), or adds, a singleton service using an <paramref name="implementationFactory"/>.
        /// </summary>
        /// <typeparam name="TService">The service <see cref="Type"/>.</typeparam>
        /// <param name="implementationFactory">The implementation factory.</param>
        /// <param name="autoResetHost">Indicates whether to automatically <see cref="ResetHost(bool)"/> (passing <c>false</c>) when configuring the service.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        public TSelf ReplaceSingleton<TService>(Func<IServiceProvider, TService> implementationFactory, bool autoResetHost = true) where TService : class => ConfigureServices(sc => sc.ReplaceSingleton(implementationFactory), autoResetHost);

        /// <summary>
        /// Replaces (where existing), or adds, a singleton service. 
        /// </summary>
        /// <typeparam name="TService">The service <see cref="Type"/>.</typeparam>
        /// <param name="autoResetHost">Indicates whether to automatically <see cref="ResetHost(bool)"/> (passing <c>false</c>) when configuring the service.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        public TSelf ReplaceSingleton<TService>(bool autoResetHost = true) where TService : class => ConfigureServices(sc => sc.ReplaceSingleton<TService>(), autoResetHost);

        /// <summary>
        /// Replaces (where existing), or adds, a singleton service. 
        /// </summary>
        /// <typeparam name="TService">The service <see cref="Type"/>.</typeparam>
        /// <typeparam name="TImplementation">The implementation <see cref="Type"/>.</typeparam>
        /// <param name="autoResetHost">Indicates whether to automatically <see cref="ResetHost(bool)"/> (passing <c>false</c>) when configuring the service.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        public TSelf ReplaceSingleton<TService, TImplementation>(bool autoResetHost = true) where TService : class where TImplementation : class, TService => ConfigureServices(sc => sc.ReplaceSingleton<TService, TImplementation>(), autoResetHost);

        /// <summary>
        /// Replaces (where existing), or adds, a scoped service using an <paramref name="implementationFactory"/>.
        /// </summary>
        /// <typeparam name="TService">The service <see cref="Type"/>.</typeparam>
        /// <param name="implementationFactory">The implementation factory.</param>
        /// <param name="autoResetHost">Indicates whether to automatically <see cref="ResetHost(bool)"/> (passing <c>false</c>) when configuring the service.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        public TSelf ReplaceScoped<TService>(Func<IServiceProvider, TService> implementationFactory, bool autoResetHost = true) where TService : class => ConfigureServices(sc => sc.ReplaceScoped(implementationFactory), autoResetHost);

        /// <summary>
        /// Replaces (where existing), or adds, a scoped service. 
        /// </summary>
        /// <typeparam name="TService">The service <see cref="Type"/>.</typeparam>
        /// <param name="autoResetHost">Indicates whether to automatically <see cref="ResetHost(bool)"/> (passing <c>false</c>) when configuring the service.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        public TSelf ReplaceScoped<TService>(bool autoResetHost = true) where TService : class => ConfigureServices(sc => sc.ReplaceScoped<TService>(), autoResetHost);

        /// <summary>
        /// Replaces (where existing), or adds, a scoped service. 
        /// </summary>
        /// <typeparam name="TService">The service <see cref="Type"/>.</typeparam>
        /// <typeparam name="TImplementation">The implementation <see cref="Type"/>.</typeparam>
        /// <param name="autoResetHost">Indicates whether to automatically <see cref="ResetHost(bool)"/> (passing <c>false</c>) when configuring the service.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        public TSelf ReplaceScoped<TService, TImplementation>(bool autoResetHost = true) where TService : class where TImplementation : class, TService => ConfigureServices(sc => sc.ReplaceScoped<TService, TImplementation>(), autoResetHost);

        /// <summary>
        /// Replaces (where existing), or adds, a transient service using an <paramref name="implementationFactory"/>.
        /// </summary>
        /// <typeparam name="TService">The service <see cref="Type"/>.</typeparam>
        /// <param name="implementationFactory">The implementation factory.</param>
        /// <param name="autoResetHost">Indicates whether to automatically <see cref="ResetHost(bool)"/> (passing <c>false</c>) when configuring the service.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        public TSelf ReplaceTransient<TService>(Func<IServiceProvider, TService> implementationFactory, bool autoResetHost = true) where TService : class => ConfigureServices(sc => sc.ReplaceTransient(implementationFactory), autoResetHost);

        /// <summary>
        /// Replaces (where existing), or adds, a transient service. 
        /// </summary>
        /// <typeparam name="TService">The service <see cref="Type"/>.</typeparam>
        /// <param name="autoResetHost">Indicates whether to automatically <see cref="ResetHost(bool)"/> (passing <c>false</c>) when configuring the service.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        public TSelf ReplaceTransient<TService>(bool autoResetHost = true) where TService : class => ConfigureServices(sc => sc.ReplaceTransient<TService>(), autoResetHost);

        /// <summary>
        /// Replaces (where existing), or adds, a transient service. 
        /// </summary>
        /// <typeparam name="TService">The service <see cref="Type"/>.</typeparam>
        /// <typeparam name="TImplementation">The implementation <see cref="Type"/>.</typeparam>
        /// <param name="autoResetHost">Indicates whether to automatically <see cref="ResetHost(bool)"/> (passing <c>false</c>) when configuring the service.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        public TSelf ReplaceTransient<TService, TImplementation>(bool autoResetHost = true) where TService : class where TImplementation : class, TService => ConfigureServices(sc => sc.ReplaceTransient<TService, TImplementation>(), autoResetHost);

        /// <summary>
        /// Wraps the host execution to perform required start-up style activities; specifically resetting the <see cref="TestSharedState"/>.
        /// </summary>
        /// <typeparam name="T">The result <see cref="Type"/>.</typeparam>
        /// <param name="result">The function to create the result.</param>
        /// <returns>The <paramref name="result"/>.</returns>
        internal T HostExecutionWrapper<T>(Func<T> result)
        {
            TestSetUp.LogAutoSetUpOutputs(Implementor);
            SharedState.ResetEventStorage();
            return result();
        }
    }
}