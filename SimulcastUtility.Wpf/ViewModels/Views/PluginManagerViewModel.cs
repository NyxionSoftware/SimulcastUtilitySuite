using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimulcastUtility.Plugins.Interfaces;
using SimulcastUtility.Plugins.Models;
using SimulcastUtility.Wpf.ViewModels.Models;
using System.Collections.ObjectModel;

namespace SimulcastUtility.Wpf.ViewModels.Views
{
    public sealed class PluginManagerViewModel : ObservableObject, IDisposable
    {
        private readonly IPluginManager _pluginManager;
        private readonly ObservableCollection<PluginViewModel> _plugins = new();
        private bool _isImporting;
        private string? _importStatus = "Import a DLL, a group of DLLs, or a ZIP package.";
        private bool _importFailed;
        private bool _isConfirmationVisible;
        private bool _isRefreshing;
        private PluginViewModel? _pluginPendingDeletion;
        private PluginViewModel? _settingsPlugin;
        private bool _isSettingsVisible;
        private bool _isSavingSettings;

        public PluginManagerViewModel(IPluginManager pluginManager)
        {
            _pluginManager = pluginManager;
            Plugins = new ReadOnlyObservableCollection<PluginViewModel>(_plugins);
            BackCommand = new RelayCommand(() => CloseRequested?.Invoke(this, EventArgs.Empty));
            ImportPluginCommand = new RelayCommand(() => ImportRequested?.Invoke(this, EventArgs.Empty), () => !IsImporting);
            OpenPluginDirectoryCommand = new RelayCommand(() => OpenPluginDirectoryRequested?.Invoke(this, EventArgs.Empty));
            RefreshPluginsCommand = new AsyncRelayCommand(RefreshPluginsAsync, () => !IsRefreshing);
            DeletePluginCommand = new AsyncRelayCommand<PluginViewModel>(DeletePluginAsync);
            ConfirmDeleteCommand = new AsyncRelayCommand(ConfirmDeleteAsync);
            CancelConfirmationCommand = new RelayCommand(CancelConfirmation);
            OpenSettingsCommand = new AsyncRelayCommand<PluginViewModel>(OpenSettingsAsync);
            SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync, () => !IsSavingSettings);
            CloseSettingsCommand = new RelayCommand(CloseSettings);
            _pluginManager.PluginsChanged += PluginsChanged;
            SynchronizePlugins();
        }

        public ReadOnlyObservableCollection<PluginViewModel> Plugins { get; }

        public int PluginCount => Plugins.Count;

        public int EnabledPluginCount => Plugins.Count(plugin => plugin.IsEnabled);

        public System.Windows.Visibility EmptyStateVisibility => Plugins.Count == 0 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

        public System.Windows.Visibility PluginListVisibility => Plugins.Count > 0 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

        public IRelayCommand BackCommand { get; }

        public IRelayCommand ImportPluginCommand { get; }

        public IRelayCommand OpenPluginDirectoryCommand { get; }

        public IAsyncRelayCommand RefreshPluginsCommand { get; }

        public IAsyncRelayCommand<PluginViewModel> DeletePluginCommand { get; }

        public IAsyncRelayCommand ConfirmDeleteCommand { get; }

        public IRelayCommand CancelConfirmationCommand { get; }

        public IAsyncRelayCommand<PluginViewModel> OpenSettingsCommand { get; }

        public IAsyncRelayCommand SaveSettingsCommand { get; }

        public IRelayCommand CloseSettingsCommand { get; }

        public ObservableCollection<PluginSettingViewModel> Settings { get; } = new();

        public string SettingsTitle => $"{_settingsPlugin?.Name} Settings";

        public System.Windows.Visibility SettingsVisibility => IsSettingsVisible ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

        public bool IsSettingsVisible
        {
            get => _isSettingsVisible;
            private set
            {
                if (SetProperty(ref _isSettingsVisible, value))
                    OnPropertyChanged(nameof(SettingsVisibility));
            }
        }

        public bool IsSavingSettings
        {
            get => _isSavingSettings;
            private set
            {
                if (SetProperty(ref _isSavingSettings, value))
                    SaveSettingsCommand.NotifyCanExecuteChanged();
            }
        }

        public string PluginDirectory => _pluginManager.PluginDirectory;

        public bool IsRefreshing
        {
            get => _isRefreshing;
            private set
            {
                if (!SetProperty(ref _isRefreshing, value))
                    return;

                RefreshPluginsCommand.NotifyCanExecuteChanged();
            }
        }

        public bool IsConfirmationVisible
        {
            get => _isConfirmationVisible;
            private set
            {
                if (!SetProperty(ref _isConfirmationVisible, value))
                    return;

                OnPropertyChanged(nameof(ConfirmationVisibility));
            }
        }

        public System.Windows.Visibility ConfirmationVisibility => IsConfirmationVisible ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

        public string ConfirmationTitle => "Delete plugin?";

        public string ConfirmationMessage => $"Are you sure you want to permanently delete '{_pluginPendingDeletion?.Name}' and its files?";

