// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using UnitTestEx.MSTest.Internal;

namespace UnitTestEx.MSTest
{
    /// <summary>
    /// Provides the <b>MSTest</b> <see cref="Mocking.MockHttpClientFactory"/> implementation.
    /// </summary>
    public static class MockHttpClientFactory
    {
        /// <summary>
        /// Creates the <see cref="Mocking.MockHttpClientFactory"/>.
        /// </summary>
        /// <returns>The <see cref="Mocking.MockHttpClientFactory"/>.</returns>
        public static Mocking.MockHttpClientFactory Create() => new(new MSTestImplementor());
    }
}