// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using CoreEx.Validation;

namespace UnitTestEx.MSTest
{
    /// <summary>
    /// Provides the <b>MSTest</b> <see cref="IValidator"/> testing capability.
    /// </summary>
    public static class ValidationTester
    {
        /// <summary>
        /// Creates a new instance of the <see cref="ValidationTester"/> class.
        /// </summary>
        /// <param name="username">The username (<c>null</c> indicates to use the existing <see cref="CoreEx.ExecutionContext.Current"/> <see cref="CoreEx.ExecutionContext.Username"/> where configured).</param>
        /// <returns>The <see cref="ValidationTester"/>.</returns>
        public static Internal.ValidationTester Create(string? username = null) => new(username);
    }
}