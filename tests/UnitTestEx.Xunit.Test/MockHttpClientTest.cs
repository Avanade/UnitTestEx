using Moq;
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using UnitTestEx.Xunit.Test.Model;
using Xunit;
using Xunit.Abstractions;

namespace UnitTestEx.Xunit.Test
{
    public class MockHttpClientTest : UnitTestBase
    {
        public MockHttpClientTest(ITestOutputHelper output) : base(output) { }

        [Fact]
        public async Task UriOnly_Single()
        {
            var mcf = MockHttpClientFactory.Create();
            mcf.CreateClient("XXX", new Uri("https://d365test")).Request(HttpMethod.Get, "products/xyz").Respond.With(HttpStatusCode.NotFound);

            var hc = mcf.GetHttpClient("XXX");
            var res = await hc.GetAsync("products/xyz");
            Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);

            res = await hc.GetAsync("products/xyz");
            Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        }

        [Fact]
        public async Task UriOnly_Multi()
        {
            var mcf = MockHttpClientFactory.Create();
            var mc = mcf.CreateClient("XXX", new Uri("https://d365test"));
            mc.Request(HttpMethod.Get, "products/xyz").Respond.With(HttpStatusCode.NotFound);
            mc.Request(HttpMethod.Get, "products/abc").Respond.With(HttpStatusCode.NoContent);

            var hc = mcf.GetHttpClient("XXX");
            var res = await hc.GetAsync("products/xyz");
            Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);

