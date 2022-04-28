using System;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

[assembly: FunctionsStartup(typeof(UnitTestEx.Function.Startup))]

namespace UnitTestEx.Function
{
    public class Startup : FunctionsStartup
    {
        public override void ConfigureAppConfiguration(IFunctionsConfigurationBuilder builder)
        {
            if (builder.ConfigurationBuilder.Build()["SpecialKey"] != "VerySpecialValue")
                throw new InvalidOperationException("The people do not feel very special!");
        }

        public override void Configure(IFunctionsHostBuilder builder)
        {
            builder.Services.AddHttpClient("XXX", hc => hc.BaseAddress = new System.Uri("https://somesys"));
        }
    }
}