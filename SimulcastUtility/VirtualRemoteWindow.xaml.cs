using SimulcastUtility.Wpf.Services;
using SimulcastUtility.Wpf.Views;
using System.Windows;

namespace SimulcastUtility
{
    public partial class VirtualRemoteWindow : Window
    {
        public VirtualRemoteWindow(VirtualRemoteView view)
        {
            InitializeComponent();
            ViewHost.Content = view;
        }

        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            WindowChromeService.ApplyToWindow(this);
        }
    }
}
