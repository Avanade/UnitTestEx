using Moq;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using UnitTestEx.NUnit.Test.Model;
using UnitTestEx.NUnit;
using NUnit.Framework;
using System.Diagnostics;
using System.Linq;

namespace UnitTestEx.NUnit.Test
{
    [TestFixture]
    public class MockHttpClientTest
    {
        [Test]
        public async Task UriOnly_Single()
        {
            var mcf = MockHttpClientFactory.Create();
            mcf.CreateClient("XXX", new Uri("https://d365test")).Request(HttpMethod.Get, "products/xyz").Respond.With(HttpStatusCode.NotFound);

            var hc = mcf.GetHttpClient("XXX");
            var res = await hc.GetAsync("products/xyz").ConfigureAwait(false);
            Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

            res = await hc.GetAsync("products/xyz").ConfigureAwait(false);
            Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

        [Test]
        public async Task UriOnly_Encoded()
        {
            var mcf = MockHttpClientFactory.Create();
            mcf.CreateClient("XXX", new Uri("https://d365test")).Request(HttpMethod.Get, "person/xyz?search=email eq \"bob@email.com\"").Respond.With(HttpStatusCode.NotFound);

            var hc = mcf.GetHttpClient("XXX");
            var res = await hc.GetAsync("person/xyz?search=email eq \"bob@email.com\"").ConfigureAwait(false);
            Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

            res = await hc.GetAsync("person/xyz?search=email%20eq%20%22bob@email.com%22").ConfigureAwait(false);
            Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

        [Test]
        public async Task UriOnly_Multi()
        {
            var mcf = MockHttpClientFactory.Create();
            var mc = mcf.CreateClient("XXX", new Uri("https://d365test"));
            mc.Request(HttpMethod.Get, "products/xyz").Respond.With(HttpStatusCode.NotFound);
            mc.Request(HttpMethod.Get, "products/abc").Respond.With(HttpStatusCode.NoContent);

            var hc = mcf.GetHttpClient("XXX");
            var res = await hc.GetAsync("products/xyz").ConfigureAwait(false);
            Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

            res = await hc.GetAsync("products/abc").ConfigureAwait(false);
            Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        }

        [Test]
        public async Task UriAndBody_String_Single()
        {
            var mcf = MockHttpClientFactory.Create();
            mcf.CreateClient("XXX", new Uri("https://d365test")).Request(HttpMethod.Post, "products/xyz").WithBody("Bananas").Respond.With(HttpStatusCode.Accepted);

            var hc = mcf.GetHttpClient("XXX");
            var res = await hc.PostAsync("products/xyz", new StringContent("Bananas")).ConfigureAwait(false);
            Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));
        }

        [Test]
        public async Task UriAndBody_String_Multi()
        {
            var mcf = MockHttpClientFactory.Create();
            var mc = mcf.CreateClient("XXX", new Uri("https://d365test"));
            mc.Request(HttpMethod.Post, "products/xyz").WithBody("Bananas").Respond.With(HttpStatusCode.Accepted);
            mc.Request(HttpMethod.Post, "products/xyz").WithBody("Apples").Respond.With(HttpStatusCode.NoContent);

            var hc = mcf.GetHttpClient("XXX");
            var res = await hc.PostAsync("products/xyz", new StringContent("Bananas")).ConfigureAwait(false);
            Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));

