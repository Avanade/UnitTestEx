using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace UnitTestEx.IsolatedFunction
{
    public class TimerFunction
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger _logger;

        public TimerFunction(IHttpClientFactory clientFactory, ILogger<TimerFunction> logger) 
        {
            _httpClient = clientFactory.CreateClient("XXX");
            _logger = logger;
        }

        [Function("TimerTriggerFunction")]
        public async Task Run([TimerTrigger("0 */5 * * * *" /*, RunOnStartup = true */)] string _)
        {
            _logger.LogInformation("C# Timer trigger function executed.");
            var hr = await _httpClient.GetAsync($"products/123").ConfigureAwait(false);
            hr.EnsureSuccessStatusCode();
        }
    }
}