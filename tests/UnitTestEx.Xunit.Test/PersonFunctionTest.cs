using CoreEx.WebApis;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;
using System.Threading.Tasks;
using UnitTestEx.Function;
using UnitTestEx.Xunit;
using Xunit;
using Xunit.Abstractions;

namespace UnitTestEx.Xunit.Test
{
    public class PersonFunctionTest : UnitTestBase
    {
        public PersonFunctionTest(ITestOutputHelper output) : base(output) { }

        [Fact]
        public async Task NoData()
        {
            using var test = CreateFunctionTester<Startup>();
            (await test.HttpTrigger<PersonFunction>()
                .RunAsync(f => f.Run(test.CreateHttpRequest(HttpMethod.Get, "person"), test.Logger)))
                .AssertOK()
                .Assert("This HTTP triggered function executed successfully. Pass a name in the query string or in the request body for a personalized response.");
        }

        [Fact]
        public void QueryString()
        {
            using var test = CreateFunctionTester<Startup>();
            test.HttpTrigger<PersonFunction>()
                .Run(f => f.Run(test.CreateHttpRequest(HttpMethod.Get, "person?name=Trevor"), test.Logger))
                .AssertOK()
                .Assert("Hello, Trevor. This HTTP triggered function executed successfully.");
        }

        [Fact]
        public void WithBody()
        {
            using var test = CreateFunctionTester<Startup>();
            test.HttpTrigger<PersonFunction>()
                .Run(f => f.Run(test.CreateJsonHttpRequest(HttpMethod.Get, "person", new { name = "Jane" }), test.Logger))
                .AssertOK()
                .Assert("Hello, Jane. This HTTP triggered function executed successfully.");
        }

        [Fact]
        public void BadRequest1()
        {
            using var test = CreateFunctionTester<Startup>();
            test.HttpTrigger<PersonFunction>()
                .Run(f => f.Run(test.CreateJsonHttpRequest(HttpMethod.Post, "person", new { name = "Brian" }), test.Logger))
                .AssertBadRequest()
                .AssertErrors("Name cannot be Brian.");
        }

        [Fact]
        public void BadRequest2()
        {
            using var test = CreateFunctionTester<Startup>();
            test.HttpTrigger<PersonFunction>()
                .Run(f => f.Run(test.CreateJsonHttpRequest(HttpMethod.Post, "person", new { name = "Brian" }), test.Logger))
                .AssertBadRequest()
                .AssertErrors(new ApiError("name", "Name cannot be Brian."));
        }

        [Fact]
        public void BadRequest3()
        {
            using var test = CreateFunctionTester<Startup>();
            test.HttpTrigger<PersonFunction>()
                .Run(f => f.Run(test.CreateJsonHttpRequest(HttpMethod.Post, "person", new { name = "Damien" }), test.Logger))
                .AssertException<CoreEx.ValidationException>("Name cannot be Damien.");
        }

        [Fact]
        public void BadRequest4()
        {
            using var test = CreateFunctionTester<Startup>();
            test.HttpTrigger<PersonFunction>()
                .Run(f => f.Run(test.CreateJsonHttpRequest(HttpMethod.Post, "person", new { name = "Bruce" }), test.Logger))
                .AssertBadRequest()
                .AssertErrors("Name cannot be Bruce.");
        }

        [Fact]
        public void BadRequestWebApi()
        {
            using var test = CreateFunctionTester<Startup>().ConfigureServices(sc => sc.AddWebApi().AddExecutionContext().AddDefaultSettings().AddJsonSerializer());
            test.HttpTrigger<PersonFunction>()
                .Run(f => f.RunWebApi(test.CreateJsonHttpRequest(HttpMethod.Get, "person", new { name = "Damien" }), test.Logger))
                .AssertBadRequest()
                .AssertErrors("Name cannot be Damien.");
        }

        [Fact]
        public void ValidJson()
        {
            using var test = CreateFunctionTester<Startup>();
            test.HttpTrigger<PersonFunction>()
                .Run(f => f.Run(test.CreateJsonHttpRequest(HttpMethod.Get, "person", new { name = "Rachel" }), test.Logger))
                .AssertOK()
                .Assert(new { FirstName = "Rachel", LastName = "Smith" });
        }

        [Fact]
        public void ValidJsonResource()
        {
            using var test = CreateFunctionTester<Startup>();
            test.HttpTrigger<PersonFunction>()
                .Run(f => f.Run(test.CreateJsonHttpRequest(HttpMethod.Get, "person", new { name = "Rachel" }), test.Logger))
                .AssertOK()
                .AssertFromJsonResource<Person>("FunctionTest-ValidJsonResource.json");
        }

        [Fact]
        public void ValueVsHttpRequestObject()
        {
            using var test = CreateFunctionTester<Startup>();
            test.HttpTrigger<PersonFunction>()
                .Run(f => f.RunWithValue(new Person { FirstName = "Rachel", LastName = "Smith" }, test.Logger))
                .AssertOK()
                .Assert(new { first = "Rachel", last = "Smith" });
        }

        [Fact]
        public void ValueVsHttpRequestContent()
        {
            using var test = CreateFunctionTester<Startup>();
            test.HttpTrigger<PersonFunction>()
                .Run(f => f.RunWithContent(new Person { FirstName = "Rachel", LastName = "Smith" }, test.Logger))
                .AssertOK()
                .Assert(new { first = "Rachel", last = "Smith" });
        }
    }
}