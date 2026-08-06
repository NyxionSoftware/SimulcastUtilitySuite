using SimulcastUtility.Application.Interfaces;
using Microsoft.Extensions.Logging;

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

        IPluginBrandingManager BrandingManager { get; }

        IPluginUiManager UiManager { get; }

        IPluginDataStore DataStore { get; }

        ILogger Logger { get; }
    }
}
