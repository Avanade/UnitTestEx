// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnitTestEx.Json;

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
        public TesterBase(TestFrameworkImplementor implementor) : base(implementor) => UseSetUp(TestSetUp.Default);

        /// <summary>
        /// Replaces the <see cref="TesterBase.SetUp"/> by cloning the <paramref name="setUp"/> and will <see cref="ResetHost(bool)"/>.
        /// </summary>
        /// <param name="setUp">The <see cref="TestSetUp"/></param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        /// <remarks>Updates the <see cref="TesterBase.JsonSerializer"/> and <see cref="TesterBase.JsonComparerOptions"/> from the <paramref name="setUp"/>.
        /// <para>As the host is <see cref="ResetHost(bool)">reset</see> it is recommended that the <see cref="UseSetUp(TestSetUp)"/> is performed early so as to not inadvertently override earlier configurations.</para></remarks>
        public TSelf UseSetUp(TestSetUp setUp)
        {
            SetUp = setUp?.Clone() ?? throw new ArgumentNullException(nameof(setUp));
            JsonSerializer = SetUp.JsonSerializer;
            JsonComparerOptions = SetUp.JsonComparerOptions;

            foreach (var ext in TestSetUp.Extensions)
                ext.OnUseSetUp(this);

            ResetHost(false);
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
        /// Updates the <see cref="TesterBase.JsonSerializer"/> used by the <see cref="TesterBase{TSelf}"/> itself, not the underlying executing host which should be configured separately.
        /// </summary>
        /// <param name="jsonSerializer">The <see cref="JsonSerializer"/>.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        public TSelf UseJsonSerializer(IJsonSerializer jsonSerializer)
        {
            JsonSerializer = jsonSerializer ?? throw new ArgumentNullException(nameof(jsonSerializer));
            return (TSelf)this;
        }

        /// <summary>
        /// Updates the <see cref="TesterBase.JsonComparerOptions"/> used by the <see cref="TesterBase{TSelf}"/> itself, not the underlying executing host which should be configured separately.
        /// </summary>
        /// <param name="options">The <see cref="JsonElementComparerOptions"/>.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        /// <para>Where the <see cref="JsonElementComparerOptions.JsonSerializer"/> is <c>null</c> then the <see cref="TesterBase.JsonSerializer"/> will be used.</para>
        public TSelf UseJsonComparerOptions(JsonElementComparerOptions options)
        {
            JsonComparerOptions = options ?? throw new ArgumentNullException(nameof(options));
            return (TSelf)this;
        }

        /// <summary>
        /// Updates (replaces) the <see cref="TesterBase.AdditionalConfiguration"/> (see <see cref="MemoryConfigurationBuilderExtensions.AddInMemoryCollection(IConfigurationBuilder, IEnumerable{KeyValuePair{string, string}})"/>).
        /// </summary>
        /// <param name="additionalConfiguration">The additional configuration key/value pairs.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        /// <remarks>Usage will result in a <see cref="TesterBase.ResetHost()"/>.</remarks>
        public TSelf UseAdditionalConfiguration(IEnumerable<KeyValuePair<string, string?>>? additionalConfiguration)
        {
            AdditionalConfiguration = additionalConfiguration;
            return (TSelf)this;
        }

        /// <summary>
        /// Updates (replaces) the <see cref="TesterBase.AdditionalConfiguration"/> (see <see cref="MemoryConfigurationBuilderExtensions.AddInMemoryCollection(IConfigurationBuilder, IEnumerable{KeyValuePair{string, string}})"/>) with specified <paramref name="key"/> and <paramref name="value"/>.
        /// </summary>
        /// <param name="key">The additional configuration key.</param>
        /// <param name="value">The additional configuration value.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        /// <remarks>Usage will result in a <see cref="TesterBase.ResetHost()"/>.</remarks>
        public TSelf UseAdditionalConfiguration(string key, string? value) => UseAdditionalConfiguration([new KeyValuePair<string, string?>(key, value)]);

        /// <summary>
        /// Resets the underlying host to instantiate a new instance.
        /// </summary>
        /// <param name="resetConfiguredServices">Indicates whether to reset the previously configured services.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        public new TSelf ResetHost(bool resetConfiguredServices = false)
        {
            base.ResetHost(resetConfiguredServices);
            return (TSelf)this;
        }

        /// <summary>
        /// Provides an opportunity to further configure the services before the underlying host is instantiated.
        /// </summary>
        /// <param name="configureServices">A delegate for configuring <see cref="IServiceCollection"/>.</param>
        /// <param name="autoResetHost">Indicates whether to automatically <see cref="ResetHost(bool)"/> (passing <c>false</c>) when configuring the services.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        /// <remarks>This can be called multiple times prior to the underlying host being instantiated. Internally, the <paramref name="configureServices"/> is queued and then played in order when the host is initially instantiated.
        /// Once instantiated, further calls will result in a <see cref="InvalidOperationException"/> unless a <see cref="ResetHost(bool)"/> is performed.</remarks>
        public new TSelf ConfigureServices(Action<IServiceCollection> configureServices, bool autoResetHost = true)
        {
            base.ConfigureServices(configureServices, autoResetHost);
            return (TSelf)this;
        }

        /// <summary>
        /// Replaces (where existing), or adds, a singleton service with the <paramref name="mockHttpClientFactory"/>.
        /// </summary>
        /// <param name="mockHttpClientFactory">The <see cref="Mocking.MockHttpClientFactory"/>.</param>
        /// <param name="autoResetHost">Indicates whether to automatically <see cref="ResetHost(bool)"/> (passing <c>false</c>) when configuring the service.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        public TSelf ReplaceHttpClientFactory(Mocking.MockHttpClientFactory mockHttpClientFactory, bool autoResetHost = true) => ConfigureServices(sc => (mockHttpClientFactory ?? throw new ArgumentNullException(nameof(mockHttpClientFactory))).Replace(sc), autoResetHost);

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
        /// Replaces (where existing), or adds, a singleton service <paramref name="instance"/>. 
        /// </summary>
        /// <typeparam name="TService">The service <see cref="Type"/>.</typeparam>
        /// <param name="instance">The instance value.</param>
        /// <param name="serviceKey">The service key.</param>
        /// <param name="autoResetHost">Indicates whether to automatically <see cref="ResetHost(bool)"/> (passing <c>false</c>) when configuring the service.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        public TSelf ReplaceKeyedSingleton<TService>(TService instance, object? serviceKey, bool autoResetHost = true) where TService : class => ConfigureServices(sc => sc.ReplaceKeyedSingleton(serviceKey, _ => instance), autoResetHost);

        /// <summary>
        /// Replaces (where existing), or adds, a singleton service using an <paramref name="implementationFactory"/>.
        /// </summary>
        /// <typeparam name="TService">The service <see cref="Type"/>.</typeparam>
        /// <param name="implementationFactory">The implementation factory.</param>
        /// <param name="serviceKey">The service key.</param>
        /// <param name="autoResetHost">Indicates whether to automatically <see cref="ResetHost(bool)"/> (passing <c>false</c>) when configuring the service.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        public TSelf ReplaceKeyedSingleton<TService>(object? serviceKey, Func<IServiceProvider, TService> implementationFactory, bool autoResetHost = true) where TService : class => ConfigureServices(sc => sc.ReplaceKeyedSingleton(serviceKey, implementationFactory), autoResetHost);

        /// <summary>
        /// Replaces (where existing), or adds, a singleton service. 
        /// </summary>
        /// <typeparam name="TService">The service <see cref="Type"/>.</typeparam>
        /// <param name="serviceKey">The service key.</param>
        /// <param name="autoResetHost">Indicates whether to automatically <see cref="ResetHost(bool)"/> (passing <c>false</c>) when configuring the service.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        public TSelf ReplaceKeyedSingleton<TService>(object? serviceKey, bool autoResetHost = true) where TService : class => ConfigureServices(sc => sc.ReplaceKeyedSingleton<TService>(serviceKey), autoResetHost);

        /// <summary>
        /// Replaces (where existing), or adds, a singleton service. 
        /// </summary>
        /// <typeparam name="TService">The service <see cref="Type"/>.</typeparam>
        /// <typeparam name="TImplementation">The implementation <see cref="Type"/>.</typeparam>
        /// <param name="serviceKey">The service key.</param>
        /// <param name="autoResetHost">Indicates whether to automatically <see cref="ResetHost(bool)"/> (passing <c>false</c>) when configuring the service.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        public TSelf ReplaceKeyedSingleton<TService, TImplementation>(object? serviceKey, bool autoResetHost = true) where TService : class where TImplementation : class, TService => ConfigureServices(sc => sc.ReplaceKeyedSingleton<TService, TImplementation>(serviceKey), autoResetHost);

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
        /// Replaces (where existing), or adds, a scoped service using an <paramref name="implementationFactory"/>.
        /// </summary>
        /// <typeparam name="TService">The service <see cref="Type"/>.</typeparam>
        /// <param name="serviceKey">The service key.</param>
        /// <param name="implementationFactory">The implementation factory.</param>
        /// <param name="autoResetHost">Indicates whether to automatically <see cref="ResetHost(bool)"/> (passing <c>false</c>) when configuring the service.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        public TSelf ReplaceKeyedScoped<TService>(object? serviceKey, Func<IServiceProvider, object?, TService> implementationFactory, bool autoResetHost = true) where TService : class => ConfigureServices(sc => sc.ReplaceKeyedScoped(serviceKey, implementationFactory), autoResetHost);

        /// <summary>
        /// Replaces (where existing), or adds, a scoped service. 
        /// </summary>
        /// <typeparam name="TService">The service <see cref="Type"/>.</typeparam>
        /// <param name="serviceKey">The service key.</param>
        /// <param name="autoResetHost">Indicates whether to automatically <see cref="ResetHost(bool)"/> (passing <c>false</c>) when configuring the service.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        public TSelf ReplaceKeyedScoped<TService>(object? serviceKey, bool autoResetHost = true) where TService : class => ConfigureServices(sc => sc.ReplaceKeyedScoped<TService>(serviceKey), autoResetHost);

        /// <summary>
        /// Replaces (where existing), or adds, a scoped service. 
        /// </summary>
        /// <typeparam name="TService">The service <see cref="Type"/>.</typeparam>
        /// <typeparam name="TImplementation">The implementation <see cref="Type"/>.</typeparam>
        /// <param name="serviceKey">The service key.</param>
        /// <param name="autoResetHost">Indicates whether to automatically <see cref="ResetHost(bool)"/> (passing <c>false</c>) when configuring the service.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        public TSelf ReplaceKeyedScoped<TService, TImplementation>(object? serviceKey, bool autoResetHost = true) where TService : class where TImplementation : class, TService => ConfigureServices(sc => sc.ReplaceKeyedScoped<TService, TImplementation>(serviceKey), autoResetHost);

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
        /// Replaces (where existing), or adds, a transient service using an <paramref name="implementationFactory"/>.
        /// </summary>
        /// <typeparam name="TService">The service <see cref="Type"/>.</typeparam>
        /// <param name="serviceKey">The service key.</param>
        /// <param name="implementationFactory">The implementation factory.</param>
        /// <param name="autoResetHost">Indicates whether to automatically <see cref="ResetHost(bool)"/> (passing <c>false</c>) when configuring the service.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        public TSelf ReplaceKeyedTransient<TService>(object? serviceKey, Func<IServiceProvider, object?, TService> implementationFactory, bool autoResetHost = true) where TService : class => ConfigureServices(sc => sc.ReplaceKeyedTransient(serviceKey, implementationFactory), autoResetHost);

        /// <summary>
        /// Replaces (where existing), or adds, a transient service. 
        /// </summary>
        /// <typeparam name="TService">The service <see cref="Type"/>.</typeparam>
        /// <param name="serviceKey">The service key.</param>
        /// <param name="autoResetHost">Indicates whether to automatically <see cref="ResetHost(bool)"/> (passing <c>false</c>) when configuring the service.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        public TSelf ReplaceKeyedTransient<TService>(object? serviceKey, bool autoResetHost = true) where TService : class => ConfigureServices(sc => sc.ReplaceKeyedTransient<TService>(serviceKey), autoResetHost);

        /// <summary>
        /// Replaces (where existing), or adds, a transient service. 
        /// </summary>
        /// <typeparam name="TService">The service <see cref="Type"/>.</typeparam>
        /// <typeparam name="TImplementation">The implementation <see cref="Type"/>.</typeparam>
        /// <param name="serviceKey">The service key.</param>
        /// <param name="autoResetHost">Indicates whether to automatically <see cref="ResetHost(bool)"/> (passing <c>false</c>) when configuring the service.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        public TSelf ReplaceKeyedTransient<TService, TImplementation>(object? serviceKey, bool autoResetHost = true) where TService : class where TImplementation : class, TService => ConfigureServices(sc => sc.ReplaceKeyedTransient<TService, TImplementation>(serviceKey), autoResetHost);

        /// <summary>
        /// Delays the execution of the test for the specified <paramref name="duration"/>.
        /// </summary>
        /// <param name="duration">The amount of time to delay the operation. Must be a non-negative <see cref="TimeSpan"/>.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        public TSelf Delay(TimeSpan duration) => Task.Delay(duration).ContinueWith(_ => (TSelf)this).Result;

        /// <summary>
        /// Delays the execution of the test for the specified <paramref name="durationInMilliseconds"/>.
        /// </summary>
        /// <param name="durationInMilliseconds">The amount of time to delay the operation. Must be a non-negative <see cref="int"/>.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        public TSelf Delay(int durationInMilliseconds) => Delay(TimeSpan.FromMilliseconds(durationInMilliseconds));

        /// <summary>
        /// Wraps the host execution to perform required start-up style activities; specifically resetting the <see cref="TestSharedState"/>.
        /// </summary>
        /// <typeparam name="T">The result <see cref="Type"/>.</typeparam>
        /// <param name="result">The function to create the result.</param>
        /// <returns>The <paramref name="result"/>.</returns>
        protected T HostExecutionWrapper<T>(Func<T> result)
        {
            SharedState.Reset();
            return result();
        }
    }
}