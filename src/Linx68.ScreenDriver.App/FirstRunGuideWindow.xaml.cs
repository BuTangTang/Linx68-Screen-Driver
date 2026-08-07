using System.Net;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Key = System.Windows.Input.Key;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using TextBox = System.Windows.Controls.TextBox;
using DataFormats = System.Windows.DataFormats;

namespace Linx68.ScreenDriver.App;

public partial class FirstRunGuideWindow : Window
{
    private TextBox[] _parts = [];
    private bool _suppressAutoAdvance;

    public string DeviceIp { get; private set; } = string.Empty;

    public FirstRunGuideWindow(string? existingIp = null)
    {
        InitializeComponent();
        _parts = [IpPart1, IpPart2, IpPart3, IpPart4];
        Populate(existingIp);
    }

    private void Window_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!SystemParameters.ClientAreaAnimation)
        {
            GuideCard.Opacity = 1;
            GuideTranslate.Y = 0;
        }
        else
        {
            var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
            GuideCard.Opacity = 1;
            GuideCard.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(280))
            {
                EasingFunction = easing,
                FillBehavior = FillBehavior.Stop
            }, HandoffBehavior.SnapshotAndReplace);
            GuideTranslate.Y = 0;
            GuideTranslate.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty,
                new DoubleAnimation(28, 0, TimeSpan.FromMilliseconds(280))
                {
                    EasingFunction = easing,
                    FillBehavior = FillBehavior.Stop
                }, HandoffBehavior.SnapshotAndReplace);
        }

        IpPart1.Focus();
        IpPart1.SelectAll();
    }

    private void IpPart_OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !e.Text.All(char.IsDigit);
    }

    private void IpPart_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressAutoAdvance || sender is not TextBox textBox)
        {
            return;
        }

        ValidationText.Text = string.Empty;
        if (textBox.Text.Length == 3 && int.TryParse(textBox.Text, out int value) && value <= 255)
        {
            int index = Array.IndexOf(_parts, textBox);
            if (index >= 0 && index < _parts.Length - 1)
            {
                _parts[index + 1].Focus();
                _parts[index + 1].SelectAll();
            }
        }
    }

    private void IpPart_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox)
        {
            return;
        }

        int index = Array.IndexOf(_parts, textBox);
        if (e.Key == Key.Back && textBox.Text.Length == 0 && index > 0)
        {
            _parts[index - 1].Focus();
            _parts[index - 1].CaretIndex = _parts[index - 1].Text.Length;
            e.Handled = true;
        }
        else if ((e.Key == Key.OemPeriod || e.Key == Key.Decimal) && index >= 0 && index < _parts.Length - 1)
        {
            _parts[index + 1].Focus();
            _parts[index + 1].SelectAll();
            e.Handled = true;
        }
    }

    private void IpPart_OnPasting(object sender, DataObjectPastingEventArgs e)
    {
        if (!e.SourceDataObject.GetDataPresent(DataFormats.UnicodeText))
        {
            e.CancelCommand();
            return;
        }

        string pasted = (e.SourceDataObject.GetData(DataFormats.UnicodeText) as string ?? string.Empty).Trim();
        if (!TryPopulateFullIp(pasted))
        {
            if (!Regex.IsMatch(pasted, @"^\d{1,3}$"))
            {
                e.CancelCommand();
            }
        }
        else
        {
            e.CancelCommand();
            IpPart4.Focus();
            IpPart4.CaretIndex = IpPart4.Text.Length;
        }
    }

    private void SaveButton_OnClick(object sender, RoutedEventArgs e)
    {
        string candidate = string.Join(".", _parts.Select(part => part.Text.Trim()));
        if (!IPAddress.TryParse(candidate, out IPAddress? address) ||
            address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            ValidationText.Text = "请输入完整且有效的 IPv4 地址";
            return;
        }

        DeviceIp = address.ToString();
        DialogResult = true;
    }

    private void LaterButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void Window_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
        }
    }

    private void Populate(string? existingIp)
    {
        if (!string.IsNullOrWhiteSpace(existingIp))
        {
            TryPopulateFullIp(existingIp);
        }
    }

    private bool TryPopulateFullIp(string value)
    {
        if (!IPAddress.TryParse(value, out IPAddress? address) ||
            address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return false;
        }

        string[] segments = address.ToString().Split('.');
        _suppressAutoAdvance = true;
        try
        {
            for (int index = 0; index < _parts.Length; index++)
            {
                _parts[index].Text = segments[index];
            }
        }
        finally
        {
            _suppressAutoAdvance = false;
        }

        return true;
    }
}
