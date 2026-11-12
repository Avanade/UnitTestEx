using NUnit.Framework;
using UnitTestEx.DurableFunction;
using UnitTestEx.Azure.Functions.Durable;

namespace UnitTestEx.Azure.Functions.Durable.Test;

/// <summary>
/// Verifies virtual-time timer, event, and cancellation behavior for the approval orchestration.
/// </summary>
[TestFixture]
public sealed class ApprovalVirtualTimeTest
{
    /// <summary>Verifies unsupported orchestration operations fail explicitly.</summary>
    [Test]
    public void OperationsOutsideTimerEventScopeFailExplicitly()
    {
        using var context = new VirtualTimeOrchestrationContext(nameof(ApprovalOrchestration.AwaitApproval), new ApprovalRequest(TimeSpan.FromHours(24)));

        Assert.Throws<NotSupportedException>(() => { _ = context.CallActivityAsync<string>("activity"); });
        Assert.Throws<NotSupportedException>(() => { _ = context.CallSubOrchestratorAsync<string>("child"); });
        Assert.Throws<NotSupportedException>(() => context.SetCustomStatus("unsupported"));
        Assert.Throws<NotSupportedException>(() => context.NewGuid());
    }

    /// <summary>Verifies that a pre-canceled timer remains canceled after advancing virtual time.</summary>
    [Test]
    public async Task PreCanceledTimerStaysCanceledAfterClockAdvance()
    {
        using var context = new VirtualTimeOrchestrationContext(nameof(ApprovalOrchestration.AwaitApproval), new ApprovalRequest(TimeSpan.FromHours(24)));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Task timer = context.CreateTimer(context.CurrentUtcDateTime.AddHours(24), cancellation.Token);
        Assert.ThrowsAsync<TaskCanceledException>(async () => await timer);

        context.AdvanceBy(TimeSpan.FromHours(24));

        Assert.That(context.Timers.Single().Outcome, Is.EqualTo(VirtualTimeOrchestrationContext.TimerOutcome.Canceled));
    }

    /// <summary>Verifies that a pre-canceled event wait rejects a later event.</summary>
    [Test]
    public async Task PreCanceledEventWaitRejectsLaterEvent()
    {
        using var context = new VirtualTimeOrchestrationContext(nameof(ApprovalOrchestration.AwaitApproval), new ApprovalRequest(TimeSpan.FromHours(24)));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Task<ApprovalDecision> wait = context.WaitForExternalEvent<ApprovalDecision>("Approved", cancellation.Token);
        Assert.ThrowsAsync<TaskCanceledException>(async () => await wait);
        Assert.That(context.EventWaits.Single().IsCanceled, Is.True);
        Assert.Throws<InvalidOperationException>(() => context.RaiseEvent("Approved", new ApprovalDecision(true)));
    }

    /// <summary>Verifies a same-named event completes the most recently registered wait and another wait can be canceled.</summary>
    [Test]
    public async Task SameNamedEventCompletesLatestWaitAndOtherWaitCanBeCanceled()
    {
        using var context = new VirtualTimeOrchestrationContext(nameof(ApprovalOrchestration.AwaitApproval), new ApprovalRequest(TimeSpan.FromHours(24)));
        using var firstCancellation = new CancellationTokenSource();
        using var secondCancellation = new CancellationTokenSource();
        Task<ApprovalDecision> firstWait = context.WaitForExternalEvent<ApprovalDecision>("Approved", firstCancellation.Token);
        Task<ApprovalDecision> secondWait = context.WaitForExternalEvent<ApprovalDecision>("Approved", secondCancellation.Token);

        context.RaiseEvent("Approved", new ApprovalDecision(true));

        Assert.That((await secondWait.WaitAsync(TimeSpan.FromSeconds(5))).Approved, Is.True);
        Assert.That(firstWait.IsCompleted, Is.False);

        firstCancellation.Cancel();
        Assert.ThrowsAsync<TaskCanceledException>(async () => await firstWait);
        Assert.That(context.EventWaits.Select(wait => wait.IsCanceled), Is.EqualTo(new[] { true, false }));
    }

    /// <summary>Verifies canceling a pending timer prevents a later clock advance from firing it.</summary>
    [Test]
    public async Task PendingTimerCancellationStaysCanceledAfterClockAdvance()
    {
        using var context = new VirtualTimeOrchestrationContext(nameof(ApprovalOrchestration.AwaitApproval), new ApprovalRequest(TimeSpan.FromHours(24)));
        using var cancellation = new CancellationTokenSource();
        Task timer = context.CreateTimer(context.CurrentUtcDateTime.AddHours(24), cancellation.Token);

        cancellation.Cancel();
        Assert.ThrowsAsync<TaskCanceledException>(async () => await timer);

        context.AdvanceBy(TimeSpan.FromHours(24));

        Assert.That(context.Timers.Single().Outcome, Is.EqualTo(VirtualTimeOrchestrationContext.TimerOutcome.Canceled));
    }

