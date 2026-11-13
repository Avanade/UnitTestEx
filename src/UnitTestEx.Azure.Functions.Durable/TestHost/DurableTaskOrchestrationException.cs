using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;

namespace UnitTestEx.Azure.Functions.Durable.TestHost;

/// <summary>
/// Represents a non-completed orchestration outcome in the test expectation pipeline.
/// </summary>
/// <remarks>This exception is not thrown by the SDK host.</remarks>
/// <param name="metadata">The orchestration metadata.</param>
public sealed class DurableTaskOrchestrationException(OrchestrationMetadata metadata) : Exception(CreateMessage(metadata))
{
    /// <summary>
    /// Gets the orchestration metadata.
    /// </summary>
    public OrchestrationMetadata Metadata { get; } = metadata;

    /// <summary>
    /// Gets the SDK failure details, if the orchestration failed.
    /// </summary>
    public TaskFailureDetails? FailureDetails => Metadata.FailureDetails;

    /// <summary>
    /// Creates a message for the specified orchestration metadata.
    /// </summary>
    /// <param name="metadata">The orchestration metadata.</param>
    /// <returns>A message describing the non-completed orchestration outcome.</returns>
    private static string CreateMessage(OrchestrationMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        if (metadata.RuntimeStatus == OrchestrationRuntimeStatus.Completed)
            throw new ArgumentException("The orchestration must not have completed successfully.", nameof(metadata));

        string message = $"Orchestration '{metadata.Name}' ended with status '{metadata.RuntimeStatus}'.";
        return metadata.FailureDetails is { } details && !string.IsNullOrWhiteSpace(details.ErrorMessage) ? $"{message} {details.ErrorMessage}" : message;
    }
}
