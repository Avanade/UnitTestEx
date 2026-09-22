// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Mime;
using System.Reflection;
using System.Text;
using System.Threading;
using UnitTestEx.Json;

namespace UnitTestEx.Abstractions
{
    /// <summary>
    /// Provides the common single in-process host (dependency injection (DI) enabled) unit-testing capabilities.
    /// </summary>
    /// <remarks>This extends the host-agnostic <see cref="TesterBaseCore"/> to add the DI-specific capabilities (<see cref="Services"/>, <see cref="Configuration"/>, <see cref="ConfigureServices(Action{IServiceCollection}, bool)"/>)
    /// that only make sense where there is a single host with a single DI container. A multi-host distributed application tester (e.g. a companion Aspire tester) should instead inherit <see cref="TesterBaseCore"/> directly.</remarks>
    public abstract class TesterBase : TesterBaseCore
    {
        private readonly List<Action<IServiceCollection>> _configureServices = [];
        private IEnumerable<KeyValuePair<string, string?>>? _additionalConfiguration;

        /// <summary>
        /// Initializes a new instance of the <see cref="TesterBase"/> class.
        /// </summary>
        /// <param name="implementor">The <see cref="TestFrameworkImplementor"/>.</param>
        public TesterBase(TestFrameworkImplementor implementor) : base(implementor) { }

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
        /// <remarks>Accessing the <see cref="Configuration"/> may result in the underlying host being instantiated (see <see cref="TesterBaseCore.IsHostInstantiated"/>) where applicable which may result in errors unless a subsequent <see cref="TesterBase{TSelf}.ResetHost(bool)"/> is performed.</remarks>
        public abstract IConfiguration Configuration { get; }

        /// <summary>
        /// Gets the <see cref="IServiceProvider"/> from the underlying host.
        /// </summary>
        /// <returns>The <see cref="IServiceProvider"/>.</returns>
        /// <remarks>Accessing the <see cref="Services"/> may result in the underlying host being instantiated (see <see cref="TesterBaseCore.IsHostInstantiated"/>) where applicable which may result in errors unless a subsequent <see cref="TesterBase{TSelf}.ResetHost(bool)"/> is performed.</remarks>
        public abstract IServiceProvider Services { get; }

        /// <summary>
        /// Resets the underlying host to instantiate a new instance.
        /// </summary>
        /// <param name="resetConfiguredServices">Indicates whether to reset the previously configured services and start-ups.</param>
        public new void ResetHost(bool resetConfiguredServices = false)
        {
            lock (SyncRoot)
            {
                if (resetConfiguredServices)
                    _configureServices.Clear();

                base.ResetHost();
            }
        }

        /// <summary>
        /// Provides an opportunity to further configure the services before the underlying host is instantiated.
        /// </summary>
        /// <param name="configureServices">A delegate for configuring <see cref="IServiceCollection"/>.</param>
        /// <param name="autoResetHost">Indicates whether to automatically <see cref="ResetHost(bool)"/> (passing <c>false</c>) when configuring the services.</param>
        /// <remarks>This can be called multiple times prior to the underlying host being instantiated. Internally, the <paramref name="configureServices"/> is queued and then played in order when the host is initially instantiated.</remarks>
        protected void ConfigureServices(Action<IServiceCollection> configureServices, bool autoResetHost = true)
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

        #region CreateHttpRequest

        /// <summary>
        /// Creates a new <see cref="HttpRequest"/> with no body.
        /// </summary>
        /// <param name="httpMethod">The <see cref="HttpMethod"/>.</param>
        /// <param name="requestUri">The request uri.</param>
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
        /// <param name="requestUri">The request uri.</param>
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
        /// <param name="requestUri">The request uri.</param>
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
        /// <param name="requestUri">The request uri.</param>
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
        /// <param name="requestUri">The request uri.</param>
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
        /// <param name="requestUri">The request uri.</param>
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
        /// <param name="requestUri">The request uri.</param>
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
        /// <param name="requestUri">The request uri.</param>
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
        /// <param name="requestUri">The request uri.</param>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualified resource name).</param>
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
        /// <param name="requestUri">The request uri.</param>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualified resource name).</param>
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
        /// <param name="requestUri">The request uri.</param>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualified resource name).</param>
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
        /// <param name="requestUri">The request uri.</param>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualified resource name).</param>
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