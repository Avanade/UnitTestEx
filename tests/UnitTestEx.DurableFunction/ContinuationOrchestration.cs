using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;

namespace UnitTestEx.DurableFunction;

/// <summary>Continues an orchestration while an external event is buffered for the next generation.</summary>
public sealed class ContinuationOrchestration
{
    /// <summary>Coordinates event arrival before continuing with new input and consuming the next payload.</summary>
    /// <param name="context">The runtime orchestration context.</param>
    /// <returns>The payload consumed by the second generation.</returns>
    [Function(nameof(ContinueWithEvents))]
    public static async Task<string> ContinueWithEvents([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var input = context.GetInput<ContinuationRequest>() ?? throw new ArgumentException("Continuation input is required.");
        if (input.Generation == 0)
        {
            context.SetCustomStatus("initial");
            await context.WaitForExternalEvent<string>("Barrier");
            context.SetCustomStatus("buffered");
            await context.WaitForExternalEvent<string>("Continue");
            context.ContinueAsNew(input with { Generation = 1 }, input.PreserveEvents);
            return "discarded generation output";
        }

        context.SetCustomStatus("continued");
        return await context.WaitForExternalEvent<string>("Payload");
    }
}
