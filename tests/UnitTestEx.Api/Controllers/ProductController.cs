using CoreEx.Events;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace UnitTestEx.Api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ProductController : ControllerBase
    {
        private readonly HttpClient _httpClient;
        private readonly IEventPublisher _eventPublisher;
        private readonly HttpClient _defaultHttpClient;

        public ProductController(IHttpClientFactory clientFactory, IEventPublisher eventPublisher)
        {
            _httpClient = clientFactory.CreateClient("XXX");
            _eventPublisher = eventPublisher;
            _defaultHttpClient = clientFactory.CreateClient();
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(string id)
        {
            var result = await _httpClient.GetAsync($"products/{id}").ConfigureAwait(false);
            if (result.StatusCode == HttpStatusCode.NotFound)
                return new NotFoundResult();

            result.EnsureSuccessStatusCode();

            var str = await result.Content.ReadAsStringAsync().ConfigureAwait(false);
            var val = JsonConvert.DeserializeObject<dynamic>(str);

            _eventPublisher.Publish("test-queue", new EventData { Source = new Uri($"/test/product/{id}", UriKind.Relative), Subject = $"test.product.{id}", Action = "update" });
            if (id == "xyz")
                _eventPublisher.Publish("test-queue2", new EventData { Source = new Uri($"/test/product/{id}", UriKind.Relative), Subject = $"test.product.{id}", Action = "update", Value = val });

            await _eventPublisher.SendAsync().ConfigureAwait(false);
            return new OkObjectResult(val);
        }

        [HttpGet("")]
        public async Task<IActionResult> Get()
        {
            var result = await _defaultHttpClient.GetAsync($"products/default").ConfigureAwait(false);
            if (result.StatusCode == HttpStatusCode.NotFound)
                return new NotFoundResult();

            result.EnsureSuccessStatusCode();
            var str = await result.Content.ReadAsStringAsync().ConfigureAwait(false);
            var val = JsonConvert.DeserializeObject<dynamic>(str);
            return new OkObjectResult(val);
        }
    }
}