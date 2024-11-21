using Microsoft.Extensions.DependencyInjection;
using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using UnitTestEx.Function;
using UnitTestEx.Json;
using Xunit;
using Xunit.Abstractions;

namespace UnitTestEx.Xunit.Test
{
    public class ServiceBusFunctionTest : UnitTestBase
    {
        public ServiceBusFunctionTest(ITestOutputHelper output) : base(output) { }

        [Fact]
        public void Object_Success()
        {
            var mcf = MockHttpClientFactory.Create();
            mcf.CreateClient("XXX", new Uri("https://somesys"))
                .Request(HttpMethod.Post, "person").WithJsonBody(new { firstName = "Bob", lastName = "Smith" }).Respond.With(HttpStatusCode.OK);

            using var test = FunctionTester.Create<Startup>();
            test.ReplaceHttpClientFactory(mcf)
                .ServiceBusTrigger<ServiceBusFunction>()
                .Run(f => f.Run(new Person { FirstName = "Bob", LastName = "Smith" }, test.Logger))
                .AssertSuccess();

            mcf.VerifyAll();
        }

        [Fact]
        public void Object_HttpClientError()
        {
            var mcf = MockHttpClientFactory.Create().UseJsonComparerOptions(new JsonElementComparerOptions { NullComparison = JsonElementComparison.Semantic });
            mcf.CreateClient("XXX", new Uri("https://somesys"))
                .Request(HttpMethod.Post, "person").WithJsonBody(new { firstName = "Bob", lastName = (string)null }).Respond.With(HttpStatusCode.InternalServerError);

            using var test = FunctionTester.Create<Startup>();
            var r = test.ReplaceHttpClientFactory(mcf)
                .Type<ServiceBusFunction>()
                .Run(f => f.Run(new Person { FirstName = "Bob", LastName = (string)null }, test.Logger))
                .AssertException<HttpRequestException>("Response status code does not indicate success: 500 (Internal Server Error).");

            mcf.VerifyAll();
        }

        [Fact]
        public void Object_ThrowsException()
        {
            var mcf = MockHttpClientFactory.Create();

            using var test = FunctionTester.Create<Startup>();
            test.ReplaceHttpClientFactory(mcf)
                .ServiceBusTrigger<ServiceBusFunction>()
                .Run(f => f.Run(new Person { FirstName = null, LastName = "Smith" }, test.Logger))
                .AssertException<InvalidOperationException>("First name is required.");

            mcf.VerifyAll();
        }

        [Fact]
        public void ServiceBusMessage_Success()
        {
            var mcf = MockHttpClientFactory.Create();
            mcf.CreateClient("XXX", new Uri("https://somesys"))
                .Request(HttpMethod.Post, "person").WithJsonBody(new { firstName = "Bob", lastName = "Smith" }).Respond.With(HttpStatusCode.OK);

            using var test = FunctionTester.Create<Startup>();
            test.ReplaceHttpClientFactory(mcf)
                .ServiceBusTrigger<ServiceBusFunction>()
                .Run(f => f.Run2(test.CreateServiceBusMessageFromValue(new Person { FirstName = "Bob", LastName = "Smith" }), test.Logger))
                .AssertSuccess();

            mcf.VerifyAll();
        }

        [Fact]
        public void ServiceBusMessage_HttpClientError()
        {
            var mcf = MockHttpClientFactory.Create().UseJsonSerializer(new Json.JsonSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            mcf.CreateClient("XXX", new Uri("https://somesys"))
                .Request(HttpMethod.Post, "person").WithJsonBody(new { firstName = "Bob", lastName = (string)null }).Respond.With(HttpStatusCode.InternalServerError);

            using var test = FunctionTester.Create<Startup>();
            test.ReplaceHttpClientFactory(mcf)
                .ServiceBusTrigger<ServiceBusFunction>()
                .Run(f => f.Run2(test.CreateServiceBusMessageFromValue(new Person { FirstName = "Bob", LastName = (string)null }), test.Logger))
                .AssertException<HttpRequestException>("Response status code does not indicate success: 500 (Internal Server Error).");

            mcf.VerifyAll();
        }

        [Fact]
        public void ServiceBusMessage_ThrowsException()
        {
            var mcf = MockHttpClientFactory.Create();

            using var test = FunctionTester.Create<Startup>();
            test.ReplaceHttpClientFactory(mcf)
                .ServiceBusTrigger<ServiceBusFunction>()
                .Run(f => f.Run2(test.CreateServiceBusMessageFromValue(new Person { FirstName = null, LastName = "Smith" }), test.Logger))
                .AssertException<InvalidOperationException>("First name is required.");

            mcf.VerifyAll();
        }

        [Fact]
        public async Task ServiceProvider()
        {
            var mcf = MockHttpClientFactory.Create();
            mcf.CreateClient("XXX", new Uri("https://somesys")).Request(HttpMethod.Get, "test").Respond.With("test output");

            using var test = FunctionTester.Create<Startup>();
            var hc = test.ReplaceHttpClientFactory(mcf)
                .Services.GetService<IHttpClientFactory>().CreateClient("XXX");

            var r = await hc.GetAsync("test");
            Assert.NotNull(r);
            Assert.Equal("test output", await r.Content.ReadAsStringAsync());
        }
    }
}