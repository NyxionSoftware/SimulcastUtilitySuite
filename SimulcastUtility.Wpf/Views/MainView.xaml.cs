using SimulcastUtility.Wpf.ViewModels.Views;
using SimulcastUtility.Plugins.Interfaces;
using System.Windows;
using System.Windows.Controls;

namespace SimulcastUtility.Wpf.Views
{
    public partial class MainView : UserControl
    {
        private readonly MainViewModel _viewModel;
        private bool _hasPerformedInitialRefresh;

        public MainView(MainViewModel viewModel, IPluginUiManager pluginUiManager)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = viewModel;
            pluginUiManager.RegisterVisualRoot(this);
        }

        private async void MainView_Loaded(object sender, RoutedEventArgs e)
        {
            if (_hasPerformedInitialRefresh)
                return;

            _hasPerformedInitialRefresh = true;
            await _viewModel.RefreshAllReceiversCommand.ExecuteAsync(null);
        }
    }
}
