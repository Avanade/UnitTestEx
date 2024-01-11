// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Azure.Core.Amqp;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.WebJobs.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System;
using System.Net.Mime;
using System.Reflection;
using System.Text;
using UnitTestEx.Functions;
using UnitTestEx.Json;
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
        protected T HostExecutionWrapper<T>(Func<T> result)
        {
            TestSetUp.LogAutoSetUpOutputs(Implementor);
            SharedState.Reset();
            return result();
        }

        #region ServiceBus

        /// <summary>
        /// Creates a <see cref="ServiceBusReceivedMessage"/> where the <see cref="ServiceBusMessage.Body"/> <see cref="BinaryData"/> will contain the <paramref name="value"/> as serialized JSON.
        /// </summary>
        /// <typeparam name="T">The <paramref name="value"/> <see cref="Type"/>.</typeparam>
        /// <param name="value">The value.</param>
        /// <returns>The <see cref="ServiceBusReceivedMessage"/>.</returns>
        public ServiceBusReceivedMessage CreateServiceBusMessageFromValue<T>(T value) => CreateServiceBusMessageFromJson(JsonSerializer.Serialize(value));

        /// <summary>
        /// Creates a <see cref="ServiceBusReceivedMessage"/> where the <see cref="ServiceBusMessage.Body"/> <see cref="BinaryData"/> will contain the <paramref name="value"/> as serialized JSON.
        /// </summary>
        /// <typeparam name="T">The <paramref name="value"/> <see cref="Type"/>.</typeparam>
        /// <param name="value">The value.</param>
        /// <param name="messageModify">Optional <see cref="AmqpAnnotatedMessage"/> modifier than enables the message to be further configured.</param>
        /// <returns>The <see cref="ServiceBusReceivedMessage"/>.</returns>
        public ServiceBusReceivedMessage CreateServiceBusMessageFromValue<T>(T value, Action<AmqpAnnotatedMessage>? messageModify = null)
            => CreateServiceBusMessageFromJson(JsonSerializer.Serialize(value), messageModify);

        /// <summary>
        /// Creates a <see cref="ServiceBusReceivedMessage"/> where the <see cref="ServiceBusMessage.Body"/> <see cref="BinaryData"/> will contain the JSON formatted embedded resource as the content (<see cref="MediaTypeNames.Application.Json"/>).
        /// </summary>
        /// <typeparam name="TAssembly">The <see cref="Type"/> to infer <see cref="Type.Assembly"/> for the embedded resources.</typeparam>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualifed resource name).</param>
        /// <param name="messageModify">Optional <see cref="AmqpAnnotatedMessage"/> modifier than enables the message to be further configured.</param>
        /// <returns>The <see cref="ServiceBusReceivedMessage"/>.</returns>
        public ServiceBusReceivedMessage CreateServiceBusMessageFromResource<TAssembly>(string resourceName, Action<AmqpAnnotatedMessage>? messageModify = null)
            => CreateServiceBusMessageFromResource(resourceName, messageModify, typeof(TAssembly).Assembly);

        /// <summary>
        /// Creates a <see cref="ServiceBusReceivedMessage"/> where the <see cref="ServiceBusMessage.Body"/> <see cref="BinaryData"/> will contain the JSON formatted embedded resource as the content (<see cref="MediaTypeNames.Application.Json"/>).
        /// </summary>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualifed resource name).</param>
        /// <param name="messageModify">Optional <see cref="AmqpAnnotatedMessage"/> modifier than enables the message to be further configured.</param>
        /// <param name="assembly">The <see cref="Assembly"/> that contains the embedded resource; defaults to <see cref="Assembly.GetEntryAssembly()"/>.</param>
        /// <returns>The <see cref="ServiceBusReceivedMessage"/>.</returns>
        public ServiceBusReceivedMessage CreateServiceBusMessageFromResource(string resourceName, Action<AmqpAnnotatedMessage>? messageModify = null, Assembly? assembly = null)
            => CreateServiceBusMessageFromJson(Resource.GetJson(resourceName, assembly ?? Assembly.GetCallingAssembly()), messageModify);

        /// <summary>
        /// Creates a <see cref="ServiceBusReceivedMessage"/> where the <see cref="ServiceBusMessage.Body"/> <see cref="BinaryData"/> will contain the serialized <paramref name="json"/>.
        /// </summary>
        /// <param name="json">The JSON body.</param>
        /// <param name="messageModify">Optional <see cref="AmqpAnnotatedMessage"/> modifier than enables the message to be further configured.</param>
        /// <returns>The <see cref="ServiceBusReceivedMessage"/>.</returns>
        public ServiceBusReceivedMessage CreateServiceBusMessageFromJson(string json, Action<AmqpAnnotatedMessage>? messageModify = null)
        {
            var message = new AmqpAnnotatedMessage(AmqpMessageBody.FromData(new ReadOnlyMemory<byte>[] { Encoding.UTF8.GetBytes(json ?? throw new ArgumentNullException(nameof(json))) }));
            message.Properties.ContentType = MediaTypeNames.Application.Json;
            message.Properties.MessageId = new AmqpMessageId(Guid.NewGuid().ToString());
            return CreateServiceBusMessage(message, messageModify);
        }

        /// <summary>
        /// Creates a <see cref="ServiceBusReceivedMessage"/> from the <paramref name="message"/>.
        /// </summary>
        /// <param name="message">The <see cref="ServiceBusMessage"/>.</param>
        /// <returns>The <see cref="ServiceBusReceivedMessage"/>.</returns>
        public ServiceBusReceivedMessage CreateServiceBusMessage(ServiceBusMessage message)
            => CreateServiceBusMessage((message ?? throw new ArgumentNullException(nameof(message))).GetRawAmqpMessage(), null);

        /// <summary>
        /// Creates a <see cref="ServiceBusReceivedMessage"/> from the <paramref name="message"/>.
        /// </summary>
        /// <param name="message">The <see cref="ServiceBusMessage"/>.</param>
        /// <param name="messageModify">Optional <see cref="AmqpAnnotatedMessage"/> modifier than enables the message to be further configured.</param>
        /// <returns>The <see cref="ServiceBusReceivedMessage"/>.</returns>
        public ServiceBusReceivedMessage CreateServiceBusMessage(ServiceBusMessage message, Action<AmqpAnnotatedMessage>? messageModify)
            => CreateServiceBusMessage((message ?? throw new ArgumentNullException(nameof(message))).GetRawAmqpMessage(), messageModify);

        /// <summary>
        /// Creates a <see cref="ServiceBusReceivedMessage"/> from the <paramref name="message"/>.
        /// </summary>
        /// <param name="message">The <see cref="AmqpAnnotatedMessage"/>.</param>
        /// <returns>The <see cref="ServiceBusReceivedMessage"/>.</returns>
        public ServiceBusReceivedMessage CreateServiceBusMessage(AmqpAnnotatedMessage message) => CreateServiceBusMessage(message, null);

        /// <summary>
        /// Creates a <see cref="ServiceBusReceivedMessage"/> from the <paramref name="message"/>.
        /// </summary>
        /// <param name="message">The <see cref="AmqpAnnotatedMessage"/>.</param>
        /// <param name="messageModify">Optional <see cref="AmqpAnnotatedMessage"/> modifier than enables the message to be further configured.</param>
        /// <returns>The <see cref="ServiceBusReceivedMessage"/>.</returns>
        public ServiceBusReceivedMessage CreateServiceBusMessage(AmqpAnnotatedMessage message, Action<AmqpAnnotatedMessage>? messageModify)
        {
            message.Header.DeliveryCount ??= 1;
            message.Header.Durable ??= true;
            message.Header.Priority ??= 1;
            message.Header.TimeToLive ??= TimeSpan.FromSeconds(60);

            messageModify?.Invoke(message);

            var t = typeof(ServiceBusReceivedMessage);
            var c = t.GetConstructor(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null, [typeof(AmqpAnnotatedMessage)], null);
            return c == null
                ? throw new InvalidOperationException($"'{typeof(ServiceBusReceivedMessage).Name}' constructor that accepts Type '{typeof(AmqpAnnotatedMessage).Name}' parameter was not found.")
                : (ServiceBusReceivedMessage)c.Invoke([message]);
        }

        /// <summary>
        /// Creates a <see cref="WebJobsServiceBusMessageActionsAssertor"/> as the <see cref="ServiceBusMessageActions"/> instance to enable test mock and assert verification.
        /// </summary>
        /// <returns>The <see cref="WebJobsServiceBusMessageActionsAssertor"/>.</returns>
        [Obsolete("Please use either CreateWebJobsServiceBusMessageActions (existing behavior) or CreateWorkerServiceBusMessageActions as required. This method will be deprecated in a future release.", false)]
        public WebJobsServiceBusMessageActionsAssertor CreateServiceBusMessageActions() => new(Implementor);

        /// <summary>
        /// Creates a <see cref="WebJobsServiceBusMessageActionsAssertor"/> as the <see cref="ServiceBusMessageActions"/> instance to enable test mock and assert verification.
        /// </summary>
        /// <returns>The <see cref="WebJobsServiceBusMessageActionsAssertor"/>.</returns>
        public WebJobsServiceBusMessageActionsAssertor CreateWebJobsServiceBusMessageActions() => new(Implementor);

        /// <summary>
        /// Creates a <see cref="WebJobsServiceBusSessionMessageActionsAssertor"/> as the <see cref="ServiceBusSessionMessageActions"/> instance to enable test mock and assert verification.
        /// </summary>
        /// <param name="sessionLockedUntil">The sessions locked until <see cref="DateTimeOffset"/>; defaults to <see cref="DateTimeOffset.UtcNow"/> plus five minutes.</param>
        /// <param name="sessionState">The session state <see cref="BinaryData"/>; defaults to <see cref="BinaryData.Empty"/>.</param>
        /// <returns>The <see cref="WebJobsServiceBusSessionMessageActionsAssertor"/>.</returns>
        [Obsolete("Please use CreateServiceBusSessionMessageActions (existing behavior). This method will be deprecated in a future release.", false)]
        public WebJobsServiceBusSessionMessageActionsAssertor CreateServiceBusSessionMessageActions(DateTimeOffset? sessionLockedUntil = default, BinaryData? sessionState = default) => new(Implementor, sessionLockedUntil, sessionState);

        /// <summary>
        /// Creates a <see cref="WebJobsServiceBusSessionMessageActionsAssertor"/> as the <see cref="ServiceBusSessionMessageActions"/> instance to enable test mock and assert verification.
        /// </summary>
        /// <param name="sessionLockedUntil">The sessions locked until <see cref="DateTimeOffset"/>; defaults to <see cref="DateTimeOffset.UtcNow"/> plus five minutes.</param>
        /// <param name="sessionState">The session state <see cref="BinaryData"/>; defaults to <see cref="BinaryData.Empty"/>.</param>
        /// <returns>The <see cref="WebJobsServiceBusSessionMessageActionsAssertor"/>.</returns>
        public WebJobsServiceBusSessionMessageActionsAssertor CreateWebJobsServiceBusSessionMessageActions(DateTimeOffset? sessionLockedUntil = default, BinaryData? sessionState = default) => new(Implementor, sessionLockedUntil, sessionState);

        /// <summary>
        /// Creates a <see cref="WorkerServiceBusMessageActionsAssertor"/> as the <see cref="Microsoft.Azure.Functions.Worker.ServiceBusMessageActions"/> instance to enable test mock and assert verification.
        /// </summary>
        /// <returns>The <see cref="WorkerServiceBusMessageActionsAssertor"/>.</returns>
        public WorkerServiceBusMessageActionsAssertor CreateWorkerServiceBusMessageActions() => new(Implementor);

        #endregion
    }
}