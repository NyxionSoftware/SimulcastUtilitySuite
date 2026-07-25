using SimulcastUtility.Shared.Enum;
using SimulcastUtility.Shared.Models;
using SimulcastUtility.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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
using System.Windows.Shapes;

namespace SimulcastUtility
{
    /// <summary>
    /// Interaction logic for ReceiverConfigurationWindow.xaml
    /// </summary>
    public partial class ReceiverConfigurationWindow : Window
    {
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int attributeValue, int attributeSize);

        private readonly ReceiverConfigurationViewModel _viewModel;

        private Point _receiverDragStartPoint;
        private Point _receiverMouseOffset;

        private Receiver? _draggedReceiver;
        private ListBoxItem? _draggedReceiverContainer;

        private ReceiverDragAdorner? _dragAdorner;
        private AdornerLayer? _dragAdornerLayer;

        private Border? _dragInputSurface;
        private Grid? _receiverListHostGrid;

        private bool _isReceiverDragging;
        private bool _receiverOrderChanged;
        private bool _isCompletingDrag;

        private int _originalReceiverIndex = -1;
        private int _lastRequestedInsertionIndex = -1;
        private int _pendingInsertionIndex = -1;

        private Task? _moveProcessingTask;

        private const double AutoScrollEdgeSize = 60;
        private const double AutoScrollMaximumSpeed = 14;

        private ScrollViewer? _receiverScrollViewer;

        public ReceiverConfigurationWindow(ReceiverConfigurationViewModel viewModel)
        {
            InitializeComponent();

            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

            DataContext = _viewModel;

            Loaded += ReceiverConfigurationWindow_Loaded;
            Closed += ReceiverConfigurationWindow_Closed;
            SourceInitialized += Window_SourceInitialized;
        }

        private void ReceiverConfigurationWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (_viewModel.SelectedReceiver is null)
                return;

            ReceiverListBox.UpdateLayout();

            ReceiverListBox.ScrollIntoView(_viewModel.SelectedReceiver);

