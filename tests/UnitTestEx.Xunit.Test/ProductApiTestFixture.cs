using System;

namespace UnitTestEx.Xunit.Test
{
    public class ProductApiTestFixture<TStartup> : ApiTestFixture<TStartup> where TStartup : class
    {
        private static int _counter = 0;

        protected override void OnConfiguration()
        {
            Test.ReplaceSingleton<TestSomeSome>();
            MockHttpClientFactory.Create();

            _counter++;
            if (_counter > 1)
            {
                throw new InvalidOperationException("ProductApiTestFixture should only be instantiated once per test run.");
            }
        }

        public class TestSomeSome { }
    }
}