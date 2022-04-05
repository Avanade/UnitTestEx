﻿// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Azure.Core.Amqp;
using Azure.Messaging.ServiceBus;
using CoreEx.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Mime;
using System.Reflection;
using System.Text;
using System.Text.Json;
using UnitTestEx.Abstractions;
using UnitTestEx.Hosting;
using Ceh = CoreEx.Http;

namespace UnitTestEx.Functions
{
    /// <summary>
    /// Provides the basic Azure Function unit-testing capabilities.
    /// </summary>
    /// <typeparam name="TEntryPoint">The API startup <see cref="Type"/>.</typeparam>
    /// <typeparam name="TSelf">The <see cref="FunctionTesterBase{TEntryPoint, TSelf}"/> to support inheriting fluent-style method-chaining.</typeparam>
    public abstract class FunctionTesterBase<TEntryPoint, TSelf> : TesterBase<TSelf>, IDisposable where TEntryPoint : FunctionsStartup, new() where TSelf : FunctionTesterBase<TEntryPoint, TSelf>
    {
        private static readonly object _lock = new();
        private static bool _localSettingsDone = false;

        private readonly IHostBuilder _hostBuilder;
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
        protected FunctionTesterBase(TestFrameworkImplementor implementor, bool? includeUnitTestConfiguration, bool? includeUserSecrets, IEnumerable<KeyValuePair<string, string>>? additionalConfiguration) : base(implementor)
        {
            Logger = implementor.CreateLogger(GetType().Name);

            var ep2 = new TEntryPoint();
            _hostBuilder = new HostBuilder()
                .UseEnvironment("Development")
                .ConfigureLogging((lb) => lb.AddProvider(implementor.CreateLoggerProvider()))
                .ConfigureWebHostDefaults(c =>
                {
                    c.ConfigureAppConfiguration((cx, cb) =>
                    {
                        cb.SetBasePath(Environment.CurrentDirectory)
                            .AddInMemoryCollection(new KeyValuePair<string, string>[] { new KeyValuePair<string, string>("AzureWebJobsConfigurationSection", "AzureFunctionsJobHost") })
                            .AddJsonFile(GetLocalSettingsJson(), optional: true)
                            .AddJsonFile("appsettings.json", optional: true)
                            .AddJsonFile("appsettings.development.json", optional: true);

                        if ((!includeUserSecrets.HasValue && FunctionTesterDefaults.IncludeUserSecrets) || (includeUserSecrets.HasValue && includeUserSecrets.Value))
                            cb.AddUserSecrets<TEntryPoint>();

                        cb.AddEnvironmentVariables();

                        if ((!includeUnitTestConfiguration.HasValue && FunctionTesterDefaults.IncludeUnitTestConfiguration) || (includeUnitTestConfiguration.HasValue && includeUnitTestConfiguration.Value))
                            cb.AddJsonFile("appsettings.unittest.json", optional: true);

                        if (additionalConfiguration != null)
                            cb.AddInMemoryCollection(additionalConfiguration);
                    });
                })
                .ConfigureAppConfiguration(configurationBuilder => ep2.ConfigureAppConfiguration(MockIFunctionsConfigurationBuilder(configurationBuilder)))
                .ConfigureServices(sc => ep2.Configure(MockIFunctionsHostBuilder(sc)));
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
        private IHost GetHost() => _host ??= _hostBuilder.Build();

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
        /// Gets the <see cref="IConfiguration"/> from the underlying host.
        /// </summary>
        /// <returns>The <see cref="IConfiguration"/>.</returns>
        public override IConfiguration Configuration => Services.GetRequiredService<IConfiguration>();

        /// <summary>
        /// Provides an opportunity to further configure the services. This can be called multiple times. 
        /// </summary>
        /// <param name="configureServices">A delegate for configuring <see cref="IServiceCollection"/>.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        public override TSelf ConfigureServices(Action<IServiceCollection> configureServices)
        {
            if (_host != null)
                throw new InvalidOperationException($"{nameof(ConfigureServices)} cannot be invoked after a test execution has occured.");

            if (configureServices != null)
                _hostBuilder.ConfigureServices(configureServices);

            return (TSelf)this;
        }

        /// <summary>
        /// Specifies the <i>Function</i> <see cref="Type"/> that utilizes the <see cref="Microsoft.Azure.WebJobs.HttpTriggerAttribute"/> that is to be tested.
        /// </summary>
        /// <typeparam name="TFunction">The Function <see cref="Type"/> that utilizes the <see cref="Microsoft.Azure.WebJobs.HttpTriggerAttribute"/> to be tested.</typeparam>
        /// <returns>The <see cref="HttpTriggerTester{TFunction}"/>.</returns>
        public HttpTriggerTester<TFunction> HttpTrigger<TFunction>() where TFunction : class => new(GetHost().Services.CreateScope(), Implementor, JsonSerializer);

        /// <summary>
        /// Specifies the <see cref="Type"/> of <typeparamref name="T"/> that is to be tested.
        /// </summary>
        /// <typeparam name="T">The <see cref="Type"/> to be tested.</typeparam>
        /// <returns>The <see cref="TypeTester{TFunction}"/>.</returns>
        public TypeTester<T> Type<T>() where T : class => new(GetHost().Services.CreateScope(), Implementor, JsonSerializer);

        /// <summary>
        /// Specifies the <i>Function</i> <see cref="Type"/> that utilizes the <see cref="Microsoft.Azure.WebJobs.ServiceBusTriggerAttribute"/> that is to be tested.
        /// </summary>
        /// <typeparam name="TFunction">The Function <see cref="Type"/> that utilizes the <see cref="Microsoft.Azure.WebJobs.ServiceBusTriggerAttribute"/> to be tested.</typeparam>
        /// <returns>The <see cref="ServiceBusTriggerTester{TFunction}"/>.</returns>
        public ServiceBusTriggerTester<TFunction> ServiceBusTrigger<TFunction>() where TFunction : class => new(GetHost().Services.CreateScope(), Implementor, JsonSerializer);

        /// <summary>
        /// Creates a new <see cref="HttpRequest"/> with no body.
        /// </summary>
        /// <param name="httpMethod">The <see cref="HttpMethod"/>.</param>
        /// <param name="requestUri">The requuest uri.</param>
        /// <returns>The <see cref="HttpRequest"/>.</returns>
        public HttpRequest CreateHttpRequest(HttpMethod httpMethod, string? requestUri = null) 
            => CreateHttpRequest(httpMethod, requestUri, (string?)null, null);

        /// <summary>
        /// Creates a new <see cref="HttpRequest"/> with no body.
        /// </summary>
        /// <param name="httpMethod">The <see cref="HttpMethod"/>.</param>
        /// <param name="requestUri">The requuest uri.</param>
        /// <param name="requestOptions">The optional <see cref="Ceh.HttpRequestOptions"/>.</param>
        /// <param name="args">Zero or more <see cref="IHttpArg"/> objects for <paramref name="requestUri"/> templating, query string additions, and content body specification.</param>
        /// <returns>The <see cref="HttpRequest"/>.</returns>
        public HttpRequest CreateHttpRequest(HttpMethod httpMethod, string? requestUri = null, Ceh.HttpRequestOptions? requestOptions = null, params IHttpArg[] args)
            => CreateHttpRequest(httpMethod, requestUri, null, requestOptions, args);

        /// <summary>
        /// Creates a new <see cref="HttpRequest"/> with <i>optional</i> <paramref name="body"/> as <see cref="HttpRequest.ContentType"/> of <see cref="MediaTypeNames.Text.Plain"/>.
        /// </summary>
        /// <param name="httpMethod">The <see cref="HttpMethod"/>.</param>
        /// <param name="requestUri">The requuest uri.</param>
        /// <param name="body">The optional body content.</param>
        /// <returns>The <see cref="HttpRequest"/>.</returns>
        public HttpRequest CreateHttpRequest(HttpMethod httpMethod, string? requestUri = null, string? body = null)
            => CreateHttpRequest(httpMethod, requestUri, body, null);

        /// <summary>
        /// Creates a new <see cref="HttpRequest"/> with <i>optional</i> <paramref name="body"/> as <see cref="HttpRequest.ContentType"/> of <see cref="MediaTypeNames.Text.Plain"/>.
        /// </summary>
        /// <param name="httpMethod">The <see cref="HttpMethod"/>.</param>
        /// <param name="requestUri">The requuest uri.</param>
        /// <param name="body">The optional body content.</param>
        /// <param name="requestOptions">The optional <see cref="Ceh.HttpRequestOptions"/>.</param>
        /// <param name="args">Zero or more <see cref="IHttpArg"/> objects for <paramref name="requestUri"/> templating, query string additions, and content body specification.</param>
        /// <returns>The <see cref="HttpRequest"/>.</returns>
        public HttpRequest CreateHttpRequest(HttpMethod httpMethod, string? requestUri = null, string? body = null, Ceh.HttpRequestOptions? requestOptions = null, params IHttpArg[] args)
        {
            if (httpMethod == HttpMethod.Get && body != null)
                Implementor.CreateLogger("FunctionTesterBase").LogWarning("A payload within a GET request message has no defined semantics; sending a payload body on a GET request might cause some existing implementations to reject the request (see https://www.rfc-editor.org/rfc/rfc7231).");

            var context = new DefaultHttpContext();

            var uri = new Uri(requestUri!, UriKind.RelativeOrAbsolute);
            if (!uri.IsAbsoluteUri)
                uri = new Uri($"http://functiontest{(requestUri != null && requestUri.StartsWith('/') ? requestUri : $"/{requestUri}")}");

            context.Request.Method = httpMethod?.Method ?? HttpMethod.Get.Method;
            context.Request.Scheme = uri.Scheme;
            context.Request.Host = new HostString(uri.Host);
            context.Request.Path = uri.LocalPath;

            // Extend the query string from the IHttpArgs.
            var qs = new QueryString(uri.Query);
            foreach (var arg in (args ??= Array.Empty<IHttpArg>()).Where(x => x != null))
            {
                qs = arg.AddToQueryString(qs, JsonSerializer);
            }

            // Extend the query string to include additional options.
            if (requestOptions != null)
                qs = requestOptions.AddToQueryString(qs);

            context.Request.QueryString = qs;

            if (body != null)
            {
                context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
                context.Request.ContentType = MediaTypeNames.Text.Plain;
            }

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
        /// <param name="args">Zero or more <see cref="IHttpArg"/> objects for <paramref name="requestUri"/> templating, query string additions, and content body specification.</param>
        /// <returns>The <see cref="HttpRequest"/>.</returns>
        public HttpRequest CreateJsonHttpRequest(HttpMethod httpMethod, string? requestUri, object value, Ceh.HttpRequestOptions? requestOptions = null, params IHttpArg[] args)
        {
            if (httpMethod == HttpMethod.Get)
                Implementor.CreateLogger("FunctionTesterBase").LogWarning("A payload within a GET request message has no defined semantics; sending a payload body on a GET request might cause some existing implementations to reject the request (see https://www.rfc-editor.org/rfc/rfc7231).");

            var hr = CreateHttpRequest(httpMethod, requestUri, requestOptions, args);
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
        /// <param name="args">Zero or more <see cref="IHttpArg"/> objects for <paramref name="requestUri"/> templating, query string additions, and content body specification.</param>
        /// <returns>The <see cref="HttpRequest"/>.</returns>
        public HttpRequest CreateJsonHttpRequestFromResource<TAssembly>(HttpMethod httpMethod, string? requestUri, string resourceName, Ceh.HttpRequestOptions? requestOptions = null, params IHttpArg[] args)
            => CreateJsonHttpRequestFromResource(httpMethod, requestUri, resourceName, typeof(TAssembly).Assembly, requestOptions, args);

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
        /// <param name="args">Zero or more <see cref="IHttpArg"/> objects for <paramref name="requestUri"/> templating, query string additions, and content body specification.</param>
        /// <returns>The <see cref="HttpRequest"/>.</returns>
        public HttpRequest CreateJsonHttpRequestFromResource(HttpMethod httpMethod, string? requestUri, string resourceName, Assembly assembly, Ceh.HttpRequestOptions? requestOptions = null, params IHttpArg[] args)
        {
            if (httpMethod == HttpMethod.Get)
                Implementor.CreateLogger("FunctionTesterBase").LogWarning("A payload within a GET request message has no defined semantics; sending a payload body on a GET request might cause some existing implementations to reject the request (see https://www.rfc-editor.org/rfc/rfc7231).");

            var hr = CreateHttpRequest(httpMethod, requestUri, requestOptions, args);
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
        public ServiceBusReceivedMessage CreateServiceBusMessage<T>(T value)
            => CreateServiceBusMessageFromJson(JsonSerializer.Serialize(value));

        /// <summary>
        /// Creates a <see cref="ServiceBusReceivedMessage"/> where the <see cref="ServiceBusMessage.Body"/> <see cref="BinaryData"/> will contain the <paramref name="value"/> as serialized JSON.
        /// </summary>
        /// <typeparam name="T">The <paramref name="value"/> <see cref="Type"/>.</typeparam>
        /// <param name="value">The value.</param>
        /// <param name="messageModify">Optional <see cref="AmqpAnnotatedMessage"/> modifier than enables the message to be further configured.</param>
        /// <returns>The <see cref="ServiceBusReceivedMessage"/>.</returns>
        public ServiceBusReceivedMessage CreateServiceBusMessage<T>(T value, Action<AmqpAnnotatedMessage> messageModify)
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
            message.Header.DeliveryCount = 1;
            message.Header.Durable = true;
            message.Header.Priority = 1;
            message.Header.TimeToLive = TimeSpan.FromSeconds(60);
            message.Properties.ContentType = MediaTypeNames.Application.Json;
            message.Properties.MessageId = new AmqpMessageId(Guid.NewGuid().ToString());

            messageModify?.Invoke(message);

            var t = typeof(ServiceBusReceivedMessage);
            var c = t.GetConstructor(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null, new Type[] { typeof(AmqpAnnotatedMessage) }, null);
            if (c == null)
                throw new InvalidOperationException($"'{typeof(ServiceBusReceivedMessage).Name}' constructor that accepts Type '{typeof(AmqpAnnotatedMessage).Name}' parameter was not found.");

            return (ServiceBusReceivedMessage)c.Invoke(new object?[] { message });
        }

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