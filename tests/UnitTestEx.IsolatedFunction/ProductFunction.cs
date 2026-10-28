using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System.Net;

namespace UnitTestEx.IsolatedFunction;

public sealed class ProductFunction
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ProductFunction> _logger;

    public ProductFunction(IHttpClientFactory clientFactory, ILogger<ProductFunction> logger)
    {
        _httpClient = clientFactory.CreateClient("XXX");
        _logger = logger;
    }

    [Function("ProductFunction")]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", Route = "product/{id}")] HttpRequest request, string id)
    {
        _logger.LogInformation("C# HTTP trigger function processed a request.");
        if (id == "exception")
            throw new InvalidOperationException("An unexpected exception occured.");

        using var response = await _httpClient.GetAsync($"products/{id}").ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return new NotFoundResult();

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var value = JObject.Parse(json);
        return new JsonResult(new { id = value["id"]?.Value<string>(), description = value["description"]?.Value<string>() });
    }

    [Function("TimerTriggered")]
    public Task DailyRun([TimerTrigger("0 0 0 */1 * *", RunOnStartup = true)] string timer)
    {
        _logger.LogInformation("C# Timer trigger function executed (DI).");
        _logger.LogInformation("C# Timer trigger function executed (method).");
        return Task.CompletedTask;
    }
}
