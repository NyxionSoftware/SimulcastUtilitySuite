using Microsoft.Xaml.Behaviors;
using SimulcastUtility.Wpf.ViewModels.Models;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace SimulcastUtility.Wpf.Behaviors
{
    public sealed class NotificationAnimationBehavior : Behavior<FrameworkElement>
    {
        private static readonly TimeSpan EntranceDuration = TimeSpan.FromMilliseconds(240);
        private static readonly TimeSpan MaximumExitDuration = TimeSpan.FromSeconds(3);

        private CancellationTokenSource? _animationCancellation;
        private TranslateTransform? _translation;

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.Loaded += AssociatedObject_Loaded;
            AssociatedObject.Unloaded += AssociatedObject_Unloaded;
        }

        protected override void OnDetaching()
        {
            AssociatedObject.Loaded -= AssociatedObject_Loaded;
            AssociatedObject.Unloaded -= AssociatedObject_Unloaded;
            CancelAnimations();
            base.OnDetaching();
        }

        private void AssociatedObject_Loaded(object sender, RoutedEventArgs e)
        {
            CancelAnimations();
            _animationCancellation = new CancellationTokenSource();

            _translation = new TranslateTransform(36, 0);
            AssociatedObject.RenderTransform = _translation;
            AssociatedObject.Opacity = 0;

            StartEntranceAnimation();
            _ = StartExitAnimationAsync(_animationCancellation.Token);
        }

        private void AssociatedObject_Unloaded(object sender, RoutedEventArgs e)
        {
            CancelAnimations();
        }

        private void StartEntranceAnimation()
        {
            CubicEase easing = new() { EasingMode = EasingMode.EaseOut };

            AssociatedObject.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, EntranceDuration) { EasingFunction = easing });

            _translation?.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(36, 0, EntranceDuration) { EasingFunction = easing });
        }

        private async Task StartExitAnimationAsync(CancellationToken cancellationToken)
        {
            TimeSpan displayDuration = AssociatedObject.DataContext is NotificationViewModel notification ? notification.DisplayDuration : TimeSpan.FromSeconds(5);

            TimeSpan availableExitDuration = displayDuration - EntranceDuration;
            TimeSpan exitDuration = availableExitDuration <= TimeSpan.Zero ? TimeSpan.Zero : availableExitDuration < MaximumExitDuration ? availableExitDuration : MaximumExitDuration;
            TimeSpan fullyVisibleDuration = displayDuration - exitDuration;

            try
            {
                await Task.Delay(fullyVisibleDuration, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (cancellationToken.IsCancellationRequested || !AssociatedObject.IsLoaded)
                return;

            CubicEase easing = new() { EasingMode = EasingMode.EaseIn };

            AssociatedObject.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, 0, exitDuration) { EasingFunction = easing });

            _translation?.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, 18, exitDuration) { EasingFunction = easing });
        }

        private void CancelAnimations()
        {
            _animationCancellation?.Cancel();
            _animationCancellation?.Dispose();
            _animationCancellation = null;
        }
    }
}
