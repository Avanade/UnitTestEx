// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Azure.Core.Amqp;
using Azure.Messaging.ServiceBus;
using CoreEx.Azure.ServiceBus;
using CoreEx.Configuration;
using CoreEx.Events;
using CoreEx.Http;
using CoreEx.Mapping.Converters;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Azure.WebJobs.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Mime;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using UnitTestEx.Abstractions;
using UnitTestEx.Hosting;
using Ceh = CoreEx.Http;

namespace UnitTestEx.Functions
{
    /// <summary>
    /// Provides the basic Azure Function unit-testing capabilities.
    /// </summary>
    /// <typeparam name="TEntryPoint">The <see cref="FunctionsStartup"/> <see cref="Type"/>.</typeparam>
    /// <typeparam name="TSelf">The <see cref="FunctionTesterBase{TEntryPoint, TSelf}"/> to support inheriting fluent-style method-chaining.</typeparam>
    public abstract class FunctionTesterBase<TEntryPoint, TSelf> : TesterBase<TSelf>, IDisposable where TEntryPoint : FunctionsStartup, new() where TSelf : FunctionTesterBase<TEntryPoint, TSelf>
    {
        private static readonly object _lock = new();
        private static bool _localSettingsDone = false;

        private readonly bool? _includeUnitTestConfiguration;
        private readonly bool? _includeUserSecrets;
        private readonly IEnumerable<KeyValuePair<string, string?>>? _additionalConfiguration;
        private IHost? _host;
        private bool _disposed;

        private class Fhb : IFunctionsHostBuilder
        {
            public Fhb(IServiceCollection services) => Services = services;

            public IServiceCollection Services { get; }
        }

        private class Fcb : IFunctionsConfigurationBuilder
        {
            public Fcb(IConfigurationBuilder configurationBuilder) => ConfigurationBuilder = configurationBuilder;

