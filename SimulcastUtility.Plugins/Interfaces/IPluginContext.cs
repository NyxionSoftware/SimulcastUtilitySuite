using SimulcastUtility.Application.Interfaces;

namespace SimulcastUtility.Plugins.Interfaces
{
    public interface IPluginContext
    {
        string InstallationDirectory { get; }

        IReceiverRepository ReceiverRepository { get; }

        IReceiverManager ReceiverManager { get; }

        IReceiverCommandManager ReceiverCommandManager { get; }

        IPluginApplicationDispatcher ApplicationDispatcher { get; }

        IPluginThemeManager ThemeManager { get; }

        IPluginUiManager UiManager { get; }

        IPluginDataStore DataStore { get; }
    }
}
