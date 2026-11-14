// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

namespace UnitTestEx.Abstractions
{
    /// <summary>
    /// Provides a framework-aware implementor without compile-time test-framework references.
    /// </summary>
    internal sealed class RuntimeTestFrameworkImplementor : TestFrameworkImplementor
    {
        private static readonly ConditionalWeakTable<Type, Lazy<OutputWriterBinding>> _outputWriterBindings = new();
        private readonly FrameworkAdapter _adapter;
        private readonly RuntimeFrameworkApiCache? _frameworkApi;
        private readonly Lazy<Action<string?>?> _suppliedOutputWriter;
        private int _diagnosticWritten;

        /// <summary>
        /// Initializes a new instance of the <see cref="RuntimeTestFrameworkImplementor"/> class.
        /// </summary>
        /// <param name="adapter">The selected framework adapter.</param>
        /// <param name="output">The optional test output object.</param>
        private RuntimeTestFrameworkImplementor(FrameworkAdapter adapter, object? output)
        {
            _adapter = adapter;
            _frameworkApi = adapter.Assembly is null ? null : RuntimeFrameworkApiCache.Get(adapter);
            _suppliedOutputWriter = new Lazy<Action<string?>?>(() => CreateSuppliedOutputWriter(output), LazyThreadSafetyMode.ExecutionAndPublication);
            if (adapter.Diagnostic is not null)
                Debug.WriteLine(adapter.Diagnostic);
        }

        /// <summary>
        /// Creates an implementor for the selected or detected framework.
        /// </summary>
        /// <param name="output">The optional test output object.</param>
        /// <returns>The implementor.</returns>
        public static RuntimeTestFrameworkImplementor Create(object? output = null)
        {
            TestSetUp.Force();
            return new RuntimeTestFrameworkImplementor(FrameworkAdapterResolver.Resolve(), output);
        }

        /// <inheritdoc/>
        public override void WriteLine(string? message)
        {
            WriteDiagnosticIfNeeded();
            if (_suppliedOutputWriter.Value is { } outputWriter)
            {
                outputWriter(message);
                return;
            }

            if (_frameworkApi?.TryWriteFrameworkOutput(message) == true)
                return;

            if (_adapter.Backend is TestFrameworkBackend.Xunit2 or TestFrameworkBackend.Xunit3)
                WriteDiagnosticOnce("UnitTestEx detected xUnit but no per-test output helper was supplied; output is falling back to Console.");

            Console.WriteLine(message);
        }

        /// <inheritdoc/>
        public override void AssertAreEqual<T>(T? expected, T? actual, string? message = null) where T : default
        {
            if (_adapter.Backend == TestFrameworkBackend.Fallback)
            {
                if (!EqualityComparer<T?>.Default.Equals(expected, actual))
                    AssertFail(message ?? $"Expected '{expected}', but found '{actual}'.");

                return;
            }

            var equalityAssert = _frameworkApi?.GetEqualityAssert<T>() ?? throw new InvalidOperationException($"Cannot create an equality assertion for '{_adapter.Backend}'.");
            if (_adapter.Backend is TestFrameworkBackend.Xunit2 or TestFrameworkBackend.Xunit3)
            {
                try
                {
                    equalityAssert(expected, actual, message);
                }
                catch (Exception ex)
                {
                    var failureMessage = message is null ? ex.Message : $"{message}{Environment.NewLine}{ex.Message}";
                    throw CreateFrameworkException(_adapter.AssertionExceptionType, failureMessage, ex);
                }

                return;
            }

            equalityAssert(expected, actual, message);
        }

        /// <inheritdoc/>
        public override void AssertFail(string? message)
        {
            if (_adapter.Backend == TestFrameworkBackend.NUnit)
            {
                (_frameworkApi ?? throw new InvalidOperationException("The NUnit framework API is unavailable.")).NUnitFail(message ?? "Not specified.");
                return;
            }

            throw CreateFrameworkException(_adapter.AssertionExceptionType, message ?? "Not specified.");
        }

