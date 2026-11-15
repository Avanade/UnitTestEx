using Xunit;
using UnitTestEx.Api;
using UnitTestEx.Abstractions;

namespace UnitTestEx.Xunit.Test
{
    /// <summary>Verifies xUnit can construct the framework-neutral shared API fixture.</summary>
    public sealed class ApiTestFixtureConstructionTest(ApiTestFixture<Startup> fixture) : IClassFixture<ApiTestFixture<Startup>>
    {
        /// <summary>Verifies the fixture supplied by xUnit is usable.</summary>
        [Fact]
        public void SharedFixture_IsConstructedByXunit() => Assert.NotNull(fixture.Test);

        /// <summary>Verifies derived fixtures can supply a custom implementor factory.</summary>
        [Fact]
        public void DerivedFixture_AcceptsFactory()
        {
            var implementor = new NullTestImplementor();
            using var customFixture = new CustomFixture(implementor);
            Assert.Same(implementor, customFixture.Test.Implementor);
        }

        /// <summary>Supplies a custom implementor for a shared fixture.</summary>
        private sealed class CustomFixture(TestFrameworkImplementor implementor) : ApiTestFixture<Startup>(() => implementor) { }
    }
}
