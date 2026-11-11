using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using UnitTestEx.DurableFunction;

var builder = FunctionsApplication.CreateBuilder(args);
builder.Services.AddSingleton<IWorkflowJournal, WorkflowJournal>();
builder.Build().Run();
