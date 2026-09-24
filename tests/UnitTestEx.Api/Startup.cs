using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace UnitTestEx.Api
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers().AddNewtonsoftJson();

            // The base address defaults to a bogus, unreachable address as Tier 1 (WebApplicationFactory) tests always replace the whole IHttpClientFactory via MockHttpClientFactory, so the
            // configured value is never actually dialled. It is overridable via configuration (e.g. an Aspire AppHost-supplied "XXX__BaseUrl" environment variable) so that Tier 2 (Aspire
            // multi-host) tests can point this client at a real resource - e.g. a WireMock.Net.Aspire resource standing in for an external dependency - since there is no DI container to
            // reach into and replace across process boundaries.
            services.AddHttpClient("XXX", hc => hc.BaseAddress = new System.Uri(Configuration["XXX:BaseUrl"] ?? "https://somesys"))
                .AddHttpMessageHandler(_ => new MessageProcessingHandler())
                .ConfigureHttpClient(hc => hc.DefaultRequestVersion = new Version(1, 2));
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }

        public class MessageProcessingHandler : DelegatingHandler
        {
            public static bool WasExecuted { get; set;}

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                WasExecuted = true;
                return base.SendAsync(request, cancellationToken);
            }
        }
    }
}