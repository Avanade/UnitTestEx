// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Mime;
using System.Reflection;
using System.Text;
using System.Threading;
using UnitTestEx.Json;
using UnitTestEx.Logging;

namespace UnitTestEx.Abstractions
{
    /// <summary>
    /// Provides the common/core base unit-testing capabilities.
    /// </summary>
    public abstract class TesterBase
    {
        private string? _userName;
        private readonly List<Action<IServiceCollection>> _configureServices = [];
        private IEnumerable<KeyValuePair<string, string?>>? _additionalConfiguration;

        /// <summary>
        /// Static constructor.
        /// </summary>
        static TesterBase()
        {
            try
            {
                var fi = new FileInfo(Path.Combine(Environment.CurrentDirectory, "appsettings.unittest.json"));
                if (!fi.Exists)
                    return;

                var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(fi.FullName));
                if (json.RootElement.TryGetProperty("DefaultJsonSerializer", out var je) && je.ValueKind == System.Text.Json.JsonValueKind.String)
                    TestSetUp.Default.JsonSerializer = (IJsonSerializer)Activator.CreateInstance(Type.GetType(je.GetString()!)!)!;
            }
            catch (Exception ex)
            {
                // Swallow and carry on; none of this logic should impact execution.
                System.Diagnostics.Debug.WriteLine($"UnitTestEx attempted to read, then load (if specified) the 'DefaultJsonSerializer' from, 'appsettings.unittest.json': {ex}.");
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TesterBase"/> class.
        /// </summary>
        /// <param name="implementor">The <see cref="TestFrameworkImplementor"/>.</param>
        public TesterBase(TestFrameworkImplementor implementor)
        {
            Implementor = implementor ?? throw new ArgumentNullException(nameof(implementor));
            LoggerProvider = new SharedStateLoggerProvider(SharedState);
            SetUp = TestSetUp.Default.Clone();
            JsonSerializer = SetUp.JsonSerializer;
            JsonComparerOptions = SetUp.JsonComparerOptions;
            TestSetUp.LogAutoSetUpOutputs(Implementor);
        }

        /// <summary>
        /// Gets the <see cref="TestFrameworkImplementor"/>.
        /// </summary>
        public TestFrameworkImplementor Implementor { get; private set; }

        /// <summary>
        /// Gets the <see cref="SharedStateLoggerProvider"/> <see cref="ILoggerProvider"/>.
        /// </summary>
        public SharedStateLoggerProvider LoggerProvider { get; }

        /// <summary>
        /// Gets the <see cref="TestSharedState"/>.
        /// </summary>
        public TestSharedState SharedState { get; } = new TestSharedState();

        /// <summary>
        /// Gets the configured <see cref="TestSetUp"/>. 
        /// </summary>
        /// <remarks>Defaults to <see cref="TestSetUp.Default"/>.</remarks>
        public TestSetUp SetUp { get; internal set; }

        /// <summary>
        /// Indicates whether the underlying host has been instantiated.
        /// </summary>
        /// <remarks>The host can be reset by invoking <see cref="TesterBase{TSelf}.ResetHost(bool)"/>.</remarks>
        public bool IsHostInstantiated { get; internal set; }

        /// <summary>
        /// Gets the synchronization object where synchronized access is required.
        /// </summary>
        protected object SyncRoot { get; } = new object();

        /// <summary>
        /// Gets the test user name.
        /// </summary>
        /// <remarks>Defaults to <see cref="SetUp"/> <see cref="TestSetUp.DefaultUserName"/>.</remarks>
        public string UserName
        {
            get => _userName ?? SetUp.DefaultUserName;
            protected set => _userName = value;
        }

        /// <summary>
        /// Gets the additional configuration used at host initialization (see <see cref="MemoryConfigurationBuilderExtensions.AddInMemoryCollection(IConfigurationBuilder, IEnumerable{KeyValuePair{string, string}})"/>).
        /// </summary>
        public IEnumerable<KeyValuePair<string, string?>>? AdditionalConfiguration
        {
            get => _additionalConfiguration?.ToArray();
            protected set
            {
                _additionalConfiguration = value;
                ResetHost(false);
            }
        }

        /// <summary>
        /// Gets the <see cref="IConfiguration"/> from the underlying host.
        /// </summary>
        /// <returns>The <see cref="IConfiguration"/>.</returns>
        /// <remarks>Accessing the <see cref="Configuration"/> may result in the underlying host being instantiated (see <see cref="IsHostInstantiated"/>) where applicable which may result in errors unless a subsequent <see cref="TesterBase{TSelf}.ResetHost(bool)"/> is performed.</remarks>
        public abstract IConfiguration Configuration { get; }

        /// <summary>
        /// Gets the <see cref="IServiceProvider"/> from the underlying host.
        /// </summary>
        /// <returns>The <see cref="IServiceProvider"/>.</returns>
        /// <remarks>Accessing the <see cref="Services"/> may result in the underlying host being instantiated (see <see cref="IsHostInstantiated"/>) where applicable which may result in errors unless a subsequent <see cref="TesterBase{TSelf}.ResetHost(bool)"/> is performed.</remarks>
        public abstract IServiceProvider Services { get; }

        /// <summary>
        /// Gets the <see cref="IJsonSerializer"/> <i>not</i> from the underlying host.
        /// </summary>
        /// <remarks>Defaults to <see cref="TestSetUp.JsonSerializer"/>. To change the <see cref="IJsonSerializer"/> use the <see cref="TesterBase{TSelf}.UseJsonSerializer"/> method. This does <i>not</i> use the
        /// instance from the underlying host as a different serializer may be required or may not have been configured.</remarks>
        public IJsonSerializer JsonSerializer { get; internal set; }

        /// <summary>
        /// Gets the <see cref="JsonElementComparerOptions"/> <i>not</i> from the underlying host.
        /// </summary>
        /// <remarks>Defaults to <see cref="TestSetUp.JsonSerializer"/>. To change the <see cref="IJsonSerializer"/> use the <see cref="TesterBase{TSelf}.UseJsonSerializer"/> method. This does <i>not</i> use the
        /// instance from the underlying host as a different serializer may be required or may not have been configured.</remarks>
        public JsonElementComparerOptions JsonComparerOptions { get; internal set; }

        /// <summary>
        /// Creates a <see cref="JsonElementComparer"/> using the configured <see cref="TesterBase.JsonComparerOptions"/> and <see cref="TesterBase.JsonSerializer"/>.
        /// </summary>
        /// <returns>A new <see cref="JsonElementComparer"/> instance.</returns>
        public JsonElementComparer CreateJsonComparer()
        {
            var options = JsonComparerOptions.Clone();
            options.JsonSerializer ??= JsonSerializer;
            return new JsonElementComparer(options);
        }

        /// <summary>
        /// Resets the underlying host to instantiate a new instance.
        /// </summary>
        /// <param name="resetConfiguredServices">Indicates whether to reset the previously configured services.</param>
        public void ResetHost(bool resetConfiguredServices = false)
        {
            lock (SyncRoot)
            {
                IsHostInstantiated = false;
                if (resetConfiguredServices)
                    _configureServices.Clear();

                ResetHost();
            }
        }

        /// <summary>
        /// Resets the underlying host to instantiate a new instance.
        /// </summary>
        protected abstract void ResetHost();

        /// <summary>
        /// Provides an opportunity to further configure the services before the underlying host is instantiated.
        /// </summary>
        /// <param name="configureServices">A delegate for configuring <see cref="IServiceCollection"/>.</param>
        /// <param name="autoResetHost">Indicates whether to automatically <see cref="ResetHost(bool)"/> (passing <c>false</c>) when configuring the services.</param>
        /// <remarks>This can be called multiple times prior to the underlying host being instantiated. Internally, the <paramref name="configureServices"/> is queued and then played in order when the host is initially instantiated.
        /// Once instantiated, further calls will result in a <see cref="InvalidOperationException"/> unless a <see cref="ResetHost(bool)"/> is performed.</remarks>
        public void ConfigureServices(Action<IServiceCollection> configureServices, bool autoResetHost = true)
        {
            lock (SyncRoot)
            {
                if (autoResetHost)
                    ResetHost(false);

                _configureServices.Add(configureServices);
            }
        }

        /// <summary>
        /// Adds the previously <see cref="ConfigureServices(Action{IServiceCollection}, bool)"/> to the <paramref name="services"/>.
        /// </summary>
        /// <remarks>It is recommended that this is performed within a <see cref="SyncRoot"/> to ensure thread-safety.</remarks>
        protected void AddConfiguredServices(IServiceCollection services)
        {
            if (IsHostInstantiated)
                throw new InvalidOperationException($"Underlying host has been instantiated and as such the {nameof(ConfigureServices)} operations can no longer be used; consider using '{nameof(ResetHost)}' prior to enable.");

            foreach (var configureService in _configureServices)
            {
                configureService(services);
            }

            IsHostInstantiated = true;
        }

        /// <summary>
        /// Replaces the <see cref="TestFrameworkImplementor"/> with the specified <paramref name="implementor"/>.
        /// </summary>
        /// <param name="implementor">The new <see cref="TestFrameworkImplementor"/>.</param>
        public void ReplaceTestFrameworkImplementor(TestFrameworkImplementor implementor)
        {
            Implementor = implementor ?? throw new ArgumentNullException(nameof(implementor));
        }

        /// <summary>
        /// Logs the <see cref="HttpResponseMessage"/>.
        /// </summary>
        /// <param name="res">The <see cref="HttpResponseMessage"/>.</param>
        /// <param name="sw">The optional <see cref="Stopwatch"/>.</param>
        internal void LogHttpResponseMessage(HttpResponseMessage res, Stopwatch? sw)
        {
            Implementor.WriteLine("");
            Implementor.WriteLine($"RESPONSE >");
            Implementor.WriteLine($"HttpStatusCode: {res.StatusCode} ({(int)res.StatusCode})");
            Implementor.WriteLine($"Elapsed (ms): {(sw == null ? "none" : sw.Elapsed.TotalMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture))}");

            var hdrs = res.Headers?.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            Implementor.WriteLine($"Headers: {(hdrs == null || hdrs.Length == 0 ? "none" : "")}");
            if (hdrs != null && hdrs.Length > 0)
            {
                foreach (var hdr in hdrs)
                {
                    Implementor.WriteLine($"  {hdr}");
                }
            }

            object? jo = null;
            var content = res.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (!string.IsNullOrEmpty(content) && res.Content?.Headers?.ContentType?.MediaType == MediaTypeNames.Application.Json)
            {
                try
                {
                    jo = JsonSerializer.Deserialize(content);
                }
                catch (Exception) { /* This is being swallowed by design. */ }
            }

            var txt = $"Content: [{res.Content?.Headers?.ContentType?.MediaType ?? "none"}]";
            if (jo != null)
            {
                Implementor.WriteLine(txt);
                Implementor.WriteLine(JsonSerializer.Serialize(jo, JsonWriteFormat.Indented));
            }
            else
                Implementor.WriteLine($"{txt} {(string.IsNullOrEmpty(content) ? "none" : content)}");

            Implementor.WriteLine("");
            Implementor.WriteLine(new string('=', 80));
            Implementor.WriteLine("");
        }

        #region CreateHttpRequest

        /// <summary>
        /// Creates a new <see cref="HttpRequest"/> with no body.
        /// </summary>
        /// <param name="httpMethod">The <see cref="HttpMethod"/>.</param>
        /// <param name="requestUri">The requuest uri.</param>
        /// <returns>The <see cref="HttpRequest"/>.</returns>
#if NET7_0_OR_GREATER
        public HttpRequest CreateHttpRequest(HttpMethod httpMethod, [StringSyntax(StringSyntaxAttribute.Uri)] string? requestUri)
#else
        public HttpRequest CreateHttpRequest(HttpMethod httpMethod, string? requestUri)
#endif
            => CreateHttpRequest(httpMethod, requestUri, null, MediaTypeNames.Text.Plain, null);

        /// <summary>
        /// Creates a new <see cref="HttpRequest"/> with <paramref name="body"/> (defaults <see cref="HttpRequest.ContentType"/> to <see cref="MediaTypeNames.Text.Plain"/>).
        /// </summary>
        /// <param name="httpMethod">The <see cref="HttpMethod"/>.</param>
        /// <param name="requestUri">The requuest uri.</param>
        /// <param name="body">The optional body content.</param>
        /// <returns>The <see cref="HttpRequest"/>.</returns>
#if NET7_0_OR_GREATER
        public HttpRequest CreateHttpRequest(HttpMethod httpMethod, [StringSyntax(StringSyntaxAttribute.Uri)] string? requestUri, string? body)
#else
        public HttpRequest CreateHttpRequest(HttpMethod httpMethod, string? requestUri, string? body)
#endif
            => CreateHttpRequest(httpMethod, requestUri, body, null, null);

        /// <summary>
        /// Creates a new <see cref="HttpRequest"/> with <paramref name="body"/> and <paramref name="contentType"/>.
        /// </summary>
        /// <param name="httpMethod">The <see cref="HttpMethod"/>.</param>
        /// <param name="requestUri">The requuest uri.</param>
        /// <param name="body">The optional body content.</param>
        /// <param name="contentType">The content type. Defaults to <see cref="MediaTypeNames.Text.Plain"/>.</param>
        /// <returns>The <see cref="HttpRequest"/>.</returns>
#if NET7_0_OR_GREATER
        public HttpRequest CreateHttpRequest(HttpMethod httpMethod, [StringSyntax(StringSyntaxAttribute.Uri)] string? requestUri, string? body, string? contentType)
#else
        public HttpRequest CreateHttpRequest(HttpMethod httpMethod, string? requestUri, string? body, string? contentType)
#endif
            => CreateHttpRequest(httpMethod, requestUri, body, contentType, null);

        /// <summary>
        /// Creates a new <see cref="HttpRequest"/> with no body.
        /// </summary>
        /// <param name="httpMethod">The <see cref="HttpMethod"/>.</param>
        /// <param name="requestUri">The requuest uri.</param>
        /// <param name="requestModifier">The optional <see cref="HttpRequest"/> modifier.</param>
        /// <returns>The <see cref="HttpRequest"/>.</returns>
#if NET7_0_OR_GREATER
        public HttpRequest CreateHttpRequest(HttpMethod httpMethod, [StringSyntax(StringSyntaxAttribute.Uri)] string? requestUri, Action<HttpRequest>? requestModifier = null)
#else
        public HttpRequest CreateHttpRequest(HttpMethod httpMethod, string? requestUri, Action<HttpRequest>? requestModifier = null)
#endif
            => CreateHttpRequest(httpMethod, requestUri, null, MediaTypeNames.Text.Plain, requestModifier);

        /// <summary>
        /// Creates a new <see cref="HttpRequest"/> with <paramref name="body"/> (defaults <see cref="HttpRequest.ContentType"/> to <see cref="MediaTypeNames.Text.Plain"/>).
        /// </summary>
        /// <param name="httpMethod">The <see cref="HttpMethod"/>.</param>
        /// <param name="requestUri">The requuest uri.</param>
        /// <param name="body">The optional body content.</param>
        /// <param name="requestModifier">The optional <see cref="HttpRequest"/> modifier.</param>
        /// <returns>The <see cref="HttpRequest"/>.</returns>
#if NET7_0_OR_GREATER
        public HttpRequest CreateHttpRequest(HttpMethod httpMethod, [StringSyntax(StringSyntaxAttribute.Uri)] string? requestUri, string? body, Action<HttpRequest>? requestModifier = null)
#else
        public HttpRequest CreateHttpRequest(HttpMethod httpMethod, string? requestUri, string? body, Action<HttpRequest>? requestModifier = null)
#endif
            => CreateHttpRequest(httpMethod, requestUri, body, null, requestModifier);

        /// <summary>
        /// Creates a new <see cref="HttpRequest"/> with <i>optional</i> <paramref name="body"/> (defaults <see cref="HttpRequest.ContentType"/> to <see cref="MediaTypeNames.Text.Plain"/>).
        /// </summary>
        /// <param name="httpMethod">The <see cref="HttpMethod"/>.</param>
        /// <param name="requestUri">The requuest uri.</param>
        /// <param name="body">The optional body content.</param>
        /// <param name="contentType">The content type. Defaults to <see cref="MediaTypeNames.Text.Plain"/>.</param>
        /// <param name="requestModifier">The optional <see cref="HttpRequest"/> modifier.</param>
        /// <returns>The <see cref="HttpRequest"/>.</returns>
#if NET7_0_OR_GREATER
        public HttpRequest CreateHttpRequest(HttpMethod httpMethod, [StringSyntax(StringSyntaxAttribute.Uri)] string? requestUri = null, string? body = null, string? contentType = null, Action<HttpRequest>? requestModifier = null)
#else
        public HttpRequest CreateHttpRequest(HttpMethod httpMethod, string? requestUri = null, string? body = null, string? contentType = null, Action<HttpRequest>? requestModifier = null)
#endif
        {
            if (httpMethod == HttpMethod.Get && body != null)
                LoggerProvider.CreateLogger("FunctionTesterBase").LogWarning("A payload within a GET request message has no defined semantics; sending a payload body on a GET request might cause some existing implementations to reject the request (see https://www.rfc-editor.org/rfc/rfc7231).");

            var context = new DefaultHttpContext
            {
                RequestServices = Services
            };

            var uri = requestUri is null ? new Uri("http://unittestex") : new Uri(requestUri, UriKind.RelativeOrAbsolute);
            if (!uri.IsAbsoluteUri)
                uri = new Uri($"http://unittestex{(requestUri != null && requestUri.StartsWith('/') ? requestUri : $"/{requestUri}")}");

            context.Request.Method = httpMethod?.Method ?? HttpMethod.Get.Method;
            context.Request.Scheme = uri.Scheme;
            context.Request.Host = new HostString(uri.Host);
            context.Request.Path = uri.LocalPath;
            context.Request.QueryString = new QueryString(uri.Query);

            if (body is not null)
            {
                context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
                context.Request.ContentType = contentType ?? MediaTypeNames.Text.Plain;
                context.Request.ContentLength = body.Length;
            }
            else
                context.Request.ContentType = contentType;

            requestModifier?.Invoke(context.Request);

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
#if NET7_0_OR_GREATER
        public HttpRequest CreateJsonHttpRequest(HttpMethod httpMethod, [StringSyntax(StringSyntaxAttribute.Uri)] string? requestUri, object? value)
#else
        public HttpRequest CreateJsonHttpRequest(HttpMethod httpMethod, string? requestUri, object? value)
#endif
            => CreateJsonHttpRequest(httpMethod, requestUri, value, null);

        /// <summary>
        /// Creates a new <see cref="HttpRequest"/> with the <paramref name="value"/> JSON serialized as <see cref="HttpRequest.ContentType"/> of <see cref="MediaTypeNames.Application.Json"/>.
        /// </summary>
        /// <param name="httpMethod">The <see cref="HttpMethod"/>.</param>
        /// <param name="requestUri">The requuest uri.</param>
        /// <param name="value">The value to JSON serialize.</param>
        /// <param name="requestModifier">The optional <see cref="HttpRequest"/> modifier.</param>
        /// <returns>The <see cref="HttpRequest"/>.</returns>
#if NET7_0_OR_GREATER
        public HttpRequest CreateJsonHttpRequest(HttpMethod httpMethod, [StringSyntax(StringSyntaxAttribute.Uri)] string? requestUri, object? value, Action<HttpRequest>? requestModifier = null)
#else
        public HttpRequest CreateJsonHttpRequest(HttpMethod httpMethod, string? requestUri, object? value, Action<HttpRequest>? requestModifier = null)
#endif
            => CreateHttpRequest(httpMethod, requestUri, JsonSerializer.Serialize(value), MediaTypeNames.Application.Json, requestModifier: requestModifier);

        /// <summary>
        /// Creates a new <see cref="HttpRequest"/> using the JSON formatted embedded resource as the content (<see cref="MediaTypeNames.Application.Json"/>).
        /// </summary>
        /// <typeparam name="TAssembly">The <see cref="Type"/> to infer <see cref="Type.Assembly"/> for the embedded resources.</typeparam>
        /// <param name="httpMethod">The <see cref="HttpMethod"/>.</param>
        /// <param name="requestUri">The requuest uri.</param>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualifed resource name).</param>
        /// <returns>The <see cref="HttpRequest"/>.</returns>
#if NET7_0_OR_GREATER
        public HttpRequest CreateJsonHttpRequestFromResource<TAssembly>(HttpMethod httpMethod, [StringSyntax(StringSyntaxAttribute.Uri)] string? requestUri, string resourceName)
#else
        public HttpRequest CreateJsonHttpRequestFromResource<TAssembly>(HttpMethod httpMethod, string? requestUri, string resourceName)
#endif
            => CreateJsonHttpRequestFromResource<TAssembly>(httpMethod, requestUri, resourceName, null);

        /// <summary>
        /// Creates a new <see cref="HttpRequest"/> using the JSON formatted embedded resource as the content (<see cref="MediaTypeNames.Application.Json"/>).
        /// </summary>
        /// <typeparam name="TAssembly">The <see cref="Type"/> to infer <see cref="Type.Assembly"/> for the embedded resources.</typeparam>
        /// <param name="httpMethod">The <see cref="HttpMethod"/>.</param>
        /// <param name="requestUri">The requuest uri.</param>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualifed resource name).</param>
        /// <param name="requestModifier">The optional <see cref="HttpRequest"/> modifier.</param>
        /// <returns>The <see cref="HttpRequest"/>.</returns>
#if NET7_0_OR_GREATER
        public HttpRequest CreateJsonHttpRequestFromResource<TAssembly>(HttpMethod httpMethod, [StringSyntax(StringSyntaxAttribute.Uri)] string? requestUri, string resourceName, Action<HttpRequest>? requestModifier = null)
#else
        public HttpRequest CreateJsonHttpRequestFromResource<TAssembly>(HttpMethod httpMethod, string? requestUri, string resourceName, Action<HttpRequest>? requestModifier = null)
#endif
            => CreateJsonHttpRequestFromResource(httpMethod, requestUri, resourceName, typeof(TAssembly).Assembly, requestModifier);

        /// <summary>
        /// Creates a new <see cref="HttpRequest"/> using the JSON formatted embedded resource as the content (<see cref="MediaTypeNames.Application.Json"/>).
        /// </summary>
        /// <param name="httpMethod">The <see cref="HttpMethod"/>.</param>
        /// <param name="requestUri">The requuest uri.</param>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualifed resource name).</param>
        /// <param name="assembly">The <see cref="Assembly"/> that contains the embedded resource; defaults to <see cref="Assembly.GetEntryAssembly()"/>.</param>
        /// <returns>The <see cref="HttpRequest"/>.</returns>
#if NET7_0_OR_GREATER
        public HttpRequest CreateJsonHttpRequestFromResource(HttpMethod httpMethod, [StringSyntax(StringSyntaxAttribute.Uri)] string? requestUri, string resourceName, Assembly assembly)
#else
        public HttpRequest CreateJsonHttpRequestFromResource(HttpMethod httpMethod, string? requestUri, string resourceName, Assembly assembly)
#endif
            => CreateJsonHttpRequestFromResource(httpMethod, requestUri, resourceName, assembly, null);

        /// <summary>
        /// Creates a new <see cref="HttpRequest"/> using the JSON formatted embedded resource as the content (<see cref="MediaTypeNames.Application.Json"/>).
        /// </summary>
        /// <param name="httpMethod">The <see cref="HttpMethod"/>.</param>
        /// <param name="requestUri">The requuest uri.</param>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualifed resource name).</param>
        /// <param name="assembly">The <see cref="Assembly"/> that contains the embedded resource; defaults to <see cref="Assembly.GetEntryAssembly()"/>.</param>
        /// <param name="requestModifier">The optional <see cref="HttpRequest"/> modifier.</param>
        /// <returns>The <see cref="HttpRequest"/>.</returns>
#if NET7_0_OR_GREATER
        public HttpRequest CreateJsonHttpRequestFromResource(HttpMethod httpMethod, [StringSyntax(StringSyntaxAttribute.Uri)] string? requestUri, string resourceName, Assembly assembly, Action<HttpRequest>? requestModifier = null)
#else
        public HttpRequest CreateJsonHttpRequestFromResource(HttpMethod httpMethod, string? requestUri, string resourceName, Assembly assembly, Action<HttpRequest>? requestModifier = null)
#endif
            => CreateHttpRequest(httpMethod, requestUri, Resource.GetJson(resourceName, assembly ?? Assembly.GetCallingAssembly()), MediaTypeNames.Application.Json, requestModifier: requestModifier);

        #endregion
    }
}