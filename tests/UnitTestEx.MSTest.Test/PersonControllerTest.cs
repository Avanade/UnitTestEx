using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnitTestEx.Api;
using UnitTestEx.Api.Controllers;
using UnitTestEx.Api.Models;

namespace UnitTestEx.MSTest.Test
{
    [TestClass]
    public class PersonControllerTest
    {
        [TestMethod]
        public async Task Get_Test1()
        {
            using var test = ApiTester.Create<Startup>();
            (await test.Controller<PersonController>()
                .RunAsync(c => c.Get(1)))
                .AssertOK()
                .Assert(new Person { Id = 1, FirstName = "Bob", LastName = "Smith" });
        }

        [TestMethod]
        public void Get_Test2()
        {
            int id = 2;
            using var test = ApiTester.Create<Startup>();
            test.Controller<PersonController>()
                .Run(c => c.Get(id))
                .AssertOK()
                .Assert(new Person { Id = id, FirstName = "Jane", LastName = "Jones" });
        }

        [TestMethod]
        public void Get_Test3()
        {
            var p = new Person { Id = 3, FirstName = "Brad", LastName = "Davies" };

            using var test = ApiTester.Create<Startup>();
            test.Controller<PersonController>().Run(c => c.Get(p.Id)).AssertOK().Assert(p);
        }

        [TestMethod]
        public void Get_Test4()
        {
            using var test = ApiTester.Create<Startup>();
            test.Controller<PersonController>()
                .Run(c => c.Get(4))
                .AssertNotFound();
        }

        [TestMethod]
        public void GetByArgs_Test1()
        {
            using var test = ApiTester.Create<Startup>();
            test.Controller<PersonController>()
                .Run(c => c.GetByArgs("Mary", "Brown", new List<int> { 88, 99 }))
                .AssertOK()
                .Assert("Mary-Brown-88,99");
        }

        [TestMethod]
        public void GetByArgs_Test2()
        {
            using var test = ApiTester.Create<Startup>();
            test.Controller<PersonController>()
                .Run(c => c.GetByArgs(null, null, null))
                .AssertOK()
                .Assert("--");
        }

        [TestMethod]
        public void Update_Test1()
        {
            using var test = ApiTester.Create<Startup>();
            test.Controller<PersonController>()
                .Run(c => c.Update(1, new Person { FirstName = "Bob", LastName = "Smith" }))
                .AssertOK()
                .Assert(new Person { Id = 1, FirstName = "Bob", LastName = "Smith" });
        }

        [TestMethod]
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

        [TestMethod]
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
    }
}