        /// <inheritdoc/>
        public override void AssertInconclusive(string? message)
        {
            if (_adapter.InconclusiveExceptionType is null)
            {
                AssertFail($"Inconclusive: {message}");
                return;
            }

            throw CreateFrameworkException(_adapter.InconclusiveExceptionType, message ?? "Not specified.");
        }

        /// <summary>
        /// Writes any detection diagnostic through the selected output route once.
        /// </summary>
        private void WriteDiagnosticIfNeeded()
        {
            if (_adapter.Diagnostic is not null && Interlocked.Exchange(ref _diagnosticWritten, 1) == 0)
                WriteToFallback(_adapter.Diagnostic);
        }

        /// <summary>
        /// Writes an additional output diagnostic once.
        /// </summary>
        /// <param name="message">The diagnostic message.</param>
        private void WriteDiagnosticOnce(string message)
        {
            if (Interlocked.Exchange(ref _diagnosticWritten, 1) == 0)
            {
                Debug.WriteLine(message);
                WriteToFallback(message);
            }
        }

        /// <summary>
        /// Binds the supplied test output object to a reusable writer delegate.
        /// </summary>
        /// <param name="output">The optional test output object.</param>
        /// <returns>The writer delegate, or <see langword="null"/> when no output object was supplied.</returns>
        private static Action<string?>? CreateSuppliedOutputWriter(object? output)
        {
            if (output is null)
                return null;

            if (output is Action<string?> callback)
                return callback;

            if (output is TextWriter writer)
                return writer.WriteLine;

            var binding = _outputWriterBindings.GetValue(output.GetType(), type => new Lazy<OutputWriterBinding>(() => FindOutputWriterBinding(type), LazyThreadSafetyMode.ExecutionAndPublication)).Value;
            if (binding.IsFormatted)
            {
                var formattedWriteLine = (Action<string, object?[]>)binding.Method.CreateDelegate(typeof(Action<string, object?[]>), output);
                return message => formattedWriteLine("{0}", [message]);
            }

            return (Action<string?>)binding.Method.CreateDelegate(typeof(Action<string?>), output);
        }

        /// <summary>
        /// Finds the supported output writer method on an output object type.
        /// </summary>
        /// <param name="outputType">The output object type.</param>
        /// <returns>The method binding.</returns>
        private static OutputWriterBinding FindOutputWriterBinding(Type outputType)
        {
            var methods = outputType.GetMethods(BindingFlags.Instance | BindingFlags.Public);
            var singleArgumentWriteLine = methods.FirstOrDefault(m => m.Name == "WriteLine" && m.ReturnType == typeof(void) && m.GetParameters() is [{ ParameterType: var parameterType }] && parameterType == typeof(string));
            if (singleArgumentWriteLine is not null)
                return new OutputWriterBinding(singleArgumentWriteLine, false);

            var formatWriteLine = methods.FirstOrDefault(m => m.Name == "WriteLine" && m.ReturnType == typeof(void) && m.GetParameters() is [{ ParameterType: var firstType }, { ParameterType: var secondType }] && firstType == typeof(string) && secondType == typeof(object[]));
            return formatWriteLine is not null
                ? new OutputWriterBinding(formatWriteLine, true)
                : throw new InvalidOperationException($"The supplied output object '{outputType.FullName}' does not expose a supported WriteLine method.");
        }

        /// <summary>
        /// Creates a framework-specific assertion exception, or a framework-neutral exception for fallback mode.
        /// </summary>
        /// <param name="exceptionTypeName">The exception type name.</param>
        /// <param name="message">The failure message.</param>
        /// <param name="innerException">The optional inner exception.</param>
        /// <returns>The assertion exception.</returns>
        private Exception CreateFrameworkException(string? exceptionTypeName, string message, Exception? innerException = null)
        {
            if (_adapter.Backend == TestFrameworkBackend.Fallback || exceptionTypeName is null)
                return new UnitTestExAssertionException(message);

            return _frameworkApi?.CreateFrameworkException(exceptionTypeName, message, innerException) ?? throw new InvalidOperationException($"The selected test framework assembly '{_adapter.Assembly?.GetName().Name}' does not expose a runtime API cache.");
        }

