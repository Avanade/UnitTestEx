using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using UnitTestEx.Api;
using UnitTestEx.Api.Controllers;

namespace UnitTestEx.MSTest.Test
{
    [TestClass]
    public class ExpressionTest
    {
        [TestMethod]
        public void Test_ExpressionRuns()
        {
            using var test = ApiTester.Create<Startup>().UseJsonSerializer(new CoreEx.Newtonsoft.Json.JsonSerializer());

            var ex = Assert.ThrowsException<InvalidOperationException>(() => test.Controller<TestController>().Run(c => c.Add(2).Add(3).Get()));
            Assert.IsTrue(ex.Message.StartsWith("UnitTestEx methods that enable an expression must not include method-chaining 'c.Add(2).Add(3).Get()'"));
        }
    }
}