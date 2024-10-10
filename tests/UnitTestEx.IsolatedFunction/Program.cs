using Microsoft.Extensions.Hosting;
using UnitTestEx.IsolatedFunction;

var startup = new Startup();
var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((hbc, sc) => startup.ConfigureServices(sc))
    .Build();

host.Run();
