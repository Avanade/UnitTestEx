using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Moq;
using NUnit.Framework;
using UnitTestEx.Azure.Functions;
using UnitTestEx.Azure.Functions.Durable.TestHost;
using UnitTestEx.DurableFunction;
using UnitTestEx.Expectations;

namespace UnitTestEx.Azure.Functions.Durable.Test;

/// <summary>Exercises the same Service Bus starter with a mock and an SDK-host client.</summary>
[TestFixture]
public sealed class DurableStarterTest
{
    /// <summary>Verifies the starter's scheduling decision without executing an orchestration.</summary>
    [Test]
    public async Task StarterSchedulesExpectedOrchestrationWithMockClient()
    {
        var client = new Mock<DurableTaskClient>(MockBehavior.Strict, "mock");
        client.Setup(x => x.ScheduleNewOrchestrationInstanceAsync(
            It.Is<TaskName>(name => name.Name == nameof(BatchOrchestration.ProcessBatch)),
            It.Is<object>(input => input is BatchRequest && ((BatchRequest)input).Reference == "batch-7"),
            It.Is<StartOrchestrationOptions>(options => options.InstanceId == "batch-7"), It.IsAny<CancellationToken>()))
            .ReturnsAsync("batch-7");

        using var functions = FunctionTester.Create<WorkflowActivities>();
        var message = functions.CreateServiceBusMessageFromValue(new BatchRequest("batch-7", [new("A", 10m)]));
        var actions = functions.CreateWorkerServiceBusMessageActions();

        (await functions.ServiceBusTrigger<BatchStarter>().RunAsync(starter => starter.StartBatch(message, actions, client.Object))).AssertSuccess();

        client.Verify(x => x.ScheduleNewOrchestrationInstanceAsync(
            It.Is<TaskName>(name => name.Name == nameof(BatchOrchestration.ProcessBatch)),
            It.Is<object>(input => input is BatchRequest && ((BatchRequest)input).Reference == "batch-7"),
            It.Is<StartOrchestrationOptions>(options => options.InstanceId == "batch-7"), It.IsAny<CancellationToken>()), Times.Once());

        actions.AssertComplete();
    }

    /// <summary>Runs a starter and its discovered root, activities and child using the SDK-host client.</summary>
    [Test]
    public async Task StarterSchedulesDiscoveredBatchWithSdkHostClient()
    {
        var journal = new Mock<IWorkflowJournal>(MockBehavior.Strict);
        journal.Setup(x => x.Record("BATCH-7"));
        journal.Setup(x => x.PublishAsync(It.IsAny<BatchResult>())).Returns(Task.CompletedTask);

        using var functions = FunctionTester.Create<WorkflowActivities>();
        functions.ReplaceSingleton<IWorkflowJournal>(journal.Object);

        await using var session = await functions.DurableTask().WithSelfDiscovery().StartHostAsync();
        var message = functions.CreateServiceBusMessageFromValue(new BatchRequest("batch-7", [new("A", 10m), new("B", 20m)]));
        var actions = functions.CreateWorkerServiceBusMessageActions();

        (await functions.ServiceBusTrigger<BatchStarter>().RunAsync(starter => starter.StartBatch(message, actions, session.Client))).AssertSuccess();
        actions.AssertComplete();

        await using var run = session.Observe<BatchResult>("batch-7");
        run.ExpectSuccess();

        await run.WaitForCompletionAsync(TimeSpan.FromSeconds(10), result => result.AssertOutput(new BatchResult("BATCH-7", 60m, [new("A", 20m), new("B", 40m)])));

        journal.Verify(x => x.Record("BATCH-7"), Times.Once());
        journal.Verify(x => x.PublishAsync(It.Is<BatchResult>(result => result.Reference == "BATCH-7" && result.Total == 60m)), Times.Once());

        await run.DisposeAsync();
        Assert.That(await session.Client.GetInstanceAsync("batch-7"), Is.Not.Null);
        Assert.Throws<ObjectDisposedException>(() => _ = run.Host);
    }

    /// <summary>Applies explicit SDK registry overrides before discovered handlers in a starter session.</summary>
    [Test]
    public async Task StarterSessionRespectsExplicitActivityOverride()
    {
        using var functions = FunctionTester.Create<WorkflowActivities>();
        string activityName = nameof(ActivityTransforms.NormalizeReference);
        await using var session = await functions.DurableTask()
            .WithSelfDiscovery()
            .ConfigureRegistry(registry => registry.AddActivityFunc<string, string>(activityName, (_, _) => "OVERRIDDEN"))
            .StartHostAsync();

        var message = functions.CreateServiceBusMessageFromValue(new BatchRequest("override-7", [new("A", 5m)]));
        var actions = functions.CreateWorkerServiceBusMessageActions();
        (await functions.ServiceBusTrigger<BatchStarter>().RunAsync(starter => starter.StartBatch(message, actions, session.Client))).AssertSuccess();

        await using var run = session.Observe<BatchResult>("override-7");
        await run.WaitForCompletionAsync(TimeSpan.FromSeconds(10), result => result.AssertOutput(new BatchResult("OVERRIDDEN", 10m, [new("A", 10m)])));
    }

    /// <summary>Allows a registry override to replace the discovered root selected by the starter.</summary>
    [Test]
    public async Task StarterSessionRespectsExplicitRootOverride()
    {
        using var functions = FunctionTester.Create<WorkflowActivities>();
        await using var session = await functions.DurableTask()
            .WithSelfDiscovery()
            .ConfigureRegistry(registry => registry.AddOrchestratorFunc<BatchRequest, BatchResult>(nameof(BatchOrchestration.ProcessBatch), (_, input) => Task.FromResult(new BatchResult(input.Reference, 0m, []))))
            .StartHostAsync();

        var message = functions.CreateServiceBusMessageFromValue(new BatchRequest("root-7", [new("A", 5m)]));
        var actions = functions.CreateWorkerServiceBusMessageActions();
        (await functions.ServiceBusTrigger<BatchStarter>().RunAsync(starter => starter.StartBatch(message, actions, session.Client))).AssertSuccess();

        await using var run = session.Observe<BatchResult>("root-7");
        await run.WaitForCompletionAsync(TimeSpan.FromSeconds(10), result => result.AssertOutput(new BatchResult("root-7", 0m, [])));
    }

    /// <summary>Rejects invalid IDs and invalidates borrowed runs when the owning session is disposed.</summary>
    [Test]
    public async Task SessionOwnsHostAndValidatesObservedInstance()
    {
        using var functions = FunctionTester.Create<WorkflowActivities>();
        var session = await functions.DurableTask().WithSelfDiscovery().StartHostAsync();
        Assert.Throws<ArgumentException>(() => session.Observe<BatchResult>(" "));

        await using var run = session.Observe<BatchResult>("not-scheduled");
        await session.DisposeAsync();
        await session.DisposeAsync();
        Assert.Throws<ObjectDisposedException>(() => _ = session.Client);
        Assert.Throws<ObjectDisposedException>(() => _ = run.Host);
        Assert.Throws<ObjectDisposedException>(() => session.Observe<BatchResult>("other"));
    }
}
