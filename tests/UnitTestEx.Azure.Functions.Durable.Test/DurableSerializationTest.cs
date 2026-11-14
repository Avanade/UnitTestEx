using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Testing;
using NUnit.Framework;

namespace UnitTestEx.Azure.Functions.Durable.Test;

/// <summary>
/// Verifies selected DTO serialization behavior through standalone Durable Task SDK boundaries.
/// </summary>
[TestFixture]
public sealed class DurableSerializationTest
{
    /// <summary>Verifies nullable values, enum values, and nested collections survive SDK boundary serialization.</summary>
    [Test]
    public async Task NullableEnumAndNestedCollectionRoundTripThroughOrchestrationAndActivity()
    {
        DurableTaskTestHost? host = null;
        var input = new SerializationProbe(
            SerializationProbeState.Completed,
            null,
            [new SerializationProbeChild("first", 1), new SerializationProbeChild(null, 2)]);

        try
        {
            host = await DurableTaskTestHost.StartAsync(registry =>
            {
                registry.AddOrchestratorFunc<SerializationProbe, SerializationProbe>(
                    "SerializationRoundTrip",
                    (context, request) => context.CallActivityAsync<SerializationProbe>("SerializationEcho", request));
                registry.AddActivityFunc<SerializationProbe, SerializationProbe>("SerializationEcho", (_, request) => request);
            });

            var instanceId = await host.Client.ScheduleNewOrchestrationInstanceAsync("SerializationRoundTrip", input);
            var metadata = await host.Client.WaitForInstanceCompletionAsync(instanceId, getInputsAndOutputs: true)
                .WaitAsync(TimeSpan.FromSeconds(5));

            Assert.That(metadata!.RuntimeStatus, Is.EqualTo(OrchestrationRuntimeStatus.Completed));
            var output = metadata.ReadOutputAs<SerializationProbe>();
            Assert.That(output, Is.Not.Null);
            Assert.That(output, Is.Not.SameAs(input));
            Assert.That(output!.State, Is.EqualTo(SerializationProbeState.Completed));
            Assert.That(output.OptionalValue, Is.Null);
            Assert.That(output.Children, Is.Not.SameAs(input.Children));
            Assert.That(output.Children, Is.EqualTo(input.Children));
        }
        finally
        {
            if (host is not null)
                await host.DisposeAsync();
        }
    }
}
