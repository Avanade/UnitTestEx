using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;

namespace UnitTestEx.DurableFunction;

public sealed class FailureOrchestration
{
    [Function(nameof(RecoverWorkflow))]
    public static async Task<string> RecoverWorkflow([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        try
        {
            return await context.CallActivityAsync<string>(nameof(ActivityTransforms.RejectWorkflow), context.GetInput<string>());
        }
        catch (TaskFailedException exception) when (exception.FailureDetails.IsCausedBy<WorkflowRejectedException>())
        {
            context.SetCustomStatus("Declined");
            return "Declined";
        }
    }

    [Function(nameof(DeclineWorkflow))]
    public static Task<string> DeclineWorkflow([OrchestrationTrigger] TaskOrchestrationContext context)
        => context.CallActivityAsync<string>(nameof(ActivityTransforms.RejectWorkflow), context.GetInput<string>());
}
