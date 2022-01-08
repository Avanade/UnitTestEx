// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Mime;
using System.Text;
using UnitTestEx.Abstractions;

namespace UnitTestEx.Functions
{
    /// <summary>
    /// Provides the basic Azure Function unit-testing capabilities.
    /// </summary>
    /// <typeparam name="TEntryPoint">The API startup <see cref="Type"/>.</typeparam>
    /// <typeparam name="TSelf">The <see cref="FunctionTesterBase{TEntryPoint, TSelf}"/> to support inheriting fluent-style method-chaining.</typeparam>
    public abstract class FunctionTesterBase<TEntryPoint, TSelf> : IDisposable where TEntryPoint : FunctionsStartup, new() where TSelf : FunctionTesterBase<TEntryPoint, TSelf>
    {
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
        /// <param name="additionalConfiguration">Additional configuration values to add/override.</param>
        protected FunctionTesterBase(TestFrameworkImplementor implementor, bool includeUnitTestConfiguration, IEnumerable<KeyValuePair<string, string>>? additionalConfiguration)
        {
            Implementor = implementor ?? throw new ArgumentNullException(nameof(implementor));
            Logger = Implementor.CreateLogger(GetType().Name);

            var ep2 = new TEntryPoint();
            _hostBuilder = new HostBuilder()
                .UseEnvironment("Development")
                .ConfigureLogging((lb) => lb.AddProvider(Implementor.CreateLoggerProvider()))
                .ConfigureWebHostDefaults(c =>
                {
                    c.ConfigureAppConfiguration((cx, cb) =>
                    {
                        cb.SetBasePath(Environment.CurrentDirectory)
                            .AddInMemoryCollection(new KeyValuePair<string, string>[] { new KeyValuePair<string, string>("AzureWebJobsConfigurationSection", "AzureFunctionsJobHost") })
                            .AddJsonFile(GetLocalSettingsJson(), optional: true)
                            .AddJsonFile("appsettings.json", optional: true)
                            .AddJsonFile("appsettings.development.json", optional: true)
                            .AddUserSecrets<TEntryPoint>()
                            .AddEnvironmentVariables();

                        if (includeUnitTestConfiguration)
                            cb.AddJsonFile("appsettings.unittest.json");

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
            // Manage a temporary local.settings.json for the values.
            var tfi = new FileInfo(Path.Combine(Environment.CurrentDirectory, "temporary.local.settings.json"));
            if (tfi.Exists)
                tfi.Delete();

            // Simulate the loading of the local.settings.json values.
            var fi = new FileInfo(Path.Combine(Environment.CurrentDirectory, "local.settings.json"));
            if (!fi.Exists)
                return "{ }";

            using var tr = new StreamReader(fi.OpenRead());
            using var jr = new JsonTextReader(tr);
            var jt = JToken.ReadFrom(jr);
            var jtv = jt["Values"];
            if (jtv != null)
            {
                using var fs = tfi.OpenWrite();
                using var tw = new StreamWriter(fs);
                using var jw = new JsonTextWriter(tw);
                jtv.WriteTo(jw);
            }

            return tfi.Name;
        }

        /// <summary>
        /// Gets the <see cref="TestFrameworkImplementor"/>.
        /// </summary>
        public TestFrameworkImplementor Implementor { get; }

        /// <summary>
        /// Gets the function runtime <see cref="ILogger"/>.
        /// </summary>
        /// <returns>The <see cref="ILogger"/>.</returns>
        public ILogger Logger { get; }

        /// <summary>
        /// Provides an opportunity to further configure the services. This can be called multiple times. 
        /// </summary>
        /// <param name="configureServices">A delegate for configuring <see cref="IServiceCollection"/>.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        public TSelf ConfigureServices(Action<IServiceCollection> configureServices)
        {
            if (_host != null)
                throw new InvalidOperationException($"{nameof(ConfigureServices)} cannot be invoked after a test execution has occured.");

            if (configureServices != null)
                _hostBuilder.ConfigureServices(configureServices);

            return (TSelf)this;
        }

        /// <summary>
        /// Replace scoped service with a mock object.
        /// </summary>
        /// <typeparam name="T">The underlying <see cref="Type"/> being mocked.</typeparam>
        /// <param name="mock">The <see cref="Mock{T}"/>.</param>
        /// <returns>The <typeparamref name="TSelf"/> to support fluent-style method-chaining.</returns>
        public TSelf MockScopedService<T>(Mock<T> mock) where T : class => ConfigureServices(sc => sc.ReplaceScoped(mock.Object));

        /// <summary>
        /// Specify the Function that has an <see cref="HttpTrigger"/> to test.
        /// </summary>
        /// <typeparam name="TFunction">The Function <see cref="Type"/>.</typeparam>
        /// <returns>The <see cref="HttpTriggerTester{TFunction}"/>.</returns>
        public HttpTriggerTester<TFunction> HttpTrigger<TFunction>() where TFunction : class
        {
            if (_host == null)
                _host = _hostBuilder.Build();

            return new HttpTriggerTester<TFunction>(_host.Services.CreateScope(), Implementor);
        }

        /// <summary>
        /// Creates a new <see cref="HttpRequest"/> with <i>optional</i> <paramref name="body"/> as <see cref="HttpRequest.ContentType"/> of <see cref="MediaTypeNames.Text.Plain"/>.
        /// </summary>
        /// <param name="httpMethod">The <see cref="HttpMethod"/>.</param>
        /// <param name="requestUri">The requuest uri.</param>
        /// <param name="body">The optional body content.</param>
        /// <returns>The <see cref="HttpRequest"/>.</returns>
        public HttpRequest CreateHttpRequest(HttpMethod httpMethod, string? requestUri = null, string? body = null)
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
            context.Request.QueryString = new QueryString(uri.Query);

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
        /// <param name="value">The value.</param>
        /// <param name="requestUri">The requuest uri.</param>
        /// <returns>The <see cref="HttpRequest"/>.</returns>
        public HttpRequest CreateJsonHttpRequest(HttpMethod httpMethod, object value, string? requestUri = null)
        {
            if (httpMethod == HttpMethod.Get)
                Implementor.CreateLogger("FunctionTesterBase").LogWarning("A payload within a GET request message has no defined semantics; sending a payload body on a GET request might cause some existing implementations to reject the request (see https://www.rfc-editor.org/rfc/rfc7231).");

            var hr = CreateHttpRequest(httpMethod, requestUri);
            hr.Body = new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(value)));
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