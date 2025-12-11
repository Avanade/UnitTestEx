using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using System;
using System.Threading.Tasks;
using UnitTestEx.Expectations;

namespace UnitTestEx.NUnit.Test.Other
{
    [TestFixture]
    public class GenericTest
    {
        [Test]
        public void Run_Success()
        {
            using var test = GenericTester.Create();
            test.Run(() => 1)
                .AssertSuccess()
                .AssertValue(1);
        }

        [Test]
        public void Run_Success_AssertJSON()
        {
            using var test = GenericTester.Create();
            test.Run(() => 1)
                .AssertSuccess()
                .AssertJson("1");
        }

        [Test]
        public void Run_Exception()
        {
            static Func<Task> ThrowBadness() => () => throw new DivideByZeroException("Badness.");

            using var test = GenericTester.Create();
            test.ExpectException("Badness.")
                .Run(ThrowBadness());
        }


        [Test]
        public void Run_Service()
        {
            using var test = GenericTester.Create<EntryPoint>();

            test.Run<Gin, int>(gin => gin.Pour())
                .AssertSuccess()
                .AssertValue(1);

#if NET9_0_OR_GREATER
            // For .NET 9.0 and greater, we can use ValueTask directly.
            test.Run<Gin, int>(gin => gin.PourAsync())
                .AssertSuccess()
                .AssertValue(1);

            test.Run<Gin, int>(async gin => await gin.PourAsync())
                .AssertSuccess()
                .AssertValue(1);
#else
            test.Run<Gin, int>(async gin => await gin.PourAsync())
                .AssertSuccess()
                .AssertValue(1);
#endif

            test.Run<Gin>(gin => gin.Shake())
                .AssertSuccess();

            test.Run<Gin>(gin => gin.ShakeAsync())
                .AssertSuccess();

            test.Run<Gin>(async gin => await gin.ShakeAsync())
                .AssertSuccess();

            test.Run<Gin>(gin => gin.Stir())
                .AssertException<DivideByZeroException>("As required by Bond; shaken, not stirred.");

            test.Run<Gin>(gin => gin.StirAsync())
                .AssertException<DivideByZeroException>("As required by Bond; shaken, not stirred.");
        }

        [Test]
        public void Configuration_Overrride_Use()
        {
            // Demonstrates how to override the configuration settings for a test.
            using var test = GenericTester.Create();
            test.UseAdditionalConfiguration([new("SpecialKey", "NotSoSpecial")]);
            var cv = test.Configuration.GetValue<string>("SpecialKey");
            Assert.That(cv, Is.EqualTo("NotSoSpecial"));
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Just for testing aye bro!")]
    public class Gin
    {
        public void Stir() => throw new DivideByZeroException("As required by Bond; shaken, not stirred.");
        public Task StirAsync() => throw new DivideByZeroException("As required by Bond; shaken, not stirred.");
        public void Shake() { }
        public Task ShakeAsync() => Task.CompletedTask;
        public int Pour() => 1;
        public ValueTask<int> PourAsync() => ValueTask.FromResult(1);
    }

    public class EntryPoint
    {
        //public void ConfigureAppConfiguration(HostBuilderContext context, IConfigurationBuilder config) { }

        //public void ConfigureHostConfiguration(IConfigurationBuilder config) { }

        //public void ConfigureServices(IServiceCollection services) { }

        public void ConfigureApplication(IHostApplicationBuilder builder)
        {
            builder.Services.AddSingleton<Gin>();
        }
    }
}