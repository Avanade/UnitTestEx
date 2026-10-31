using Aspire.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnitTestEx.Aspire;
using Xunit;
using Xunit.Abstractions;

namespace UnitTestEx.Aspire.Xunit.Test
{
    public class AspireDisposalTest(ITestOutputHelper output) : UnitTestBase(output)
    {
        [Fact]
        public async Task DisposeAsync_WithoutBuilding_IsSafeAndRepeatable()
        {
            var tester = AspireTester.Create<Projects.UnitTestEx_Aspire_AppHost>();
            await tester.DisposeAsync();
            await tester.DisposeAsync();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task DisposeAsync_FailedConstructionDoesNotMaskOriginalFailure(bool canceled)
        {
            var tester = AspireTester.Create<Projects.UnitTestEx_Aspire_AppHost>();
            var task = canceled
                ? Task.FromCanceled<DistributedApplication>(new CancellationToken(true))
                : Task.FromException<DistributedApplication>(new InvalidOperationException("construction failed"));
            SetAppTask(tester, task);
            await tester.DisposeAsync();
            await tester.DisposeAsync();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task DisposeAsync_CompletedApplicationDisposesAndPreservesFailure(bool throws)
        {
            var tester = AspireTester.Create<Projects.UnitTestEx_Aspire_AppHost>();
            var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions { DisableDashboard = true, Args = [] });
            builder.Services.AddSingleton(_ => new DisposalProbe(throws));
            var app = builder.Build();
            var probe = app.Services.GetRequiredService<DisposalProbe>();
            // Seed only the construction result; starting an AppHost would introduce unrelated process/resource dependencies.
            SetAppTask(tester, Task.FromResult(app));
            if (throws)
            {
                var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => tester.DisposeAsync().AsTask());
                Assert.Equal("application disposal failed", exception.Message);
            }
            else
                await tester.DisposeAsync();

            Assert.Equal(1, probe.DisposeCount);
            await tester.DisposeAsync();
            Assert.Equal(1, probe.DisposeCount);
        }

        private static void SetAppTask(AspireTesterBase tester, Task<DistributedApplication> task)
            => typeof(AspireTesterBase).GetField("_appTask", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(tester, task);

        private sealed class DisposalProbe(bool throws) : IDisposable
        {
            public int DisposeCount { get; private set; }

            public void Dispose()
            {
                DisposeCount++;
                if (throws)
                    throw new InvalidOperationException("application disposal failed");
            }
        }
    }
}
