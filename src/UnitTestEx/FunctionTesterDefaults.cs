// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using UnitTestEx.Functions;

namespace UnitTestEx
{
    /// <summary>
    /// Provides <see cref="FunctionTesterBase{TEntryPoint, TSelf}"/> defaults.
    /// </summary>
    public static class FunctionTesterDefaults
    {
        /// <summary>
        /// Indicates whether to include '<c>appsettings.unittest.json</c>' configuration file when the <see cref="FunctionTesterBase{TEntryPoint, TSelf}"/> host starts; defaults to <c>true</c>.
        /// </summary>
        public static bool IncludeUnitTestConfiguration { get; set; } = true;

        /// <summary>
        /// Indicates whether to include user secrets configuration when the <see cref="FunctionTesterBase{TEntryPoint, TSelf}"/> host starts.
        /// </summary>
        public static bool IncludeUserSecrets { get; set; }
    }
}