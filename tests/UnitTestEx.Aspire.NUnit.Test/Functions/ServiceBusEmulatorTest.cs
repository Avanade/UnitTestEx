using Aspire.Hosting.Testing;
using Azure.Messaging.ServiceBus;
using NUnit.Framework;
using System;
using System.Threading.Tasks;
using UnitTestEx;

namespace UnitTestEx.Aspire.NUnit.Test.Functions;

[TestFixture]
public sealed class ServiceBusEmulatorTest
{
    [Test]
    public async Task ServiceBusMessage_RunsFunction_AndWritesOutputQueue()
    {
        await using var tester = AspireTester.Create<Projects.UnitTestEx_Aspire_ServiceBus_AppHost>();
        try
        {
            await tester.WaitForFunctionsAsync("servicebus-functions", ["ServiceBusEcho"]);
        }
        catch
        {
            tester.Checkpoint("Service Bus Functions host did not become ready.");
            throw;
        }

        var connectionString = "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";

        await using var client = new ServiceBusClient(connectionString);
        await using var sender = client.CreateSender("input");
        await using var receiver = client.CreateReceiver("processed");
        const string content = "message delivered by the emulator";
        await sender.SendMessageAsync(new ServiceBusMessage(content));

        var processed = await tester.EventuallyAsync(
            async cancellationToken => (await receiver.PeekMessageAsync(cancellationToken: cancellationToken))?.Body.ToString(),
            value => string.Equals(value, content, StringComparison.Ordinal),
            timeout: TimeSpan.FromSeconds(60));

        Assert.That(processed, Is.EqualTo(content));
        tester.Checkpoint("Service Bus emulator trigger completed.");
        tester.AssertLogContains($"Processed Service Bus message: {content}");
    }
}
