using Microsoft.DurableTask;

namespace UnitTestEx.Azure.Functions.Durable.Test;

/// <summary>
/// Calls an activity that fails to verify class-based orchestration failure metadata.
/// </summary>
public sealed class ClassBasedFailureOrchestrator : TaskOrchestrator<string, string>
{
    /// <summary>Schedules the failing activity.</summary>
    /// <param name="context">The orchestration context.</param>
    /// <param name="input">The orchestration input.</param>
    /// <returns>The activity result, or a failed task.</returns>
    public override Task<string> RunAsync(TaskOrchestrationContext context, string input)
        => context.CallActivityAsync<string>("ClassBasedFailureActivity", input);
}
