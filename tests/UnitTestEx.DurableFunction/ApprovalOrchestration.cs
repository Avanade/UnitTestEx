using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;

namespace UnitTestEx.DurableFunction;

public sealed class ApprovalOrchestration
{
    [Function(nameof(AwaitApproval))]
    public static async Task<ApprovalDecision> AwaitApproval([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var input = context.GetInput<ApprovalRequest>() ?? throw new ArgumentException("Approval input is required.");
        using var timerCancellation = new CancellationTokenSource();
        using var eventCancellation = new CancellationTokenSource();
        var timeout = context.CreateTimer(context.CurrentUtcDateTime.Add(input.Timeout), timerCancellation.Token);
        var approval = context.WaitForExternalEvent<ApprovalDecision>("Approved", eventCancellation.Token);
        try
        {
            var winner = await Task.WhenAny(approval, timeout);
            return winner == approval ? await approval : new ApprovalDecision(false);
        }
        finally
        {
            timerCancellation.Cancel();
            eventCancellation.Cancel();
        }
    }
}
