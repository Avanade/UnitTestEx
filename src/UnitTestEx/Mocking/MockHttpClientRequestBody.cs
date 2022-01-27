// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

namespace UnitTestEx.Mocking
{
    /// <summary>
    /// Represents the result of adding a body to the <see cref="MockHttpClientRequest"/> and to <see cref="Respond"/> accordingly.
    /// </summary>
    public class MockHttpClientRequestBody
    {
        private readonly MockHttpClientRequestRule _rule;

        /// <summary>
        /// Initializes a new <see cref="MockHttpClientRequestBody"/>.
        /// </summary>
        /// <param name="rule">The <see cref="MockHttpClientRequestRule"/>.</param>
        internal MockHttpClientRequestBody(MockHttpClientRequestRule rule) => _rule = rule;

        /// <summary>
        /// Gets the <see cref="MockHttpClientResponse"/>.
        /// </summary>
        public MockHttpClientResponse Respond => _rule.Response!;
    }
}