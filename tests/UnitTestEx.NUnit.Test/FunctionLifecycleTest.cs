using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using UnitTestEx.IsolatedFunction;

namespace UnitTestEx.NUnit.Test
{
    [TestFixture]
    [NonParallelizable]
    public class FunctionLifecycleTest
    {
        [TestCase("generic")]
        [TestCase("http")]
        [TestCase("servicebus")]
        public void TriggerScopes_AreOwnedUntilResetOrDisposal(string kind)
        {
            var resources = new List<ScopedResource>();
            using var test = FunctionTester.Create<HttpFunction>()
                .UseWorkerMiddleware(false)
                .ConfigureServices(sc => sc.AddScoped(_ =>
                {
                    var resource = new ScopedResource();
                    resources.Add(resource);
                    return resource;
                }));

            void Run()
            {
                if (kind == "generic")
                    test.Trigger<ScopedFunction>().Run(f => f.Generic(null!)).AssertValue("alive");
                else if (kind == "http")
                    test.HttpTrigger<ScopedFunction>().Run(f => f.Http(test.CreateHttpRequest(HttpMethod.Get, "scope"))).AssertOK();
                else
                    test.ServiceBusTrigger<ScopedFunction>().Run(f => f.ServiceBus(null!)).AssertSuccess();
            }

            Run();
            Run();
            Assert.That(resources, Has.Count.EqualTo(2));
            Assert.That(resources.TrueForAll(r => r.DisposeCount == 0), Is.True);
            test.ResetHost();
            Assert.That(resources.TrueForAll(r => r.DisposeCount == 1), Is.True);
            Run();
            Assert.That(resources, Has.Count.EqualTo(3));
            Assert.That(resources[2].DisposeCount, Is.Zero);
            test.Dispose();
            test.Dispose();
            Assert.That(resources.TrueForAll(r => r.DisposeCount == 1), Is.True);
            Assert.Throws<ObjectDisposedException>(() => test.Trigger<ScopedFunction>());
        }

