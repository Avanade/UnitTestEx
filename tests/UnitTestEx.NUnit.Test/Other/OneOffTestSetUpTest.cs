using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using UnitTestEx.Abstractions;

[assembly: UnitTestEx.Abstractions.OneOffTestSetUp(typeof(UnitTestEx.NUnit.Test.Other.OneOffTestSetUp))]

namespace UnitTestEx.NUnit.Test.Other
{
    [TestFixture]
    public class OneOffTestSetUpTest
    {
        [OneTimeSetUp]
        public void OneTimeSetUp() => OneOffTestSetUpAttribute.SetUp(GetType().Assembly);

        [Test]
        public void SetUp_SetDefaultUserName()
        {
            Assert.AreEqual("Luke", TestSetUp.Default.DefaultUserName);
        }

        [Test]
        public void TesterExtensions_ApiTester()
        {
            using var test = ApiTester.Create<UnitTestEx.Api.Startup>();
            var bs = test.Services.GetService<BlahService>();
            Assert.IsNotNull(bs);
        }

        [Test]
        public void TesterExtensions_FunctionTester()
        {
            using var test = FunctionTester.Create<Function.Startup>();
            var bs = test.Services.GetService<BlahService>();
            Assert.IsNotNull(bs);
        }

        [Test]
        public void TesterExtensions_GenericTester()
        {
            using var test = GenericTester.Create();
            var bs = test.Services.GetService<BlahService>();
            Assert.IsNotNull(bs);
        }
    }

    public class OneOffTestSetUp : OneOffTestSetUpBase
    {
        public override void SetUp()
        {
            TestSetUp.Default.DefaultUserName = "Luke";
            TestSetUp.Extensions.Add(new TestExtension());
        }
    }

    public class TestExtension : TesterExtensionsConfig
    {
        public override void ConfigureServices(TesterBase owner, IServiceCollection services)
        {
            services.AddSingleton<BlahService>();
        }
    }

    public class BlahService { }
}