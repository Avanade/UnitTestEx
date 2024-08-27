using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace UnitTestEx.IsolatedFunction
{
    public class HttpFunction
    {
        private readonly ILogger _logger;

        public HttpFunction(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<HttpFunction>();
        }

        [Function("HttpFunction")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");
            await Task.CompletedTask;
            return new OkObjectResult("Welcome to Azure Functions!");
        }
    }
}