using Microsoft.VisualBasic;
using SimulcastUtility.Handlers;
using SimulcastUtility.Shared.Commands;
using SimulcastUtility.Shared.Models;
using SimulcastUtility.ViewModels;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace SimulcastUtility
{
    /// <summary>
    /// Interaction logic for VirtualRemoteWindow.xaml
    /// </summary>
    public partial class VirtualRemoteWindow : Window
    {
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(
            IntPtr hwnd,
            int attribute,
            ref int attributeValue,
            int attributeSize);

        private readonly VirtualRemoteViewModel _viewModel;

        public VirtualRemoteWindow(VirtualRemoteViewModel viewModel)
        {
            ArgumentNullException.ThrowIfNull(viewModel);

            InitializeComponent();

            _viewModel = viewModel;
            DataContext = _viewModel;

            SourceInitialized += Window_SourceInitialized;
            Closed += VirtualRemoteWindow_Closed;
        }

        private void VirtualRemoteWindow_Closed(object? sender, EventArgs e)
        {
            _viewModel.Dispose();
        }

        private void Window_SourceInitialized(object? sender, EventArgs e)
        {
            IntPtr windowHandle = new WindowInteropHelper(this).Handle;

            int enabled = 1;

            int result = DwmSetWindowAttribute(
                windowHandle,
                DWMWA_USE_IMMERSIVE_DARK_MODE,
                ref enabled,
                sizeof(int));

            // Some older Windows 10 versions use attribute 19.
            if (result != 0)
            {
                DwmSetWindowAttribute(
                    windowHandle,
                    DWMWA_USE_IMMERSIVE_DARK_MODE_OLD,
                    ref enabled,
                    sizeof(int));
            }
        }
    }
}
