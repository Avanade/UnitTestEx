using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);
builder.ConfigureFunctionsWebApplication();
builder.UseMiddleware<UnitTestEx.IsolatedFunction.TestMiddleware>();
builder.Services.AddHttpClient("XXX", client => client.BaseAddress = new Uri("https://somesys"));
builder.Configuration.AddInMemoryCollection([new KeyValuePair<string, string?>("SpecialKey", "VerySpecialValue")]);
builder.Build().Run();