        public bool IsImporting
        {
            get => _isImporting;
            private set
            {
                if (!SetProperty(ref _isImporting, value))
                    return;

                ImportPluginCommand.NotifyCanExecuteChanged();
            }
        }

        public string? ImportStatus
        {
            get => _importStatus;
            private set => SetProperty(ref _importStatus, value);
        }

        public bool ImportFailed
        {
            get => _importFailed;
            private set => SetProperty(ref _importFailed, value);
        }

        public event EventHandler? CloseRequested;

        public event EventHandler? ImportRequested;

        public event EventHandler? OpenPluginDirectoryRequested;

        public async Task ImportAsync(IReadOnlyList<string> sourcePaths)
        {
            IsImporting = true;
            ImportFailed = false;
            ImportStatus = "Importing plugin...";

            try
            {
                PluginImportResult result = await _pluginManager.ImportAsync(sourcePaths);
                ImportFailed = result.LoadedPluginCount == 0;
                ImportStatus = result.LoadedPluginCount > 0
                    ? $"Imported and loaded {result.LoadedPluginCount} plugin(s)."
                    : $"Imported {result.ImportedFileCount} file(s), but no plugin implementation was found.";
            }
            catch (Exception ex)
            {
                ImportFailed = true;
                ImportStatus = $"Plugin import failed: {ex.Message}";
            }
            finally
            {
                IsImporting = false;
            }
        }

        public void Dispose()
        {
            _pluginManager.PluginsChanged -= PluginsChanged;
        }

        private void PluginsChanged(object? sender, EventArgs e)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(SynchronizePlugins);
        }

        private void SynchronizePlugins()
        {
            _plugins.Clear();

            foreach (LoadedPlugin plugin in _pluginManager.Plugins)
                _plugins.Add(new PluginViewModel(_pluginManager, plugin));

            OnPropertyChanged(nameof(PluginCount));
            OnPropertyChanged(nameof(EnabledPluginCount));
            OnPropertyChanged(nameof(EmptyStateVisibility));
            OnPropertyChanged(nameof(PluginListVisibility));
        }

        private Task DeletePluginAsync(PluginViewModel? plugin)
        {
            if (plugin is null)
                return Task.CompletedTask;

            _pluginPendingDeletion = plugin;
            OnPropertyChanged(nameof(ConfirmationMessage));
            IsConfirmationVisible = true;
            return Task.CompletedTask;
        }

        private async Task RefreshPluginsAsync()
        {
            IsRefreshing = true;
            ImportFailed = false;
            ImportStatus = "Refreshing installed plugins...";

            try
            {
                int loadedPluginCount = await _pluginManager.RefreshAsync();
                ImportStatus = loadedPluginCount > 0
                    ? $"Loaded {loadedPluginCount} newly discovered plugin(s)."
                    : "Installed plugins are up to date.";
            }
            catch (Exception ex)
            {
                ImportFailed = true;
                ImportStatus = $"Could not refresh plugins: {ex.Message}";
            }
            finally
            {
                IsRefreshing = false;
            }
        }

        private async Task ConfirmDeleteAsync()
        {
            PluginViewModel? plugin = _pluginPendingDeletion;
            CancelConfirmation();

            if (plugin is null)
                return;

            try
            {
                await _pluginManager.DeleteAsync(plugin.Identifier);
                ImportFailed = false;
                ImportStatus = $"Deleted '{plugin.Name}'.";
            }
            catch (Exception ex)
            {
                ImportFailed = true;
                ImportStatus = $"Could not delete '{plugin.Name}': {ex.Message}";
            }
        }

        private void CancelConfirmation()
        {
            IsConfirmationVisible = false;
            _pluginPendingDeletion = null;
        }

        private async Task OpenSettingsAsync(PluginViewModel? plugin)
        {
            if (plugin is null)
                return;

            _settingsPlugin = plugin;
            Settings.Clear();

            try
            {
                foreach (PluginSettingDescriptor descriptor in await _pluginManager.GetSettingsAsync(plugin.Identifier))
                    Settings.Add(new PluginSettingViewModel(descriptor));
            }
            catch (Exception ex)
            {
                _settingsPlugin = null;
                ImportFailed = true;
                ImportStatus = $"Could not load settings for '{plugin.Name}': {ex.Message}";
                return;
            }

            OnPropertyChanged(nameof(SettingsTitle));
            IsSettingsVisible = true;
        }

        private async Task SaveSettingsAsync()
        {
            if (_settingsPlugin is null)
                return;

            IsSavingSettings = true;

            try
            {
                foreach (PluginSettingViewModel setting in Settings)
                    await _pluginManager.SetSettingAsync(_settingsPlugin.Identifier, setting.Key, setting.CreateValue());

                ImportFailed = false;
                ImportStatus = $"Saved settings for '{_settingsPlugin.Name}'.";
                CloseSettings();
            }
            catch (Exception ex)
            {
                ImportFailed = true;
                ImportStatus = $"Could not save plugin settings: {ex.Message}";
            }
            finally
            {
                IsSavingSettings = false;
            }
        }

        private void CloseSettings()
        {
            IsSettingsVisible = false;
            _settingsPlugin = null;
            Settings.Clear();
        }
    }
}
