using Microsoft.Extensions.DependencyInjection;
using Moq;
using NUnit.Framework;
using UnitTestEx.Generic;
using System.Collections.Generic;

namespace UnitTestEx.NUnit.Test.Other
{
    [TestFixture]
    public class DependencyInjectionTest
    {
        public interface IGreeter { string Greet(string name); }

        public class Greeter : IGreeter { public string Greet(string name) => $"Hello {name}"; }

        public class StubGreeter : IGreeter { public string Greet(string name) => $"Stub {name}"; }

        public sealed class ScopedResource : System.IDisposable
        {
            public bool IsDisposed { get; private set; }
            public void Dispose() => IsDisposed = true;
        }

        public sealed class ScopedService(ScopedResource resource)
        {
            public ScopedResource Resource { get; } = resource;
        }

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

        [Test]
        public void TypeTester_CreatesServiceWithinFreshScopeForEachRun()
        {
            using var test = GenericTester.Create().ConfigureServices(sc => sc.AddScoped<ScopedResource>());
            var services = new List<ScopedService>();
            var resources = new List<ScopedResource>();
            var tester = test.Type<ScopedService>();

            tester.Run(service =>
            {
                services.Add(service);
                resources.Add(service.Resource);
                Assert.That(service.Resource.IsDisposed, Is.False);
            }).AssertSuccess();

            tester.Run(service =>
            {
                services.Add(service);
                resources.Add(service.Resource);
                Assert.That(service.Resource.IsDisposed, Is.False);
            }).AssertSuccess();

            Assert.That(services[0], Is.Not.SameAs(services[1]));
            Assert.That(resources[0], Is.Not.SameAs(resources[1]));
            Assert.That(resources, Has.All.Property(nameof(ScopedResource.IsDisposed)).True);
        }
    }
}
