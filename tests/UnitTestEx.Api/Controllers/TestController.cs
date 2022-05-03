using System;
using System.Collections.Generic;
using CoreEx.Entities;
using CoreEx.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using UnitTestEx.Api.Models;

namespace UnitTestEx.Api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class TestController : ControllerBase
    {
        private int _state;

        public TestController Add(int value)
        {
            _state += value;
            return this;
        }

        [HttpGet()]
        public int Get()
        {
            return _state;
        }
    }
}