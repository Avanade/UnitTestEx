// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using CoreEx;
using CoreEx.Configuration;
using CoreEx.Events;
using CoreEx.Json;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using UnitTestEx.Expectations;

namespace UnitTestEx.Abstractions
{
    /// <summary>
    /// Provides the common/core base unit-testing capabilities.
    /// </summary>
    public abstract class TesterBase
    {
        private readonly string? _username;

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
                {
                    if (string.Compare(je.GetString(), "System.Text.Json", StringComparison.InvariantCultureIgnoreCase) == 0)
                        CoreEx.Json.JsonSerializer.Default = new CoreEx.Text.Json.JsonSerializer();
                    else if (string.Compare(je.GetString(), "Newtonsoft.Json", StringComparison.InvariantCultureIgnoreCase) == 0)
                        CoreEx.Json.JsonSerializer.Default = new CoreEx.Newtonsoft.Json.JsonSerializer();
                }
            }
            catch { } // Swallow and carry on; none of this logic should impact execution.
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TesterBase"/> class.
        /// </summary>
        /// <param name="implementor">The <see cref="TestFrameworkImplementor"/>.</param>
        /// <param name="username">The username (<c>null</c> indicates to use the existing <see cref="ExecutionContext.Current"/> <see cref="ExecutionContext.Username"/> where configured).</param>
        protected TesterBase(TestFrameworkImplementor implementor, string? username)
        {
            Implementor = implementor ?? throw new ArgumentNullException(nameof(implementor));
            JsonSerializer = CoreEx.Json.JsonSerializer.Default;
            _username = username;
        }

        /// <summary>
        /// Gets the <see cref="TestFrameworkImplementor"/>.
        /// </summary>
        protected internal TestFrameworkImplementor Implementor { get; }

        /// <summary>
        /// Gets the <see cref="TestSharedState"/>.
        /// </summary>
        public TestSharedState SharedState { get; } = new TestSharedState();

        /// <summary>
        /// Gets the configured <see cref="TestSetUp"/>. 
        /// </summary>
        /// <remarks>Defaults to <see cref="TestSetUp.Default"/>.</remarks>
        public TestSetUp SetUp { get; internal set; } = TestSetUp.Default;

        /// <summary>
        /// Gets the test username.
        /// </summary>
        /// <remarks>This is determined as follows (uses first non <c>null</c> value): as set via the constructor, using the <see cref="ExecutionContext.Username"/>, and finally <see cref="SetUp"/> <see cref="TestSetUp.DefaultUsername"/>.</remarks>
        public string? Username => _username ?? (ExecutionContext.HasCurrent ? ExecutionContext.Current.Username : SetUp.DefaultUsername);

        /// <summary>
        /// Gets the <see cref="IConfiguration"/> from the underlying host.
        /// </summary>
        /// <returns>The <see cref="IConfiguration"/>.</returns>
        public abstract IConfiguration Configuration { get; }

        /// <summary>
        /// Gets the <see cref="SettingsBase"/> from the underlying host.
        /// </summary>
        public abstract SettingsBase Settings { get; }

        /// <summary>
        /// Gets the <see cref="IJsonSerializer"/>.
        /// </summary>
        /// <remarks>Defaults to <see cref="CoreEx.Json.JsonSerializer.Default"/>. To change the <see cref="IJsonSerializer"/> use the <see cref="TesterBase{TSelf}.UseJsonSerializer(IJsonSerializer)"/> method.</remarks>
        public IJsonSerializer JsonSerializer { get; internal set; }

        /// <summary>
        /// Gets the <see cref="IServiceProvider"/> from the underlying host.
        /// </summary>
        /// <returns>The <see cref="IServiceProvider"/>.</returns>
        public abstract IServiceProvider Services { get; }

        /// <summary>
        /// Indicates whether the <see cref="ExpectedEventPublisher"/> has been enabled; i.e. the <see cref="IEventPublisher"/> has been explicitly replaced.
        /// </summary>
        /// <remarks>This is set by <see cref="TestSetUp.ExpectedEventsEnabled"/> either via <see cref="TestSetUp.Default"/> or <see cref="TesterBase{TSelf}.UseSetUp(TestSetUp)"/>; or alternatively via <see cref="TesterBase{TSelf}.UseExpectedEvents"/>.</remarks>
        public bool IsExpectedEventPublisherEnabled { get; internal set; }
    }
}