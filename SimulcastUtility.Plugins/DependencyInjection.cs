using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SimulcastUtility.Plugins.Interfaces;
using SimulcastUtility.Plugins.Options;
using SimulcastUtility.Plugins.Services;

namespace SimulcastUtility.Plugins
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddSimulcastPlugins(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<PluginOptions>(configuration.GetSection(PluginOptions.SectionName));
            services.AddSingleton<IPluginApplicationDispatcher, PluginApplicationDispatcher>();
            services.AddSingleton<IPluginThemeManager, NullPluginThemeManager>();
            services.AddSingleton<IPluginUiManager, NullPluginUiManager>();
            services.AddSingleton<IPluginManager, PluginManager>();
            return services;
        }
    }
}
