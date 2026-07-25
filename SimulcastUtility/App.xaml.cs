using SimulcastUtility.Plugin.Abstractions.Interfaces;
using SimulcastUtility.Plugins;
using SimulcastUtility.Services;
using SimulcastUtility.ViewModels;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace SimulcastUtility
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private readonly PluginManager _pluginManager = new();

        public ReceiverConfigurationService ReceiverConfigurationService = new();
        public ReceiverControllerService ReceiverControllerService;

        public ReceiverDiscoveryService ReceiverDiscoveryService = new();

        public Dictionary<ISimulcastPlugin, IPluginContext> LoadedPlugins = new Dictionary<ISimulcastPlugin, IPluginContext>();
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            ReceiverControllerService = new ReceiverControllerService(ReceiverConfigurationService);

            try
            {
                await StartApplicationAsync(e.Args);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Application Startup Failed", MessageBoxButton.OK, MessageBoxImage.Error);

                Shutdown(-1);
            }
        }

        private async Task StartApplicationAsync(string[] args)
        {
            IReadOnlyList<ISimulcastPlugin> plugins = InitializePlugins(GetPluginPath(args) ?? Path.Combine(AppContext.BaseDirectory, "Plugins"));

            MainWindowViewModel mainViewModel = new MainWindowViewModel(ReceiverControllerService);
            MainWindow mainWindow = new MainWindow(mainViewModel, ReceiverControllerService);

            MainWindow = mainWindow;
            mainWindow.Show();

            await InitializePluginContextsAsync(plugins, mainWindow, args, CancellationToken.None);
        }

        private async Task InitializePluginContextsAsync(IReadOnlyList<ISimulcastPlugin> plugins, MainWindow window, string[] args, CancellationToken cancellation = default)
        {
            foreach (ISimulcastPlugin plugin in plugins)
            {
                PluginInfo PluginInfo = new PluginInfo(plugin, args);
                PluginContext Context = new PluginContext(PluginInfo, ReceiverConfigurationService, ReceiverControllerService, Dispatcher, window);
                try
                {
                    plugin.OnPluginContextInitialized(Context);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Plugin '{plugin.Name}' failed to initialize its contexts: {ex}");
                }
                finally
                {
                    LoadedPlugins.Add(plugin, Context);
                }
            }
        }

        private IReadOnlyList<ISimulcastPlugin> InitializePlugins(string PluginDirectory)
        {
            IReadOnlyList<LoadedPlugin> results = _pluginManager.LoadPlugins(PluginDirectory);

            List<ISimulcastPlugin> Plugins = new();

            foreach (LoadedPlugin result in results)
            {
                if (!result.LoadedSuccessfully)
                {
                    Debug.WriteLine($"Failed to load plugin '{result.AssemblyPath}': " + result.Error);

                    continue;
                }

                try
                {
                    result.Plugin!.OnPluginInitialized();

                    Debug.WriteLine($"Loaded plugin: {result.Plugin.Name}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Plugin '{result.Plugin!.Name}' failed to initialize: {ex}");
                }
                finally
                {
                    Plugins.Add(result.Plugin!);
                }
            }

            return Plugins;
        }

        private static string? GetPluginPath(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];

                if (string.Equals(arg, "/PluginPath", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(arg, "--PluginPath", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 < args.Length)
                    {
                        string path = Path.GetFullPath(args[i + 1]);

                        if (Directory.Exists(path))
                            return path;
                    }

                    return null;
                }

                const string slashPrefix = "/PluginPath:";
                const string dashPrefix = "--PluginPath:";

                if (arg.StartsWith(slashPrefix, StringComparison.OrdinalIgnoreCase) ||
                    arg.StartsWith(dashPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    int colonIndex = arg.IndexOf(':');
                    string path = arg[(colonIndex + 1)..].Trim('"');

                    path = Path.GetFullPath(path);

                    if (Directory.Exists(path))
                        return path;

                    return null;
                }
            }

            return null;
        }
    }

}
