using CoreEx.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace UnitTestEx.IsolatedFunction
{
    public class Startup : HostStartup
    {
        public override void ConfigureServices(IServiceCollection services)
        {
            services.AddHttpClient("XXX", hc => hc.BaseAddress = new System.Uri("https://somesys"));
            base.ConfigureServices(services);
        }
    }
}