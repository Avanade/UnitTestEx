using Microsoft.DurableTask;

namespace UnitTestEx.Azure.Functions.Durable.Test;

/// <summary>
/// Calls the echo activity using the typed TaskOrchestrator base class.
/// </summary>
public sealed class ClassBasedEchoOrchestrator : TaskOrchestrator<string, string>
{
    /// <summary>Schedules the echo activity and returns its result.</summary>
    /// <param name="context">The orchestration context.</param>
    /// <param name="input">The orchestration input.</param>
    /// <returns>The activity result.</returns>
    public override Task<string> RunAsync(TaskOrchestrationContext context, string input) => context.CallActivityAsync<string>("ClassBasedEchoActivity", input);
}
