using System.Windows;

namespace SimulcastUtility.Plugins.Models
{
    public sealed record PluginDialogRequest(string Title, FrameworkElement Content, double Width = 560, double Height = 420);
}
