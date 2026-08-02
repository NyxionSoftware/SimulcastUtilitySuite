using SimulcastUtility.Plugins.Models;

namespace SimulcastUtility.Plugins.Interfaces
{
    public interface IPluginSettingsProvider
    {
        object Settings { get; }

        IReadOnlyList<PluginSettingOption> GetSettingOptions(string settingKey);

        Task OnSettingChangedAsync(string settingKey, CancellationToken cancellationToken = default);
    }
}
