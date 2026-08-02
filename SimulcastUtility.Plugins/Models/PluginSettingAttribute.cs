namespace SimulcastUtility.Plugins.Models
{
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class PluginSettingAttribute : Attribute
    {
        public PluginSettingAttribute(string name, string description, PluginSettingControlType controlType)
        {
            Name = name;
            Description = description;
            ControlType = controlType;
        }

        public string Name { get; }

        public string Description { get; }

        public PluginSettingControlType ControlType { get; }

        public string Group { get; set; } = "General";

        public int Order { get; set; }

        public string SelectedItemsName { get; set; } = "Selected";
    }
}