        [Test]
        public void TriggerScope_RemainsUsableBetweenInvocations()
        {
            using var test = FunctionTester.Create<HttpFunction>().UseWorkerMiddleware(false)
                .ConfigureServices(sc => sc.AddScoped<ScopedResource>());
            var trigger = test.Trigger<ScopedFunction>();
            trigger.Run(f => f.Generic(null!)).AssertValue("alive");
            trigger.Run(f => f.Generic(null!)).AssertValue("alive");
            Assert.That(trigger.Services.GetRequiredService<ScopedResource>().DisposeCount, Is.Zero);
            test.ResetHost();
            Assert.Throws<ObjectDisposedException>(() => trigger.Services.GetRequiredService<ScopedResource>());
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Cleanup_AttemptsOtherScopesAndHostWhenScopeThrows(bool reset)
        {
            var disposed = new List<string>();
            var test = FunctionTester.Create<HttpFunction>()
                .ConfigureServices(sc =>
                {
                    sc.AddScoped(_ => new ThrowingResource(disposed));
                    sc.AddKeyedScoped("scope", (_, _) => new RecordingResource(disposed, "scope"));
                    sc.AddSingleton(_ => new RecordingResource(disposed, "host"));
                });
            test.Services.GetRequiredService<RecordingResource>();
            test.Trigger<ScopedFunction>().Services.GetRequiredKeyedService<RecordingResource>("scope");
            test.Trigger<ScopedFunction>().Services.GetRequiredService<ThrowingResource>();

            var exception = Assert.Throws<InvalidOperationException>(() =>
            {
                if (reset)
                    test.ResetHost();
                else
                    test.Dispose();
            });
            Assert.That(exception!.Message, Is.EqualTo("scope disposal failed"));
            Assert.That(disposed, Is.EqualTo(new[] { "throwing", "scope", "host" }));
            test.Dispose();
            Assert.That(disposed, Has.Count.EqualTo(3));
        }

        [Test]
        public void Cleanup_SupportsAsyncOnlyScopedResources()
        {
            using var test = FunctionTester.Create<HttpFunction>().ConfigureServices(sc => sc.AddScoped<AsyncResource>());
            var resource = test.Trigger<ScopedFunction>().Services.GetRequiredService<AsyncResource>();
            test.Dispose();
            Assert.That(resource.DisposeCount, Is.EqualTo(1));
        }

        [Test]
        public void Cleanup_ReportsAllScopeFailures()
        {
            var disposed = new List<string>();
            var test = FunctionTester.Create<HttpFunction>()
                .ConfigureServices(sc => sc.AddScoped(_ => new ThrowingResource(disposed)));
            test.Trigger<ScopedFunction>().Services.GetRequiredService<ThrowingResource>();
            test.Trigger<ScopedFunction>().Services.GetRequiredService<ThrowingResource>();
            var exception = Assert.Throws<AggregateException>(() => test.Dispose());
            Assert.That(exception!.InnerExceptions, Has.Count.EqualTo(2));
            Assert.That(disposed, Has.Count.EqualTo(2));
            test.Dispose();
        }

        [TestCase(null)]
        [TestCase("original-environment")]
        public void HostConstruction_RestoresEnvironmentImmediatelyAndAfterReset(string previous)
        {
            var original = Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT");
            try
            {
                Environment.SetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT", previous);
                using var test = FunctionTester.Create<HttpFunction>();
                Assert.That(test.Services.GetRequiredService<IHostEnvironment>().EnvironmentName, Is.EqualTo(TestSetUp.Environment));
                Assert.That(Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT"), Is.EqualTo(previous));
                test.ResetHost();
                Assert.That(test.Services.GetRequiredService<IHostEnvironment>().EnvironmentName, Is.EqualTo(TestSetUp.Environment));
                Assert.That(Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT"), Is.EqualTo(previous));
                test.Dispose();
                Assert.That(Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT"), Is.EqualTo(previous));
            }
            finally
            {
                Environment.SetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT", original);
            }
        }

        [Test]
        public void CreateHttpRequest_UsesUtf8ContentLength()
        {
            using var test = FunctionTester.Create<HttpFunction>();
            var request = test.CreateHttpRequest(HttpMethod.Post, "unicode", "café");

            Assert.That(request.ContentLength, Is.EqualTo(System.Text.Encoding.UTF8.GetByteCount("café")));
        }

        [Test]
        public void FailedConstruction_RestoresEnvironment()
        {
            var original = Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT");
            try
            {
                Environment.SetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT", "original-environment");
                using var test = FunctionTester.Create<HttpFunction>()
                    .ConfigureServices(_ => throw new InvalidOperationException("construction failed"));
                Assert.Throws<InvalidOperationException>(() => _ = test.Services);
                Assert.That(Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT"), Is.EqualTo("original-environment"));
            }
            finally
            {
                Environment.SetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT", original);
            }
        }

        [Test]
        public async Task ConcurrentGenericTypes_SerializeHostConstruction()
        {
            var active = 0;
            var maximum = 0;
            void Configure(IServiceCollection services)
            {
                var count = Interlocked.Increment(ref active);
                maximum = Math.Max(maximum, count);
                try
                {
                    Assert.That(Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT"), Is.EqualTo(TestSetUp.Environment));
                    Thread.Sleep(100);
                }
                finally
                {
                    Interlocked.Decrement(ref active);
                }
            }

            using var first = FunctionTester.Create<HttpFunction>().ConfigureServices(Configure);
            using var second = FunctionTester.Create<HttpVariantFunctions>().ConfigureServices(Configure);
            using var start = new ManualResetEventSlim();
            var one = Task.Run(() => { start.Wait(); return first.Services.GetRequiredService<IHostEnvironment>().EnvironmentName; });
            var two = Task.Run(() => { start.Wait(); return second.Services.GetRequiredService<IHostEnvironment>().EnvironmentName; });
            var original = Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT");
            start.Set();
            Assert.That(await Task.WhenAll(one, two), Is.All.EqualTo(TestSetUp.Environment));
            Assert.That(maximum, Is.EqualTo(1));
            Assert.That(Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT"), Is.EqualTo(original));
        }

        public sealed class ScopedResource : IDisposable
        {
            public int DisposeCount { get; private set; }
            public void Dispose() => DisposeCount++;
        }

        public sealed class AsyncResource : IAsyncDisposable
        {
            public int DisposeCount { get; private set; }
            public async ValueTask DisposeAsync()
            {
                await Task.Yield();
                DisposeCount++;
            }
        }

        public sealed class RecordingResource(List<string> disposed, string name) : IDisposable
        {
            public void Dispose() => disposed.Add(name);
        }

        public sealed class ThrowingResource(List<string> disposed) : IDisposable
        {
            public void Dispose()
            {
                disposed.Add("throwing");
                throw new InvalidOperationException("scope disposal failed");
            }
        }

        public sealed class ScopedFunction(ScopedResource resource)
        {
            [Function("ScopedGeneric")]
            public string Generic([HttpTrigger(AuthorizationLevel.Anonymous)] HttpRequest request)
                => resource.DisposeCount == 0 ? "alive" : throw new ObjectDisposedException(nameof(resource));

            [Function("ScopedHttp")]
            public IActionResult Http([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "scope")] HttpRequest request)
                => new OkObjectResult(Generic(request));

            [Function("ScopedServiceBus")]
            public Task ServiceBus([ServiceBusTrigger("scope")] string message)
            {
                Generic(null!);
                return Task.CompletedTask;
            }
        }
    }
}
