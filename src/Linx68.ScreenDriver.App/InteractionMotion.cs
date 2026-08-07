using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ButtonBase = System.Windows.Controls.Primitives.ButtonBase;
using ComboBox = System.Windows.Controls.ComboBox;

namespace Linx68.ScreenDriver.App;

/// <summary>
/// Centralized, interruptible motion for the desktop application UI.
/// It intentionally animates only opacity and render transforms so layout and
/// the keyboard-screen renderer remain untouched.
/// </summary>
public static class InteractionMotion
{
    private const int HoverDurationMs = 140;
    private const int PressDurationMs = 90;
    private const int ReleaseDurationMs = 180;
    private const int RevealDurationMs = 220;
    private const int HideDurationMs = 130;

    private static readonly ConditionalWeakTable<FrameworkElement, MotionState> States = new();

    public static readonly DependencyProperty IsInteractiveProperty =
        DependencyProperty.RegisterAttached(
            "IsInteractive",
            typeof(bool),
            typeof(InteractionMotion),
            new PropertyMetadata(false, OnIsInteractiveChanged));

    public static readonly DependencyProperty IsComboBoxProperty =
        DependencyProperty.RegisterAttached(
            "IsComboBox",
            typeof(bool),
            typeof(InteractionMotion),
            new PropertyMetadata(false, OnIsComboBoxChanged));

    public static void SetIsInteractive(DependencyObject element, bool value) =>
        element.SetValue(IsInteractiveProperty, value);

    public static bool GetIsInteractive(DependencyObject element) =>
        (bool)element.GetValue(IsInteractiveProperty);

    public static void SetIsComboBox(DependencyObject element, bool value) =>
        element.SetValue(IsComboBoxProperty, value);

    public static bool GetIsComboBox(DependencyObject element) =>
        (bool)element.GetValue(IsComboBoxProperty);

    public static void Reveal(FrameworkElement element, double offsetY = 8, double startScale = 0.992)
    {
        if (!CanAnimate(element))
        {
            Reset(element);
            return;
        }

        var state = EnsureState(element);
        element.Opacity = 1;
        state.Translate.Y = 0;
        state.Scale.ScaleX = 1;
        state.Scale.ScaleY = 1;

        Animate(element, UIElement.OpacityProperty, 0, 1, RevealDurationMs, EaseOut());
        Animate(state.Translate, TranslateTransform.YProperty, offsetY, 0, RevealDurationMs, EaseOut());
        Animate(state.Scale, ScaleTransform.ScaleXProperty, startScale, 1, RevealDurationMs, EaseOut());
        Animate(state.Scale, ScaleTransform.ScaleYProperty, startScale, 1, RevealDurationMs, EaseOut());
    }

    public static async Task HideAsync(FrameworkElement element, double offsetY = 5)
    {
        if (!CanAnimate(element))
        {
            return;
        }

        var state = EnsureState(element);
        AnimateTo(element, UIElement.OpacityProperty, 0, HideDurationMs, EaseInOut());
        AnimateTo(state.Translate, TranslateTransform.YProperty, offsetY, HideDurationMs, EaseInOut());
        AnimateTo(state.Scale, ScaleTransform.ScaleXProperty, 0.992, HideDurationMs, EaseInOut());
        AnimateTo(state.Scale, ScaleTransform.ScaleYProperty, 0.992, HideDurationMs, EaseInOut());
        await Task.Delay(HideDurationMs);
    }

    public static void Reset(FrameworkElement element)
    {
        element.BeginAnimation(UIElement.OpacityProperty, null);
        element.Opacity = 1;
        if (!States.TryGetValue(element, out var state))
        {
            return;
        }

        state.Scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        state.Scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        state.Translate.BeginAnimation(TranslateTransform.XProperty, null);
        state.Translate.BeginAnimation(TranslateTransform.YProperty, null);
        state.Scale.ScaleX = 1;
        state.Scale.ScaleY = 1;
        state.Translate.X = 0;
        state.Translate.Y = 0;
    }

