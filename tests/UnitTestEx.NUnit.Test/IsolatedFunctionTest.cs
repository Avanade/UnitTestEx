using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using UnitTestEx.Expectations;
using UnitTestEx.IsolatedFunction;

namespace UnitTestEx.NUnit.Test
{
    /// <summary>
    /// The function application uses plain top-level statements in its <c>Program.cs</c>; no test-specific structure is required.
    /// </summary>
    [TestFixture]
    public class IsolatedFunctionTest
    {
        [Test]
        public void HttpTrigger()
        {
            using var test = FunctionTester.Create<HttpFunction>();
            test.HttpTrigger<HttpFunction>()
                .ExpectLogContains("C# HTTP trigger function processed a request.")
                .Run(f => f.Run(test.CreateHttpRequest(HttpMethod.Get, null)))
                .AssertSuccess();
        }

        [Test]
        public void Program_ServicesUsed_WorkerNotStarted()
        {
            using var test = FunctionTester.Create<HttpFunction>()
                .ConfigureServices(sc => sc.AddHostedService<MustNotStartService>());

            using var client = test.Services.GetRequiredService<IHttpClientFactory>().CreateClient("XXX");
            Assert.That(client.BaseAddress, Is.EqualTo(new Uri("https://somesys")));
        }

        [Test]
        public void Program_TestOverrides()
        {
            using var test = FunctionTester.Create<HttpFunction>(additionalConfiguration: [new("SpecialKey", "VerySpecial")])
                .ReplaceSingleton(new TestService("test override"))
                .ConfigureServices(sc => sc.AddHttpClient("XXX", c => c.BaseAddress = new Uri("https://test-override")));

            Assert.Multiple(() =>
            {
                Assert.That(test.Configuration["SpecialKey"], Is.EqualTo("VerySpecial"));
                Assert.That(test.Services.GetRequiredService<IHostEnvironment>().EnvironmentName, Is.EqualTo(UnitTestEx.TestSetUp.Environment));
                Assert.That(test.Services.GetRequiredService<TestService>().Value, Is.EqualTo("test override"));
                using var client = test.Services.GetRequiredService<IHttpClientFactory>().CreateClient("XXX");
                Assert.That(client.BaseAddress, Is.EqualTo(new Uri("https://test-override")));
            });
        }

        [Test]
        public async Task Program_ConcurrentHostsAreIsolated()
        {
            static string Build(string value)
            {
                using var test = FunctionTester.Create<HttpFunction>().ReplaceSingleton(new TestService(value));
                return test.Services.GetRequiredService<TestService>().Value;
            }

            var results = await Task.WhenAll(Task.Run(() => Build("first")), Task.Run(() => Build("second")));
            Assert.That(results, Is.EqualTo(new[] { "first", "second" }));
        }

        public sealed record TestService(string Value);

        public sealed class MustNotStartService : IHostedService
        {
            public Task StartAsync(CancellationToken cancellationToken) => throw new InvalidOperationException("The test must not start hosted services.");

            public Task StopAsync(CancellationToken cancellationToken) => throw new InvalidOperationException("The test must not stop an unstarted host.");
        }
    }
}
