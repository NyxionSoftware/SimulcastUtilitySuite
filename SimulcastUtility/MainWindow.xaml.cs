using SimulcastUtility.Plugins.Interfaces;
using SimulcastUtility.Plugins.Models;
using SimulcastUtility.Application.Interfaces;
using SimulcastUtility.Wpf.ViewModels.Models;
using SimulcastUtility.Wpf.ViewModels.Views;
using SimulcastUtility.Wpf.Views;
using SimulcastUtility.Wpf.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace SimulcastUtility
{
    public partial class MainWindow : Window
    {
        private readonly Dictionary<Guid, VirtualRemoteWindow> _virtualRemoteWindows = new();
        private readonly IReceiverCommandManager _receiverCommandManager;
        private readonly IReceiverManager _receiverManager;
        private readonly MainView _mainView;
        private readonly MainViewModel _mainViewModel;
        private readonly IPluginManager _pluginManager;
        private readonly IPluginApplicationDispatcher _pluginApplicationDispatcher;
        private readonly IServiceProvider _serviceProvider;
        private readonly ApplicationNavigationService _navigationService;

        public MainWindow(MainView mainView, MainViewModel viewModel, IPluginManager pluginManager, IPluginApplicationDispatcher pluginApplicationDispatcher, IReceiverCommandManager receiverCommandManager, IReceiverManager receiverManager, IServiceProvider serviceProvider, ApplicationNavigationService navigationService, ApplicationOverlayViewModel overlayViewModel)
        {
            InitializeComponent();
            _mainView = mainView;
            _mainViewModel = viewModel;
            _receiverCommandManager = receiverCommandManager;
            _receiverManager = receiverManager;
            _pluginManager = pluginManager;
            _pluginApplicationDispatcher = pluginApplicationDispatcher;
            _serviceProvider = serviceProvider;
            _navigationService = navigationService;
            viewModel.UpdateLoadedPluginCount(pluginManager.Plugins.Count);
            pluginManager.PluginsChanged += PluginsChanged;
            pluginApplicationDispatcher.CommandDispatched += PluginCommandDispatched;
            viewModel.VirtualRemoteRequested += OpenVirtualRemote;
            viewModel.ManageReceiversRequested += (_, _) => OpenReceiverManager();
            viewModel.ManagePluginsRequested += (_, _) => OpenPluginManager();
            viewModel.AddReceiverRequested += (_, _) => OpenReceiverManager(beginAdd: true);
            viewModel.EditReceiverRequested += receiver => OpenReceiverManager(receiver.Id);
            viewModel.SettingsRequested += (_, _) => OpenSettings();
            DataContext = overlayViewModel;
            _navigationService.Initialize(ViewHost, mainView);
        }

        private void OpenSettings()
        {
            ApplicationSettingsViewModel viewModel = _serviceProvider.GetRequiredService<ApplicationSettingsViewModel>();
            ApplicationSettingsView view = new(viewModel);
            viewModel.CloseRequested += (_, _) => _navigationService.NavigateBack();
            NavigateTo(view, slideFromRight: true);
        }

        private void OpenPluginManager()
        {
            PluginManagerViewModel viewModel = new(_pluginManager);
            PluginManagerView view = new(viewModel);
            viewModel.CloseRequested += (_, _) =>
            {
                viewModel.Dispose();
                _navigationService.NavigateBack();
            };
            NavigateTo(view, slideFromRight: true);
        }

        protected override void OnClosed(EventArgs e)
        {
            _pluginManager.PluginsChanged -= PluginsChanged;
            _pluginApplicationDispatcher.CommandDispatched -= PluginCommandDispatched;
            base.OnClosed(e);
        }

        private void PluginsChanged(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() => _mainViewModel.UpdateLoadedPluginCount(_pluginManager.Plugins.Count));
        }

        private void PluginCommandDispatched(object? sender, PluginApplicationCommand command)
        {
            Dispatcher.Invoke(() => HandlePluginCommand(command));
        }

        private void HandlePluginCommand(PluginApplicationCommand command)
        {
            switch (command.Name.Trim().ToLowerInvariant())
            {
                case "show-main":
                    _navigationService.NavigateToFallback(clearHistory: true);
                    Activate();
                    break;
                case "manage-receivers":
                    OpenReceiverManager();
                    break;
                case "add-receiver":
                    OpenReceiverManager(beginAdd: true);
                    break;
                case "select-receiver" when command.Arguments.TryGetValue("receiverId", out string? receiverIdValue) && Guid.TryParse(receiverIdValue, out Guid receiverId):
                    _receiverManager.SelectReceiver(receiverId);
                    _navigationService.NavigateToFallback(clearHistory: true);
                    break;
            }
        }

        private void OpenReceiverManager(Guid? receiverToEdit = null, bool beginAdd = false, bool slideFromRight = true)
        {
            ReceiverManagerViewModel viewModel = new(_receiverManager, _receiverCommandManager, _mainViewModel, receiverToEdit, beginAdd);
            ReceiverManagerView view = new(viewModel);

            viewModel.CloseRequested += (_, _) =>
            {
                viewModel.Dispose();
                _navigationService.NavigateBack();
            };

            viewModel.DiscoverReceiversRequested += (_, _) =>
            {
                OpenReceiverDiscovery();
            };

            NavigateTo(view, slideFromRight);
        }

        private void OpenReceiverDiscovery()
        {
            ReceiverDiscoveryViewModel viewModel = new(_receiverManager, _receiverCommandManager, _mainViewModel);
            ReceiverDiscoveryView view = new(viewModel);
            viewModel.BackRequested += (_, _) =>
            {
                viewModel.Dispose();
                _navigationService.NavigateBack();
            };
            NavigateTo(view, slideFromRight: true);
        }

        private void NavigateTo(UIElement view, bool slideFromRight)
        {
            _navigationService.NavigateTo(view, slideFromRight);
        }

        private void OpenVirtualRemote(ReceiverViewModel receiver)
        {
            if (_virtualRemoteWindows.TryGetValue(receiver.Id, out VirtualRemoteWindow? existingWindow))
            {
                if (existingWindow.WindowState == WindowState.Minimized)
                    existingWindow.WindowState = WindowState.Normal;

                existingWindow.Activate();
                existingWindow.Focus();
                return;
            }

            VirtualRemoteViewModel viewModel = new(_receiverCommandManager, receiver);
            VirtualRemoteView view = new(viewModel);
            VirtualRemoteWindow remoteWindow = new(view)
            {
                Owner = this,
                Title = $"Virtual Remote - {receiver.Name}"
            };

            _virtualRemoteWindows.Add(receiver.Id, remoteWindow);
            remoteWindow.Closed += (_, _) =>
            {
                viewModel.Dispose();
                _virtualRemoteWindows.Remove(receiver.Id);
            };
            remoteWindow.Show();
        }

        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            WindowChromeService.ApplyToWindow(this);
        }
    }
}
