using Azure.Messaging.EventHubs;
using NUnit.Framework;
using System;
using System.Threading.Tasks;
using UnitTestEx.Azure.Functions;
using UnitTestEx.Expectations;
using UnitTestEx.IsolatedFunction;

namespace UnitTestEx.NUnit.Test;

[TestFixture]
public sealed class EventHubsTest
{
    [Test]
    public void FromValue_UsesTesterSerializerAndProperties()
    {
        using var test = FunctionTester.Create<EventHubsFunctions>();
        var value = new Person("Jane", "Doe");
        var message = test.CreateEventDataFromValue(value, e => e.Properties["source"] = "unit-test");

        Assert.Multiple(() =>
        {
            Assert.That(message.EventBody.ToString(), Is.EqualTo(test.JsonSerializer.Serialize(value)));
            Assert.That(message.Properties["source"], Is.EqualTo("unit-test"));
        });
    }

    [Test]
    public void FromResource_UsesSpecifiedAssembly()
    {
        using var test = FunctionTester.Create<EventHubsFunctions>();
        var message = test.CreateEventDataFromResource<EventHubsTest>("FunctionTest-ValidJsonResource.json");

        Assert.That(message.EventBody.ToString(), Is.EqualTo(Resource.GetJson("FunctionTest-ValidJsonResource.json", typeof(EventHubsTest).Assembly)));
    }

    [Test]
    public void ReceivedMetadataAndBinaryBody_ArePreserved()
    {
        using var test = FunctionTester.Create<EventHubsFunctions>();
        var body = new byte[] { 0, 255, 128, 13, 10 };
        var time = DateTimeOffset.Parse("2026-01-02T03:04:05Z");
        var message = test.CreateEventData(BinaryData.FromBytes(body), 42, "offset-43", time, "customers");

        Assert.Multiple(() =>
        {
            Assert.That(message.EventBody.ToArray(), Is.EqualTo(body));
            Assert.That(message.SequenceNumber, Is.EqualTo(42));
            Assert.That(message.OffsetString, Is.EqualTo("offset-43"));
            Assert.That(message.EnqueuedTime, Is.EqualTo(time));
            Assert.That(message.PartitionKey, Is.EqualTo("customers"));
        });
    }

    [Test]
    public void SingleEvent_ExpectationsAndMiddleware()
    {
        using var test = FunctionTester.Create<EventHubsFunctions>();
        var message = test.CreateEventData(BinaryData.FromString("hello"), sequenceNumber: 42, partitionKey: "customers");

        test.Trigger<EventHubsFunctions>()
            .ExpectLogContains("Received event 42 for customers.")
            .ExpectLogContains("Middleware before EventHubSingle.")
            .ExpectLogContains("Middleware after EventHubSingle.")
            .Run(f => f.Single(message))
            .AssertValue("hello");
    }

    [TestCase(0)]
    [TestCase(2)]
    public async Task BatchEvents_AsyncAndEmpty(int count)
    {
        using var test = FunctionTester.Create<EventHubsFunctions>();
        var messages = new EventData[count];
        var expected = new string[count];
        for (var i = 0; i < count; i++)
        {
            expected[i] = $"event-{i}";
            messages[i] = test.CreateEventDataFromJson(expected[i]);
        }

        var result = await test.Trigger<EventHubsFunctions>()
            .ExpectLogContains($"Received {count} events.")
            .RunAsync(f => f.Batch(messages));
        result.AssertValue(expected);
    }

    [Test]
    public void FunctionException_IsAssertable()
    {
        using var test = FunctionTester.Create<EventHubsFunctions>();
        var message = test.CreateEventData(BinaryData.FromString("bad"), sequenceNumber: 42);

        test.Trigger<EventHubsFunctions>()
            .Run(f => f.Fail(message))
            .AssertException<InvalidOperationException>("Rejected event 42.");
    }

    [Test]
    public void InvalidBuilderArguments_AreRejected()
    {
        using var test = FunctionTester.Create<EventHubsFunctions>();
        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentNullException>(() => test.CreateEventData(null!));
            Assert.Throws<ArgumentNullException>(() => test.CreateEventDataFromJson(null!));
            Assert.Throws<ArgumentOutOfRangeException>(() => test.CreateEventData(BinaryData.Empty, sequenceNumber: -1));
        });
    }
}
