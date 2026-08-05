using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Options;
using SimulcastUtility.Configuration.Models;
using SimulcastUtility.Configuration;
using SimulcastUtility.Infrastructure.Options;
using SimulcastUtility.Plugins.Options;
using SimulcastUtility.Plugins.Interfaces;
using SimulcastUtility.Application.Interfaces;
using SimulcastUtility.Wpf.Options;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Principal;

namespace SimulcastUtility.Wpf.ViewModels.Views
{
    public sealed class ApplicationSettingsViewModel : ObservableObject
    {
        private readonly string _userSettingsFilePath;
        private readonly string _workstationSettingsFilePath;
        private readonly string _logDirectory;
        private readonly IOptionsMonitor<PluginOptions> _pluginOptions;
        private readonly IOptionsMonitor<JsonReceiverRepositoryOptions> _receiverOptions;
        private readonly IReceiverManager _receiverManager;
        private readonly IPluginManager _pluginManager;
        private string _settingsFilePath;
        private string _notificationDurationSeconds;
        private string _logRetentionDays;
        private string _logLevel;
        private string _pluginDirectory;
        private string _pluginDataDirectory;
        private string _receiverDirectory;
        private string _receiverFileName;
        private string _statusMessage = string.Empty;
        private bool _isSaving;
        private bool _hasError;
        private bool _hasChanges;
        private string _selectedConfigurationScope = "User";
        private bool _suppressChangeTracking;

        public ObservableCollection<string> LogLevels { get; } = new() { "Verbose", "Debug", "Information", "Warning", "Error", "Fatal" };

        public ObservableCollection<string> ConfigurationScopes { get; }

        public bool IsWorkstationScopeAvailable { get; }

        public string SelectedConfigurationScope
        {
            get => _selectedConfigurationScope;
            set
            {
                if (!SetProperty(ref _selectedConfigurationScope, value))
                    return;

                _settingsFilePath = string.Equals(value, "Workstation", StringComparison.Ordinal) && IsWorkstationScopeAvailable ? _workstationSettingsFilePath : _userSettingsFilePath;
                LoadSettingsForSelectedScope();
            }
        }

        public string NotificationDurationSeconds
        {
            get => _notificationDurationSeconds;
            set => SetSetting(ref _notificationDurationSeconds, value);
        }

        public string LogRetentionDays
        {
            get => _logRetentionDays;
            set => SetSetting(ref _logRetentionDays, value);
        }

        public string LogLevel
        {
            get => _logLevel;
            set => SetSetting(ref _logLevel, value);
        }

        public string PluginDirectory
        {
            get => _pluginDirectory;
            set => SetSetting(ref _pluginDirectory, value);
        }

        public string PluginDataDirectory
        {
            get => _pluginDataDirectory;
            set => SetSetting(ref _pluginDataDirectory, value);
        }

        public string ReceiverDirectory
        {
            get => _receiverDirectory;
            set => SetSetting(ref _receiverDirectory, value);
        }

