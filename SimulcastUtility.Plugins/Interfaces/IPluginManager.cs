using SimulcastUtility.Plugins.Models;

namespace SimulcastUtility.Plugins.Interfaces
{
    public interface IPluginManager
    {
        IReadOnlyList<LoadedPlugin> Plugins { get; }

        string PluginDirectory { get; }

        event EventHandler? PluginsChanged;

        Task LoadAsync(IReadOnlyList<string> applicationArguments, CancellationToken cancellationToken = default);

        Task<int> RefreshAsync(CancellationToken cancellationToken = default);

        Task ReloadAsync(CancellationToken cancellationToken = default);

        Task<PluginImportResult> ImportAsync(IReadOnlyList<string> sourcePaths, CancellationToken cancellationToken = default);

        Task DeleteAsync(Guid pluginIdentifier, CancellationToken cancellationToken = default);

        Task SetEnabledAsync(Guid pluginIdentifier, bool isEnabled, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<PluginSettingDescriptor>> GetSettingsAsync(Guid pluginIdentifier, CancellationToken cancellationToken = default);

        Task SetSettingAsync(Guid pluginIdentifier, string settingKey, System.Text.Json.JsonElement value, CancellationToken cancellationToken = default);

        Task HandleApplicationArgumentsAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken = default);
    }
}
