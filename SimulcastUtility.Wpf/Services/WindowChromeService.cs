using SimulcastUtility.Plugins.Models;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace SimulcastUtility.Wpf.Services
{
    public static class WindowChromeService
    {
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19;

        public static PluginWindowChromeMode CurrentMode { get; private set; } = PluginWindowChromeMode.Dark;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int attributeValue, int attributeSize);

        public static void SetMode(PluginWindowChromeMode mode)
        {
            CurrentMode = mode;

            foreach (Window window in System.Windows.Application.Current.Windows)
                ApplyToWindow(window);
        }

        public static void ApplyToWindow(Window window)
        {
            IntPtr windowHandle = new WindowInteropHelper(window).Handle;

            if (windowHandle == IntPtr.Zero)
                return;

            int enabled = CurrentMode == PluginWindowChromeMode.Dark ? 1 : 0;
            int result = DwmSetWindowAttribute(windowHandle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref enabled, sizeof(int));

            if (result != 0)
                DwmSetWindowAttribute(windowHandle, DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, ref enabled, sizeof(int));
        }
    }
}
