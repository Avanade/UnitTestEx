using Aspire.Hosting.Testing;
using Azure;
using Azure.Storage;
using Azure.Storage.Blobs;
using NUnit.Framework;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnitTestEx;

namespace UnitTestEx.Aspire.NUnit.Test.Functions;

[TestFixture]
public sealed class AzuriteBlobTriggerTest
{
    private const string AccountName = "devstoreaccount1";
    private const string AccountKey = "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==";
    private const string InputContainerName = "unittestex";
    private const string OutputContainerName = "processed";
    private const string OutputBlobName = "result.txt";

    [Test]
    public async Task BlobCreatedWebhook_RunsFunction_AndWritesProcessedBlob()
    {
        await using var tester = AspireTester.Create<Projects.UnitTestEx_Aspire_Functions_AppHost>();
        await tester.WaitForFunctionsAsync("functions", ["BlobTrigger", "ServiceBusFunction4"]);

        var app = await tester.GetDistributedApplicationAsync();
        var storageEndpoint = app.GetEndpoint("azurite", "blob");
        var storageUri = new Uri($"{storageEndpoint.GetLeftPart(UriPartial.Authority)}/{AccountName}/");
        var blobService = new BlobServiceClient(
            storageUri,
            new StorageSharedKeyCredential(AccountName, AccountKey),
            new BlobClientOptions(BlobClientOptions.ServiceVersion.V2021_12_02));
        var inputContainer = blobService.GetBlobContainerClient(InputContainerName);
        await inputContainer.CreateIfNotExistsAsync();
        await blobService.GetBlobContainerClient(OutputContainerName).CreateIfNotExistsAsync();

        const string blobName = "customers.csv";
        const string content = "id,name\n1,Ada Lovelace";
        var inputBlob = GetBlobClient(inputContainer, blobName);
        var upload = await inputBlob.UploadAsync(BinaryData.FromString(content), overwrite: true);

        var createdEvent = EventGridExtensionMethods.CreateBlobCreatedEvent(
            InputContainerName,
            blobName,
            inputBlob.Uri,
            upload.Value.ETag.ToString(),
            "text/csv",
            Encoding.UTF8.GetByteCount(content));

        using (var response = await tester.FunctionsWebhook("functions").PostBlobCreatedAsync("Host.Functions.BlobTrigger", createdEvent))
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                tester.Checkpoint("BlobCreated webhook failed.");
                Assert.Fail($"BlobCreated webhook returned {(int)response.StatusCode} ({response.ReasonPhrase}): {responseBody}");
            }
        }

        var outputBlob = GetBlobClient(blobService.GetBlobContainerClient(OutputContainerName), OutputBlobName);
        var processed = await tester.EventuallyAsync(
            async cancellationToken => await TryDownloadTextAsync(outputBlob, cancellationToken),
            value => string.Equals(value, content, StringComparison.Ordinal),
            timeout: TimeSpan.FromSeconds(60));

        Assert.That(processed, Is.EqualTo(content));
        tester.Checkpoint("Azurite blob trigger completed.");
        tester.AssertLogContains("Middleware before BlobTrigger.");
    }

    /// <summary>
    /// Creates a blob client for Azurite that preserves the emulator's path-style account URL.
    /// </summary>
    /// <param name="container">The target blob container.</param>
    /// <param name="blobName">The blob name.</param>
    /// <returns>A client for the specified blob.</returns>
    private static BlobClient GetBlobClient(BlobContainerClient container, string blobName) =>
        new(new Uri($"{container.Uri.AbsoluteUri.TrimEnd('/')}/{blobName}"),
            new StorageSharedKeyCredential(AccountName, AccountKey),
            new BlobClientOptions(BlobClientOptions.ServiceVersion.V2021_12_02));

    /// <summary>
    /// Downloads a blob's text content, returning <see langword="null"/> while the blob does not yet exist.
    /// </summary>
    /// <param name="blob">The blob to download.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The blob content, or <see langword="null"/> when the service reports that the blob is missing.</returns>
    private static async Task<string?> TryDownloadTextAsync(BlobClient blob, CancellationToken cancellationToken)
    {
        try
        {
            var response = await blob.DownloadContentAsync(cancellationToken);
            return response.Value.Content.ToString();
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }
}
