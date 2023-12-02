using Microsoft.Extensions.DependencyInjection;

namespace UnitTestEx.IsolatedFunction
{
    public class Startup 
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddHttpClient("XXX", hc => hc.BaseAddress = new System.Uri("https://somesys"));
        }
    }
}