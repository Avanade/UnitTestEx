using NUnit.Framework;
using System.Net.Http;
using System.Threading.Tasks;
using UnitTestEx.Api;
using UnitTestEx.Api.Models;

namespace UnitTestEx.NUnit.Test.Other
{
    [TestFixture]
    public class UsingApiTesterTest : UsingApiTester<Startup>
    {
        [Test]
        public async Task Get1()
        {
            (await Http()
                .RunAsync(HttpMethod.Get, "Person/1"))
                .AssertOK()
                .Assert(new Person { Id = 1, FirstName = "Bob", LastName = "Smith" });
        }

        [Test]
        public void Get2()
        {
            Agent<PersonAgent, Person>()
                .Run(a => a.GetAsync(1))
                .AssertOK()
                .Assert(new Person { Id = 1, FirstName = "Bob", LastName = "Smith" });
        }

        [Test]
        public void Get3()
        {
            Controller<Api.Controllers.PersonController>()
                .Run(a => a.Get(1))
                .AssertOK()
                .Assert(new Person { Id = 1, FirstName = "Bob", LastName = "Smith" });
        }
    }
}