        /// <summary>
        /// Writes to Console without re-entering the framework output path.
        /// </summary>
        /// <param name="message">The message.</param>
        private static void WriteToFallback(string message) => Console.Error.WriteLine(message);

        /// <summary>
        /// Stores a supported output writer method and whether it uses a format argument.
        /// </summary>
        /// <param name="Method">The writer method.</param>
        /// <param name="IsFormatted">Indicates whether the writer accepts a format and argument array.</param>
        private sealed record OutputWriterBinding(MethodInfo Method, bool IsFormatted);

    }

    /// <summary>
    /// Provides a framework-neutral assertion failure for an unidentified test framework.
    /// </summary>
    public sealed class UnitTestExAssertionException(string? message) : Exception(message ?? "UnitTestEx assertion failed.");

    /// <summary>
    /// Identifies the resolved test-framework backend.
    /// </summary>
    internal enum TestFrameworkBackend
    {
        /// <summary>Framework was not identified.</summary>
        Fallback,

        /// <summary>xUnit.net v2.</summary>
        Xunit2,

        /// <summary>xUnit.net v3.</summary>
        Xunit3,

        /// <summary>NUnit v4 or v5.</summary>
        NUnit,

        /// <summary>MSTest v3.</summary>
        MSTest3,

        /// <summary>MSTest v4.</summary>
        MSTest4
    }

    /// <summary>
    /// Stores the assembly and failure behavior for a selected framework.
    /// </summary>
    /// <param name="Backend">The resolved backend.</param>
    /// <param name="Assembly">The framework assembly.</param>
    /// <param name="AssertionExceptionType">The framework assertion exception type.</param>
    /// <param name="InconclusiveExceptionType">The framework inconclusive exception type.</param>
    /// <param name="Diagnostic">An optional detection diagnostic.</param>
    internal sealed record FrameworkAdapter(TestFrameworkBackend Backend, Assembly? Assembly, string? AssertionExceptionType, string? InconclusiveExceptionType, string? Diagnostic);

    /// <summary>
    /// Resolves an explicit or automatically detected test framework.
    /// </summary>
    internal static class FrameworkAdapterResolver
    {
        private static readonly Dictionary<TestFrameworkBackend, (string AssemblyName, string AssertionExceptionType, string? InconclusiveExceptionType)> _frameworks = new()
        {
            [TestFrameworkBackend.Xunit2] = ("xunit.assert", "Xunit.Sdk.XunitException", null),
            [TestFrameworkBackend.Xunit3] = ("xunit.v3.assert", "Xunit.Sdk.XunitException", null),
            [TestFrameworkBackend.NUnit] = ("nunit.framework", "NUnit.Framework.AssertionException", "NUnit.Framework.InconclusiveException"),
            [TestFrameworkBackend.MSTest3] = ("Microsoft.VisualStudio.TestPlatform.TestFramework", "Microsoft.VisualStudio.TestTools.UnitTesting.AssertFailedException", "Microsoft.VisualStudio.TestTools.UnitTesting.AssertInconclusiveException"),
            [TestFrameworkBackend.MSTest4] = ("MSTest.TestFramework", "Microsoft.VisualStudio.TestTools.UnitTesting.AssertFailedException", "Microsoft.VisualStudio.TestTools.UnitTesting.AssertInconclusiveException")
        };

