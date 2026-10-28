using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json.Serialization;

namespace UnitTestEx.IsolatedFunction
{
    public sealed class Person
    {
        public Person() { }

        public Person(string? firstName, string? lastName)
        {
            FirstName = firstName;
            LastName = lastName;
        }

        [JsonPropertyName("firstName")]
        public string? FirstName { get; set; }

        [JsonPropertyName("lastName")]
        public string? LastName { get; set; }
    }

    public class HttpVariantFunctions(ILogger<HttpVariantFunctions> logger)
    {
        private readonly ILogger _logger = logger;

        [Function("MultiOutput")]
        public MultiOutputResult MultiOutput([HttpTrigger(AuthorizationLevel.Function, "get", Route = "multi-output")] HttpRequestData req)
        {
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.WriteString("multi-output response");
            return new MultiOutputResult { HttpResponse = response, Message = "multi-output message" };
        }

        [Function("DataJson")]
        public async Task<HttpResponseData> DataJson([HttpTrigger(AuthorizationLevel.Function, "post", Route = "data/json")] HttpRequestData req)
        {
            var person = await req.ReadFromJsonAsync<Person>();
            _logger.LogInformation("Data person {FirstName}.", person?.FirstName);

            var res = req.CreateResponse();
            res.Headers.Add("X-Test", "abc");
            res.Cookies.Append(new HttpCookie("session", "xyz") { Path = "/", HttpOnly = true });
            await res.WriteAsJsonAsync(new Person(person?.LastName, person?.FirstName));
            return res;
        }

        [Function("DataText")]
        public HttpResponseData DataText([HttpTrigger(AuthorizationLevel.Function, "get", Route = "data/text/{name}")] HttpRequestData req, string name)
        {
            var res = req.CreateResponse(HttpStatusCode.Accepted);
            res.Headers.Add("Content-Type", "text/plain; charset=utf-8");
            res.WriteString($"Hello {name}");
            return res;
        }

        [Function("DataHttpContext")]
        public HttpResponseData DataHttpContext([HttpTrigger(AuthorizationLevel.Function, "get", Route = "data/context")] HttpRequestData req)
        {
            var ctx = req.FunctionContext.GetHttpContext();
            var res = req.CreateResponse(ctx is null ? HttpStatusCode.InternalServerError : HttpStatusCode.OK);
            res.WriteString(ctx?.Request.Path.Value ?? "none");
            return res;
        }

        [Function("Result")]
        public async Task<IResult> Result([HttpTrigger(AuthorizationLevel.Function, "get", Route = "result")] HttpRequest req)
        {
            await Task.CompletedTask;
            return Results.Ok(new Person("Jane", "Doe"));
        }

        [Function("ResultSync")]
        public IResult ResultSync([HttpTrigger(AuthorizationLevel.Function, "get", Route = "result/sync")] HttpRequest req) => Results.NotFound();

        [Function("ActionSync")]
        public IActionResult ActionSync([HttpTrigger(AuthorizationLevel.Function, "get", Route = "action/sync")] HttpRequest req) => new OkObjectResult(new Person("John", "Smith"));
    }

    public sealed class MultiOutputResult
    {
        [HttpResult]
        public HttpResponseData? HttpResponse { get; set; }

        [ServiceBusOutput("myqueue", Connection = "ServiceBusConnection")]
        public string? Message { get; set; }
    }
}