            res = await hc.GetAsync("products/abc");
            Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        }

        [Fact]
        public async Task UriAndBody_String_Single()
        {
            var mcf = MockHttpClientFactory.Create();
            mcf.CreateClient("XXX", new Uri("https://d365test")).Request(HttpMethod.Post, "products/xyz").WithBody("Bananas").Respond.With(HttpStatusCode.Accepted);

            var hc = mcf.GetHttpClient("XXX");
            var res = await hc.PostAsync("products/xyz", new StringContent("Bananas"));
            Assert.Equal(HttpStatusCode.Accepted, res.StatusCode);
        }

        [Fact]
        public async Task UriAndBody_String_Multi()
        {
            var mcf = MockHttpClientFactory.Create();
            var mc = mcf.CreateClient("XXX", new Uri("https://d365test"));
            mc.Request(HttpMethod.Post, "products/xyz").WithBody("Bananas").Respond.With(HttpStatusCode.Accepted);
            mc.Request(HttpMethod.Post, "products/xyz").WithBody("Apples").Respond.With(HttpStatusCode.NoContent);

            var hc = mcf.GetHttpClient("XXX");
            var res = await hc.PostAsync("products/xyz", new StringContent("Bananas"));
            Assert.Equal(HttpStatusCode.Accepted, res.StatusCode);

            res = await hc.PostAsync("products/xyz", new StringContent("Apples"));
            Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        }

        [Fact]
        public async Task UriAndBody_Invalid()
        {
            var mcf = MockHttpClientFactory.Create();
            mcf.CreateClient("XXX", new Uri("https://d365test")).Request(HttpMethod.Post, "products/xyz").WithBody("Bananas").Respond.With(HttpStatusCode.Accepted);

            var hc = mcf.GetHttpClient("XXX");

            await Assert.ThrowsAsync<MockHttpClientException>(() => hc.PostAsync("products/xyz", new StringContent("Apples")));
        }

        [Fact]
        public async Task UriAndBody_Json_Single()
        {
            var mcf = MockHttpClientFactory.Create();
            mcf.CreateClient("XXX", new Uri("https://d365test")).Request(HttpMethod.Post, "products/xyz").WithJsonBody(new Person { FirstName = "Bob", LastName = "Jane" }).Respond.With(HttpStatusCode.Accepted);

            var hc = mcf.GetHttpClient("XXX");
            var res = await hc.PostAsJsonAsync("products/xyz", new Person { LastName = "Jane", FirstName = "Bob" });
            Assert.Equal(HttpStatusCode.Accepted, res.StatusCode);
        }

        [Fact]
        public async Task UriAndBody_Json_Multi()
        {
            var mcf = MockHttpClientFactory.Create();
            var mc = mcf.CreateClient("XXX", new Uri("https://d365test"));
            mc.Request(HttpMethod.Post, "products/xyz").WithJsonBody("{ \"firstName\": \"Bob\", \"lastName\": \"Jane\" }").Respond.With(HttpStatusCode.Accepted);
            mc.Request(HttpMethod.Post, "products/xyz").WithJsonBody(new Person { FirstName = "Jenny", LastName = "Browne" }).Respond.With(HttpStatusCode.OK);

            var hc = mcf.GetHttpClient("XXX");
            var res = await hc.PostAsJsonAsync("products/xyz", new Person { LastName = "Jane", FirstName = "Bob" });
            Assert.Equal(HttpStatusCode.Accepted, res.StatusCode);

            res = await hc.PostAsync("products/xyz", new StringContent("{ \"firstName\": \"Jenny\", \"lastName\": \"Browne\" }", Encoding.UTF8, MediaTypeNames.Application.Json));
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        }

        [Fact]
        public async Task UriAndBody_WithJsonResponse()
        {
            var mcf = MockHttpClientFactory.Create();
            mcf.CreateClient("XXX", new Uri("https://d365test"))
                .Request(HttpMethod.Post, "products/xyz").WithJsonBody(new Person { FirstName = "Bob", LastName = "Jane" })
                .Respond.WithJson(new Person2 { First = "Bob", Last = "Jane" }, HttpStatusCode.Accepted);

            var hc = mcf.GetHttpClient("XXX");
            var res = await hc.PostAsJsonAsync("products/xyz", new Person { LastName = "Jane", FirstName = "Bob" });
            Assert.Equal(HttpStatusCode.Accepted, res.StatusCode);
            Assert.Equal("{\"first\":\"Bob\",\"last\":\"Jane\"}", await res.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task UriAndBody_WithJsonResponse2()
        {
            var mcf = MockHttpClientFactory.Create();
            mcf.CreateClient("XXX", new Uri("https://d365test"))
                .Request(HttpMethod.Post, "products/xyz").WithJsonBody(new Person { FirstName = "Bob", LastName = "Jane" })
                .Respond.WithJson("{\"first\":\"Bob\",\"last\":\"Jane\"}", HttpStatusCode.Accepted);

            var hc = mcf.GetHttpClient("XXX");
            var res = await hc.PostAsJsonAsync("products/xyz", new Person { LastName = "Jane", FirstName = "Bob" });
            Assert.Equal(HttpStatusCode.Accepted, res.StatusCode);
            Assert.Equal("{\"first\":\"Bob\",\"last\":\"Jane\"}", await res.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task UriAndBody_WithJsonResponse3()
        {
            var mcf = MockHttpClientFactory.Create();
            mcf.CreateClient("XXX", new Uri("https://d365test"))
                .Request(HttpMethod.Post, "products/xyz").WithJsonBody(new Person { FirstName = "Bob", LastName = "Jane" })
                .Respond.WithJsonResource("MockHttpClientTest-UriAndBody_WithJsonResponse3.json", HttpStatusCode.Accepted);

            var hc = mcf.GetHttpClient("XXX");
            var res = await hc.PostAsJsonAsync("products/xyz", new Person { LastName = "Jane", FirstName = "Bob" });
            Assert.Equal(HttpStatusCode.Accepted, res.StatusCode);
            Assert.Equal("{\"first\":\"Bob\",\"last\":\"Jane\"}", await res.Content.ReadAsStringAsync());
        }

        [Fact]
        public void VerifyMock_NotExecuted()
        {
            var mcf = MockHttpClientFactory.Create();
            mcf.CreateClient("XXX", new Uri("https://d365test"))
                .Request(HttpMethod.Post, "products/xyz").WithJsonBody(new Person { FirstName = "Bob", LastName = "Jane" })
                .Respond.WithJsonResource("MockHttpClientTest-UriAndBody_WithJsonResponse3.json", HttpStatusCode.Accepted);

            try
            {
                mcf.VerifyAll();
                throw new InvalidOperationException();
            }
            catch (MockHttpClientException mhcex)
            {
                Assert.Equal("The request was invoked 0 times; expected AtLeastOnce. Request: <XXX> POST https://d365test/products/xyz {\"firstName\":\"Bob\",\"lastName\":\"Jane\"} (application/json)", mhcex.Message);
            }
        }

        [Fact]
        public async Task VerifyMock_NotExecuted_Multi()
        {
            var mcf = MockHttpClientFactory.Create();
            var mc = mcf.CreateClient("XXX", new Uri("https://d365test"));
            mc.Request(HttpMethod.Post, "products/xyz").WithJsonBody(new Person { FirstName = "Bob", LastName = "Jane" })
                .Respond.WithJsonResource("MockHttpClientTest-UriAndBody_WithJsonResponse3.json", HttpStatusCode.Accepted);

            mc.Request(HttpMethod.Post, "products/abc").WithJsonBody(new Person { FirstName = "David", LastName = "Jane" })
                .Respond.WithJsonResource("MockHttpClientTest-UriAndBody_WithJsonResponse3.json", HttpStatusCode.Accepted);

            try
            {
                var hc = mcf.GetHttpClient("XXX");
                var res = await hc.PostAsJsonAsync("products/xyz", new Person { LastName = "Jane", FirstName = "Bob" });
                Assert.Equal(HttpStatusCode.Accepted, res.StatusCode);

                mcf.VerifyAll();
                throw new InvalidOperationException();
            }
            catch (MockHttpClientException mhcex)
            {
                Assert.Equal("The request was invoked 0 times; expected AtLeastOnce. Request: <XXX> POST https://d365test/products/abc {\"firstName\":\"David\",\"lastName\":\"Jane\"} (application/json)", mhcex.Message);
            }
        }

        [Fact]
        public async Task VerifyMock_NotExecuted_Times()
        {
            var mcf = MockHttpClientFactory.Create();
            var mc = mcf.CreateClient("XXX", new Uri("https://d365test"));
            mc.Request(HttpMethod.Post, "products/xyz").Times(Times.Exactly(2)).WithJsonBody(new Person { FirstName = "Bob", LastName = "Jane" })
                .Respond.WithJsonResource("MockHttpClientTest-UriAndBody_WithJsonResponse3.json", HttpStatusCode.Accepted);

            try
            {
                var hc = mcf.GetHttpClient("XXX");
                var res = await hc.PostAsJsonAsync("products/xyz", new Person { LastName = "Jane", FirstName = "Bob" });
                Assert.Equal(HttpStatusCode.Accepted, res.StatusCode);
                res = await hc.PostAsJsonAsync("products/xyz", new Person { LastName = "Jane", FirstName = "Bob" });
                Assert.Equal(HttpStatusCode.Accepted, res.StatusCode);
                res = await hc.PostAsJsonAsync("products/xyz", new Person { LastName = "Jane", FirstName = "Bob" });
                Assert.Equal(HttpStatusCode.Accepted, res.StatusCode);

                mcf.VerifyAll();
                throw new InvalidOperationException();
            }
            catch (MockHttpClientException mhcex)
            {
                Assert.Equal("The request was invoked 3 times; expected Exactly(2). Request: <XXX> POST https://d365test/products/xyz {\"firstName\":\"Bob\",\"lastName\":\"Jane\"} (application/json)", mhcex.Message);
            }
        }

        [Fact]
        public async Task VerifyMock_Executed()
        {
            var mcf = MockHttpClientFactory.Create();
            mcf.CreateClient("XXX", new Uri("https://d365test"))
                .Request(HttpMethod.Post, "products/xyz").WithJsonBody(new Person { FirstName = "Bob", LastName = "Jane" })
                .Respond.WithJsonResource("MockHttpClientTest-UriAndBody_WithJsonResponse3.json", HttpStatusCode.Accepted);

            var hc = mcf.GetHttpClient("XXX");
            await hc.PostAsJsonAsync("products/xyz", new Person { LastName = "Jane", FirstName = "Bob" });

            mcf.VerifyAll();
        }

        [Fact]
        public async Task UriAndAnyBody()
        {
            var mcf = MockHttpClientFactory.Create();
            mcf.CreateClient("XXX", new Uri("https://d365test"))
                .Request(HttpMethod.Post, "products/xyz").WithAnyBody()
                .Respond.WithJson("{\"first\":\"Bob\",\"last\":\"Jane\"}", HttpStatusCode.Accepted);

            var hc = mcf.GetHttpClient("XXX");
            var res = await hc.PostAsJsonAsync("products/xyz", new Person { LastName = "Jane", FirstName = "Bob" });
            Assert.Equal(HttpStatusCode.Accepted, res.StatusCode);
            Assert.Equal("{\"first\":\"Bob\",\"last\":\"Jane\"}", await res.Content.ReadAsStringAsync());

            await Assert.ThrowsAsync<MockHttpClientException>(async () => await hc.SendAsync(new HttpRequestMessage(HttpMethod.Post, "products/xyz")));
        }

        [Fact]
        public async Task MockSequence_Body()
        {
            var mcf = MockHttpClientFactory.Create();
            var mc = mcf.CreateClient("XXX", new Uri("https://d365test"));
            mc.Request(HttpMethod.Post, "products/xyz").WithAnyBody().Respond.WithSequence(s =>
            {
                s.Respond().With(HttpStatusCode.Accepted);
                s.Respond().With(HttpStatusCode.OK);
            });

            var hc = mcf.GetHttpClient("XXX");
            var res = await hc.PostAsJsonAsync("products/xyz", new Person { LastName = "Jane", FirstName = "Bob" });
            Assert.Equal(HttpStatusCode.Accepted, res.StatusCode);

            res = await hc.PostAsJsonAsync("products/xyz", new Person { LastName = "Jane", FirstName = "Bob" });
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        }


        [Fact]
        public async Task MockSequence()
        {
            var mcf = MockHttpClientFactory.Create();
            var mc = mcf.CreateClient("XXX", new Uri("https://d365test"));
            mc.Request(HttpMethod.Get, "products/xyz").Respond.WithSequence(s =>
            {
                s.Respond().With(HttpStatusCode.NotModified);
                s.Respond().With(HttpStatusCode.NotFound);
            });

            var hc = mcf.GetHttpClient("XXX");
            var res = await hc.GetAsync("products/xyz");
            Assert.Equal(HttpStatusCode.NotModified, res.StatusCode);

            res = await hc.GetAsync("products/xyz");
            Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        }

        [Fact]
        public async Task MockDelay()
        {
            var mcf = MockHttpClientFactory.Create();
            mcf.CreateClient("XXX", new Uri("https://d365test")).Request(HttpMethod.Get, "products/xyz").Respond.Delay(500).With(HttpStatusCode.NotFound);

            var hc = mcf.GetHttpClient("XXX");

            var sw = Stopwatch.StartNew();
            var res = await hc.GetAsync("products/xyz");
            sw.Stop();

            Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
            Assert.True(sw.ElapsedMilliseconds >= 495, $"Actual elapsed milliseconds {sw.ElapsedMilliseconds}.");
        }

        [Fact]
        public async Task MockSequenceDelay()
        {
            var mcf = MockHttpClientFactory.Create();
            var mc = mcf.CreateClient("XXX", new Uri("https://d365test"));
            mc.Request(HttpMethod.Get, "products/xyz").Respond.WithSequence(s =>
            {
                s.Respond().Delay(250).With(HttpStatusCode.NotModified);
                s.Respond().Delay(100, 150).With(HttpStatusCode.NotFound);
            });

            var hc = mcf.GetHttpClient("XXX");

            var sw = Stopwatch.StartNew();
            var res = await hc.GetAsync("products/xyz");
            sw.Stop();
            Assert.Equal(HttpStatusCode.NotModified, res.StatusCode);
            Assert.True(sw.ElapsedMilliseconds >= 245, $"Actual elapsed milliseconds {sw.ElapsedMilliseconds}.");

            sw.Restart();
            res = await hc.GetAsync("products/xyz");
            sw.Stop();
            Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
            Assert.True(sw.ElapsedMilliseconds >= 95, $"Actual elapsed milliseconds {sw.ElapsedMilliseconds}.");
        }

        [Fact]
        public async Task UriAndBody_WithXmlRequest()
        {
            var mcf = MockHttpClientFactory.Create();
            mcf.CreateClient("XXX", new Uri("https://d365test"))
                .Request(HttpMethod.Post, "products/xyz").WithBody("<Person xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns=\"http://schemas.datacontract.org/2004/07/UnitTestEx.Xunit.Test.Model\"><FirstName>Bob</FirstName><LastName>Jane</LastName></Person>", MediaTypeNames.Application.Xml)
                .Respond.With(new StringContent("<person><first>Bob</first><last>Jane</last></person>", Encoding.UTF8, MediaTypeNames.Application.Xml), HttpStatusCode.Accepted);

            var hc = mcf.GetHttpClient("XXX");
            var res = await hc.PostAsXmlAsync("products/xyz", new Person { LastName = "Jane", FirstName = "Bob" });
            Assert.Equal(HttpStatusCode.Accepted, res.StatusCode);
            Assert.Equal("<person><first>Bob</first><last>Jane</last></person>", await res.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task UriAndBody_WithAnyTypeRequest()
        {
            var mcf = MockHttpClientFactory.Create();
            mcf.CreateClient("XXX", new Uri("https://d365test"))
                .Request(HttpMethod.Post, "testing").WithBody("--my--custom--format--", "application/custom-format")
                .Respond.With(new StringContent("--ok--", Encoding.UTF8, "application/custom-format"), HttpStatusCode.Accepted);

            var hc = mcf.GetHttpClient("XXX");
            var res = await hc.PostAsync("testing", new StringContent("--my--custom--format--", Encoding.UTF8, "application/custom-format"));
            Assert.Equal(HttpStatusCode.Accepted, res.StatusCode);
            Assert.Equal("--ok--", await res.Content.ReadAsStringAsync());
        }
    }
}