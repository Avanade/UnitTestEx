using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UnitTestEx.IsolatedFunction;

public sealed class PersonFunction(ILogger<PersonFunction> logger)
{
    [Function("PersonFunction")]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req)
    {
        logger.LogInformation("C# HTTP trigger function processed a request.");
        var name = req.Query["name"].FirstOrDefault();
        using var reader = new StreamReader(req.Body);
        var body = await reader.ReadToEndAsync().ConfigureAwait(false);
        var data = string.IsNullOrEmpty(body) ? null : JsonSerializer.Deserialize<Namer>(body);
        name ??= data?.Name;

        if (name == "Brian")
        {
            var modelState = new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary();
            modelState.AddModelError("name", "Name cannot be Brian.");
            return new BadRequestObjectResult(modelState);
        }

        if (name == "Rachel")
            return new JsonResult(new { FirstName = "Rachel", LastName = "Smith" });

        if (name == "Damien")
            return new BadRequestObjectResult("Name cannot be Damien.");

        if (name == "Bruce")
            return new ContentResult { Content = "Name cannot be Bruce.", StatusCode = StatusCodes.Status400BadRequest };

        var response = string.IsNullOrEmpty(name)
            ? "This HTTP triggered function executed successfully. Pass a name in the query string or in the request body for a personalized response."
            : $"Hello, {name}. This HTTP triggered function executed successfully.";
        return new OkObjectResult(response);
    }

    [Function("PersonFunctionObj")]
    public async Task<IActionResult> RunWithValue([HttpTrigger(AuthorizationLevel.Function, "post", Route = "people/{name}")] HttpRequest req, string name)
    {
        logger.LogInformation("C# HTTP trigger function processed a request.");
        var person = await JsonSerializer.DeserializeAsync<Person>(req.Body).ConfigureAwait(false);
        return new OkObjectResult(new { first = person?.FirstName, last = person?.LastName });
    }

    [Function("PersonFunctionContent")]
    public async Task<IActionResult> RunWithContent([HttpTrigger(AuthorizationLevel.Function, "post", Route = "people/content/{name}")] HttpRequest req, string name)
    {
        logger.LogInformation("C# HTTP trigger function processed a request.");
        var person = await JsonSerializer.DeserializeAsync<Person>(req.Body).ConfigureAwait(false);
        return new ContentResult
        {
            Content = JsonSerializer.Serialize(new { first = person?.FirstName, last = person?.LastName }),
            ContentType = "application/json",
            StatusCode = StatusCodes.Status200OK
        };
    }

    [Function("PersonFunctionQuery")]
    public IActionResult RunWithQuery([HttpTrigger(AuthorizationLevel.Function, "get", Route = "api/people/{name}")] HttpRequest request, string name)
    {
        logger.LogInformation("C# HTTP trigger function processed a request.");
        return new OkObjectResult(new { name });
    }
}

public sealed class Namer
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}
