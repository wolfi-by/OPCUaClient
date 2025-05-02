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
        /// <summary>
        ///   This method is used to register the OPC UA client services in the dependency injection container.
        /// </summary>
        /// <param name="services"></param>
        public static void ALaMeR_OpcUa(this IServiceCollection services)
        {
            services.AddSingleton<OpcUaClientCollection>();
            services.AddSingleton<OpcUaSessionBuilder>();
            services.AddSingleton<OpcUaComplexTypeSystemSessionBuilder>();
        }
    }
}
