using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using UnitTestEx.Abstractions;
using Xunit;

namespace UnitTestEx.Xunit.Test.Other
{
    public class SupportsServiceConfigurationTest
    {
        [Fact]
        public void Default_Is_True_And_ConfigureServices_Succeeds()
        {
            using var tester = new NoDiTester();
            Assert.True(tester.SupportsServiceConfiguration);

            // Should not throw as service configuration is supported by default.
            tester.ReplaceScoped<IDisposable>(_ => throw new NotImplementedException());
        }

        [Fact]
        public void Unsupported_ConfigureServices_Throws_NotSupportedException()
        {
            using var tester = new NoDiTester(supportsServiceConfiguration: false);
            Assert.False(tester.SupportsServiceConfiguration);

            var ex = Assert.Throws<NotSupportedException>(() => tester.ReplaceScoped<IDisposable>(_ => throw new NotImplementedException()));
            Assert.Contains(nameof(NoDiTester.SupportsServiceConfiguration), ex.Message);
        }

        /// <summary>
        /// A minimal <see cref="TesterBase{TSelf}"/> that does not run a real host; used purely to validate <see cref="TesterBase.SupportsServiceConfiguration"/> guarding behaviour.
        /// </summary>
        private sealed class NoDiTester(bool supportsServiceConfiguration = true) : TesterBase<NoDiTester>(TestFrameworkImplementor.Null), IDisposable
        {
            public override bool SupportsServiceConfiguration { get; } = supportsServiceConfiguration;

            public override IServiceProvider Services => throw new NotSupportedException($"{nameof(Services)} is not supported as {nameof(SupportsServiceConfiguration)} is false.");

            public override IConfiguration Configuration => throw new NotSupportedException($"{nameof(Configuration)} is not supported as {nameof(SupportsServiceConfiguration)} is false.");

            protected override void ResetHost() { }

            public void Dispose() { }
        }
    }
}
