using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ClassFinance.Behaviors
{
    public static class HoverLiftBehavior
    {
        public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(HoverLiftBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

        private static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached(
            "State",
            typeof(HoverLiftState),
            typeof(HoverLiftBehavior),
            new PropertyMetadata(null));

        public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

        public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

        private static void OnIsEnabledChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            if (dependencyObject is not UIElement element)
                return;

            if (e.NewValue is true)
            {
                if (element.GetValue(StateProperty) is HoverLiftState)
                    return;

                var originalTransform = element.RenderTransform;
                var lift = new TranslateTransform();
                if (originalTransform == null || originalTransform == Transform.Identity)
                {
                    element.RenderTransform = lift;
                }
                else
                {
                    var transformGroup = new TransformGroup();
                    transformGroup.Children.Add(originalTransform);
                    transformGroup.Children.Add(lift);
                    element.RenderTransform = transformGroup;
                }

                element.SetValue(StateProperty, new HoverLiftState(originalTransform, lift));
                element.MouseEnter += Element_MouseEnter;
                element.MouseLeave += Element_MouseLeave;
            }
            else if (element.GetValue(StateProperty) is HoverLiftState state)
            {
                element.MouseEnter -= Element_MouseEnter;
                element.MouseLeave -= Element_MouseLeave;
                state.Lift.BeginAnimation(TranslateTransform.YProperty, null);
                element.RenderTransform = state.OriginalTransform ?? Transform.Identity;
                element.ClearValue(StateProperty);
            }
        }

        private static void Element_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (sender is UIElement element && element.GetValue(StateProperty) is HoverLiftState state)
                AnimateLift(state.Lift, -2.5);
        }

        private static void Element_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (sender is UIElement element && element.GetValue(StateProperty) is HoverLiftState state)
                AnimateLift(state.Lift, 0);
        }

        private static void AnimateLift(TranslateTransform lift, double target)
        {
            var animation = new DoubleAnimation(lift.Y, target, TimeSpan.FromMilliseconds(130))
            {
                EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseOut, Exponent = 4 }
            };
            lift.BeginAnimation(TranslateTransform.YProperty, animation, HandoffBehavior.SnapshotAndReplace);
        }

        private sealed class HoverLiftState
        {
            public HoverLiftState(Transform originalTransform, TranslateTransform lift)
            {
                OriginalTransform = originalTransform;
                Lift = lift;
            }

            public Transform OriginalTransform { get; }
            public TranslateTransform Lift { get; }
        }
    }
}
