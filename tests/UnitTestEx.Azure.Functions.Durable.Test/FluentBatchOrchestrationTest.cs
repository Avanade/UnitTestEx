using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Moq;
using NUnit.Framework;
using UnitTestEx.Azure.Functions;
using UnitTestEx.Azure.Functions.Durable.TestHost;
using UnitTestEx.DurableFunction;

namespace UnitTestEx.Azure.Functions.Durable.Test;

/// <summary>
/// Verifies fan-out/fan-in orchestration behavior through the fluent SDK-host API.
/// </summary>
[TestFixture]
public sealed class FluentBatchOrchestrationTest
{
    /// <summary>Verifies concurrent activity starts, ordered fan-in, and child finalization.</summary>
    [Test]
    public async Task FansOutActivitiesAndFansInBeforeFinalization()
    {
        using var functions = FunctionTester.Create<WorkflowActivities>();
        WorkItem[] items = [new("A", 10m), new("B", 20m), new("C", 30m)];
        var activityStarted = items.ToDictionary(
            item => item.Id,
            _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously),
            StringComparer.Ordinal);
        var releaseActivity = items.ToDictionary(
            item => item.Id,
            _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously),
            StringComparer.Ordinal);
        var activityCompleted = items.ToDictionary(
            item => item.Id,
            _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously),
            StringComparer.Ordinal);
        var allActivitiesStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var aggregatedItems = new TaskCompletionSource<ItemResult[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var finalizationInput = new TaskCompletionSource<(string InstanceId, BatchResult Input)>(TaskCreationOptions.RunContinuationsAsynchronously);
        var completionOrder = new ConcurrentQueue<string>();
        int startedCount = 0;
        var request = new BatchRequest(" batch-7 ", items);
        ItemResult[] expectedItems = [new("A", 20m), new("B", 40m), new("C", 60m)];
        var expectedResult = new BatchResult("BATCH-7", 120m, expectedItems);

        await using var run = await functions.DurableTask()
            .Orchestrator<BatchRequest, BatchResult>(
                nameof(BatchOrchestration.ProcessBatch),
                (context, _) => BatchOrchestration.ProcessBatch(context))
            .Activity<string, string>(
                nameof(ActivityTransforms.NormalizeReference),
                (_, reference) => ActivityTransforms.NormalizeReference(reference),
                configure: options => options.Times(Times.Once()))
            .Activity<WorkItem, ItemResult>(
                nameof(WorkflowActivities.ProcessItem),
                async (_, item) =>
                {
                    if (activityStarted[item.Id].TrySetResult()
                        && Interlocked.Increment(ref startedCount) == items.Length)
                        allActivitiesStarted.TrySetResult();

                    await releaseActivity[item.Id].Task.ConfigureAwait(false);
                    completionOrder.Enqueue(item.Id);
                    activityCompleted[item.Id].TrySetResult();
                    return new ItemResult(item.Id, item.Amount * 2m);
                },
                configure: options => options.Times(Times.Exactly(3)))
            .Activity<ItemResult[], decimal>(
                nameof(ActivityTransforms.TotalItems),
                (_, results) =>
                {
                    aggregatedItems.TrySetResult([.. results]);
                    return ActivityTransforms.TotalItems(results);
                },
                configure: options => options.Times(Times.Once()))
            .SubOrchestrator<BatchResult, BatchResult>(
                nameof(BatchOrchestration.FinalizeBatch),
                async (context, input) =>
                {
                    finalizationInput.TrySetResult((context.InstanceId, input));
                    await context.WaitForExternalEvent<string>("ReleaseFinalization");
                    return input;
                })
            .StartAsync(request);

        try
        {
            Task<DurableTaskTestAssertor<BatchResult>> completion = run.WaitForCompletionAsync(TimeSpan.FromSeconds(10));
            await allActivitiesStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

            releaseActivity["C"].TrySetResult();
            await activityCompleted["C"].Task.WaitAsync(TimeSpan.FromSeconds(5));
            releaseActivity["B"].TrySetResult();
            await activityCompleted["B"].Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.ThrowsAsync<TimeoutException>(async () =>
                await run.WaitForCustomStatusAsync(IsFinalizing, TimeSpan.FromMilliseconds(100)));

            releaseActivity["A"].TrySetResult();
            await activityCompleted["A"].Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.That(completionOrder, Is.EqualTo(new[] { "C", "B", "A" }));
            Assert.That(await aggregatedItems.Task.WaitAsync(TimeSpan.FromSeconds(5)), Is.EqualTo(expectedItems));

            var finalizingMetadata = await run.WaitForCustomStatusAsync(IsFinalizing);
            Assert.That(finalizingMetadata.RuntimeStatus, Is.EqualTo(OrchestrationRuntimeStatus.Running));
            var finalization = await finalizationInput.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var childInput = finalization.Input;
            Assert.That(childInput.Reference, Is.EqualTo(expectedResult.Reference));
            Assert.That(childInput.Total, Is.EqualTo(expectedResult.Total));
            Assert.That(childInput.Items, Is.EqualTo(expectedItems));
            Assert.That(completion.IsCompleted, Is.False);

            await run.Host.Client.RaiseEventAsync(finalization.InstanceId, "ReleaseFinalization", "release");
            (await completion).AssertCompleted().AssertOutput(expectedResult);
            run.Verify();
        }
        finally
        {
            foreach (TaskCompletionSource release in releaseActivity.Values)
                release.TrySetResult();
        }
    }

    /// <summary>Verifies input-matched activity responses with both delayed and test-released completion.</summary>
    [Test]
    public async Task FansOutAndFansInWithScriptedActivityResponses()
    {
        using var processItems = ScriptedActivityHandler.ByMatch(
        [
            ActivityResponse.Match<WorkItem, ItemResult>(item => item.Id == "A", item => new ItemResult(item.Id, item.Amount * 2m)).Pending(),
            ActivityResponse.Match<WorkItem, ItemResult>(item => item.Id == "B", item => new ItemResult(item.Id, item.Amount * 2m)).Pending(),
            ActivityResponse.Match<WorkItem, ItemResult>(item => item.Id == "C", item => new ItemResult(item.Id, item.Amount * 2m)).After(TimeSpan.FromMilliseconds(100))
        ]);

        using var workflowFunc = FunctionTester.Create<WorkflowActivities>();
        var request = new BatchRequest(" batch-7 ", [new("A", 10m), new("B", 20m), new("C", 30m)]);

        await using var test = await workflowFunc.DurableTask()
            .Orchestrator<BatchRequest, BatchResult>(nameof(BatchOrchestration.ProcessBatch), (context, _) => BatchOrchestration.ProcessBatch(context))
            .Activity<string, string>(nameof(ActivityTransforms.NormalizeReference), (_, reference) => ActivityTransforms.NormalizeReference(reference), configure: options => options.Times(Times.Once()))
            .Activity<WorkItem, ItemResult>(nameof(WorkflowActivities.ProcessItem), processItems.InvokeAsync, configure: options => options.Times(Times.Exactly(3)))
            .Activity<ItemResult[], decimal>(nameof(ActivityTransforms.TotalItems), (_, results) => ActivityTransforms.TotalItems(results), configure: options => options.Times(Times.Once()))
            .SubOrchestrator<BatchResult, BatchResult>(nameof(BatchOrchestration.FinalizeBatch), (_, input) => Task.FromResult(input))
            .StartAsync(request);

        await processItems.WaitForAllInvocationsAsync();
        await processItems[2].WaitForCompletionAsync(TimeSpan.FromSeconds(5));
        await processItems[1].ReleaseAsync(TimeSpan.FromSeconds(5));
        await processItems[0].ReleaseAsync(TimeSpan.FromSeconds(5));

        ItemResult[] expectedItems = [new("A", 20m), new("B", 40m), new("C", 60m)];
        var expectedResult = new BatchResult("BATCH-7", 120m, expectedItems);

        await test.WaitForCompletionAsync(TimeSpan.FromSeconds(10), r => r.AssertCompleted().AssertOutput(expectedResult));

        Assert.That(processItems.CompletionOrder.Select(item => item.Id), Is.EqualTo(new[] { "C", "B", "A" }));
        test.Verify();
    }

    /// <summary>Verifies explicit invocation-order matching for repeated identical activity inputs.</summary>
    [Test]
    public async Task ScriptedActivityHandlerCanMatchResponsesByInvocationOrder()
    {
        using var functions = FunctionTester.Create<WorkflowActivities>();
        using var processItems = ScriptedActivityHandler.InOrder(
        [
            ActivityResponse.Next<string, string>(_ => "first").Pending(),
            ActivityResponse.Next<string, string>(_ => "second").Pending()
        ]);

        await using var run = await functions.DurableTask()
            .Orchestrator<string>(
                "SequentialActivityCalls",
                async context =>
                {
                    string first = await context.CallActivityAsync<string>("RepeatedActivity", "same");
                    string second = await context.CallActivityAsync<string>("RepeatedActivity", "same");
                    return $"{first},{second}";
                })
            .Activity<string, string>(
                "RepeatedActivity",
                processItems.InvokeAsync,
                configure: options => options.Times(Times.Exactly(2)))
            .StartAsync();

        await processItems[0].ReleaseAsync(TimeSpan.FromSeconds(5));
        await processItems[1].ReleaseAsync(TimeSpan.FromSeconds(5));
        (await run.WaitForCompletionAsync())
            .AssertCompleted()
            .AssertOutput("first,second");
        processItems.Verify();
        run.Verify();
    }

    /// <summary>Verifies that an unused response is reported even when the activity invocation count is correct.</summary>
    [Test]
    public async Task ScriptedActivityVerifyReportsUnusedResponse()
    {
        using var functions = FunctionTester.Create<WorkflowActivities>();
        using var responses = ScriptedActivityHandler.ByMatch(
            ActivityResponse.Match<string, string>(input => input == "called", _ => "done"),
            ActivityResponse.Match<string, string>(input => input == "unused", _ => "unused"));

        await using var run = await functions.DurableTask()
            .Orchestrator<string>("SingleActivity", context => context.CallActivityAsync<string>("Activity", "called"))
            .Activity<string, string>("Activity", responses.InvokeAsync, configure: options => options.Times(Times.Once()))
            .StartAsync();

        (await run.WaitForCompletionAsync()).AssertCompleted().AssertOutput("done");
        run.Verify();
        Assert.ThrowsAsync<TimeoutException>(async () => await responses.WaitForAllInvocationsAsync(TimeSpan.FromMilliseconds(50)));
        Assert.Throws<ArgumentOutOfRangeException>(() => responses.WaitForAllInvocationsAsync(TimeSpan.Zero));
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(responses.Verify)!;
        Assert.That(error.Message, Does.Contain("2"));
    }

    /// <summary>Verifies that unmatched and ambiguous activity calls fail through the Durable host.</summary>
    /// <param name="ambiguous">Whether multiple responses match rather than none.</param>
    /// <param name="expectedMessage">The expected activity failure message.</param>
    [TestCase(false, "No scripted activity response matched input 'called'.")]
    [TestCase(true, "Multiple scripted activity responses matched input 'called'.")]
    public async Task ScriptedActivityRejectsInvalidMatches(bool ambiguous, string expectedMessage)
    {
        using var functions = FunctionTester.Create<WorkflowActivities>();
        using var responses = ScriptedActivityHandler.ByMatch(
            ActivityResponse.Match<string, string>(input => ambiguous && input == "called", _ => "first"),
            ActivityResponse.Match<string, string>(input => ambiguous && input == "called", _ => "second"));

        await using var run = await functions.DurableTask()
            .Orchestrator<string>("SingleActivity", context => context.CallActivityAsync<string>("Activity", "called"))
            .Activity<string, string>("Activity", responses.InvokeAsync)
            .StartAsync();

        var result = await run.WaitForCompletionAsync();
        result.AssertFailed().AssertFailureCausedBy<InvalidOperationException>();
        Assert.That(result.FailureDetails?.InnerFailure?.ErrorMessage, Does.Contain(expectedMessage));
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(responses.Verify)!;
        Assert.That(error.Message, Does.Contain("1").And.Contain("2"));
    }

    /// <summary>Verifies each response is reserved once under concurrent activity calls.</summary>
    /// <param name="byMatch">Whether responses are selected by input instead of arrival order.</param>
    [TestCase(true)]
    [TestCase(false)]
    public async Task ScriptedResponsesAreReservedOnceUnderConcurrency(bool byMatch)
    {
        var plans = Enumerable.Range(0, 20).Select(index => byMatch
            ? ActivityResponse.Match<int, int>(input => input == index, _ => index).Pending()
            : ActivityResponse.Next<int, int>(_ => index).Pending()).ToArray();
        using var handler = byMatch ? ScriptedActivityHandler.ByMatch(plans) : ScriptedActivityHandler.InOrder(plans);
        Task<int>[] calls = Enumerable.Range(0, plans.Length)
            .Select(input => Task.Run(() => handler.InvokeAsync(null!, input))).ToArray();

        await handler.WaitForAllInvocationsAsync();
        handler.Verify();
        foreach (var plan in plans)
            await plan.ReleaseAsync(TimeSpan.FromSeconds(5));

        Assert.That(await Task.WhenAll(calls), Is.EquivalentTo(Enumerable.Range(0, plans.Length)));
        if (byMatch)
            Assert.That(calls.Select(call => call.Result), Is.EqualTo(Enumerable.Range(0, plans.Length)));
    }

    /// <summary>Verifies failed attempts consume responses and propagate original errors to response waiters.</summary>
    [Test]
    public async Task FailedAttemptsConsumeResponsesAndFaultWaiters()
    {
        var error = new InvalidOperationException("Activity failed");
        var first = ActivityResponse.Next<string, string>(_ => throw error).Pending();
        var second = ActivityResponse.Next<string, string>(_ => "recovered");
        using var handler = ScriptedActivityHandler.InOrder(first, second);

        Task<string> attempt = handler.InvokeAsync(null!, "same");
        Assert.That(Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await first.ReleaseAsync(TimeSpan.FromSeconds(5))), Is.SameAs(error));
        Assert.That(Assert.ThrowsAsync<InvalidOperationException>(async () => await attempt), Is.SameAs(error));
        Assert.That(Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await first.WaitForCompletionAsync(TimeSpan.FromSeconds(5))), Is.SameAs(error));

        Assert.That(await handler.InvokeAsync(null!, "same"), Is.EqualTo("recovered"));
        handler.Verify();
        InvalidOperationException exhausted = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await handler.InvokeAsync(null!, "same"))!;
        Assert.That(exhausted.Message, Does.Contain("No unused in-order"));
    }

    /// <summary>Verifies the SDK's activity retry consumes the next ordered response.</summary>
    [Test]
    public async Task DurableRetryConsumesNextScriptedResponse()
    {
        using var functions = FunctionTester.Create<WorkflowActivities>();
        var failed = ActivityResponse.Next<string, string>(_ => throw new InvalidOperationException("First attempt failed"));
        using var handler = ScriptedActivityHandler.InOrder(failed, ActivityResponse.Next<string, string>(_ => "recovered"));
        await using var run = await functions.DurableTask()
            .Orchestrator<string>("RetryScriptedActivity", context => context.CallActivityAsync<string>(
                "Activity", "same", TaskOptions.FromRetryPolicy(new RetryPolicy(2, TimeSpan.FromMilliseconds(10)))))
            .Activity<string, string>("Activity", handler.InvokeAsync, configure: options => options.Times(Times.Exactly(2)))
            .StartAsync();

        await run.WaitForCompletionAsync(assert: result => result.AssertCompleted().AssertOutput("recovered"));
        Assert.ThrowsAsync<InvalidOperationException>(async () => await failed.WaitForCompletionAsync(TimeSpan.FromSeconds(5)));
        handler.Verify();
        run.Verify();
    }

    /// <summary>Verifies disposal cancels pending and delayed calls without running their output factories.</summary>
    [Test]
    public async Task DisposingScriptCancelsBlockedResponses()
    {
        int outputsCreated = 0;
        var pending = ActivityResponse.Next<string, string>(_ => { Interlocked.Increment(ref outputsCreated); return "pending"; }).Pending();
        var delayed = ActivityResponse.Next<string, string>(_ => { Interlocked.Increment(ref outputsCreated); return "delayed"; }).After(TimeSpan.FromHours(24));
        using var handler = ScriptedActivityHandler.InOrder(pending, delayed);
        Task<string> pendingCall = handler.InvokeAsync(null!, "same");
        Task<string> delayedCall = handler.InvokeAsync(null!, "same");
        await handler.WaitForAllInvocationsAsync();

        handler.Dispose();
        Assert.CatchAsync<OperationCanceledException>(async () => await pendingCall.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.CatchAsync<OperationCanceledException>(async () => await delayedCall.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.CatchAsync<OperationCanceledException>(async () => await pending.WaitForCompletionAsync(TimeSpan.FromSeconds(5)));
        Assert.That(outputsCreated, Is.Zero);
        Assert.ThrowsAsync<ObjectDisposedException>(async () => await handler.InvokeAsync(null!, "same"));
    }

    /// <summary>Verifies response ownership, matching modes, and completion configuration cannot be mixed or reused.</summary>
    [Test]
    public void ScriptedResponseConfigurationIsFrozenAndExclusive()
    {
        var plan = ActivityResponse.Next<string, string>(_ => "output");
        using var handler = ScriptedActivityHandler.InOrder(plan);
        Assert.Throws<InvalidOperationException>(() => plan.Pending());
        Assert.Throws<InvalidOperationException>(() => plan.After(TimeSpan.Zero));
        Assert.Throws<InvalidOperationException>(() => ScriptedActivityHandler.InOrder(plan));

        var duplicate = ActivityResponse.Next<string, string>(_ => "output");
        Assert.Throws<ArgumentException>(() => ScriptedActivityHandler.InOrder(duplicate, duplicate));
        var matched = ActivityResponse.Match<string, string>(_ => true, _ => "output");
        Assert.Throws<ArgumentException>(() => ScriptedActivityHandler.InOrder(matched));
        Assert.Throws<ArgumentException>(() => ScriptedActivityHandler.ByMatch(duplicate));
        var pending = ActivityResponse.Next<string, string>(_ => "output").Pending();
        Assert.Throws<InvalidOperationException>(() => pending.After(TimeSpan.Zero));
    }

    /// <summary>Verifies that explicit run verification fails when a registered activity is not invoked.</summary>
    [Test]
    public async Task VerifyFailsWhenRegisteredActivityWasNotCalled()
    {
        using var functions = FunctionTester.Create<WorkflowActivities>();
        await using var run = await functions.DurableTask()
            .Orchestrator<string>("NoActivityOrchestration", _ => Task.FromResult("completed"))
            .Activity<string, string>("UnusedActivity", (_, input) => input)
            .StartAsync();

        (await run.WaitForCompletionAsync()).AssertCompleted();
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(run.Verify)!;
        Assert.That(error.Message, Does.Contain("UnusedActivity"));
    }

    /// <summary>Returns whether the orchestration has entered the finalizing stage.</summary>
    /// <param name="metadata">The orchestration metadata.</param>
    /// <returns>True when the custom status identifies the finalizing stage.</returns>
    private static bool IsFinalizing(OrchestrationMetadata metadata)
    {
        if (metadata.SerializedCustomStatus is null)
            return false;

        JsonElement status = metadata.ReadCustomStatusAs<JsonElement>();
        return status.ValueKind == JsonValueKind.Object
            && status.TryGetProperty("Stage", out JsonElement stage)
            && stage.GetString() == "Finalizing";
    }
}
