namespace UnitTestEx.DurableFunction;

public record WorkItem(string Id, decimal Amount);
public record ItemResult(string Id, decimal Amount);
public record BatchRequest(string Reference, WorkItem[] Items);
public record BatchResult(string Reference, decimal Total, ItemResult[] Items);
public record ApprovalRequest(TimeSpan Timeout);
public record ApprovalDecision(bool Approved);

public sealed class WorkflowRejectedException(string message) : Exception(message);
