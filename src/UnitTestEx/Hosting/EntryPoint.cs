// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Reflection;

namespace UnitTestEx.Hosting
{
    /// <summary>
    /// Provides a generic <see cref="EntryPoint"/> to support dependency injection.
    /// </summary>
    /// <remarks>Uses reflection to map to the same named methods: <see cref="ConfigureAppConfiguration"/>, <see cref="ConfigureHostConfiguration"/> and <see cref="ConfigureServices"/>.</remarks>
    public class EntryPoint
    {
        private readonly object _instance;
        private readonly MethodInfo? _mi1;
        private readonly MethodInfo? _mi2;
        private readonly MethodInfo? _mi3;

        /// <summary>
        /// Initializes a new instance of the <see cref="EntryPoint"/> class.
        /// </summary>
        /// <param name="instance">The entry point instance.</param>
        public EntryPoint(object instance)
        {
            _instance = instance ?? throw new ArgumentNullException(nameof(instance));
            _mi1 = instance.GetType().GetMethod(nameof(ConfigureAppConfiguration), BindingFlags.Instance | BindingFlags.Public, [typeof(HostBuilderContext), typeof(IConfigurationBuilder)]);
            _mi2 = instance.GetType().GetMethod(nameof(ConfigureHostConfiguration), BindingFlags.Instance | BindingFlags.Public, [typeof(IConfigurationBuilder)]);
            _mi3 = instance.GetType().GetMethod(nameof(ConfigureServices), BindingFlags.Instance | BindingFlags.Public, [typeof(IServiceCollection)]);
        }

        /// <summary>
        /// Sets up the configuration for the remainder of the build process and application.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="config"></param>
        /// <remarks>This is intended to be invoked by the <see cref="IHostBuilder.ConfigureAppConfiguration(System.Action{HostBuilderContext, IConfigurationBuilder})"/>.</remarks>
        public void ConfigureAppConfiguration(HostBuilderContext context, IConfigurationBuilder config) => _mi1?.Invoke(_instance, new object[] { context, config });

        /// <summary>
        /// Sets up the configuration for the builder itself to initialize the <see cref="IHostEnvironment"/>.
        /// </summary>
        /// <param name="config">The <see cref="IConfigurationBuilder"/>.</param>
        /// <remarks>This is intended to be invoked by the <see cref="IHostBuilder.ConfigureHostConfiguration(System.Action{IConfigurationBuilder})"/>.</remarks>
        public void ConfigureHostConfiguration(IConfigurationBuilder config) => _mi2?.Invoke(_instance, new object[] { config });

        /// <summary>
        /// Adds services to the container.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <remarks>This is intended to be invoked by the <see cref="IHostBuilder.ConfigureServices(System.Action{HostBuilderContext, IServiceCollection})"/>.</remarks>
        public void ConfigureServices(IServiceCollection services) => _mi3?.Invoke(_instance, new object[] { services });
    }
}