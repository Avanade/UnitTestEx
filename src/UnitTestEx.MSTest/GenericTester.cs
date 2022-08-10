// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

namespace UnitTestEx.MSTest
{
    /// <summary>
    /// Provides the <b>MSTest</b> generic testing capability.
    /// </summary>
    public static class GenericTester
    {
        /// <summary>
        /// Creates a new instance of the <see cref="GenericTester"/> class.
        /// </summary>
        /// <param name="username">The username (<c>null</c> indicates to use the existing <see cref="CoreEx.ExecutionContext.Current"/> <see cref="CoreEx.ExecutionContext.Username"/> where configured).</param>
        /// <returns>The <see cref="GenericTester"/>.</returns>
        public static Internal.GenericTester Create(string? username = null) => new(username);
    }
}