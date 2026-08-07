using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using Linx68.ScreenDriver.App;
using Linx68.ScreenDriver.Core;
using System.Windows.Threading;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var app = new Application();
        app.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/Linx68.ScreenDriver.App;component/Themes/Palette.Light.xaml")
        });
        app.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/Linx68.ScreenDriver.App;component/DesignSystem.xaml")
        });
        var uiFont = (FontFamily)app.FindResource("UiFontFamily");
        Assert(!uiFont.Source.Contains("PingFang", StringComparison.OrdinalIgnoreCase) &&
               !uiFont.Source.Contains("pack://", StringComparison.OrdinalIgnoreCase),
            $"control UI font must use the Windows system stack: {uiFont.Source}");
        Console.WriteLine($"PASS UI font stack {uiFont.Source}");
        VerifyAppearancePalettes();

        VerifyTextBox(app, 44, new Thickness(14, 0, 14, 0), "\u5317\u4EAC Ag09");
        VerifyTextBox(app, 40, new Thickness(6, 0, 6, 0), "\u5317\u4EAC Ag09");
        VerifyTextBox(app, 36, new Thickness(10, 0, 10, 0), "\u5317\u4EAC Ag09");
        VerifyFixedLengthTextBox(app);
        VerifyPasswordBox(app, 44);
        VerifySidebarSelectionGeometry(app);
        VerifyComboBoxItemGeometry(app);
        VerifyFirstRunGuideGeometry();
        VerifyFeatureNoticeGeometry();
        VerifySettingsIpEditorGeometry();
        VerifyWorkspaceGeometry();
		VerifyRuntimeAppearanceSwitch(app);

        int captureIndex = Array.IndexOf(args, "--capture");
        if (captureIndex >= 0)
        {
            Assert(captureIndex + 3 < args.Length, "--capture requires path, width and height");
            CaptureMainWindow(
                args[captureIndex + 1],
                int.Parse(args[captureIndex + 2]),
                int.Parse(args[captureIndex + 3]),
                args.Contains("--dark", StringComparer.OrdinalIgnoreCase),
                args.Contains("--appearance", StringComparer.OrdinalIgnoreCase));
        }

        Console.WriteLine("All UI smoke tests passed.");
    }

    private static void CaptureMainWindow(string path, int width, int height, bool dark, bool appearance)
    {
        var settings = new AppSettings
        {
            AppearanceMode = dark ? AppearanceMode.Dark : AppearanceMode.Light,
            HasCompletedOnboarding = true,
            SelectedThemeId = "music-vinyl"
        };
        var window = new MainWindow(settings)
        {
            Width = width,
            Height = height,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = 20,
            Top = 20
        };
        window.Show();
        WaitForDispatcher(TimeSpan.FromMilliseconds(1400));
        if (appearance)
        {
            ((RadioButton)window.FindName("ThemeNav")).IsChecked = true;
        }
        WaitForDispatcher(TimeSpan.FromMilliseconds(250));
        window.UpdateLayout();

        int pixelWidth = Math.Max(1, (int)Math.Round(window.ActualWidth));
        int pixelHeight = Math.Max(1, (int)Math.Round(window.ActualHeight));
        var bitmap = new RenderTargetBitmap(pixelWidth, pixelHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using (var stream = File.Create(path)) encoder.Save(stream);
        window.Close();
        Console.WriteLine($"PASS captured {(dark ? "dark" : "light")} UI {pixelWidth}x{pixelHeight} to {path}");
    }

    private static void WaitForDispatcher(TimeSpan duration)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = duration
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            frame.Continue = false;
        };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    private static void VerifyAppearancePalettes()
    {
        var light = new ResourceDictionary { Source = new Uri("pack://application:,,,/Linx68.ScreenDriver.App;component/Themes/Palette.Light.xaml") };
        var dark = new ResourceDictionary { Source = new Uri("pack://application:,,,/Linx68.ScreenDriver.App;component/Themes/Palette.Dark.xaml") };
        var lightKeys = light.Keys.Cast<object>().Select(key => key.ToString()).OrderBy(key => key).ToArray();
        var darkKeys = dark.Keys.Cast<object>().Select(key => key.ToString()).OrderBy(key => key).ToArray();
        Assert(lightKeys.SequenceEqual(darkKeys), "light and dark palettes must expose identical semantic keys");
        var lightBackground = ((SolidColorBrush)light["AppBackground"]).Color;
        var darkBackground = ((SolidColorBrush)dark["AppBackground"]).Color;
        Assert(lightBackground != darkBackground && darkBackground == Color.FromRgb(16, 17, 18),
            "Graphite Studio dark background must be #101112 and differ from light mode");
        Console.WriteLine($"PASS appearance palettes share {lightKeys.Length} semantic keys");
    }

	private static void VerifyRuntimeAppearanceSwitch(Application app)
	{
		var window = new MainWindow(new AppSettings
		{
			AppearanceMode = AppearanceMode.Light,
			HasCompletedOnboarding = true
		});
		window.Show();
		WaitForDispatcher(TimeSpan.FromMilliseconds(900));
		((RadioButton)window.FindName("DarkAppearanceRadio")).IsChecked = true;
		WaitForDispatcher(TimeSpan.FromMilliseconds(100));
		Assert(((SolidColorBrush)app.FindResource("AppBackground")).Color == Color.FromRgb(16, 17, 18),
			"runtime dark-mode switch must replace the semantic palette");
		((RadioButton)window.FindName("LightAppearanceRadio")).IsChecked = true;
		WaitForDispatcher(TimeSpan.FromMilliseconds(100));
		Assert(((SolidColorBrush)app.FindResource("AppBackground")).Color != Color.FromRgb(16, 17, 18),
			"runtime light-mode switch must restore the light semantic palette");
		window.Close();
		Console.WriteLine("PASS runtime light/dark appearance switching");
	}

    private static void VerifyWorkspaceGeometry()
    {
        var window = new MainWindow();
		Assert(window.Title == "灵犀68屏幕驱动", $"unexpected product title: {window.Title}");
        var workspace = (Grid)window.FindName("WorkspaceLayout");
        var content = (Grid)window.FindName("ContentLayout");
        var gallery = (WrapPanel)window.FindName("ThemeListPanel");
		var locateCurrent = (Button)window.FindName("LocateCurrentButton");
        Assert(workspace.ColumnDefinitions[0].Width.Value == 196,
            $"workspace sidebar must be 196px: {workspace.ColumnDefinitions[0].Width.Value}");
        Assert(content.ColumnDefinitions[2].Width.Value == 288,
            $"preview rail must remain 288px: {content.ColumnDefinitions[2].Width.Value}");
        Assert(gallery.Children.OfType<RadioButton>().Count() == 19,
            "theme gallery must expose every built-in display scheme");
		Assert(locateCurrent.Visibility == Visibility.Visible && Equals(locateCurrent.Content, "定位当前"),
			"display page must expose a single locate-current action instead of mode tabs");
		var categoryRow = (StackPanel)((StackPanel)window.FindName("ThemeLibraryPanel")).Children[0];
		var musicCategory = categoryRow.Children.OfType<RadioButton>().Single(item => Equals(item.Tag, "music"));
		musicCategory.IsChecked = true;
		Assert(gallery.Children.Count == 5, "music category must narrow the gallery before locate-current");
		locateCurrent.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
		WaitForDispatcher(TimeSpan.FromMilliseconds(50));
		Assert(gallery.Children.Count == 19 && gallery.Children.OfType<RadioButton>().Any(item => item.IsChecked == true),
			"locate-current must restore all schemes and select the active card");
        window.Close();
		Console.WriteLine("PASS compact sidebar, working locate-current action, 19-card gallery and fixed preview rail");
    }

    private static void VerifyFeatureNoticeGeometry()
    {
        var stockWindow = FeatureNoticeWindow.CreateStockNotice();
        var card = (Border)stockWindow.FindName("NoticeCard");
        var content = (Grid)card.Child;
        var close = (Button)stockWindow.FindName("CloseButton");
        var acknowledge = (Button)stockWindow.FindName("AcknowledgeButton");
        var title = (TextBlock)stockWindow.FindName("TitleText");
        var details = (ItemsControl)stockWindow.FindName("DetailsList");

        Assert(stockWindow.SizeToContent == SizeToContent.Height,
            "feature notice must size itself to content");
        Assert(card.Margin.Left == card.Margin.Top && card.Margin.Top == card.Margin.Right && card.Margin.Right == card.Margin.Bottom,
            $"feature notice outer margins must be equal: {card.Margin}");
        Assert(content.Margin.Left == content.Margin.Top && content.Margin.Top == content.Margin.Right && content.Margin.Right == content.Margin.Bottom,
            $"feature notice inner margins must be equal: {content.Margin}");
        Assert(close.Width == close.Height && close.MinWidth == close.MinHeight,
            $"feature notice close button must be square: {close.Width}x{close.Height}");
        Assert(acknowledge.Height == 48 && title.Text.Contains("股票") && details.Items.Count == 3,
            "stock notice must keep its acknowledgement action and three concise points");
        stockWindow.Close();

        var mimoWindow = FeatureNoticeWindow.CreateMiMoNotice();
        var mimoTitle = (TextBlock)mimoWindow.FindName("TitleText");
        Assert(mimoTitle.Text.Contains("MiMo"), "MiMo notice must identify the integration");
        mimoWindow.Close();
        Console.WriteLine("PASS one-time feature notices share first-run geometry and content structure");
    }

    private static void VerifyFirstRunGuideGeometry()
    {
        var window = new FirstRunGuideWindow("192.168.1.100");
        var card = (Border)window.FindName("GuideCard");
        var content = (Grid)card.Child;
        var close = (Button)window.FindName("CloseButton");
        var later = (Button)window.FindName("LaterButton");
        var save = (Button)window.FindName("SaveButton");
        var ipHost = (Border)window.FindName("IpAddressHost");

        Assert(window.SizeToContent == SizeToContent.Height, "first-run guide must size itself to content");
        Assert(card.Margin.Left == card.Margin.Top && card.Margin.Top == card.Margin.Right && card.Margin.Right == card.Margin.Bottom,
            $"first-run guide outer margins must be equal: {card.Margin}");
        Assert(content.Margin.Left == content.Margin.Top && content.Margin.Top == content.Margin.Right && content.Margin.Right == content.Margin.Bottom,
            $"first-run guide inner margins must be equal: {content.Margin}");
        Assert(close.Width == close.Height && close.MinWidth == close.MinHeight,
            $"close button must be square: {close.Width}x{close.Height}, min {close.MinWidth}x{close.MinHeight}");
        Assert(later.FontSize == save.FontSize && later.FontWeight == save.FontWeight,
            "first-run guide action buttons must use the same typography");
        Assert(ipHost.Child is Grid ipGrid && ipGrid.Children.OfType<TextBox>().Count() == 4,
            "IP address editor must keep four equal input segments");
        Console.WriteLine($"PASS first-run guide square close={close.Width:0}px equal margins={content.Margin.Left:0}px segmented IP");
    }

    private static void VerifySettingsIpEditorGeometry()
    {
        var window = new MainWindow();
        var host = (Border)window.FindName("SettingsIpAddressHost");
        var safeAreaHost = (Border)window.FindName("SafeAreaInputHost");
        var firstRunWindow = new FirstRunGuideWindow("192.168.1.100");
        var firstRunHost = (Border)firstRunWindow.FindName("IpAddressHost");
        var populate = typeof(MainWindow).GetMethod("PopulateEndpointParts", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert(populate is not null, "settings IP editor must expose its population path");
        populate!.Invoke(window, ["192.168.1.100"]);

        var summary = (TextBlock)window.FindName("EndpointSummaryText");
        Assert(host.Height == firstRunHost.Height && host.CornerRadius == firstRunHost.CornerRadius,
            $"settings and first-run IP editors must share geometry: settings {host.Height}px/{host.CornerRadius}, guide {firstRunHost.Height}px/{firstRunHost.CornerRadius}");
        Assert(host.Child is Grid ipGrid && ipGrid.Children.OfType<TextBox>().Count() == 4,
            "settings IP address editor must keep four equal input segments");
        Assert(safeAreaHost.Height == host.Height && safeAreaHost.CornerRadius == host.CornerRadius,
            $"safe-area and device-address editors must share geometry: safe {safeAreaHost.Height}px/{safeAreaHost.CornerRadius}, address {host.Height}px/{host.CornerRadius}");
        Assert(window.FindName("SafeLeftTextBox") is TextBox && window.FindName("SafeTopTextBox") is TextBox &&
               window.FindName("SafeRightTextBox") is TextBox && window.FindName("SafeBottomTextBox") is TextBox,
            "safe-area editor must keep four named input segments");
        Assert(summary.Text == "192.168.1.100",
            $"sidebar endpoint summary must refresh after onboarding-style population: {summary.Text}");
        window.Close();
        firstRunWindow.Close();
        Console.WriteLine("PASS address and safe-area editors share segmented style; sidebar summary synchronizes");
    }

    private static void VerifyComboBoxItemGeometry(Application app)
    {
        var item = new ComboBoxItem
        {
            Style = (Style)app.FindResource(typeof(ComboBoxItem)),
            Content = "五日天气",
            Width = 300
        };
        item.Measure(new Size(300, double.PositiveInfinity));
        item.Arrange(new Rect(0, 0, 300, item.DesiredSize.Height));
        item.ApplyTemplate();
        item.UpdateLayout();

        var presenter = FindVisualChild<ContentPresenter>(item);
        Assert(item.DesiredSize.Height >= 40 && item.DesiredSize.Height <= 44,
            $"ComboBoxItem height must stay compact: {item.DesiredSize.Height:0.##}px");
        Assert(presenter is not null && presenter.VerticalAlignment == VerticalAlignment.Center,
            "ComboBoxItem content must be vertically centered");

        var normalHeight = item.ActualHeight;
        item.IsSelected = true;
        item.UpdateLayout();
        Assert(Math.Abs(item.ActualHeight - normalHeight) <= 0.01,
            $"ComboBoxItem height changes on selection: {normalHeight:0.##} -> {item.ActualHeight:0.##}");
        Console.WriteLine($"PASS ComboBoxItem compact height={item.ActualHeight:0.##}px");
    }

    private static void VerifySidebarSelectionGeometry(Application app)
    {
        var item = new RadioButton
        {
            Style = (Style)app.FindResource("SidebarItem"),
            Content = "电脑状态",
            Width = 280,
            Height = 56
        };
        item.Measure(new Size(280, 56));
        item.Arrange(new Rect(0, 0, 280, 56));
        item.ApplyTemplate();
        item.UpdateLayout();

        var chrome = (Border?)item.Template.FindName("Chrome", item);
        var label = FindVisualChild<TextBlock>(item);
        Assert(chrome is not null && label is not null, "SidebarItem template must expose stable chrome and label elements");
        var normalY = label!.TranslatePoint(new Point(0, 0), item).Y;
        var normalThickness = chrome!.BorderThickness;

        item.IsChecked = true;
        item.UpdateLayout();
        var selectedY = label.TranslatePoint(new Point(0, 0), item).Y;
        var selectedThickness = chrome.BorderThickness;

        Assert(normalThickness == selectedThickness,
            $"SidebarItem border thickness changes on selection: {normalThickness} -> {selectedThickness}");
        Assert(Math.Abs(normalY - selectedY) <= 0.01,
            $"SidebarItem label moves vertically on selection: {normalY:0.##} -> {selectedY:0.##}");
        Console.WriteLine($"PASS SidebarItem stable selection y={selectedY:0.##} border={selectedThickness.Top:0.##}px");
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match) return match;
            var nested = FindVisualChild<T>(child);
            if (nested is not null) return nested;
        }
        return null;
    }
    private static void VerifyTextBox(Application app, double height, Thickness padding, string text)
    {
        var textBox = new TextBox
        {
            Style = (Style)app.FindResource(typeof(TextBox)),
            Text = text,
            Width = 300,
            Height = height,
            MinHeight = height,
            Padding = padding,
            VerticalContentAlignment = VerticalAlignment.Center
        };

        var result = MeasureAndRender(textBox, (int)height);
        Assert(textBox.Padding.Top == 0 && textBox.Padding.Bottom == 0,
            $"{height}px TextBox must not use vertical padding");
        var requiredLineHeight = MeasureLineHeight(textBox, text);
        Assert(result.HostHeight >= requiredLineHeight - 0.25,
            $"{height}px TextBox content host is clipped: host={result.HostHeight:0.##}px, required={requiredLineHeight:0.##}px");
        Assert(result.InkRows >= 9 && result.InkBottom - result.InkTop >= 9,
            $"{height}px TextBox glyphs are clipped: rows={result.InkRows}, ink={result.InkTop}..{result.InkBottom}");

        var inkCenter = (result.InkTop + result.InkBottom) / 2.0;
        Assert(Math.Abs(inkCenter - (height / 2.0)) <= 2.5,
            $"{height}px TextBox glyphs are not vertically centered: center={inkCenter:0.##}");

        Console.WriteLine($"PASS TextBox {height:0}px host={result.HostHeight:0.##} ink={result.InkTop}..{result.InkBottom}");
    }

    private static void VerifyFixedLengthTextBox(Application app)
    {
        const string value = "#E1000C";
        const double rowWidth = 243;
        var row = new Grid
        {
            Width = rowWidth,
            Height = 44
        };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(88) });

        var swatch = new Border
        {
            Width = 32,
            Height = 32,
            CornerRadius = new CornerRadius(16),
            VerticalAlignment = VerticalAlignment.Center
        };
        var box = new TextBox
        {
            Style = (Style)app.FindResource(typeof(TextBox)),
            Text = value,
            Height = 44,
            Padding = new Thickness(8, 0, 8, 0),
            TextAlignment = TextAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        var button = new Button
        {
            Content = "\u9009\u62E9\u989C\u8272",
            Height = 44
        };

        Grid.SetColumn(box, 2);
        Grid.SetColumn(button, 4);
        row.Children.Add(swatch);
        row.Children.Add(box);
        row.Children.Add(button);

        row.Measure(new Size(rowWidth, 44));
        row.Arrange(new Rect(0, 0, rowWidth, 44));
        row.UpdateLayout();
        box.ApplyTemplate();
        box.UpdateLayout();
        box.Select(value.Length, 0);

        var boxLeft = box.TranslatePoint(new Point(0, 0), row).X;
        var buttonLeft = button.TranslatePoint(new Point(0, 0), row).X;
        var boxRight = boxLeft + box.ActualWidth;
        Assert(box.ActualWidth >= 100,
            $"accent HEX field is too narrow: {box.ActualWidth:0.##}px");
        Assert(boxRight + 8 <= buttonLeft + 0.01,
            $"accent controls overlap: fieldRight={boxRight:0.##}, buttonLeft={buttonLeft:0.##}");

        var caret = box.GetRectFromCharacterIndex(value.Length);
        Assert(!caret.IsEmpty, "accent HEX field must expose its trailing caret");
        Assert(caret.Right <= box.ActualWidth - box.Padding.Right + 1,
            $"accent HEX value is clipped at the trailing edge: caret={caret.Right:0.##}, width={box.ActualWidth:0.##}");

        var renderBox = new TextBox
        {
            Style = (Style)app.FindResource(typeof(TextBox)),
            Text = value,
            Width = box.ActualWidth,
            Height = 44,
            Padding = box.Padding,
            TextAlignment = TextAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        renderBox.Measure(new Size(box.ActualWidth, 44));
        renderBox.Arrange(new Rect(0, 0, box.ActualWidth, 44));
        renderBox.ApplyTemplate();
        renderBox.UpdateLayout();

        var pixelWidth = (int)Math.Ceiling(renderBox.ActualWidth);
        var bitmap = new RenderTargetBitmap(pixelWidth, 44, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(renderBox);
        var pixels = new byte[pixelWidth * 44 * 4];
        bitmap.CopyPixels(pixels, pixelWidth * 4, 0);
        var inkColumns = new List<int>();
        for (var x = 2; x < pixelWidth - 2; x++)
        {
            var darkPixels = 0;
            for (var y = 10; y < 34; y++)
            {
                var offset = ((y * pixelWidth) + x) * 4;
                if (pixels[offset] < 100 &&
                    pixels[offset + 1] < 100 &&
                    pixels[offset + 2] < 100 &&
                    pixels[offset + 3] > 100)
                {
                    darkPixels++;
                }
            }

            if (darkPixels >= 2)
            {
                inkColumns.Add(x);
            }
        }

        Assert(inkColumns.Count > 0, "accent HEX field did not render visible text");
        Assert(inkColumns.Max() < pixelWidth - box.Padding.Right,
            $"accent HEX ink reaches the clipping boundary: right={inkColumns.Max()}");
        Console.WriteLine(
            $"PASS accent row width={rowWidth:0}px field={box.ActualWidth:0.##}px gap={buttonLeft - boxRight:0.##}px caret={caret.Right:0.##}");
    }

    private static void VerifyPasswordBox(Application app, double height)
    {
        var box = new PasswordBox
        {
            Style = (Style)app.FindResource(typeof(PasswordBox)),
            Password = "Ag09",
            Width = 300,
            Height = height,
            VerticalContentAlignment = VerticalAlignment.Center
        };

        box.Measure(new Size(300, height));
        box.Arrange(new Rect(0, 0, 300, height));
        box.ApplyTemplate();
        box.UpdateLayout();
        var host = (FrameworkElement?)box.Template.FindName("PART_ContentHost", box);

        Assert(box.Padding.Top == 0 && box.Padding.Bottom == 0,
            "PasswordBox must not use vertical padding");
        var requiredLineHeight = MeasureLineHeight(box, "Ag09");
        Assert(host is not null && host.ActualHeight >= requiredLineHeight - 0.25,
            $"PasswordBox content host is clipped: host={host?.ActualHeight:0.##}px, required={requiredLineHeight:0.##}px");
        Console.WriteLine($"PASS PasswordBox {height:0}px host={host!.ActualHeight:0.##}");
    }

    private static double MeasureLineHeight(Control control, string sample)
    {
        var text = new TextBlock
        {
            Text = sample,
            FontFamily = control.FontFamily,
            FontSize = control.FontSize,
            FontWeight = control.FontWeight,
            FontStyle = control.FontStyle,
            FontStretch = control.FontStretch
        };
        text.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return text.DesiredSize.Height;
    }
    private static RenderResult MeasureAndRender(TextBox textBox, int pixelHeight)
    {
        textBox.Measure(new Size(300, pixelHeight));
        textBox.Arrange(new Rect(0, 0, 300, pixelHeight));
        textBox.ApplyTemplate();
        textBox.UpdateLayout();

        var host = (FrameworkElement?)textBox.Template.FindName("PART_ContentHost", textBox);
        Assert(host is not null, "TextBox template must expose PART_ContentHost");

        var bitmap = new RenderTargetBitmap(300, pixelHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(textBox);
        var pixels = new byte[300 * pixelHeight * 4];
        bitmap.CopyPixels(pixels, 300 * 4, 0);

        var inkRows = new List<int>();
        for (var y = 2; y < pixelHeight - 2; y++)
        {
            var darkPixels = 0;
            for (var x = 8; x < 150; x++)
            {
                var offset = ((y * 300) + x) * 4;
                if (pixels[offset] < 100 &&
                    pixels[offset + 1] < 100 &&
                    pixels[offset + 2] < 100 &&
                    pixels[offset + 3] > 100)
                {
                    darkPixels++;
                }
            }

            if (darkPixels >= 2)
            {
                inkRows.Add(y);
            }
        }

        Assert(inkRows.Count > 0, "TextBox did not render visible glyph ink");
        return new RenderResult(host!.ActualHeight, inkRows.Min(), inkRows.Max(), inkRows.Count);
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed record RenderResult(double HostHeight, int InkTop, int InkBottom, int InkRows);
}
