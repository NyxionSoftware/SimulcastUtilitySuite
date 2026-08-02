using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SimulcastUtility.Application.Interfaces;
using SimulcastUtility.Infrastructure.Options;
using SimulcastUtility.Infrastructure.Repository;

namespace SimulcastUtility.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddSimulcastInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<JsonReceiverRepositoryOptions>(
                configuration.GetSection(JsonReceiverRepositoryOptions.SectionName));

            services.AddSingleton<IReceiverRepository, JsonReceiverRepository>();

            return services;
        }
    }
}
