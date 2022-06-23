// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using CoreEx;
using CoreEx.Json;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;

namespace UnitTestEx.Abstractions
{
    /// <summary>
    /// Provides the common/core base unit-testing capabilities.
    /// </summary>
    public abstract class TesterBase
    {
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
            Username = username ?? (ExecutionContext.HasCurrent ? ExecutionContext.Current.Username : null);
        }

        /// <summary>
        /// Gets the <see cref="TestFrameworkImplementor"/>.
        /// </summary>
        protected internal TestFrameworkImplementor Implementor { get; }

        /// <summary>
        /// Gets the test username.
        /// </summary>
        public string? Username { get; }

        /// <summary>
        /// Gets the <see cref="IConfiguration"/> from the underlying host.
        /// </summary>
        /// <returns>The <see cref="IConfiguration"/>.</returns>
        public abstract IConfiguration Configuration { get; }

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
    }
}