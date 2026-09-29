using System.Net.Http;
using UnitTestEx.Api;
using Xunit;
using Xunit.Abstractions;

namespace UnitTestEx.Xunit.Test.Other
{
    // Regression coverage for a bug where ApiTesterBase.CreateHttpClient() routed through GetTestServer() -> HostExecutionWrapper() -> SharedState.Reset() on every request. Since
    // CreateHttpClient() is invoked from HttpTesterBase.SendAsync() *after* any Expect*()-style call has already registered request-scoped state (e.g. CoreEx's ExpectEvents()) into
    // SharedState.RequestStateData(requestId), that state was silently wiped immediately before the request was even sent - causing genuine, correctly-matched expectations to be
    // reported as failed (or simply ignored) once the request actually ran. See TesterBaseCore.SharedState/TestSharedState.RequestStateData/TestSharedState.Reset.
    public class RequestStateDataTest : UnitTestBase
    {
        public RequestStateDataTest(ITestOutputHelper output) : base(output) { }

        [Fact]
        public void RequestStateData_SurvivesUntilPostRun()
        {
            const string flagKey = "UnitTestEx_RegressionFlag";

            using var test = ApiTester.Create<Startup>();
            var http = test.Http();

            // Simulate what an Expect*()-style extension (e.g. CoreEx's ExpectEvents()) does: register request-scoped state, keyed by the HttpTester's own RequestId, before .Run() executes.
            test.SharedState.RequestStateData(http.RequestId)[flagKey] = true;

            bool? flagStillPresentAtPostRun = null;
            test.AddPostRunBeforeExpectationsAction(_ => flagStillPresentAtPostRun = test.SharedState.RequestStateData(http.RequestId).ContainsKey(flagKey));

            http.Run(HttpMethod.Get, "Person/1").AssertOK();

            Assert.True(flagStillPresentAtPostRun, "Request-scoped state registered before Run() must still be present once the request completes - CreateHttpClient() must not reset SharedState per request.");
        }
    }
}
