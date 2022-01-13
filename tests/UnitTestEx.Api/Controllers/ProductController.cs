using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
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

        public ProductController(IHttpClientFactory clientFactory)
        {
            _httpClient = clientFactory.CreateClient("XXX");
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
            return new OkObjectResult(val);
        }
    }
}