            res = await hc.PostAsync("products/xyz", new StringContent("Apples")).ConfigureAwait(false);
            Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        }

        [Test]
        public void UriAndBody_Invalid()
        {
            var mcf = MockHttpClientFactory.Create();
            mcf.CreateClient("XXX", new Uri("https://d365test")).Request(HttpMethod.Post, "products/xyz").WithBody("Bananas").Respond.With(HttpStatusCode.Accepted);

            var hc = mcf.GetHttpClient("XXX");

            Assert.ThrowsAsync<MockHttpClientException>(() => hc.PostAsync("products/xyz", new StringContent("Apples")));
        }

        [Test]
        public async Task UriAndBody_Json_Single()
        {
            var mcf = MockHttpClientFactory.Create();
            mcf.CreateClient("XXX", new Uri("https://d365test")).Request(HttpMethod.Post, "products/xyz").WithJsonBody(new Person { FirstName = "Bob", LastName = "Jane" }).Respond.With(HttpStatusCode.Accepted);

            var hc = mcf.GetHttpClient("XXX");
            var res = await hc.PostAsJsonAsync("products/xyz", new Person { LastName = "Jane", FirstName = "Bob" }).ConfigureAwait(false);
            Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));
        }

        [Test]
        public async Task UriAndBody_Json_Multi()
        {
            var mcf = MockHttpClientFactory.Create();
            var mc = mcf.CreateClient("XXX", new Uri("https://d365test"));
            mc.Request(HttpMethod.Post, "products/xyz").WithJsonBody("{ \"firstName\": \"Bob\", \"lastName\": \"Jane\" }").Respond.With(HttpStatusCode.Accepted);
            mc.Request(HttpMethod.Post, "products/xyz").WithJsonBody(new Person { FirstName = "Jenny", LastName = "Browne" }).Respond.With(HttpStatusCode.OK);

            var hc = mcf.GetHttpClient("XXX");
            var res = await hc.PostAsJsonAsync("products/xyz", new Person { LastName = "Jane", FirstName = "Bob" }).ConfigureAwait(false);
            Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));

            res = await hc.PostAsync("products/xyz", new StringContent("{ \"firstName\": \"Jenny\", \"lastName\": \"Browne\" }", Encoding.UTF8, MediaTypeNames.Application.Json)).ConfigureAwait(false);
            Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        [Test]
        public async Task UriAndBody_WithJsonResponse()
        {
            var mcf = MockHttpClientFactory.Create();
            mcf.CreateClient("XXX", new Uri("https://d365test"))
                .Request(HttpMethod.Post, "products/xyz").WithJsonBody(new Person { FirstName = "Bob", LastName = "Jane" })
                .Respond.WithJson(new Person2 { First = "Bob", Last = "Jane" }, HttpStatusCode.Accepted);

            var hc = mcf.GetHttpClient("XXX");
            var res = await hc.PostAsJsonAsync("products/xyz", new Person { LastName = "Jane", FirstName = "Bob" }).ConfigureAwait(false);
            Assert.Multiple(async () =>
            {
                Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));
                Assert.That(await res.Content.ReadAsStringAsync().ConfigureAwait(false), Is.EqualTo("{\"first\":\"Bob\",\"last\":\"Jane\"}"));
            });
        }

        [Test]
        public async Task UriAndBody_WithJsonResponse2()
        {
            var mcf = MockHttpClientFactory.Create();
            mcf.CreateClient("XXX", new Uri("https://d365test"))
                .Request(HttpMethod.Post, "products/xyz").WithJsonBody(new Person { FirstName = "Bob", LastName = "Jane" })
                .Respond.WithJson("{\"first\":\"Bob\",\"last\":\"Jane\"}", HttpStatusCode.Accepted);

            var hc = mcf.GetHttpClient("XXX");
            var res = await hc.PostAsJsonAsync("products/xyz", new Person { LastName = "Jane", FirstName = "Bob" }).ConfigureAwait(false);
            Assert.Multiple(async () =>
            {
                Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));
                Assert.That(await res.Content.ReadAsStringAsync().ConfigureAwait(false), Is.EqualTo("{\"first\":\"Bob\",\"last\":\"Jane\"}"));
            });
        }

        [Test]
        public async Task UriAndBody_WithJsonResponse3()
        {
            var mcf = MockHttpClientFactory.Create();
            mcf.CreateClient("XXX", new Uri("https://d365test"))
                .Request(HttpMethod.Post, "products/xyz").WithJsonBody(new Person { FirstName = "Bob", LastName = "Jane" })
                .Respond.WithJsonResource("MockHttpClientTest-UriAndBody_WithJsonResponse3.json", HttpStatusCode.Accepted);

            var hc = mcf.GetHttpClient("XXX");
            var res = await hc.PostAsJsonAsync("products/xyz", new Person { LastName = "Jane", FirstName = "Bob" }).ConfigureAwait(false);
            Assert.Multiple(async () =>
            {
                Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));
                Assert.That(await res.Content.ReadAsStringAsync().ConfigureAwait(false), Is.EqualTo("{\"first\":\"Bob\",\"last\":\"Jane\"}"));
            });
        }

        [Test]
        public void VerifyMock_NotExecuted()
        {
            var mcf = MockHttpClientFactory.Create();
            mcf.CreateClient("XXX", new Uri("https://d365test"))
                .Request(HttpMethod.Post, "products/xyz").WithJsonBody(new Person { FirstName = "Bob", LastName = "Jane" })
                .Respond.WithJsonResource("MockHttpClientTest-UriAndBody_WithJsonResponse3.json", HttpStatusCode.Accepted);

            try
            {
                mcf.VerifyAll();
                Assert.Fail();
            }
            catch (MockHttpClientException mhcex)
            {
                Assert.That(mhcex.Message, Is.EqualTo("The request was invoked 0 times; expected AtLeastOnce. Request: <XXX> POST https://d365test/products/xyz {\"firstName\":\"Bob\",\"lastName\":\"Jane\"} (application/json)"));
            }
        }

        [Test]
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
                var res = await hc.PostAsJsonAsync("products/xyz", new Person { LastName = "Jane", FirstName = "Bob" }).ConfigureAwait(false);
                Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));

                mcf.VerifyAll();
                Assert.Fail();
            }
            catch (MockHttpClientException mhcex)
            {
                Assert.That(mhcex.Message, Is.EqualTo("The request was invoked 0 times; expected AtLeastOnce. Request: <XXX> POST https://d365test/products/abc {\"firstName\":\"David\",\"lastName\":\"Jane\"} (application/json)"));
            }
        }

        [Test]
        public async Task VerifyMock_NotExecuted_Times()
        {
            var mcf = MockHttpClientFactory.Create();
            var mc = mcf.CreateClient("XXX", new Uri("https://d365test"));
            mc.Request(HttpMethod.Post, "products/xyz").Times(Times.Exactly(2)).WithJsonBody(new Person { FirstName = "Bob", LastName = "Jane" })
                .Respond.WithJsonResource("MockHttpClientTest-UriAndBody_WithJsonResponse3.json", HttpStatusCode.Accepted);

            try
            {
                var hc = mcf.GetHttpClient("XXX");
                var res = await hc.PostAsJsonAsync("products/xyz", new Person { LastName = "Jane", FirstName = "Bob" }).ConfigureAwait(false);
                Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));
                res = await hc.PostAsJsonAsync("products/xyz", new Person { LastName = "Jane", FirstName = "Bob" }).ConfigureAwait(false);
                Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));
                res = await hc.PostAsJsonAsync("products/xyz", new Person { LastName = "Jane", FirstName = "Bob" }).ConfigureAwait(false);
                Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));

                mcf.VerifyAll();
                Assert.Fail();
            }
            catch (MockHttpClientException mhcex)
            {
                Assert.That(mhcex.Message, Is.EqualTo("The request was invoked 3 times; expected Exactly(2). Request: <XXX> POST https://d365test/products/xyz {\"firstName\":\"Bob\",\"lastName\":\"Jane\"} (application/json)"));
            }
        }

        [Test]
        public async Task VerifyMock_Executed()
        {
            var mcf = MockHttpClientFactory.Create();
            mcf.CreateClient("XXX", new Uri("https://d365test"))
                .Request(HttpMethod.Post, "products/xyz").WithJsonBody(new Person { FirstName = "Bob", LastName = "Jane" })
                .Respond.WithJsonResource("MockHttpClientTest-UriAndBody_WithJsonResponse3.json", HttpStatusCode.Accepted);

            var hc = mcf.GetHttpClient("XXX");
            await hc.PostAsJsonAsync("products/xyz", new Person { LastName = "Jane", FirstName = "Bob" }).ConfigureAwait(false);

            mcf.VerifyAll();
        }

        [Test]
        public async Task UriAndAnyBody()
        {
            var mcf = MockHttpClientFactory.Create();
            mcf.CreateClient("XXX", new Uri("https://d365test"))
                .Request(HttpMethod.Post, "products/xyz").WithAnyBody()
                .Respond.WithJson("{\"first\":\"Bob\",\"last\":\"Jane\"}", HttpStatusCode.Accepted);

            var hc = mcf.GetHttpClient("XXX");
            var res = await hc.PostAsJsonAsync("products/xyz", new Person { LastName = "Jane", FirstName = "Bob" }).ConfigureAwait(false);
            Assert.Multiple(async () =>
            {
                Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));
                Assert.That(await res.Content.ReadAsStringAsync().ConfigureAwait(false), Is.EqualTo("{\"first\":\"Bob\",\"last\":\"Jane\"}"));
                Assert.That(res.RequestMessage, Is.Not.Null);
            });

            Assert.ThrowsAsync<MockHttpClientException>(async () => await hc.SendAsync(new HttpRequestMessage(HttpMethod.Post, "products/xyz")));
        }

        [Test]
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
            var res = await hc.PostAsJsonAsync("products/xyz", new Person { LastName = "Jane", FirstName = "Bob" }).ConfigureAwait(false);
            Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));

            res = await hc.PostAsJsonAsync("products/xyz", new Person { LastName = "Jane", FirstName = "Bob" }).ConfigureAwait(false);
            Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        [Test]
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
            var res = await hc.GetAsync("products/xyz").ConfigureAwait(false);
            Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.NotModified));

            res = await hc.GetAsync("products/xyz").ConfigureAwait(false);
            Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

        [Test]
        public async Task MockDelay()
        {
            var mcf = MockHttpClientFactory.Create();
            mcf.CreateClient("XXX", new Uri("https://d365test")).Request(HttpMethod.Get, "products/xyz").Respond.Delay(500).With(HttpStatusCode.NotFound);

            var hc = mcf.GetHttpClient("XXX");

            var sw = Stopwatch.StartNew();
            var res = await hc.GetAsync("products/xyz").ConfigureAwait(false);
            sw.Stop();

            Assert.Multiple(() =>
            {
                Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
                Assert.That(sw.ElapsedMilliseconds, Is.GreaterThanOrEqualTo(495));
            });
        }

        [Test]
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
            var res = await hc.GetAsync("products/xyz").ConfigureAwait(false);
            sw.Stop();
            Assert.Multiple(() =>
            {
                Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.NotModified));
                Assert.That(sw.ElapsedMilliseconds, Is.GreaterThanOrEqualTo(245));
            });

            sw.Restart();
            res = await hc.GetAsync("products/xyz").ConfigureAwait(false);
            sw.Stop();
            Assert.Multiple(() =>
            {
                Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
                Assert.That(sw.ElapsedMilliseconds, Is.GreaterThanOrEqualTo(95));
            });
        }

        [Test]
        public async Task UriAndBody_WithXmlRequest()
        {
            var mcf = MockHttpClientFactory.Create();
            mcf.CreateClient("XXX", new Uri("https://d365test"))
                .Request(HttpMethod.Post, "products/xyz").WithBody("<Person xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns=\"http://schemas.datacontract.org/2004/07/UnitTestEx.NUnit.Test.Model\"><FirstName>Bob</FirstName><LastName>Jane</LastName></Person>", MediaTypeNames.Application.Xml)
                .Respond.With(new StringContent("<person><first>Bob</first><last>Jane</last></person>", Encoding.UTF8, MediaTypeNames.Application.Xml), HttpStatusCode.Accepted);

            var hc = mcf.GetHttpClient("XXX");
            var res = await hc.PostAsXmlAsync("products/xyz", new Person { LastName = "Jane", FirstName = "Bob" }).ConfigureAwait(false);
            Assert.Multiple(async () =>
            {
                Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));
                Assert.That(await res.Content.ReadAsStringAsync().ConfigureAwait(false), Is.EqualTo("<person><first>Bob</first><last>Jane</last></person>"));
            });
        }

        [Test]
        public async Task UriAndBody_WithAnyTypeRequest()
        {
            var mcf = MockHttpClientFactory.Create();
            mcf.CreateClient("XXX", new Uri("https://d365test"))
                .Request(HttpMethod.Post, "testing").WithBody("--my--custom--format--", "application/custom-format")
                .Respond.With(new StringContent("--ok--", Encoding.UTF8, "application/custom-format"), HttpStatusCode.Accepted);

            var hc = mcf.GetHttpClient("XXX");
            var res = await hc.PostAsync("testing", new StringContent("--my--custom--format--", Encoding.UTF8, "application/custom-format")).ConfigureAwait(false);
            Assert.Multiple(async () =>
            {
                Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));
                Assert.That(await res.Content.ReadAsStringAsync().ConfigureAwait(false), Is.EqualTo("--ok--"));
            });
        }

        [Test]
        public async Task DefaultHttpClient()
        {
            var mcf = MockHttpClientFactory.Create();
            mcf.CreateDefaultClient(new Uri("https://d365test"))
                .Request(HttpMethod.Post, "products/xyz").WithAnyBody()
                .Respond.Headers([new("x-blah", "abc"), new("Age", "55")]).WithJson("{\"first\":\"Bob\",\"last\":\"Jane\"}", HttpStatusCode.Accepted);

            var hc = mcf.GetHttpClient();
            var res = await hc.PostAsJsonAsync("products/xyz", new Person { LastName = "Jane", FirstName = "Bob" }).ConfigureAwait(false);
            Assert.Multiple(async () =>
            {
                Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));
                Assert.That(await res.Content.ReadAsStringAsync().ConfigureAwait(false), Is.EqualTo("{\"first\":\"Bob\",\"last\":\"Jane\"}"));
                Assert.That(res.RequestMessage, Is.Not.Null);
                Assert.That(res.Headers.GetValues("x-blah").Single(), Is.EqualTo("abc"));
                Assert.That(res.Headers.Age, Is.EqualTo(TimeSpan.FromSeconds(55)));
            });

            Assert.ThrowsAsync<MockHttpClientException>(async () => await hc.SendAsync(new HttpRequestMessage(HttpMethod.Post, "products/xyz")));
        }

        [Test]
        public async Task WithYamlRequests()
        {
            var mcf = MockHttpClientFactory.Create();
            mcf.CreateClient("XXX").WithRequestsFromResource("mock.unittestex.yaml");

            var hc = mcf.GetHttpClient("XXX");
            var res = await hc.PostAsJsonAsync("products/xyz", new { DoesNotMatter = true });
            Assert.Multiple(async () =>
            {
                Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));
                ObjectComparer.AssertJson("{\"product\":\"xyz\",\"quantity\":1}", await res.Content.ReadAsStringAsync().ConfigureAwait(false));
            });

            res = await hc.GetAsync("people/123");
            Assert.Multiple(async () =>
            {
                Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                ObjectComparer.AssertJson("{\"first\":\"Bob\",\"last\":\"Jane\"}", await res.Content.ReadAsStringAsync().ConfigureAwait(false));
                Assert.That(res.Headers.GetValues("x-blah").Single(), Is.EqualTo("abc"));
                Assert.That(res.Headers.Age, Is.EqualTo(TimeSpan.FromSeconds(55)));
            });

            mcf.VerifyAll();
        }

        [Test]
        public async Task WithYamlSequence()
        {
            var mcf = MockHttpClientFactory.Create();
            mcf.CreateClient("XXX").WithRequestsFromResource("sequence.unittestex.yaml");

            var hc = mcf.GetHttpClient("XXX");
            var res = await hc.GetAsync("people/123");
            Assert.Multiple(async () =>
            {
                Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                ObjectComparer.AssertJson("{\"first\":\"Bob\",\"last\":\"Jane\"}", await res.Content.ReadAsStringAsync().ConfigureAwait(false));
            });

            res = await hc.GetAsync("people/123");
            Assert.Multiple(async () =>
            {
                Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                ObjectComparer.AssertJson("{\"first\":\"Sarah\",\"last\":\"Johns\"}", await res.Content.ReadAsStringAsync().ConfigureAwait(false));
            });

            mcf.VerifyAll();
        }
    }
}