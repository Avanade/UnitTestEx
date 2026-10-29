using Microsoft.Extensions.DependencyInjection;
using Moq;
using NUnit.Framework;
using UnitTestEx.Generic;

namespace UnitTestEx.NUnit.Test.Other
{
    [TestFixture]
    public class DependencyInjectionTest
    {
        public interface IGreeter { string Greet(string name); }

        public class Greeter : IGreeter { public string Greet(string name) => $"Hello {name}"; }

        public class StubGreeter : IGreeter { public string Greet(string name) => $"Stub {name}"; }

        /// <summary>
        /// Creates a tester with the default scoped greeter registration.
        /// </summary>
        /// <returns>The configured generic tester.</returns>
        private static GenericTesterBase<object, GenericTester<object>> CreateTester() => GenericTester.Create().ConfigureServices(sc => sc.AddScoped<IGreeter, Greeter>());

        [Test]
        public void ReplaceSingleton_OverridesRegistration()
        {
            using var test = CreateTester().ReplaceSingleton<IGreeter>(new StubGreeter());
            test.Run<IGreeter, string>(g => g.Greet("Bob"))
                .AssertSuccess()
                .AssertValue("Stub Bob");
        }

        [Test]
        public void ReplaceScoped_OverridesRegistration()
        {
            // Scoped services must be resolved within a scope; Run<TService> resolves from the root provider so use ScopedType.
            using var test = CreateTester().ReplaceScoped<IGreeter, StubGreeter>();
            test.ScopedType<IGreeter>(st => st
                .Run(g => g.Greet("Bob"))
                .AssertValue("Stub Bob"));
        }

        [Test]
        public void MockSingleton_UsesMoqObject()
        {
            var mock = new Mock<IGreeter>();
            mock.Setup(x => x.Greet("Bob")).Returns("Mocked Bob");

            using var test = CreateTester().MockSingleton(mock);
            test.Run<IGreeter, string>(g => g.Greet("Bob"))
                .AssertValue("Mocked Bob");

            mock.Verify(x => x.Greet("Bob"), Times.Once);
        }

        [Test]
        public void ScopedType_RunsWithinServiceScope()
        {
            using var test = CreateTester();
            test.ScopedType<IGreeter>(st => st
                .Run(g => g.Greet("Bob"))
                .AssertValue("Hello Bob"));
        }
    }
}
