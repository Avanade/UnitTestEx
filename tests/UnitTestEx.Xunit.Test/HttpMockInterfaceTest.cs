// Copyright (c) Avanade. Licensed under the MIT License. See https://github.com/Avanade/UnitTestEx

using Moq;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using UnitTestEx.Mocking;
using Xunit;
using Xunit.Abstractions;

namespace UnitTestEx.Xunit.Test
{
    /// <summary>
    /// Exercises the shared <c>IHttpMock*</c> interfaces' default interface method ("DIM") convenience extras (e.g. <c>Headers</c>, an integer <c>Delay</c>, no-body/raw-JSON-string/
    /// embedded-resource overloads) - not just the core abstract members - via interface-typed variables against Tier 1's <see cref="MockHttpClient"/>. These are not otherwise exercised
    /// by <see cref="MockHttpClientTest"/> (which calls the concrete native API directly) nor by <see cref="ProductControllerTest.HttpMock_SharedInterface_ConfiguresIdenticallyAcrossTiers"/>
    /// (which only proves the core members).
    /// </summary>
    public class HttpMockInterfaceTest : UnitTestBase
    {
        public HttpMockInterfaceTest(ITestOutputHelper output) : base(output) { }

        [Fact]
        public async Task Interface_Request_WithBody_TextDim_MatchesPlainTextBody()
        {
            var mcf = MockHttpClientFactory.Create();
            IHttpMockClient client = mcf.CreateClient("XXX", new System.Uri("https://d365test"));

            var stub = await client.Request(HttpMethod.Post, "echo")
                .Times(Times.Once())
                .WithBody("hello world") // DIM: single-arg overload, defaults to text/plain.
                .Respond.WithAsync("received", HttpStatusCode.OK, MediaTypeNames.Text.Plain);

            var hc = mcf.GetHttpClient("XXX");
            var res = await hc.PostAsync("echo", new StringContent("hello world", Encoding.UTF8, MediaTypeNames.Text.Plain));
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            Assert.Equal("received", await res.Content.ReadAsStringAsync());

            await stub.VerifyAsync();
        }

        [Fact]
        public async Task Interface_Request_WithJsonResourceBody_Dim_MatchesResourceContent()
        {
            var mcf = MockHttpClientFactory.Create();
            IHttpMockClient client = mcf.CreateClient("XXX", new System.Uri("https://d365test"));

            var stub = await client.Request(HttpMethod.Post, "products/xyz")
                .Times(Times.Once())
                .WithJsonResourceBody<HttpMockInterfaceTest>("MockHttpClientTest-UriAndBody_WithJsonResponse3.json") // DIM: matches request body against embedded resource content.
                .Respond.WithJsonAsync(new { message = "matched" });

            var hc = mcf.GetHttpClient("XXX");
            var res = await hc.PostAsJsonAsync("products/xyz", new { first = "Bob", last = "Jane" });
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            Assert.Equal("{\"message\":\"matched\"}", await res.Content.ReadAsStringAsync());

            await stub.VerifyAsync();
        }

        [Fact]
        public async Task Interface_Response_Extras_HeadersDelayAndNoContentWithAsync_Dim()
        {
            var mcf = MockHttpClientFactory.Create();
            IHttpMockClient client = mcf.CreateClient("XXX", new System.Uri("https://d365test"));

            var stub = await client.Request(HttpMethod.Get, "ping")
                .Times(Times.Once())
                .WithAnyBody()
                .Respond
                .Headers([new("x-custom", "abc"), new("x-other", "def")]) // DIM: iterates and calls the core Header member.
                .Delay(50) // DIM: int milliseconds overload.
                .WithAsync(HttpStatusCode.NoContent); // DIM: no-content overload.

            var hc = mcf.GetHttpClient("XXX");
            var sw = Stopwatch.StartNew();
            var res = await hc.GetAsync("ping");
            sw.Stop();

            Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
            Assert.Equal("abc", res.Headers.GetValues("x-custom").Single());
            Assert.Equal("def", res.Headers.GetValues("x-other").Single());
            Assert.True(sw.ElapsedMilliseconds >= 45, $"Actual elapsed milliseconds {sw.ElapsedMilliseconds}.");

            await stub.VerifyAsync();
        }

        [Fact]
        public async Task Interface_Response_WithJsonAsync_StringAndResource_Dim()
        {
            var mcf = MockHttpClientFactory.Create();
            IHttpMockClient client = mcf.CreateClient("XXX", new System.Uri("https://d365test"));

            var stub1 = await client.Request(HttpMethod.Get, "raw-json")
                .Times(Times.Once())
                .WithAnyBody()
                .Respond.WithJsonAsync("{\"literal\":true}"); // DIM: raw JSON string overload.

            var stub2 = await client.Request(HttpMethod.Get, "resource-json")
                .Times(Times.Once())
                .WithAnyBody()
                .Respond.WithJsonResourceAsync<HttpMockInterfaceTest>("MockHttpClientTest-UriAndBody_WithJsonResponse3.json", HttpStatusCode.Accepted); // DIM: embedded resource overload.

            var hc = mcf.GetHttpClient("XXX");

            var res1 = await hc.GetAsync("raw-json");
            Assert.Equal(HttpStatusCode.OK, res1.StatusCode);
            Assert.Equal("{\"literal\":true}", await res1.Content.ReadAsStringAsync());

            var res2 = await hc.GetAsync("resource-json");
            Assert.Equal(HttpStatusCode.Accepted, res2.StatusCode);
            Assert.Equal("{\"first\":\"Bob\",\"last\":\"Jane\"}", await res2.Content.ReadAsStringAsync());

            await stub1.VerifyAsync();
            await stub2.VerifyAsync();
        }

        [Fact]
        public async Task Interface_ResponseSequenceItem_Extras_Dim()
        {
            var mcf = MockHttpClientFactory.Create();
            IHttpMockClient client = mcf.CreateClient("XXX", new System.Uri("https://d365test"));

            var stub = await client.Request(HttpMethod.Get, "sequence")
                .WithAnyBody()
                .Respond.WithSequenceAsync(seq =>
                {
                    seq.Respond()
                        .Headers([new("x-seq", "1")]) // DIM: iterates and calls the core Header member.
                        .Delay(10) // DIM: int milliseconds overload.
                        .With(HttpStatusCode.NoContent); // DIM: no-content overload.

                    seq.Respond().WithJson("{\"raw\":true}", HttpStatusCode.OK); // DIM: raw JSON string overload.

                    seq.Respond().WithJsonResource<HttpMockInterfaceTest>("MockHttpClientTest-UriAndBody_WithJsonResponse3.json", HttpStatusCode.Accepted); // DIM: embedded resource overload.
                });

            var hc = mcf.GetHttpClient("XXX");

            var res1 = await hc.GetAsync("sequence");
            Assert.Equal(HttpStatusCode.NoContent, res1.StatusCode);
            Assert.Equal("1", res1.Headers.GetValues("x-seq").Single());

            var res2 = await hc.GetAsync("sequence");
            Assert.Equal(HttpStatusCode.OK, res2.StatusCode);
            Assert.Equal("{\"raw\":true}", await res2.Content.ReadAsStringAsync());

            var res3 = await hc.GetAsync("sequence");
            Assert.Equal(HttpStatusCode.Accepted, res3.StatusCode);
            Assert.Equal("{\"first\":\"Bob\",\"last\":\"Jane\"}", await res3.Content.ReadAsStringAsync());

            await stub.VerifyAsync();
        }
    }
}
