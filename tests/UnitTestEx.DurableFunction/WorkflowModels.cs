namespace UnitTestEx.DurableFunction;

public record WorkItem(string Id, decimal Amount);
public record ItemResult(string Id, decimal Amount);
public record BatchRequest(string Reference, WorkItem[] Items);
public record BatchResult(string Reference, decimal Total, ItemResult[] Items);
public record ApprovalRequest(TimeSpan Timeout);
public record ApprovalDecision(bool Approved);

/// <summary>Specifies the continuation generation and external-event preservation policy.</summary>
/// <param name="Generation">The current orchestration generation.</param>
/// <param name="PreserveEvents">Whether unconsumed events are preserved when continuing.</param>
public record ContinuationRequest(int Generation, bool PreserveEvents);

public sealed class WorkflowRejectedException(string message) : Exception(message);
