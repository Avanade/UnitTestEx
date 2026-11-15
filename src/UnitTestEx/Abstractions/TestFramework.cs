// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

namespace UnitTestEx.Abstractions
{
    /// <summary>
    /// Identifies a test framework for explicit framework selection.
    /// </summary>
    public enum TestFramework
    {
        /// <summary>Selects xUnit.net v2.</summary>
        Xunit2,

        /// <summary>Selects xUnit.net v3.</summary>
        Xunit3,

        /// <summary>Selects the loaded NUnit version.</summary>
        NUnit,

        /// <summary>Selects NUnit v4.</summary>
        NUnit4,

        /// <summary>Selects NUnit v5.</summary>
        NUnit5,

        /// <summary>Selects MSTest v3.</summary>
        MSTest3,

        /// <summary>Selects MSTest v4.</summary>
        MSTest4
    }
}
