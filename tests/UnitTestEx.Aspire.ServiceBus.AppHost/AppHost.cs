using Aspire.Hosting.ApplicationModel;

var builder = DistributedApplication.CreateBuilder(args);
const string sqlPassword = "UnitTestEx_Emulator!2026";
const string serviceBusConnectionString = "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";

var sql = builder.AddContainer("sqledge", "mcr.microsoft.com/azure-sql-edge", "latest")
    .WithEnvironment("ACCEPT_EULA", "Y")
    .WithEnvironment("MSSQL_SA_PASSWORD", sqlPassword);

var configFile = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "ServiceBus", "Config.json"));
var serviceBus = builder.AddContainer("servicebus-emulator", "mcr.microsoft.com/azure-messaging/servicebus-emulator", "latest")
    .WithEnvironment("ACCEPT_EULA", "Y")
    .WithEnvironment("SQL_SERVER", "sqledge")
    .WithEnvironment("MSSQL_SA_PASSWORD", sqlPassword)
    .WithEnvironment("EMULATOR_HTTP_PORT", "5300")
    .WithEnvironment("SQL_WAIT_INTERVAL", "30")
    .WithBindMount(configFile, "/ServiceBus_Emulator/ConfigFiles/Config.json")
    .WithEndpoint(port: 5672, targetPort: 5672, scheme: "tcp", name: "amqp", isProxied: false)
    .WithEndpoint(port: 5300, targetPort: 5300, scheme: "http", name: "management", isProxied: false)
    .WithHttpHealthCheck("/health", endpointName: "management")
    .WaitFor(sql);

var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name
    ?? throw new InvalidOperationException("Could not determine the AppHost build configuration.");
var functionDirectory = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "UnitTestEx.Aspire.ServiceBus.Function", "bin", configuration, "net8.0"));
var functions = OperatingSystem.IsWindows()
    ? builder.AddExecutable("servicebus-functions", "cmd.exe", functionDirectory, "/c", "func", "start", "--dotnet-isolated", "--no-build", "--verbose", "--port", "7072")
    : builder.AddExecutable("servicebus-functions", "func", functionDirectory, "start", "--dotnet-isolated", "--no-build", "--verbose", "--port", "7072");

functions
    .WithHttpEndpoint(port: 7072, targetPort: 7072, name: "http", isProxied: false)
    .WithHttpHealthCheck("/admin/host/status", endpointName: "http")
    .WithEnvironment("FUNCTIONS_WORKER_RUNTIME", "dotnet-isolated")
    .WithEnvironment("AzureWebJobsSecretStorageType", "files")
    .WithEnvironment("AzureWebJobsStorage", "UseDevelopmentStorage=true")
    .WithEnvironment("ServiceBusConnectionString", serviceBusConnectionString)
    .WaitFor(serviceBus);

builder.Build().Run();
