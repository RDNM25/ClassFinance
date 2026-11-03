using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ClassFinance.Behaviors
{
    public static class ButtonHoverAnimationBehavior
    {
        private const double HoverScale = 1.015;
        private const double PressedScale = 0.99;

        private static readonly ConditionalWeakTable<Button, ButtonAnimationState> AnimationStates = new();
        private static bool _registered;

        public static void Register()
        {
            if (_registered)
                return;

            _registered = true;
            EventManager.RegisterClassHandler(typeof(Button), UIElement.MouseEnterEvent,
                new MouseEventHandler(OnMouseEnter), true);
            EventManager.RegisterClassHandler(typeof(Button), UIElement.MouseLeaveEvent,
                new MouseEventHandler(OnMouseLeave), true);
            EventManager.RegisterClassHandler(typeof(Button), UIElement.PreviewMouseDownEvent,
                new MouseButtonEventHandler(OnPreviewMouseDown), true);
            EventManager.RegisterClassHandler(typeof(Button), UIElement.PreviewMouseUpEvent,
                new MouseButtonEventHandler(OnPreviewMouseUp), true);
        }

        private static void OnMouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is Button button && button.IsEnabled)
                AnimateScale(button, HoverScale, 130);
        }

        private static void OnMouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is Button button)
                AnimateScale(button, 1, 105);
        }

        private static void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Button button && button.IsEnabled)
                AnimateScale(button, PressedScale, 70);
        }

        private static void OnPreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is Button button && button.IsEnabled)
                AnimateScale(button, button.IsMouseOver ? HoverScale : 1, 90);
        }

        private static void AnimateScale(Button button, double targetScale, int durationMilliseconds)
        {
            var scale = GetScaleTransform(button);
            if (!SystemParameters.ClientAreaAnimation)
            {
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                scale.ScaleX = 1;
                scale.ScaleY = 1;
                return;
            }

            var duration = TimeSpan.FromMilliseconds(durationMilliseconds);
            var easing = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            scale.BeginAnimation(ScaleTransform.ScaleXProperty,
                new DoubleAnimation { To = targetScale, Duration = duration, EasingFunction = easing },
                HandoffBehavior.SnapshotAndReplace);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty,
                new DoubleAnimation { To = targetScale, Duration = duration, EasingFunction = easing },
                HandoffBehavior.SnapshotAndReplace);
        }

        private static ScaleTransform GetScaleTransform(Button button)
        {
            if (AnimationStates.TryGetValue(button, out var state))
                return state.Scale;

            var existingTransform = button.RenderTransform;
            var scale = new ScaleTransform(1, 1);
            var combinedTransform = new TransformGroup();

            if (existingTransform != null && existingTransform != Transform.Identity)
                combinedTransform.Children.Add(existingTransform);

            combinedTransform.Children.Add(scale);
            button.RenderTransform = combinedTransform;

            if (button.ReadLocalValue(UIElement.RenderTransformOriginProperty) == DependencyProperty.UnsetValue)
                button.RenderTransformOrigin = new Point(0.5, 0.5);

            AnimationStates.Add(button, new ButtonAnimationState(scale));
            return scale;
        }

        private sealed class ButtonAnimationState
        {
            public ButtonAnimationState(ScaleTransform scale)
            {
                Scale = scale;
            }

            public ScaleTransform Scale { get; }
        }
    }
}
