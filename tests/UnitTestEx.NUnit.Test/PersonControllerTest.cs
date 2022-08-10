using CoreEx.Configuration;
using CoreEx.Entities;
using CoreEx.Http;
using CoreEx.Json;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using UnitTestEx.Api;
using UnitTestEx.Api.Controllers;
using UnitTestEx.Api.Models;
using UnitTestEx.Expectations;

namespace UnitTestEx.NUnit.Test
{
    [TestFixture]
    public class PersonControllerTest
    {
        [Test]
        public async Task Get_Test1()
        {
            using var test = ApiTester.Create<Startup>();
            (await test.Controller<PersonController>()
                .RunAsync(c => c.Get(1)))
                .AssertOK()
                .Assert(new Person { Id = 1, FirstName = "Bob", LastName = "Smith" });
        }

        [Test]
        public void Get_Test2()
        {
            int id = 2;
            using var test = ApiTester.Create<Startup>();
            test.Controller<PersonController>()
                .Run(c => c.Get(id))
                .AssertOK()
                .Assert(new Person { Id = id, FirstName = "Jane", LastName = "Jones" });
        }

        [Test]
        public void Get_Test3()
        {
            var p = new Person { Id = 3, FirstName = "Brad", LastName = "Davies" };

            using var test = ApiTester.Create<Startup>();
            test.Controller<PersonController>().Run(c => c.Get(p.Id)).AssertOK().Assert(p);
        }

        [Test]
        public void Get_Test4()
        {
            using var test = ApiTester.Create<Startup>();
            test.Controller<PersonController>()
                .Run(c => c.Get(4))
                .AssertNotFound();
        }

        [Test]
        public void GetByArgs_Test1()
        {
            using var test = ApiTester.Create<Startup>();
            test.Controller<PersonController>()
                .Run(c => c.GetByArgs("Mary", "Brown", new List<int> { 88, 99 }))
                .AssertOK()
                .Assert("Mary-Brown-88,99");
        }

        [Test]
        public void GetByArgs_Test2()
        {
            using var test = ApiTester.Create<Startup>();
            test.Controller<PersonController>()
                .Run(c => c.GetByArgs(null, null, null))
                .AssertOK()
                .Assert("--");
        }

        [Test]
        public void Update_Test1()
        {
            using var test = ApiTester.Create<Startup>();
            test.Controller<PersonController>()
                .Run(c => c.Update(1, new Person { FirstName = "Bob", LastName = "Smith" }))
                .AssertOK()
                .Assert(new Person { Id = 1, FirstName = "Bob", LastName = "Smith" });
        }

        [Test]
        public void Update_Test2()
        {
            using var test = ApiTester.Create<Startup>();
            test.Controller<PersonController>()
                .Run(c => c.Update(1, new Person { FirstName = null, LastName = null }))
                .AssertBadRequest()
                .AssertErrors(
                    "First name is required.",
                    "Last name is required.");
        }

        [Test]
        public void Update_Test3()
        {
            using var test = ApiTester.Create<Startup>();
            test.Controller<PersonController>()
                .Run(c => c.Update(1, new Person { FirstName = null, LastName = null }))
                .AssertBadRequest()
                .AssertErrors(
                    new ApiError("firstName", "First name is required."),
                    new ApiError("lastName", "Last name is required."));
        }

        [Test]
        public void Update_Test4()
        {
            using var test = ApiTester.Create<Startup>();
            test.Controller<PersonController>()
                .ExpectStatusCode(System.Net.HttpStatusCode.BadRequest)
                .ExpectErrors(
                    "First name is required.",
                    "Last name is required.")
                .Run(c => c.Update(1, new Person { FirstName = null, LastName = null }));
        }

        [Test]
        public void Update_Test5_ExpectationFailure()
        {
            var ex = Assert.Throws<AssertionException>(() =>
            {
                using var test = ApiTester.Create<Startup>();
                test.Controller<PersonController>()
                    .ExpectStatusCode(System.Net.HttpStatusCode.BadRequest)
                    .ExpectErrors(
                        "First name is requiredx.",
                        "Last name is required.")
                    .Run(c => c.Update(1, new Person { FirstName = null, LastName = null }));
            });

            Assert.IsTrue(ex.Message.Contains("Error: First name is requiredx."));
        }

        [Test]
        public void Update_Test6()
        {
            Assert.Inconclusive("This should be removed when CoreEx v1.0.8 is published.");
            using var test = ApiTester.Create<Startup>();
            test.Controller<PersonController>()
                .ExpectStatusCode(System.Net.HttpStatusCode.BadRequest)
                .ExpectErrorType(CoreEx.Abstractions.ErrorType.ValidationError, "No can do eighty-eight.")
                .Run(c => c.Update(88, new Person { FirstName = null, LastName = null }));
        }

        [Test]
        public void Update_Test7_ExpectationFailure2()
        {
            var ex = Assert.Throws<AssertionException>(() =>
            {
                using var test = ApiTester.Create<Startup>();
                test.Controller<PersonController>()
                    .ExpectStatusCode(System.Net.HttpStatusCode.BadRequest)
                    .ExpectErrorType(CoreEx.Abstractions.ErrorType.BusinessError, "No can do eighty-eight.")
                    .Run(c => c.Update(88, new Person { FirstName = null, LastName = null }));
            });

            Assert.IsTrue(ex.Message.Contains("Expected ErrorType"));
        }

        [Test]
        public void GetPaging()
        {
            using var test = ApiTester.Create<Startup>();
            test.Controller<PersonController>()
                .Run(c => c.GetPaging(), new HttpRequestOptions { Paging = PagingArgs.CreateSkipAndTake(2, 5) })
                .AssertOK()
                .AssertJson("{\"page\":null,\"isSkipTake\":true,\"size\":5,\"skip\":2,\"take\":5,\"isGetCount\":false}");
        }

