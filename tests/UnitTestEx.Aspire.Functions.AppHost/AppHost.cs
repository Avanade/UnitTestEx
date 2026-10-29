using Aspire.Hosting.ApplicationModel;

var builder = DistributedApplication.CreateBuilder(args);

var azurite = builder.AddContainer("azurite", "mcr.microsoft.com/azure-storage/azurite", "3.35.0")
    .WithArgs("azurite", "-l", "/data", "--blobHost", "0.0.0.0", "--queueHost", "0.0.0.0", "--tableHost", "0.0.0.0")
    .WithEndpoint(port: 10000, targetPort: 10000, scheme: "http", name: "blob", isProxied: false)
    .WithEndpoint(port: 10001, targetPort: 10001, scheme: "http", name: "queue", isProxied: false)
    .WithEndpoint(port: 10002, targetPort: 10002, scheme: "http", name: "table", isProxied: false);

var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name
    ?? throw new InvalidOperationException("Could not determine the AppHost build configuration.");
var functionsDirectory = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "UnitTestEx.IsolatedFunction", "bin", configuration, "net8.0"));
var functions = OperatingSystem.IsWindows()
    ? builder.AddExecutable("functions", "cmd.exe", functionsDirectory, "/c", "func", "start", "--dotnet-isolated", "--no-build", "--verbose", "--port", "7071")
    : builder.AddExecutable("functions", "func", functionsDirectory, "start", "--dotnet-isolated", "--no-build", "--verbose", "--port", "7071");

functions
    .WithHttpEndpoint(port: 7071, targetPort: 7071, name: "http", isProxied: false)
    .WithHttpHealthCheck("/admin/host/status", endpointName: "http")
    .WithEnvironment("FUNCTIONS_WORKER_RUNTIME", "dotnet-isolated")
    .WithEnvironment("AzureWebJobsSecretStorageType", "files")
    .WithEnvironment("AzureWebJobsStorage", "UseDevelopmentStorage=true")
    .WithEnvironment("Run4QueueName", "unittestex");

builder.Build().Run();
