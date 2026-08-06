using System.Windows;

namespace SimulcastUtility.Plugins.Interfaces
{
    public interface IPluginSettingPreviewProvider
    {
        Task<FrameworkElement?> CreateSettingPreviewAsync(string settingKey, string selectedValue, CancellationToken cancellationToken = default);
    }
}
