using Microsoft.Azure.Functions.Worker;

namespace UnitTestEx.IsolatedFunction
{
    public class TimerFunction
    {
        private readonly HttpClient _httpClient;

        public TimerFunction(IHttpClientFactory clientFactory)
        {
            _httpClient = clientFactory.CreateClient("XXX");
        }

        [Function("TimerTriggerFunction")]
        public async Task Run([TimerTrigger("0 */5 * * * *" /*, RunOnStartup = true */)] string timerInfo)
        {
            var hr = await _httpClient.GetAsync($"products/123").ConfigureAwait(false);
            hr.EnsureSuccessStatusCode();
        }
    }
}