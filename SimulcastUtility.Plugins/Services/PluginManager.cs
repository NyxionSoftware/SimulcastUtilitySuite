using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SimulcastUtility.Application.Interfaces;
using SimulcastUtility.Plugins.Interfaces;
using SimulcastUtility.Plugins.Models;
using SimulcastUtility.Plugins.Options;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.IO;
using System.IO.Compression;
using System.Text.Json;

namespace SimulcastUtility.Plugins.Services
{
    public sealed class PluginManager : IPluginManager
    {
        private readonly PluginOptions _options;
        private readonly ILogger<PluginManager> _logger;
        private readonly IReceiverRepository _receiverRepository;
        private readonly IReceiverManager _receiverManager;
        private readonly IReceiverCommandManager _receiverCommandManager;
        private readonly IPluginApplicationDispatcher _applicationDispatcher;
        private readonly IPluginThemeManager _pluginThemeManager;
        private readonly IPluginUiManager _pluginUiManager;
        private readonly List<PluginRuntime> _runtimes = new();
        private readonly SemaphoreSlim _stateLock = new(1, 1);
        private readonly JsonSerializerOptions _serializerOptions = new() { WriteIndented = true };

        public PluginManager(IOptions<PluginOptions> options, ILogger<PluginManager> logger, IReceiverRepository receiverRepository, IReceiverManager receiverManager, IReceiverCommandManager receiverCommandManager, IPluginApplicationDispatcher applicationDispatcher, IPluginThemeManager pluginThemeManager, IPluginUiManager pluginUiManager)
        {
            _options = options.Value;
            _logger = logger;
            _pluginThemeManager = pluginThemeManager;
            _pluginUiManager = pluginUiManager;
            _receiverRepository = receiverRepository;
            _receiverManager = receiverManager;
            _receiverCommandManager = receiverCommandManager;
            _applicationDispatcher = applicationDispatcher;
        }

        public IReadOnlyList<LoadedPlugin> Plugins => _runtimes.Select(runtime => runtime.Model).ToArray();

        public string PluginDirectory => _options.Directory;

        public event EventHandler? PluginsChanged;

        public async Task LoadAsync(IReadOnlyList<string> applicationArguments, CancellationToken cancellationToken = default)
        {
            await _stateLock.WaitAsync(cancellationToken);

            try
            {
                if (_runtimes.Count > 0)
                    return;

                Directory.CreateDirectory(_options.Directory);
                HashSet<Guid> disabledPlugins = await LoadDisabledPluginIdentifiersAsync(cancellationToken);

                foreach (string assemblyPath in EnumeratePluginAssemblyCandidates(_options.Directory))
                    await TryLoadAssemblyAsync(assemblyPath, disabledPlugins, applicationArguments, cancellationToken);
            }
            finally
            {
                _stateLock.Release();
            }

            PluginsChanged?.Invoke(this, EventArgs.Empty);
        }

        public async Task SetEnabledAsync(Guid pluginIdentifier, bool isEnabled, CancellationToken cancellationToken = default)
        {
            await _stateLock.WaitAsync(cancellationToken);
            PluginRuntime? runtime = null;

            try
            {
                runtime = _runtimes.SingleOrDefault(item => item.Model.Info.PluginIdentifier == pluginIdentifier) ?? throw new KeyNotFoundException($"Plugin '{pluginIdentifier}' is not loaded.");

                if (runtime.Model.IsEnabled == isEnabled)
                    return;

                if (isEnabled)
                {
                    await EnsureInitializedAsync(runtime, cancellationToken);
                    await runtime.Plugin.EnableAsync(cancellationToken);
                }
                else
                {
                    try
                    {
                        await runtime.Plugin.DisableAsync(cancellationToken);
                    }
                    finally
                    {
                        await _pluginThemeManager.RemoveResourceDictionaryAsync(pluginIdentifier, cancellationToken);
                        await _pluginUiManager.RemovePluginUiAsync(pluginIdentifier, cancellationToken);
                    }
                }

                runtime.Model.IsEnabled = isEnabled;
                runtime.Model.Error = null;
                await SaveDisabledPluginIdentifiersAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                if (runtime is not null)
                    runtime.Model.Error = ex.Message;

                _logger.LogError(ex, "Plugin {PluginIdentifier} could not be changed to enabled={IsEnabled}.", pluginIdentifier, isEnabled);
                throw;
            }
            finally
            {
                _stateLock.Release();
            }

            PluginsChanged?.Invoke(this, EventArgs.Empty);
        }