    /// <summary>Verifies canceling a pending event wait prevents a later event from completing it.</summary>
    [Test]
    public async Task PendingEventCancellationRejectsLaterEvent()
    {
        using var context = new VirtualTimeOrchestrationContext(nameof(ApprovalOrchestration.AwaitApproval), new ApprovalRequest(TimeSpan.FromHours(24)));
        using var cancellation = new CancellationTokenSource();
        Task<ApprovalDecision> wait = context.WaitForExternalEvent<ApprovalDecision>("Approved", cancellation.Token);

        cancellation.Cancel();
        Assert.ThrowsAsync<TaskCanceledException>(async () => await wait);

        Assert.That(context.EventWaits.Single().IsCanceled, Is.True);
        Assert.Throws<InvalidOperationException>(() => context.RaiseEvent("Approved", new ApprovalDecision(true)));
    }

    /// <summary>Verifies a timer at or before the current virtual time completes immediately.</summary>
    [Test]
    public void DueTimerCompletesImmediately()
    {
        using var context = new VirtualTimeOrchestrationContext(nameof(ApprovalOrchestration.AwaitApproval), new ApprovalRequest(TimeSpan.FromHours(24)));

        Task timer = context.CreateTimer(context.CurrentUtcDateTime.AddTicks(-1), CancellationToken.None);

        Assert.That(timer.IsCompletedSuccessfully, Is.True);
        Assert.That(context.Timers.Single().Outcome, Is.EqualTo(VirtualTimeOrchestrationContext.TimerOutcome.Fired));
    }

    /// <summary>Verifies negative clock advances are rejected without changing virtual time.</summary>
    [Test]
    public void NegativeClockAdvanceIsRejectedWithoutChangingTime()
    {
        using var context = new VirtualTimeOrchestrationContext(nameof(ApprovalOrchestration.AwaitApproval), new ApprovalRequest(TimeSpan.FromHours(24)));
        DateTime before = context.CurrentUtcDateTime;

        Assert.Throws<ArgumentOutOfRangeException>(() => context.AdvanceBy(TimeSpan.FromTicks(-1)));

        Assert.That(context.CurrentUtcDateTime, Is.EqualTo(before));
    }

    /// <summary>Verifies timer deadlines must use UTC and are not registered when invalid.</summary>
    [Test]
    public void NonUtcTimerDeadlineIsRejectedBeforeRegistration()
    {
        using var context = new VirtualTimeOrchestrationContext(nameof(ApprovalOrchestration.AwaitApproval), new ApprovalRequest(TimeSpan.FromHours(24)));
        DateTime deadline = DateTime.SpecifyKind(context.CurrentUtcDateTime.AddHours(1), DateTimeKind.Unspecified);

        Assert.Throws<ArgumentException>(() => context.CreateTimer(deadline, CancellationToken.None));

        Assert.That(context.Timers, Is.Empty);
    }

    /// <summary>Verifies timer deadlines determine which timers fire as the virtual clock advances.</summary>
    [Test]
    public void TimersFireByDeadlineIndependentOfCreationOrder()
    {
        using var context = new VirtualTimeOrchestrationContext(nameof(ApprovalOrchestration.AwaitApproval), new ApprovalRequest(TimeSpan.FromHours(24)));
        Task later = context.CreateTimer(context.CurrentUtcDateTime.AddHours(2), CancellationToken.None);
        Task earlier = context.CreateTimer(context.CurrentUtcDateTime.AddHours(1), CancellationToken.None);

        context.AdvanceBy(TimeSpan.FromHours(1));

        Assert.That(earlier.IsCompletedSuccessfully, Is.True);
        Assert.That(later.IsCompleted, Is.False);

        context.AdvanceBy(TimeSpan.FromHours(1));

        Assert.That(later.IsCompletedSuccessfully, Is.True);
    }

    /// <summary>Verifies every timer due during one clock advance completes, including equal deadlines.</summary>
    [Test]
    public void OneClockAdvanceCompletesAllDueTimers()
    {
        using var context = new VirtualTimeOrchestrationContext(nameof(ApprovalOrchestration.AwaitApproval), new ApprovalRequest(TimeSpan.FromHours(24)));
        Task[] timers =
        [
            context.CreateTimer(context.CurrentUtcDateTime.AddHours(1), CancellationToken.None),
            context.CreateTimer(context.CurrentUtcDateTime.AddHours(2), CancellationToken.None),
            context.CreateTimer(context.CurrentUtcDateTime.AddHours(2), CancellationToken.None),
            context.CreateTimer(context.CurrentUtcDateTime.AddHours(4), CancellationToken.None),
        ];

        context.AdvanceBy(TimeSpan.FromHours(2));

        Assert.That(timers.Take(3).All(timer => timer.IsCompletedSuccessfully), Is.True);
        Assert.That(timers[3].IsCompleted, Is.False);
        Assert.That(context.Timers.Select(timer => timer.Outcome), Is.EqualTo(new[]
        {
            VirtualTimeOrchestrationContext.TimerOutcome.Fired,
            VirtualTimeOrchestrationContext.TimerOutcome.Fired,
            VirtualTimeOrchestrationContext.TimerOutcome.Fired,
            VirtualTimeOrchestrationContext.TimerOutcome.Pending,
        }));

        context.AdvanceBy(TimeSpan.FromHours(2));

        Assert.That(timers.All(timer => timer.IsCompletedSuccessfully), Is.True);
        Assert.That(context.Timers.All(timer => timer.Outcome == VirtualTimeOrchestrationContext.TimerOutcome.Fired), Is.True);
    }

