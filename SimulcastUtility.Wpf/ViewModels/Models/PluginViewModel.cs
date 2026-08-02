using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimulcastUtility.Plugins.Interfaces;
using SimulcastUtility.Plugins.Models;

namespace SimulcastUtility.Wpf.ViewModels.Models
{
    public sealed class PluginViewModel : ObservableObject
    {
        private readonly IPluginManager _pluginManager;
        private readonly LoadedPlugin _plugin;
        private bool _isChangingState;

        public PluginViewModel(IPluginManager pluginManager, LoadedPlugin plugin)
        {
            _pluginManager = pluginManager;
            _plugin = plugin;
            ToggleEnabledCommand = new AsyncRelayCommand(ToggleEnabledAsync, () => !IsChangingState);
        }

        public Guid Identifier => _plugin.Info.PluginIdentifier;

        public string Name => _plugin.Info.Name;

        public string Description => _plugin.Info.Description;

        public string Author => _plugin.Info.Author;

        public string Version => _plugin.Info.Version.ToString();

        public bool IsEnabled => _plugin.IsEnabled;

        public bool HasSettings => _plugin.HasSettings;

        public System.Windows.Visibility SettingsVisibility => HasSettings ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

        public string StatusText => IsEnabled ? "Enabled" : "Disabled";

        public string? Error => _plugin.Error;

        public bool IsChangingState
        {
            get => _isChangingState;
            private set
            {
                if (!SetProperty(ref _isChangingState, value))
                    return;

                ToggleEnabledCommand.NotifyCanExecuteChanged();
            }
        }

        public IAsyncRelayCommand ToggleEnabledCommand { get; }

        public void Refresh()
        {
            OnPropertyChanged(nameof(IsEnabled));
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(Error));
        }

        private async Task ToggleEnabledAsync()
        {
            IsChangingState = true;

            try
            {
                await _pluginManager.SetEnabledAsync(Identifier, !IsEnabled);
            }
            catch
            {
                Refresh();
            }
            finally
            {
                IsChangingState = false;
                Refresh();
            }
        }
    }
}
