// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnitTestEx.Json;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using Stj = System.Text.Json;

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

        /// <summary>
        /// Gets (determines) the <see cref="StreamContentType"/> from the <paramref name="fileName"/> extension.
        /// </summary>
        /// <param name="fileName">The file name.</param>
        /// <returns>The corresponding <see cref="StreamContentType"/>.</returns>
        public static StreamContentType GetContentType(string fileName) => new FileInfo(fileName).Extension.ToLowerInvariant() switch
        {
            ".yaml" => StreamContentType.Yaml,
            ".yml" => StreamContentType.Yaml,
            ".json" => StreamContentType.Json,
            ".jsn" => StreamContentType.Json,
            _ => StreamContentType.Unknown
        };

        /// <summary>
        /// Defines the supported <see cref="Stream"/> content types.
        /// </summary>
        public enum StreamContentType
        {
            /// <summary>
            /// Specifies that the content type is unknown.
            /// </summary>
            Unknown,

            /// <summary>
            /// Specifies that the content type is YAML.
            /// </summary>
            Yaml,

            /// <summary>
            /// Specifies that the content type is JSON.
            /// </summary>
            Json
        }

        /// <summary>
        /// Converts the <paramref name="yaml"/> <see cref="TextReader"/> content into <typeparamref name="T"/>.
        /// </summary>
        /// <param name="yaml">The YAML <see cref="TextReader"/>.</param>
        /// <param name="jsonSerializer">The <see cref="IJsonSerializer"/>.</param>
        /// <returns>The value.</returns>
        public static T? DeserializeYaml<T>(this TextReader yaml, IJsonSerializer? jsonSerializer = null)
        {
            var yml = new DeserializerBuilder().WithNodeTypeResolver(new YamlNodeTypeResolver()).Build().Deserialize(yaml);

#pragma warning disable IDE0063 // Use simple 'using' statement; cannot as need to be more explicit with managing the close and dispose.
            using (var ms = new MemoryStream())
            {
                using (var sw = new StreamWriter(ms))
                {
                    sw.Write(new SerializerBuilder().JsonCompatible().Build().Serialize(yml!));
                    sw.Flush();

                    ms.Position = 0;
                    using var sr = new StreamReader(ms);
                    return sr.DeserializeJson<T>(jsonSerializer);
                }
            }
#pragma warning restore IDE0063
        }

        /// <summary>
        /// Converts the <paramref name="json"/> <see cref="TextReader"/> content into <typeparamref name="T"/>.
        /// </summary>
        /// <param name="json">The YAML <see cref="TextReader"/>.</param>
        /// <param name="jsonSerializer">The <see cref="IJsonSerializer"/>.</param>
        /// <returns>The value.</returns>
        public static T? DeserializeJson<T>(this TextReader json, IJsonSerializer? jsonSerializer = null) => (jsonSerializer ?? JsonSerializer.Default).Deserialize<T>(json.ReadToEnd());

        private class YamlNodeTypeResolver : INodeTypeResolver
        {
            private static readonly string[] boolValues = ["true", "false"];

            /// <inheritdoc/>
            bool INodeTypeResolver.Resolve(NodeEvent? nodeEvent, ref Type currentType)
            {
                if (nodeEvent is Scalar scalar && scalar.Style == YamlDotNet.Core.ScalarStyle.Plain)
                {
                    if (decimal.TryParse(scalar.Value, out _))
                    {
                        if (scalar.Value.Length > 1 && scalar.Value.StartsWith('0')) // Valid JSON does not support a number that starts with a zero.
                            currentType = typeof(string);
                        else
                            currentType = typeof(decimal);

                        return true;
                    }

                    if (boolValues.Contains(scalar.Value))
                    {
                        currentType = typeof(bool);
                        return true;
                    }
                }

                return false;
            }
        }
    }
}