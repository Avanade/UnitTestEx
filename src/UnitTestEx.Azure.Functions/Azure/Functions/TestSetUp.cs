// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx


// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

namespace UnitTestEx.Azure.Functions
{
    /// <summary>
    /// Extends the <see cref="UnitTestEx.TestSetUp"/>.
    /// </summary>
    public static class TestSetUp
    {
        /// <summary>
        /// Indicates whether to include '<c>appsettings.unittest.json</c>' configuration file when the <see cref="FunctionTesterBase{TEntryPoint, TSelf}"/> host starts; defaults to <c>true</c>.
        /// </summary>
        public static bool FunctionTesterIncludeUnitTestConfiguration { get; set; } = true;

        /// <summary>
        /// Indicates whether to include user secrets configuration when the <see cref="FunctionTesterBase{TEntryPoint, TSelf}"/> host starts; defaults to <c>false</c>.
        /// </summary>
        public static bool FunctionTesterIncludeUserSecrets { get; set; }
    }
}