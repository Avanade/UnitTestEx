using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using UnitTestEx.Abstractions;
using UnitTestEx.Assertors;

namespace UnitTestEx.Azure.Functions.Durable.TestHost;

/// <summary>
/// Provides fluent assertions over SDK-host orchestration metadata.
/// </summary>
/// <typeparam name="TOutput">The orchestration output type.</typeparam>
public sealed class DurableTaskTestAssertor<TOutput>(TesterBase owner, OrchestrationMetadata metadata) : AssertorBase<DurableTaskTestAssertor<TOutput>>(owner, null, null)
{
    /// <summary>
    /// Gets the SDK orchestration metadata.
    /// </summary>
    public OrchestrationMetadata Metadata { get; } = metadata ?? throw new ArgumentNullException(nameof(metadata));

    /// <summary>
    /// Gets the orchestration runtime status.
    /// </summary>
    public OrchestrationRuntimeStatus Status => Metadata.RuntimeStatus;

    /// <summary>
    /// Gets the deserialized orchestration output.
    /// </summary>
    public TOutput? Output => Metadata.ReadOutputAs<TOutput>();

    /// <summary>
    /// Gets the SDK failure details, if the orchestration failed.
    /// </summary>
    public TaskFailureDetails? FailureDetails => Metadata.FailureDetails;

    /// <summary>
    /// Asserts that the orchestration completed successfully.
    /// </summary>
    /// <returns>This assertor for fluent chaining.</returns>
    public DurableTaskTestAssertor<TOutput> AssertCompleted()
    {
        if (Status != OrchestrationRuntimeStatus.Completed)
            Implementor.AssertFail($"Expected orchestration '{Metadata.Name}' to be completed, but its status was '{Status}'.");

        return this;
    }

    /// <summary>
    /// Asserts that the orchestration failed.
    /// </summary>
    /// <returns>This assertor for fluent chaining.</returns>
    public DurableTaskTestAssertor<TOutput> AssertFailed()
    {
        if (Status != OrchestrationRuntimeStatus.Failed)
            Implementor.AssertFail($"Expected orchestration '{Metadata.Name}' to have failed, but its status was '{Status}'.");

        return this;
    }

    /// <summary>
    /// Asserts that the orchestration output matches the expected value.
    /// </summary>
    /// <param name="expected">The expected output.</param>
    /// <param name="pathsToIgnore">Optional JSON paths to ignore when comparing the output.</param>
    /// <returns>This assertor for fluent chaining.</returns>
    public DurableTaskTestAssertor<TOutput> AssertOutput(TOutput? expected, params string[] pathsToIgnore)
    {
        AssertCompleted();
        var comparison = Owner.CreateJsonComparer().CompareValue(expected, Output, pathsToIgnore);
        if (comparison.HasDifferences)
            Implementor.AssertFail($"Expected and actual orchestration outputs are not equal:{Environment.NewLine}{comparison}");

        return this;
    }

    /// <summary>
    /// Asserts that the orchestration's failure details identify the specified exception type.
    /// </summary>
    /// <typeparam name="TException">The expected underlying exception type.</typeparam>
    /// <returns>This assertor for fluent chaining.</returns>
    public DurableTaskTestAssertor<TOutput> AssertFailureCausedBy<TException>()
        where TException : Exception
    {
        AssertFailed();
        TaskFailureDetails? failure = FailureDetails;
        while (failure is not null && !failure.IsCausedBy<TException>())
            failure = failure.InnerFailure;

        if (failure is null)
            Implementor.AssertFail($"Expected orchestration failure to be caused by '{typeof(TException).FullName}'.");

        return this;
    }
}