        /// <summary>
        /// Resolves the selected framework or falls back when automatic detection is missing or ambiguous.
        /// </summary>
        /// <returns>The framework adapter.</returns>
        public static FrameworkAdapter Resolve()
        {
            var setting = TestSetUp.TestFramework?.ToString() ?? TestSetUp.GetConfiguration()["UnitTestEx:TestFramework"];
            if (!string.IsNullOrWhiteSpace(setting))
            {
                var name = NormalizeFrameworkName(setting);
                var kind = name switch
                {
                    "xunit" => DetectUnambiguousXunitBackend(setting),
                    "mstest" => DetectUnambiguousMSTestBackend(setting),
                    _ => ParseFramework(setting)
                };

                if (!_frameworks.TryGetValue(kind, out var selected))
                    throw new InvalidOperationException($"Test framework '{setting}' is not supported. Supported values: xunit2, xunit3, nunit, nunit4, nunit5, mstest3, mstest4.");

                var assembly = FindAssembly(selected.AssemblyName) ?? throw new InvalidOperationException($"Test framework '{setting}' was explicitly selected, but its assembly '{selected.AssemblyName}' is not loaded.");
                if (kind is TestFrameworkBackend.MSTest3 or TestFrameworkBackend.MSTest4 && DetectMSTestBackend(assembly) != kind)
                    throw new InvalidOperationException($"Test framework '{setting}' was explicitly selected, but the loaded MSTest assembly is not that major version.");

                if (name is "nunit4" or "nunit5" && DetectNUnitMajorVersion(assembly) != int.Parse(name[^1].ToString()))
                    throw new InvalidOperationException($"Test framework '{setting}' was explicitly selected, but the loaded NUnit assembly is not that major version.");

                return new FrameworkAdapter(kind, assembly, selected.AssertionExceptionType, selected.InconclusiveExceptionType, null);
            }

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var candidates = _frameworks.Where(pair => pair.Key is not (TestFrameworkBackend.MSTest3 or TestFrameworkBackend.MSTest4) && assemblies.Any(a => string.Equals(a.GetName().Name, pair.Value.AssemblyName, StringComparison.OrdinalIgnoreCase))).Select(pair => pair.Key).ToList();
            candidates.AddRange(FindMSTestAssemblies().Select(DetectMSTestBackend).Where(kind => kind is not null).Select(kind => kind!.Value));

            if (candidates.Count == 1)
            {
                var selected = _frameworks[candidates[0]];
                return new FrameworkAdapter(candidates[0], FindAssembly(selected.AssemblyName), selected.AssertionExceptionType, selected.InconclusiveExceptionType, null);
            }

            var diagnostic = candidates.Count == 0
                ? "UnitTestEx could not identify a supported test framework; using framework-neutral assertions and Console output."
                : $"UnitTestEx found multiple test frameworks ({string.Join(", ", candidates)}); set TestSetUp.TestFramework or UnitTestEx:TestFramework to select one. Using framework-neutral assertions and Console output.";

            return new FrameworkAdapter(TestFrameworkBackend.Fallback, null, null, null, diagnostic);
        }

        /// <summary>
        /// Parses a user-specified framework name.
        /// </summary>
        /// <param name="value">The setting value.</param>
        /// <returns>The normalized framework kind.</returns>
        private static TestFrameworkBackend ParseFramework(string value) =>NormalizeFrameworkName(value) switch
        {
            "xunit2" or "xunitv2" => TestFrameworkBackend.Xunit2,
            "xunit3" or "xunitv3" => TestFrameworkBackend.Xunit3,
            "nunit" or "nunit4" or "nunit5" => TestFrameworkBackend.NUnit,
            "mstest3" or "mstestv3" => TestFrameworkBackend.MSTest3,
            "mstest4" or "mstestv4" => TestFrameworkBackend.MSTest4,
            _ => throw new InvalidOperationException($"Test framework '{value}' is not supported. Supported values: xunit2, xunit3, nunit, nunit4, nunit5, mstest3, mstest4.")
        };

        /// <summary>
        /// Normalizes a test framework setting.
        /// </summary>
        /// <param name="value">The setting value.</param>
        /// <returns>The normalized setting.</returns>
        private static string NormalizeFrameworkName(string value) => value.Replace("-", "").Replace("_", "").Replace(".", "").Trim().ToLowerInvariant();

        /// <summary>
        /// Gets the loaded NUnit framework assembly's major version.
        /// </summary>
        /// <param name="assembly">The loaded NUnit framework assembly.</param>
        /// <returns>The major version, or zero when it cannot be identified.</returns>
        private static int DetectNUnitMajorVersion(Assembly assembly) => int.TryParse(GetFileVersion(assembly)?.Split('.')[0], out var major) ? major : 0;

