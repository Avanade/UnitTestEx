using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using System.Text.Json;
using UnitTestEx;
using UnitTestEx.Azure.Functions;
using UnitTestEx.Azure.Functions.Durable.TestHost;
using UnitTestEx.DurableFunction;
using UnitTestEx.NUnit.Internal;

namespace UnitTestEx.Azure.Functions.Durable.Test;

/// <summary>
/// Verifies real orchestration bodies and failure details through the in-memory Durable Task host.
/// </summary>
[TestFixture]
public sealed class DurableTaskHostTest
{
    /// <summary>Executes production function bodies and verifies SDK failure details.</summary>
    [Test]
    public async Task ExecutesFunctionsBodiesAndCapturesSdkFailureDetails()
    {
        using var functions = FunctionTester.Create<WorkflowActivities>(createFactory: () => new NUnitTestImplementor());
        DurableTaskTestHost? host = null;
        int replayCount = 0;
        int retryAttempts = 0;
        int retryExhaustionAttempts = 0;
        var normalizationStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseNormalization = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finalizationStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finalizationInstanceId = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            host = await functions.StartDurableTaskTestHostAsync(registry =>
            {
                registry.AddOrchestratorFunc<BatchRequest, BatchResult>(
                    nameof(BatchOrchestration.ProcessBatch),
                    (context, _) =>
                    {
                        if (context.IsReplaying)
                            Interlocked.Increment(ref replayCount);
                        return BatchOrchestration.ProcessBatch(context);
                    });
                registry.AddOrchestratorFunc<BatchResult, BatchResult>(
                    nameof(BatchOrchestration.FinalizeBatch),
                    async (context, input) =>
                    {
                        if (input.Reference == "ORDER-CHILD-FAIL")
                            throw new WorkflowRejectedException("Batch finalization was declined.");

                        if (input.Reference == "STATUS")
                        {
                            finalizationStarted.TrySetResult();
                            finalizationInstanceId.TrySetResult(context.InstanceId);
                            await context.WaitForExternalEvent<string>("ReleaseFinalization");
                        }

                        return await new BatchOrchestration().FinalizeBatch(context);
                    });
                registry.AddOrchestratorFunc<string, string>(nameof(FailureOrchestration.RecoverWorkflow), (context, _) => FailureOrchestration.RecoverWorkflow(context));
                registry.AddOrchestratorFunc<string, string>(nameof(FailureOrchestration.DeclineWorkflow), (context, _) => FailureOrchestration.DeclineWorkflow(context));

                registry.AddActivityFunc<string, string>(
                    nameof(ActivityTransforms.NormalizeReference),
                    async (_, reference) =>
                    {
                        if (string.Equals(reference, "snapshot", StringComparison.OrdinalIgnoreCase))
                        {
                            normalizationStarted.TrySetResult();
                            await releaseNormalization.Task;
                        }

                        return ActivityTransforms.NormalizeReference(reference);
                    });
                registry.AddActivityFunc<WorkItem, ItemResult>(
                    nameof(WorkflowActivities.ProcessItem),
                    async (_, item) =>
                    {
                        if (item.Id == "RETRY-FAIL")
                        {
                            Interlocked.Increment(ref retryExhaustionAttempts);
                            throw new WorkflowRejectedException("Item retry limit reached.");
                        }

                        if (item.Id == "RETRY" && Interlocked.Increment(ref retryAttempts) == 1)
                            throw new WorkflowRejectedException("Transient item failure.");

                        using var scope = functions.Services.CreateScope();
                        var activity = ActivatorUtilities.CreateInstance<WorkflowActivities>(scope.ServiceProvider);
                        return await activity.ProcessItem(item, new TestFunctionContext(scope.ServiceProvider));
                    });
                registry.AddActivityFunc<ItemResult[], decimal>(nameof(ActivityTransforms.TotalItems), (_, items) => ActivityTransforms.TotalItems(items));
                registry.AddActivityFunc<string>(
                    nameof(WorkflowActivities.RecordWorkflow),
                    (_, reference) =>
                    {
                        using var scope = functions.Services.CreateScope();
                        ActivatorUtilities.CreateInstance<WorkflowActivities>(scope.ServiceProvider).RecordWorkflow(reference);
                    });
                registry.AddActivityFunc<BatchResult>(
                    nameof(WorkflowActivities.PublishWorkflow),
                    async (_, result) =>
                    {
                        using var scope = functions.Services.CreateScope();
                        var activity = ActivatorUtilities.CreateInstance<WorkflowActivities>(scope.ServiceProvider);
                        await activity.PublishWorkflow(result);
                    });
                registry.AddActivityFunc<string, string>(nameof(ActivityTransforms.RejectWorkflow), (_, reference) => ActivityTransforms.RejectWorkflow(reference));
            });

            var request = new BatchRequest(" order-1 ", [new("A", 1m), new("B", 2m), new("C", 3m)]);
            var batchId = await host.Client.ScheduleNewOrchestrationInstanceAsync(nameof(BatchOrchestration.ProcessBatch), request);
            var batchMetadata = await host.Client.WaitForInstanceCompletionAsync(batchId, getInputsAndOutputs: true);

            Assert.That(batchMetadata, Is.Not.Null);
            Assert.That(batchMetadata!.RuntimeStatus, Is.EqualTo(OrchestrationRuntimeStatus.Completed));
            var result = batchMetadata.ReadOutputAs<BatchResult>()!;
            Assert.That(result.Reference, Is.EqualTo("ORDER-1"));
            Assert.That(result.Total, Is.EqualTo(12m));
            Assert.That(result.Items, Is.EqualTo(new[]
            {
                new ItemResult("A", 2m),
                new ItemResult("B", 4m),
                new ItemResult("C", 6m),
            }));
            Assert.That(batchMetadata.ReadCustomStatusAs<JsonElement>().GetProperty("Stage").GetString(), Is.EqualTo("Finalizing"));
            result.Items[0] = new ItemResult("MUTATED", 999m);
            Assert.That(batchMetadata.ReadOutputAs<BatchResult>()!.Items[0], Is.EqualTo(new ItemResult("A", 2m)));
            Assert.That(replayCount, Is.GreaterThan(0), "The host should replay the orchestration after durable work completes.");
            Assert.That(functions.SharedState.GetLoggerMessages().Any(message =>
                message?.Contains("Processing item A.", StringComparison.Ordinal) == true), Is.True);

            var snapshotRequest = new BatchRequest("snapshot", [new WorkItem("ORIGINAL", 5m)]);
            var snapshotId = await host.Client.ScheduleNewOrchestrationInstanceAsync(nameof(BatchOrchestration.ProcessBatch), snapshotRequest);
            await normalizationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            snapshotRequest.Items[0] = new WorkItem("MUTATED", 99m);
            releaseNormalization.TrySetResult();
            var snapshotMetadata = await host.Client.WaitForInstanceCompletionAsync(snapshotId, getInputsAndOutputs: true)
                .WaitAsync(TimeSpan.FromSeconds(5));

            Assert.That(snapshotMetadata!.RuntimeStatus, Is.EqualTo(OrchestrationRuntimeStatus.Completed));
            Assert.That(snapshotMetadata.ReadOutputAs<BatchResult>()!.Items, Is.EqualTo(new[] { new ItemResult("ORIGINAL", 10m) }));

            var retryId = await host.Client.ScheduleNewOrchestrationInstanceAsync(
                nameof(BatchOrchestration.ProcessBatch),
                new BatchRequest("ORDER-RETRY", [new WorkItem("RETRY", 7m)]));
            var retryMetadata = await host.Client.WaitForInstanceCompletionAsync(retryId, getInputsAndOutputs: true)
                .WaitAsync(TimeSpan.FromSeconds(10));

            Assert.That(retryMetadata!.RuntimeStatus, Is.EqualTo(OrchestrationRuntimeStatus.Completed));
            Assert.That(retryMetadata.ReadOutputAs<BatchResult>()!.Items, Is.EqualTo(new[] { new ItemResult("RETRY", 14m) }));
            Assert.That(retryAttempts, Is.EqualTo(2), "The host should retry the first failed activity attempt once.");

            var retryFailureId = await host.Client.ScheduleNewOrchestrationInstanceAsync(
                nameof(BatchOrchestration.ProcessBatch),
                new BatchRequest("ORDER-RETRY-FAIL", [new WorkItem("RETRY-FAIL", 3m)]));
            var retryFailureMetadata = await host.Client.WaitForInstanceCompletionAsync(retryFailureId, getInputsAndOutputs: true)
                .WaitAsync(TimeSpan.FromSeconds(10));

            Assert.That(retryFailureMetadata!.RuntimeStatus, Is.EqualTo(OrchestrationRuntimeStatus.Failed));
            Assert.That(retryExhaustionAttempts, Is.EqualTo(3), "The activity should stop after its configured maximum of three attempts.");
            Assert.That(retryFailureMetadata.FailureDetails?.InnerFailure?.ErrorType, Is.EqualTo(typeof(WorkflowRejectedException).FullName));
            Assert.That(retryFailureMetadata.FailureDetails?.InnerFailure?.ErrorMessage, Is.EqualTo("Item retry limit reached."));

            var recoveryId = await host.Client.ScheduleNewOrchestrationInstanceAsync(nameof(FailureOrchestration.RecoverWorkflow), "ORDER-FAIL");
            var recoveryMetadata = await host.Client.WaitForInstanceCompletionAsync(recoveryId, getInputsAndOutputs: true);
            Assert.That(recoveryMetadata!.RuntimeStatus, Is.EqualTo(OrchestrationRuntimeStatus.Completed));
            Assert.That(recoveryMetadata.ReadOutputAs<string>(), Is.EqualTo("Declined"));

            var declineId = await host.Client.ScheduleNewOrchestrationInstanceAsync(nameof(FailureOrchestration.DeclineWorkflow), "ORDER-FAIL");
            var declineMetadata = await host.Client.WaitForInstanceCompletionAsync(declineId, getInputsAndOutputs: true);
            Assert.That(declineMetadata.RuntimeStatus, Is.EqualTo(OrchestrationRuntimeStatus.Failed));
            var declineFailure = declineMetadata.FailureDetails
                ?? throw new AssertionException("Failed orchestration metadata should include failure details.");
            Assert.That(declineFailure.ErrorType, Is.EqualTo(typeof(TaskFailedException).FullName));
            var activityFailure = declineFailure.InnerFailure
                ?? throw new AssertionException("The orchestration failure should retain the activity failure as an inner detail.");
            Assert.That(activityFailure.ErrorType, Is.EqualTo(typeof(WorkflowRejectedException).FullName));
            Assert.That(activityFailure.ErrorMessage, Is.EqualTo("Workflow ORDER-FAIL was declined."));
            Assert.That(activityFailure.IsCausedBy<WorkflowRejectedException>(), Is.True);

            var childFailureId = await host.Client.ScheduleNewOrchestrationInstanceAsync(
                nameof(BatchOrchestration.ProcessBatch),
                new BatchRequest("ORDER-CHILD-FAIL", [new("A", 4m)]));
            var childFailureMetadata = await host.Client.WaitForInstanceCompletionAsync(childFailureId, getInputsAndOutputs: true)
                .WaitAsync(TimeSpan.FromSeconds(5));
            Assert.That(childFailureMetadata!.RuntimeStatus, Is.EqualTo(OrchestrationRuntimeStatus.Failed));
            var childFailure = childFailureMetadata.FailureDetails
                ?? throw new AssertionException("Failed parent metadata should include sub-orchestration failure details.");
            Assert.That(childFailure.ErrorType, Is.EqualTo(typeof(TaskFailedException).FullName));
            Assert.That(childFailure.InnerFailure?.ErrorType, Is.EqualTo(typeof(WorkflowRejectedException).FullName));
            Assert.That(childFailure.InnerFailure?.ErrorMessage, Is.EqualTo("Batch finalization was declined."));
            Assert.That(childFailure.InnerFailure?.IsCausedBy<WorkflowRejectedException>(), Is.True);

            var statusId = await host.Client.ScheduleNewOrchestrationInstanceAsync(
                nameof(BatchOrchestration.ProcessBatch),
                new BatchRequest("status", [new WorkItem("STATUS", 1m)]));
            await finalizationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

            var runningStatusMetadata = await host.Client.GetInstanceAsync(statusId, getInputsAndOutputs: true)
                .WaitAsync(TimeSpan.FromSeconds(5));
            Assert.That(runningStatusMetadata!.RuntimeStatus, Is.EqualTo(OrchestrationRuntimeStatus.Running));
            Assert.That(runningStatusMetadata.ReadCustomStatusAs<JsonElement>().GetProperty("Stage").GetString(), Is.EqualTo("Finalizing"));

            string finalizationId = await finalizationInstanceId.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await host.Client.RaiseEventAsync(finalizationId, "ReleaseFinalization", "release");
            var completedStatusMetadata = await host.Client.WaitForInstanceCompletionAsync(statusId, getInputsAndOutputs: true)
                .WaitAsync(TimeSpan.FromSeconds(5));
            Assert.That(completedStatusMetadata!.RuntimeStatus, Is.EqualTo(OrchestrationRuntimeStatus.Completed));
            Assert.That(completedStatusMetadata.ReadCustomStatusAs<JsonElement>().GetProperty("Stage").GetString(), Is.EqualTo("Finalizing"));
        }
        finally
        {
            releaseNormalization.TrySetResult();
            if (host is not null)
                await host.DisposeAsync();
        }
    }
}
