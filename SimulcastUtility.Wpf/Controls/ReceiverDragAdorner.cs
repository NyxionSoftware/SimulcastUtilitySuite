using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace SimulcastUtility.Wpf.Controls
{
    public sealed class ReceiverDragAdorner : Adorner
    {
        private readonly VisualBrush _visualBrush;
        private readonly Size _previewSize;
        private readonly Point _grabOffset;
        private Point _position;

        public ReceiverDragAdorner(UIElement adornedElement, UIElement previewElement, Point grabOffset) : base(adornedElement)
        {
            _visualBrush = new VisualBrush(previewElement);
            _previewSize = previewElement.RenderSize;
            _grabOffset = grabOffset;
            IsHitTestVisible = false;
        }

        public void UpdatePosition(Point position)
        {
            _position = position;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            Rect previewBounds = new(_position.X - _grabOffset.X, _position.Y - _grabOffset.Y, _previewSize.Width, _previewSize.Height);
            drawingContext.PushOpacity(0.62);
            drawingContext.DrawRoundedRectangle(_visualBrush, null, previewBounds, 10, 10);
            drawingContext.Pop();
        }
    }
}
