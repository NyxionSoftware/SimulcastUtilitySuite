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
using SimulcastUtility.Wpf.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Principal;
using System.Windows.Documents;

namespace SimulcastUtility.Wpf.ViewModels.Views
{
    public sealed class ApplicationSettingsViewModel : ObservableObject
    {
        private const string ReleasesUrl = "https://github.com/NyxionSoftware/SimulcastUtilitySuite/releases";
        private const string RepositoryUrl = "https://github.com/NyxionSoftware/SimulcastUtilitySuite";
        private const string WebsiteUrl = "https://nyxionsoftware.com/";
        private const string LatestReleaseApiUrl = "https://api.github.com/repos/NyxionSoftware/SimulcastUtilitySuite/releases/latest";
        private static readonly HttpClient UpdateClient = CreateUpdateClient();
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
        private bool _isCheckingForUpdates;
        private bool _isUpdateAvailable;
        private string _updateStatusMessage = string.Empty;
        private string _availableVersion = string.Empty;
        private bool _isPatchNotesVisible;
        private bool _isAboutVisible;
        private bool _hasLoadedPatchNotes;
        private bool _isLoadingPatchNotes;
        private FlowDocument _patchNotesDocument = MarkdownFlowDocumentRenderer.CreateMessage("Select Patch Notes to load release information.");

        public ObservableCollection<string> LogLevels { get; } = new() { "Verbose", "Debug", "Information", "Warning", "Error", "Fatal" };

        public ObservableCollection<string> ConfigurationScopes { get; }

        public bool IsWorkstationScopeAvailable { get; }

        public string CurrentVersion { get; } = GetCurrentVersion();

        public string UpdateStatusMessage
        {
            get => _updateStatusMessage;
            private set => SetProperty(ref _updateStatusMessage, value);
        }

        public bool IsCheckingForUpdates
        {
            get => _isCheckingForUpdates;
            private set
            {
                if (!SetProperty(ref _isCheckingForUpdates, value))
                    return;

                CheckForUpdatesCommand.NotifyCanExecuteChanged();
            }
        }

        public bool IsUpdateAvailable
        {
            get => _isUpdateAvailable;
            private set
            {
                if (!SetProperty(ref _isUpdateAvailable, value))
                    return;

                OnPropertyChanged(nameof(UpdateOverlayVisibility));
            }
        }

        public string AvailableVersion
        {
            get => _availableVersion;
            private set => SetProperty(ref _availableVersion, value);
        }

        public System.Windows.Visibility UpdateOverlayVisibility => IsUpdateAvailable ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

        public FlowDocument PatchNotesDocument
        {
            get => _patchNotesDocument;
            private set => SetProperty(ref _patchNotesDocument, value);
        }

        public bool IsPatchNotesVisible
        {
            get => _isPatchNotesVisible;
            private set
            {
                if (!SetProperty(ref _isPatchNotesVisible, value))
                    return;

                OnPropertyChanged(nameof(PatchNotesVisibility));
            }
        }

        public bool IsAboutVisible
        {
            get => _isAboutVisible;
            private set
            {
                if (!SetProperty(ref _isAboutVisible, value))
                    return;

                OnPropertyChanged(nameof(AboutVisibility));
            }
        }

        public System.Windows.Visibility PatchNotesVisibility => IsPatchNotesVisible ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

        public System.Windows.Visibility AboutVisibility => IsAboutVisible ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

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

        public IAsyncRelayCommand CheckForUpdatesCommand { get; }

        public IRelayCommand DownloadUpdateCommand { get; }

        public IRelayCommand DismissUpdateCommand { get; }

        public IRelayCommand ShowPatchNotesCommand { get; }

        public IRelayCommand DismissPatchNotesCommand { get; }

        public IRelayCommand ShowAboutCommand { get; }

        public IRelayCommand DismissAboutCommand { get; }

        public IRelayCommand OpenRepositoryCommand { get; }

        public IRelayCommand OpenWebsiteCommand { get; }

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
            CheckForUpdatesCommand = new AsyncRelayCommand(CheckForUpdatesAsync, () => !IsCheckingForUpdates);
            DownloadUpdateCommand = new RelayCommand(() =>
            {
                IsUpdateAvailable = false;
                OpenReleasesPage();
            });
            DismissUpdateCommand = new RelayCommand(() => IsUpdateAvailable = false);
            ShowPatchNotesCommand = new AsyncRelayCommand(ShowPatchNotesAsync, () => !IsLoadingPatchNotes);
            DismissPatchNotesCommand = new RelayCommand(() => IsPatchNotesVisible = false);
            ShowAboutCommand = new RelayCommand(() => IsAboutVisible = true);
            DismissAboutCommand = new RelayCommand(() => IsAboutVisible = false);
            OpenRepositoryCommand = new RelayCommand(() => OpenUrl(RepositoryUrl));
            OpenWebsiteCommand = new RelayCommand(() => OpenUrl(WebsiteUrl));
        }

