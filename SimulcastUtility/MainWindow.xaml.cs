using SimulcastUtility.Plugins.Interfaces;
using SimulcastUtility.Plugins.Models;
using SimulcastUtility.Application.Interfaces;
using SimulcastUtility.Wpf.ViewModels.Models;
using SimulcastUtility.Wpf.ViewModels.Views;
using SimulcastUtility.Wpf.Views;
using SimulcastUtility.Wpf.Services;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

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

        public MainWindow(MainView mainView, MainViewModel viewModel, IPluginManager pluginManager, IPluginApplicationDispatcher pluginApplicationDispatcher, IReceiverCommandManager receiverCommandManager, IReceiverManager receiverManager)
        {
            InitializeComponent();
            _mainView = mainView;
            _mainViewModel = viewModel;
            _receiverCommandManager = receiverCommandManager;
            _receiverManager = receiverManager;
            _pluginManager = pluginManager;
            _pluginApplicationDispatcher = pluginApplicationDispatcher;
            viewModel.UpdateLoadedPluginCount(pluginManager.Plugins.Count);
            pluginManager.PluginsChanged += PluginsChanged;
            pluginApplicationDispatcher.CommandDispatched += PluginCommandDispatched;
            viewModel.VirtualRemoteRequested += OpenVirtualRemote;
            viewModel.ManageReceiversRequested += (_, _) => OpenReceiverManager();
            viewModel.ManagePluginsRequested += (_, _) => OpenPluginManager();
            viewModel.AddReceiverRequested += (_, _) => OpenReceiverManager(beginAdd: true);
            viewModel.EditReceiverRequested += receiver => OpenReceiverManager(receiver.Id);
            ViewHost.Content = mainView;
        }

        private void OpenPluginManager()
        {
            PluginManagerViewModel viewModel = new(_pluginManager);
            PluginManagerView view = new(viewModel);
            viewModel.CloseRequested += (_, _) =>
            {
                viewModel.Dispose();
                NavigateTo(_mainView, slideFromRight: false);
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
                    NavigateTo(_mainView, slideFromRight: false);
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
                    NavigateTo(_mainView, slideFromRight: false);
                    break;
            }
        }

        private void OpenReceiverManager(Guid? receiverToEdit = null, bool beginAdd = false)
        {
            ReceiverManagerViewModel viewModel = new(_receiverManager, _receiverCommandManager, _mainViewModel, receiverToEdit, beginAdd);
            ReceiverManagerView view = new(viewModel);

            viewModel.CloseRequested += (_, _) =>
            {
                viewModel.Dispose();
                NavigateTo(_mainView, slideFromRight: false);
            };

            NavigateTo(view, slideFromRight: true);
        }

        private void NavigateTo(UIElement view, bool slideFromRight)
        {
            ViewHost.Content = view;

            TranslateTransform transform = new(slideFromRight ? ActualWidth : -ActualWidth, 0);
            view.RenderTransform = transform;

            DoubleAnimation animation = new()
            {
                From = transform.X,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(260),
                EasingFunction = new CubicEase() { EasingMode = EasingMode.EaseOut }
            };

            transform.BeginAnimation(TranslateTransform.XProperty, animation);
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