        [Test]
        public async Task Http_Get1()
        {
            using var test = ApiTester.Create<Startup>();
            (await test.Http()
                .RunAsync(HttpMethod.Get, "Person/1" ))
                .AssertOK()
                .Assert(new Person { Id = 1, FirstName = "Bob", LastName = "Smith" });
        }

        [Test]
        public void Http_Get2()
        {
            using var test = ApiTester.Create<Startup>();
            test.Http()
                .Run(HttpMethod.Get, "Person/1")
                .AssertOK()
                .Assert(new Person { Id = 1, FirstName = "Bob", LastName = "Smith" });
        }

        [Test]
        public void Http_Update1()
        {
            using var test = ApiTester.Create<Startup>();
            test.Http()
                .Run(HttpMethod.Post, "Person/1", new Person { FirstName = "Bob", LastName = "Smith" })
                .AssertOK()
                .Assert(new Person { Id = 1, FirstName = "Bob", LastName = "Smith" });
        }

        [Test]
        public void Http_Update2()
        {
            using var test = ApiTester.Create<Startup>();
            test.Http()
                .ExpectStatusCode(System.Net.HttpStatusCode.MethodNotAllowed)
                .Run(HttpMethod.Put, "Person/1", new Person { FirstName = "Bob", LastName = "Smith" });
        }

        [Test]
        public void Http_Update_Http1()
        {
            using var test = ApiTester.Create<Startup>();
            test.Http<Person>()
                .Run(HttpMethod.Post, "Person/1", new Person { FirstName = "Bob", LastName = "Smith" })
                .AssertOK()
                .Assert(new Person { Id = 1, FirstName = "Bob", LastName = "Smith" });
        }

        [Test]
        public void Http_Update_Http2()
        {
            using var test = ApiTester.Create<Startup>();
            test.Http<Person>()
                .ExpectStatusCode(System.Net.HttpStatusCode.OK)
                .ExpectValue(_ => new Person { Id = 1, FirstName = "Bob", LastName = "Smith" })
                .Run(HttpMethod.Post, "Person/1", new Person { FirstName = "Bob", LastName = "Smith" });
        }

        [Test]
        public void Http_Update_Http3()
        {
            using var test = ApiTester.Create<Startup>();
            test.Http<Person>()
                .ExpectStatusCode(System.Net.HttpStatusCode.OK)
                .ExpectValue(new Person { Id = 1, FirstName = "Bob", LastName = "Smith" })
                .Run(HttpMethod.Post, "Person/1", new Person { FirstName = "Bob", LastName = "Smith" });
        }

        [Test]
        public void Http_Get_Typed1()
        {
            using var test = ApiTester.Create<Startup>();
            test.Agent<PersonAgent, Person>()
                .Run(a => a.GetAsync(1))
                .AssertOK()
                .Assert(new Person { Id = 1, FirstName = "Bob", LastName = "Smith" });
        }

        [Test]
        public void Http_Get_Typed2()
        {
            using var test = ApiTester.Create<Startup>();
            var v = test.Agent<PersonAgent>()
                .Run(a => a.GetAsync(1))
                .AssertOK()
                .Assert(new Person { Id = 1, FirstName = "Bob", LastName = "Smith" })
                .Value;

            Assert.NotNull(v);
        }

        [Test]
        public void Http_Get_Typed3()
        {
            using var test = ApiTester.Create<Startup>();
            var v = test.Agent<PersonAgent, Person>()
                .ExpectStatusCode(System.Net.HttpStatusCode.OK)
                .ExpectValue(new Person { Id = 1, FirstName = "Bob", LastName = "Smith" })
                .Run(a => a.GetAsync(1))
                .Value;

            Assert.NotNull(v);
        }

        [Test]
        public void Http_Update_Typed1()
        {
            using var test = ApiTester.Create<Startup>();
            test.Agent<PersonAgent, Person>()
                .Run(a => a.UpdateAsync(new Person { FirstName = "Bob", LastName = "Smith" }, 1))
                .AssertOK()
                .Assert(new Person { Id = 1, FirstName = "Bob", LastName = "Smith" });
        }

        [Test]
        public void Http_Update_Typed2()
        {
            using var test = ApiTester.Create<Startup>();
            var v = test.Agent<PersonAgent, Person>()
                .ExpectStatusCode(System.Net.HttpStatusCode.OK)
                .ExpectValue(new Person { Id = 1, FirstName = "Bob", LastName = "Smith" })
                .Run(a => a.UpdateAsync(new Person { FirstName = "Bob", LastName = "Smith" }, 1))
                .Value;

            Assert.NotNull(v);
        }
    }

    public class PersonAgent : CoreEx.Http.TypedHttpClientBase<PersonAgent>
    {
        public PersonAgent(HttpClient client, IJsonSerializer jsonSerializer, CoreEx.ExecutionContext executionContext, SettingsBase settings, ILogger<PersonAgent> logger)
            : base(client, jsonSerializer, executionContext, settings, logger) { }
        public Task<HttpResult<Person>> GetAsync(int id, HttpRequestOptions requestOptions = null, CancellationToken cancellationToken = default)
            => GetAsync<Person>("Person/{id}", requestOptions: requestOptions, args: new IHttpArg[] { new HttpArg<int>("id", id) }, cancellationToken: cancellationToken);

        public Task<HttpResult<Person>> UpdateAsync(Person value, int id, HttpRequestOptions requestOptions = null, CancellationToken cancellationToken = default)
            => PostAsync<Person, Person>("Person/{id}", value, requestOptions: requestOptions, args: new IHttpArg[] { new HttpArg<int>("id", id) }, cancellationToken: cancellationToken);
    }
}