        private async Task ShowPatchNotesAsync()
        {
            IsPatchNotesVisible = true;

            if (_hasLoadedPatchNotes)
                return;

            IsLoadingPatchNotes = true;
            PatchNotesDocument = MarkdownFlowDocumentRenderer.CreateMessage("Loading patch notes...");

            try
            {
                string releaseJson = await GetReleaseForCurrentVersionAsync();
                using JsonDocument document = JsonDocument.Parse(releaseJson);
                string releaseName = document.RootElement.TryGetProperty("name", out JsonElement nameElement) ? nameElement.GetString() ?? string.Empty : string.Empty;
                string markdown = document.RootElement.TryGetProperty("body", out JsonElement bodyElement) ? bodyElement.GetString() ?? string.Empty : string.Empty;

                if (string.IsNullOrWhiteSpace(markdown))
                    markdown = $"# {releaseName}\n\nNo patch notes were provided for this release.";

                PatchNotesDocument = MarkdownFlowDocumentRenderer.Render(markdown);
                _hasLoadedPatchNotes = true;
            }
            catch (Exception ex)
            {
                PatchNotesDocument = MarkdownFlowDocumentRenderer.CreateError($"Patch notes could not be loaded.\n\n{ex.Message}");
            }
            finally
            {
                IsLoadingPatchNotes = false;
            }
        }

        private async Task<string> GetReleaseForCurrentVersionAsync()
        {
            string escapedVersion = Uri.EscapeDataString(CurrentVersion);

            foreach (string tag in new[] { $"v{escapedVersion}", escapedVersion })
            {
                using HttpResponseMessage response = await UpdateClient.GetAsync($"https://api.github.com/repos/NyxionSoftware/SimulcastUtilitySuite/releases/tags/{tag}");

                if (response.IsSuccessStatusCode)
                    return await response.Content.ReadAsStringAsync();

                if (response.StatusCode != System.Net.HttpStatusCode.NotFound)
                    response.EnsureSuccessStatusCode();
            }

            throw new InvalidOperationException($"No GitHub release was found for version {CurrentVersion}.");
        }

        private bool IsLoadingPatchNotes
        {
            get => _isLoadingPatchNotes;
            set
            {
                if (!SetProperty(ref _isLoadingPatchNotes, value))
                    return;

                ShowPatchNotesCommand.NotifyCanExecuteChanged();
            }
        }

        private async Task CheckForUpdatesAsync()
        {
            IsCheckingForUpdates = true;
            UpdateStatusMessage = "Checking for updates...";

            try
            {
                using HttpResponseMessage response = await UpdateClient.GetAsync(LatestReleaseApiUrl);
                response.EnsureSuccessStatusCode();
                using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                string tagName = document.RootElement.GetProperty("tag_name").GetString() ?? string.Empty;

                if (!TryParseVersion(tagName, out Version? latestVersion) || latestVersion is null || !TryParseVersion(CurrentVersion, out Version? currentVersion) || currentVersion is null)
                    throw new InvalidOperationException("GitHub returned an invalid release version.");

                if (latestVersion > currentVersion)
                {
                    AvailableVersion = latestVersion.ToString(3);
                    UpdateStatusMessage = $"Version {AvailableVersion} is available.";
                    IsUpdateAvailable = true;
                }
                else
                {
                    UpdateStatusMessage = "Up to date.";
                }
            }
            catch (Exception ex)
            {
                UpdateStatusMessage = $"Unable to check for updates: {ex.Message}";
            }
            finally
            {
                IsCheckingForUpdates = false;
            }
        }

        private static void OpenReleasesPage()
        {
            OpenUrl(ReleasesUrl);
        }

        private static void OpenUrl(string url)
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }

        private static HttpClient CreateUpdateClient()
        {
            HttpClient client = new() { Timeout = TimeSpan.FromSeconds(10) };
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("SimulcastUtility", GetCurrentVersion()));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            return client;
        }

        private static string GetCurrentVersion()
        {
            Assembly assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            string? informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

            if (!string.IsNullOrWhiteSpace(informationalVersion))
                return informationalVersion.Split('+')[0];

            Version version = assembly.GetName().Version ?? new Version(1, 0, 0);
            return $"{version.Major}.{version.Minor}.{Math.Max(version.Build, 0)}";
        }

        private static bool TryParseVersion(string value, out Version? version)
        {
            string normalized = value.Trim().TrimStart('v', 'V').Split('-', '+')[0];
            return Version.TryParse(normalized, out version);
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
