using System.Collections.Generic;
using System.Net;
using System.Net.Mime;
using System.Threading.Tasks;
using Castle.Core.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UnitTestEx.Api;
using UnitTestEx.Api.Controllers;
using UnitTestEx.Api.Models;

namespace UnitTestEx.MSTest.Test
{
    [TestClass]
    public class ExpressionTest
    {
        [TestMethod]
        public void Test_ExpressionRuns()
        {
            using var test = ApiTester.Create<Startup>().UseJsonSerializer(new CoreEx.Newtonsoft.Json.JsonSerializer());

            var result = test.Controller<TestController>()
                .Run(c => c.Add(2).Add(3).Get());

            result.AssertOK().Assert(5);
        }
    }
}