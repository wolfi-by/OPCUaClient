using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OPCUaClient
{
    public static class DependencyInjection
    {
        public static void ALaMeR_OpcUa(this IServiceCollection services)
        {
            services.AddSingleton<OpcUaClientCollection>();
            services.AddSingleton<OpcUaSessionBuilder>();
            services.AddSingleton<OpcUaComplexTypeSystemSessionBuilder>();
        }
    }
}
