using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTestEx.MSTest.Test.Other
{
    [TestClass]
    public class WithGenericTesterTest : WithGenericTester<object>
    {
        [TestMethod]
        public void Run_Success()
        {
            var c = Test.Services.GetService<IConfiguration>();

            Test.Run(() => 1)
                .AssertSuccess()
                .AssertValue(1);
        }

        [TestMethod]
        public void Run_Success_AssertJSON()
        {
            Test.Run(() => 1)
                .AssertSuccess()
                .AssertJson("1");
        }
    }
}