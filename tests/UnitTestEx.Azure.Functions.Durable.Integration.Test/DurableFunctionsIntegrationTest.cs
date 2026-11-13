using Aspire.Hosting.Testing;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using UnitTestEx.Aspire;
using UnitTestEx.DurableFunction;

namespace UnitTestEx.Azure.Functions.Durable.Integration.Test;

/// <summary>Exercises the real Functions worker and Azure Storage Durable provider against disposable Azurite storage.</summary>
/// <remarks>Requires Core Tools and a container runtime. No in-memory SDK test host, mock activity or virtual clock is used.</remarks>
[TestFixture]
[NonParallelizable]
[Category("DurableIntegration")]
public sealed class DurableFunctionsIntegrationTest
{
    /// <summary>Provides case-insensitive JSON conversion for Functions management payloads and workflow models.</summary>
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Reads the finalization stage independently of the worker's JSON property casing.</summary>
    private sealed record FinalizationStatus(string Stage);

    /// <summary>Executes the real batch, including the instance child, injected journal and worker-supplied activity context.</summary>
    [Test]
    public async Task BatchExecutesRealActivitiesAndChild()
    {
        await using var tester = AspireTester.Create<Projects.UnitTestEx_Aspire_Durable_AppHost>();
        await tester.WaitForFunctionsAsync("durable-functions", ["ProcessBatch", "NormalizeReference", "ProcessItem", "TotalItems", "FinalizeBatch", "RecordWorkflow", "PublishWorkflow"]);
        using var client = (await tester.GetDistributedApplicationAsync()).CreateHttpClient("durable-functions");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(90));

        var request = new BatchRequest(" batch-7 ", [new("A", 10m), new("B", 20m), new("C", 30m)]);
        var management = await StartAsync(client, nameof(BatchOrchestration.ProcessBatch), request, deadline.Token);
        var status = await WaitForTerminalAsync(tester, client, management, deadline.Token);
        Assert.That(status.GetProperty("runtimeStatus").GetString(), Is.EqualTo("Completed"), status.ToString());
        var storedInput = status.GetProperty("input").Deserialize<BatchRequest>(JsonOptions) ?? throw new InvalidOperationException("Stored batch input was null.");
        Assert.That(storedInput.Reference, Is.EqualTo(request.Reference));
        Assert.That(storedInput.Items, Is.EqualTo(request.Items));
        var output = status.GetProperty("output").Deserialize<BatchResult>(JsonOptions) ?? throw new InvalidOperationException("Completed batch output was null.");
        Assert.That(output.Reference, Is.EqualTo("BATCH-7"));
        Assert.That(output.Total, Is.EqualTo(120m));
        Assert.That(output.Items, Is.EqualTo(new[] { new ItemResult("A", 20m), new ItemResult("B", 40m), new ItemResult("C", 60m) }));
        Assert.That(status.GetProperty("customStatus").Deserialize<FinalizationStatus>(JsonOptions)?.Stage, Is.EqualTo("Finalizing"));

