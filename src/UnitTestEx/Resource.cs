// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnitTestEx.Json;

namespace UnitTestEx
{
    /// <summary>
    /// Provides utility functionality for embedded resources. 
    /// </summary>
    public static class Resource
    {
        /// <summary>
        /// Gets the named embedded resource <see cref="StreamReader"/>.
        /// </summary>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualifed resource name).</param>
        /// <param name="assembly">The <see cref="Assembly"/> that contains the embedded resource; defaults to <see cref="Assembly.GetCallingAssembly"/>.</param>
        /// <returns>The <see cref="StreamReader"/>; otherwise, an <see cref="ArgumentException"/> will be thrown.</returns>
        public static StreamReader GetStream(string resourceName, Assembly? assembly = null)
        {
            assembly ??= Assembly.GetCallingAssembly();
            var coll = assembly.GetManifestResourceNames().Where(x => x.EndsWith(resourceName, StringComparison.InvariantCultureIgnoreCase));
            return coll.Count() switch
            {
                0 => throw new ArgumentException($"No embedded resource ending with '{resourceName}' was found in {assembly.FullName}.", nameof(resourceName)),
                1 => new StreamReader(assembly.GetManifestResourceStream(coll.First())!),
                _ => throw new ArgumentException($"More than one embedded resource ending with '{resourceName}' was found in {assembly.FullName}.", nameof(resourceName)),

            };
        }

        /// <summary>
        /// Gets the named embedded resource as a <see cref="string"/>.
        /// </summary>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualifed resource name).</param>
        /// <param name="assembly">The <see cref="Assembly"/> that contains the embedded resource; defaults to <see cref="Assembly.GetCallingAssembly"/>.</param>
        /// <returns>The JSON <see cref="string"/>.</returns>
        public static string GetString(string resourceName, Assembly? assembly = null)
        {
            using var sr = GetStream(resourceName, assembly ?? Assembly.GetCallingAssembly());
            return sr.ReadToEnd();
        }

        /// <summary>
        /// Gets the value by deserializing the JSON within the named embedded resource to the specified <see cref="Type"/> of <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The value <see cref="Type"/>.</typeparam>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualifed resource name).</param>
        /// <param name="assembly">The <see cref="Assembly"/> that contains the embedded resource; defaults to <see cref="Assembly.GetCallingAssembly"/>.</param>
        /// <param name="jsonSerializer">The <see cref="IJsonSerializer"/>; defaults to <see cref="JsonSerializer.Default"/>.</param>
        /// <returns>The deserialized value.</returns>
        public static T GetJsonValue<T>(string resourceName, Assembly? assembly = null, IJsonSerializer? jsonSerializer = null)
        {
            using var sr = GetStream(resourceName, assembly ?? Assembly.GetCallingAssembly());
            return (T)(jsonSerializer ?? JsonSerializer.Default).Deserialize(sr.ReadToEnd(), typeof(T))!;
        }

        /// <summary>
        /// Gets the named embedded resource as a validated JSON <see cref="string"/>.
        /// </summary>
        /// <param name="resourceName">The embedded resource name (matches to the end of the fully qualifed resource name).</param>
        /// <param name="assembly">The <see cref="Assembly"/> that contains the embedded resource; defaults to <see cref="Assembly.GetCallingAssembly"/>.</param>
        /// <returns>The JSON <see cref="string"/>.</returns>
        public static string GetJson(string resourceName, Assembly? assembly = null)
        {
            using var sr = GetStream(resourceName, assembly ?? Assembly.GetCallingAssembly());
            var json = sr.ReadToEnd();

            try
            {
                _ = JsonSerializer.Default.Deserialize(json);
                return json;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"JSON resource '{resourceName}' does not contain valid JSON: {ex.Message}");
            }
        }
    }
}