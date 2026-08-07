using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace Linx68.ScreenDriver.App;

internal static class SmoothScrollBehavior
{
    private const double WheelDistanceFactor = 0.62;
    private const int DurationMilliseconds = 210;
    private static readonly ConditionalWeakTable<ScrollViewer, ScrollState> States = new();

    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(SmoothScrollBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    private static readonly DependencyProperty AnimatedVerticalOffsetProperty =
        DependencyProperty.RegisterAttached(
            "AnimatedVerticalOffset",
            typeof(double),
            typeof(SmoothScrollBehavior),
            new PropertyMetadata(0d, OnAnimatedVerticalOffsetChanged));

    public static void SetIsEnabled(DependencyObject element, bool value) =>
        element.SetValue(IsEnabledProperty, value);

    public static bool GetIsEnabled(DependencyObject element) =>
        (bool)element.GetValue(IsEnabledProperty);

    private static void OnIsEnabledChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not ScrollViewer viewer)
        {
            return;
        }

        if ((bool)args.NewValue)
        {
            viewer.PreviewMouseWheel += Viewer_OnPreviewMouseWheel;
            viewer.ScrollChanged += Viewer_OnScrollChanged;
            viewer.PanningMode = PanningMode.VerticalOnly;
            viewer.PanningDeceleration = 0.0025;
        }
        else
        {
            viewer.PreviewMouseWheel -= Viewer_OnPreviewMouseWheel;
            viewer.ScrollChanged -= Viewer_OnScrollChanged;
            viewer.BeginAnimation(AnimatedVerticalOffsetProperty, null);
            States.Remove(viewer);
        }
    }

    private static void Viewer_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer viewer || viewer.ScrollableHeight <= 0)
        {
            return;
        }

        ScrollState state = States.GetValue(viewer, static item => new ScrollState(item.VerticalOffset));
        if (!state.IsAnimating)
        {
            state.TargetOffset = viewer.VerticalOffset;
        }

        double target = Math.Clamp(
            state.TargetOffset - e.Delta * WheelDistanceFactor,
            0,
            viewer.ScrollableHeight);
        if (Math.Abs(target - viewer.VerticalOffset) < 0.1)
        {
            return;
        }

        state.TargetOffset = target;
        state.IsAnimating = true;
        double current = viewer.VerticalOffset;

        viewer.BeginAnimation(AnimatedVerticalOffsetProperty, null);
        viewer.SetValue(AnimatedVerticalOffsetProperty, target);
        var animation = new DoubleAnimation
        {
            From = current,
            To = target,
            Duration = TimeSpan.FromMilliseconds(DurationMilliseconds),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.Stop
        };
        animation.Completed += (_, _) =>
        {
            state.IsAnimating = false;
            state.TargetOffset = viewer.VerticalOffset;
        };
        viewer.BeginAnimation(
            AnimatedVerticalOffsetProperty,
            animation,
            HandoffBehavior.SnapshotAndReplace);
        e.Handled = true;
    }

    private static void Viewer_OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (sender is ScrollViewer viewer
            && States.TryGetValue(viewer, out ScrollState? state)
            && !state.IsAnimating)
        {
            state.TargetOffset = viewer.VerticalOffset;
        }
    }

    private static void OnAnimatedVerticalOffsetChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is ScrollViewer viewer)
        {
            viewer.ScrollToVerticalOffset((double)args.NewValue);
        }
    }

    private sealed class ScrollState(double targetOffset)
    {
        public double TargetOffset { get; set; } = targetOffset;

        public bool IsAnimating { get; set; }
    }
}