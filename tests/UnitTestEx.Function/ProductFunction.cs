using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace UnitTestEx.Function
{
    public class ProductFunction
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ProductFunction> _logger;

        public ProductFunction(IHttpClientFactory clientFactory, ILogger<ProductFunction> logger)
        {
            _httpClient = clientFactory.CreateClient("XXX");
            _logger = logger;
        }

        [FunctionName("ProductFunction")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", Route = "product/{id}")] HttpRequest _, string id, ILogger log)
            => await Logic(id, log).ConfigureAwait(false);

        private async Task<IActionResult> Logic(string id, ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            if (id == "exception")
                throw new InvalidOperationException("An unexpected exception occured.");

            var result = await _httpClient.GetAsync($"products/{id}").ConfigureAwait(false);
            if (result.StatusCode == HttpStatusCode.NotFound)
                return new NotFoundResult();

            result.EnsureSuccessStatusCode();

            var str = await result.Content.ReadAsStringAsync().ConfigureAwait(false);
            JObject val = JObject.Parse(str);
          
            return new JsonResult(new { id = val["id"].Value<string>(), description = val["description"].Value<string>() });
        }

        [FunctionName("TimerTriggered")]
        public Task DailyRun([TimerTrigger("0 0 0 */1 * *", RunOnStartup = true)] TimerInfo _, ILogger otherLogger)
        {
            _logger.LogInformation("C# Timer trigger function executed (DI).");
            otherLogger.LogInformation("C# Timer trigger function executed (method).");
            return Task.CompletedTask;
        }
    }
}
