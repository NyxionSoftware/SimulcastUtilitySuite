namespace SimulcastUtility.Plugins.Models
{
    public sealed record PluginNotificationRequest(string Title, string Message, PluginNotificationSeverity Severity);
}
