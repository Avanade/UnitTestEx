using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;

namespace UnitTestEx.NUnit.Test.Other
{
    [TestFixture]
    [NonParallelizable]
    public class ApiTesterConfigurationTest
    {
        [Test]
        public void ApiTester_UsesHostScopedEnvironmentAndConfiguration()
        {
            var variables = new[]
            {
                "DOTNET_ENVIRONMENT",
                "ASPNETCORE_ENVIRONMENT"
            };
            var originalValues = Array.ConvertAll(variables, Environment.GetEnvironmentVariable);
            var originalSpecialKey = Environment.GetEnvironmentVariable("SpecialKey");
            var testValues = new[] { "OriginalDotnet", "OriginalAspNetCore" };

            try
            {
                for (var i = 0; i < variables.Length; i++)
                    Environment.SetEnvironmentVariable(variables[i], testValues[i]);

                using var test = ApiTester.Create<UnitTestEx.Api.Startup>();
                Assert.That(test.Services.GetRequiredService<IWebHostEnvironment>().EnvironmentName, Is.EqualTo(TestSetUp.Environment));
                Assert.That(test.Configuration["SpecialKey"], Is.EqualTo("VerySpecialValue"));
                for (var i = 0; i < variables.Length; i++)
                    Assert.That(Environment.GetEnvironmentVariable(variables[i]), Is.EqualTo(testValues[i]));
                Assert.That(Environment.GetEnvironmentVariable("SpecialKey"), Is.EqualTo(originalSpecialKey));
            }
            finally
            {
                for (var i = 0; i < variables.Length; i++)
                    Environment.SetEnvironmentVariable(variables[i], originalValues[i]);
            }
        }
    }
}
