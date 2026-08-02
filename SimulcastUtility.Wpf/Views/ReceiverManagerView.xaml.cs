using SimulcastUtility.Wpf.Controls;
using SimulcastUtility.Wpf.ViewModels.Models;
using SimulcastUtility.Wpf.ViewModels.Views;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace SimulcastUtility.Wpf.Views
{
    public partial class ReceiverManagerView : UserControl
    {
        private Point _dragStartPoint;
        private ReceiverConfigurationItemViewModel? _draggedReceiver;
        private ReceiverDragAdorner? _dragAdorner;
        private ListBoxItem? _draggedContainer;
        private AdornerLayer? _adornerLayer;
        private int _dropIndex = -1;
        private int _originalDragIndex = -1;
        private bool _dropCompleted;

        public ReceiverManagerView(ReceiverManagerViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            Loaded += ReceiverManagerView_Loaded;
        }

        private void ReceiverManagerView_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= ReceiverManagerView_Loaded;

            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
            {
                if (DataContext is not ReceiverManagerViewModel { SelectedReceiver: not null } viewModel)
                    return;

                ReceiverListBox.UpdateLayout();
                ReceiverListBox.ScrollIntoView(viewModel.SelectedReceiver);
            });
        }

        private void ReceiverListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(ReceiverListBox);
            _draggedReceiver = GetReceiverFromElement(e.OriginalSource as DependencyObject);
        }

        private void ReceiverListBox_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _draggedReceiver is null)
                return;

            Point currentPosition = e.GetPosition(ReceiverListBox);

            if (Math.Abs(currentPosition.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(currentPosition.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
                return;

            if (ReceiverListBox.ItemContainerGenerator.ContainerFromItem(_draggedReceiver) is not ListBoxItem draggedContainer)
                return;

            BeginDragVisuals(draggedContainer, currentPosition);
            _originalDragIndex = ReceiverListBox.Items.IndexOf(_draggedReceiver);
            _dropCompleted = false;

            try
            {
                DragDropEffects result = DragDrop.DoDragDrop(ReceiverListBox, new DataObject(typeof(ReceiverConfigurationItemViewModel), _draggedReceiver), DragDropEffects.Move);

                if ((result != DragDropEffects.Move || !_dropCompleted) && DataContext is ReceiverManagerViewModel viewModel)
                    viewModel.RestoreReceiverPosition(_draggedReceiver, _originalDragIndex);
            }
            finally
            {
                EndDragVisuals();
                _draggedReceiver = null;
            }
        }

        private void ReceiverListBox_PreviewDragOver(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(typeof(ReceiverConfigurationItemViewModel)))
            {
                e.Effects = DragDropEffects.None;
                return;
            }

            Point position = e.GetPosition(ReceiverListBox);
            _dropIndex = GetDropIndex(position);
            _dragAdorner?.UpdatePosition(position);

            if (_draggedReceiver is not null && DataContext is ReceiverManagerViewModel viewModel)
                viewModel.PreviewReceiverMove(_draggedReceiver, _dropIndex);

            e.Effects = DragDropEffects.Move;
            e.Handled = true;
        }

        private async void ReceiverListBox_PreviewDrop(object sender, DragEventArgs e)
        {
            _dropCompleted = true;

            if (e.Data.GetData(typeof(ReceiverConfigurationItemViewModel)) is ReceiverConfigurationItemViewModel receiver && DataContext is ReceiverManagerViewModel viewModel)
                await viewModel.PersistReceiverMoveAsync(receiver, _originalDragIndex);

            e.Handled = true;
        }

        private void ReceiverListBox_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_dragAdorner is null)
                _draggedReceiver = null;
        }

        private void BeginDragVisuals(ListBoxItem draggedContainer, Point position)
        {
            _adornerLayer = AdornerLayer.GetAdornerLayer(ReceiverListBox);

            if (_adornerLayer is null)
                return;

            Point containerPosition = draggedContainer.TranslatePoint(new Point(), ReceiverListBox);
            Point grabOffset = new(_dragStartPoint.X - containerPosition.X, _dragStartPoint.Y - containerPosition.Y);
            _dragAdorner = new ReceiverDragAdorner(ReceiverListBox, draggedContainer, grabOffset);
            _adornerLayer.Add(_dragAdorner);
            _dragAdorner.UpdatePosition(position);
            _draggedContainer = draggedContainer;
            _draggedContainer.Opacity = 0.18;
        }

        private void EndDragVisuals()
        {
            if (_adornerLayer is not null)
            {
                if (_dragAdorner is not null)
                    _adornerLayer.Remove(_dragAdorner);

            }

            if (_draggedContainer is not null)
                _draggedContainer.Opacity = 1;

            _dragAdorner = null;
            _draggedContainer = null;
            _adornerLayer = null;
            _dropIndex = -1;
            _originalDragIndex = -1;
            _dropCompleted = false;
        }

        private int GetDropIndex(Point position)
        {
            for (int index = 0; index < ReceiverListBox.Items.Count; index++)
            {
                if (ReceiverListBox.ItemContainerGenerator.ContainerFromIndex(index) is not ListBoxItem item)
                    continue;

                Point itemPosition = item.TranslatePoint(new Point(), ReceiverListBox);

                if (position.Y < itemPosition.Y + item.ActualHeight / 2)
                    return index;
            }

            return ReceiverListBox.Items.Count;
        }

        private ReceiverConfigurationItemViewModel? GetReceiverFromElement(DependencyObject? element)
        {
            while (element is not null && element is not ListBoxItem)
                element = VisualTreeHelper.GetParent(element);

            return (element as ListBoxItem)?.DataContext as ReceiverConfigurationItemViewModel;
        }
    }
}
