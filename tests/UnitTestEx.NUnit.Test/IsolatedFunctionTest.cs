using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker.Http;
using UnitTestEx.Azure.Functions;
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

        [Test]
        public void HttpTrigger_RequestData_Json()
        {
            using var test = FunctionTester.Create<HttpFunction>();
            test.HttpTrigger<HttpVariantFunctions>()
                .ExpectLogContains("Data person Jane.")
                .Run(f => f.DataJson(test.CreateJsonHttpRequestData(HttpMethod.Post, "data/json", new Person("Jane", "Doe"), null)))
                .AssertSuccess()
                .ToHttpResponseMessageAssertor()
                .AssertOK()
                .AssertContentType("application/json")
                .AssertNamedHeader("X-Test", "abc")
                .AssertNamedHeader("Set-Cookie", "session=xyz; path=/; httponly")
                .AssertValue(new Person("Doe", "Jane"));
        }

        [Test]
        public void HttpTrigger_RequestData_SyncText_RouteParameter()
        {
            using var test = FunctionTester.Create<HttpFunction>();
            test.HttpTrigger<HttpVariantFunctions>()
                .Run(f => f.DataText(test.CreateHttpRequestData(HttpMethod.Get, "data/text/bob", null, null, null), "bob"))
                .ToHttpResponseMessageAssertor()
                .AssertAccepted()
                .AssertContentType("text/plain")
                .AssertContent("Hello bob");
        }

        [Test]
        public void HttpTrigger_RequestData_MethodCheck()
        {
            using var test = FunctionTester.Create<HttpFunction>();
            var ex = Assert.Throws<InvalidOperationException>(() => test.HttpTrigger<HttpVariantFunctions>()
                .Run(f => f.DataText(test.CreateHttpRequestData(HttpMethod.Post, "data/text/bob", null, null, null), "bob")));
            Assert.That(ex!.Message, Does.Contain("invoked using 'POST'"));
        }

        [Test]
        public void HttpTrigger_RequestData_RouteCheck()
        {
            using var test = FunctionTester.Create<HttpFunction>();
            Assert.Throws<InvalidOperationException>(() => test.HttpTrigger<HttpVariantFunctions>()
                .Run(f => f.DataText(test.CreateHttpRequestData(HttpMethod.Get, "data/text/alice", null, null, null), "bob")));
        }

        [Test]
        public void HttpTrigger_RequestData_GetHttpContext()
        {
            using var test = FunctionTester.Create<HttpFunction>();
            test.HttpTrigger<HttpVariantFunctions>()
                .Run(f => f.DataHttpContext(test.CreateHttpRequestData(HttpMethod.Get, "data/context", null, null, null)))
                .ToHttpResponseMessageAssertor()
                .AssertOK()
                .AssertContent("/data/context");
        }

        [Test]
        public void HttpTrigger_MultiOutput()
        {
            using var test = FunctionTester.Create<HttpFunction>();
            test.HttpTrigger<HttpVariantFunctions>()
                .Run(f => f.MultiOutput(test.CreateHttpRequestData(HttpMethod.Get, "multi-output")))
                .Output(x => x.Message)
                .AssertValue("multi-output message");

            test.HttpTrigger<HttpVariantFunctions>()
                .Run(f => f.MultiOutput(test.CreateHttpRequestData(HttpMethod.Get, "multi-output")))
                .ToHttpResponseMessageAssertor()
                .AssertOK()
                .AssertContent("multi-output response");
        }

        [Test]
        public void GenericTrigger_ServiceBus()
        {
            using var test = FunctionTester.Create<HttpFunction>();
            test.Trigger<ServiceBusFunction>()
                .Run(f => f.Run5(test.CreateServiceBusMessageFromValue(new Person("Jane", "Doe"))))
                .AssertSuccess();

            test.Trigger<ServiceBusFunction>()
                .Run(f => ServiceBusFunction.Run4(new Person("Jane", "Doe")))
                .AssertSuccess();
        }

        [Test]
        public async Task HttpTrigger_Result_Async()
        {
            using var test = FunctionTester.Create<HttpFunction>();
            (await test.HttpTrigger<HttpVariantFunctions>()
                .RunAsync(f => f.Result(test.CreateHttpRequest(HttpMethod.Get, "result"))))
                .AssertSuccess()
                .ToHttpResponseMessageAssertor()
                .AssertOK()
                .AssertValue(new Person("Jane", "Doe"));
        }

        [Test]
        public void HttpTrigger_Result_Sync()
        {
            using var test = FunctionTester.Create<HttpFunction>();
            test.HttpTrigger<HttpVariantFunctions>()
                .Run(f => f.ResultSync(test.CreateHttpRequest(HttpMethod.Get, "result/sync")))
                .ToHttpResponseMessageAssertor()
                .AssertNotFound();
        }

        [Test]
        public void HttpTrigger_ActionResult_Sync()
        {
            using var test = FunctionTester.Create<HttpFunction>();
            test.HttpTrigger<HttpVariantFunctions>()
                .Run(f => f.ActionSync(test.CreateHttpRequest(HttpMethod.Get, "action/sync")))
                .AssertOK()
                .AssertValue(new Person("John", "Smith"))
                .ToHttpResponseMessageAssertor()
                .AssertOK()
                .AssertValue(new Person("John", "Smith"));
        }

        [Test]
        public void HttpTrigger_ActionResult_ToHttpResponseMessage()
        {
            using var test = FunctionTester.Create<HttpFunction>();
            test.HttpTrigger<HttpFunction>()
                .Run(f => f.Run(test.CreateHttpRequest(HttpMethod.Get, null)))
                .ToHttpResponseMessageAssertor()
                .AssertOK()
                .AssertContent("Welcome to Azure Functions!");
        }

        [Test]
        public async Task HttpResponseData_ToHttpResponseMessage_PreservesBodyPosition()
        {
            using var test = FunctionTester.Create<HttpFunction>();
            var res = new TestHttpResponseData(new TestFunctionContext(test.Services)) { StatusCode = System.Net.HttpStatusCode.Created };
            res.Headers.Add("Content-Type", "text/plain");
            res.Headers.Add("X-Id", "42");
            await res.WriteStringAsync("abc");
            var pos = res.Body.Position;

            using var hrm = res.ToHttpResponseMessage();
            Assert.Multiple(async () =>
            {
                Assert.That(hrm.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.Created));
                Assert.That(hrm.Content.Headers.ContentType!.MediaType, Is.EqualTo("text/plain"));
                Assert.That(hrm.Headers.GetValues("X-Id"), Is.EqualTo(new[] { "42" }));
                Assert.That(await hrm.Content.ReadAsStringAsync(), Is.EqualTo("abc"));
                Assert.That(res.Body.Position, Is.EqualTo(pos));
            });
        }

        [Test]
        public void Middleware_Logs_Captured_Http()
        {
            using var test = FunctionTester.Create<HttpFunction>();
            test.HttpTrigger<HttpVariantFunctions>()
                .ExpectLogContains("Middleware before DataText.")
                .ExpectLogContains("Middleware after DataText.")
                .Run(f => f.DataText(test.CreateHttpRequestData(HttpMethod.Get, "data/text/bob"), "bob"))
                .ToHttpResponseMessageAssertor()
                .AssertAccepted();
        }

        [Test]
        public void Middleware_Logs_Captured_NonHttp()
        {
            using var test = FunctionTester.Create<HttpFunction>();
            test.Trigger<ServiceBusFunction>()
                .ExpectLogContains("Middleware before")
                .ExpectLogContains("Middleware after")
                .Run(f => f.Run5(test.CreateServiceBusMessageFromValue(new Person("Jane", "Doe"))))
                .AssertSuccess();
        }

        [Test]
        public async Task Middleware_AsyncFunction_UsesTheEvaluatedRequest()
        {
            using var test = FunctionTester.Create<HttpFunction>();
            var evaluations = 0;
            Func<Microsoft.AspNetCore.Http.HttpRequest> createRequest = () =>
            {
                evaluations++;
                return test.CreateHttpRequest(HttpMethod.Get, "middleware/async", r => r.Headers["X-Middleware"] = "set-item");
            };

            (await test.HttpTrigger<MiddlewareFunctions>().RunAsync(f => f.RequestAsync(createRequest())))
                .AssertOK()
                .AssertValue("set by middleware");

            Assert.That(evaluations, Is.EqualTo(1));
        }

        [Test]
        public void Middleware_SyncFunction_UsesTheEvaluatedRequest()
        {
            using var test = FunctionTester.Create<HttpFunction>();
            var evaluations = 0;
            Func<Microsoft.AspNetCore.Http.HttpRequest> createRequest = () =>
            {
                evaluations++;
                return test.CreateHttpRequest(HttpMethod.Get, "middleware/sync", r => r.Headers["X-Middleware"] = "set-item");
            };

            test.HttpTrigger<MiddlewareFunctions>().Run(f => f.RequestSync(createRequest()))
                .AssertOK()
                .AssertValue("set by middleware");

            Assert.That(evaluations, Is.EqualTo(1));
        }

        [Test]
        public void Middleware_VoidFunction_UsesTheEvaluatedRequest()
        {
            using var test = FunctionTester.Create<HttpFunction>();
            var request = test.CreateHttpRequest(HttpMethod.Get, "middleware/void", r => r.Headers["X-Middleware"] = "set-item");
            var evaluations = 0;
            Func<Microsoft.AspNetCore.Http.HttpRequest> createRequest = () =>
            {
                evaluations++;
                return request;
            };

            test.Trigger<MiddlewareFunctions>().Run(f => f.RequestVoid(createRequest())).AssertSuccess();

            Assert.That(evaluations, Is.EqualTo(1));
            Assert.That(request.Headers["X-Function-Value"].ToString(), Is.EqualTo("set by middleware"));
        }

        [Test]
        public void Middleware_ShortCircuit_HttpResponseData()
        {
            using var test = FunctionTester.Create<HttpFunction>();
            var req = test.CreateHttpRequestData(HttpMethod.Get, "data/text/bob", requestModifier: r => r.Headers["X-Middleware"] = "deny");
            test.HttpTrigger<HttpVariantFunctions>()
                .ExpectLogContains("Middleware before DataText.")
                .Run(f => f.DataText(req, "bob"))
                .ToHttpResponseMessageAssertor()
                .AssertUnauthorized()
                .AssertContent("denied by middleware");
        }

        [Test]
        public void Middleware_ShortCircuit_ActionResult()
        {
            using var test = FunctionTester.Create<HttpFunction>();
            var req = test.CreateHttpRequest(HttpMethod.Get, "action/sync", r => r.Headers["X-Middleware"] = "deny");
            test.HttpTrigger<HttpVariantFunctions>()
                .Run(f => f.ActionSync(req))
                .ToHttpResponseMessageAssertor()
                .AssertUnauthorized()
                .AssertContent("denied by middleware");
        }

        [Test]
        public void Middleware_HandlesException_ActionResult()
        {
            using var test = FunctionTester.Create<HttpFunction>();
            var req = test.CreateHttpRequest(HttpMethod.Get, "throws/action", r => r.Headers["X-Middleware"] = "handle");
            test.HttpTrigger<MiddlewareFunctions>()
                .ExpectLogContains("Middleware handled action boom.")
                .Run(f => f.ThrowsAction(req))
                .ToHttpResponseMessageAssertor()
                .AssertInternalServerError()
                .AssertContent("handled by middleware");
        }

        [Test]
        public void Middleware_HandlesException_HttpResponseData()
        {
            using var test = FunctionTester.Create<HttpFunction>();
            var req = test.CreateHttpRequestData(HttpMethod.Get, "throws/data", requestModifier: r => r.Headers["X-Middleware"] = "handle");
            test.HttpTrigger<MiddlewareFunctions>()
                .ExpectLogContains("Middleware handled data boom.")
                .Run(f => f.ThrowsData(req))
                .ToHttpResponseMessageAssertor()
                .AssertInternalServerError()
                .AssertContent("handled by middleware");
        }

        [Test]
        public void Middleware_UnhandledException_Propagates()
        {
            using var test = FunctionTester.Create<HttpFunction>();
            test.HttpTrigger<MiddlewareFunctions>()
                .Run(f => f.ThrowsAction(test.CreateHttpRequest(HttpMethod.Get, "throws/action")))
                .AssertException<InvalidOperationException>("action boom");
        }

        [Test]
        public void Middleware_Bypassed()
        {
            using var test = FunctionTester.Create<HttpFunction>().UseWorkerMiddleware(false);
            var req = test.CreateHttpRequest(HttpMethod.Get, "action/sync", r => r.Headers["X-Middleware"] = "deny");
            test.HttpTrigger<HttpVariantFunctions>()
                .Run(f => f.ActionSync(req))
                .AssertOK();
        }

        public sealed record TestService(string Value);

        public sealed class MustNotStartService : IHostedService
        {
            public Task StartAsync(CancellationToken cancellationToken) => throw new InvalidOperationException("The test must not start hosted services.");

            public Task StopAsync(CancellationToken cancellationToken) => throw new InvalidOperationException("The test must not stop an unstarted host.");
        }
    }
}
