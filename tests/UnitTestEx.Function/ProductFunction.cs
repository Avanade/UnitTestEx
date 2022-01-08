using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace UnitTestEx.Function
{
    public class ProductFunction
    {
        private readonly HttpClient _httpClient;

        public ProductFunction(IHttpClientFactory clientFactory)
        {
            _httpClient = clientFactory.CreateClient("XXX");
        }

        [FunctionName("ProductFunction")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", Route = "product/{id}")] HttpRequest req, string id, ILogger log)
            => await Logic(id, log).ConfigureAwait(false);

        private async Task<IActionResult> Logic(string id, ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            var result = await _httpClient.GetAsync($"products/{id}").ConfigureAwait(false);
            if (result.StatusCode == HttpStatusCode.NotFound)
                return new NotFoundResult();

            result.EnsureSuccessStatusCode();

            var str = await result.Content.ReadAsStringAsync().ConfigureAwait(false);
            var val = JsonConvert.DeserializeObject(str);
            return new JsonResult(val);
        }
    }
}
