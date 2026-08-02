using CommunityToolkit.Mvvm.ComponentModel;

namespace SimulcastUtility.Wpf.ViewModels.Models
{
    public sealed class PluginSettingOptionViewModel : ObservableObject
    {
        private bool _isSelected;

        public PluginSettingOptionViewModel(string value, string displayName, bool isSelected)
        {
            Value = value;
            DisplayName = displayName;
            _isSelected = isSelected;
        }

        public string Value { get; }

        public string DisplayName { get; }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
    }
}