        // Resource logs are forwarded asynchronously; wait for publication before checking the complete application path.
        await tester.EventuallyAsync(_ =>
        {
            try
            {
                tester.AssertLogContains("Published workflow BATCH-7 with total 120.", "durable-functions");
                return Task.FromResult(true);
            }
            catch (AssertionException)
            {
                return Task.FromResult(false);
            }
        }, observed => observed, cancellationToken: deadline.Token);
        tester.Checkpoint("Real Durable batch completed.");
        tester.AssertLogContains("Processing item A.", "durable-functions");
        tester.AssertLogContains("Processing item B.", "durable-functions");
        tester.AssertLogContains("Processing item C.", "durable-functions");
        tester.AssertLogContains("Recorded workflow BATCH-7.", "durable-functions");
        tester.AssertLogContains("Published workflow BATCH-7 with total 120.", "durable-functions");
        await PurgeAsync(client, management, deadline.Token);
    }

    /// <summary>Verifies that a real failed activity crosses the Functions extension boundary and is caught by the orchestration.</summary>
    [Test]
    public async Task ActivityFailureIsRecoveredByRealOrchestration()
    {
        await using var tester = AspireTester.Create<Projects.UnitTestEx_Aspire_Durable_AppHost>().ErrorWhenLogContains(Microsoft.Extensions.Logging.LogLevel.None);
        await tester.WaitForFunctionsAsync("durable-functions", ["RecoverWorkflow", "RejectWorkflow"]);
        using var client = (await tester.GetDistributedApplicationAsync()).CreateHttpClient("durable-functions");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var management = await StartAsync(client, nameof(FailureOrchestration.RecoverWorkflow), "ORDER-REJECT", deadline.Token);
        var status = await WaitForTerminalAsync(tester, client, management, deadline.Token);
        Assert.That(status.GetProperty("runtimeStatus").GetString(), Is.EqualTo("Completed"), status.ToString());
        Assert.That(status.GetProperty("output").GetString(), Is.EqualTo("Declined"));
        Assert.That(status.GetProperty("customStatus").GetString(), Is.EqualTo("Declined"));
        await PurgeAsync(client, management, deadline.Token);
    }

    /// <summary>Delivers an event through the real management API without shortening the application's 24-hour timeout.</summary>
    [Test]
    public async Task ApprovalEventWinsProductionLengthTimer()
    {
        await using var tester = AspireTester.Create<Projects.UnitTestEx_Aspire_Durable_AppHost>();
        await tester.WaitForFunctionsAsync("durable-functions", ["AwaitApproval"]);
        using var client = (await tester.GetDistributedApplicationAsync()).CreateHttpClient("durable-functions");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var management = await StartAsync(client, nameof(ApprovalOrchestration.AwaitApproval), new ApprovalRequest(TimeSpan.FromHours(24)), deadline.Token);
        await tester.EventuallyAsync(token => ReadStatusAsync(client, management, token), status => status?.GetProperty("runtimeStatus").GetString() == "Running",
            timeout: TimeSpan.FromSeconds(60), cancellationToken: deadline.Token);
        var eventUrl = management.GetProperty("sendEventPostUri").GetString()!.Replace("{eventName}", "Approved", StringComparison.Ordinal);
        using var response = await client.PostAsJsonAsync(LocalUri(client, eventUrl), new ApprovalDecision(true), deadline.Token);
        await RequireStatusAsync(response, HttpStatusCode.Accepted, deadline.Token);
        var status = await WaitForTerminalAsync(tester, client, management, deadline.Token);
        Assert.That(status.GetProperty("runtimeStatus").GetString(), Is.EqualTo("Completed"), status.ToString());
        Assert.That(status.GetProperty("output").Deserialize<ApprovalDecision>(JsonOptions), Is.EqualTo(new ApprovalDecision(true)));
        await PurgeAsync(client, management, deadline.Token);
    }

    /// <summary>Schedules an instance through the extension's documented HTTP API and retains its returned management URLs.</summary>
    private static async Task<JsonElement> StartAsync<T>(HttpClient client, string name, T input, CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync($"runtime/webhooks/durabletask/orchestrators/{Uri.EscapeDataString(name)}/{Guid.NewGuid():N}", input, cancellationToken);
        await RequireStatusAsync(response, HttpStatusCode.Accepted, cancellationToken);
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
    }

    /// <summary>Polls persisted management state and returns any terminal result so failures are asserted rather than mistaken for timeouts.</summary>
    private static async Task<JsonElement> WaitForTerminalAsync(AspireTesterBase tester, HttpClient client, JsonElement management, CancellationToken cancellationToken)
    {
        var status = await tester.EventuallyAsync(token => ReadStatusAsync(client, management, token),
            value => value?.GetProperty("runtimeStatus").GetString() is "Completed" or "Failed" or "Terminated" or "Canceled",
            timeout: TimeSpan.FromSeconds(60), cancellationToken: cancellationToken);
        return status ?? throw new InvalidOperationException("Durable terminal status was missing.");
    }

    /// <summary>Reads the instance's management status; only documented successful pending/completed responses are accepted.</summary>
    private static async Task<JsonElement?> ReadStatusAsync(HttpClient client, JsonElement management, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(LocalUri(client, management.GetProperty("statusQueryGetUri").GetString()!), cancellationToken);
        // The extension can acknowledge scheduling before the instance is visible in Azure Storage.
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        if (response.StatusCode is not (HttpStatusCode.OK or HttpStatusCode.Accepted))
            throw new HttpRequestException($"Durable status returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(cancellationToken)}", null, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
    }

    /// <summary>Purges the completed parent history; AppHost disposal removes the nonpersistent container, including child history and pending timers.</summary>
    private static async Task PurgeAsync(HttpClient client, JsonElement management, CancellationToken cancellationToken)
    {
        using var response = await client.DeleteAsync(LocalUri(client, management.GetProperty("purgeHistoryDeleteUri").GetString()!), cancellationToken);
        await RequireStatusAsync(response, HttpStatusCode.OK, cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        Assert.That(result.GetProperty("instancesDeleted").GetInt32(), Is.EqualTo(1));
    }

    /// <summary>Restricts returned management URLs to the test-owned Functions host.</summary>
    private static Uri LocalUri(HttpClient client, string value)
    {
        var uri = new Uri(value, UriKind.Absolute);
        if (client.BaseAddress is null || uri.GetLeftPart(UriPartial.Authority) != client.BaseAddress.GetLeftPart(UriPartial.Authority))
            throw new InvalidOperationException("Durable management URL does not belong to the test Functions host.");
        return uri;
    }

    /// <summary>Reports nonexpected HTTP responses with their bodies rather than losing extension diagnostics.</summary>
    private static async Task RequireStatusAsync(HttpResponseMessage response, HttpStatusCode expected, CancellationToken cancellationToken)
    {
        if (response.StatusCode != expected)
            throw new HttpRequestException($"Expected {(int)expected}, received {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(cancellationToken)}", null, response.StatusCode);
    }
}
