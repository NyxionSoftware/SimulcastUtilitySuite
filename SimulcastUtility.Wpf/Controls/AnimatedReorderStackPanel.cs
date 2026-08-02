using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace SimulcastUtility.Wpf.Controls
{
    public sealed class AnimatedReorderStackPanel : StackPanel
    {
        private static readonly Duration MovementDuration = new(TimeSpan.FromMilliseconds(170));
        private readonly Dictionary<UIElement, double> _verticalPositions = new();
        private bool _positionUpdatePending;

        protected override Size ArrangeOverride(Size arrangeSize)
        {
            Size arrangedSize = base.ArrangeOverride(arrangeSize);

            if (!_positionUpdatePending)
            {
                _positionUpdatePending = true;
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, AnimatePositionChanges);
            }

            return arrangedSize;
        }

        private void AnimatePositionChanges()
        {
            _positionUpdatePending = false;
            HashSet<UIElement> currentChildren = new();

            foreach (UIElement child in InternalChildren)
            {
                currentChildren.Add(child);
                double currentPosition = VisualTreeHelper.GetOffset(child).Y;

                if (_verticalPositions.TryGetValue(child, out double previousPosition))
                    AnimateMovement(child, previousPosition - currentPosition);

                _verticalPositions[child] = currentPosition;
            }

            foreach (UIElement removedChild in _verticalPositions.Keys.Except(currentChildren).ToArray())
                _verticalPositions.Remove(removedChild);
        }

        private static void AnimateMovement(UIElement child, double verticalOffset)
        {
            if (Math.Abs(verticalOffset) < 0.5)
                return;

            TranslateTransform transform = child.RenderTransform as TranslateTransform ?? new TranslateTransform();
            child.RenderTransform = transform;
            transform.BeginAnimation(TranslateTransform.YProperty, null);
            transform.Y = 0;

            DoubleAnimation animation = new(verticalOffset, 0, MovementDuration)
            {
                EasingFunction = new CubicEase() { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop
            };

            transform.BeginAnimation(TranslateTransform.YProperty, animation, HandoffBehavior.SnapshotAndReplace);
        }
    }
}