        public string ReceiverFileName
        {
            get => _receiverFileName;
            set => SetSetting(ref _receiverFileName, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            private set
            {
                if (SetProperty(ref _statusMessage, value))
                {
                    OnPropertyChanged(nameof(StatusVisibility));
                    OnPropertyChanged(nameof(ErrorStatusVisibility));
                    OnPropertyChanged(nameof(SuccessStatusVisibility));
                }
            }
        }

        public bool HasError
        {
            get => _hasError;
            private set
            {
                if (!SetProperty(ref _hasError, value))
                    return;

                OnPropertyChanged(nameof(ErrorStatusVisibility));
                OnPropertyChanged(nameof(SuccessStatusVisibility));
            }
        }

        public System.Windows.Visibility StatusVisibility => string.IsNullOrWhiteSpace(StatusMessage) ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;

        public System.Windows.Visibility ErrorStatusVisibility => StatusVisibility == System.Windows.Visibility.Visible && HasError ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

        public System.Windows.Visibility SuccessStatusVisibility => StatusVisibility == System.Windows.Visibility.Visible && !HasError ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

        public bool IsSaving
        {
            get => _isSaving;
            private set
            {
                if (!SetProperty(ref _isSaving, value))
                    return;

                SaveCommand.NotifyCanExecuteChanged();
            }
        }

        public bool HasChanges
        {
            get => _hasChanges;
            private set
            {
                if (!SetProperty(ref _hasChanges, value))
                    return;

                SaveCommand?.NotifyCanExecuteChanged();
            }
        }

        public IAsyncRelayCommand SaveCommand { get; }

        public IRelayCommand CloseCommand { get; }

        public event EventHandler? CloseRequested;

        public ApplicationSettingsViewModel(IOptionsMonitor<NotificationOptions> notificationOptions, IOptionsMonitor<LoggingOptions> loggingOptions, IOptionsMonitor<PluginOptions> pluginOptions, IOptionsMonitor<JsonReceiverRepositoryOptions> receiverOptions, IReceiverManager receiverManager, IPluginManager pluginManager)
        {
            _userSettingsFilePath = ApplicationConfigurationPaths.GetUserSettingsFilePath();
            _workstationSettingsFilePath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            _settingsFilePath = _userSettingsFilePath;
            _logDirectory = loggingOptions.CurrentValue.Directory;
            _pluginOptions = pluginOptions;
            _receiverOptions = receiverOptions;
            _receiverManager = receiverManager;
            _pluginManager = pluginManager;
            IsWorkstationScopeAvailable = IsRunningAsAdministrator();
            ConfigurationScopes = new ObservableCollection<string>(IsWorkstationScopeAvailable ? new[] { "User", "Workstation" } : new[] { "User" });
            _notificationDurationSeconds = notificationOptions.CurrentValue.DisplayDurationSeconds.ToString();
            _logRetentionDays = loggingOptions.CurrentValue.RetentionDays.ToString();
            _pluginDirectory = pluginOptions.CurrentValue.Directory;
            _pluginDataDirectory = pluginOptions.CurrentValue.DataDirectory;
            _receiverDirectory = receiverOptions.CurrentValue.Directory;
            _receiverFileName = receiverOptions.CurrentValue.FileName;
            _logLevel = ReadLogLevel(_userSettingsFilePath, ReadLogLevel(_workstationSettingsFilePath, "Information"));
            SaveCommand = new AsyncRelayCommand(SaveAsync, () => !IsSaving && HasChanges);
            CloseCommand = new RelayCommand(() => CloseRequested?.Invoke(this, EventArgs.Empty));
        }

        private async Task SaveAsync()
        {
            if (!TryValidate(out int notificationDuration, out int retentionDays, out string error))
            {
                HasError = true;
                StatusMessage = error;
                return;
            }

            IsSaving = true;
            string previousReceiverPath = _receiverOptions.CurrentValue.GetFullPath();
            string previousPluginDirectory = _pluginOptions.CurrentValue.Directory;
            string previousPluginDataDirectory = _pluginOptions.CurrentValue.DataDirectory;

            try
            {
                JsonObject root = await ReadSettingsAsync();
                SetSectionValue(root, NotificationOptions.SectionName, nameof(NotificationOptions.DisplayDurationSeconds), notificationDuration);
                SetSectionValue(root, LoggingOptions.SectionName, nameof(LoggingOptions.RetentionDays), retentionDays);
                SetSectionValue(root, PluginOptions.SectionName, nameof(PluginOptions.Directory), Path.GetFullPath(PluginDirectory.Trim()));
                SetSectionValue(root, PluginOptions.SectionName, nameof(PluginOptions.DataDirectory), Path.GetFullPath(PluginDataDirectory.Trim()));
                SetSectionValue(root, JsonReceiverRepositoryOptions.SectionName, nameof(JsonReceiverRepositoryOptions.Directory), Path.GetFullPath(ReceiverDirectory.Trim()));
                SetSectionValue(root, JsonReceiverRepositoryOptions.SectionName, nameof(JsonReceiverRepositoryOptions.FileName), ReceiverFileName.Trim());
                JsonObject serilog = GetOrCreateObject(root, "Serilog");
                JsonObject minimumLevel = GetOrCreateObject(serilog, "MinimumLevel");
                minimumLevel["Default"] = LogLevel;

                string json = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
                Directory.CreateDirectory(Path.GetDirectoryName(_settingsFilePath)!);
                await File.WriteAllTextAsync(_settingsFilePath, json);
                await Task.Delay(500);
                string currentReceiverPath = _receiverOptions.CurrentValue.GetFullPath();
                string currentPluginDirectory = _pluginOptions.CurrentValue.Directory;
                string currentPluginDataDirectory = _pluginOptions.CurrentValue.DataDirectory;

                if (!string.Equals(previousReceiverPath, currentReceiverPath, StringComparison.OrdinalIgnoreCase))
                    await _receiverManager.ReloadAsync();

                if (!string.Equals(previousPluginDirectory, currentPluginDirectory, StringComparison.OrdinalIgnoreCase) || !string.Equals(previousPluginDataDirectory, currentPluginDataDirectory, StringComparison.OrdinalIgnoreCase))
                    await _pluginManager.ReloadAsync();

                DeleteExpiredLogs(Path.GetFullPath(_logDirectory), retentionDays);
                HasChanges = false;
                HasError = false;
                StatusMessage = $"{SelectedConfigurationScope} settings saved. Runtime settings are active; log level will update after restart.";
            }
            catch (Exception ex)
            {
                HasError = true;
                StatusMessage = $"Settings could not be saved: {ex.Message}";
            }
            finally
            {
                IsSaving = false;
            }
        }

        private bool TryValidate(out int notificationDuration, out int retentionDays, out string error)
        {
            if (!int.TryParse(NotificationDurationSeconds, out notificationDuration) || notificationDuration is < 1 or > 300)
            {
                retentionDays = 0;
                error = "Notification duration must be between 1 and 300 seconds.";
                return false;
            }

            if (!int.TryParse(LogRetentionDays, out retentionDays) || retentionDays is < 1 or > 3650)
            {
                error = "Log retention must be between 1 and 3,650 days.";
                return false;
            }

            if (!LogLevels.Contains(LogLevel))
            {
                error = "Select a valid log level.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(PluginDirectory) || string.IsNullOrWhiteSpace(PluginDataDirectory) || string.IsNullOrWhiteSpace(ReceiverDirectory))
            {
                error = "Storage directories cannot be empty.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(ReceiverFileName) || ReceiverFileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || Path.GetFileName(ReceiverFileName) != ReceiverFileName || !string.Equals(Path.GetExtension(ReceiverFileName), ".json", StringComparison.OrdinalIgnoreCase))
            {
                error = "Receiver storage must use a valid .json file name.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private string ReadLogLevel(string filePath, string fallback)
        {
            try
            {
                JsonNode? root = JsonNode.Parse(File.ReadAllText(filePath));
                return root?["Serilog"]?["MinimumLevel"]?["Default"]?.GetValue<string>() is { } level && LogLevels.Contains(level) ? level : fallback;
            }
            catch
            {
                return fallback;
            }
        }

        private void LoadSettingsForSelectedScope()
        {
            JsonObject workstation = ReadSettings(_workstationSettingsFilePath);
            JsonObject selected = string.Equals(SelectedConfigurationScope, "Workstation", StringComparison.Ordinal) ? workstation : ReadSettings(_userSettingsFilePath);
            JsonObject? fallback = ReferenceEquals(selected, workstation) ? null : workstation;
            NotificationOptions notificationDefaults = new();
            LoggingOptions loggingDefaults = new();
            PluginOptions pluginDefaults = new();
            JsonReceiverRepositoryOptions receiverDefaults = new();
            _suppressChangeTracking = true;

            try
            {
                NotificationDurationSeconds = GetValue(selected, fallback, NotificationOptions.SectionName, nameof(NotificationOptions.DisplayDurationSeconds), notificationDefaults.DisplayDurationSeconds).ToString();
                LogRetentionDays = GetValue(selected, fallback, LoggingOptions.SectionName, nameof(LoggingOptions.RetentionDays), loggingDefaults.RetentionDays).ToString();
                LogLevel = GetValue(selected, fallback, "Serilog:MinimumLevel", "Default", "Information");
                PluginDirectory = GetValue(selected, fallback, PluginOptions.SectionName, nameof(PluginOptions.Directory), pluginDefaults.Directory);
                PluginDataDirectory = GetValue(selected, fallback, PluginOptions.SectionName, nameof(PluginOptions.DataDirectory), pluginDefaults.DataDirectory);
                ReceiverDirectory = GetValue(selected, fallback, JsonReceiverRepositoryOptions.SectionName, nameof(JsonReceiverRepositoryOptions.Directory), receiverDefaults.Directory);
                ReceiverFileName = GetValue(selected, fallback, JsonReceiverRepositoryOptions.SectionName, nameof(JsonReceiverRepositoryOptions.FileName), receiverDefaults.FileName);
                HasChanges = false;
                HasError = false;
                StatusMessage = string.Empty;
            }
            finally
            {
                _suppressChangeTracking = false;
            }
        }

        private static JsonObject ReadSettings(string filePath)
        {
            try
            {
                return JsonNode.Parse(File.ReadAllText(filePath)) as JsonObject ?? new JsonObject();
            }
            catch
            {
                return new JsonObject();
            }
        }

        private static T GetValue<T>(JsonObject source, JsonObject? fallback, string sectionPath, string propertyName, T defaultValue)
        {
            JsonNode? value = GetSection(source, sectionPath)?[propertyName] ?? (fallback is null ? null : GetSection(fallback, sectionPath)?[propertyName]);

            try
            {
                return value is null ? defaultValue : value.GetValue<T>();
            }
            catch
            {
                return defaultValue;
            }
        }

        private static JsonObject? GetSection(JsonObject root, string sectionPath)
        {
            JsonObject? current = root;

            foreach (string segment in sectionPath.Split(':'))
                current = current?[segment] as JsonObject;

            return current;
        }

        private static bool IsRunningAsAdministrator()
        {
            using WindowsIdentity identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }

        private async Task<JsonObject> ReadSettingsAsync()
        {
            if (!File.Exists(_settingsFilePath))
                return new JsonObject();

            string json = await File.ReadAllTextAsync(_settingsFilePath);
            return JsonNode.Parse(json) as JsonObject ?? new JsonObject();
        }

        private static void SetSectionValue(JsonObject root, string sectionName, string propertyName, JsonNode value)
        {
            GetOrCreateObject(root, sectionName)[propertyName] = value;
        }

        private static JsonObject GetOrCreateObject(JsonObject parent, string name)
        {
            if (parent[name] is JsonObject existing)
                return existing;

            JsonObject created = new();
            parent[name] = created;
            return created;
        }

        private static void DeleteExpiredLogs(string directory, int retentionDays)
        {
            if (!Directory.Exists(directory))
                return;

            DateTime expirationUtc = DateTime.UtcNow.AddDays(-retentionDays);

            foreach (string file in Directory.EnumerateFiles(directory, "SimulcastUtility_*.log"))
            {
                if (File.GetLastWriteTimeUtc(file) < expirationUtc)
                    File.Delete(file);
            }
        }

        private void SetSetting(ref string field, string value)
        {
            if (!SetProperty(ref field, value))
                return;

            if (_suppressChangeTracking)
                return;

            HasError = false;
            StatusMessage = string.Empty;
            HasChanges = true;
        }
    }
}
