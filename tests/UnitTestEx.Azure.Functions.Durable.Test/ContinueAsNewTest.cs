using Microsoft.DurableTask;
using Moq;
using NUnit.Framework;
using UnitTestEx.Azure.Functions.Durable.TestHost;
using UnitTestEx.DurableFunction;
using UnitTestEx.Expectations;

namespace UnitTestEx.Azure.Functions.Durable.Test;

/// <summary>Verifies SDK-host continuation with new input and buffered external events.</summary>
[TestFixture]
public sealed class ContinueAsNewTest
{
    /// <summary>Preserves the instance across generations, carrying serialized input and counting activities across restarts.</summary>
    [Test]
    public async Task ContinuesWithNewInputUntilFinalOutput()
    {
        using var functions = FunctionTester.Create<WorkflowActivities>();
        using var advances = ActivityMock.ByMatch(
        [
            ActivityResponse.Match<int, int>(input => input == 0, input => input + 1),
            ActivityResponse.Match<int, int>(input => input == 1, input => input + 1),
            ActivityResponse.Match<int, int>(input => input == 2, input => input + 1)
        ]);
        await using var run = await functions.DurableTask()
            .Orchestrator<int, int>("ContinueUntilFinished", async (context, input) =>
            {
                int next = await context.CallActivityAsync<int>("AdvanceGeneration", input);
                if (next < 3)
                {
                    context.ContinueAsNew(next, preserveUnprocessedEvents: false);
                    return -1;
                }

                return next;
            })
            .Activity<int, int>("AdvanceGeneration", advances.InvokeAsync, configure: options => options.Times(Times.Exactly(3)))
            .StartAsync(0);

        var result = await run.WaitForCompletionAsync();
        result.AssertCompleted().AssertOutput(3);
        Assert.That(result.Metadata.InstanceId, Is.EqualTo(run.InstanceId));
        Assert.That(result.Metadata.ReadInputAs<int>(), Is.EqualTo(2));
        advances.Verify();
        run.Verify();
    }

    /// <summary>Characterizes the pinned host's loss of buffered events, including when preservation is requested.</summary>
    /// <param name="preserveEvents">Whether preservation is requested despite the pinned host's limitation.</param>
    [TestCase(true)]
    [TestCase(false)]
    public async Task PinnedHostDiscardsBufferedEventsOnContinuation(bool preserveEvents)
    {
        using var functions = FunctionTester.Create<WorkflowActivities>();
        await using var run = await functions.DurableTask()
            .Orchestrator<ContinuationRequest, string>(nameof(ContinuationOrchestration.ContinueWithEvents), (context, _) => ContinuationOrchestration.ContinueWithEvents(context))
            .StartAsync(new ContinuationRequest(0, preserveEvents));

        run.ExpectSuccess();
        if (preserveEvents)
            run.ExpectLogContains("buffered-event preservation is unsupported");
        await run.WaitForCustomStatusAsync(metadata => metadata.ReadCustomStatusAs<string>() == "initial");
        await run.RaiseEventAsync("Payload", "buffered payload");
        await run.RaiseEventAsync("Barrier", "payload sent");
        await run.WaitForCustomStatusAsync(metadata => metadata.ReadCustomStatusAs<string>() == "buffered");
        await run.RaiseEventAsync("Continue", "restart");
        await run.WaitForCustomStatusAsync(metadata => metadata.ReadCustomStatusAs<string>() == "continued");
        await run.RaiseEventAsync("Payload", "fresh payload");

        var result = await run.WaitForCompletionAsync();
        result.AssertCompleted().AssertOutput("fresh payload");
        Assert.That(result.Metadata.ReadInputAs<ContinuationRequest>()?.Generation, Is.EqualTo(1));
    }
}
