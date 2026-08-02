using Microsoft.Win32;
using SimulcastUtility.Wpf.Controls;
using SimulcastUtility.Wpf.ViewModels.Models;
using SimulcastUtility.Wpf.ViewModels.Views;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace SimulcastUtility.Wpf.Views
{
    public partial class PluginManagerView : UserControl
    {
        private Point _settingDragStartPoint;
        private PluginSettingOptionViewModel? _draggedSettingOption;
        private ListBoxItem? _draggedSettingContainer;
        private ReceiverDragAdorner? _settingDragAdorner;
        private AdornerLayer? _settingAdornerLayer;
        private ListBox? _highlightedSettingDropList;

        public PluginManagerView(PluginManagerViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            viewModel.ImportRequested += ImportRequested;
            viewModel.OpenPluginDirectoryRequested += OpenPluginDirectoryRequested;
        }

        private void OpenPluginDirectoryRequested(object? sender, EventArgs e)
        {
            PluginManagerViewModel viewModel = (PluginManagerViewModel)DataContext;
            Directory.CreateDirectory(viewModel.PluginDirectory);
            Process.Start(new ProcessStartInfo("explorer.exe", viewModel.PluginDirectory) { UseShellExecute = true });
        }

        private async void ImportRequested(object? sender, EventArgs e)
        {
            OpenFileDialog dialog = new()
            {
                Title = "Import Plugin",
                Filter = "Plugin files (*.zip;*.dll)|*.zip;*.dll|ZIP archives (*.zip)|*.zip|Plugin assemblies (*.dll)|*.dll",
                Multiselect = true,
                CheckFileExists = true
            };

            if (dialog.ShowDialog() != true)
                return;

            bool containsZip = dialog.FileNames.Any(path => string.Equals(Path.GetExtension(path), ".zip", StringComparison.OrdinalIgnoreCase));

            if (containsZip && dialog.FileNames.Length != 1)
            {
                MessageBox.Show("Select either one ZIP archive or one or more DLL files.", "Invalid Plugin Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            await ((PluginManagerViewModel)DataContext).ImportAsync(dialog.FileNames);
        }

        private void SettingOptionsList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not ListBox listBox)
                return;

            _settingDragStartPoint = e.GetPosition(listBox);
            _draggedSettingContainer = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject);
            _draggedSettingOption = _draggedSettingContainer?.DataContext as PluginSettingOptionViewModel;
        }

        private void SettingOptionsList_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || sender is not ListBox listBox || _draggedSettingOption is null || _draggedSettingContainer is null)
                return;

            Point currentPosition = e.GetPosition(listBox);

            if (Math.Abs(currentPosition.X - _settingDragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(currentPosition.Y - _settingDragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
                return;

            BeginSettingDrag(_draggedSettingContainer, e.GetPosition(_draggedSettingContainer));

            try
            {
                DragDrop.DoDragDrop(listBox, new DataObject(typeof(PluginSettingOptionViewModel), _draggedSettingOption), DragDropEffects.Move);
            }
            finally
            {
                EndSettingDrag();
            }
        }

        private void SettingOptionsList_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(typeof(PluginSettingOptionViewModel)) ? DragDropEffects.Move : DragDropEffects.None;

            if (sender is ListBox listBox && e.Effects == DragDropEffects.Move)
            {
                HighlightSettingDropList(listBox);
                _settingDragAdorner?.UpdatePosition(e.GetPosition(this));
            }

            e.Handled = true;
        }

        private void SettingOptionsList_Drop(object sender, DragEventArgs e)
        {
            if (sender is not ListBox listBox || listBox.DataContext is not PluginSettingViewModel setting || e.Data.GetData(typeof(PluginSettingOptionViewModel)) is not PluginSettingOptionViewModel option)
                return;

            bool isSelected = string.Equals(listBox.Tag?.ToString(), bool.TrueString, StringComparison.OrdinalIgnoreCase);
            setting.MoveOption(option, isSelected);
            HighlightSettingDropList(null);
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
        }

        private void BeginSettingDrag(ListBoxItem draggedContainer, Point grabOffset)
        {
            _settingAdornerLayer = AdornerLayer.GetAdornerLayer(this);

            if (_settingAdornerLayer is not null)
            {
                _settingDragAdorner = new ReceiverDragAdorner(this, draggedContainer, grabOffset);
                _settingAdornerLayer.Add(_settingDragAdorner);
                _settingDragAdorner.UpdatePosition(Mouse.GetPosition(this));
            }

            draggedContainer.Opacity = 0.2;
        }

        private void EndSettingDrag()
        {
            if (_settingAdornerLayer is not null && _settingDragAdorner is not null)
                _settingAdornerLayer.Remove(_settingDragAdorner);

            if (_draggedSettingContainer is not null)
                _draggedSettingContainer.Opacity = 1;

            HighlightSettingDropList(null);
            _settingDragAdorner = null;
            _settingAdornerLayer = null;
            _draggedSettingContainer = null;
            _draggedSettingOption = null;
        }

        private void HighlightSettingDropList(ListBox? listBox)
        {
            if (_highlightedSettingDropList is not null && !ReferenceEquals(_highlightedSettingDropList, listBox))
                _highlightedSettingDropList.ClearValue(BackgroundProperty);

            _highlightedSettingDropList = listBox;

            if (listBox is not null)
                listBox.Background = (Brush)FindResource("AccentMutedBrush");
        }

        private static TElement? FindAncestor<TElement>(DependencyObject? element) where TElement : DependencyObject
        {
            while (element is not null && element is not TElement)
                element = VisualTreeHelper.GetParent(element);

            return element as TElement;
        }
    }
}
