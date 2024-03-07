// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

namespace UnitTestEx.Mocking
{
    /// <summary>
    /// Mocks the <see cref="MockHttpClientResponse"/> within a sequence.
    /// </summary>
    public sealed class MockHttpClientResponseSequence
    {
        private readonly MockHttpClientRequest _clientRequest;
        private readonly MockHttpClientRequestRule _rule;

        /// <summary>
        /// Initializes a new instance of the <see cref="MockHttpClientResponseSequence"/> class.
        /// </summary>
        /// <param name="clientRequest">The <see cref="MockHttpClientRequest"/>.</param>
        /// <param name="rule">The <see cref="MockHttpClientRequestRule"/>.</param>
        internal MockHttpClientResponseSequence(MockHttpClientRequest clientRequest, MockHttpClientRequestRule rule)
        {
            _clientRequest = clientRequest;
            _rule = rule;
        }

        /// <summary>
        /// Adds the next <see cref="MockHttpClientResponse"/> in sequence.
        /// </summary>
        /// <returns>The next <see cref="MockHttpClientResponse"/> in sequence.</returns>
        public MockHttpClientResponse Respond()
        {
            var resp = new MockHttpClientResponse(_clientRequest, null);
            _rule.Responses!.Add(resp);
            return resp;
        }
    }
}