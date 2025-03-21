using NUnit.Framework;
using System.Net.Http;
using System.Threading.Tasks;
using UnitTestEx.Function;

namespace UnitTestEx.NUnit.Test
{
    [TestFixture]
    public class PersonFunctionTest
    {
        [Test]
        public async Task NoData()
        {
            using var test = FunctionTester.Create<Startup>();
            (await test.HttpTrigger<PersonFunction>()
                .WithNoRouteCheck()
                .RunAsync(f => f.Run(test.CreateHttpRequest(HttpMethod.Get, "person"), test.Logger)))
                .AssertOK()
                .AssertValue("This HTTP triggered function executed successfully. Pass a name in the query string or in the request body for a personalized response.");
        }

        [Test]
        public void QueryString()
        {
            using var test = FunctionTester.Create<Startup>();
            test.HttpTrigger<PersonFunction>()
                .WithNoRouteCheck()
                .Run(f => f.Run(test.CreateHttpRequest(HttpMethod.Get, "person?name=Trevor"), test.Logger))
                .AssertOK()
                .AssertValue("Hello, Trevor. This HTTP triggered function executed successfully.");
        }

        [Test]
        public void WithBody()
        {
            using var test = FunctionTester.Create<Startup>();
            test.HttpTrigger<PersonFunction>()
                .WithNoRouteCheck()
                .Run(f => f.Run(test.CreateJsonHttpRequest(HttpMethod.Get, "person", new { name = "Jane" }), test.Logger))
                .AssertOK()
                .AssertValue("Hello, Jane. This HTTP triggered function executed successfully.");
        }

        [Test]
        public void BadRequest1()
        {
            using var test = FunctionTester.Create<Startup>();
            test.HttpTrigger<PersonFunction>()
                .WithNoMethodCheck()
                .WithNoRouteCheck()
                .Run(f => f.Run(test.CreateJsonHttpRequest(HttpMethod.Delete, "person", new { name = "Brian" }), test.Logger))
                .AssertBadRequest()
                .AssertErrors("Name cannot be Brian.");
        }

        [Test]
        public void BadRequest2()
        {
            using var test = FunctionTester.Create<Startup>();
            test.HttpTrigger<PersonFunction>()
                .WithNoRouteCheck()
                .Run(f => f.Run(test.CreateJsonHttpRequest(HttpMethod.Post, "person", new { name = "Brian" }), test.Logger))
                .AssertBadRequest()
                .AssertErrors(new ApiError("name", "Name cannot be Brian."));
        }

        [Test]
        public void ValidJson()
        {
            using var test = FunctionTester.Create<Startup>();
            test.HttpTrigger<PersonFunction>()
                .WithNoRouteCheck()
                .Run(f => f.Run(test.CreateJsonHttpRequest(HttpMethod.Get, "person", new { name = "Rachel" }), test.Logger))
                .AssertOK()
                .AssertValue(new { FirstName = "Rachel", LastName = "Smith" });
        }

        [Test]
        public void ValidJsonResource()
        {
            using var test = FunctionTester.Create<Startup>();
            test.HttpTrigger<PersonFunction>()
                .WithNoRouteCheck()
                .Run(f => f.Run(test.CreateJsonHttpRequest(HttpMethod.Get, "person", new { name = "Rachel" }), test.Logger))
                .AssertOK()
                .AssertValueFromJsonResource<Person>("FunctionTest-ValidJsonResource.json");
        }

        [Test]
        public void ValueVsHttpRequestObject()
        {
            using var test = FunctionTester.Create<Startup>();
            test.HttpTrigger<PersonFunction>()
                .Run(f => f.RunWithValue(new Person { FirstName = "Rachel", LastName = "Smith" }, test.Logger))
                .AssertOK()
                .AssertValue(new { first = "Rachel", last = "Smith" });
        }

        [Test]
        public void ValueVsHttpRequestContent()
        {
            using var test = FunctionTester.Create<Startup>();
            test.HttpTrigger<PersonFunction>()
                .Run(f => f.RunWithContent(new Person { FirstName = "Rachel", LastName = "Smith" }, test.Logger))
                .AssertOK()
                .AssertValue(new { first = "Rachel", last = "Smith" });
        }

        [Test]
        public void WithPathAndQueryCheck()
        {
            using var test = FunctionTester.Create<Startup>();
            test.HttpTrigger<PersonFunction>()
                .WithRouteCheck(Azure.Functions.RouteCheckOption.Query)
                .Run(f => f.RunWithQuery(test.CreateHttpRequest(HttpMethod.Get, "https://blah/api/persons?name=Damien"), "Damien", test.Logger))
                .AssertOK()
                .AssertValue(new { name = "Damien" });
        }

        [Test]
        public void WithPathAndQueryStartsWithCheck()
        {
            using var test = FunctionTester.Create<Startup>();
            test.HttpTrigger<PersonFunction>()
                .WithRouteCheck(Azure.Functions.RouteCheckOption.QueryStartsWith)
                .Run(f => f.RunWithQuery(test.CreateHttpRequest(HttpMethod.Get, "https://blah/api/persons?name=Damien&$order=name"), "Damien", test.Logger))
                .AssertOK()
                .AssertValue(new { name = "Damien" });
        }
    }
}