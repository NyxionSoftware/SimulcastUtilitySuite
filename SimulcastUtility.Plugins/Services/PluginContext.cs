using SimulcastUtility.Application.Interfaces;
using SimulcastUtility.Plugins.Interfaces;
using Microsoft.Extensions.Logging;

namespace SimulcastUtility.Plugins.Services
{
    internal sealed class PluginContext : IPluginContext
    {
        public PluginContext(string installationDirectory, IReceiverRepository receiverRepository, IReceiverManager receiverManager, IReceiverCommandManager receiverCommandManager, IPluginApplicationDispatcher applicationDispatcher, IPluginThemeManager themeManager, IPluginBrandingManager brandingManager, IPluginUiManager uiManager, IPluginDataStore dataStore, ILogger logger)
        {
            InstallationDirectory = installationDirectory;
            ReceiverRepository = receiverRepository;
            ReceiverManager = receiverManager;
            ReceiverCommandManager = receiverCommandManager;
            ApplicationDispatcher = applicationDispatcher;
            ThemeManager = themeManager;
            BrandingManager = brandingManager;
            UiManager = uiManager;
            DataStore = dataStore;
            Logger = logger;
        }

        public string InstallationDirectory { get; }

        public IReceiverRepository ReceiverRepository { get; }

        public IReceiverManager ReceiverManager { get; }

        public IReceiverCommandManager ReceiverCommandManager { get; }

        public IPluginApplicationDispatcher ApplicationDispatcher { get; }

        public IPluginThemeManager ThemeManager { get; }

        public IPluginBrandingManager BrandingManager { get; }

        public IPluginUiManager UiManager { get; }

        public IPluginDataStore DataStore { get; }

        public ILogger Logger { get; }
    }
}
