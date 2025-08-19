// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;

namespace UnitTestEx
{
    /// <summary>
    /// Provides extension methods to support testing.
    /// </summary>
    public static class ExtensionMethods
    {
        #region Singleton

        /// <summary>
        /// Replaces (where existing), or adds, a singleton service <paramref name="instance"/>. 
        /// </summary>
        /// <typeparam name="TService">The service <see cref="Type"/>.</typeparam>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <param name="instance">The instance value.</param>
        /// <remarks>The <see cref="IServiceCollection"/> to support fluent-style method-chaining.</remarks>
        public static IServiceCollection ReplaceSingleton<TService>(this IServiceCollection services, TService instance) where TService : class => ReplaceSingleton(services, _ => instance);

        /// <summary>
        /// Replaces (where existing), or adds, a singleton service using an <paramref name="implementationFactory"/>.
        /// </summary>
        /// <typeparam name="TService">The service <see cref="Type"/>.</typeparam>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <param name="implementationFactory">The implementation factory.</param>
        /// <remarks>The <see cref="IServiceCollection"/> to support fluent-style method-chaining.</remarks>
        public static IServiceCollection ReplaceSingleton<TService>(this IServiceCollection services, Func<IServiceProvider, TService> implementationFactory) where TService : class
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(implementationFactory);

            services.Remove<TService>();
            return services.AddSingleton(implementationFactory);
        }

        /// <summary>
        /// Replaces (where existing), or adds, a singleton service. 
        /// </summary>
        /// <typeparam name="TService">The service <see cref="Type"/>.</typeparam>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <remarks>The <see cref="IServiceCollection"/> to support fluent-style method-chaining.</remarks>
        public static IServiceCollection ReplaceSingleton<TService>(this IServiceCollection services) where TService : class
            => ReplaceSingleton<TService, TService>(services);

        /// <summary>
        /// Replaces (where existing), or adds, a singleton service. 
        /// </summary>
        /// <typeparam name="TService">The service <see cref="Type"/>.</typeparam>
        /// <typeparam name="TImplementation">The implementation <see cref="Type"/>.</typeparam>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <remarks>The <see cref="IServiceCollection"/> to support fluent-style method-chaining.</remarks>
        public static IServiceCollection ReplaceSingleton<TService, TImplementation>(this IServiceCollection services) where TService : class where TImplementation : class, TService
        {
            ArgumentNullException.ThrowIfNull(services);

            services.Remove<TService>();
            return services.AddSingleton<TService, TImplementation>();
        }

        /* Keyed */

        /// <summary>
        /// Replaces (where existing), or adds, a keyed singleton service <paramref name="instance"/>. 
        /// </summary>
        /// <typeparam name="TService">The service <see cref="Type"/>.</typeparam>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <param name="serviceKey">The service key.</param>
        /// <param name="instance">The instance value.</param>
        /// <remarks>The <see cref="IServiceCollection"/> to support fluent-style method-chaining.</remarks>
        public static IServiceCollection ReplaceKeyedSingleton<TService>(this IServiceCollection services, object? serviceKey, TService instance) where TService : class => ReplaceKeyedSingleton(services, serviceKey, _ => instance);

        /// <summary>
        /// Replaces (where existing), or adds, a keyed singleton service using an <paramref name="implementationFactory"/>.
        /// </summary>
        /// <typeparam name="TService">The service <see cref="Type"/>.</typeparam>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <param name="serviceKey">The service key.</param>
        /// <param name="implementationFactory">The implementation factory.</param>
        /// <remarks>The <see cref="IServiceCollection"/> to support fluent-style method-chaining.</remarks>
        public static IServiceCollection ReplaceKeyedSingleton<TService>(this IServiceCollection services, object? serviceKey, Func<IServiceProvider, TService> implementationFactory) where TService : class
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(implementationFactory);

