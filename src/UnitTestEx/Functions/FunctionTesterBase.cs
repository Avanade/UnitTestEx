// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using CoreEx.AspNetCore.Http;
using CoreEx.Configuration;
using CoreEx.Hosting;
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
    /// <typeparam name="TEntryPoint">The <see cref="FunctionsStartup"/> or <see cref="IHostStartup"/> <see cref="Type"/>.</typeparam>
    /// <typeparam name="TSelf">The <see cref="FunctionTesterBase{TEntryPoint, TSelf}"/> to support inheriting fluent-style method-chaining.</typeparam>
    public abstract class FunctionTesterBase<TEntryPoint, TSelf> : TesterBase<TSelf>, IDisposable where TEntryPoint : class, new() where TSelf : FunctionTesterBase<TEntryPoint, TSelf>
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

                var ep = new TEntryPoint();
                var ep2 = ep as FunctionsStartup;
                var ep3 = ep as IHostStartup;
                if (ep2 is null && ep3 is null)
                    throw new InvalidOperationException($"The {typeof(TEntryPoint).Name} must be a {typeof(FunctionsStartup).Name} or {typeof(IHostStartup).Name}.");

                return _host = new HostBuilder()
                    .UseEnvironment(TestSetUp.Environment)
                    .ConfigureLogging((lb) => { lb.SetMinimumLevel(SetUp.MinimumLogLevel); lb.ClearProviders(); lb.AddProvider(LoggerProvider); })
                    .ConfigureHostConfiguration(cb =>
                    {
                        cb.SetBasePath(Environment.CurrentDirectory)
                            .AddInMemoryCollection(new KeyValuePair<string, string?>[] { new KeyValuePair<string, string?>("AzureWebJobsConfigurationSection", "AzureFunctionsJobHost") })
                            .AddJsonFile(GetLocalSettingsJson(), optional: true)
                            .AddJsonFile("appsettings.json", optional: true)
                            .AddJsonFile($"appsettings.{TestSetUp.Environment.ToLowerInvariant()}.json", optional: true);

                        if ((!_includeUserSecrets.HasValue && TestSetUp.FunctionTesterIncludeUserSecrets) || (_includeUserSecrets.HasValue && _includeUserSecrets.Value))
                            cb.AddUserSecrets<TEntryPoint>();

                        cb.AddEnvironmentVariables();

                        ep3?.ConfigureHostConfiguration(cb);

                        // Apply early so can be reference.
                        if ((!_includeUnitTestConfiguration.HasValue && TestSetUp.FunctionTesterIncludeUnitTestConfiguration) || (_includeUnitTestConfiguration.HasValue && _includeUnitTestConfiguration.Value))
                            cb.AddJsonFile("appsettings.unittest.json", optional: true);

                        if (_additionalConfiguration != null)
                            cb.AddInMemoryCollection(_additionalConfiguration);
                    })
                    .ConfigureAppConfiguration((hbc, cb) =>
                    {
                        ep2?.ConfigureAppConfiguration(MockIFunctionsConfigurationBuilder(cb));
                        ep3?.ConfigureAppConfiguration(hbc, cb);
                    })
                    .ConfigureServices(sc =>
                    {
                        ep2?.Configure(MockIFunctionsHostBuilder(sc));
                        ep3?.ConfigureServices(sc);
                        sc.ReplaceScoped(_ => SharedState);
                        SetUp.ConfigureServices?.Invoke(sc);
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
            context.Request.QueryString = new QueryString(uri.Query);

            // Extend the query string to include additional options.
            if (requestOptions != null)
                context.Request.ApplyRequestOptions(requestOptions);

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