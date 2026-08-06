using CommunityToolkit.Mvvm.ComponentModel;
using SimulcastUtility.Plugins.Models;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using System.Windows;

namespace SimulcastUtility.Wpf.ViewModels.Models
{
    public sealed class PluginSettingViewModel : ObservableObject, IDisposable
    {
        private readonly Func<string, CancellationToken, Task<FrameworkElement?>>? _previewFactory;
        private bool _booleanValue;
        private string _textValue = string.Empty;
        private PluginSettingOptionViewModel? _selectedOption;
        private FrameworkElement? _previewContent;
        private string _previewError = string.Empty;
        private CancellationTokenSource? _previewCancellation;

        public PluginSettingViewModel(PluginSettingDescriptor descriptor, Func<string, CancellationToken, Task<FrameworkElement?>>? previewFactory = null)
        {
            _previewFactory = previewFactory;
            Key = descriptor.Key;
            Name = descriptor.Name;
            Description = descriptor.Description;
            Group = descriptor.Group;
            SelectedItemsName = descriptor.SelectedItemsName;
            ControlType = descriptor.ControlType;
            _booleanValue = (descriptor.ControlType is PluginSettingControlType.Toggle or PluginSettingControlType.Checkbox) && descriptor.Value.ValueKind == JsonValueKind.True;
            _textValue = descriptor.Value.ValueKind == JsonValueKind.String ? descriptor.Value.GetString() ?? string.Empty : descriptor.Value.ToString();
            HashSet<string> selectedValues = (descriptor.ControlType is PluginSettingControlType.MultiCheckbox or PluginSettingControlType.SideBySideList) && descriptor.Value.ValueKind == JsonValueKind.Array
                ? descriptor.Value.EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToHashSet(StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Options = new ObservableCollection<PluginSettingOptionViewModel>(descriptor.Options.Select(option => new PluginSettingOptionViewModel(option.Value, option.DisplayName, selectedValues.Contains(option.Value))));
            AvailableOptions = new ObservableCollection<PluginSettingOptionViewModel>(Options.Where(option => !option.IsSelected));
            SelectedOptions = new ObservableCollection<PluginSettingOptionViewModel>(Options.Where(option => option.IsSelected));
            _selectedOption = Options.FirstOrDefault(option => option.Value == _textValue);

            if (ControlType == PluginSettingControlType.ThemeSelection && _selectedOption is not null)
                _ = LoadPreviewAsync(_selectedOption.Value);
        }

        public string Key { get; }

        public string Name { get; }

        public string Description { get; }

        public string Group { get; }

        public string SelectedItemsName { get; }

        public PluginSettingControlType ControlType { get; }

        public ObservableCollection<PluginSettingOptionViewModel> Options { get; }

        public ObservableCollection<PluginSettingOptionViewModel> AvailableOptions { get; }

        public ObservableCollection<PluginSettingOptionViewModel> SelectedOptions { get; }

        public Visibility ToggleVisibility => ControlType == PluginSettingControlType.Toggle ? Visibility.Visible : Visibility.Collapsed;

        public Visibility CheckboxVisibility => ControlType == PluginSettingControlType.Checkbox ? Visibility.Visible : Visibility.Collapsed;

        public Visibility TextVisibility => ControlType == PluginSettingControlType.Text ? Visibility.Visible : Visibility.Collapsed;

        public Visibility NumericVisibility => ControlType == PluginSettingControlType.Numeric ? Visibility.Visible : Visibility.Collapsed;

        public Visibility DropdownVisibility => ControlType is PluginSettingControlType.Dropdown or PluginSettingControlType.ThemeSelection ? Visibility.Visible : Visibility.Collapsed;

        public Visibility ThemeSelectionVisibility => ControlType == PluginSettingControlType.ThemeSelection ? Visibility.Visible : Visibility.Collapsed;

        public Visibility MultiCheckboxVisibility => ControlType == PluginSettingControlType.MultiCheckbox ? Visibility.Visible : Visibility.Collapsed;

        public Visibility SideBySideListVisibility => ControlType == PluginSettingControlType.SideBySideList ? Visibility.Visible : Visibility.Collapsed;

        public bool BooleanValue
        {
            get => _booleanValue;
            set => SetProperty(ref _booleanValue, value);
        }

        public string TextValue
        {
            get => _textValue;
            set => SetProperty(ref _textValue, value);
        }

        public PluginSettingOptionViewModel? SelectedOption
        {
            get => _selectedOption;
            set
            {
                if (!SetProperty(ref _selectedOption, value) || ControlType != PluginSettingControlType.ThemeSelection || value is null)
                    return;

                _ = LoadPreviewAsync(value.Value);
            }
        }

        public FrameworkElement? PreviewContent
        {
            get => _previewContent;
            private set => SetProperty(ref _previewContent, value);
        }

        public string PreviewError
        {
            get => _previewError;
            private set
            {
                if (SetProperty(ref _previewError, value))
                    OnPropertyChanged(nameof(PreviewErrorVisibility));
            }
        }

        public Visibility PreviewErrorVisibility => string.IsNullOrWhiteSpace(PreviewError) ? Visibility.Collapsed : Visibility.Visible;

        public JsonElement CreateValue()
        {
            return ControlType switch
            {
                PluginSettingControlType.Toggle => JsonSerializer.SerializeToElement(BooleanValue),
                PluginSettingControlType.Checkbox => JsonSerializer.SerializeToElement(BooleanValue),
                PluginSettingControlType.Numeric => CreateNumericValue(),
                PluginSettingControlType.MultiCheckbox => JsonSerializer.SerializeToElement(Options.Where(option => option.IsSelected).Select(option => option.Value).ToArray()),
                PluginSettingControlType.SideBySideList => JsonSerializer.SerializeToElement(SelectedOptions.Select(option => option.Value).ToArray()),
                PluginSettingControlType.Dropdown => JsonSerializer.SerializeToElement(SelectedOption?.Value ?? string.Empty),
                PluginSettingControlType.ThemeSelection => JsonSerializer.SerializeToElement(SelectedOption?.Value ?? string.Empty),
                _ => JsonSerializer.SerializeToElement(TextValue)
            };
        }

        public void Dispose()
        {
            _previewCancellation?.Cancel();
            _previewCancellation?.Dispose();
            _previewCancellation = null;
            PreviewContent = null;
        }

        private async Task LoadPreviewAsync(string selectedValue)
        {
            _previewCancellation?.Cancel();
            _previewCancellation?.Dispose();
            _previewCancellation = new CancellationTokenSource();
            CancellationToken cancellationToken = _previewCancellation.Token;
            PreviewContent = null;
            PreviewError = string.Empty;

            if (_previewFactory is null)
            {
                PreviewError = "This plugin does not provide a preview for this setting.";
                return;
            }

            try
            {
                FrameworkElement? previewContent = await _previewFactory(selectedValue, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                PreviewContent = previewContent;

                if (PreviewContent is null)
                    PreviewError = "No preview is available for this selection.";
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                PreviewError = $"Preview unavailable: {exception.Message}";
            }
        }

        private JsonElement CreateNumericValue()
        {
            if (!decimal.TryParse(TextValue, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value))
                throw new FormatException($"'{Name}' must contain a valid number.");

            return JsonSerializer.SerializeToElement(value);
        }

        public void MoveOption(PluginSettingOptionViewModel option, bool isSelected)
        {
            AvailableOptions.Remove(option);
            SelectedOptions.Remove(option);
            option.IsSelected = isSelected;

            if (isSelected)
                InsertSorted(SelectedOptions, option);
            else
                InsertSorted(AvailableOptions, option);
        }

        private static void InsertSorted(ObservableCollection<PluginSettingOptionViewModel> collection, PluginSettingOptionViewModel option)
        {
            int index = 0;

            while (index < collection.Count && string.Compare(collection[index].DisplayName, option.DisplayName, StringComparison.CurrentCultureIgnoreCase) < 0)
                index++;

            collection.Insert(index, option);
        }
    }
}
