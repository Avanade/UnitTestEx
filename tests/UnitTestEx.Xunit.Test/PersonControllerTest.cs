using System.Collections.Generic;
using System.Threading.Tasks;
using UnitTestEx.Api;
using UnitTestEx.Api.Controllers;
using UnitTestEx.Api.Models;
using UnitTestEx.Xunit;
using Xunit;
using Xunit.Abstractions;

namespace UnitTestEx.Xunit.Test
{
    public class PersonControllerTest : UnitTestBase
    {
        public PersonControllerTest(ITestOutputHelper output) : base(output) { }

        [Fact]
        public async Task Get_Test1()
        {
            using var test = CreateApiTester<Startup>();
            (await test.Controller<PersonController>()
                .RunAsync(c => c.Get(1)))
                .AssertOK()
                .Assert(new Person { Id = 1, FirstName = "Bob", LastName = "Smith" });
        }

        [Fact]
        public void Get_Test2()
        {
            int id = 2;
            using var test = CreateApiTester<Startup>();
            test.Controller<PersonController>()
                .Run(c => c.Get(id))
                .AssertOK()
                .Assert(new Person { Id = id, FirstName = "Jane", LastName = "Jones" });
        }

        [Fact]
        public void Get_Test3()
        {
            var p = new Person { Id = 3, FirstName = "Brad", LastName = "Davies" };

            using var test = CreateApiTester<Startup>();
            test.Controller<PersonController>().Run(c => c.Get(p.Id)).AssertOK().Assert(p);
        }

        [Fact]
        public void Get_Test4()
        {
            using var test = CreateApiTester<Startup>();
            test.Controller<PersonController>()
                .Run(c => c.Get(4))
                .AssertNotFound();
        }

        [Fact]
        public void GetByArgs_Test1()
        {
            using var test = CreateApiTester<Startup>();
            test.Controller<PersonController>()
                .Run(c => c.GetByArgs("Mary", "Brown", new List<int> { 88, 99 }))
                .AssertOK()
                .Assert("Mary-Brown-88,99");
        }

        [Fact]
        public void GetByArgs_Test2()
        {
            using var test = CreateApiTester<Startup>();
            test.Controller<PersonController>()
                .Run(c => c.GetByArgs(null, null, null))
                .AssertOK()
                .Assert("--");
        }

        [Fact]
        public void Update_Test1()
        {
            using var test = CreateApiTester<Startup>();
            test.Controller<PersonController>()
                .Run(c => c.Update(1, new Person { FirstName = "Bob", LastName = "Smith" }))
                .AssertOK()
                .Assert(new Person { Id = 1, FirstName = "Bob", LastName = "Smith" });
        }

        [Fact]
        public void Update_Test2()
        {
            using var test = CreateApiTester<Startup>();
            test.Controller<PersonController>()
                .Run(c => c.Update(1, new Person { FirstName = null, LastName = null }))
                .AssertBadRequest()
                .AssertErrors(
                    "First name is required.",
                    "Last name is required.");
        }

        [Fact]
        public void Update_Test3()
        {
            using var test = CreateApiTester<Startup>();
            test.Controller<PersonController>()
                .Run(c => c.Update(1, new Person { FirstName = null, LastName = null }))
                .AssertBadRequest()
                .AssertErrors(
                    new ApiError("firstName", "First name is required."),
                    new ApiError("lastName", "Last name is required."));
        }
    }
}