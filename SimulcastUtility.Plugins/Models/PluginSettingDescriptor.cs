using System.Text.Json;

namespace SimulcastUtility.Plugins.Models
{
    public sealed record PluginSettingDescriptor(string Key, string Name, string Description, string Group, int Order, PluginSettingControlType ControlType, string SelectedItemsName, JsonElement Value, IReadOnlyList<PluginSettingOption> Options);
}
