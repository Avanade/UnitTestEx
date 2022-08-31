using CoreEx;
using CoreEx.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using UnitTestEx.Api.Models;

namespace UnitTestEx.Api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class PersonController : ControllerBase
    {
        private readonly ILogger<PersonController> _logger;
        private readonly IConfiguration _config;

        public PersonController(ILogger<PersonController> logger, IConfiguration config)
        {
            _logger = logger;
            _config = config;
        }

        [HttpGet("{id}")]
        public IActionResult Get(int id)
        {
            if (_config["SpecialKey"] != "VerySpecialValue")
                throw new InvalidOperationException("The people do not feel very special!");

            if (id == 1)
                return new JsonResult(new Person { Id = 1, FirstName = "Bob", LastName = "Smith" });
            else if (id == 2)
                return new ObjectResult(new Person { Id = 2, FirstName = "Jane", LastName = "Jones" });
            else if (id == 3)
                return new ObjectResult(new Person { Id = 3, FirstName = "Brad", LastName = "Davies" });
            else
                return new NotFoundResult();
        }

        [HttpGet("")]
        public IActionResult GetByArgs(string firstName, string lastName, [FromQuery] List<int> id = default)
        {
            return new ObjectResult($"{firstName}-{lastName}-{string.Join(",", id)}");
        }

        [HttpGet("paging")]
        public IActionResult GetPaging()
        {
            var ro = Request.GetRequestOptions();
            return new ObjectResult(ro.Paging);
        }

        [HttpPost("{id}")]
        public IActionResult Update(int id, [FromBody] Person person)
        {
            if (id == 88)
                return new ValidationException("No can do eighty-eight.").ToResult();

            var msd = new ModelStateDictionary();

            if (string.IsNullOrEmpty(person.FirstName))
                msd.AddModelError("firstName", "First name is required.");

            if (string.IsNullOrEmpty(person.LastName))
                msd.AddModelError("lastName", "Last name is required.");

            if (!msd.IsValid)
                return new BadRequestObjectResult(msd);

            _logger.LogInformation($"Person {id} is being updated.");
            person.Id = id;
            return new JsonResult(person);
        }
    }
}