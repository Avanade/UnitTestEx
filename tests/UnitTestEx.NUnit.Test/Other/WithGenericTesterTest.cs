using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace UnitTestEx.NUnit.Test.Other
{
    public class WithGenericTesterTest : WithGenericTester<EntryPoint>
    {
        [Test]
        public void Run_Success()
        {
            var c = Test.Services.GetService<IConfiguration>();

            Test.Run(() => 1)
                .AssertSuccess()
                .AssertValue(1);
        }

        [Test]
        public void Run_Success_AssertJSON()
        {
            Test.Run(() => 1)
                .AssertSuccess()
                .AssertJson("1");
        }
    }
}