using SimulcastUtility.Wpf.ViewModels.Views;

namespace SimulcastUtility.Wpf.Views
{
    public partial class ApplicationSettingsView : System.Windows.Controls.UserControl
    {
        public ApplicationSettingsView(ApplicationSettingsViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
