using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using Key = System.Windows.Input.Key;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using TextBox = System.Windows.Controls.TextBox;

namespace Linx68.ScreenDriver.App;

public partial class AccentColorDialog : Window
{
    private bool _synchronizing;
    private double _hue;
    private double _saturation;
    private double _value;
    private bool _closing;

    public Color SelectedColor { get; private set; }

    public AccentColorDialog(Color initialColor)
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            SetColor(initialColor);
            UpdatePointers();
            InteractionMotion.Reveal(DialogRoot, 8, 0.992);
        };
        SetColor(initialColor);
    }

    private void SetColor(Color color)
    {
        RgbToHsv(color, out _hue, out _saturation, out _value);
        SelectedColor = Color.FromRgb(color.R, color.G, color.B);
        SynchronizeControls();
    }

    private void SetColorFromHsv()
    {
        SelectedColor = HsvToRgb(_hue, _saturation, _value);
        SynchronizeControls();
    }

    private void SynchronizeControls()
    {
        _synchronizing = true;
        try
        {
            HexTextBox.Text = $"#{SelectedColor.R:X2}{SelectedColor.G:X2}{SelectedColor.B:X2}";
            RedTextBox.Text = SelectedColor.R.ToString(CultureInfo.InvariantCulture);
            GreenTextBox.Text = SelectedColor.G.ToString(CultureInfo.InvariantCulture);
            BlueTextBox.Text = SelectedColor.B.ToString(CultureInfo.InvariantCulture);
            SetInputValidity(HexTextBox, true);
            SetInputValidity(RedTextBox, true);
            SetInputValidity(GreenTextBox, true);
            SetInputValidity(BlueTextBox, true);

            var selectedBrush = new SolidColorBrush(SelectedColor);
            CurrentColorBar.Background = selectedBrush;
            ColorPointer.Fill = selectedBrush;
            HuePointer.Fill = new SolidColorBrush(HsvToRgb(_hue, 1, 1));
            ColorSurface.Background = new SolidColorBrush(HsvToRgb(_hue, 1, 1));
            UpdatePointers();
        }
        finally
        {
            _synchronizing = false;
        }
    }

    private void UpdatePointers()
    {
        if (ColorField.ActualWidth > 0 && ColorField.ActualHeight > 0)
        {
            var x = Math.Clamp(_saturation * ColorField.ActualWidth, 10, ColorField.ActualWidth - 10);
            var y = Math.Clamp((1 - _value) * ColorField.ActualHeight, 10, ColorField.ActualHeight - 10);
            Canvas.SetLeft(ColorPointer, x - 10);
            Canvas.SetTop(ColorPointer, y - 10);
        }

        if (HueField.ActualWidth > 0)
        {
            var x = Math.Clamp(_hue / 360d * HueField.ActualWidth, 9, HueField.ActualWidth - 9);
            Canvas.SetLeft(HuePointer, x - 9);
            Canvas.SetTop(HuePointer, Math.Max(0, (HueField.ActualHeight - 18) / 2));
        }
    }

    private void ColorField_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        ColorField.CaptureMouse();
        UpdateSaturationAndValue(e.GetPosition(ColorField));
    }

    private void ColorField_OnMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && ColorField.IsMouseCaptured)
        {
            UpdateSaturationAndValue(e.GetPosition(ColorField));
        }
    }

    private void ColorField_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (ColorField.IsMouseCaptured)
        {
            UpdateSaturationAndValue(e.GetPosition(ColorField));
            ColorField.ReleaseMouseCapture();
        }
    }

    private void UpdateSaturationAndValue(System.Windows.Point point)
    {
        if (ColorField.ActualWidth <= 0 || ColorField.ActualHeight <= 0)
        {
            return;
        }

        _saturation = Math.Clamp(point.X / ColorField.ActualWidth, 0, 1);
        _value = 1 - Math.Clamp(point.Y / ColorField.ActualHeight, 0, 1);
        SetColorFromHsv();
    }

    private void HueField_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        HueField.CaptureMouse();
        UpdateHue(e.GetPosition(HueField).X);
    }

    private void HueField_OnMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && HueField.IsMouseCaptured)
        {
            UpdateHue(e.GetPosition(HueField).X);
        }
    }

    private void HueField_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (HueField.IsMouseCaptured)
        {
            UpdateHue(e.GetPosition(HueField).X);
            HueField.ReleaseMouseCapture();
        }
    }

    private void UpdateHue(double x)
    {
        if (HueField.ActualWidth <= 0)
        {
            return;
        }

        _hue = Math.Clamp(x / HueField.ActualWidth, 0, 1) * 359.999;
        SetColorFromHsv();
    }

    private void HexTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_synchronizing || HexTextBox is null)
        {
            return;
        }

        var isValid = TryParseHex(HexTextBox.Text, out var color);
        SetInputValidity(HexTextBox, isValid);
        if (isValid)
        {
            SetColor(color);
        }
    }

    private void RgbTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_synchronizing || RedTextBox is null || GreenTextBox is null || BlueTextBox is null)
        {
            return;
        }

        var redValid = TryParseChannel(RedTextBox.Text, out var red);
        var greenValid = TryParseChannel(GreenTextBox.Text, out var green);
        var blueValid = TryParseChannel(BlueTextBox.Text, out var blue);
        SetInputValidity(RedTextBox, redValid);
        SetInputValidity(GreenTextBox, greenValid);
        SetInputValidity(BlueTextBox, blueValid);
        if (redValid && greenValid && blueValid)
        {
            SetColor(Color.FromRgb(red, green, blue));
        }
    }

    private static bool TryParseChannel(string text, out byte value) =>
        byte.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    private static bool TryParseHex(string text, out Color color)
    {
        color = default;
        var value = text.Trim();
        if (value.Length != 7 || value[0] != '#' ||
            !byte.TryParse(value.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var red) ||
            !byte.TryParse(value.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var green) ||
            !byte.TryParse(value.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var blue))
        {
            return false;
        }

        color = Color.FromRgb(red, green, blue);
        return true;
    }

    private void SetInputValidity(TextBox textBox, bool isValid)
    {
        textBox.BorderBrush = (Brush)FindResource(isValid ? "Stroke.Default" : "DangerBrush");
    }

    private static Color HsvToRgb(double hue, double saturation, double value)
    {
        var chroma = value * saturation;
        var sector = hue / 60d;
        var x = chroma * (1 - Math.Abs(sector % 2 - 1));
        var (red, green, blue) = sector switch
        {
            < 1 => (chroma, x, 0d),
            < 2 => (x, chroma, 0d),
            < 3 => (0d, chroma, x),
            < 4 => (0d, x, chroma),
            < 5 => (x, 0d, chroma),
            _ => (chroma, 0d, x)
        };
        var match = value - chroma;
        return Color.FromRgb(
            (byte)Math.Round((red + match) * 255),
            (byte)Math.Round((green + match) * 255),
            (byte)Math.Round((blue + match) * 255));
    }

    private static void RgbToHsv(Color color, out double hue, out double saturation, out double value)
    {
        var red = color.R / 255d;
        var green = color.G / 255d;
        var blue = color.B / 255d;
        var max = Math.Max(red, Math.Max(green, blue));
        var min = Math.Min(red, Math.Min(green, blue));
        var delta = max - min;

        hue = delta switch
        {
            0 => 0,
            _ when max == red => 60 * (((green - blue) / delta) % 6),
            _ when max == green => 60 * (((blue - red) / delta) + 2),
            _ => 60 * (((red - green) / delta) + 4)
        };
        if (hue < 0)
        {
            hue += 360;
        }

        saturation = max == 0 ? 0 : delta / max;
        value = max;
    }

    private async void ApplyButton_OnClick(object sender, RoutedEventArgs e) => await CompleteAsync(true);

    private async void CancelButton_OnClick(object sender, RoutedEventArgs e) => await CompleteAsync(false);

    private async Task CompleteAsync(bool result)
    {
        if (_closing)
        {
            return;
        }

        _closing = true;
        await InteractionMotion.HideAsync(DialogRoot, 4);
        DialogResult = result;
    }

    private void TitleBar_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private async void Window_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            await CompleteAsync(false);
        }
    }
}