using SimulcastUtility.Wpf.ViewModels.Views;
using System.Windows.Controls;

namespace SimulcastUtility.Wpf.Views
{
    public partial class VirtualRemoteView : UserControl
    {
        public VirtualRemoteView(VirtualRemoteViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
