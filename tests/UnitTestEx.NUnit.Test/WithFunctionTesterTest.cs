#nullable enable

using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Net.Http;
using UnitTestEx.Abstractions;
using UnitTestEx.Azure.Functions;
using UnitTestEx.IsolatedFunction;

namespace UnitTestEx.NUnit.Test
{
    [TestFixture]
    public class WithFunctionTesterTest
    {
        [Test]
        public void Fixture_ReusesTesterAndLazyHostWithDefaultFramework()
        {
            using var fixture = new FunctionFixture();
            var test = fixture.Test;
            Assert.That(fixture.Test, Is.SameAs(test));
            Assert.That(test.IsHostInstantiated, Is.False);

            test.HttpTrigger<HttpFunction>()
                .Run(f => f.Run(test.CreateHttpRequest(HttpMethod.Get, "/")))
                .AssertOK()
                .AssertValue("Welcome to Azure Functions!");
            var services = test.Services;

            test.HttpTrigger<HttpFunction>()
                .Run(f => f.Run(test.CreateHttpRequest(HttpMethod.Get, "/")))
                .AssertOK()
                .AssertValue("Welcome to Azure Functions!");
            Assert.That(test.Services, Is.SameAs(services));
        }

        [Test]
        public void Fixture_UsesExplicitFactoryOnceWithoutBuildingHost()
        {
            var implementor = new NullTestImplementor();
            var calls = 0;
            using var fixture = new FunctionFixture(() =>
            {
                calls++;
                return implementor;
            });

            Assert.That(fixture.Test.Implementor, Is.SameAs(implementor));
            Assert.That(fixture.Test.IsHostInstantiated, Is.False);
            Assert.That(calls, Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Create_PreservesConfigurationWithExplicitFactory(bool directConstruction)
        {
            var implementor = new NullTestImplementor();
            var calls = 0;
            TestFrameworkImplementor CreateImplementor()
            {
                calls++;
                return implementor;
            }

            using var test = directConstruction
                ? new FunctionTester<HttpFunction>(false, false, [new("FixtureKey", "override")], CreateImplementor)
                : FunctionTester.Create<HttpFunction>(includeUnitTestConfiguration: false, includeUserSecrets: false,
                    additionalConfiguration: [new("FixtureKey", "override")], createFactory: CreateImplementor);

            Assert.That(test.Implementor, Is.SameAs(implementor));
            Assert.That(test.Configuration["FixtureKey"], Is.EqualTo("override"));
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void Fixture_RejectsFactoryReturningNull()
        {
            var exception = Assert.Throws<InvalidOperationException>(() => new FunctionFixture(() => null!));
            Assert.That(exception!.Message, Does.Contain("createFactory").And.Contain("returned null"));
        }

        [Test]
        public void Fixture_PropagatesFactoryFailure()
        {
            var failure = new InvalidOperationException("factory failed");
            var exception = Assert.Throws<InvalidOperationException>(() => new FunctionFixture(() => throw failure));
            Assert.That(exception, Is.SameAs(failure));
        }

        [Test]
        public void Fixture_DisposalWithoutHostIsIdempotent()
        {
            using var fixture = new FunctionFixture();
            var test = fixture.Test;
            fixture.Dispose();
            fixture.Dispose();

            Assert.That(test.IsHostInstantiated, Is.False);
            Assert.Throws<ObjectDisposedException>(() => _ = fixture.Test);
            Assert.Throws<ObjectDisposedException>(() => _ = test.Services);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Fixture_DisposalReleasesScopesAndHostEvenWhenScopeThrows(bool throwOnDispose)
        {
            using var fixture = new FunctionFixture();
            var test = fixture.Test.ConfigureServices(sc =>
            {
                sc.AddKeyedSingleton("host", (_, _) => new DisposableResource());
                sc.AddScoped(_ => new DisposableResource(throwOnDispose));
            });
            var hostResource = test.Services.GetRequiredKeyedService<DisposableResource>("host");
            var trigger = test.Trigger<HttpFunction>();
            var scopedResource = trigger.Services.GetRequiredService<DisposableResource>();

            if (throwOnDispose)
            {
                var exception = Assert.Throws<InvalidOperationException>(() => fixture.Dispose());
                Assert.That(exception!.Message, Is.EqualTo("scope disposal failed"));
            }
            else
                fixture.Dispose();

            Assert.Throws<ObjectDisposedException>(() => _ = fixture.Test);
            Assert.Throws<ObjectDisposedException>(() => _ = test.Services);
            Assert.Throws<ObjectDisposedException>(() => trigger.Services.GetRequiredService<DisposableResource>());
            Assert.DoesNotThrow(() => fixture.Dispose());
            Assert.That(hostResource.DisposeCount, Is.EqualTo(1));
            Assert.That(scopedResource.DisposeCount, Is.EqualTo(1));
        }

        private sealed class FunctionFixture(Func<TestFrameworkImplementor>? createFactory = null)
            : WithFunctionTester<HttpFunction>(createFactory) { }

        private sealed class DisposableResource(bool throwOnDispose = false) : IDisposable
        {
            public int DisposeCount { get; private set; }

            public void Dispose()
            {
                DisposeCount++;
                if (throwOnDispose)
                    throw new InvalidOperationException("scope disposal failed");
            }
        }
    }
}