        /// <summary>
        /// Determines whether the loaded MSTest framework assembly is version 3 or 4.
        /// </summary>
        /// <param name="assembly">The loaded MSTest framework assembly.</param>
        /// <returns>The detected major version, or <see langword="null"/> when it cannot be identified.</returns>
        private static TestFrameworkBackend? DetectMSTestBackend(Assembly assembly)
        {
            if (!int.TryParse(GetFileVersion(assembly)?.Split('.')[0], out var major))
                return null;

            return major switch
            {
                3 => TestFrameworkBackend.MSTest3,
                4 => TestFrameworkBackend.MSTest4,
                _ => null
            };
        }

        /// <summary>
        /// Gets an assembly's file version from metadata or its file.
        /// </summary>
        /// <param name="assembly">The framework assembly.</param>
        /// <returns>The file version, if available.</returns>
        private static string? GetFileVersion(Assembly assembly)
        {
            var version = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
            if (!string.IsNullOrEmpty(version))
                return version;

            return string.IsNullOrEmpty(assembly.Location) ? null : FileVersionInfo.GetVersionInfo(assembly.Location).FileVersion;
        }

        /// <summary>
        /// Detects the loaded xUnit major version for an unversioned explicit selection.
        /// </summary>
        /// <param name="setting">The setting value.</param>
        /// <returns>The detected xUnit version.</returns>
        private static TestFrameworkBackend DetectUnambiguousXunitBackend(string setting)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var candidates = new[]
            {
                (AssemblyName: "xunit.assert", Backend: TestFrameworkBackend.Xunit2),
                (AssemblyName: "xunit.v3.assert", Backend: TestFrameworkBackend.Xunit3)
            }.Where(candidate => assemblies.Any(assembly => string.Equals(assembly.GetName().Name, candidate.AssemblyName, StringComparison.OrdinalIgnoreCase))).ToArray();

            if (candidates.Length == 0)
                throw new InvalidOperationException($"Test framework '{setting}' was explicitly selected, but no supported xUnit framework assembly is loaded.");

            if (candidates.Length > 1)
                throw new InvalidOperationException($"Test framework '{setting}' is ambiguous because both xUnit major versions are loaded; select xunit2 or xunit3.");

            return candidates[0].Backend;
        }

        /// <summary>
        /// Detects the loaded MSTest major version for an unversioned explicit selection.
        /// </summary>
        /// <param name="setting">The setting value.</param>
        /// <returns>The detected MSTest version.</returns>
        private static TestFrameworkBackend DetectUnambiguousMSTestBackend(string setting)
        {
            var candidates = FindMSTestAssemblies().Select(assembly => (Assembly: assembly, Backend: DetectMSTestBackend(assembly))).Where(candidate => candidate.Backend is not null).ToArray();
            if (candidates.Length == 0)
                throw new InvalidOperationException($"Test framework '{setting}' was explicitly selected, but no supported MSTest framework assembly is loaded.");

            if (candidates.Length > 1)
                throw new InvalidOperationException($"Test framework '{setting}' is ambiguous because multiple MSTest major versions are loaded; select mstest3 or mstest4.");

            return candidates[0].Backend!.Value;
        }

        /// <summary>
        /// Finds the loaded MSTest framework assemblies.
        /// </summary>
        /// <returns>The loaded MSTest framework assemblies.</returns>
        private static IEnumerable<Assembly> FindMSTestAssemblies() => AppDomain.CurrentDomain.GetAssemblies().Where(assembly =>
            string.Equals(assembly.GetName().Name, "Microsoft.VisualStudio.TestPlatform.TestFramework", StringComparison.OrdinalIgnoreCase)
            || string.Equals(assembly.GetName().Name, "MSTest.TestFramework", StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// Finds a loaded assembly by simple name.
        /// </summary>
        /// <param name="name">The assembly name.</param>
        /// <returns>The matching assembly, if loaded.</returns>
        private static Assembly? FindAssembly(string name) => AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => string.Equals(a.GetName().Name, name, StringComparison.OrdinalIgnoreCase));
    }
}
