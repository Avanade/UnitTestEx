using Aspire.Hosting.ApplicationModel;

var builder = DistributedApplication.CreateBuilder(args);
var azurite = builder.AddContainer("durable-storage", "mcr.microsoft.com/azure-storage/azurite", "3.35.0")
    .WithArgs("azurite", "-l", "/data", "--blobHost", "0.0.0.0", "--queueHost", "0.0.0.0", "--tableHost", "0.0.0.0")
    .WithEndpoint(port: 11000, targetPort: 10000, scheme: "http", name: "blob", isProxied: false)
    .WithEndpoint(port: 11001, targetPort: 10001, scheme: "http", name: "queue", isProxied: false)
    .WithEndpoint(port: 11002, targetPort: 10002, scheme: "http", name: "table", isProxied: false);

// This is Azurite's public development credential, not an Azure account key.
const string accountKey = "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==";
var storage = $"DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey={accountKey};BlobEndpoint=http://127.0.0.1:11000/devstoreaccount1;QueueEndpoint=http://127.0.0.1:11001/devstoreaccount1;TableEndpoint=http://127.0.0.1:11002/devstoreaccount1";
var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name ?? throw new InvalidOperationException("Could not determine the AppHost build configuration.");
var functionsDirectory = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "UnitTestEx.DurableFunction", "bin", configuration, "net10.0"));
var functions = OperatingSystem.IsWindows()
    ? builder.AddExecutable("durable-functions", "cmd.exe", functionsDirectory, "/c", "func", "start", "--dotnet-isolated", "--no-build", "--verbose", "--port", "7073")
    : builder.AddExecutable("durable-functions", "func", functionsDirectory, "start", "--dotnet-isolated", "--no-build", "--verbose", "--port", "7073");

functions
    .WithHttpEndpoint(port: 7073, targetPort: 7073, name: "http", isProxied: false)
    .WithHttpHealthCheck("/admin/host/status", endpointName: "http")
    .WithEnvironment("FUNCTIONS_WORKER_RUNTIME", "dotnet-isolated")
    .WithEnvironment("AzureWebJobsSecretStorageType", "files")
    .WithEnvironment("AzureWebJobsStorage", storage)
    .WithEnvironment("AzureFunctionsJobHost__extensions__durableTask__hubName", $"UnitTestEx{Guid.NewGuid():N}")
    .WithEnvironment("AzureFunctionsJobHost__extensions__durableTask__storageProvider__type", "azureStorage")
    .WithEnvironment("AzureFunctionsJobHost__extensions__durableTask__storageProvider__maxQueuePollingInterval", "00:00:01")
    .WaitFor(azurite);

builder.Build().Run();
