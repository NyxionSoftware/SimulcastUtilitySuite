using SimulcastUtility.Handlers;
using SimulcastUtility.Plugin.Abstractions.Interfaces;
using SimulcastUtility.Plugins;
using SimulcastUtility.Shared.Commands;
using SimulcastUtility.Shared.Models;
using SimulcastUtility.ViewModels;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace SimulcastUtility
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MainWindowViewModel _viewModel;
        private readonly IReceiverControllerService _receiverController;

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19;
        private const uint MONITOR_DEFAULTTONEAREST = 2;

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(WINPOINT point, uint flags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO monitorInfo);

        [StructLayout(LayoutKind.Sequential)]
        private struct WINPOINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WINRECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int Size;
            public WINRECT Monitor;
            public WINRECT WorkArea;
            public uint Flags;
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int attributeValue, int attributeSize);

        public MainWindow(MainWindowViewModel viewModel, IReceiverControllerService receiverController)
        {
            InitializeComponent();

            _viewModel = viewModel;

            _receiverController = receiverController;

            DataContext = _viewModel;

            _viewModel.ChannelChangedSuccessfully += ViewModel_ChannelChangedSuccessfully;

            Closing += MainWindow_Closing;
            SourceInitialized += Window_SourceInitialized;

            receiverController.InitializeAsync();
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

        private void WindowDragBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                WindowState = (WindowState == WindowState.Maximized) ? WindowState.Normal : WindowState.Maximized;

                return;
            }

            if (e.ButtonState == MouseButtonState.Pressed)
            {
                if(WindowState == WindowState.Maximized)
                {
                    Point mousePosition = e.GetPosition(this);
                    Point mousePositionOnScreen = PointToScreen(mousePosition);
                    double horizontalPercent = mousePosition.X / ActualWidth;
                    var monitorPoint = new WINPOINT
                    {
                        X = (int)mousePositionOnScreen.X,
                        Y = (int)mousePositionOnScreen.Y
                    };
                    IntPtr monitor = MonitorFromPoint(monitorPoint, MONITOR_DEFAULTTONEAREST);
                    var monitorInfo = new MONITORINFO
                    {
                        Size = Marshal.SizeOf<MONITORINFO>()
                    };
                    GetMonitorInfo(monitor, ref monitorInfo);
                    PresentationSource? source = PresentationSource.FromVisual(this);
                    Matrix fromDevice = source?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
                    Point mousePositionDip = fromDevice.Transform(mousePositionOnScreen);
                    Point workAreaTopLeftDip = fromDevice.Transform(new Point(monitorInfo.WorkArea.Left, monitorInfo.WorkArea.Top));
                    double restoredWidth = RestoreBounds.Width;
                    WindowState = WindowState.Normal;
                    Left = mousePositionDip.X - (restoredWidth * horizontalPercent);
                    Top = workAreaTopLeftDip.Y;
                }
                DragMove();
            }
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            _viewModel.ChannelChangedSuccessfully -= ViewModel_ChannelChangedSuccessfully;

            _viewModel.Dispose();
        }

        private async void ViewModel_ChannelChangedSuccessfully(object? sender, bool IsSuccessful)
        {
            Style originalStyle = (Style)FindResource("PrimaryButtonStyle");

            try
            {
                if (IsSuccessful) 
                    SetChannelButton.Style = (Style)FindResource("SuccessButtonStyle");
                else
                    SetChannelButton.Style = (Style)FindResource("DangerButtonStyle");

                await Task.Delay(2000);
            }
            finally
            {
                SetChannelButton.Style = originalStyle;
            }
        }

        private void VirtualRemoteButton_Click(object sender, RoutedEventArgs e)
        {
            Receiver? receiver = _viewModel.SelectedReceiver;

            if (receiver is null)
                return;

            var viewModel = new VirtualRemoteViewModel(_receiverController, receiver);

            var window = new VirtualRemoteWindow(viewModel)
            {
                Owner = this
            };

            window.ShowDialog();
        }

        private void ManageReceiversButton_Click(object sender, RoutedEventArgs e)
        {
            OpenReceiverConfiguration();
        }

        private void AddReceiverButton_Click(object sender, RoutedEventArgs e)
        {
            var viewModel = new ReceiverConfigurationViewModel(_receiverController);

            viewModel.BeginAddReceiver();

            var window = new ReceiverConfigurationWindow(viewModel)
            {
                Owner = this
            };

            window.ShowDialog();
        }

        private void EditReceiverButton_Click(object sender, RoutedEventArgs e)
        {
            var viewModel = new ReceiverConfigurationViewModel(_receiverController);

            if (_viewModel.SelectedReceiver is not null)
            {
                viewModel.SelectReceiver(_viewModel.SelectedReceiver);
            }

            var window = new ReceiverConfigurationWindow(viewModel)
            {
                Owner = this
            };

            window.ShowDialog();
        }

        private void OpenReceiverConfiguration()
        {
            var viewModel = new ReceiverConfigurationViewModel(_receiverController);

            var window = new ReceiverConfigurationWindow(viewModel)
            {
                Owner = this
            };

            window.ShowDialog();
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.RefreshAllCommand.CanExecute(null))
            {
                _viewModel.RefreshAllCommand.Execute(null);
            }
        }

        private void RefreshSelectedReceiverButton_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.RefreshSelectedCommand.CanExecute(null))
            {
                _viewModel.RefreshSelectedCommand.Execute(null);
            }
        }

        private void SetChannelButton_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.SetChannelCommand.CanExecute(null))
            {
                _viewModel.SetChannelCommand.Execute(null);
            }
        }

        private void ChannelTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, "^[0-9]+$");
        }
    }
}