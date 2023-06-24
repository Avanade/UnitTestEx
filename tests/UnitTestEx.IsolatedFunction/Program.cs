using CoreEx.Hosting;
using Microsoft.Extensions.Hosting;
using UnitTestEx.IsolatedFunction;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureHostStartup<Startup>()
    .Build();

host.Run();
