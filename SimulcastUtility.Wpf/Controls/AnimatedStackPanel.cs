using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace SimulcastUtility.Wpf.Controls
{
    public sealed class AnimatedStackPanel : StackPanel
    {
        private static readonly Duration MovementDuration = new(TimeSpan.FromMilliseconds(220));
        private readonly Dictionary<UIElement, double> _stableVerticalPositions = new();
        private bool _positionUpdatePending;
        private bool _animateNextPositionUpdate;

        protected override Size ArrangeOverride(Size arrangeSize)
        {
            Size arrangedSize = base.ArrangeOverride(arrangeSize);
            SchedulePositionUpdate(false);

            return arrangedSize;
        }

        protected override void OnVisualChildrenChanged(DependencyObject visualAdded, DependencyObject visualRemoved)
        {
            base.OnVisualChildrenChanged(visualAdded, visualRemoved);
            SchedulePositionUpdate(visualRemoved is UIElement);
        }

        private void SchedulePositionUpdate(bool animate)
        {
            _animateNextPositionUpdate |= animate;

            if (_positionUpdatePending)
                return;

            _positionUpdatePending = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, ApplySettledPositions);
        }

        private void ApplySettledPositions()
        {
            _positionUpdatePending = false;

            bool animate = _animateNextPositionUpdate;
            _animateNextPositionUpdate = false;
            HashSet<UIElement> currentChildren = new();

            foreach (UIElement child in InternalChildren)
            {
                currentChildren.Add(child);

                double currentVerticalPosition = GetLayoutVerticalPosition(child);

                if (animate && _stableVerticalPositions.TryGetValue(child, out double previousVerticalPosition))
                {
                    double activeAnimationOffset = child.RenderTransform is TranslateTransform existingTranslation ? existingTranslation.Y : 0;
                    AnimateVerticalMovement(child, previousVerticalPosition + activeAnimationOffset - currentVerticalPosition);
                }

                _stableVerticalPositions[child] = currentVerticalPosition;
            }

            foreach (UIElement removedChild in _stableVerticalPositions.Keys.Except(currentChildren).ToArray())
                _stableVerticalPositions.Remove(removedChild);
        }

        private static double GetLayoutVerticalPosition(Visual visual)
        {
            double verticalPosition = 0;
            DependencyObject? current = visual;

            while (current is Visual currentVisual)
            {
                verticalPosition += VisualTreeHelper.GetOffset(currentVisual).Y;
                current = VisualTreeHelper.GetParent(currentVisual);
            }

            return verticalPosition;
        }

        private static void AnimateVerticalMovement(UIElement child, double verticalOffset)
        {
            if (Math.Abs(verticalOffset) < 0.5)
                return;

            TranslateTransform translation = child.RenderTransform as TranslateTransform ?? new TranslateTransform();
            child.RenderTransform = translation;
            translation.BeginAnimation(TranslateTransform.YProperty, null);
            translation.Y = 0;

            CubicEase easing = new() { EasingMode = EasingMode.EaseOut };
            DoubleAnimation movementAnimation = new(verticalOffset, 0, MovementDuration) { EasingFunction = easing, FillBehavior = FillBehavior.Stop };

            translation.BeginAnimation(TranslateTransform.YProperty, movementAnimation, HandoffBehavior.SnapshotAndReplace);
        }
    }
}
