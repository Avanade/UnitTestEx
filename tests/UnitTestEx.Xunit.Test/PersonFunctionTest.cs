using System.Net.Http;
using UnitTestEx.Function;
using UnitTestEx.XUnit;
using Xunit;
using Xunit.Abstractions;

namespace UnitTestEx.Xunit.Test
{
    public class PersonFunctionTest : UnitTestBase
    {
        public PersonFunctionTest(ITestOutputHelper output) : base(output) { }

        [Fact]
        public void NoData()
        {
            using var test = CreateFunctionTester<Startup>();
            test.HttpTrigger<PersonFunction>()
                .Run(f => f.Run(test.CreateHttpRequest(HttpMethod.Get, "person", null), test.Logger))
                .AssertOK("This HTTP triggered function executed successfully. Pass a name in the query string or in the request body for a personalized response.");
        }

        [Fact]
        public void QueryString()
        {
            using var test = CreateFunctionTester<Startup>();
            test.HttpTrigger<PersonFunction>()
                .Run(f => f.Run(test.CreateHttpRequest(HttpMethod.Get, "person?name=Trevor", null), test.Logger))
                .AssertOK("Hello, Trevor. This HTTP triggered function executed successfully.");
        }

        [Fact]
        public void WithBody()
        {
            using var test = CreateFunctionTester<Startup>();
            test.HttpTrigger<PersonFunction>()
                .Run(f => f.Run(test.CreateJsonHttpRequest(HttpMethod.Get, "person", new { name = "Jane" }), test.Logger))
                .AssertOK("Hello, Jane. This HTTP triggered function executed successfully.");
        }

        [Fact]
        public void BadRequest1()
        {
            using var test = CreateFunctionTester<Startup>();
            test.HttpTrigger<PersonFunction>()
                .Run(f => f.Run(test.CreateJsonHttpRequest(HttpMethod.Post, "person", new { name = "Brian" }), test.Logger))
                .AssertBadRequest("Name cannot be Brian.");
        }

        [Fact]
        public void BadRequest2()
        {
            using var test = CreateFunctionTester<Startup>();
            test.HttpTrigger<PersonFunction>()
                .Run(f => f.Run(test.CreateJsonHttpRequest(HttpMethod.Post, "person", new { name = "Brian" }), test.Logger))
                .AssertBadRequest(new ApiError("name", "Name cannot be Brian."));
        }

        [Fact]
        public void ValidJson()
        {
            using var test = CreateFunctionTester<Startup>();
            test.HttpTrigger<PersonFunction>()
                .Run(f => f.Run(test.CreateJsonHttpRequest(HttpMethod.Get, "person", new { name = "Rachel" }), test.Logger))
                .AssertOK(new { FirstName = "Rachel", LastName = "Smith" });
        }

        [Fact]
        public void ValidJsonResource()
        {
            using var test = CreateFunctionTester<Startup>();
            test.HttpTrigger<PersonFunction>()
                .Run(f => f.Run(test.CreateJsonHttpRequest(HttpMethod.Get, "person", new { name = "Rachel" }), test.Logger))
                .AssertOKFromJsonResource("FunctionTest-ValidJsonResource.json");
        }

        [Fact]
        public void ValueVsHttpRequestObject()
        {
            using var test = CreateFunctionTester<Startup>();
            test.HttpTrigger<PersonFunction>()
                .Run(f => f.RunWithValue(new Person { FirstName = "Rachel", LastName = "Smith" }, test.Logger))
                .AssertOK(new { first = "Rachel", last = "Smith" });
        }
    }
}