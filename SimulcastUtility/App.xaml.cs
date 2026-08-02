using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using SimulcastUtility.Application;
using SimulcastUtility.Application.Interfaces;
using SimulcastUtility.Application.Requests;
using SimulcastUtility.Application.Services;
using SimulcastUtility.Infrastructure;
using SimulcastUtility.Configuration.Models;
using SimulcastUtility.Plugins;
using SimulcastUtility.Plugins.Interfaces;
using SimulcastUtility.Wpf;
using SimulcastUtility.Wpf.Options;
using System.IO;
using System.Windows;

namespace SimulcastUtility
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private readonly IHost _host;

        public App()
        {
            HostApplicationBuilder builder = Host.CreateApplicationBuilder();

            ConfigureConfiguration(builder.Configuration);
            ConfigureLogging(builder);
            ConfigureServices(builder.Services, builder.Configuration);

            _host = builder.Build();
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            try
            {
                await _host.StartAsync();

                ReceiverManager receiverManagerInitializer = _host.Services.GetRequiredService<ReceiverManager>();

                await receiverManagerInitializer.InitializeAsync();

                MainWindow mainWindow = _host.Services.GetRequiredService<MainWindow>();

                MainWindow = mainWindow;
                mainWindow.Opacity = 0;
                mainWindow.Show();
                await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);

                IPluginManager pluginManager = _host.Services.GetRequiredService<IPluginManager>();
                await pluginManager.LoadAsync(e.Args);

                mainWindow.Opacity = 1;

                base.OnStartup(e);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Simulcast Utility failed to start.\n\n{ex.Message}", "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);

                Shutdown(-1);
            }
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            try
            {
                await _host.StopAsync();
            }
            finally
            {
                _host.Dispose();
                await Log.CloseAndFlushAsync();

                base.OnExit(e);
            }
        }

        private static void ConfigureConfiguration(ConfigurationManager configuration)
        {
            configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
        }

        private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton<MainWindow>();
            services.AddSimulcastInfrastructure(configuration);
            services.AddSimulcastApplication();
            services.AddSimulcastPlugins(configuration);
            services.AddSimulcastWpf();
            services.Configure<NotificationOptions>(configuration.GetSection(NotificationOptions.SectionName));
        }

        private static void DeleteExpiredLogs(string logDirectory, int retentionDays)
        {
            if (retentionDays <= 0)
                return;

            DateTime expirationTimeUtc = DateTime.UtcNow.AddDays(-retentionDays);

            foreach (string logFilePath in Directory.EnumerateFiles(logDirectory, "SimulcastUtility_*.log"))
            {
                try
                {
                    DateTime lastWriteTimeUtc = File.GetLastWriteTimeUtc(logFilePath);

                    if (lastWriteTimeUtc < expirationTimeUtc)
                        File.Delete(logFilePath);
                }
                catch
                {
                    // Logging is not initialized yet, so cleanup failures are intentionally ignored. •‿•
                }
            }
        }

        private static void ConfigureLogging(HostApplicationBuilder builder)
        {
            LoggingOptions loggingOptions = new();
            builder.Configuration.GetSection(LoggingOptions.SectionName).Bind(loggingOptions);
            string logDirectory = loggingOptions.Directory;

            Directory.CreateDirectory(logDirectory);

            int retentionDays = loggingOptions.RetentionDays;

            DeleteExpiredLogs(logDirectory, retentionDays);

            string sessionTimestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-fff");
            string logFilePath = Path.Combine(logDirectory, $"SimulcastUtility_{sessionTimestamp}.log");

            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(builder.Configuration)
                .WriteTo.Debug()
                .WriteTo.File(
                    logFilePath,
                    rollingInterval: RollingInterval.Infinite,
                    retainedFileCountLimit: null,
                    retainedFileTimeLimit: TimeSpan.FromDays(retentionDays),
                    shared: false,
                    flushToDiskInterval: TimeSpan.FromSeconds(1))
                .CreateLogger();

            builder.Logging.ClearProviders();
            builder.Logging.AddSerilog(Log.Logger, dispose: true);
        }
    }

}