        public async Task<IReadOnlyList<PluginSettingDescriptor>> GetSettingsAsync(Guid pluginIdentifier, CancellationToken cancellationToken = default)
        {
            await _stateLock.WaitAsync(cancellationToken);

            try
            {
                PluginRuntime runtime = GetRuntime(pluginIdentifier);
                await EnsureInitializedAsync(runtime, cancellationToken);
                return CreateSettingDescriptors(runtime);
            }
            finally
            {
                _stateLock.Release();
            }
        }

        public async Task SetSettingAsync(Guid pluginIdentifier, string settingKey, JsonElement value, CancellationToken cancellationToken = default)
        {
            await _stateLock.WaitAsync(cancellationToken);

            try
            {
                PluginRuntime runtime = GetRuntime(pluginIdentifier);
                await EnsureInitializedAsync(runtime, cancellationToken);
                IPluginSettingsProvider provider = GetSettingsProvider(runtime);
                PropertyInfo property = GetSettingProperties(provider).SingleOrDefault(item => item.Name == settingKey) ?? throw new KeyNotFoundException($"Plugin setting '{settingKey}' was not found.");
                object? convertedValue = value.Deserialize(property.PropertyType, _serializerOptions);
                property.SetValue(provider.Settings, convertedValue);
                await runtime.DataStore!.WriteAsync(GetSettingDataName(property.Name), value, cancellationToken);
                await provider.OnSettingChangedAsync(property.Name, cancellationToken);
            }
            finally
            {
                _stateLock.Release();
            }
        }

        public async Task<int> RefreshAsync(CancellationToken cancellationToken = default)
        {
            await _stateLock.WaitAsync(cancellationToken);
            int pluginCountBeforeRefresh = _runtimes.Count;

            try
            {
                Directory.CreateDirectory(_options.Directory);
                HashSet<Guid> disabledPlugins = await LoadDisabledPluginIdentifiersAsync(cancellationToken);
                HashSet<string> loadedAssemblyPaths = _runtimes.Select(runtime => Path.GetFullPath(runtime.Model.AssemblyPath)).ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (string assemblyPath in EnumeratePluginAssemblyCandidates(_options.Directory))
                {
                    if (loadedAssemblyPaths.Contains(Path.GetFullPath(assemblyPath)))
                        continue;

                    await TryLoadAssemblyAsync(assemblyPath, disabledPlugins, Array.Empty<string>(), cancellationToken);
                }
            }
            finally
            {
                _stateLock.Release();
            }

            int loadedPluginCount = _runtimes.Count - pluginCountBeforeRefresh;
            PluginsChanged?.Invoke(this, EventArgs.Empty);
            return loadedPluginCount;
        }

