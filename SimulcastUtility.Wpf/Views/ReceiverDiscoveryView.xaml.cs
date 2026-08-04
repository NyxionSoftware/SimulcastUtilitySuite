using SimulcastUtility.Wpf.ViewModels.Views;
using System.Windows.Controls;

namespace SimulcastUtility.Wpf.Views
{
    public partial class ReceiverDiscoveryView : UserControl
    {
        public ReceiverDiscoveryView(ReceiverDiscoveryViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
