using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnitTestEx.Azure.Functions;
using UnitTestEx.DurableFunction;
using UnitTestEx.Expectations;

namespace UnitTestEx.NUnit.Test
{
    [TestFixture]
    public class DurableActivityTest
    {
        [Test]
        public void NormalEntryPoint_BuildsWithoutStartingHostedServices()
        {
            using var test = FunctionTester.Create<WorkflowActivities>()
                .ConfigureServices(sc => sc.AddHostedService<MustNotStartService>());

            Assert.That(test.Services.GetRequiredService<IWorkflowJournal>(), Is.TypeOf<WorkflowJournal>());
        }

        [TestCase(" order-1 ", "ORDER-1")]
        [TestCase("order-2", "ORDER-2")]
        public void StaticSyncScalar(string input, string expected)
        {
            using var test = FunctionTester.Create<WorkflowActivities>();
            test.Trigger<WorkflowActivities>()
                .Run(_ => ActivityTransforms.NormalizeReference(input))
                .AssertSuccess()
                .AssertValue(expected);
        }

        [TestCase(null)]
        [TestCase(" ")]
        public void StaticSyncScalar_InvalidInput(string input)
        {
            using var test = FunctionTester.Create<WorkflowActivities>();
            var result = test.Trigger<WorkflowActivities>().Run(_ => ActivityTransforms.NormalizeReference(input));
            if (input is null)
                result.AssertException<ArgumentNullException>();
            else
                result.AssertException<ArgumentException>();
        }

        [Test]
        public void StaticSyncCollection()
        {
            using var test = FunctionTester.Create<WorkflowActivities>();
            var items = new[] { new ItemResult("A", 2m), new ItemResult("B", 6m), new ItemResult("C", 10m) };
            test.Trigger<WorkflowActivities>()
                .Run(_ => ActivityTransforms.TotalItems(items))
                .AssertSuccess()
                .AssertValue(18m);
        }

        [Test]
        public void InstanceAsyncValue_WithContextAndLogging()
        {
            using var test = FunctionTester.Create<WorkflowActivities>();
            var trigger = test.Trigger<WorkflowActivities>();
            var context = new TestFunctionContext(trigger.Services);
            trigger.ExpectLogContains("Processing item A.")
                .Run(f => f.ProcessItem(new WorkItem("A", 3m), context))
                .AssertSuccess()
                .AssertValue(new ItemResult("A", 6m));
        }

        [Test]
        public void InstanceAsyncValue_InvalidInput()
        {
            using var test = FunctionTester.Create<WorkflowActivities>();
            var trigger = test.Trigger<WorkflowActivities>();
            var context = new TestFunctionContext(trigger.Services);
            trigger.Run(f => f.ProcessItem(new WorkItem("A", -1m), context))
                .AssertException<ArgumentOutOfRangeException>();
        }

        [Test]
        public void InstanceSyncVoid_UsesInjectedJournal()
        {
            var journal = new RecordingJournal();
            using var test = FunctionTester.Create<WorkflowActivities>().ReplaceSingleton<IWorkflowJournal>(journal);
            test.Trigger<WorkflowActivities>()
                .Run(f => f.RecordWorkflow("ORDER-1"))
                .AssertSuccess();

            Assert.That(journal.References, Is.EqualTo(new[] { "ORDER-1" }));
        }

        [Test]
        public async Task InstanceAsyncNoResult_AwaitsInjectedJournal()
        {
            var journal = new RecordingJournal();
            using var test = FunctionTester.Create<WorkflowActivities>().ReplaceSingleton<IWorkflowJournal>(journal);
            var result = new BatchResult("ORDER-1", 18m, [new ItemResult("A", 18m)]);
            (await test.Trigger<WorkflowActivities>().RunAsync(f => f.PublishWorkflow(result)))
                .AssertSuccess();

            Assert.That(journal.Published, Is.EqualTo(new[] { result }));
        }

        [Test]
        public void StaticAsyncFailure_PreservesActivityException()
        {
            using var test = FunctionTester.Create<WorkflowActivities>();
            test.Trigger<WorkflowActivities>()
                .Run(_ => ActivityTransforms.RejectWorkflow("ORDER-1"))
                .AssertException<WorkflowRejectedException>("Workflow ORDER-1 was declined.");
        }

        private sealed class RecordingJournal : IWorkflowJournal
        {
            public List<string> References { get; } = [];
            public List<BatchResult> Published { get; } = [];
            public void Record(string reference) => References.Add(reference);

            public async Task PublishAsync(BatchResult result)
            {
                await Task.Yield();
                Published.Add(result);
            }
        }

        private sealed class MustNotStartService : IHostedService
        {
            public Task StartAsync(CancellationToken cancellationToken) => throw new InvalidOperationException("The worker host must not start.");
            public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        }
    }
}
