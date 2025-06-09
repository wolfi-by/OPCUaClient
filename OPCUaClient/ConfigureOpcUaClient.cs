using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Runtime;
using System.Text;
using System.Threading.Tasks;

namespace OPCUaClient
{
    public static class ConfigureOpcUaClient
    {
        public static void ConfigureOpcuaClient(this WebApplicationBuilder builder)
        {
            //builder.Services.Configure<MyOpcUaClientOptions>(options => builder.Configuration.GetSection(nameof(MyOpcUaClientOptions)).Bind(options));
            builder.Services.AddOptions<MyOpcUaClientOptions>()
                //.Bind(builder.Configuration.GetSection(nameof(MyOpcUaClientOptions)))
                .BindConfiguration(nameof(MyOpcUaClientOptions))
                .ValidateDataAnnotations()
                .ValidateOnStart();
            builder.Services.AddScoped<MyOpcUaClient>();
        }
    }
}
