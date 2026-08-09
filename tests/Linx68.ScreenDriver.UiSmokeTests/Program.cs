using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Ellipse = System.Windows.Shapes.Ellipse;
using System.IO;
using Linx68.ScreenDriver.App;
using Linx68.ScreenDriver.App.ViewModels;
using Linx68.ScreenDriver.Application;
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
        VerifyShellNavigationViewModel();
        VerifyScreenViewModel();
        VerifyDataServicesViewModel();
        VerifyAppearanceViewModel();
        VerifyAutomationViewModel();
        VerifySettingsViewModel();

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
		VerifyDataServicesGeometry();
		VerifyCompactPreviewGeometry();
		VerifyDevicePreviewPlaceholder();
		VerifyThemeCategoryNavigation();
		VerifyManualThemeSelectionStopsAutoMusicSwitch();
		VerifyLatestThemeRefreshWins();
		VerifyAutomationControlDependency();
		VerifyNavigationResetsScrollPosition();
		VerifyRuntimeAppearanceSwitch(app);
        VerifySaveFailureIsHandled();

        int captureIndex = Array.IndexOf(args, "--capture");
        if (captureIndex >= 0)
        {
            Assert(captureIndex + 3 < args.Length, "--capture requires path, width and height");
            bool allSchemes = args.Contains("--all-schemes", StringComparer.OrdinalIgnoreCase);
            CaptureMainWindow(
                args[captureIndex + 1],
                int.Parse(args[captureIndex + 2]),
                int.Parse(args[captureIndex + 3]),
                args.Contains("--dark", StringComparer.OrdinalIgnoreCase),
                args.Contains("--appearance", StringComparer.OrdinalIgnoreCase)
                    ? "appearance"
                    : args.Contains("--data-services", StringComparer.OrdinalIgnoreCase)
                        ? "dataServices"
                        : args.Contains("--automation", StringComparer.OrdinalIgnoreCase)
                        ? "automation"
                        : args.Contains("--settings", StringComparer.OrdinalIgnoreCase)
                            ? "settings"
                            : args.Contains("--about", StringComparer.OrdinalIgnoreCase)
                                ? "about"
                                : "screen",
                allSchemes);
        }

        Console.WriteLine("All UI smoke tests passed.");
    }

    private static void VerifyShellNavigationViewModel()
    {
        var viewModel = new ShellViewModel();
        Assert(viewModel.IsScreenPage && viewModel.PageTitle == "显示方案",
            "shell must start on the display-scheme page");
        viewModel.NavigateCommand.Execute("appearance");
        Assert(viewModel.IsAppearancePage
               && !viewModel.IsScreenPage
               && viewModel.PageTitle == "外观",
            "shell navigation command must update page state and title");
		viewModel.NavigateCommand.Execute("dataServices");
		Assert(viewModel.IsDataServicesPage
		       && !viewModel.IsAppearancePage
		       && viewModel.PageTitle == "数据服务",
			"shell must expose the shared data-services page as a first-level destination");
        Console.WriteLine("PASS MVVM shell navigation state and command");
    }

    private static void VerifyScreenViewModel()
    {
        var definitions = BuiltInThemes.CreateDefinitions(new ImageTheme());
        var viewModel = new ScreenViewModel();
        int selectionCount = 0;
        viewModel.ThemeSelected += _ => selectionCount++;
        viewModel.SetThemes(
            definitions.Select(definition => new ThemeCardViewModel(definition, preview: null)),
            "clock-weather");
        Assert(viewModel.ThemeGroups.Count == 4
               && viewModel.ThemeGroups.Single(group => group.Id == "music").Themes.Count == 1
               && viewModel.ThemeGroups.Sum(group => group.Themes.Count) == definitions.Count
               && !viewModel.IsAllCategorySelected
               && viewModel.VisibleThemes.All(theme => theme.Definition.CategoryId == viewModel.SelectedTheme!.Definition.CategoryId),
            "screen view model must open the single confirmed music scheme through the left navigation");
        viewModel.SelectCategory("all");
        Assert(viewModel.IsAllCategorySelected && viewModel.VisibleThemes.Count == definitions.Count,
            "the all-schemes category must remain available on demand");
        viewModel.SelectCategory("music");
        viewModel.SelectTheme("music", notify: true);
        viewModel.UpdateCardWidth(500);
        Assert(!viewModel.IsAllCategorySelected
               && viewModel.ThemeGroups.Single(group => group.Id == "music").IsSelected
               && viewModel.VisibleThemes.All(theme => theme.Definition.CategoryId == "music")
               && viewModel.SelectedTheme?.Id == "music"
               && selectionCount == 1
               && viewModel.ThemeGroups.SelectMany(group => group.Themes).All(theme => theme.CardWidth >= 148)
               && viewModel.ThemeGroups.SelectMany(group => group.Themes).All(theme => theme.CardWidth * 2 + 24 <= 500),
            "screen view model must synchronize category filtering, selection and responsive compact cards");
        viewModel.UpdateCardWidth(960);
        Assert(viewModel.ThemeGroups.SelectMany(group => group.Themes).All(theme => theme.CardWidth * 3 + 36 <= 960),
            "wide galleries must use three columns to reduce unnecessary vertical scrolling");
        Console.WriteLine("PASS MVVM screen categories, selection and responsive compact cards");
    }

    private static void VerifyAppearanceViewModel()
    {
        var settings = new AppSettings
        {
            AppearanceMode = AppearanceMode.Light,
            AccentColor = "#123456",
            ImageTimePlacement = ImageTimePlacement.Top,
            SelectedFontId = ScreenFontOption.Default.Id
        };
        var viewModel = new AppearanceViewModel();
        viewModel.Load(settings, [ScreenFontOption.Default]);
        Assert(viewModel.IsLightAppearance
               && viewModel.IsAccentColorValid
               && viewModel.SelectedImageTimePlacement?.Value == ImageTimePlacement.Top,
            "appearance view model must load the persisted display settings");
        viewModel.IsDarkAppearance = true;
        viewModel.AccentColor = "#E4694C";
        viewModel.SelectedImageTimePlacement = viewModel.ImageTimePlacements.Single(option =>
            option.Value == ImageTimePlacement.Bottom);
        viewModel.ApplyTo(settings);
        Assert(settings.AppearanceMode == AppearanceMode.Dark
               && settings.AccentColor == "#E4694C"
               && settings.ImageTimePlacement == ImageTimePlacement.Bottom,
            "appearance view model must apply edited settings without reading UI controls");
        Console.WriteLine("PASS MVVM appearance state and settings mapping");
    }

    private static void VerifyAutomationViewModel()
    {
        var settings = new AppSettings
        {
            AutoPush = false,
            RefreshSeconds = 99,
            AutoSwitchToMusic = true
        };
        var viewModel = new AutomationViewModel();
        viewModel.Load(settings);
        Assert(!viewModel.AutoPush
               && viewModel.RefreshSeconds == 30
               && viewModel.AutoSwitchToMusic,
            "automation view model must load and normalize saved automation settings");
        viewModel.RefreshSeconds = 0;
        viewModel.AutoPush = true;
        viewModel.AutoSwitchToMusic = false;
        viewModel.ApplyTo(settings);
        Assert(settings.RefreshSeconds == 1
               && settings.AutoPush
               && !settings.AutoSwitchToMusic,
            "automation view model must clamp and apply edited automation settings");
        Console.WriteLine("PASS MVVM automation state and settings mapping");
    }

    private static void VerifySettingsViewModel()
    {
        var settings = new AppSettings
        {
            DeviceEndpoint = "http://192.168.1.100/image/upload",
            SafeArea = new ScreenInsets(8, 48, 8, 10),
            MinimizeToTray = false,
            CloseToTray = false,
            StartMinimized = true,
            LaunchAtStartup = true
        };
        var viewModel = new SettingsViewModel();
        viewModel.Load(settings);
        Assert(viewModel.EndpointIp == "192.168.1.100"
               && viewModel.SafeTop == "48"
               && !viewModel.MinimizeToTray
               && viewModel.StartMinimized,
            "settings view model must load device, safe-area and tray settings");
        Assert(viewModel.SetEndpoint("10.0.0.12"),
            "settings view model must accept a valid IPv4 address");
        viewModel.SafeLeft = "70";
        viewModel.SafeRight = "70";
        viewModel.ApplyTo(settings);
        Assert(settings.DeviceEndpoint == "http://10.0.0.12/image/upload"
               && settings.SafeArea == new ScreenInsets(60, 48, 60, 10)
               && settings.LaunchAtStartup,
            "settings view model must normalize endpoint and clamp safe-area values");
        Console.WriteLine("PASS MVVM settings state and device endpoint mapping");
    }

    private static void CaptureMainWindow(string path, int width, int height, bool dark, string page, bool allSchemes)
    {
        var settings = new AppSettings
        {
            AppearanceMode = dark ? AppearanceMode.Dark : AppearanceMode.Light,
            HasCompletedOnboarding = true,
            SelectedThemeId = "music"
        };
        var window = new MainWindow(settings, new InMemorySettingsStore(settings))
        {
            Width = width,
            Height = height,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = 20,
            Top = 20
        };
        var shell = (ShellViewModel)window.DataContext;
        if (!string.Equals(page, "screen", StringComparison.OrdinalIgnoreCase))
        {
            shell.NavigateCommand.Execute(page);
        }
        window.Show();
        WaitForDispatcher(TimeSpan.FromMilliseconds(1400));
        if (allSchemes)
        {
            ((ShellViewModel)window.DataContext).Screen.SelectCategory("all");
        }
        WaitForDispatcher(TimeSpan.FromMilliseconds(250));
        var captureRoot = (FrameworkElement)window.FindName("WindowRoot");
        InteractionMotion.Reset(captureRoot);
        window.UpdateLayout();

        int pixelWidth = Math.Max(1, (int)Math.Round(captureRoot.ActualWidth));
        int pixelHeight = Math.Max(1, (int)Math.Round(captureRoot.ActualHeight));
        var bitmap = new RenderTargetBitmap(pixelWidth, pixelHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(captureRoot);
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

    private static void VerifyDataServicesViewModel()
    {
        var viewModel = new DataServicesViewModel();
        string preservedSummary = viewModel.Codex.Summary;
        viewModel.Codex.BeginRefresh();
        Assert(viewModel.Codex.State == DataLoadState.Loading
               && viewModel.Codex.Summary == preservedSummary
               && viewModel.Codex.StateLabel == "更新中",
            "loading a data source must preserve its last summary and expose a stable loading state");
        viewModel.Codex.Set(DataLoadState.Ready, "可用 80%", "读取成功", TimeSpan.FromMilliseconds(120));
        Assert(viewModel.Codex.IsReady && viewModel.Codex.Timing == "120 ms",
            "ready data state must expose the latest value and measured duration");
        viewModel.Codex.Set(DataLoadState.Stale, "可用 80%", "保留上次数据");
        Assert(viewModel.Codex.IsProblem && viewModel.Codex.StateLabel == "上次数据",
            "stale data must remain visibly distinct without clearing the last value");
        viewModel.Codex.Set(DataLoadState.Error, "尚未获得 Codex 数据", "读取失败");
        viewModel.CompleteRefresh(TimeSpan.FromMilliseconds(250), DateTimeOffset.Now);
        Assert(viewModel.OverallStatus.Contains("需要留意", StringComparison.Ordinal),
            "overall data health must surface stale or error states");
        Console.WriteLine("PASS data services five-state projection and stable loading content");
    }

    private static bool WaitForCondition(Func<bool> condition, TimeSpan timeout)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        while (!condition() && stopwatch.Elapsed < timeout)
        {
            WaitForDispatcher(TimeSpan.FromMilliseconds(50));
        }

        return condition();
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
			HasCompletedOnboarding = true,
			MinimizeToTray = false,
			CloseToTray = false
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

    private static void VerifySaveFailureIsHandled()
    {
        var settings = new AppSettings
        {
            HasCompletedOnboarding = true,
            AutoPush = false,
            MinimizeToTray = false,
            CloseToTray = false
        };
        var window = new MainWindow(settings, new ThrowingSettingsStore(settings));
        window.Show();
        WaitForDispatcher(TimeSpan.FromMilliseconds(900));
        ((RadioButton)window.FindName("DarkAppearanceRadio")).IsChecked = true;
        var status = (TextBlock)window.FindName("DeviceStatusText");
		Assert(WaitForCondition(() => status.Text == "保存失败", TimeSpan.FromSeconds(5)),
            "a settings-store failure must be handled without an unhandled UI exception");
        window.Close();
        Console.WriteLine("PASS settings save failures stay in-app instead of crashing the UI test host");
    }

    private static void VerifyWorkspaceGeometry()
    {
        var window = new MainWindow();
		Assert(window.Title == "灵犀68屏幕驱动", $"unexpected product title: {window.Title}");
        var windowRoot = (Border)window.FindName("WindowRoot");
        var titleBar = (Grid)window.FindName("TitleBar");
        var workspace = (Grid)window.FindName("WorkspaceLayout");
        var content = (Grid)window.FindName("ContentLayout");
		var themeCategories = (ItemsControl)window.FindName("ThemeCategoryPanel");
		var themeGallery = (ItemsControl)window.FindName("ThemeGalleryPanel");
		var themeCategoryNavigation = (StackPanel)window.FindName("ThemeCategoryNavigation");
		var shell = (ShellViewModel)window.DataContext;
		var locateCurrent = (Button)window.FindName("LocateCurrentButton");
		var endpointShortcut = (Button)window.FindName("EndpointShortcutButton");
		var sidebarNavigation = (StackPanel)window.FindName("SidebarNavigationPanel");
		var screenNavigation = (RadioButton)window.FindName("ScreenNav");
		var deviceStatus = (TextBlock)window.FindName("DeviceStatusText");
		var deviceStatusDot = (System.Windows.Shapes.Ellipse)window.FindName("DeviceStatusDot");
		var previewStatus = (TextBlock)window.FindName("PreviewStatusText");
		var previewThemeSummary = (Border)window.FindName("PreviewThemeSummary");
		var previewRail = (Border)window.FindName("DevicePreviewRail");
		var windowBehavior = (Border)window.FindName("WindowBehaviorCard");
		var startupBehavior = (Border)window.FindName("StartupBehaviorCard");
        window.UpdateLayout();
		WaitForDispatcher(TimeSpan.FromMilliseconds(50));
		Assert(windowRoot.Background is not null
		       && titleBar.Background is not null
		       && System.Windows.Shell.WindowChrome.GetWindowChrome(window)?.CaptionHeight == 56,
			"the application frame must keep a stable themed background and compact title bar");
        Assert(workspace.ColumnDefinitions[0].Width.Value == 210,
			$"workspace sidebar must follow the 210px redesign: {workspace.ColumnDefinitions[0].Width.Value}");
        Assert(content.ColumnDefinitions[2].Width.IsAuto && previewRail.Width == 288,
			"preview rail must remain 288px on the screen page and collapse cleanly elsewhere");
		Assert(themeCategories.Items.Count == shell.Screen.ThemeGroups.Count
		       && themeGallery.Items.Count == shell.Screen.VisibleThemes.Count
		       && shell.Screen.ThemeGroups.Sum(group => group.Themes.Count) == shell.Screen.AllThemeCount,
			"display schemes must use the left category navigation with one compact gallery");
		Assert(shell.Screen.VisibleThemes.All(theme => !string.IsNullOrWhiteSpace(theme.Metadata))
		       && shell.Screen.ThemeGroups.Single(group => group.Id == "music").Themes.All(theme => theme.Metadata.Contains("音乐")),
			"theme cards must expose readable category and dynamic-state metadata, including the confirmed music scheme");
		Assert(themeCategoryNavigation.VerticalAlignment == VerticalAlignment.Top
		       && themeCategoryNavigation.Orientation == Orientation.Horizontal
		       && themeCategoryNavigation.MinHeight == 0,
			"scheme categories must use compact horizontal chips instead of a second sidebar");
		Assert(window.FindName("ThemeGroupPanel") is null && window.FindName("ThemeListPanel") is null,
			"the old stacked category sections and mixed gallery must no longer be used");
		Assert(sidebarNavigation.Children.Count == 6
		       && screenNavigation.FontSize == 15,
			"sidebar navigation must include data services and keep readable first-level destinations");
		Assert(deviceStatus.Text == "设备离线"
		       && deviceStatus.FontSize == 14
		       && deviceStatus.Foreground == deviceStatusDot.Fill,
			"device status must start as a clear offline state with a matching status indicator");
		Assert(previewStatus.Text == "本地预览",
			"offline devices must explain that the preview remains local and responsive");
		Assert(previewThemeSummary.Child is StackPanel { Children.Count: 3 },
			"the preview rail must summarize the currently selected scheme below the device frame");
		Assert(windowBehavior.Padding == new Thickness(0)
		       && startupBehavior.Padding == new Thickness(0),
			"related tray and startup controls must be compactly grouped into their respective setting cards");
		Assert(locateCurrent.Visibility == Visibility.Visible && Equals(locateCurrent.Content, "定位当前"),
			"display page must expose a single locate-current action instead of mode tabs");
		locateCurrent.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
		WaitForDispatcher(TimeSpan.FromMilliseconds(50));
		Assert(shell.Screen.SelectedTheme?.IsSelected == true,
			"locate-current must find the active card inside its category section");
		endpointShortcut.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
		WaitForDispatcher(TimeSpan.FromMilliseconds(50));
		Assert(shell.IsSettingsPage && endpointShortcut.ToolTip?.ToString()?.Contains("设置") == true,
			"clicking the device-address shortcut must open the settings page");
        window.Close();
        Console.WriteLine("PASS single sidebar, horizontal scheme categories, compact gallery and fixed screen preview rail");
    }

    private static void VerifyCompactPreviewGeometry()
    {
        var settings = new AppSettings
        {
            HasCompletedOnboarding = true,
            SelectedThemeId = "music",
            MinimizeToTray = false,
            CloseToTray = false
        };
        var window = new MainWindow(settings, new InMemorySettingsStore(settings))
        {
            Width = 1080,
            Height = 680,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = 20,
            Top = 20
        };
        window.Show();
        WaitForDispatcher(TimeSpan.FromMilliseconds(900));
        window.UpdateLayout();

        var previewHost = (Viewbox)window.FindName("DevicePreviewHost");
        var devicePreview = (DevicePreviewControl)window.FindName("DevicePreview");
        var summary = (Border)window.FindName("PreviewThemeSummary");
        Rect hostBounds = previewHost.TransformToAncestor(window)
            .TransformBounds(new Rect(new Point(), previewHost.RenderSize));
        Rect summaryBounds = summary.TransformToAncestor(window)
            .TransformBounds(new Rect(new Point(), summary.RenderSize));

        Assert(previewHost.Stretch == Stretch.Uniform
               && previewHost.StretchDirection == StretchDirection.DownOnly
               && previewHost.ActualHeight > 0
               && previewHost.ActualHeight < devicePreview.Height,
            "the compact preview must scale the device uniformly down instead of clipping it");
        Assert(hostBounds.Top >= 0
               && hostBounds.Bottom <= summaryBounds.Top - summary.Margin.Top + 0.5
               && summaryBounds.Bottom <= window.ActualHeight,
            "the compact preview frame and current-scheme summary must remain fully inside the window");

        window.Close();
        Console.WriteLine("PASS compact preview scales without clipping the device frame or summary");
    }

    private static void VerifyDataServicesGeometry()
    {
        var settings = new AppSettings
        {
            HasCompletedOnboarding = true,
            MinimizeToTray = false,
            CloseToTray = false,
            Weather = new WeatherSettings { LocationQuery = "北海", UseAutomaticLocation = true },
            Music = new MusicSettings { EnableOnlineLyrics = true }
        };
        var window = new MainWindow(settings, new InMemorySettingsStore(settings))
        {
            Width = 1080,
            Height = 680
        };
        window.Show();
        WaitForDispatcher(TimeSpan.FromMilliseconds(900));
        ((RadioButton)window.FindName("DataServicesNav")).IsChecked = true;
        WaitForDispatcher(TimeSpan.FromMilliseconds(120));
        window.UpdateLayout();

        var panel = (StackPanel)window.FindName("DataServicesPanel");
        var previewRail = (Border)window.FindName("DevicePreviewRail");
        var refreshAll = (Button)window.FindName("RefreshAllDataButton");
        var weatherAuto = (RadioButton)window.FindName("DataWeatherAutoRadio");
        var weatherManual = (RadioButton)window.FindName("DataWeatherManualRadio");
        var city = (TextBox)window.FindName("DataWeatherCityTextBox");
        Assert(panel.IsVisible
               && panel.ActualWidth <= ((ScrollViewer)window.FindName("ThemeScrollViewer")).ViewportWidth + 1
               && previewRail.Visibility == Visibility.Collapsed
               && refreshAll.IsVisible,
            "data services must use the full content width while the device preview rail is collapsed");
        Assert(weatherAuto.IsChecked == true
               && weatherManual.IsChecked == false
               && !city.IsEnabled
               && city.Text == "北海",
            "weather controls must clearly distinguish Windows auto-location from the saved manual fallback city");
        Assert(window.FindName("CodexServiceCard") is Border
               && window.FindName("MusicServiceCard") is Border
               && window.FindName("WeatherServiceCard") is Border
               && window.FindName("RefreshActivityCard") is Border,
            "data services must expose fixed cards for Codex, music, weather and refresh timings");
        window.Close();
        Console.WriteLine("PASS data services status cards and 1080x680 location controls");
    }

    private static void VerifyDevicePreviewPlaceholder()
    {
        var preview = new DevicePreviewControl();
        preview.Measure(new Size(154, 440));
        preview.Arrange(new Rect(0, 0, 154, 440));
        preview.UpdateLayout();

        var bitmap = new RenderTargetBitmap(154, 440, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(preview);
        var pixels = new byte[154 * 440 * 4];
        bitmap.CopyPixels(pixels, 154 * 4, 0);
        var placeholderInk = 0;
        for (var y = 160; y < 280; y++)
        {
            for (var x = 20; x < 134; x++)
            {
                var offset = ((y * 154) + x) * 4;
                if (pixels[offset] + pixels[offset + 1] + pixels[offset + 2] > 180)
                {
                    placeholderInk++;
                }
            }
        }

        Assert(placeholderInk > 40,
            "an empty device preview must render a visible wait-for-data placeholder instead of a pure black screen");
        Console.WriteLine($"PASS empty device preview placeholder ink={placeholderInk}");
    }

    private static void VerifyThemeCategoryNavigation()
    {
        var settings = new AppSettings
        {
            HasCompletedOnboarding = true,
            SelectedThemeId = "music-pulse",
            MinimizeToTray = false,
            CloseToTray = false
        };
        var window = new MainWindow(settings, new InMemorySettingsStore(settings));
        window.Show();
        WaitForDispatcher(TimeSpan.FromMilliseconds(900));

        var shell = (ShellViewModel)window.DataContext;
        var categories = (ItemsControl)window.FindName("ThemeCategoryPanel");
        var music = shell.Screen.ThemeGroups.Single(group => group.Id == "music");
        var musicCategory = FindVisualChild<RadioButton>(
            categories.ItemContainerGenerator.ContainerFromItem(music));
        Assert(settings.SelectedThemeId == "music" && shell.Screen.SelectedTheme?.Id == "music" && musicCategory is not null,
            "saved music presentation variants must migrate to the confirmed music scheme and render an interactive radio button");

        Assert(!InteractionMotion.GetIsInteractive(musicCategory!),
            "scheme categories must not scale beyond the scroll viewport and clip their left border");

        var categoryCommand = musicCategory!.Command;
		Assert(categoryCommand?.CanExecute(musicCategory.CommandParameter) == true,
			"a scheme category must bind an executable selection command");
		categoryCommand!.Execute(musicCategory.CommandParameter);
        WaitForDispatcher(TimeSpan.FromMilliseconds(100));
        Assert(music.IsSelected
               && !shell.Screen.IsAllCategorySelected
               && shell.Screen.VisibleThemes.Count == music.Themes.Count
               && shell.Screen.VisibleThemes.All(theme => theme.Definition.CategoryId == "music"),
            "selecting a scheme category must refresh the gallery to its matching themes");

        foreach (ThemeGroupViewModel group in shell.Screen.ThemeGroups)
        {
            var categoryButton = FindVisualChild<RadioButton>(
                categories.ItemContainerGenerator.ContainerFromItem(group));
            var label = categoryButton is null ? null : FindVisualChild<TextBlock>(categoryButton);
            Assert(categoryButton is not null
                   && label?.Text == group.DisplayName
                   && categoryButton.Command?.CanExecute(categoryButton.CommandParameter) == true,
                $"scheme category {group.Id} must keep a visible, executable left-nav label");

            categoryButton!.Command!.Execute(categoryButton.CommandParameter);
            WaitForDispatcher(TimeSpan.FromMilliseconds(80));
            Assert(group.IsSelected
                   && !shell.Screen.IsAllCategorySelected
                   && shell.Screen.VisibleThemes.Count == group.Themes.Count
                   && shell.Screen.VisibleThemes.All(theme => theme.Definition.CategoryId == group.Id)
                   && shell.Screen.VisibleThemeCountText == $"{group.DisplayName} · {group.Themes.Count} 个",
                $"scheme category {group.Id} must synchronize its selection, header and gallery: selected={group.IsSelected}; all={shell.Screen.IsAllCategorySelected}; visible={shell.Screen.VisibleThemes.Count}/{group.Themes.Count}; ids={string.Join(',', shell.Screen.VisibleThemes.Select(theme => theme.Definition.CategoryId).Distinct())}; header={shell.Screen.VisibleThemeCountText}");
        }

        shell.Screen.SelectCategoryCommand.Execute("all");
        WaitForDispatcher(TimeSpan.FromMilliseconds(80));
        Assert(shell.Screen.IsAllCategorySelected
               && shell.Screen.ThemeGroups.All(group => !group.IsSelected)
               && shell.Screen.VisibleThemes.Count == shell.Screen.AllThemeCount
               && shell.Screen.VisibleThemeCountText == $"全部方案 · {shell.Screen.AllThemeCount} 个",
            "all-schemes navigation must restore the full gallery without hiding category labels");

        window.Close();
        Console.WriteLine("PASS every scheme category keeps its label and synchronizes the gallery");
    }

    private static void VerifyAutomationControlDependency()
    {
        var settings = new AppSettings
        {
            HasCompletedOnboarding = true,
            AutoPush = true,
            MinimizeToTray = false,
            CloseToTray = false
        };
        var window = new MainWindow(settings, new InMemorySettingsStore(settings));
        window.Show();
        WaitForDispatcher(TimeSpan.FromMilliseconds(900));
        ((RadioButton)window.FindName("AutomationNav")).IsChecked = true;
        WaitForDispatcher(TimeSpan.FromMilliseconds(100));
        var autoPush = (CheckBox)window.FindName("AutoPushCheckBox");
        var refreshPanel = (Grid)window.FindName("RefreshIntervalPanel");
        var refreshSlider = (Slider)window.FindName("RefreshIntervalSlider");
		var deviceStatusDot = (Ellipse)window.FindName("DeviceStatusDot");
		var automationPanel = (Grid)window.FindName("AutomationPanel");
		var automationStatus = (Border)window.FindName("AutomationStatusCard");
		var autoMusicCard = (Border)window.FindName("AutoMusicCard");
		var autoMusicCheckBox = (CheckBox)window.FindName("AutoMusicCheckBox");
		var autoMusicDescription = (TextBlock)window.FindName("AutoMusicDescription");
        Assert(autoPush.IsChecked == true && refreshPanel.IsEnabled && refreshSlider.IsEnabled,
            "the refresh interval must be editable while timed push is enabled");
		Assert(!deviceStatusDot.HasAnimatedProperties,
			"device connection state must remain static instead of continuously pulsing");
		bool hasCoverLyricsDescription = autoMusicDescription.Text.Contains("封面歌词", StringComparison.Ordinal);
		bool hasRemovedMediaThemeCard = window.FindName("MediaThemeAutoSwitchCard") is not null;
		Assert(automationPanel.ColumnDefinitions.Count == 3
			&& automationStatus.IsVisible
			&& autoMusicCard.IsVisible
			&& autoMusicCheckBox.IsVisible
			&& hasCoverLyricsDescription
			&& !hasRemovedMediaThemeCard,
			$"automation must retain timed push and cover-lyrics controls beside a status rail: statusVisible={automationStatus.IsVisible}, cardVisible={autoMusicCard.IsVisible}, switchVisible={autoMusicCheckBox.IsVisible}, coverLyrics={hasCoverLyricsDescription}, oldCard={hasRemovedMediaThemeCard}");
        autoPush.IsChecked = false;
        WaitForDispatcher(TimeSpan.FromMilliseconds(100));
        Assert(!refreshPanel.IsEnabled && !refreshSlider.IsEnabled && refreshPanel.Opacity < 1,
            "the refresh interval must be visibly disabled while timed push is off");
        window.Close();
        Console.WriteLine("PASS automation keeps the cover-lyrics switch without redundant media-theme controls");
    }

    private static void VerifyLatestThemeRefreshWins()
    {
        var settings = new AppSettings
        {
            HasCompletedOnboarding = true,
            HasAcknowledgedCodexNotice = true,
            AiQuota = new AiQuotaSettings { DisplayName = "Codex" },
            SelectedThemeId = "system",
            AutoPush = false,
            MinimizeToTray = false,
            CloseToTray = false
        };
        var window = new MainWindow(settings, new InMemorySettingsStore(settings));
        window.Show();
        WaitForDispatcher(TimeSpan.FromMilliseconds(900));
        var shell = (ShellViewModel)window.DataContext;
        shell.Screen.SelectTheme("ai-quota", notify: true);
        shell.Screen.SelectTheme("day-rhythm", notify: true);
        bool settled = WaitForCondition(
            () => ((TextBlock)window.FindName("CurrentThemeNameText")).Text == "今日节奏"
                  && ((TextBlock)window.FindName("PreviewStatusText")).Text is not "正在更新 · 保留上次预览",
            TimeSpan.FromSeconds(5));
        string? effectiveThemeId = typeof(MainWindow)
            .GetField("_lastEffectiveThemeId", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.GetValue(window) as string;
        Assert(settled
               && shell.Screen.SelectedTheme?.Id == "day-rhythm"
               && effectiveThemeId == "day-rhythm",
            $"a cancelled Codex refresh must not overwrite the latest theme selection: selected={shell.Screen.SelectedTheme?.Id}; effective={effectiveThemeId}");
        window.Close();
        Console.WriteLine("PASS latest theme request cancels stale Codex refresh results");
    }

    private static void VerifyManualThemeSelectionStopsAutoMusicSwitch()
    {
        var settings = new AppSettings
        {
            HasCompletedOnboarding = true,
            SelectedThemeId = "music",
            AutoSwitchToMusic = true,
            AutoPush = false,
            MinimizeToTray = false,
            CloseToTray = false
        };
        var window = new MainWindow(settings, new InMemorySettingsStore(settings));
        window.Show();
        WaitForDispatcher(TimeSpan.FromMilliseconds(900));

        var shell = (ShellViewModel)window.DataContext;
        shell.Screen.SelectTheme("dashboard", notify: true);
        WaitForDispatcher(TimeSpan.FromMilliseconds(900));

        Assert(shell.Screen.SelectedTheme?.Id == "dashboard"
               && !shell.Automation.AutoSwitchToMusic
               && !settings.AutoSwitchToMusic
               && settings.SelectedThemeId == "dashboard",
            "manually selecting another scheme must override music auto switching instead of reverting the selection");

        window.Close();
        Console.WriteLine("PASS manual scheme selection takes priority over music auto switching");
    }

    private static void VerifyNavigationResetsScrollPosition()
    {
        var settings = new AppSettings
        {
            HasCompletedOnboarding = true,
            MinimizeToTray = false,
            CloseToTray = false
        };
        var window = new MainWindow(settings, new InMemorySettingsStore(settings))
        {
            Width = 1080,
            Height = 680
        };
        window.Show();
        WaitForDispatcher(TimeSpan.FromMilliseconds(900));
        var shell = (ShellViewModel)window.DataContext;
        shell.Screen.SelectCategory("all");
        var scrollViewer = (ScrollViewer)window.FindName("ThemeScrollViewer");
        window.UpdateLayout();
        scrollViewer.ScrollToVerticalOffset(160);
        WaitForDispatcher(TimeSpan.FromMilliseconds(100));
        Assert(scrollViewer.VerticalOffset > 0,
            "the all-schemes gallery must be scrollable for navigation reset verification");
        ((RadioButton)window.FindName("SettingsNav")).IsChecked = true;
        WaitForDispatcher(TimeSpan.FromMilliseconds(100));
        Assert(scrollViewer.VerticalOffset == 0,
            "switching pages must return the shared content scroll position to the top");
        window.Close();
        Console.WriteLine("PASS page navigation returns shared content to the top");
    }

    private static void VerifyFeatureNoticeGeometry()
    {
        var codexWindow = FeatureNoticeWindow.CreateCodexNotice();
        var card = (Border)codexWindow.FindName("NoticeCard");
        var content = (Grid)card.Child;
        var close = (Button)codexWindow.FindName("CloseButton");
        var acknowledge = (Button)codexWindow.FindName("AcknowledgeButton");
        var title = (TextBlock)codexWindow.FindName("TitleText");
        var details = (ItemsControl)codexWindow.FindName("DetailsList");

        Assert(codexWindow.SizeToContent == SizeToContent.Height,
            "feature notice must size itself to content");
        Assert(card.Margin.Left == card.Margin.Top && card.Margin.Top == card.Margin.Right && card.Margin.Right == card.Margin.Bottom,
            $"feature notice outer margins must be equal: {card.Margin}");
        Assert(content.Margin.Left == content.Margin.Top && content.Margin.Top == content.Margin.Right && content.Margin.Right == content.Margin.Bottom,
            $"feature notice inner margins must be equal: {content.Margin}");
        Assert(close.Width == close.Height && close.MinWidth == close.MinHeight,
            $"feature notice close button must be square: {close.Width}x{close.Height}");
        Assert(acknowledge.Height == 48 && title.Text.Contains("Codex") && details.Items.Count == 3,
            "Codex notice must keep its acknowledgement action and three concise points");
        codexWindow.Close();

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
        var settings = new AppSettings
        {
            HasCompletedOnboarding = true,
            MinimizeToTray = false,
            CloseToTray = false
        };
        var window = new MainWindow(settings, new InMemorySettingsStore(settings));
        var host = (Border)window.FindName("SettingsIpAddressHost");
        var safeAreaHost = (Border)window.FindName("SafeAreaInputHost");
        var firstRunWindow = new FirstRunGuideWindow("192.168.1.100");
        var firstRunHost = (Border)firstRunWindow.FindName("IpAddressHost");
        var firstPlaceholder = (TextBlock)window.FindName("EndpointIpPart1Placeholder");
        var firstRunPlaceholder = (TextBlock)firstRunWindow.FindName("IpPart1Placeholder");
        var populate = typeof(MainWindow).GetMethod("PopulateEndpointParts", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert(populate is not null, "settings IP editor must expose its population path");
        window.Show();
        firstRunWindow.Show();
        WaitForDispatcher(TimeSpan.FromMilliseconds(900));
        Assert(firstPlaceholder.Visibility == Visibility.Visible && firstRunPlaceholder.Visibility == Visibility.Collapsed,
            "empty settings fields must show an IP format example while populated first-run fields must hide it");
        populate!.Invoke(window, ["192.168.1.100"]);
        window.UpdateLayout();

        var summary = (TextBlock)window.FindName("EndpointSummaryText");
        Assert(host.Height == firstRunHost.Height && host.CornerRadius == firstRunHost.CornerRadius,
            $"settings and first-run IP editors must share geometry: settings {host.Height}px/{host.CornerRadius}, guide {firstRunHost.Height}px/{firstRunHost.CornerRadius}");
        Assert(firstPlaceholder.Visibility == Visibility.Collapsed,
            "the IP format example must disappear after a valid address is populated");
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

    private sealed class InMemorySettingsStore(AppSettings settings) : ISettingsStore
    {
        private AppSettings _settings = settings;

        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_settings);

        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
        {
            _settings = settings;
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingSettingsStore(AppSettings settings) : ISettingsStore
    {
        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(settings);

        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default) =>
            Task.FromException(new UnauthorizedAccessException("Test settings path is unavailable."));
    }
}