            public IConfigurationBuilder ConfigurationBuilder { get; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FunctionTesterBase{TEntryPoint, TSelf}"/> class.
        /// </summary>
        /// <param name="implementor">The <see cref="TestFrameworkImplementor"/>.</param>
        /// <param name="includeUnitTestConfiguration">Indicates whether to include '<c>appsettings.unittest.json</c>' configuration file.</param>
        /// <param name="includeUserSecrets">Indicates whether to include user secrets.</param>
        /// <param name="additionalConfiguration">Additional configuration values to add/override.</param>
        protected FunctionTesterBase(TestFrameworkImplementor implementor, bool? includeUnitTestConfiguration, bool? includeUserSecrets, IEnumerable<KeyValuePair<string, string?>>? additionalConfiguration) : base(implementor)
        {
            Logger = LoggerProvider.CreateLogger(GetType().Name);
            _includeUnitTestConfiguration = includeUnitTestConfiguration;
            _includeUserSecrets = includeUserSecrets;
            _additionalConfiguration = additionalConfiguration;
        }

        /// <summary>
        /// Mock the <see cref="IFunctionsConfigurationBuilder"/> interface.
        /// </summary>
        private static IFunctionsConfigurationBuilder MockIFunctionsConfigurationBuilder(IConfigurationBuilder configurationBuilder)
        {
            var mock = new Mock<IFunctionsConfigurationBuilder>();
            mock.Setup(x => x.ConfigurationBuilder).Returns(configurationBuilder);
            return mock.Object;
        }

        /// <summary>
        /// Mock the <see cref="IFunctionsHostBuilder"/> interface.
        /// </summary>
        private static IFunctionsHostBuilder MockIFunctionsHostBuilder(IServiceCollection services)
        {
            var mock = new Mock<IFunctionsHostBuilder>();
            mock.Setup(x => x.Services).Returns(services);
            return mock.Object;
        }

        /// <summary>
        /// Get the local.settings.json values and store in a temporary file.
        /// </summary>
        private static string GetLocalSettingsJson()
        {
            lock (_lock)
            {
                // Manage a temporary local.settings.json for the values.
                var tfi = new FileInfo(Path.Combine(Environment.CurrentDirectory, "temporary.local.settings.json"));
                if (tfi.Exists)
                {
                    if (_localSettingsDone)
                        return tfi.Name;

                    tfi.Delete();
                }

                // Simulate the loading of the local.settings.json values.
                var fi = new FileInfo(Path.Combine(Environment.CurrentDirectory, "local.settings.json"));
                if (!fi.Exists)
                    return tfi.Name;

                var json = File.ReadAllText(fi.FullName);
                var je = (JsonElement)System.Text.Json.JsonSerializer.Deserialize<dynamic>(json, new JsonSerializerOptions { ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
                if (je.TryGetProperty("Values", out var jv))
                {
                    using var fs = tfi.OpenWrite();
                    using var uw = new Utf8JsonWriter(fs, new JsonWriterOptions { Indented = true });
                    jv.WriteTo(uw);
                    uw.Flush();
                }

                _localSettingsDone = true;
                return tfi.Name;
            }
        }

        /// <summary>
        /// Gets the runtime <see cref="ILogger"/>.
        /// </summary>
        /// <returns>The <see cref="ILogger"/>.</returns>
        public ILogger Logger { get; }

        /// <summary>
        /// Gets the <see cref="IHost"/>.
        /// </summary>
        private IHost GetHost()
        {
            lock (SyncRoot)
            {
                if (_host != null)
                    return _host;

                var ep2 = new TEntryPoint();
                return _host = new HostBuilder()
                    .UseEnvironment(TestSetUp.Environment)
                    .ConfigureLogging((lb) => { lb.SetMinimumLevel(SetUp.MinimumLogLevel); lb.ClearProviders(); lb.AddProvider(LoggerProvider); })
                    .ConfigureWebHostDefaults(c =>
                    {
                        c.ConfigureAppConfiguration((cx, cb) =>
                        {
                            cb.SetBasePath(Environment.CurrentDirectory)
                                .AddInMemoryCollection(new KeyValuePair<string, string?>[] { new KeyValuePair<string, string?>("AzureWebJobsConfigurationSection", "AzureFunctionsJobHost") })
                                .AddJsonFile(GetLocalSettingsJson(), optional: true)
                                .AddJsonFile("appsettings.json", optional: true)
                                .AddJsonFile("appsettings.development.json", optional: true);

                            if ((!_includeUserSecrets.HasValue && TestSetUp.FunctionTesterIncludeUserSecrets) || (_includeUserSecrets.HasValue && _includeUserSecrets.Value))
                                cb.AddUserSecrets<TEntryPoint>();

                            cb.AddEnvironmentVariables();

                            // Apply early so can be reference.
                            if ((!_includeUnitTestConfiguration.HasValue && TestSetUp.FunctionTesterIncludeUnitTestConfiguration) || (_includeUnitTestConfiguration.HasValue && _includeUnitTestConfiguration.Value))
                                cb.AddJsonFile("appsettings.unittest.json", optional: true);

                            if (_additionalConfiguration != null)
                                cb.AddInMemoryCollection(_additionalConfiguration);
                        });
                    })
                    .ConfigureAppConfiguration(cb =>
                    {
                        ep2.ConfigureAppConfiguration(MockIFunctionsConfigurationBuilder(cb));

                        if ((!_includeUserSecrets.HasValue && TestSetUp.FunctionTesterIncludeUserSecrets) || (_includeUserSecrets.HasValue && _includeUserSecrets.Value))
                            cb.AddUserSecrets<TEntryPoint>();

                        // Apply again near the end to ensure override.
                        if ((!_includeUnitTestConfiguration.HasValue && TestSetUp.FunctionTesterIncludeUnitTestConfiguration) || (_includeUnitTestConfiguration.HasValue && _includeUnitTestConfiguration.Value))
                            cb.AddJsonFile("appsettings.unittest.json", optional: true);

                        if (_additionalConfiguration != null)
                            cb.AddInMemoryCollection(_additionalConfiguration);
                    })
                    .ConfigureServices(sc =>
                    {
                        ep2.Configure(MockIFunctionsHostBuilder(sc));
                        sc.ReplaceScoped(_ => SharedState);
                        SetUp.ConfigureServices?.Invoke(sc);
                        if (SetUp.ExpectedEventsEnabled)
                            ReplaceExpectedEventPublisher(sc);

                        AddConfiguredServices(sc);
                    }).Build();
            }
        }

        /// <inheritdoc/>
        protected override void ResetHost() => _host = null;

        /// <summary>
        /// Gets the <see cref="IServiceProvider"/> from the underlying host.
        /// </summary>
        /// <returns>The <see cref="IServiceProvider"/>.</returns>
        public override IServiceProvider Services => GetHost().Services;

        /// <summary>
        /// Gets the <see cref="ILogger"/> for the specified <typeparamref name="TCategoryName"/> from the underlying <see cref="Services"/>.
        /// </summary>
        /// <typeparam name="TCategoryName">The <see cref="Type"/> to infer the category name.</typeparam>
        /// <returns>The <see cref="ILogger{TCategoryName}"/>.</returns>
        public ILogger<TCategoryName> GetLogger<TCategoryName>() => Services.GetRequiredService<ILogger<TCategoryName>>();

        /// <summary>
        /// Gets the <see cref="IConfiguration"/> from the underlying <see cref="Services"/>.
        /// </summary>
        /// <returns>The <see cref="IConfiguration"/>.</returns>
        public override IConfiguration Configuration => Services.GetRequiredService<IConfiguration>();

        /// <summary>
        /// Gets the <see cref="SettingsBase"/> from the underlying host.
        /// </summary>
        public override SettingsBase Settings => Services.GetService<SettingsBase>() ?? new DefaultSettings(Configuration);

        /// <summary>
        /// Specifies the <i>Function</i> <see cref="Type"/> that utilizes the <see cref="Microsoft.Azure.WebJobs.HttpTriggerAttribute"/> that is to be tested.
        /// </summary>
        /// <typeparam name="TFunction">The Function <see cref="Type"/> that utilizes the <see cref="Microsoft.Azure.WebJobs.HttpTriggerAttribute"/> to be tested.</typeparam>
        /// <returns>The <see cref="HttpTriggerTester{TFunction}"/>.</returns>
        public HttpTriggerTester<TFunction> HttpTrigger<TFunction>() where TFunction : class => new(this, HostExecutionWrapper(() => GetHost().Services.CreateScope()));

        /// <summary>
        /// Specifies the <see cref="Type"/> of <typeparamref name="T"/> that is to be tested.
        /// </summary>
        /// <typeparam name="T">The <see cref="Type"/> to be tested.</typeparam>
        /// <returns>The <see cref="TypeTester{TFunction}"/>.</returns>
        public TypeTester<T> Type<T>() where T : class => new(this, HostExecutionWrapper(() => GetHost().Services.CreateScope()));

        /// <summary>
        /// Specifies the <i>Function</i> <see cref="Type"/> that utilizes the <see cref="Microsoft.Azure.WebJobs.ServiceBusTriggerAttribute"/> that is to be tested.
        /// </summary>
        /// <typeparam name="TFunction">The Function <see cref="Type"/> that utilizes the <see cref="Microsoft.Azure.WebJobs.ServiceBusTriggerAttribute"/> to be tested.</typeparam>
        /// <returns>The <see cref="ServiceBusTriggerTester{TFunction}"/>.</returns>
        public ServiceBusTriggerTester<TFunction> ServiceBusTrigger<TFunction>() where TFunction : class => new(this, HostExecutionWrapper(() => GetHost().Services.CreateScope()));

        /// <summary>
        /// Creates a new <see cref="HttpRequest"/> with no body.
        /// </summary>
        /// <param name="httpMethod">The <see cref="HttpMethod"/>.</param>
        /// <param name="requestUri">The requuest uri.</param>
        /// <returns>The <see cref="HttpRequest"/>.</returns>
        public HttpRequest CreateHttpRequest(HttpMethod httpMethod, string? requestUri = null)
            => CreateHttpRequestInternal(httpMethod, requestUri, false, null, null, null);

        /// <summary>
        /// Creates a new <see cref="HttpRequest"/> with no body.
        /// </summary>
        /// <param name="httpMethod">The <see cref="HttpMethod"/>.</param>
        /// <param name="requestUri">The requuest uri.</param>
        /// <param name="requestOptions">The optional <see cref="Ceh.HttpRequestOptions"/>.</param>
        /// <returns>The <see cref="HttpRequest"/>.</returns>
        public HttpRequest CreateHttpRequest(HttpMethod httpMethod, string? requestUri, Ceh.HttpRequestOptions? requestOptions = null)
            => CreateHttpRequestInternal(httpMethod, requestUri, false, null, requestOptions, null);

        /// <summary>
        /// Creates a new <see cref="HttpRequest"/> with <i>optional</i> <paramref name="body"/> (defaults <see cref="HttpRequest.ContentType"/> to <see cref="MediaTypeNames.Text.Plain"/>).
        /// </summary>
        /// <param name="httpMethod">The <see cref="HttpMethod"/>.</param>
        /// <param name="requestUri">The requuest uri.</param>
        /// <param name="body">The optional body content.</param>
        /// <returns>The <see cref="HttpRequest"/>.</returns>
        public HttpRequest CreateHttpRequest(HttpMethod httpMethod, string? requestUri, string? body)
            => CreateHttpRequestInternal(httpMethod, requestUri, true, body, null, null);

        /// <summary>
        /// Creates a new <see cref="HttpRequest"/> with <i>optional</i> <paramref name="body"/> (defaults <see cref="HttpRequest.ContentType"/> to <see cref="MediaTypeNames.Text.Plain"/>).
        /// </summary>
        /// <param name="httpMethod">The <see cref="HttpMethod"/>.</param>
        /// <param name="requestUri">The requuest uri.</param>
        /// <param name="body">The optional body content.</param>
        /// <param name="contentType">The content type. Defaults to <see cref="MediaTypeNames.Text.Plain"/>.</param>
        /// <returns>The <see cref="HttpRequest"/>.</returns>
        public HttpRequest CreateHttpRequest(HttpMethod httpMethod, string? requestUri, string? body, string? contentType = MediaTypeNames.Text.Plain)
            => CreateHttpRequestInternal(httpMethod, requestUri, true, body, null, contentType);

        /// <summary>
        /// Creates a new <see cref="HttpRequest"/> with <i>optional</i> <paramref name="body"/> (defaults <see cref="HttpRequest.ContentType"/> to <see cref="MediaTypeNames.Text.Plain"/>).
        /// </summary>
        /// <param name="httpMethod">The <see cref="HttpMethod"/>.</param>
        /// <param name="requestUri">The requuest uri.</param>
        /// <param name="body">The optional body content.</param>
        /// <param name="requestOptions">The optional <see cref="Ceh.HttpRequestOptions"/>.</param>
        /// <param name="contentType">The content type. Defaults to <see cref="MediaTypeNames.Text.Plain"/>.</param>
        /// <returns>The <see cref="HttpRequest"/>.</returns>
        public HttpRequest CreateHttpRequest(HttpMethod httpMethod, string? requestUri, string? body, Ceh.HttpRequestOptions? requestOptions, string? contentType = MediaTypeNames.Text.Plain)
            => CreateHttpRequestInternal(httpMethod, requestUri, true, body, requestOptions, contentType);

        /// <summary>
        /// Creates a new <see cref="HttpRequest"/> based on the supplied parameters.
        /// </summary>
        private HttpRequest CreateHttpRequestInternal(HttpMethod httpMethod, string? requestUri, bool hasBody, string? body, Ceh.HttpRequestOptions? requestOptions, string? contentType = MediaTypeNames.Text.Plain)
        {
            if (httpMethod == HttpMethod.Get && body != null)
                LoggerProvider.CreateLogger("FunctionTesterBase").LogWarning("A payload within a GET request message has no defined semantics; sending a payload body on a GET request might cause some existing implementations to reject the request (see https://www.rfc-editor.org/rfc/rfc7231).");

            var context = new DefaultHttpContext();

            var uri = new Uri(requestUri!, UriKind.RelativeOrAbsolute);
            if (!uri.IsAbsoluteUri)
                uri = new Uri($"http://functiontest{(requestUri != null && requestUri.StartsWith('/') ? requestUri : $"/{requestUri}")}");

            context.Request.Method = httpMethod?.Method ?? HttpMethod.Get.Method;
            context.Request.Scheme = uri.Scheme;
            context.Request.Host = new HostString(uri.Host);
            context.Request.Path = uri.LocalPath;

            // Extend the query string to include additional options.
            var qs = new QueryString(uri.Query);
            if (requestOptions != null)
                qs = requestOptions.AddToQueryString(qs);

            context.Request.QueryString = qs;
            context.Request.ApplyETag(requestOptions?.ETag);

            if (hasBody)
            {
                context.Request.Body = new MemoryStream(body == null ? Array.Empty<byte>() : Encoding.UTF8.GetBytes(body));
                context.Request.ContentType = contentType ?? MediaTypeNames.Text.Plain;
            }

            if (SetUp.OnBeforeHttpRequestSendAsync != null)
                SetUp.OnBeforeHttpRequestSendAsync(context.Request, UserName, CancellationToken.None).GetAwaiter().GetResult();

            return context.Request;
        }

        /// <summary>
        /// Creates a new <see cref="HttpRequest"/> with the <paramref name="value"/> JSON serialized as <see cref="HttpRequest.ContentType"/> of <see cref="MediaTypeNames.Application.Json"/>.
        /// </summary>
        /// <param name="httpMethod">The <see cref="HttpMethod"/>.</param>
        /// <param name="requestUri">The requuest uri.</param>
        /// <param name="value">The value to JSON serialize.</param>
        /// <returns>The <see cref="HttpRequest"/>.</returns>
        public HttpRequest CreateJsonHttpRequest(HttpMethod httpMethod, string? requestUri, object value)
            => CreateJsonHttpRequest(httpMethod, requestUri, value, null);

        /// <summary>
        /// Creates a new <see cref="HttpRequest"/> with the <paramref name="value"/> JSON serialized as <see cref="HttpRequest.ContentType"/> of <see cref="MediaTypeNames.Application.Json"/>.
        /// </summary>
        /// <param name="httpMethod">The <see cref="HttpMethod"/>.</param>
        /// <param name="requestUri">The requuest uri.</param>
        /// <param name="value">The value to JSON serialize.</param>
        /// <param name="requestOptions">The optional <see cref="Ceh.HttpRequestOptions"/>.</param>
        /// <returns>The <see cref="HttpRequest"/>.</returns>
        public HttpRequest CreateJsonHttpRequest(HttpMethod httpMethod, string? requestUri, object value, Ceh.HttpRequestOptions? requestOptions = null)
        {
            if (httpMethod == HttpMethod.Get)
                LoggerProvider.CreateLogger("FunctionTesterBase").LogWarning("A payload within a GET request message has no defined semantics; sending a payload body on a GET request might cause some existing implementations to reject the request (see https://www.rfc-editor.org/rfc/rfc7231).");

            var hr = CreateHttpRequest(httpMethod, requestUri, requestOptions);
            hr.Body = new MemoryStream(JsonSerializer.SerializeToBinaryData(value).ToArray());
            hr.ContentType = MediaTypeNames.Application.Json;
            return hr;
        }

        /// <summary>
        /// Creates a new <see cref="HttpRequest"/> using the JSON formatted embedded resource as the content (<see cref="MediaTypeNames.Application.Json"/>).
        /// </summary>
        /// <typeparam name="TAssembly">The <see cref="Type"/> to infer <see cref="Type.Assembly"/> for the embedded resources.</typeparam>
        /// <param name="httpMethod">The <see cref="HttpMethod"/>.</param>
        /// <param name="requestUri">The requuest uri.</param>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualifed resource name).</param>
        /// <returns>The <see cref="HttpRequest"/>.</returns>
        public HttpRequest CreateJsonHttpRequestFromResource<TAssembly>(HttpMethod httpMethod, string? requestUri, string resourceName)
            => CreateJsonHttpRequestFromResource(httpMethod, requestUri, resourceName, typeof(TAssembly).Assembly, null);

        /// <summary>
        /// Creates a new <see cref="HttpRequest"/> using the JSON formatted embedded resource as the content (<see cref="MediaTypeNames.Application.Json"/>).
        /// </summary>
        /// <typeparam name="TAssembly">The <see cref="Type"/> to infer <see cref="Type.Assembly"/> for the embedded resources.</typeparam>
        /// <param name="httpMethod">The <see cref="HttpMethod"/>.</param>
        /// <param name="requestUri">The requuest uri.</param>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualifed resource name).</param>
        /// <param name="requestOptions">The optional <see cref="Ceh.HttpRequestOptions"/>.</param>
        /// <returns>The <see cref="HttpRequest"/>.</returns>
        public HttpRequest CreateJsonHttpRequestFromResource<TAssembly>(HttpMethod httpMethod, string? requestUri, string resourceName, Ceh.HttpRequestOptions? requestOptions = null)
            => CreateJsonHttpRequestFromResource(httpMethod, requestUri, resourceName, typeof(TAssembly).Assembly, requestOptions);

        /// <summary>
        /// Creates a new <see cref="HttpRequest"/> using the JSON formatted embedded resource as the content (<see cref="MediaTypeNames.Application.Json"/>).
        /// </summary>
        /// <param name="httpMethod">The <see cref="HttpMethod"/>.</param>
        /// <param name="requestUri">The requuest uri.</param>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualifed resource name).</param>
        /// <param name="assembly">The <see cref="Assembly"/> that contains the embedded resource; defaults to <see cref="Assembly.GetEntryAssembly()"/>.</param>
        /// <returns>The <see cref="HttpRequest"/>.</returns>
        public HttpRequest CreateJsonHttpRequestFromResource(HttpMethod httpMethod, string? requestUri, string resourceName, Assembly assembly)
            => CreateJsonHttpRequestFromResource(httpMethod, requestUri, resourceName, assembly, null);

        /// <summary>
        /// Creates a new <see cref="HttpRequest"/> using the JSON formatted embedded resource as the content (<see cref="MediaTypeNames.Application.Json"/>).
        /// </summary>
        /// <param name="httpMethod">The <see cref="HttpMethod"/>.</param>
        /// <param name="requestUri">The requuest uri.</param>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualifed resource name).</param>
        /// <param name="assembly">The <see cref="Assembly"/> that contains the embedded resource; defaults to <see cref="Assembly.GetEntryAssembly()"/>.</param>
        /// <param name="requestOptions">The optional <see cref="Ceh.HttpRequestOptions"/>.</param>
        /// <returns>The <see cref="HttpRequest"/>.</returns>
        public HttpRequest CreateJsonHttpRequestFromResource(HttpMethod httpMethod, string? requestUri, string resourceName, Assembly assembly, Ceh.HttpRequestOptions? requestOptions = null)
        {
            if (httpMethod == HttpMethod.Get)
                LoggerProvider.CreateLogger("FunctionTesterBase").LogWarning("A payload within a GET request message has no defined semantics; sending a payload body on a GET request might cause some existing implementations to reject the request (see https://www.rfc-editor.org/rfc/rfc7231).");

            var hr = CreateHttpRequest(httpMethod, requestUri, requestOptions);
            hr.Body = new MemoryStream(Encoding.UTF8.GetBytes(Resource.GetJson(resourceName, assembly ?? Assembly.GetCallingAssembly())));
            hr.ContentType = MediaTypeNames.Application.Json;
            return hr;
        }

        /// <summary>
        /// Creates a <see cref="ServiceBusReceivedMessage"/> where the <see cref="ServiceBusMessage.Body"/> <see cref="BinaryData"/> will contain the <paramref name="value"/> as serialized JSON.
        /// </summary>
        /// <typeparam name="T">The <paramref name="value"/> <see cref="Type"/>.</typeparam>
        /// <param name="value">The value.</param>
        /// <returns>The <see cref="ServiceBusReceivedMessage"/>.</returns>
        public ServiceBusReceivedMessage CreateServiceBusMessage<T>(T value) => (value is EventData ed) ? CreateServiceBusMessage(ed) : CreateServiceBusMessageFromJson(JsonSerializer.Serialize(value));

        /// <summary>
        /// Creates a <see cref="ServiceBusReceivedMessage"/> where the <see cref="ServiceBusMessage.Body"/> <see cref="BinaryData"/> will contain the <paramref name="value"/> as serialized JSON.
        /// </summary>
        /// <typeparam name="T">The <paramref name="value"/> <see cref="Type"/>.</typeparam>
        /// <param name="value">The value.</param>
        /// <param name="messageModify">Optional <see cref="AmqpAnnotatedMessage"/> modifier than enables the message to be further configured.</param>
        /// <returns>The <see cref="ServiceBusReceivedMessage"/>.</returns>
        public ServiceBusReceivedMessage CreateServiceBusMessage<T>(T value, Action<AmqpAnnotatedMessage>? messageModify = null)
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
        /// Creates a <see cref="ServiceBusReceivedMessage"/> from the <paramref name="event"/> leveraging the <see cref="EventDataToServiceBusConverter"/> to perform the underlying conversion.
        /// </summary>
        /// <param name="event">The <see cref="EventData"/> or <see cref="EventData{T}"/> value.</param>
        /// <returns>The <see cref="ServiceBusReceivedMessage"/>.</returns>
        /// <remarks>Attempts to use the configured <see cref="EventDataToServiceBusConverter"/> by leveraging the underlying host <see cref="FunctionTesterBase{TEntryPoint, TSelf}.Services"/> where found; otherwise, will instantiate as new. As accessing
        /// the <see cref="FunctionTesterBase{TEntryPoint, TSelf}.Services"/> will result in the underlying host being instantiated a corresponding <see cref="ResetHost"/> will be performed to enable further configuration.</remarks>
        public ServiceBusReceivedMessage CreateServiceBusMessage(EventData @event) => CreateServiceBusMessage(@event, null);

        /// <summary>
        /// Creates a <see cref="ServiceBusReceivedMessage"/> from the <paramref name="event"/> leveraging the <see cref="EventDataToServiceBusConverter"/> to perform the underlying conversion.
        /// </summary>
        /// <param name="event">The <see cref="EventData"/> or <see cref="EventData{T}"/> value.</param>
        /// <param name="messageModify">Optional <see cref="AmqpAnnotatedMessage"/> modifier than enables the message to be further configured.</param>
        /// <returns>The <see cref="ServiceBusReceivedMessage"/>.</returns>
        /// <remarks>Attempts to use the configured <see cref="EventDataToServiceBusConverter"/> by leveraging the underlying host <see cref="FunctionTesterBase{TEntryPoint, TSelf}.Services"/> where found; otherwise, will instantiate as new. As accessing
        /// the <see cref="FunctionTesterBase{TEntryPoint, TSelf}.Services"/> will result in the underlying host being instantiated a corresponding <see cref="ResetHost"/> will be performed to enable further configuration.</remarks>
        public ServiceBusReceivedMessage CreateServiceBusMessage(EventData @event, Action<AmqpAnnotatedMessage>? messageModify)
        {
            if (@event == null) throw new ArgumentNullException(nameof(@event));
            var message = (Services.GetService<EventDataToServiceBusConverter>() ?? new EventDataToServiceBusConverter(Services.GetService<IEventSerializer>(), Services.GetService<IValueConverter<EventSendData, ServiceBusMessage>>())).Convert(@event).GetRawAmqpMessage();
            ResetHost(false);
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
            var c = t.GetConstructor(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null, new Type[] { typeof(AmqpAnnotatedMessage) }, null);
            return c == null
                ? throw new InvalidOperationException($"'{typeof(ServiceBusReceivedMessage).Name}' constructor that accepts Type '{typeof(AmqpAnnotatedMessage).Name}' parameter was not found.")
                : (ServiceBusReceivedMessage)c.Invoke(new object?[] { message });
        }

        /// <summary>
        /// Creates a <see cref="ServiceBusMessageActionsAssertor"/> as the <see cref="ServiceBusMessageActions"/> instance to enable test mock and assert verification.
        /// </summary>
        /// <returns>The <see cref="ServiceBusMessageActionsAssertor"/>.</returns>
        public ServiceBusMessageActionsAssertor CreateServiceBusMessageActions() => new(Implementor);

        /// <summary>
        /// Creates a <see cref="ServiceBusSessionMessageActionsAssertor"/> as the <see cref="ServiceBusSessionMessageActions"/> instance to enable test mock and assert verification.
        /// </summary>
        /// <param name="sessionLockedUntil">The sessions locked until <see cref="DateTimeOffset"/>; defaults to <see cref="DateTimeOffset.UtcNow"/> plus five minutes.</param>
        /// <param name="sessionState">The session state <see cref="BinaryData"/>; defaults to <see cref="BinaryData.Empty"/>.</param>
        /// <returns>The <see cref="ServiceBusSessionMessageActionsAssertor"/>.</returns>
        public ServiceBusSessionMessageActionsAssertor CreateServiceBusSessionMessageActions(DateTimeOffset? sessionLockedUntil = default, BinaryData? sessionState = default) => new(Implementor, sessionLockedUntil, sessionState);

        /// <summary>
        /// Releases all resources.
        /// </summary>
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

            if (_host != null)
            {
                _host.Dispose();
                _host = null;
            }

            _disposed = true;
        }
    }
}