            services.RemoveKeyed<TService>(serviceKey);
            return services.AddKeyedSingleton(serviceKey, implementationFactory);
        }

        /// <summary>
        /// Replaces (where existing), or adds, a keyed singleton service. 
        /// </summary>
        /// <typeparam name="TService">The service <see cref="Type"/>.</typeparam>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <param name="serviceKey">The service key.</param>
        /// <remarks>The <see cref="IServiceCollection"/> to support fluent-style method-chaining.</remarks>
        public static IServiceCollection ReplaceKeyedSingleton<TService>(this IServiceCollection services, object? serviceKey) where TService : class
            => ReplaceKeyedSingleton<TService, TService>(services, serviceKey);

        /// <summary>
        /// Replaces (where existing), or adds, a keyed singleton service. 
        /// </summary>
        /// <typeparam name="TService">The service <see cref="Type"/>.</typeparam>
        /// <typeparam name="TImplementation">The implementation <see cref="Type"/>.</typeparam>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <param name="serviceKey">The service key.</param>
        /// <remarks>The <see cref="IServiceCollection"/> to support fluent-style method-chaining.</remarks>
        public static IServiceCollection ReplaceKeyedSingleton<TService, TImplementation>(this IServiceCollection services, object? serviceKey) where TService : class where TImplementation : class, TService
        {
            ArgumentNullException.ThrowIfNull(services);

            services.RemoveKeyed<TService>(serviceKey);
            return services.AddKeyedSingleton<TService, TImplementation>(serviceKey);
        }

        #endregion

        #region Scoped

        /// <summary>
        /// Replaces (where existing), or adds, a scoped service using an <paramref name="implementationFactory"/>.
        /// </summary>
        /// <typeparam name="TService">The service <see cref="Type"/>.</typeparam>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <param name="implementationFactory">The implementation factory.</param>
        /// <remarks>The <see cref="IServiceCollection"/> to support fluent-style method-chaining.</remarks>
        public static IServiceCollection ReplaceScoped<TService>(this IServiceCollection services, Func<IServiceProvider, TService> implementationFactory) where TService : class
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(implementationFactory);

            services.Remove<TService>();
            return services.AddScoped(implementationFactory);
        }

        /// <summary>
        /// Replaces (where existing), or adds, a scoped service. 
        /// </summary>
        /// <typeparam name="TService">The service <see cref="Type"/>.</typeparam>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <remarks>The <see cref="IServiceCollection"/> to support fluent-style method-chaining.</remarks>
        public static IServiceCollection ReplaceScoped<TService>(this IServiceCollection services) where TService : class
            => ReplaceScoped<TService, TService>(services);

        /// <summary>
        /// Replaces (where existing), or adds, a scoped service. 
        /// </summary>
        /// <typeparam name="TService">The service <see cref="Type"/>.</typeparam>
        /// <typeparam name="TImplementation">The implementation <see cref="Type"/>.</typeparam>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <remarks>The <see cref="IServiceCollection"/> to support fluent-style method-chaining.</remarks>
        public static IServiceCollection ReplaceScoped<TService, TImplementation>(this IServiceCollection services) where TService : class where TImplementation : class, TService
        {
            ArgumentNullException.ThrowIfNull(services);

            services.Remove<TService>();
            return services.AddScoped<TService, TImplementation>();
        }

        /* Keyed */

        /// <summary>
        /// Replaces (where existing), or adds, a keyed scoped service using an <paramref name="implementationFactory"/>.
        /// </summary>
        /// <typeparam name="TService">The service <see cref="Type"/>.</typeparam>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <param name="serviceKey">The service key.</param>
        /// <param name="implementationFactory">The implementation factory.</param>
        /// <remarks>The <see cref="IServiceCollection"/> to support fluent-style method-chaining.</remarks>
        public static IServiceCollection ReplaceKeyedScoped<TService>(this IServiceCollection services, object? serviceKey, Func<IServiceProvider, object?, TService> implementationFactory) where TService : class
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(implementationFactory);

            services.RemoveKeyed<TService>(serviceKey);
            return services.AddKeyedScoped<TService>(serviceKey, implementationFactory);
        }

        /// <summary>
        /// Replaces (where existing), or adds, a keyed scoped service. 
        /// </summary>
        /// <typeparam name="TService">The service <see cref="Type"/>.</typeparam>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <param name="serviceKey">The service key.</param>
        /// <remarks>The <see cref="IServiceCollection"/> to support fluent-style method-chaining.</remarks>
        public static IServiceCollection ReplaceKeyedScoped<TService>(this IServiceCollection services, object? serviceKey) where TService : class
            => ReplaceKeyedScoped<TService, TService>(services, serviceKey);

        /// <summary>
        /// Replaces (where existing), or adds, a keyed scoped service. 
        /// </summary>
        /// <typeparam name="TService">The service <see cref="Type"/>.</typeparam>
        /// <typeparam name="TImplementation">The implementation <see cref="Type"/>.</typeparam>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <param name="serviceKey">The service key.</param>
        /// <remarks>The <see cref="IServiceCollection"/> to support fluent-style method-chaining.</remarks>
        public static IServiceCollection ReplaceKeyedScoped<TService, TImplementation>(this IServiceCollection services, object? serviceKey) where TService : class where TImplementation : class, TService
        {
            ArgumentNullException.ThrowIfNull(services);

            services.RemoveKeyed<TService>(serviceKey);
            return services.AddKeyedScoped<TService, TImplementation>(serviceKey);
        }

        #endregion

        #region Transient

        /// <summary>
        /// Replaces (where existing), or adds, a transient service using an <paramref name="implementationFactory"/>.
        /// </summary>
        /// <typeparam name="TService">The service <see cref="Type"/>.</typeparam>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <param name="implementationFactory">The implementation factory.</param>
        /// <remarks>The <see cref="IServiceCollection"/> to support fluent-style method-chaining.</remarks>
        public static IServiceCollection ReplaceTransient<TService>(this IServiceCollection services, Func<IServiceProvider, TService> implementationFactory) where TService : class
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(implementationFactory);

            services.Remove<TService>();
            return services.AddTransient(implementationFactory);
        }

        /// <summary>
        /// Replaces (where existing), or adds, a transient service. 
        /// </summary>
        /// <typeparam name="TService">The service <see cref="Type"/>.</typeparam>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <remarks>The <see cref="IServiceCollection"/> to support fluent-style method-chaining.</remarks>
        public static IServiceCollection ReplaceTransient<TService>(this IServiceCollection services) where TService : class
            => ReplaceTransient<TService, TService>(services);

        /// <summary>
        /// Replaces (where existing), or adds, a transient service. 
        /// </summary>
        /// <typeparam name="TService">The service <see cref="Type"/>.</typeparam>
        /// <typeparam name="TImplementation">The implementation <see cref="Type"/>.</typeparam>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <remarks>The <see cref="IServiceCollection"/> to support fluent-style method-chaining.</remarks>
        public static IServiceCollection ReplaceTransient<TService, TImplementation>(this IServiceCollection services) where TService : class where TImplementation : class, TService
        {
            ArgumentNullException.ThrowIfNull(services);

            services.Remove<TService>();
            return services.AddTransient<TService, TImplementation>();
        }

        /* Keyed */

        /// <summary>
        /// Replaces (where existing), or adds, a keyed transient service using an <paramref name="implementationFactory"/>.
        /// </summary>
        /// <typeparam name="TService">The service <see cref="Type"/>.</typeparam>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <param name="serviceKey">The service key.</param>
        /// <param name="implementationFactory">The implementation factory.</param>
        /// <remarks>The <see cref="IServiceCollection"/> to support fluent-style method-chaining.</remarks>
        public static IServiceCollection ReplaceKeyedTransient<TService>(this IServiceCollection services, object? serviceKey, Func<IServiceProvider, object?, TService> implementationFactory) where TService : class
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(implementationFactory);

            services.RemoveKeyed<TService>(serviceKey);
            return services.AddKeyedTransient(serviceKey, implementationFactory);
        }

        /// <summary>
        /// Replaces (where existing), or adds, a keyed transient service. 
        /// </summary>
        /// <typeparam name="TService">The service <see cref="Type"/>.</typeparam>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <param name="serviceKey">The service key.</param>
        /// <remarks>The <see cref="IServiceCollection"/> to support fluent-style method-chaining.</remarks>
        public static IServiceCollection ReplaceKeyedTransient<TService>(this IServiceCollection services, object? serviceKey) where TService : class
            => ReplaceKeyedTransient<TService, TService>(services, serviceKey);

        /// <summary>
        /// Replaces (where existing), or adds, a keyed transient service. 
        /// </summary>
        /// <typeparam name="TService">The service <see cref="Type"/>.</typeparam>
        /// <typeparam name="TImplementation">The implementation <see cref="Type"/>.</typeparam>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <param name="serviceKey">The service key.</param>
        /// <remarks>The <see cref="IServiceCollection"/> to support fluent-style method-chaining.</remarks>
        public static IServiceCollection ReplaceKeyedTransient<TService, TImplementation>(this IServiceCollection services, object? serviceKey) where TService : class where TImplementation : class, TService
        {
            ArgumentNullException.ThrowIfNull(services);

            services.RemoveKeyed<TService>(serviceKey);
            return services.AddKeyedTransient<TService, TImplementation>(serviceKey);
        }

        #endregion

        /// <summary>
        /// Create (or get) an instance of <see cref="Type"/> <typeparamref name="T"/> using Dependency Injection (DI) using the <paramref name="serviceProvider"/>.
        /// </summary>
        /// <typeparam name="T">The <see cref="Type"/> to instantiate.</typeparam>
        /// <param name="serviceProvider">The <see cref="IServiceProvider"/>.</param>
        /// <param name="serviceKey">The optional keyed service key.</param>
        /// <returns>A reference to the newly created object.</returns>
        /// <remarks>Where <see cref="Type"/> <typeparamref name="T"/> not specifically configured within the <paramref name="serviceProvider"/> DI simulatution will occur by performing constructor-based injection for all required parameters.</remarks>
        public static T CreateInstance<T>(this IServiceProvider serviceProvider, object? serviceKey = null) where T : class
        {
            // Try instantiating using service provider and use if successful.
            var val = serviceKey is null
                ? (serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider))).GetService<T>()
                : (serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider))).GetKeyedService<T>(serviceKey);

            if (val != null)
                return val;

            if (serviceKey is not null)
                throw new InvalidOperationException($"Unable to instantiate Type '{typeof(T).Name}' with key '{serviceKey}'.");

            var type = typeof(T);
            var ctor = type.GetConstructors().FirstOrDefault();
            if (ctor == null)
                return (T)(Activator.CreateInstance(type) ?? throw new InvalidOperationException($"Unable to instantiate Type '{type.Name}'"));

            // Simulate dependency injection for each parameter.
            var pis = ctor.GetParameters();
            var args = new object?[pis.Length];
            for (int i = 0; i < pis.Length; i++)
            {
                args[i] = serviceProvider.GetService(pis[i].ParameterType);
            }

            return (T)(Activator.CreateInstance(type, args) ?? throw new InvalidOperationException($"Unable to instantiate Type '{type.Name}'"));
        }

        /// <summary>
        /// Removes the first occurrence from the <see cref="IServiceCollection"/> for the specified <typeparamref name="TService"/>.
        /// </summary>
        /// <typeparam name="TService">The service <see cref="Type"/>.</typeparam>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <returns><c>true</c> if item was successfully removed; otherwise, <c>false</c>. Also returns <c>false</c> where item was not found.</returns>
        public static bool Remove<TService>(this IServiceCollection services) where TService : class
        {
            var descriptor = (services ?? throw new ArgumentNullException(nameof(services))).FirstOrDefault(d => d.ServiceType == typeof(TService));
            return descriptor != null && services.Remove(descriptor);
        }

        /// <summary>
        /// Removes the first occurrence from the <see cref="IServiceCollection"/> for the specified <typeparamref name="TService"/> and <paramref name="serviceKey"/>.
        /// </summary>
        /// <typeparam name="TService">The service <see cref="Type"/>.</typeparam>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <param name="serviceKey">The service key.</param>
        /// <returns><c>true</c> if item was successfully removed; otherwise, <c>false</c>. Also returns <c>false</c> where item was not found.</returns>
        public static bool RemoveKeyed<TService>(this IServiceCollection services, object? serviceKey) where TService : class
        {
            var descriptor = (services ?? throw new ArgumentNullException(nameof(services))).FirstOrDefault(d => d.ServiceType == typeof(TService) && d.IsKeyedService && d.ServiceKey == serviceKey);
            return descriptor != null && services.Remove(descriptor);
        }
    }
}