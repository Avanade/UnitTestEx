using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Net.Mime;
using System.Threading.Tasks;
using UnitTestEx.Api;
using UnitTestEx.Api.Controllers;
using UnitTestEx.Api.Models;
using UnitTestEx.Expectations;

namespace UnitTestEx.MSTest.Test
{
    [TestClass]
    public class PersonControllerTest
    {
        [TestMethod]
        public void VerifyNewtonsoftJsonSerializer()
        {
            using var test = ApiTester.Create<Startup>();
            Assert.IsInstanceOfType(TestSetUp.Default.JsonSerializer, typeof(NewtonsoftJsonSerializer));
        }

        [TestMethod]
        public async Task Get_Test1()
        {
            using var test = ApiTester.Create<Startup>();
            (await test.Controller<PersonController>()
                .RunAsync(c => c.Get(1)))
                .AssertOK()
                .AssertValue(new Person { Id = 1, FirstName = "Bob", LastName = "Smith" });
        }

        [TestMethod]
        public void Get_Test2()
        {
            int id = 2;
            using var test = ApiTester.Create<Startup>().UseJsonSerializer(new NewtonsoftJsonSerializer());
            test.Controller<PersonController>()
                .Run(c => c.Get(id))
                .AssertOK()
                .AssertValue(new Person { Id = id, FirstName = "Jane", LastName = "Jones" });
        }

        [TestMethod]
        public void Get_Test3()
        {
            var p = new Person { Id = 3, FirstName = "Brad", LastName = "Davies" };

            using var test = ApiTester.Create<Startup>();
            test.Controller<PersonController>().Run(c => c.Get(p.Id)).AssertOK().AssertValue(p);
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
                .AssertValue("Mary-Brown-88,99");
        }

        [TestMethod]
        public void GetByArgs_Test2()
        {
            using var test = ApiTester.Create<Startup>();
            test.Controller<PersonController>()
                .Run(c => c.GetByArgs(null, null, null))
                .AssertOK()
                .AssertValue("--");
        }

        [TestMethod]
        public void Update_Test1()
        {
            using var test = ApiTester.Create<Startup>();
            test.Controller<PersonController>()
                .Run(c => c.Update(1, new Person { FirstName = "Bob", LastName = "Smith" }))
                .AssertOK()
                .AssertValue(new Person { Id = 1, FirstName = "Bob", LastName = "Smith" });
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

        [TestMethod]
        public void Update_Test4()
        {
            using var test = ApiTester.Create<Startup>();
            test.Controller<PersonController>()
                .Run(c => c.Update(1, null), new Person { FirstName = null, LastName = null })
                .AssertBadRequest()
                .AssertErrors(
                    new ApiError("firstName", "First name is required."),
                    new ApiError("lastName", "Last name is required."));
        }

        [TestMethod]
        public void Update_Test5()
        {
            using var test = ApiTester.Create<Startup>();
            test.Controller<PersonController>()
                .RunContent(c => c.Update(1, null), "{\"firstName\":null,\"lastName\":null}", MediaTypeNames.Application.Json)
                .AssertBadRequest()
                .AssertErrors(
                    new ApiError("firstName", "First name is required."),
                    new ApiError("lastName", "Last name is required."));
        }

        [TestMethod]
        public void TypeTester()
        {
            using var test = ApiTester.Create<Startup>();
            test.Type<PersonController>()
                .ExpectSuccess()
                .Run(c => c.Get(1))
                .ToActionResultAssertor()
                .AssertOK()
                .AssertValue(new Person { Id = 1, FirstName = "Bob", LastName = "Smith" });
        }
    }
}