            if (ReceiverListBox.ItemContainerGenerator.ContainerFromItem(_viewModel.SelectedReceiver) is ListBoxItem item)
            {
                item.BringIntoView();
            }
        }

        private void Window_SourceInitialized(object? sender, EventArgs e)
        {
            IntPtr windowHandle = new WindowInteropHelper(this).Handle;
            int enabled = 1;

            int result = DwmSetWindowAttribute(windowHandle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref enabled, sizeof(int));

            if (result != 0)
                DwmSetWindowAttribute(windowHandle, DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, ref enabled, sizeof(int));
        }

        private void ReceiverConfigurationWindow_Closed(object? sender, EventArgs e)
        {
            RemoveDragInputSurface();
            RemoveDragAdorner();
            RestoreDraggedContainer();

            _viewModel.Dispose();
        }

        private void ReceiverIdTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, "^[0-9]+$");
        }

        private void ReceiverListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_viewModel.IsBusy || _isCompletingDrag)
                return;

            ListBoxItem? container = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject);

            if (container?.DataContext is not Receiver receiver)
            {
                ResetDragState();
                return;
            }

            ReceiverListBox.SelectedItem = receiver;
            _viewModel.SelectedReceiver = receiver;

            _originalReceiverIndex = ReceiverListBox.Items.IndexOf(receiver);

            _receiverDragStartPoint = e.GetPosition(ReceiverListBox);
            _receiverMouseOffset = e.GetPosition(container);

            _draggedReceiver = receiver;
            _draggedReceiverContainer = container;

            _isReceiverDragging = false;
            _receiverOrderChanged = false;

            _lastRequestedInsertionIndex = -1;
            _pendingInsertionIndex = -1;

            e.Handled = true;
        }

        private void ReceiverListBox_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            HandleReceiverMouseMove(e);
        }

        private async void ReceiverListBox_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            bool wasDragging = _isReceiverDragging;

            await CompleteReceiverDragAsync();

            if (wasDragging)
                e.Handled = true;
        }

        private async void ReceiverListBox_LostMouseCapture(object sender, MouseEventArgs e)
        {
            if (_isReceiverDragging && !_isCompletingDrag)
                await CompleteReceiverDragAsync();
        }

        private void DragInputSurface_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            HandleReceiverMouseMove(e);
            e.Handled = true;
        }

        private async void DragInputSurface_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            await CompleteReceiverDragAsync();
            e.Handled = true;
        }

        private async void DragInputSurface_LostMouseCapture(object sender, MouseEventArgs e)
        {
            if (_isReceiverDragging && !_isCompletingDrag)
                await CompleteReceiverDragAsync();
        }

        private void HandleReceiverMouseMove(MouseEventArgs e)
        {
            if (_viewModel.IsBusy || _isCompletingDrag)
                return;

            if (e.LeftButton != MouseButtonState.Pressed)
                return;

            if (_draggedReceiver is null || _draggedReceiverContainer is null)
                return;

            Point currentPosition = e.GetPosition(ReceiverListBox);

            if (!_isReceiverDragging)
            {
                Vector movement = currentPosition - _receiverDragStartPoint;

                bool passedHorizontalThreshold = Math.Abs(movement.X) >= SystemParameters.MinimumHorizontalDragDistance;
                bool passedVerticalThreshold = Math.Abs(movement.Y) >= SystemParameters.MinimumVerticalDragDistance;

                if (!passedHorizontalThreshold && !passedVerticalThreshold)
                    return;

                if (!BeginReceiverDrag(currentPosition))
                    return;
            }

            AutoScrollReceiverList(currentPosition);

            Point updatedPosition = Mouse.GetPosition(ReceiverListBox);

            _dragAdorner?.UpdatePosition(updatedPosition);

            int requestedInsertionIndex = GetRequestedInsertionIndex(updatedPosition);

            if (requestedInsertionIndex < 0 || requestedInsertionIndex == _lastRequestedInsertionIndex)
                return;

            _lastRequestedInsertionIndex = requestedInsertionIndex;

            QueuePreviewMove(requestedInsertionIndex);
        }

        private void AutoScrollReceiverList(Point mousePosition)
        {
            if (_receiverScrollViewer is null)
                return;

            double viewportHeight = ReceiverListBox.ActualHeight;

            if (viewportHeight <= 0)
                return;

            if (mousePosition.Y < AutoScrollEdgeSize)
            {
                double distanceIntoEdge = AutoScrollEdgeSize - mousePosition.Y;
                double percentage = Math.Clamp(distanceIntoEdge / AutoScrollEdgeSize, 0, 1);
                double scrollAmount = Math.Max(1, AutoScrollMaximumSpeed * percentage);

                _receiverScrollViewer.ScrollToVerticalOffset(_receiverScrollViewer.VerticalOffset - scrollAmount);
            }
            else if (mousePosition.Y > viewportHeight - AutoScrollEdgeSize)
            {
                double distanceIntoEdge = mousePosition.Y - (viewportHeight - AutoScrollEdgeSize);
                double percentage = Math.Clamp(distanceIntoEdge / AutoScrollEdgeSize, 0, 1);
                double scrollAmount = Math.Max(1, AutoScrollMaximumSpeed * percentage);

                _receiverScrollViewer.ScrollToVerticalOffset(_receiverScrollViewer.VerticalOffset + scrollAmount);
            }
        }

        private bool BeginReceiverDrag(Point currentPosition)
        {
            if (_draggedReceiverContainer is null)
                return false;

            _dragAdornerLayer = AdornerLayer.GetAdornerLayer(ReceiverListBox);

            if (_dragAdornerLayer is null)
            {
                _viewModel.DisplayError("The receiver list does not have an available adorner layer.");
                ResetDragState();

                return false;
            }

            ImageSource snapshot = CreateElementSnapshot(_draggedReceiverContainer);

            _dragAdorner = new ReceiverDragAdorner(
                ReceiverListBox,
                snapshot,
                new Size(_draggedReceiverContainer.ActualWidth, _draggedReceiverContainer.ActualHeight),
                _receiverMouseOffset);

            _dragAdornerLayer.Add(_dragAdorner);

            if (!CreateDragInputSurface())
            {
                RemoveDragAdorner();

                _viewModel.DisplayError("The receiver drag surface could not be created.");
                ResetDragState();

                return false;
            }

            _isReceiverDragging = true;

            _receiverScrollViewer ??= FindVisualChild<ScrollViewer>(ReceiverListBox);

            SetDraggedContainerAppearance(_draggedReceiverContainer);

            _dragAdorner.UpdatePosition(currentPosition);

            Mouse.Capture(_dragInputSurface, CaptureMode.Element);

            return true;
        }

        private bool CreateDragInputSurface()
        {
            _receiverListHostGrid = ReceiverListBox.Parent as Grid;

            if (_receiverListHostGrid is null)
                return false;

            _dragInputSurface = new Border
            {
                Background = Brushes.Transparent,
                Cursor = Cursors.SizeAll,
                Focusable = false
            };

            Panel.SetZIndex(_dragInputSurface, 1000);

            _dragInputSurface.PreviewMouseMove += DragInputSurface_PreviewMouseMove;
            _dragInputSurface.PreviewMouseLeftButtonUp += DragInputSurface_PreviewMouseLeftButtonUp;
            _dragInputSurface.LostMouseCapture += DragInputSurface_LostMouseCapture;

            _receiverListHostGrid.Children.Add(_dragInputSurface);

            return true;
        }

        private void RemoveDragInputSurface()
        {
            if (_dragInputSurface is not null)
            {
                _dragInputSurface.PreviewMouseMove -= DragInputSurface_PreviewMouseMove;
                _dragInputSurface.PreviewMouseLeftButtonUp -= DragInputSurface_PreviewMouseLeftButtonUp;
                _dragInputSurface.LostMouseCapture -= DragInputSurface_LostMouseCapture;
            }

            if (_dragInputSurface is not null && _receiverListHostGrid is not null)
                _receiverListHostGrid.Children.Remove(_dragInputSurface);

            _dragInputSurface = null;
            _receiverListHostGrid = null;
        }

        private int GetRequestedInsertionIndex(Point mousePosition)
        {
            if (_draggedReceiver is null || _draggedReceiverContainer is null || ReceiverListBox.Items.Count == 0)
                return -1;

            /*
             * Use the center of the dragged card rather than the exact mouse
             * pointer. This means the row moves when the card itself crosses
             * another row, regardless of where the user grabbed the card.
             */
            double draggedCardTop = mousePosition.Y - _receiverMouseOffset.Y;
            double draggedCardCenter = draggedCardTop + _draggedReceiverContainer.ActualHeight / 2;

            for (int index = 0; index < ReceiverListBox.Items.Count; index++)
            {
                object item = ReceiverListBox.Items[index];

                if (ReferenceEquals(item, _draggedReceiver))
                    continue;

                ListBoxItem? container = ReceiverListBox.ItemContainerGenerator.ContainerFromIndex(index) as ListBoxItem;

                if (container is null || container.ActualHeight <= 0)
                    continue;

                Point containerPosition = container.TranslatePoint(new Point(0, 0), ReceiverListBox);
                double containerCenter = containerPosition.Y + container.ActualHeight / 2;

                if (draggedCardCenter < containerCenter)
                    return index;
            }

            return ReceiverListBox.Items.Count;
        }

        private void QueuePreviewMove(int insertionIndex)
        {
            /*
             * Replace any older queued destination with the newest destination.
             * The ghost remains fully responsive while collection moves happen.
             */
            _pendingInsertionIndex = insertionIndex;

            if (_moveProcessingTask is null || _moveProcessingTask.IsCompleted)
                _moveProcessingTask = ProcessPendingMovesAsync();
        }

        private async Task ProcessPendingMovesAsync()
        {
            while (_pendingInsertionIndex >= 0 && _draggedReceiver is not null && _isReceiverDragging)
            {
                int insertionIndex = _pendingInsertionIndex;

                _pendingInsertionIndex = -1;

                ReceiverOperationResult result = await _viewModel.PreviewMoveReceiverAsync(_draggedReceiver, insertionIndex);

                if (!result.Successful)
                {
                    _viewModel.DisplayError(result.Error ?? "The receiver could not be moved.");
                    continue;
                }

                if (!result.Changed)
                    continue;

                _receiverOrderChanged = true;

                ReceiverListBox.UpdateLayout();
                UpdateDraggedContainer();
            }
        }

        private async Task CompleteReceiverDragAsync()
        {
            if (_isCompletingDrag)
                return;

            if (!_isReceiverDragging)
            {
                ResetDragState();
                return;
            }

            _isCompletingDrag = true;

            try
            {
                /*
                 * Allow the most recently queued destination to finish before
                 * saving the final receiver order.
                 */
                if (_moveProcessingTask is not null)
                    await _moveProcessingTask;

                int finalIndex = ReceiverListBox.Items.IndexOf(_draggedReceiver);

                bool shouldSave = _receiverOrderChanged && finalIndex != _originalReceiverIndex;

                if (Mouse.Captured is not null)
                    Mouse.Capture(null);

                RemoveDragInputSurface();
                RemoveDragAdorner();
                RestoreDraggedContainer();

                if (!shouldSave)
                    return;

                ReceiverOperationResult result = await _viewModel.SaveReceiverOrderAsync();

                if (!result.Successful)
                    _viewModel.DisplayError(result.Error ?? "The receiver order could not be saved.");
            }
            finally
            {
                ResetDragState();
                _isCompletingDrag = false;
            }
        }

        private void UpdateDraggedContainer()
        {
            if (_draggedReceiver is null)
                return;

            RestoreDraggedContainer();

            ListBoxItem? currentContainer = ReceiverListBox.ItemContainerGenerator.ContainerFromItem(_draggedReceiver) as ListBoxItem;

            _draggedReceiverContainer = currentContainer;

            if (_draggedReceiverContainer is not null)
                SetDraggedContainerAppearance(_draggedReceiverContainer);
        }

        private static void SetDraggedContainerAppearance(ListBoxItem container)
        {
            container.Opacity = 0.15;
            container.IsHitTestVisible = false;
        }

        private void RestoreDraggedContainer()
        {
            if (_draggedReceiverContainer is not null)
            {
                _draggedReceiverContainer.Opacity = 1.0;
                _draggedReceiverContainer.IsHitTestVisible = true;
            }

            if (_draggedReceiver is null)
                return;

            ListBoxItem? currentContainer = ReceiverListBox.ItemContainerGenerator.ContainerFromItem(_draggedReceiver) as ListBoxItem;

            if (currentContainer is not null)
            {
                currentContainer.Opacity = 1.0;
                currentContainer.IsHitTestVisible = true;
            }
        }

        private void RemoveDragAdorner()
        {
            if (_dragAdorner is not null && _dragAdornerLayer is not null)
                _dragAdornerLayer.Remove(_dragAdorner);

            _dragAdorner = null;
            _dragAdornerLayer = null;
        }

        private void ResetDragState()
        {
            RemoveDragInputSurface();
            RemoveDragAdorner();
            RestoreDraggedContainer();

            _draggedReceiver = null;
            _draggedReceiverContainer = null;
            _receiverScrollViewer = null;

            _isReceiverDragging = false;
            _receiverOrderChanged = false;

            _originalReceiverIndex = -1;
            _lastRequestedInsertionIndex = -1;
            _pendingInsertionIndex = -1;

            _moveProcessingTask = null;
        }

        private static ImageSource CreateElementSnapshot(FrameworkElement element)
        {
            double width = Math.Max(1, element.ActualWidth);
            double height = Math.Max(1, element.ActualHeight);

            DpiScale dpi = VisualTreeHelper.GetDpi(element);

            int pixelWidth = Math.Max(1, (int)Math.Ceiling(width * dpi.DpiScaleX));
            int pixelHeight = Math.Max(1, (int)Math.Ceiling(height * dpi.DpiScaleY));

            RenderTargetBitmap bitmap = new RenderTargetBitmap(pixelWidth, pixelHeight, 96 * dpi.DpiScaleX, 96 * dpi.DpiScaleY, PixelFormats.Pbgra32);
            DrawingVisual drawingVisual = new DrawingVisual();

            using (DrawingContext drawingContext = drawingVisual.RenderOpen())
            {
                VisualBrush visualBrush = new VisualBrush(element);
                drawingContext.DrawRectangle(visualBrush, null, new Rect(0, 0, width, height));
            }

            bitmap.Render(drawingVisual);
            bitmap.Freeze();

            return bitmap;
        }

        private static T? FindVisualChild<T>(DependencyObject? parent) where T : DependencyObject
        {
            if (parent is null)
                return null;

            int childCount = VisualTreeHelper.GetChildrenCount(parent);

            for (int index = 0; index < childCount; index++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, index);

                if (child is T result)
                    return result;

                T? descendant = FindVisualChild<T>(child);

                if (descendant is not null)
                    return descendant;
            }

            return null;
        }

        private static T? FindAncestor<T>(DependencyObject? dependencyObject) where T : DependencyObject
        {
            while (dependencyObject is not null)
            {
                if (dependencyObject is T result)
                    return result;

                dependencyObject = VisualTreeHelper.GetParent(dependencyObject);
            }

            return null;
        }
    }

    public sealed class ReceiverDragAdorner : Adorner
    {
        private readonly ImageSource _snapshot;
        private readonly Size _snapshotSize;
        private readonly Point _mouseOffset;

        private double _left;
        private double _top;

        public ReceiverDragAdorner(UIElement adornedElement, ImageSource snapshot, Size snapshotSize, Point mouseOffset) : base(adornedElement)
        {
            _snapshot = snapshot;
            _snapshotSize = snapshotSize;
            _mouseOffset = mouseOffset;

            IsHitTestVisible = false;
        }

        public void UpdatePosition(Point mousePosition)
        {
            _left = mousePosition.X - _mouseOffset.X;
            _top = mousePosition.Y - _mouseOffset.Y;

            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            Rect shadowRect = new Rect(_left + 4, _top + 6, _snapshotSize.Width, _snapshotSize.Height);
            Rect snapshotRect = new Rect(_left, _top, _snapshotSize.Width, _snapshotSize.Height);

            drawingContext.PushOpacity(0.28);
            drawingContext.DrawRoundedRectangle(Brushes.Black, null, shadowRect, 6, 6);
            drawingContext.Pop();

            drawingContext.PushOpacity(0.92);
            drawingContext.DrawImage(_snapshot, snapshotRect);
            drawingContext.Pop();
        }
    }
}
