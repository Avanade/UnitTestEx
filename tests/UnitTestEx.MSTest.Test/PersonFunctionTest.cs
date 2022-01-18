using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net.Http;
using UnitTestEx.Function;

namespace UnitTestEx.MSTest.Test
{
    [TestClass]
    public class PersonFunctionTest
    {
        [TestMethod]
        public void NoData()
        {
            using var test = FunctionTester.Create<Startup>();
            test.HttpTrigger<PersonFunction>()
                .Run(f => f.Run(test.CreateHttpRequest(HttpMethod.Get, "person", null), test.Logger))
                .AssertOK("This HTTP triggered function executed successfully. Pass a name in the query string or in the request body for a personalized response.");
        }

        [TestMethod]
        public void QueryString()
        {
            using var test = FunctionTester.Create<Startup>();
            test.HttpTrigger<PersonFunction>()
                .Run(f => f.Run(test.CreateHttpRequest(HttpMethod.Get, "person?name=Trevor", null), test.Logger))
                .AssertOK("Hello, Trevor. This HTTP triggered function executed successfully.");
        }

        [TestMethod]
        public void WithBody()
        {
            using var test = FunctionTester.Create<Startup>();
            test.HttpTrigger<PersonFunction>()
                .Run(f => f.Run(test.CreateJsonHttpRequest(HttpMethod.Get, "person", new { name = "Jane" }), test.Logger))
                .AssertOK("Hello, Jane. This HTTP triggered function executed successfully.");
        }

        [TestMethod]
        public void BadRequest1()
        {
            using var test = FunctionTester.Create<Startup>();
            test.HttpTrigger<PersonFunction>()
                .Run(f => f.Run(test.CreateJsonHttpRequest(HttpMethod.Post, "person", new { name = "Brian" }), test.Logger))
                .AssertBadRequest("Name cannot be Brian.");
        }

        [TestMethod]
        public void BadRequest2()
        {
            using var test = FunctionTester.Create<Startup>();
            test.HttpTrigger<PersonFunction>()
                .Run(f => f.Run(test.CreateJsonHttpRequest(HttpMethod.Post, "person", new { name = "Brian" }), test.Logger))
                .AssertBadRequest(new ApiError("name", "Name cannot be Brian."));
        }

        [TestMethod]
        public void ValidJson()
        {
            using var test = FunctionTester.Create<Startup>();
            test.HttpTrigger<PersonFunction>()
                .Run(f => f.Run(test.CreateJsonHttpRequest(HttpMethod.Get, "person", new { name = "Rachel" }), test.Logger))
                .AssertOK(new { FirstName = "Rachel", LastName = "Smith" });
        }

        [TestMethod]
        public void ValidJsonResource()
        {
            using var test = FunctionTester.Create<Startup>();
            test.HttpTrigger<PersonFunction>()
                .Run(f => f.Run(test.CreateJsonHttpRequest(HttpMethod.Get, "person", new { name = "Rachel" }), test.Logger))
                .AssertOKFromJsonResource("FunctionTest-ValidJsonResource.json");
        }

        [TestMethod]
        public void ValueVsHttpRequestObject()
        {
            using var test = FunctionTester.Create<Startup>();
            test.HttpTrigger<PersonFunction>()
                .Run(f => f.RunWithValue(new Person { FirstName = "Rachel", LastName = "Smith" }, test.Logger))
                .AssertOK(new { first = "Rachel", last = "Smith" });
        }
    }
}