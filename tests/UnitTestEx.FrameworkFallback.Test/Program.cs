using System;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using UnitTestEx;
using UnitTestEx.Abstractions;

/// <summary>
/// Runs framework-neutral fallback integration checks without loading a test framework.
/// </summary>
internal static class Program
{
    /// <summary>
    /// Runs the fallback integration checks.
    /// </summary>
    /// <returns>Zero when all checks pass.</returns>
    private static int Main()
    {
        if (!VerifyFallback(noFramework: true))
            return 1;

        if (!VerifyFactoryScopes())
            return 1;

        AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("nunit.framework"), AssemblyBuilderAccess.Run);
        AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("xunit.assert"), AssemblyBuilderAccess.Run);
        if (!VerifyFallback(noFramework: false))
            return 1;

        return VerifyInvalidFrameworkSelection() ? 0 : 1;
    }

    /// <summary>Verifies factory precedence and restoration across per-test contexts.</summary>
    /// <returns>Whether the expected implementors were used.</returns>
    private static bool VerifyFactoryScopes()
    {
        var global = new NullTestImplementor();
        var local = new NullTestImplementor();
        var explicitImplementor = new NullTestImplementor();
        TestFrameworkImplementor.SetGlobalCreateFactory(() => global);
        try
        {
            using var outer = new Fixture();
            if (!ReferenceEquals(TestFrameworkImplementor.Create(), global))
                return false;

            TestFrameworkImplementor.SetLocalCreateFactory(() => local);
            try
            {
                using (var inner = new Fixture())
                {
                    if (!ReferenceEquals(TestFrameworkImplementor.Create(output: new StringWriter()), local))
                        return false;

                    using var explicitFixture = new Fixture(() => explicitImplementor);
                    if (!ReferenceEquals(TestFrameworkImplementor.Create(), explicitImplementor))
                        return false;
                }

                if (!ReferenceEquals(TestFrameworkImplementor.Create(), global))
                    return false;
            }
            finally
            {
                TestFrameworkImplementor.ResetLocalCreateFactory();
            }

            return ReferenceEquals(TestFrameworkImplementor.Create(), global);
        }
        finally
        {
            TestFrameworkImplementor.ResetGlobalCreateFactory();
        }
    }

    /// <summary>Creates a per-test scope for framework-free factory checks.</summary>
    private sealed class Fixture(Func<TestFrameworkImplementor> factory = null) : UnitTestBase(createFactory: factory) { }

    /// <summary>
    /// Verifies neutral assertions, diagnostics, and output for no-match or ambiguous detection.
    /// </summary>
    /// <param name="noFramework">Indicates whether no framework is loaded.</param>
    /// <returns><see langword="true"/> when the expected fallback behavior occurs.</returns>
    private static bool VerifyFallback(bool noFramework)
    {
        var standardOutput = new StringWriter();
        var standardError = new StringWriter();
        var originalOutput = Console.Out;
        var originalError = Console.Error;
        var originalFrameworkSetting = Environment.GetEnvironmentVariable("UnitTestEx__TestFramework");
        try
        {
            Console.SetOut(standardOutput);
            Console.SetError(standardError);
            Environment.SetEnvironmentVariable("UnitTestEx__TestFramework", null);
            TestSetUp.TestFramework = null;
            var implementor = TestFrameworkImplementor.Create();
            implementor.AssertAreEqual(1, 1);
            implementor.WriteLine("UNITTESTEX_FALLBACK_OUTPUT");
            try
            {
                implementor.AssertFail("fallback assertion");
                return false;
            }
            catch (UnitTestExAssertionException)
            {
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("UnitTestEx__TestFramework", originalFrameworkSetting);
            Console.SetOut(originalOutput);
            Console.SetError(originalError);
        }

        var expectedDiagnostic = noFramework ? "could not identify a supported test framework" : "found multiple test frameworks";
        return standardOutput.ToString().Contains("UNITTESTEX_FALLBACK_OUTPUT", StringComparison.Ordinal)
            && standardError.ToString().Contains(expectedDiagnostic, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that an invalid explicit framework name is rejected.
    /// </summary>
    /// <returns><see langword="true"/> when the invalid selection throws.</returns>
    private static bool VerifyInvalidFrameworkSelection()
    {
        var previousSetting = Environment.GetEnvironmentVariable("UnitTestEx__TestFramework");
        TestSetUp.TestFramework = null;
        Environment.SetEnvironmentVariable("UnitTestEx__TestFramework", "unknown");
        try
        {
            TestFrameworkImplementor.Create();
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
        finally
        {
            Environment.SetEnvironmentVariable("UnitTestEx__TestFramework", previousSetting);
            TestSetUp.TestFramework = null;
        }
    }
}
