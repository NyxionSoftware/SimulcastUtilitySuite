using SimulcastUtility.Application.Interfaces;
using SimulcastUtility.Plugins.Interfaces;

namespace SimulcastUtility.Plugins.Services
{
    internal sealed class PluginContext : IPluginContext
    {
        public PluginContext(string installationDirectory, IReceiverRepository receiverRepository, IReceiverManager receiverManager, IReceiverCommandManager receiverCommandManager, IPluginApplicationDispatcher applicationDispatcher, IPluginThemeManager themeManager, IPluginUiManager uiManager, IPluginDataStore dataStore)
        {
            InstallationDirectory = installationDirectory;
            ReceiverRepository = receiverRepository;
            ReceiverManager = receiverManager;
            ReceiverCommandManager = receiverCommandManager;
            ApplicationDispatcher = applicationDispatcher;
            ThemeManager = themeManager;
            UiManager = uiManager;
            DataStore = dataStore;
        }

        public string InstallationDirectory { get; }

        public IReceiverRepository ReceiverRepository { get; }

        public IReceiverManager ReceiverManager { get; }

        public IReceiverCommandManager ReceiverCommandManager { get; }

        public IPluginApplicationDispatcher ApplicationDispatcher { get; }

        public IPluginThemeManager ThemeManager { get; }

        public IPluginUiManager UiManager { get; }

        public IPluginDataStore DataStore { get; }
    }
}