    private static void OnIsInteractiveChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not FrameworkElement element)
        {
            return;
        }

        if ((bool)args.NewValue)
        {
            element.Loaded += Interactive_OnLoaded;
            element.Unloaded += Interactive_OnUnloaded;
            element.MouseEnter += Interactive_OnMouseEnter;
            element.MouseLeave += Interactive_OnMouseLeave;
            element.PreviewMouseLeftButtonDown += Interactive_OnMouseDown;
            element.PreviewMouseLeftButtonUp += Interactive_OnMouseUp;
            element.LostMouseCapture += Interactive_OnLostMouseCapture;

        }
        else
        {
            element.Loaded -= Interactive_OnLoaded;
            element.Unloaded -= Interactive_OnUnloaded;
            element.MouseEnter -= Interactive_OnMouseEnter;
            element.MouseLeave -= Interactive_OnMouseLeave;
            element.PreviewMouseLeftButtonDown -= Interactive_OnMouseDown;
            element.PreviewMouseLeftButtonUp -= Interactive_OnMouseUp;
            element.LostMouseCapture -= Interactive_OnLostMouseCapture;

            Reset(element);
        }
    }

    private static void OnIsComboBoxChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not ComboBox comboBox)
        {
            return;
        }

        if ((bool)args.NewValue)
        {
            comboBox.DropDownOpened += ComboBox_OnDropDownOpened;
        }
        else
        {
            comboBox.DropDownOpened -= ComboBox_OnDropDownOpened;
        }
    }

    private static void Interactive_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            EnsureState(element);

        }
    }

    private static void Interactive_OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            Reset(element);
        }
    }

    private static void Interactive_OnMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is FrameworkElement { IsEnabled: true } element &&
            !IsPressed(element))
        {
            AnimateScale(element, 1.012, HoverDurationMs, EaseOut());
        }
    }

    private static void Interactive_OnMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            AnimateScale(element, 1, ReleaseDurationMs, EaseOut());
        }
    }

    private static void Interactive_OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { IsEnabled: true } element)
        {
            AnimateScale(element, 0.975, PressDurationMs, EaseInOut());
        }
    }

    private static void Interactive_OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            AnimateScale(element, element.IsMouseOver ? 1.012 : 1, ReleaseDurationMs, EaseOut());
        }
    }

    private static void Interactive_OnLostMouseCapture(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            AnimateScale(element, element.IsMouseOver ? 1.012 : 1, ReleaseDurationMs, EaseOut());
        }
    }

    private static void ComboBox_OnDropDownOpened(object? sender, EventArgs e)
    {
        if (sender is not ComboBox comboBox ||
            comboBox.Template.FindName("PART_Popup", comboBox) is not Popup { Child: FrameworkElement popupChild })
        {
            return;
        }

        Reveal(popupChild, 5, 0.995);
    }

    private static bool IsPressed(FrameworkElement element) =>
        element is ButtonBase button && button.IsPressed;

    private static void AnimateScale(FrameworkElement element, double target, int durationMs, IEasingFunction easing)
    {
        if (!CanAnimate(element))
        {
            return;
        }

        var state = EnsureState(element);
        AnimateTo(state.Scale, ScaleTransform.ScaleXProperty, target, durationMs, easing);
        AnimateTo(state.Scale, ScaleTransform.ScaleYProperty, target, durationMs, easing);
    }

    private static MotionState EnsureState(FrameworkElement element)
    {
        return States.GetValue(element, static target =>
        {
            var scale = new ScaleTransform(1, 1);
            var translate = new TranslateTransform();
            var group = new TransformGroup();

            if (target.RenderTransform is { } existing &&
                existing != Transform.Identity &&
                !existing.Value.IsIdentity)
            {
                group.Children.Add(existing.CloneCurrentValue());
            }

            group.Children.Add(scale);
            group.Children.Add(translate);
            target.RenderTransform = group;
            target.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
            return new MotionState(scale, translate);
        });
    }

    private static bool CanAnimate(DependencyObject element) =>
        SystemParameters.ClientAreaAnimation &&
        element is not FrameworkElement { IsVisible: false };


    private static void AnimateTo(UIElement target, DependencyProperty property, double targetValue, int durationMs, IEasingFunction easing)
    {
        var current = (double)target.GetValue(property);
        target.SetValue(property, targetValue);
        Animate(target, property, current, targetValue, durationMs, easing);
    }

    private static void Animate(UIElement target, DependencyProperty property, double from, double to, int durationMs, IEasingFunction easing)
    {
        var animation = CreateAnimation(from, to, durationMs, easing);
        target.BeginAnimation(property, animation, HandoffBehavior.SnapshotAndReplace);
    }
    private static void AnimateTo(Animatable target, DependencyProperty property, double targetValue, int durationMs, IEasingFunction easing)
    {
        var current = (double)target.GetValue(property);
        target.SetValue(property, targetValue);
        Animate(target, property, current, targetValue, durationMs, easing);
    }

    private static void Animate(Animatable target, DependencyProperty property, double from, double to, int durationMs, IEasingFunction easing)
    {
        var animation = CreateAnimation(from, to, durationMs, easing);
        target.BeginAnimation(property, animation, HandoffBehavior.SnapshotAndReplace);
    }

    private static DoubleAnimation CreateAnimation(double from, double to, int durationMs, IEasingFunction easing) => new()
    {
        From = from,
        To = to,
        Duration = TimeSpan.FromMilliseconds(durationMs),
        EasingFunction = easing,
        FillBehavior = FillBehavior.Stop
    };

    private static CubicEase EaseOut() => new() { EasingMode = EasingMode.EaseOut };

    private static CubicEase EaseInOut() => new() { EasingMode = EasingMode.EaseInOut };

    private sealed record MotionState(ScaleTransform Scale, TranslateTransform Translate);
}
