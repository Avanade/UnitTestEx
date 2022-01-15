using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace UnitTestEx.Function
{
    public class PersonFunction
    {
        private readonly IConfiguration _config;

        public PersonFunction(IConfiguration config) => _config = config;

        [FunctionName("PersonFunction")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string name = req.Query["name"];

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            name ??= data?.name;

            if (name == "Brian")
            {
                var msd = new ModelStateDictionary();
                msd.AddModelError("name", "Name cannot be Brian.");
                return new BadRequestObjectResult(msd);
            }

            if (name == "Rachel")
            {
                var obj = new { FirstName = "Rachel", LastName = "Smith" };
                return new JsonResult(obj);
            }

            string responseMessage = string.IsNullOrEmpty(name)
                ? "This HTTP triggered function executed successfully. Pass a name in the query string or in the request body for a personalized response."
                : $"Hello, {name}. This HTTP triggered function executed successfully.";

            return new OkObjectResult(responseMessage);
        }

        [FunctionName("PersonFunctionObj")]
        public async Task<IActionResult> RunWithValue([HttpTrigger(AuthorizationLevel.Function, "post", Route = "people/{name}")] Person person, ILogger log)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            log.LogInformation("C# HTTP trigger function processed a request.");
            return new OkObjectResult(new { first = person.FirstName, last = person.LastName });
        }
    }

    public class Person
    {
        public string FirstName { get; set; }

        public string LastName { get; set; }
    }
}