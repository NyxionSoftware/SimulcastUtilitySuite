using Microsoft.Extensions.DependencyInjection;
using SimulcastUtility.Wpf.ViewModels.Views;
using SimulcastUtility.Wpf.Views;
using SimulcastUtility.Plugins.Interfaces;
using SimulcastUtility.Wpf.Services;

namespace SimulcastUtility.Wpf
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddSimulcastWpf(this IServiceCollection services)
        {
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<MainView>();
            services.AddSingleton<ApplicationOverlayViewModel>();
            services.AddSingleton<ApplicationNavigationService>();
            services.AddTransient<ApplicationSettingsViewModel>();
            services.AddTransient<ApplicationSettingsView>();
            services.AddSingleton<IPluginThemeManager, WpfPluginThemeManager>();
            services.AddSingleton<IPluginUiManager, WpfPluginUiManager>();

            return services;
        }
    }
}
