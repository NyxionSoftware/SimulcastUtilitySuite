using Microsoft.Extensions.DependencyInjection;
using SimulcastUtility.Application.Interfaces;
using SimulcastUtility.Application.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace SimulcastUtility.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddSimulcastApplication(this IServiceCollection services)
        {
            services.AddSingleton<ReceiverManager>();

            services.AddSingleton<IReceiverManager>(serviceProvider =>
                serviceProvider.GetRequiredService<ReceiverManager>());

            services.AddSingleton<IReceiverManagerInitializer>(serviceProvider =>
                serviceProvider.GetRequiredService<ReceiverManager>());

            services.AddSingleton<IReceiverCommandManager, ReceiverCommandManager>();

            return services;
        }
    }
}
