using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;
using System.Collections.Generic;
using System.Net.Http;
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
                .ExpectLogContains("Get using identifier 1")
                .RunAsync(c => c.Get(1)))
                .AssertOK()
                .AssertValue(new Person { Id = 1, FirstName = "Bob", LastName = "Smith" });
        }

        [Test]
        public void Get_Test2()
        {
            int id = 2;
            using var test = ApiTester.Create<Startup>();
            test.Controller<PersonController>()
                .Run(c => c.Get(id))
                .AssertOK()
                .AssertValue(new Person { Id = id, FirstName = "Jane", LastName = "Jones" });
        }

        [Test]
        public void Get_Test3()
        {
            var p = new Person { Id = 3, FirstName = "Brad", LastName = "Davies" };

            using var test = ApiTester.Create<Startup>();
            test.Controller<PersonController>().Run(c => c.Get(p.Id)).AssertOK().AssertValue(p);
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
        public void Get_Test4_WithResetHost()
        {
            using var test = ApiTester.Create<Startup>().ResetHost();
            test.Controller<PersonController>()
                .Run(c => c.Get(4))
                .AssertNotFound();
        }

        [Test]
        public async Task Get_Test5_AnonymousType_OK()
        {
            using var test = ApiTester.Create<Startup>();
            (await test.Controller<PersonController>()
                .RunAsync(c => c.Get(1)))
                .AssertOK()
                .AssertValue(new { Id = 1, FirstName = "Bob", LastName = "Smith" });
        }

        [Test]
        public void Get_Test5_AnonymousType_AssertFail()
        {
            Assert.Throws<AssertionException>(() =>
            {
                using var test = ApiTester.Create<Startup>();
                test.Controller<PersonController>()
                    .Run(c => c.Get(1))
                    .AssertOK()
                    .AssertValue(new { Fruit = "Apple" });
            });
        }

        [Test]
        public void Get_Test5_AnonymousType_AssertFail2()
        {
            Assert.Throws<AssertionException>(() =>
            {
                using var test = ApiTester.Create<Startup>();
                test.Controller<PersonController>()
                    .Run(c => c.Get(1))
                    .AssertOK()
                    .AssertJson("{\"data\": \"this_is_a_test\"}");
            });
        }

        [Test]
        public void GetByArgs_Test1()
        {
            using var test = ApiTester.Create<Startup>();
            test.Controller<PersonController>()
                .Run(c => c.GetByArgs("Mary", "Brown", new List<int> { 88, 99 }))
                .AssertOK()
                .AssertValue("Mary-Brown-88,99");
        }

        [Test]
        public void GetByArgs_Test2()
        {
            using var test = ApiTester.Create<Startup>();
            test.Controller<PersonController>()
                .Run(c => c.GetByArgs(null, null, null))
                .AssertOK()
                .AssertValue("--");
        }

        [Test]
        public void Update_Test1()
        {
            using var test = ApiTester.Create<Startup>();
            test.Controller<PersonController>()
                .Run(c => c.Update(1, new Person { FirstName = "Bob", LastName = "Smith" }))
                .AssertOK()
                .AssertValue(new Person { Id = 1, FirstName = "Bob", LastName = "Smith" });
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

            Assert.That(ex.Message, Does.Contain("Error: First name is requiredx."));
        }

        [Test]
        public void Update_Test6()
        {
            using var test = ApiTester.Create<Startup>();
            test.Controller<PersonController>()
                .ExpectStatusCode(System.Net.HttpStatusCode.BadRequest)
                .ExpectError("No can do eighty-eight.")
                .Run(c => c.Update(88, new Person { FirstName = null, LastName = null }));
        }

        [Test]
        public void Update_Test7_ExpectationFailure2()
        {
            using var test = ApiTester.Create<Startup>();
            test.Controller<PersonController>()
                .ExpectStatusCode(System.Net.HttpStatusCode.BadRequest)
                .ExpectError("No can do eighty-eight.")
                .Run(c => c.Update(88, new Person { FirstName = null, LastName = null }));
        }

        [Test]
        public async Task Http_Get1()
        {
            using var test = ApiTester.Create<Startup>();
            (await test.Http()
                .ExpectLogContains("Get using identifier 1")
                .RunAsync(HttpMethod.Get, "Person/1" ))
                .AssertOK()
                .AssertValue(new Person { Id = 1, FirstName = "Bob", LastName = "Smith" });
        }

        [Test]
        public void Http_Get2()
        {
            using var test = ApiTester.Create<Startup>();
            test.Http()
                .Run(HttpMethod.Get, "Person/1")
                .AssertOK()
                .AssertValue(new Person { Id = 1, FirstName = "Bob", LastName = "Smith" });
        }

        [Test]
        public void Http_Update1()
        {
            using var test = ApiTester.Create<Startup>();
            test.Http()
                .Run(HttpMethod.Post, "Person/1", new Person { FirstName = "Bob", LastName = "Smith" })
                .AssertOK()
                .AssertValue(new Person { Id = 1, FirstName = "Bob", LastName = "Smith" });
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
                .AssertValue(new Person { Id = 1, FirstName = "Bob", LastName = "Smith" });
        }

        [Test]
        public void Http_Update_Http2()
        {
            using var test = ApiTester.Create<Startup>();
            test.Http<Person>()
                .ExpectStatusCode(System.Net.HttpStatusCode.OK)
                .ExpectValue(new Person { Id = 1, FirstName = "Bob", LastName = "Smith" })
                .Run(HttpMethod.Post, "Person/1", new Person { FirstName = "Bob", LastName = "Smith" });
        }

        [Test]
        public void Http_Update_Http3()
        {
            using var test = ApiTester.Create<Startup>();
            test.Http<Person>()
                .ExpectStatusCode(System.Net.HttpStatusCode.OK)
                .ExpectValue(_ => new Person { Id = 1, FirstName = "Bob", LastName = "Smith" })
                .Run(HttpMethod.Post, "Person/1", new Person { FirstName = "Bob", LastName = "Smith" });
        }

        [Test]
        public void Http_Update_Http3_AsJson()
        {
            using var test = ApiTester.Create<Startup>();
            test.Http<Person>()
                .ExpectStatusCode(System.Net.HttpStatusCode.OK)
                .ExpectJson("{ \"id\": 1, \"firstName\": \"Bob\", \"lastName\": \"Smith\" }")
                .Run(HttpMethod.Post, "Person/1", new Person { FirstName = "Bob", LastName = "Smith" });
        }

        [Test]
        public void Http_Update_Http4()
        {
            using var test = ApiTester.Create<Startup>();
            test.Http<Person>()
                .Run(HttpMethod.Post, "Person/99", new Person { FirstName = "Bob", LastName = "Smith" })
                .AssertNotFound();
        }

        [Test]
        public void Http_Update_Http5()
        {
            using var test = ApiTester.Create<Startup>();
            test.Http<Person>()
                .Run(HttpMethod.Post, "Person/88", new Person { FirstName = "Bob", LastName = "Smith" })
                .Assert(System.Net.HttpStatusCode.BadRequest, "No can do eighty-eight.");
        }

        [Test]
        public void Http_Update_Http6()
        {
            using var test = ApiTester.Create<Startup>();
            test.Http<Person>()
                .Run(HttpMethod.Post, "Person/1", new Person { FirstName = "Bob", LastName = "Smith" })
                .AssertValue(new Person { Id = 1, FirstName = "Bob", LastName = "Smith" });
        }

        [Test]
        public void Type_IActionResult()
        {
            using var test = ApiTester.Create<Startup>();
            var hr = test.CreateHttpRequest(HttpMethod.Get, "Person/1");
            hr.HttpContext.Response.Headers.Add("X-Test", "Test");

            var iar = new OkResult();

            new Assertors.ValueAssertor<IActionResult>(test, iar, null)
                .ToHttpResponseMessageAssertor(hr)
                .AssertNamedHeader("X-Test", "Test");
        }
    }
}