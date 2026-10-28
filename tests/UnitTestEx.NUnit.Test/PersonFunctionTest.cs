using NUnit.Framework;
using System.Net.Http;
using System.Threading.Tasks;
using UnitTestEx.IsolatedFunction;

namespace UnitTestEx.NUnit.Test
{
    [TestFixture]
    public class PersonFunctionTest
    {
        [Test]
        public async Task NoData()
        {
            using var test = FunctionTester.Create<PersonFunction>();
            (await test.HttpTrigger<PersonFunction>()
                .WithNoRouteCheck()
                .RunAsync(f => f.Run(test.CreateHttpRequest(HttpMethod.Get, "person"))))
                .AssertOK()
                .AssertValue("This HTTP triggered function executed successfully. Pass a name in the query string or in the request body for a personalized response.");
        }

        [Test]
        public void QueryString()
        {
            using var test = FunctionTester.Create<PersonFunction>();
            test.HttpTrigger<PersonFunction>()
                .WithNoRouteCheck()
                .Run(f => f.Run(test.CreateHttpRequest(HttpMethod.Get, "person?name=Trevor")))
                .AssertOK()
                .AssertValue("Hello, Trevor. This HTTP triggered function executed successfully.");
        }

        [Test]
        public void WithBody()
        {
            using var test = FunctionTester.Create<PersonFunction>();
            test.HttpTrigger<PersonFunction>()
                .WithNoRouteCheck()
                .Run(f => f.Run(test.CreateJsonHttpRequest(HttpMethod.Get, "person", new { name = "Jane" })))
                .AssertOK()
                .AssertValue("Hello, Jane. This HTTP triggered function executed successfully.");
        }

        [Test]
        public void BadRequest1()
        {
            using var test = FunctionTester.Create<PersonFunction>();
            test.HttpTrigger<PersonFunction>()
                .WithNoMethodCheck()
                .WithNoRouteCheck()
                .Run(f => f.Run(test.CreateJsonHttpRequest(HttpMethod.Delete, "person", new { name = "Brian" })))
                .AssertBadRequest()
                .AssertErrors("Name cannot be Brian.");
        }

        [Test]
        public void BadRequest2()
        {
            using var test = FunctionTester.Create<PersonFunction>();
            test.HttpTrigger<PersonFunction>()
                .WithNoRouteCheck()
                .Run(f => f.Run(test.CreateJsonHttpRequest(HttpMethod.Post, "person", new { name = "Brian" })))
                .AssertBadRequest()
                .AssertErrors(new ApiError("name", "Name cannot be Brian."));
        }

        [Test]
        public void ValidJson()
        {
            using var test = FunctionTester.Create<PersonFunction>();
            test.HttpTrigger<PersonFunction>()
                .WithNoRouteCheck()
                .Run(f => f.Run(test.CreateJsonHttpRequest(HttpMethod.Get, "person", new { name = "Rachel" })))
                .AssertOK()
                .AssertValue(new { FirstName = "Rachel", LastName = "Smith" });
        }

        [Test]
        public void ValidJsonResource()
        {
            using var test = FunctionTester.Create<PersonFunction>();
            test.HttpTrigger<PersonFunction>()
                .WithNoRouteCheck()
                .Run(f => f.Run(test.CreateJsonHttpRequest(HttpMethod.Get, "person", new { name = "Rachel" })))
                .AssertOK()
                .AssertValueFromJsonResource<Person>("FunctionTest-ValidJsonResource.json");
        }

        [Test]
        public void ValueVsHttpRequestObject()
        {
            using var test = FunctionTester.Create<PersonFunction>();
            test.HttpTrigger<PersonFunction>()
                .Run(f => f.RunWithValue(test.CreateJsonHttpRequest(HttpMethod.Post, "people/rachel", new Person { FirstName = "Rachel", LastName = "Smith" }), "rachel"))
                .AssertOK()
                .AssertValue(new { first = "Rachel", last = "Smith" });
        }

        [Test]
        public void ValueVsHttpRequestContent()
        {
            using var test = FunctionTester.Create<PersonFunction>();
            test.HttpTrigger<PersonFunction>()
                .Run(f => f.RunWithContent(test.CreateJsonHttpRequest(HttpMethod.Post, "people/content/rachel", new Person { FirstName = "Rachel", LastName = "Smith" }), "rachel"))
                .AssertOK()
                .AssertValue(new { first = "Rachel", last = "Smith" });
        }

        [Test]
        public void WithPathAndQueryCheck()
        {
            using var test = FunctionTester.Create<PersonFunction>();
            test.HttpTrigger<PersonFunction>()
                .WithRouteCheck(Azure.Functions.RouteCheckOption.Path)
                .Run(f => f.RunWithQuery(test.CreateHttpRequest(HttpMethod.Get, "https://blah/api/people/Damien?name=Damien"), "Damien"))
                .AssertOK()
                .AssertValue(new { name = "Damien" });
        }

        [Test]
        public void WithPathAndQueryStartsWithCheck()
        {
            using var test = FunctionTester.Create<PersonFunction>();
            test.HttpTrigger<PersonFunction>()
                .WithRouteCheck(Azure.Functions.RouteCheckOption.PathAndQueryStartsWith)
                .Run(f => f.RunWithQuery(test.CreateHttpRequest(HttpMethod.Get, "https://blah/api/people/Damien?name=Damien&$order=name"), "Damien"))
                .AssertOK()
                .AssertValue(new { name = "Damien" });
        }
    }
}