        public async Task<PluginImportResult> ImportAsync(IReadOnlyList<string> sourcePaths, CancellationToken cancellationToken = default)
        {
            if (sourcePaths.Count == 0)
                throw new ArgumentException("At least one plugin file must be selected.", nameof(sourcePaths));

            string[] fullSourcePaths = sourcePaths.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

            foreach (string sourcePath in fullSourcePaths)
            {
                if (!File.Exists(sourcePath))
                    throw new FileNotFoundException("A selected plugin file does not exist.", sourcePath);
            }

            bool isZipImport = fullSourcePaths.Length == 1 && string.Equals(Path.GetExtension(fullSourcePaths[0]), ".zip", StringComparison.OrdinalIgnoreCase);

            if (!isZipImport && fullSourcePaths.Any(path => !string.Equals(Path.GetExtension(path), ".dll", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("Select either one ZIP archive or one or more DLL files.");

            Directory.CreateDirectory(_options.Directory);
            string importName = MakeSafeDirectoryName(Path.GetFileNameWithoutExtension(fullSourcePaths[0]));
            string destinationDirectory = GetAvailableDestinationDirectory(importName);
            Directory.CreateDirectory(destinationDirectory);

            try
            {
                if (isZipImport)
                    await ExtractArchiveAsync(fullSourcePaths[0], destinationDirectory, cancellationToken);
                else
                    await CopyFilesAsync(fullSourcePaths, destinationDirectory, cancellationToken);
            }
            catch
            {
                Directory.Delete(destinationDirectory, recursive: true);
                throw;
            }

            int pluginCountBeforeImport = _runtimes.Count;

            await _stateLock.WaitAsync(cancellationToken);

            try
            {
                HashSet<Guid> disabledPlugins = await LoadDisabledPluginIdentifiersAsync(cancellationToken);

                foreach (string assemblyPath in EnumeratePluginAssemblyCandidates(destinationDirectory))
                    await TryLoadAssemblyAsync(assemblyPath, disabledPlugins, Array.Empty<string>(), cancellationToken);
            }
            finally
            {
                _stateLock.Release();
            }

            int loadedPluginCount = _runtimes.Count - pluginCountBeforeImport;
            PluginsChanged?.Invoke(this, EventArgs.Empty);
            int importedFileCount = Directory.EnumerateFiles(destinationDirectory, "*", SearchOption.AllDirectories).Count();
            return new PluginImportResult(destinationDirectory, importedFileCount, loadedPluginCount);
        }

        public async Task DeleteAsync(Guid pluginIdentifier, CancellationToken cancellationToken = default)
        {
            await PreparePluginPackageForUnloadAsync(pluginIdentifier, cancellationToken);
            PluginUnloadResult unloadResult = UnloadPluginPackage(pluginIdentifier);
            await SavePluginStateAfterRemovalAsync(cancellationToken);
            bool unloaded = await WaitForUnloadAsync(unloadResult.LoadContextReferences, cancellationToken);
            DeleteTarget(unloadResult.DeletionTarget);

            if (!unloaded)
                _logger.LogWarning("Plugin files were deleted after unload was requested, but one or more plugin load contexts are still awaiting garbage collection.");

            PluginsChanged?.Invoke(this, EventArgs.Empty);
        }

        private async Task PreparePluginPackageForUnloadAsync(Guid pluginIdentifier, CancellationToken cancellationToken)
        {
            await _stateLock.WaitAsync(cancellationToken);

            try
            {
                PluginRuntime selectedRuntime = _runtimes.SingleOrDefault(runtime => runtime.Model.Info.PluginIdentifier == pluginIdentifier) ?? throw new KeyNotFoundException($"Plugin '{pluginIdentifier}' is not loaded.");
                string deletionTarget = GetPluginDeletionTarget(selectedRuntime.Model.AssemblyPath);
                PluginRuntime[] packageRuntimes = _runtimes.Where(runtime => IsPathWithin(runtime.Model.AssemblyPath, deletionTarget)).ToArray();

                foreach (PluginRuntime runtime in packageRuntimes)
                {
                    try
                    {
                        if (runtime.Model.IsEnabled)
                            await runtime.Plugin.DisableAsync(cancellationToken);

                        if (runtime.Plugin is IAsyncDisposable asyncDisposable)
                            await asyncDisposable.DisposeAsync();
                        else if (runtime.Plugin is IDisposable disposable)
                            disposable.Dispose();
                    }
                    finally
                    {
                        await _pluginThemeManager.RemoveResourceDictionaryAsync(runtime.Model.Info.PluginIdentifier, cancellationToken);
                        await _pluginUiManager.RemovePluginUiAsync(runtime.Model.Info.PluginIdentifier, cancellationToken);
                    }
                }
            }
            finally
            {
                _stateLock.Release();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private PluginUnloadResult UnloadPluginPackage(Guid pluginIdentifier)
        {
            PluginRuntime selectedRuntime = _runtimes.SingleOrDefault(runtime => runtime.Model.Info.PluginIdentifier == pluginIdentifier) ?? throw new KeyNotFoundException($"Plugin '{pluginIdentifier}' is not loaded.");
            string deletionTarget = GetPluginDeletionTarget(selectedRuntime.Model.AssemblyPath);
            PluginRuntime[] packageRuntimes = _runtimes.Where(runtime => IsPathWithin(runtime.Model.AssemblyPath, deletionTarget)).ToArray();
            PluginLoadContext[] loadContexts = packageRuntimes.Select(runtime => runtime.LoadContext).Distinct().ToArray();
            List<WeakReference> loadContextReferences = loadContexts.Select(loadContext => new WeakReference(loadContext, trackResurrection: false)).ToList();

            foreach (PluginRuntime runtime in packageRuntimes)
                _runtimes.Remove(runtime);

            foreach (PluginLoadContext loadContext in loadContexts)
                loadContext.Unload();

            return new PluginUnloadResult(deletionTarget, loadContextReferences);
        }

        private static async Task<bool> WaitForUnloadAsync(IReadOnlyList<WeakReference> loadContextReferences, CancellationToken cancellationToken)
        {
            for (int attempt = 0; attempt < 10 && loadContextReferences.Any(reference => reference.IsAlive); attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                await Task.Delay(50, cancellationToken);
            }

            return loadContextReferences.All(reference => !reference.IsAlive);
        }

        private async Task SavePluginStateAfterRemovalAsync(CancellationToken cancellationToken)
        {
            await _stateLock.WaitAsync(cancellationToken);

            try
            {
                await SaveDisabledPluginIdentifiersAsync(cancellationToken);
            }
            finally
            {
                _stateLock.Release();
            }
        }

        public async Task HandleApplicationArgumentsAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken = default)
        {
            PluginRuntime[] enabledPlugins = _runtimes.Where(runtime => runtime.Model.IsEnabled).ToArray();

            foreach (PluginRuntime runtime in enabledPlugins)
            {
                try
                {
                    await runtime.Plugin.HandleApplicationArgumentsAsync(arguments, cancellationToken);
                }
                catch (Exception ex)
                {
                    runtime.Model.Error = ex.Message;
                    _logger.LogError(ex, "Plugin {PluginName} failed while handling application arguments.", runtime.Model.Info.Name);
                }
            }

            PluginsChanged?.Invoke(this, EventArgs.Empty);
        }

        private async Task TryLoadAssemblyAsync(string assemblyPath, HashSet<Guid> disabledPlugins, IReadOnlyList<string> applicationArguments, CancellationToken cancellationToken)
        {
            PluginLoadContext? loadContext = null;
            bool runtimeAdded = false;

            try
            {
                loadContext = new PluginLoadContext(assemblyPath);
                Assembly assembly = loadContext.LoadPluginAssembly();
                string assemblyName = assembly.GetName().Name ?? throw new InvalidOperationException($"Plugin assembly '{assemblyPath}' does not have a valid assembly name.");
                PluginRuntime? conflictingRuntime = _runtimes.FirstOrDefault(runtime => !ReferenceEquals(runtime.LoadContext, loadContext) && string.Equals(runtime.AssemblyName, assemblyName, StringComparison.OrdinalIgnoreCase));

                if (conflictingRuntime is not null)
                    throw new InvalidOperationException($"Plugin assembly name '{assemblyName}' is already used by '{conflictingRuntime.Model.Info.Name}'. Plugin assembly names must be unique so WPF resources can be resolved safely.");

                Type[] pluginTypes = GetLoadableTypes(assembly).Where(type => !type.IsAbstract && !type.IsInterface && typeof(ISimulcastPlugin).IsAssignableFrom(type)).ToArray();

                foreach (Type pluginType in pluginTypes)
                {
                    if (Activator.CreateInstance(pluginType) is not ISimulcastPlugin plugin)
                        continue;

                    IPluginInfo info = plugin.Info ?? throw new InvalidOperationException($"Plugin type '{pluginType.FullName}' did not provide plugin information.");

                    if (info.PluginIdentifier == Guid.Empty)
                        throw new InvalidOperationException($"Plugin '{info.Name}' has an empty identifier.");

                    if (_runtimes.Any(runtime => runtime.Model.Info.PluginIdentifier == info.PluginIdentifier))
                        throw new InvalidOperationException($"Plugin identifier '{info.PluginIdentifier}' is already loaded.");

                    bool isEnabled = !disabledPlugins.Contains(info.PluginIdentifier);
                    PluginRuntime runtime = new(plugin, new LoadedPlugin(PluginInfo.CreateSnapshot(info), assemblyPath, false, plugin is IPluginSettingsProvider, null), loadContext);
                    _runtimes.Add(runtime);
                    runtimeAdded = true;

                    if (!isEnabled)
                        continue;

                    await EnsureInitializedAsync(runtime, cancellationToken);
                    await plugin.EnableAsync(cancellationToken);
                    runtime.Model.IsEnabled = true;
                    await plugin.HandleApplicationArgumentsAsync(applicationArguments, cancellationToken);
                    _logger.LogInformation("Loaded plugin {PluginName} {PluginVersion} from {AssemblyPath}.", info.Name, info.Version, assemblyPath);
                }
            }
            catch (BadImageFormatException)
            {
                _logger.LogDebug("Skipping non-managed plugin candidate {AssemblyPath}.", assemblyPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Plugin candidate {AssemblyPath} could not be loaded.", assemblyPath);
            }
            finally
            {
                if (!runtimeAdded)
                    loadContext?.Unload();
            }
        }

        private async Task EnsureInitializedAsync(PluginRuntime runtime, CancellationToken cancellationToken)
        {
            if (runtime.IsInitialized)
                return;

            IPluginDataStore dataStore = new PluginDataStore(_options.DataDirectory, runtime.Model.Info.PluginIdentifier);
            string installationDirectory = Path.GetDirectoryName(runtime.Model.AssemblyPath) ?? throw new InvalidOperationException("The plugin installation directory is unavailable.");
            IPluginContext pluginContext = new PluginContext(installationDirectory, _receiverRepository, _receiverManager, _receiverCommandManager, _applicationDispatcher, _pluginThemeManager, _pluginUiManager, dataStore);
            await runtime.Plugin.InitializeAsync(pluginContext, cancellationToken);
            runtime.DataStore = dataStore;

            if (runtime.Plugin is IPluginSettingsProvider settingsProvider)
                await LoadPluginSettingsAsync(settingsProvider, dataStore, cancellationToken);

            runtime.IsInitialized = true;
        }

        private async Task LoadPluginSettingsAsync(IPluginSettingsProvider provider, IPluginDataStore dataStore, CancellationToken cancellationToken)
        {
            foreach (PropertyInfo property in GetSettingProperties(provider))
            {
                JsonElement? savedValue = await dataStore.ReadAsync<JsonElement>(GetSettingDataName(property.Name), cancellationToken);

                if (savedValue is not null && savedValue.Value.ValueKind is not JsonValueKind.Undefined and not JsonValueKind.Null)
                    property.SetValue(provider.Settings, savedValue.Value.Deserialize(property.PropertyType, _serializerOptions));
            }
        }

        private static IReadOnlyList<PluginSettingDescriptor> CreateSettingDescriptors(PluginRuntime runtime)
        {
            if (runtime.Plugin is not IPluginSettingsProvider provider)
                return Array.Empty<PluginSettingDescriptor>();

            return GetSettingProperties(provider).Select(property =>
            {
                PluginSettingAttribute attribute = property.GetCustomAttribute<PluginSettingAttribute>()!;
                JsonElement value = JsonSerializer.SerializeToElement(property.GetValue(provider.Settings), property.PropertyType);
                IReadOnlyList<PluginSettingOption> options = provider.GetSettingOptions(property.Name);
                return new PluginSettingDescriptor(property.Name, attribute.Name, attribute.Description, attribute.Group, attribute.Order, attribute.ControlType, attribute.SelectedItemsName, value, options);
            }).OrderBy(setting => setting.Group).ThenBy(setting => setting.Order).ToArray();
        }

        private static PropertyInfo[] GetSettingProperties(IPluginSettingsProvider provider)
        {
            return provider.Settings.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public).Where(property => property.CanRead && property.CanWrite && property.GetCustomAttribute<PluginSettingAttribute>() is not null).ToArray();
        }

        private PluginRuntime GetRuntime(Guid pluginIdentifier)
        {
            return _runtimes.SingleOrDefault(item => item.Model.Info.PluginIdentifier == pluginIdentifier) ?? throw new KeyNotFoundException($"Plugin '{pluginIdentifier}' is not loaded.");
        }

        private static IPluginSettingsProvider GetSettingsProvider(PluginRuntime runtime)
        {
            return runtime.Plugin as IPluginSettingsProvider ?? throw new InvalidOperationException($"Plugin '{runtime.Model.Info.Name}' does not expose settings.");
        }

        private static string GetSettingDataName(string settingKey)
        {
            return $"setting-{settingKey}";
        }

        private async Task<HashSet<Guid>> LoadDisabledPluginIdentifiersAsync(CancellationToken cancellationToken)
        {
            string stateFilePath = _options.GetStateFilePath();

            if (!File.Exists(stateFilePath))
                return new HashSet<Guid>();

            await using FileStream stream = new(stateFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
            PluginState? state = await JsonSerializer.DeserializeAsync<PluginState>(stream, _serializerOptions, cancellationToken);
            return state?.DisabledPluginIdentifiers.ToHashSet() ?? new HashSet<Guid>();
        }

        private async Task SaveDisabledPluginIdentifiersAsync(CancellationToken cancellationToken)
        {
            Directory.CreateDirectory(_options.Directory);
            PluginState state = new() { DisabledPluginIdentifiers = _runtimes.Where(runtime => !runtime.Model.IsEnabled).Select(runtime => runtime.Model.Info.PluginIdentifier).ToList() };
            await using FileStream stream = new(_options.GetStateFilePath(), FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
            await JsonSerializer.SerializeAsync(stream, state, _serializerOptions, cancellationToken);
        }

        private static Type[] GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(type => type is not null).Cast<Type>().ToArray();
            }
        }

        private static IReadOnlyList<string> EnumeratePluginAssemblyCandidates(string pluginDirectory)
        {
            HashSet<string> candidates = new(StringComparer.OrdinalIgnoreCase);

            foreach (string dependencyManifestPath in Directory.EnumerateFiles(pluginDirectory, "*.deps.json", SearchOption.AllDirectories))
            {
                string assemblyPath = dependencyManifestPath[..^".deps.json".Length] + ".dll";

                if (File.Exists(assemblyPath))
                    candidates.Add(Path.GetFullPath(assemblyPath));
            }

            foreach (string assemblyPath in Directory.EnumerateFiles(pluginDirectory, "*.dll", SearchOption.TopDirectoryOnly))
                candidates.Add(Path.GetFullPath(assemblyPath));

            foreach (string packageDirectory in Directory.EnumerateDirectories(pluginDirectory, "*", SearchOption.TopDirectoryOnly))
            {
                bool hasDependencyManifest = Directory.EnumerateFiles(packageDirectory, "*.deps.json", SearchOption.AllDirectories).Any();

                if (hasDependencyManifest)
                    continue;

                foreach (string assemblyPath in Directory.EnumerateFiles(packageDirectory, "*.dll", SearchOption.TopDirectoryOnly))
                    candidates.Add(Path.GetFullPath(assemblyPath));
            }

            return candidates.ToArray();
        }

        private string GetPluginDeletionTarget(string assemblyPath)
        {
            string pluginRoot = Path.GetFullPath(_options.Directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string fullAssemblyPath = Path.GetFullPath(assemblyPath);

            if (!fullAssemblyPath.StartsWith(pluginRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The plugin is outside the configured plugin directory and cannot be deleted.");

            string relativePath = Path.GetRelativePath(pluginRoot, fullAssemblyPath);
            string[] segments = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return segments.Length > 1 ? Path.Combine(pluginRoot, segments[0]) : fullAssemblyPath;
        }

        private static bool IsPathWithin(string path, string target)
        {
            string fullPath = Path.GetFullPath(path);
            string fullTarget = Path.GetFullPath(target);
            return File.Exists(fullTarget)
                ? string.Equals(fullPath, fullTarget, StringComparison.OrdinalIgnoreCase)
                : fullPath.StartsWith(fullTarget.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        private static void DeleteTarget(string deletionTarget)
        {
            if (Directory.Exists(deletionTarget))
                Directory.Delete(deletionTarget, recursive: true);
            else if (File.Exists(deletionTarget))
                File.Delete(deletionTarget);
        }

        private string GetAvailableDestinationDirectory(string importName)
        {
            string destinationDirectory = Path.Combine(_options.Directory, importName);
            int suffix = 2;

            while (Directory.Exists(destinationDirectory))
            {
                destinationDirectory = Path.Combine(_options.Directory, $"{importName} ({suffix})");
                suffix++;
            }

            return destinationDirectory;
        }

        private static string MakeSafeDirectoryName(string value)
        {
            string sanitizedName = string.Concat(value.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character)).Trim();
            return string.IsNullOrWhiteSpace(sanitizedName) ? "Imported Plugin" : sanitizedName;
        }

        private static async Task CopyFilesAsync(IEnumerable<string> sourcePaths, string destinationDirectory, CancellationToken cancellationToken)
        {
            foreach (string sourcePath in sourcePaths)
            {
                string destinationPath = Path.Combine(destinationDirectory, Path.GetFileName(sourcePath));
                await using FileStream source = new(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
                await using FileStream destination = new(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
                await source.CopyToAsync(destination, cancellationToken);
            }
        }

        private static async Task ExtractArchiveAsync(string archivePath, string destinationDirectory, CancellationToken cancellationToken)
        {
            string destinationRoot = Path.GetFullPath(destinationDirectory) + Path.DirectorySeparatorChar;
            await using FileStream archiveStream = new(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
            using ZipArchive archive = new(archiveStream, ZipArchiveMode.Read);

            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string destinationPath = Path.GetFullPath(Path.Combine(destinationDirectory, entry.FullName));

                if (!destinationPath.StartsWith(destinationRoot, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("The plugin archive contains an unsafe file path.");

                if (string.IsNullOrEmpty(entry.Name))
                {
                    Directory.CreateDirectory(destinationPath);
                    continue;
                }

                string? entryDirectory = Path.GetDirectoryName(destinationPath);

                if (entryDirectory is not null)
                    Directory.CreateDirectory(entryDirectory);

                await using Stream entryStream = entry.Open();
                await using FileStream destinationStream = new(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
                await entryStream.CopyToAsync(destinationStream, cancellationToken);
            }
        }

        private sealed class PluginRuntime
        {
            public PluginRuntime(ISimulcastPlugin plugin, LoadedPlugin model, PluginLoadContext loadContext)
            {
                Plugin = plugin;
                Model = model;
                LoadContext = loadContext;
            }

            public ISimulcastPlugin Plugin { get; }

            public LoadedPlugin Model { get; }

            public PluginLoadContext LoadContext { get; }

            public string AssemblyName => Plugin.GetType().Assembly.GetName().Name ?? string.Empty;

            public bool IsInitialized { get; set; }

            public IPluginDataStore? DataStore { get; set; }
        }

        private sealed class PluginState
        {
            public List<Guid> DisabledPluginIdentifiers { get; set; } = new();
        }

        private sealed record PluginUnloadResult(string DeletionTarget, IReadOnlyList<WeakReference> LoadContextReferences);
    }
}