    /// <summary>Verifies an event delivered after the clock fires the due timer cannot change the timeout result.</summary>
    [Test]
    public async Task EventAfterAdvanceToDeadlineCannotChangeTimeoutResult()
    {
        using var context = new VirtualTimeOrchestrationContext(nameof(ApprovalOrchestration.AwaitApproval), new ApprovalRequest(TimeSpan.FromHours(24)));
        Task<ApprovalDecision> execution = ApprovalOrchestration.AwaitApproval(context);

        context.AdvanceBy(TimeSpan.FromHours(24));
        ApprovalDecision result = await execution.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.That(result.Approved, Is.False);
        Assert.That(context.Timers.Single().Outcome, Is.EqualTo(VirtualTimeOrchestrationContext.TimerOutcome.Fired));
        Assert.That(context.EventWaits.Single().IsCanceled, Is.True);
        Assert.Throws<InvalidOperationException>(() => context.RaiseEvent("Approved", new ApprovalDecision(true)));
    }

    /// <summary>Verifies that an event just before the deadline cancels the timer.</summary>
    [Test]
    public async Task EventOneTickBeforeTimeoutCancelsTimer()
    {
        using var context = new VirtualTimeOrchestrationContext(nameof(ApprovalOrchestration.AwaitApproval), new ApprovalRequest(TimeSpan.FromHours(24)));
        Task<ApprovalDecision> execution = ApprovalOrchestration.AwaitApproval(context);

        context.AdvanceBy(TimeSpan.FromHours(24) - TimeSpan.FromTicks(1));
        Assert.That(context.CurrentUtcDateTime, Is.EqualTo(new DateTime(2026, 1, 1, 23, 59, 59, DateTimeKind.Utc).AddTicks(9_999_999)));
        Assert.That(execution.IsCompleted, Is.False);

        context.RaiseEvent("Approved", new ApprovalDecision(true));
        ApprovalDecision result = await execution.WaitAsync(TimeSpan.FromSeconds(5));
        context.AdvanceBy(TimeSpan.FromTicks(1));

        Assert.That(result.Approved, Is.True);
        Assert.That(context.Timers.Single().Outcome, Is.EqualTo(VirtualTimeOrchestrationContext.TimerOutcome.Canceled));
    }

    /// <summary>Verifies that an event completes the orchestration before the configured timeout.</summary>
    [Test]
    public async Task EventBeforeProductionTimeoutCompletesAndCancelsTimer()
    {
        using var context = new VirtualTimeOrchestrationContext(nameof(ApprovalOrchestration.AwaitApproval), new ApprovalRequest(TimeSpan.FromHours(24)));
        Task<ApprovalDecision> execution = ApprovalOrchestration.AwaitApproval(context);

        Assert.That(execution.IsCompleted, Is.False);
        Assert.That(context.Timers, Has.Count.EqualTo(1));
        Assert.That(context.EventWaits, Has.Count.EqualTo(1));

        context.AdvanceBy(TimeSpan.FromHours(12));
        Assert.That(execution.IsCompleted, Is.False);

        context.RaiseEvent("Approved", new ApprovalDecision(true));
        ApprovalDecision result = await execution.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.That(result.Approved, Is.True);
        Assert.That(context.Timers.Single().Outcome, Is.EqualTo(VirtualTimeOrchestrationContext.TimerOutcome.Canceled));
        Assert.That(context.EventWaits.Single().IsCanceled, Is.False);
    }

    /// <summary>Verifies that a production-length timeout completes by advancing virtual time.</summary>
    [Test]
    public async Task ProductionTimeoutCanAdvanceWithoutWaitingAndCancelsEventWait()
    {
        using var context = new VirtualTimeOrchestrationContext(nameof(ApprovalOrchestration.AwaitApproval), new ApprovalRequest(TimeSpan.FromHours(24)));
        Task<ApprovalDecision> execution = ApprovalOrchestration.AwaitApproval(context);

        context.AdvanceBy(TimeSpan.FromHours(24));
        ApprovalDecision result = await execution.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.That(result.Approved, Is.False);
        Assert.That(context.Timers.Single().Outcome, Is.EqualTo(VirtualTimeOrchestrationContext.TimerOutcome.Fired));
        Assert.That(context.EventWaits.Single().IsCanceled, Is.True);
    }
}
