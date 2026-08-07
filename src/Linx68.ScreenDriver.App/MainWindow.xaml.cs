using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Resources;
using System.Windows.Shapes;
using System.Windows.Threading;
using Linx68.ScreenDriver.App.ViewModels;
using Linx68.ScreenDriver.Application;
using Linx68.ScreenDriver.Core;
using Linx68.ScreenDriver.Infrastructure;
using Microsoft.Win32;

namespace Linx68.ScreenDriver.App;

public partial class MainWindow : Window
{
	private readonly ISystemSnapshotSource _systemSource;

	private readonly IMusicSnapshotSource _musicSource;

	private readonly ILyricsSnapshotSource _lyricsSource;

	private readonly IWeatherSnapshotSource _weatherSource;

	private readonly WindowsWeatherLocationProvider _weatherLocationProvider;

	private readonly IStockSnapshotSource _stockSource;

	private readonly IDashboardSnapshotBuilder _snapshotBuilder;

	private readonly IDeviceTransport _transport;

	private readonly ISettingsStore _settingsStore;

	private readonly ImageTheme _imageTheme;

	private readonly FontFolderCatalog _fontCatalog;

	private readonly bool _ownsServices;

	private readonly DispatcherTimer _timer;

	private readonly DispatcherTimer _autoCommitTimer;

	private readonly NotifyIcon _trayIcon;

	private IReadOnlyList<ThemeDefinition> _themeDefinitions = Array.Empty<ThemeDefinition>();

	private AppSettings _settings = new AppSettings();

	private readonly AppSettings? _initialSettings;

	private ScreenRenderer _renderer = new ScreenRenderer();

	private RenderedFrame? _latestFrame;

	private MiMoTokenPlanWindow? _miMoWindow;

	private AiQuotaSnapshot? _latestAiQuota;

	private SystemSnapshot? _latestSnapshot;

	private bool _busy;

	private bool _loaded;

	private bool _suppressThemeRefresh;

	private bool _updatingFontList;

	private bool _autoCommitRunning;

	private bool _autoCommitPending;

	private bool _explicitExit;

	private bool _startMinimizeApplied;

	private bool _windowTransitionRunning;

	private bool _revealAfterMinimize;

	private bool _mediaAutomationThemeChanged;

	private bool _automaticLocationFallback;

	private string? _lastEffectiveThemeId;

	private WindowState _restoreWindowState;

	private System.Windows.Controls.TextBox[] _endpointParts = [];

	private bool _suppressEndpointPartSync;

	private string _themeCategory = "all";

	private bool _themeGalleryPreviewDirty;

	public MainWindow(AppSettings? initialSettings = null)
		: this(
			initialSettings,
			new WindowsSystemSnapshotSource(),
			new WindowsMusicSnapshotSource(),
			new LrcLibLyricsSnapshotSource(),
			new OpenMeteoWeatherSnapshotSource(),
			new WindowsWeatherLocationProvider(),
			new YahooStockSnapshotSource(),
			null,
			new HttpImageDeviceTransport(),
			new JsonSettingsStore(),
			new ImageTheme(),
			new FontFolderCatalog(System.IO.Path.Combine(AppContext.BaseDirectory, "Fonts")),
			new ShellViewModel(),
			ownsServices: true)
	{
	}

	public MainWindow(
		AppSettings initialSettings,
		ISystemSnapshotSource systemSource,
		IMusicSnapshotSource musicSource,
		ILyricsSnapshotSource lyricsSource,
		IWeatherSnapshotSource weatherSource,
		WindowsWeatherLocationProvider weatherLocationProvider,
		IStockSnapshotSource stockSource,
		IDashboardSnapshotBuilder snapshotBuilder,
		IDeviceTransport transport,
		ISettingsStore settingsStore,
		ImageTheme imageTheme,
		FontFolderCatalog fontCatalog,
		ShellViewModel shellViewModel)
		: this(
			initialSettings,
			systemSource,
			musicSource,
			lyricsSource,
			weatherSource,
			weatherLocationProvider,
			stockSource,
			snapshotBuilder,
			transport,
			settingsStore,
			imageTheme,
			fontCatalog,
			shellViewModel,
			ownsServices: false)
	{
	}

	private MainWindow(
		AppSettings? initialSettings,
		ISystemSnapshotSource systemSource,
		IMusicSnapshotSource musicSource,
		ILyricsSnapshotSource lyricsSource,
		IWeatherSnapshotSource weatherSource,
		WindowsWeatherLocationProvider weatherLocationProvider,
		IStockSnapshotSource stockSource,
		IDashboardSnapshotBuilder? snapshotBuilder,
		IDeviceTransport transport,
		ISettingsStore settingsStore,
		ImageTheme imageTheme,
		FontFolderCatalog fontCatalog,
		ShellViewModel shellViewModel,
		bool ownsServices)
	{
		_initialSettings = initialSettings;
		_systemSource = systemSource;
		_musicSource = musicSource;
		_lyricsSource = lyricsSource;
		_weatherSource = weatherSource;
		_weatherLocationProvider = weatherLocationProvider;
		_stockSource = stockSource;
		_snapshotBuilder = snapshotBuilder ?? new DashboardSnapshotBuilder(
			systemSource,
			lyricsSource,
			weatherSource,
			stockSource);
		_transport = transport;
		_settingsStore = settingsStore;
		_imageTheme = imageTheme;
		_fontCatalog = fontCatalog;
		_ownsServices = ownsServices;
		if (initialSettings is not null)
		{
			_settings = initialSettings;
		}
		InitializeComponent();
		DataContext = shellViewModel;
		_endpointParts = [EndpointIpPart1, EndpointIpPart2, EndpointIpPart3, EndpointIpPart4];
		_themeDefinitions = BuiltInThemes.CreateDefinitions(_imageTheme, () => _settings.Music?.LyricOffsetSeconds ?? 0);
		BuildThemeList();
		PopulateMediaAutomationThemeSelectors();
		_trayIcon = CreateTrayIcon();
		SystemEvents.UserPreferenceChanged += SystemEvents_OnUserPreferenceChanged;
		_timer = new DispatcherTimer
		{
			Interval = TimeSpan.FromSeconds(1.0)
		};
		_timer.Tick += Timer_OnTick;
		_autoCommitTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromMilliseconds(450.0)
		};
		_autoCommitTimer.Tick += AutoCommitTimer_OnTick;
		_fontCatalog.FontsChanged += FontCatalog_OnFontsChanged;
		base.SourceInitialized += delegate
		{
			ApplyWindowBackdrop();
		};
		base.Loaded += MainWindow_OnLoaded;
		base.StateChanged += MainWindow_OnStateChanged;
		base.Closing += MainWindow_OnClosing;
		System.Windows.Application.Current.SessionEnding += Application_OnSessionEnding;
		base.Closed += delegate
		{
			_timer.Stop();
			_autoCommitTimer.Stop();
			if (_ownsServices)
			{
				(_transport as IDisposable)?.Dispose();
				(_weatherSource as IDisposable)?.Dispose();
				_weatherLocationProvider.Dispose();
				(_stockSource as IDisposable)?.Dispose();
				(_lyricsSource as IDisposable)?.Dispose();
				(_musicSource as IDisposable)?.Dispose();
				(_systemSource as IDisposable)?.Dispose();
				_fontCatalog.Dispose();
			}
			_miMoWindow?.Dispose();
			SystemEvents.UserPreferenceChanged -= SystemEvents_OnUserPreferenceChanged;
			_trayIcon.Visible = false;
			_trayIcon.ContextMenuStrip?.Dispose();
			Icon? currentTrayIcon = _trayIcon.Icon;
			_trayIcon.Dispose();
			currentTrayIcon?.Dispose();
			System.Windows.Application.Current.SessionEnding -= Application_OnSessionEnding;
		};
	}

	private void BuildThemeList()
	{
		ThemeListPanel.Children.Clear();
		Style itemStyle = (Style)FindResource("ThemeGalleryCard");
		bool previousSuppression = _suppressThemeRefresh;
		_suppressThemeRefresh = true;
		try
		{
			foreach (ThemeDefinition definition in _themeDefinitions.Where(definition =>
				_themeCategory == "all" || definition.CategoryId == _themeCategory))
			{
				IScreenTheme theme = definition.Theme;
				System.Windows.Controls.Image preview = new System.Windows.Controls.Image
				{
					Stretch = Stretch.UniformToFill,
					SnapsToDevicePixels = false
				};
				try
				{
					RenderedFrame frame = _renderer.Render(theme, SystemSnapshot.DesignSample, 86, GetAccentColor(), GetSelectedFontFamily(), GetScreenDisplayOptions());
					preview.Source = LoadBitmap(frame.JpegBytes);
				}
				catch
				{
					preview.Source = null;
				}

				StackPanel content = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
				content.Children.Add(new TextBlock
				{
					Text = theme.DisplayName,
					FontSize = 14,
					FontWeight = FontWeights.SemiBold,
					TextTrimming = TextTrimming.CharacterEllipsis
				});
				TextBlock descriptionText = new TextBlock
				{
					Text = theme.Description,
					FontSize = 11,
					TextWrapping = TextWrapping.Wrap,
					MaxHeight = 34,
					Margin = new Thickness(0, 7, 0, 0)
				};
				descriptionText.SetResourceReference(TextBlock.ForegroundProperty, "SecondaryText");
				content.Children.Add(descriptionText);
				TextBlock metadataText = new TextBlock
				{
					Text = $"{definition.CategoryDisplayName}  ·  {(definition.IsStatic ? "静态" : "动态")}",
					FontSize = 10,
					Margin = new Thickness(0, 12, 0, 0)
				};
				metadataText.SetResourceReference(TextBlock.ForegroundProperty, "SecondaryText");
				content.Children.Add(metadataText);

				System.Windows.Controls.RadioButton radioButton = new System.Windows.Controls.RadioButton
				{
					Content = content,
					DataContext = preview,
					Tag = theme.Id,
					GroupName = "Theme",
					Style = itemStyle,
					IsChecked = string.Equals(theme.Id, _settings.SelectedThemeId, StringComparison.OrdinalIgnoreCase)
				};
				radioButton.Checked += ThemeItem_OnChecked;
				ThemeListPanel.Children.Add(radioButton);
			}
		}
		finally
		{
			_suppressThemeRefresh = previousSuppression;
		}
		ResizeThemeCards(ThemeListPanel.ActualWidth);
	}

	private void ResizeThemeCards(double availableWidth)
	{
		if (availableWidth <= 0)
		{
			return;
		}
		double cardWidth = availableWidth >= 400 ? Math.Max(196, (availableWidth - 44) / 2) : Math.Max(196, availableWidth - 12);
		foreach (System.Windows.Controls.RadioButton card in ThemeListPanel.Children.OfType<System.Windows.Controls.RadioButton>())
		{
			card.Width = cardWidth;
		}
	}

	private void PopulateMediaAutomationThemeSelectors()
	{
		MediaIdleThemeComboBox.Items.Clear();
		MediaPlayingThemeComboBox.Items.Clear();
		foreach (ThemeDefinition definition in _themeDefinitions)
		{
			IScreenTheme theme = definition.Theme;
			ComboBoxItem item = new ComboBoxItem
			{
				Content = theme.DisplayName,
				Tag = theme.Id
			};
			if (definition.Category == ThemeCategory.Music)
			{
				MediaPlayingThemeComboBox.Items.Add(item);
			}
			else
			{
				MediaIdleThemeComboBox.Items.Add(item);
			}
		}
	}
	private NotifyIcon CreateTrayIcon()
	{
		ContextMenuStrip contextMenuStrip = new ContextMenuStrip();
		contextMenuStrip.Items.Add("打开 灵犀68屏幕驱动", null, delegate
		{
			Dispatcher.BeginInvoke(RestoreFromTray);
		});
		contextMenuStrip.Items.Add("刷新并推送", null, delegate
		{
			Dispatcher.BeginInvoke(async () =>
			{
				await CommitAndPushAsync();
			});
		});
		contextMenuStrip.Items.Add(new ToolStripSeparator());
		contextMenuStrip.Items.Add("退出", null, delegate
		{
			Dispatcher.BeginInvoke(ExitApplication);
		});
		NotifyIcon notifyIcon = new NotifyIcon();
		notifyIcon.Text = "灵犀68屏幕驱动";
		notifyIcon.Icon = LoadTrayIcon(IsWindowsSystemDarkMode());
		notifyIcon.ContextMenuStrip = contextMenuStrip;
		notifyIcon.Visible = false;
		notifyIcon.DoubleClick += delegate
		{
			Dispatcher.BeginInvoke(RestoreFromTray);
		};
		return notifyIcon;
	}

	private void SystemEvents_OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
	{
		if (e.Category != UserPreferenceCategory.Color &&
			e.Category != UserPreferenceCategory.General &&
			e.Category != UserPreferenceCategory.VisualStyle)
		{
			return;
		}

		Dispatcher.BeginInvoke((Action)(() =>
		{
			UpdateTrayIconForSystemTheme();
			if (_settings.AppearanceMode == AppearanceMode.System)
			{
				ApplyAppearance();
			}
		}));
	}

	private void UpdateTrayIconForSystemTheme()
	{
		Icon nextIcon = LoadTrayIcon(IsWindowsSystemDarkMode());
		Icon? previousIcon = _trayIcon.Icon;
		_trayIcon.Icon = nextIcon;
		previousIcon?.Dispose();
	}

	private static bool IsWindowsSystemDarkMode() => AppearanceManager.IsSystemDarkMode();

	private static Icon LoadTrayIcon(bool useWhiteIcon)
	{
		string iconName = useWhiteIcon ? "TrayIcon.White.ico" : "TrayIcon.ico";
		StreamResourceInfo resourceStream = System.Windows.Application.GetResourceStream(
			new Uri($"pack://application:,,,/Linx68.ScreenDriver.App;component/Assets/{iconName}", UriKind.Absolute));
		if (resourceStream == null)
		{
			return (Icon)SystemIcons.Application.Clone();
		}
		using (resourceStream.Stream)
		{
			using Icon icon = new Icon(resourceStream.Stream);
			return (Icon)icon.Clone();
		}
	}

	private void ApplyWindowBackdrop()
	{
		if (WindowBackdrop.TryApplyMica(this, AppearanceManager.IsDark))
		{
			WindowRoot.Background = System.Windows.Media.Brushes.Transparent;
			WindowRoot.BorderBrush = System.Windows.Media.Brushes.Transparent;
		}
		else
		{
			WindowRoot.Background = (System.Windows.Media.Brush)FindResource("AppBackground");
			WindowRoot.BorderBrush = (System.Windows.Media.Brush)FindResource("Stroke.Default");
		}
	}

	private void ApplyAppearance()
	{
		AppearanceManager.Apply(_settings.AppearanceMode);
		ApplyWindowBackdrop();
		DevicePreview?.InvalidateVisual();
		if (_themeDefinitions.Count > 0 && ThemeListPanel is not null)
		{
			BuildThemeList();
		}
	}

	private async void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
	{
		_settings = _initialSettings ?? await _settingsStore.LoadAsync();
		ApplyAppearance();
		ReloadFontOptions(_settings.SelectedFontId);
		ApplySettingsToControls();
		if (!_settings.HasCompletedOnboarding)
		{
			var guide = new FirstRunGuideWindow(ExtractDeviceIp(_settings.DeviceEndpoint))
			{
				Owner = this
			};
			bool? accepted = guide.ShowDialog();
			_settings.HasCompletedOnboarding = true;
			if (accepted == true && !string.IsNullOrWhiteSpace(guide.DeviceIp))
			{
				PopulateEndpointParts(guide.DeviceIp);
				_settings.DeviceEndpoint = $"http://{guide.DeviceIp}/image/upload";
				UpdateEndpointSummary();
			}
			await _settingsStore.SaveAsync(_settings);
		}
		_loaded = true;
		_timer.Start();
		await RefreshPreviewAsync();
		UpdateTrayVisibility();
		if ((_settings.StartMinimized || Environment.GetCommandLineArgs().Any(argument => string.Equals(argument, "--startup", StringComparison.OrdinalIgnoreCase))) && !_startMinimizeApplied)
		{
			_startMinimizeApplied = true;
			HideToTray();
		}
		else
		{
			InteractionMotion.Reveal(WindowRoot, 10.0, 0.994);
		}
	}

	private void ApplySettingsToControls()
	{
		PopulateEndpointParts(ExtractDeviceIp(_settings.DeviceEndpoint));
		AutoPushCheckBox.IsChecked = _settings.AutoPush;
		AutoMusicCheckBox.IsChecked = _settings.AutoSwitchToMusic;
		MediaThemeAutomationCheckBox.IsChecked = _settings.AutoMediaThemeSwitch;
		SelectComboByTag(MediaIdleThemeComboBox, _settings.MediaIdleThemeId ?? "system");
		SelectComboByTag(MediaPlayingThemeComboBox, _settings.MediaPlayingThemeId ?? "music");
		MinimizeToTrayCheckBox.IsChecked = _settings.MinimizeToTray;
		CloseToTrayCheckBox.IsChecked = _settings.CloseToTray;
		StartMinimizedCheckBox.IsChecked = _settings.StartMinimized;
		LaunchAtStartupCheckBox.IsChecked = _settings.LaunchAtStartup;
		SafeLeftTextBox.Text = _settings.SafeArea.Left.ToString();
		SafeTopTextBox.Text = _settings.SafeArea.Top.ToString();
		SafeRightTextBox.Text = _settings.SafeArea.Right.ToString();
		SafeBottomTextBox.Text = _settings.SafeArea.Bottom.ToString();
		AccentColorTextBox.Text = _settings.AccentColor;
		SelectAppearanceMode(_settings.AppearanceMode);
		SelectComboByTag(ImageTimePlacementComboBox, _settings.ImageTimePlacement.ToString());
		RefreshIntervalSlider.Value = Math.Clamp(_settings.RefreshSeconds, 1, 30);
		AppSettings settings = _settings;
		if (settings.AiQuota == null)
		{
			AiQuotaSettings aiQuotaSettings = (settings.AiQuota = new AiQuotaSettings());
		}
		AiDisplayNameTextBox.Text = (string.IsNullOrWhiteSpace(_settings.AiQuota.DisplayName) ? "MiMo" : _settings.AiQuota.DisplayName);
		_settings.Weather ??= new WeatherSettings();
		WeatherAutomaticLocationCheckBox.IsChecked = _settings.Weather.UseAutomaticLocation;
		WeatherLocationTextBox.Text = string.IsNullOrWhiteSpace(_settings.Weather.LocationQuery) ? "北京" : _settings.Weather.LocationQuery;
		WeatherLocationTextBox.IsEnabled = !_settings.Weather.UseAutomaticLocation;
		_settings.Stocks ??= new StockSettings();
		_settings.Music ??= new MusicSettings();
		OnlineLyricsCheckBox.IsChecked = _settings.Music.EnableOnlineLyrics;
		LyricOffsetSlider.Value = Math.Clamp(_settings.Music.LyricOffsetSeconds, -3, 3);
		var stockItems = NormalizeStockItems(_settings.Stocks);
		StockSymbol1TextBox.Text = stockItems[0].Symbol;
		StockAlias1TextBox.Text = stockItems[0].Alias;
		StockSymbol2TextBox.Text = stockItems[1].Symbol;
		StockAlias2TextBox.Text = stockItems[1].Alias;
		StockSymbol3TextBox.Text = stockItems[2].Symbol;
		StockAlias3TextBox.Text = stockItems[2].Alias;
		SelectComboByTag(StockColorPreferenceComboBox, _settings.Stocks.RedForGain ? "RedGain" : "GreenGain");
		_imageTheme.ImagePath = _settings.ImagePath;
		SelectTheme(_settings.SelectedThemeId);
		UpdateEndpointSummary();
		UpdateRenderer();
		BuildThemeList();
	}

	private void ApplyControlsToSettings()
	{
		_settings.DeviceEndpoint = (TryCreateDeviceEndpoint(EndpointTextBox.Text, out Uri endpoint) ? endpoint.AbsoluteUri : EndpointTextBox.Text.Trim());
		_settings.AutoPush = AutoPushCheckBox.IsChecked == true;
		_settings.AutoSwitchToMusic = AutoMusicCheckBox.IsChecked == true;
		_settings.AutoMediaThemeSwitch = MediaThemeAutomationCheckBox.IsChecked == true;
		_settings.MediaIdleThemeId = ReadComboTag(MediaIdleThemeComboBox, "system");
		_settings.MediaPlayingThemeId = ReadComboTag(MediaPlayingThemeComboBox, "music");
		_settings.MinimizeToTray = MinimizeToTrayCheckBox.IsChecked == true;
		_settings.CloseToTray = CloseToTrayCheckBox.IsChecked == true;
		_settings.StartMinimized = StartMinimizedCheckBox.IsChecked == true;
		_settings.LaunchAtStartup = LaunchAtStartupCheckBox.IsChecked == true;
		StartupRegistration.TrySetEnabled(_settings.LaunchAtStartup);
		_settings.RefreshSeconds = (int)Math.Round(RefreshIntervalSlider.Value);
		_settings.ImageTimePlacement = ReadComboEnum(ImageTimePlacementComboBox, ImageTimePlacement.Bottom);
		_settings.AccentColor = (TryParseAccentColor(AccentColorTextBox.Text, out var _) ? AccentColorTextBox.Text.Trim().ToUpperInvariant() : "#E4694C");
		_settings.AppearanceMode = ReadAppearanceMode();
		_settings.SelectedFontId = (FontFamilyComboBox.SelectedItem as ScreenFontOption)?.Id ?? "builtin:segoe-variable-display";
		_settings.SafeArea = ReadSafeArea();
		_settings.ImagePath = _imageTheme.ImagePath;
		_settings.SelectedThemeId = GetSelectedTheme()?.Id ?? "system";
		_settings.AiQuota = new AiQuotaSettings
		{
			DisplayName = ReadAiDisplayName()
		};
		_settings.Weather = new WeatherSettings
		{
			LocationQuery = ReadWeatherLocation(),
			UseAutomaticLocation = WeatherAutomaticLocationCheckBox.IsChecked == true
		};
		_settings.Stocks = new StockSettings
		{
			RedForGain = ReadComboTag(StockColorPreferenceComboBox, "RedGain") == "RedGain",
			Items =
			[
				new StockItemSettings { Symbol = StockSymbol1TextBox.Text.Trim(), Alias = StockAlias1TextBox.Text.Trim() },
				new StockItemSettings { Symbol = StockSymbol2TextBox.Text.Trim(), Alias = StockAlias2TextBox.Text.Trim() },
				new StockItemSettings { Symbol = StockSymbol3TextBox.Text.Trim(), Alias = StockAlias3TextBox.Text.Trim() }
			]
		};
		_settings.Music = new MusicSettings
		{
			EnableOnlineLyrics = OnlineLyricsCheckBox.IsChecked == true,
			LyricOffsetSeconds = Math.Round(LyricOffsetSlider.Value * 2) / 2
		};
		_timer.Interval = TimeSpan.FromSeconds(_settings.RefreshSeconds);
		UpdateEndpointSummary();
		UpdateRenderer();
		UpdateTrayVisibility();
	}

	private static IReadOnlyList<StockItemSettings> NormalizeStockItems(StockSettings settings)
	{
		var items = (settings.Items ?? []).Take(3).ToList();
		while (items.Count < 3) items.Add(new StockItemSettings());
		return items;
	}
	private void UpdateRenderer()
	{
		ScreenProfile profile = new ScreenProfile(142, 428, 524288, _settings.SafeArea);
		_renderer = new ScreenRenderer(profile);
	}

	private ScreenInsets ReadSafeArea()
	{
		int num = ReadClamped(SafeLeftTextBox.Text, 10, 0, 60);
		int num2 = ReadClamped(SafeTopTextBox.Text, 52, 0, 160);
		int num3 = ReadClamped(SafeRightTextBox.Text, 10, 0, 60);
		int num4 = ReadClamped(SafeBottomTextBox.Text, 12, 0, 100);
		if (num + num3 >= 132 || num2 + num4 >= 418)
		{
			return new ScreenInsets(10, 52, 10, 12);
		}
		return new ScreenInsets(num, num2, num3, num4);
	}

	private static int ReadClamped(string value, int fallback, int minimum, int maximum)
	{
		if (!int.TryParse(value, out var result))
		{
			return fallback;
		}
		return Math.Clamp(result, minimum, maximum);
	}

	private async void Timer_OnTick(object? sender, EventArgs e)
	{
		bool selectedThemeIsStatic = GetSelectedThemeDefinition()?.IsStatic == true;
		if (!_busy && (!selectedThemeIsStatic || _settings.AutoMediaThemeSwitch))
		{
			await RefreshPreviewAsync();
			bool shouldPush = _settings.AutoPush || _mediaAutomationThemeChanged;
			_mediaAutomationThemeChanged = false;
			if (shouldPush)
			{
				await PushLatestAsync();
			}
		}
	}

	private void ScheduleAutoCommit()
	{
		if (_loaded)
		{
			if (_autoCommitRunning)
			{
				_autoCommitPending = true;
				return;
			}
			_autoCommitTimer.Stop();
			_autoCommitTimer.Start();
		}
	}

	private async void AutoCommitTimer_OnTick(object? sender, EventArgs e)
	{
		_autoCommitTimer.Stop();
		await CommitAndPushAsync();
	}

	private async Task CommitAndPushAsync()
	{
		if (!_loaded)
		{
			return;
		}
		if (_busy)
		{
			ScheduleAutoCommit();
			return;
		}
		if (_autoCommitRunning)
		{
			_autoCommitPending = true;
			return;
		}
		_autoCommitRunning = true;
		try
		{
			ApplyControlsToSettings();
			await _settingsStore.SaveAsync(_settings);
			await RefreshPreviewAsync();
			if (_themeGalleryPreviewDirty)
			{
				_themeGalleryPreviewDirty = false;
				BuildThemeList();
			}
			_mediaAutomationThemeChanged = false;
			await PushLatestAsync();
		}
		finally
		{
			_autoCommitRunning = false;
			if (_autoCommitPending)
			{
				_autoCommitPending = false;
				ScheduleAutoCommit();
			}
		}
	}

	private async Task RefreshPreviewAsync()
	{
		if (_busy || !_loaded)
		{
			return;
		}
		_busy = true;
		try
		{
			MusicSnapshot currentMusic = await _musicSource.ReadAsync();
			ThemeDefinition selectedDefinition = GetSelectedThemeDefinition() ?? _themeDefinitions[0];
			IScreenTheme screenTheme = selectedDefinition.Theme;
			bool mediaIsPlaying = currentMusic.Available && currentMusic.IsPlaying;
			string effectiveThemeId = MediaThemeAutomation.ResolveThemeId(
				_settings,
				mediaIsPlaying,
				screenTheme.Id,
				id => GetThemeDefinition(id)?.Category == ThemeCategory.Music);
			ThemeDefinition effectiveDefinition = GetThemeDefinition(effectiveThemeId)
				?? GetThemeDefinition(mediaIsPlaying ? "music" : "system")
				?? _themeDefinitions[0];
			IScreenTheme theme2 = effectiveDefinition.Theme;
			_mediaAutomationThemeChanged = _settings.AutoMediaThemeSwitch &&
				_lastEffectiveThemeId != null &&
				!string.Equals(_lastEffectiveThemeId, theme2.Id, StringComparison.OrdinalIgnoreCase);
			_lastEffectiveThemeId = theme2.Id;
			bool needsLyrics = effectiveDefinition.Requires(ThemeDataRequirements.Lyrics);
			AiQuotaSnapshot? aiQuota = effectiveDefinition.Requires(ThemeDataRequirements.AiQuota)
				? await ReadAiQuotaAsync()
				: null;
			bool needsWeather = effectiveDefinition.Requires(ThemeDataRequirements.Weather);
			WeatherSettings? effectiveWeatherSettings = needsWeather
				? await ResolveEffectiveWeatherSettingsAsync()
				: null;
			bool needsStocks = effectiveDefinition.Requires(ThemeDataRequirements.Stocks);
			_latestSnapshot = await _snapshotBuilder.BuildAsync(
				effectiveDefinition,
				_settings,
				currentMusic,
				effectiveWeatherSettings,
				aiQuota);
			SystemSnapshot system = _latestSnapshot;
			MusicSnapshot musicSnapshot = system.Music ?? currentMusic;
			WeatherSnapshot? weather = system.Weather;
			StockSnapshot? stocks = system.Stocks;
			_latestFrame = _renderer.Render(theme2, _latestSnapshot, 100, GetAccentColor(), GetSelectedFontFamily(), GetScreenDisplayOptions());
			BitmapImage frameSource = LoadBitmap(_latestFrame.JpegBytes);
			DevicePreview.FrameSource = frameSource;
			CpuValueText.Text = $"CPU  {system.CpuPercent:0}%";
			MemoryValueText.Text = $"内存  {system.MemoryPercent:0}%";
			DownloadValueText.Text = $"下载  {system.DownloadMbps:0.0}M";
			UploadValueText.Text = $"上传  {system.UploadMbps:0.0}M";
			string lyricStatus = _settings.Music?.EnableOnlineLyrics == true && needsLyrics
				? (musicSnapshot.Lyrics.Available ? " · 歌词已匹配" : " · 暂无同步歌词")
				: string.Empty;
			string musicSource = ResolveMusicSourceName(musicSnapshot.SourceAppId);
			MusicSourceText.Text = (musicSnapshot.Available ? $"{musicSource} · {(musicSnapshot.IsPlaying ? "正在播放" : "已暂停")} · {musicSnapshot.Title}  —  {musicSnapshot.Artist}{lyricStatus}" : "当前没有可用的 Windows 媒体会话");
			if (weather is { Available: true })
			{
				WeatherSourceStatusText.Text = $"{(_automaticLocationFallback ? "自动定位不可用，已使用 " : string.Empty)}{weather.LocationName} · {weather.TemperatureC:0}° · {weather.ConditionText}{(weather.IsStale ? " · 上次数据" : string.Empty)}";
			}
			else if (needsWeather)
			{
				WeatherSourceStatusText.Text = weather?.ErrorMessage ?? "暂时无法获取天气数据";
			}
			if (stocks is { Quotes.Count: > 0 })
			{
				StockSourceStatusText.Text = $"{stocks.Quotes.Count} 项 · {(stocks.IsStale ? "上次数据" : $"更新 {stocks.UpdatedAt:HH:mm}")}";
			}
			else if (needsStocks)
			{
				StockSourceStatusText.Text = stocks?.ErrorMessage ?? "请添加至少一个行情代码";
			}
		}
		catch (Exception)
		{
		}
		finally
		{
			_busy = false;
		}
	}

	private async Task<AiQuotaSnapshot?> ReadAiQuotaAsync()
	{
		if (_miMoWindow == null)
		{
			AiSourceStatusText.Text = "请先登录小米控制台";
			return _latestAiQuota is null ? AiQuotaSnapshot.Empty : ApplyAiDisplayName(_latestAiQuota);
		}
		try
		{
			return UpdateMiMoUsage(await _miMoWindow.ReadAsync());
		}
		catch (Exception ex)
		{
			AiSourceStatusText.Text = ex.Message;
			AiSourceStatusText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 74, 84));
			return _latestAiQuota is null ? AiQuotaSnapshot.Empty : ApplyAiDisplayName(_latestAiQuota);
		}
	}

	private MiMoTokenPlanWindow GetOrCreateMiMoWindow()
	{
		if (_miMoWindow != null)
		{
			return _miMoWindow;
		}
		_miMoWindow = new MiMoTokenPlanWindow
		{
			Owner = this
		};
		_miMoWindow.SnapshotUpdated += delegate(object? _, AiQuotaSnapshot snapshot)
		{
			Dispatcher.BeginInvoke((Action)delegate
			{
				UpdateMiMoUsage(snapshot);
				ScheduleAutoCommit();
			});
		};
		return _miMoWindow;
	}

	private AiQuotaSnapshot UpdateMiMoUsage(AiQuotaSnapshot snapshot)
	{
		snapshot = ApplyAiDisplayName(snapshot);
		_latestAiQuota = snapshot;
		MiMoRemainingValueText.Text = $"{snapshot.ClampedRemainingPercent:0}%";
		MiMoRemainingProgress.Value = snapshot.ClampedRemainingPercent;
		AiQuotaBalance? balance = snapshot.Balance;
		if (balance is not null)
		{
			MiMoCreditsValueText.Text = FormatCompactNumber(balance.Used) + " / " + FormatCompactNumber(balance.Limit);
		}
		MiMoExpiryValueText.Text = snapshot.ResetsAt?.ToLocalTime().ToString("yyyy年M月d日") ?? "—";
		AiSourceStatusText.Text = "已连接 · " + snapshot.PlatformName;
		AiSourceStatusText.Foreground = (System.Windows.Media.Brush)FindResource("SecondaryText");
		return snapshot;
	}

	private AiQuotaSnapshot ApplyAiDisplayName(AiQuotaSnapshot snapshot)
	{
		return snapshot with
		{
			PlatformName = ReadAiDisplayName()
		};
	}

	private string ReadAiDisplayName()
	{
		if (!string.IsNullOrWhiteSpace(AiDisplayNameTextBox.Text))
		{
			return AiDisplayNameTextBox.Text.Trim();
		}
		return "MiMo";
	}

	private string ReadWeatherLocation()
	{
		return string.IsNullOrWhiteSpace(WeatherLocationTextBox.Text)
			? "北京"
			: WeatherLocationTextBox.Text.Trim();
	}

	private async Task<WeatherSettings> ResolveEffectiveWeatherSettingsAsync()
	{
		WeatherSettings saved = _settings.Weather ?? new WeatherSettings();
		_automaticLocationFallback = false;
		if (!saved.UseAutomaticLocation)
		{
			return saved;
		}

		AutomaticWeatherLocation? location = await _weatherLocationProvider.TryGetAsync();
		if (location is null)
		{
			_automaticLocationFallback = true;
			return new WeatherSettings
			{
				LocationQuery = string.IsNullOrWhiteSpace(saved.LocationQuery) ? "北京" : saved.LocationQuery
			};
		}

		return new WeatherSettings
		{
			LocationQuery = saved.LocationQuery,
			UseAutomaticLocation = true,
			Latitude = location.Latitude,
			Longitude = location.Longitude,
			AutomaticLocationName = location.DisplayName
		};
	}

	private static string FormatCompactNumber(decimal value)
	{
		if (value >= 1000000m)
		{
			if (value >= 1000000000m)
			{
				return $"{value / 1000000000m:0.##}B";
			}
			return $"{value / 1000000m:0.##}M";
		}
		if (value >= 1000m)
		{
			return $"{value / 1000m:0.##}K";
		}
		return $"{value:0}";
	}

	private async void MiMoLoginButton_OnClick(object sender, RoutedEventArgs e)
	{
		await ShowOneTimeFeatureNoticeAsync("ai-quota");
		MiMoTokenPlanWindow window = GetOrCreateMiMoWindow();
		window.Show();
		window.Activate();
		try
		{
			await window.InitializeAsync();
		}
		catch (Exception ex)
		{
			AiSourceStatusText.Text = "登录窗口启动失败：" + ex.Message;
		}
	}

	private async void MiMoRefreshButton_OnClick(object sender, RoutedEventArgs e)
	{
		if (_miMoWindow == null)
		{
			MiMoLoginButton_OnClick(sender, e);
			return;
		}
		AiSourceStatusText.Text = "正在读取 MiMo 用量…";
		try
		{
			UpdateMiMoUsage(await _miMoWindow.ReadAsync(force: true));
			await CommitAndPushAsync();
		}
		catch (Exception ex)
		{
			AiSourceStatusText.Text = ex.Message;
			AiSourceStatusText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 74, 84));
		}
	}

	private async Task PushLatestAsync()
	{
		if (_busy)
		{
			return;
		}
		if (_latestFrame is null)
		{
			await RefreshPreviewAsync();
		}
		if (_latestFrame is null || !TryGetEndpoint(out Uri endpoint))
		{
			SetDeviceStatus(success: false);
			return;
		}
		_busy = true;
		try
		{
			SetDeviceStatus((await _transport.PushAsync(endpoint, _latestFrame)).Success);
		}
		finally
		{
			_busy = false;
		}
	}

	private bool TryGetEndpoint(out Uri endpoint)
	{
		return TryCreateDeviceEndpoint(EndpointTextBox.Text, out endpoint);
	}

	private static bool TryCreateDeviceEndpoint(string? value, out Uri endpoint)
	{
		endpoint = null!;
		if (!IPAddress.TryParse(ExtractDeviceIp(value), out IPAddress? address) || address.AddressFamily != AddressFamily.InterNetwork)
		{
			return false;
		}
		endpoint = new Uri($"http://{address}/image/upload", UriKind.Absolute);
		return true;
	}

	private static string ExtractDeviceIp(string? value)
	{
		string text = value?.Trim() ?? string.Empty;
		if (Uri.TryCreate(text, UriKind.Absolute, out Uri? result)
			&& (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps))
		{
			return result.Host;
		}
		return text;
	}

	private IScreenTheme? GetSelectedTheme()
	{
		return GetSelectedThemeDefinition()?.Theme;
	}

	private ThemeDefinition? GetSelectedThemeDefinition()
	{
		return GetThemeDefinition(_settings.SelectedThemeId);
	}

	private ThemeDefinition? GetThemeDefinition(string? id)
	{
		return _themeDefinitions.FirstOrDefault(definition =>
			string.Equals(definition.Id, id, StringComparison.OrdinalIgnoreCase));
	}

	private void SelectTheme(string id)
	{
		_settings.SelectedThemeId = GetThemeDefinition(id)?.Id ?? _themeDefinitions[0].Id;
		System.Windows.Controls.RadioButton? card = ThemeListPanel.Children.OfType<System.Windows.Controls.RadioButton>()
			.FirstOrDefault(candidate => string.Equals(candidate.Tag?.ToString(), _settings.SelectedThemeId, StringComparison.OrdinalIgnoreCase));
		if (card is not null)
		{
			card.IsChecked = true;
		}
	}

	private void ThemeListPanel_OnSizeChanged(object sender, SizeChangedEventArgs e) => ResizeThemeCards(e.NewSize.Width);

	private void ThemeCategory_OnChecked(object sender, RoutedEventArgs e)
	{
		if (sender is not System.Windows.Controls.RadioButton { Tag: string category } || ThemeListPanel is null)
		{
			return;
		}
		_themeCategory = category;
		BuildThemeList();
	}

	private void LocateCurrent_OnClick(object sender, RoutedEventArgs e)
	{
		_themeCategory = "all";
		if (AllThemeCategoryRadio.IsChecked != true)
		{
			AllThemeCategoryRadio.IsChecked = true;
		}
		else
		{
			BuildThemeList();
		}

		Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
		{
			System.Windows.Controls.RadioButton? current = ThemeListPanel.Children
				.OfType<System.Windows.Controls.RadioButton>()
				.FirstOrDefault(card => string.Equals(card.Tag?.ToString(), _settings.SelectedThemeId, StringComparison.OrdinalIgnoreCase));
			current?.BringIntoView();
		});
	}

	private void AppearanceMode_OnChecked(object sender, RoutedEventArgs e)
	{
		if (!_loaded || sender is not System.Windows.Controls.RadioButton { Tag: string mode } || !Enum.TryParse(mode, true, out AppearanceMode appearanceMode))
		{
			return;
		}
		_settings.AppearanceMode = appearanceMode;
		ApplyAppearance();
		ScheduleAutoCommit();
	}

	private void SelectAppearanceMode(AppearanceMode mode)
	{
		System.Windows.Controls.RadioButton target = mode switch
		{
			AppearanceMode.Light => LightAppearanceRadio,
			AppearanceMode.Dark => DarkAppearanceRadio,
			_ => SystemAppearanceRadio
		};
		target.IsChecked = true;
	}

	private AppearanceMode ReadAppearanceMode()
	{
		if (DarkAppearanceRadio.IsChecked == true) return AppearanceMode.Dark;
		if (LightAppearanceRadio.IsChecked == true) return AppearanceMode.Light;
		return AppearanceMode.System;
	}

	private void UpdateEndpointSummary()
	{
		string ipString = ExtractDeviceIp(EndpointTextBox.Text);
		EndpointSummaryText.Text = ((IPAddress.TryParse(ipString, out IPAddress? address) && address.AddressFamily == AddressFamily.InterNetwork) ? address.ToString() : "地址未配置");
	}

	private async void ThemeItem_OnChecked(object sender, RoutedEventArgs e)
	{
		if (sender is System.Windows.Controls.RadioButton radioButton)
		{
			string id = radioButton.Tag?.ToString() ?? "system";
			ThemeDefinition definition = GetThemeDefinition(id) ?? _themeDefinitions[0];
			IScreenTheme screenTheme = definition.Theme;
			CurrentThemeNameText.Text = screenTheme.DisplayName;
			CurrentThemeDescription.Text = ((id == "image" && !string.IsNullOrWhiteSpace(_imageTheme.ImagePath)) ? _imageTheme.ImagePath : screenTheme.Description);
			CurrentThemeDetailsText.Text = screenTheme.Details;
			SelectImageButton.Visibility = definition.Shows(ThemeSettingsSections.Image) ? Visibility.Visible : Visibility.Collapsed;
			UpdateContextualDataCards(definition);
			UpdateAutomationVisibility(definition);
			if (_loaded)
			{
				InteractionMotion.Reveal(CurrentThemeNameText, 4.0, 0.996);
				InteractionMotion.Reveal(CurrentThemeDescription, 4.0, 0.996);
				InteractionMotion.Reveal(CurrentThemeDetailsText, 4.0, 0.996);
			}
			if (_loaded && !_suppressThemeRefresh)
			{
				await ShowOneTimeFeatureNoticeAsync(id);
				_settings.SelectedThemeId = id;
				await CommitAndPushAsync();
			}
		}
	}

	private async Task ShowOneTimeFeatureNoticeAsync(string themeId)
	{
		FeatureNoticeWindow? notice = null;
		if (themeId == "stocks" && !_settings.HasAcknowledgedStockNotice)
		{
			notice = FeatureNoticeWindow.CreateStockNotice();
		}
		else if (themeId == "ai-quota" && !_settings.HasAcknowledgedMiMoNotice)
		{
			notice = FeatureNoticeWindow.CreateMiMoNotice();
		}

		if (notice is null)
		{
			return;
		}

		notice.Owner = this;
		notice.ShowDialog();
		if (themeId == "stocks")
		{
			_settings.HasAcknowledgedStockNotice = true;
		}
		else
		{
			_settings.HasAcknowledgedMiMoNotice = true;
		}
		await _settingsStore.SaveAsync(_settings);
	}

	private void UpdateContextualDataCards(ThemeDefinition definition)
	{
		SetContextCardVisibility(SystemDataCard, definition.Shows(ThemeSettingsSections.System));
		SetContextCardVisibility(MusicDataCard, definition.Shows(ThemeSettingsSections.Music));
		SetContextCardVisibility(AiQuotaDataCard, definition.Shows(ThemeSettingsSections.AiQuota));
		SetContextCardVisibility(WeatherDataCard, definition.Shows(ThemeSettingsSections.Weather));
		SetContextCardVisibility(StockDataCard, definition.Shows(ThemeSettingsSections.Stocks));
	}

	private void UpdateAutomationVisibility(ThemeDefinition definition)
	{
		SetContextCardVisibility(AutoMusicCard, definition.Category == ThemeCategory.Music);
	}
	private static void SetContextCardVisibility(FrameworkElement card, bool visible)
	{
		if (!visible)
		{
			card.Visibility = Visibility.Collapsed;
			InteractionMotion.Reset(card);
			return;
		}
		card.Visibility = Visibility.Visible;
		if (card.IsLoaded)
		{
			InteractionMotion.Reveal(card, 6.0, 0.996);
		}
	}

	private void Navigation_OnChecked(object sender, RoutedEventArgs e)
	{
		if (sender is not System.Windows.Controls.RadioButton radioButton
			|| radioButton.Tag is not string page
			|| DataContext is not ShellViewModel viewModel)
		{
			return;
		}

		viewModel.NavigateCommand.Execute(page);
		if (_loaded)
		{
			FrameworkElement frameworkElement = viewModel.CurrentPage switch
			{
				ShellPage.Screen => ScreenPanel,
				ShellPage.Appearance => ThemePanel,
				ShellPage.Automation => AutomationPanel,
				ShellPage.Settings => SettingsPanel,
				ShellPage.About => AboutPanel,
				_ => ScreenPanel
			};
			InteractionMotion.Reveal(frameworkElement, 8.0, 0.994);
		}
	}

	private async void ChooseImageButton_OnClick(object sender, RoutedEventArgs e)
	{
		Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog
		{
			Title = "选择键盘屏幕图片",
			Filter = "图片文件|*.jpg;*.jpeg;*.png;*.bmp;*.webp|所有文件|*.*"
		};
		if (openFileDialog.ShowDialog(this) != true)
		{
			return;
		}
		_imageTheme.ImagePath = openFileDialog.FileName;
		_settings.ImagePath = openFileDialog.FileName;
		_settings.SelectedThemeId = "image";
		_suppressThemeRefresh = true;
		try
		{
			SelectTheme("image");
		}
		finally
		{
			_suppressThemeRefresh = false;
		}
		await CommitAndPushAsync();
	}

	private async void TestConnectionButton_OnClick(object sender, RoutedEventArgs e)
	{
		await PushLatestAsync();
	}

	private void SettingsNavButton_OnClick(object sender, RoutedEventArgs e)
	{
		SettingsNav.IsChecked = true;
	}

	private void ZCat95Link_OnClick(object sender, RoutedEventArgs e)
	{
		Process.Start(new ProcessStartInfo
		{
			FileName = "https://github.com/zcat95",
			UseShellExecute = true
		});
	}

	private void RefreshIntervalSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		int num = (int)Math.Round(e.NewValue);
		if (RefreshIntervalValueText != null)
		{
			RefreshIntervalValueText.Text = $"{num} 秒";
		}
		if (_loaded)
		{
			_settings.RefreshSeconds = num;
			_timer.Interval = TimeSpan.FromSeconds(num);
			ScheduleAutoCommit();
		}
	}

	private void LyricOffsetSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		double value = Math.Round(e.NewValue * 2) / 2;
		if (LyricOffsetValueText != null)
		{
			LyricOffsetValueText.Text = $"{value:+0.0;-0.0;+0.0} 秒";
		}
		if (_loaded)
		{
			_settings.Music ??= new MusicSettings();
			_settings.Music.LyricOffsetSeconds = value;
			ScheduleAutoCommit();
		}
	}

	private void AccentColorTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
	{
		if (AccentColorPreview == null)
		{
			return;
		}
		if (!TryParseAccentColor(AccentColorTextBox.Text, out var color))
		{
			AccentColorTextBox.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 74, 84));
			return;
		}
		AccentColorTextBox.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(217, 221, 229));
		AccentColorPreview.Background = new SolidColorBrush(color);
		if (_loaded)
		{
			_settings.AccentColor = AccentColorTextBox.Text.Trim().ToUpperInvariant();
			_themeGalleryPreviewDirty = true;
			ScheduleAutoCommit();
		}
	}

	private async void ChooseAccentColorButton_OnClick(object sender, RoutedEventArgs e)
	{
		AccentColorDialog accentColorDialog = new AccentColorDialog(GetAccentColor())
		{
			Owner = this
		};
		if (accentColorDialog.ShowDialog() == true)
		{
			AccentColorTextBox.Text = $"#{accentColorDialog.SelectedColor.R:X2}{accentColorDialog.SelectedColor.G:X2}{accentColorDialog.SelectedColor.B:X2}";
			await RefreshPreviewAsync();
		}
	}

	private void OpenFontsFolderButton_OnClick(object sender, RoutedEventArgs e)
	{
		Directory.CreateDirectory(_fontCatalog.FolderPath);
		Process.Start(new ProcessStartInfo
		{
			FileName = _fontCatalog.FolderPath,
			UseShellExecute = true
		});
	}

	private async void FontFamilyComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (!_updatingFontList && FontFamilyComboBox.SelectedItem is ScreenFontOption screenFontOption)
		{
			_settings.SelectedFontId = screenFontOption.Id;
			if (_loaded)
			{
				_themeGalleryPreviewDirty = true;
				await CommitAndPushAsync();
			}
		}
	}

	private void FontCatalog_OnFontsChanged(object? sender, EventArgs e)
	{
		Dispatcher.BeginInvoke(async () =>
		{
			string preferredId = (FontFamilyComboBox.SelectedItem as ScreenFontOption)?.Id ?? _settings.SelectedFontId;
			ReloadFontOptions(preferredId);
			if (_loaded)
			{
				await RefreshPreviewAsync();
			}
		}, DispatcherPriority.Background);
	}

	private void ReloadFontOptions(string? preferredId)
	{
		IReadOnlyList<ScreenFontOption> readOnlyList = _fontCatalog.Scan();
		ScreenFontOption screenFontOption = readOnlyList.FirstOrDefault((ScreenFontOption font) => string.Equals(font.Id, preferredId, StringComparison.OrdinalIgnoreCase)) ?? ScreenFontOption.Default;
		_updatingFontList = true;
		try
		{
			FontFamilyComboBox.ItemsSource = readOnlyList;
			FontFamilyComboBox.SelectedItem = screenFontOption;
		}
		finally
		{
			_updatingFontList = false;
		}
		_settings.SelectedFontId = screenFontOption.Id;
	}

	private System.Windows.Media.FontFamily GetSelectedFontFamily()
	{
		return (FontFamilyComboBox.SelectedItem as ScreenFontOption)?.FontFamily ?? ScreenFontOption.Default.FontFamily;
	}

	private ScreenDisplayOptions GetScreenDisplayOptions()
	{
		return new ScreenDisplayOptions(_settings.ImageTimePlacement);
	}

	private System.Windows.Media.Color GetAccentColor()
	{
		if (!TryParseAccentColor(_settings.AccentColor, out var color))
		{
			return System.Windows.Media.Color.FromRgb(228, 105, 76);
		}
		return color;
	}

	private static bool TryParseAccentColor(string? value, out System.Windows.Media.Color color)
	{
		color = default(System.Windows.Media.Color);
		try
		{
			if (string.IsNullOrWhiteSpace(value) || !(System.Windows.Media.ColorConverter.ConvertFromString(value.Trim()) is System.Windows.Media.Color color2))
			{
				return false;
			}
			color = System.Windows.Media.Color.FromRgb(color2.R, color2.G, color2.B);
			return true;
		}
		catch (FormatException)
		{
			return false;
		}
	}

	private void MediaAutomationComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		ScheduleAutoCommit();
	}
	private void ThemeOptionComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		ScheduleAutoCommit();
	}

	private void SettingToggle_OnChanged(object sender, RoutedEventArgs e)
	{
		ScheduleAutoCommit();
	}

	private void WeatherAutomaticLocationCheckBox_OnChanged(object sender, RoutedEventArgs e)
	{
		if (WeatherLocationTextBox is not null)
		{
			WeatherLocationTextBox.IsEnabled = WeatherAutomaticLocationCheckBox.IsChecked != true;
		}
		ScheduleAutoCommit();
	}

	private void SettingTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
	{
		ScheduleAutoCommit();
	}

	private void EndpointIpPart_OnPreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
	{
		e.Handled = !e.Text.All(char.IsDigit);
	}

	private void EndpointIpPart_OnTextChanged(object sender, TextChangedEventArgs e)
	{
		if (_suppressEndpointPartSync || sender is not System.Windows.Controls.TextBox textBox)
		{
			return;
		}

		SyncEndpointTextFromParts();
		if (textBox.Text.Length == 3 && int.TryParse(textBox.Text, out int value) && value <= 255)
		{
			int index = Array.IndexOf(_endpointParts, textBox);
			if (index >= 0 && index < _endpointParts.Length - 1)
			{
				_endpointParts[index + 1].Focus();
				_endpointParts[index + 1].SelectAll();
			}
		}
	}

	private void EndpointIpPart_OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
	{
		if (sender is not System.Windows.Controls.TextBox textBox)
		{
			return;
		}

		int index = Array.IndexOf(_endpointParts, textBox);
		if (e.Key == System.Windows.Input.Key.Back && textBox.Text.Length == 0 && index > 0)
		{
			_endpointParts[index - 1].Focus();
			_endpointParts[index - 1].CaretIndex = _endpointParts[index - 1].Text.Length;
			e.Handled = true;
		}
		else if ((e.Key == System.Windows.Input.Key.OemPeriod || e.Key == System.Windows.Input.Key.Decimal) &&
			index >= 0 && index < _endpointParts.Length - 1)
		{
			_endpointParts[index + 1].Focus();
			_endpointParts[index + 1].SelectAll();
			e.Handled = true;
		}
	}

	private void EndpointIpPart_OnPasting(object sender, System.Windows.DataObjectPastingEventArgs e)
	{
		if (!e.SourceDataObject.GetDataPresent(System.Windows.DataFormats.UnicodeText))
		{
			e.CancelCommand();
			return;
		}

		string pasted = (e.SourceDataObject.GetData(System.Windows.DataFormats.UnicodeText) as string ?? string.Empty).Trim();
		if (TryPopulateEndpointParts(pasted))
		{
			e.CancelCommand();
			EndpointIpPart4.Focus();
			EndpointIpPart4.CaretIndex = EndpointIpPart4.Text.Length;
			return;
		}

		if (pasted.Length is < 1 or > 3 || !pasted.All(char.IsDigit))
		{
			e.CancelCommand();
		}
	}

	private void PopulateEndpointParts(string? value)
	{
		if (TryPopulateEndpointParts(value))
		{
			return;
		}

		_suppressEndpointPartSync = true;
		try
		{
			foreach (System.Windows.Controls.TextBox part in _endpointParts)
			{
				part.Text = string.Empty;
			}
		}
		finally
		{
			_suppressEndpointPartSync = false;
		}
		EndpointTextBox.Text = string.Empty;
	}

	private bool TryPopulateEndpointParts(string? value)
	{
		string candidate = ExtractDeviceIp(value);
		if (!IPAddress.TryParse(candidate, out IPAddress? address) || address.AddressFamily != AddressFamily.InterNetwork)
		{
			return false;
		}

		string[] segments = address.ToString().Split('.');
		_suppressEndpointPartSync = true;
		try
		{
			for (int index = 0; index < _endpointParts.Length; index++)
			{
				_endpointParts[index].Text = segments[index];
			}
		}
		finally
		{
			_suppressEndpointPartSync = false;
		}
		EndpointTextBox.Text = address.ToString();
		UpdateEndpointSummary();
		return true;
	}

	private void SyncEndpointTextFromParts()
	{
		EndpointTextBox.Text = string.Join(".", _endpointParts.Select(part => part.Text.Trim()));
		UpdateEndpointSummary();
	}

	private static string ReadComboTag(System.Windows.Controls.ComboBox comboBox, string fallback)
	{
		return (comboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? fallback;
	}
	private static void SelectComboByTag(System.Windows.Controls.ComboBox comboBox, string tag)
	{
		comboBox.SelectedItem = comboBox.Items.OfType<ComboBoxItem>().FirstOrDefault((ComboBoxItem item) => string.Equals(item.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase)) ?? comboBox.Items.OfType<ComboBoxItem>().FirstOrDefault();
	}

	private static T ReadComboEnum<T>(System.Windows.Controls.ComboBox comboBox, T fallback) where T : struct, Enum
	{
		if (!Enum.TryParse<T>((comboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString(), ignoreCase: true, out var result))
		{
			return fallback;
		}
		return result;
	}

	private async void MinimizeButton_OnClick(object sender, RoutedEventArgs e)
	{
		if (_windowTransitionRunning)
		{
			return;
		}
		_windowTransitionRunning = true;
		try
		{
			await InteractionMotion.HideAsync(WindowRoot, 4.0);
			if (MinimizeToTrayCheckBox.IsChecked == true)
			{
				HideToTray();
				return;
			}
			_revealAfterMinimize = true;
			base.WindowState = WindowState.Minimized;
		}
		finally
		{
			InteractionMotion.Reset(WindowRoot);
			_windowTransitionRunning = false;
		}
	}

	private void MaximizeButton_OnClick(object sender, RoutedEventArgs e)
	{
		base.WindowState = ((base.WindowState != WindowState.Maximized) ? WindowState.Maximized : WindowState.Normal);
	}

	private async void CloseButton_OnClick(object sender, RoutedEventArgs e)
	{
		if (_windowTransitionRunning)
		{
			return;
		}
		_windowTransitionRunning = true;
		try
		{
			await InteractionMotion.HideAsync(WindowRoot, 4.0);
			Close();
		}
		finally
		{
			InteractionMotion.Reset(WindowRoot);
			_windowTransitionRunning = false;
		}
	}

	private void MainWindow_OnStateChanged(object? sender, EventArgs e)
	{
		if (base.WindowState != WindowState.Minimized)
		{
			_restoreWindowState = base.WindowState;
			if (_revealAfterMinimize)
			{
				_revealAfterMinimize = false;
				InteractionMotion.Reveal(WindowRoot, 8.0, 0.994);
			}
		}
		else if (_loaded && MinimizeToTrayCheckBox.IsChecked == true)
		{
			HideToTray();
		}
	}

	private void MainWindow_OnClosing(object? sender, CancelEventArgs e)
	{
		if (!_explicitExit && _loaded && CloseToTrayCheckBox.IsChecked == true)
		{
			e.Cancel = true;
			HideToTray();
		}
	}

	private void HideToTray()
	{
		WindowState windowState = base.WindowState;
		if (windowState == WindowState.Normal || windowState == WindowState.Maximized)
		{
			_restoreWindowState = base.WindowState;
		}
		_trayIcon.Visible = true;
		base.ShowInTaskbar = false;
		Hide();
	}

	private void RestoreFromTray()
	{
		base.ShowInTaskbar = true;
		Show();
		base.WindowState = ((_restoreWindowState != WindowState.Minimized) ? _restoreWindowState : WindowState.Normal);
		Activate();
		base.Topmost = true;
		base.Topmost = false;
		Focus();
		InteractionMotion.Reveal(WindowRoot, 8.0, 0.994);
	}

	private void ExitApplication()
	{
		_explicitExit = true;
		_trayIcon.Visible = false;
		Close();
	}

	internal void RestoreFromExternalActivation()
	{
		RestoreFromTray();
	}

	private void Application_OnSessionEnding(object sender, SessionEndingCancelEventArgs e)
	{
		_explicitExit = true;
	}

	private void UpdateTrayVisibility()
	{
		if (_loaded)
		{
			_trayIcon.Visible = !base.IsVisible || _settings.MinimizeToTray || _settings.CloseToTray;
		}
	}

	private static BitmapImage LoadBitmap(byte[] bytes)
	{
		using MemoryStream streamSource = new MemoryStream(bytes);
		BitmapImage bitmapImage = new BitmapImage();
		bitmapImage.BeginInit();
		bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
		bitmapImage.StreamSource = streamSource;
		bitmapImage.EndInit();
		bitmapImage.Freeze();
		return bitmapImage;
	}

	private static string ResolveMusicSourceName(string? sourceAppId)
	{
		if (string.IsNullOrWhiteSpace(sourceAppId)) return "Windows 媒体";
		if (sourceAppId.Contains("cloudmusic", StringComparison.OrdinalIgnoreCase) ||
			sourceAppId.Contains("netease", StringComparison.OrdinalIgnoreCase)) return "网易云音乐";
		if (sourceAppId.Contains("spotify", StringComparison.OrdinalIgnoreCase)) return "Spotify";
		if (sourceAppId.Contains("qqmusic", StringComparison.OrdinalIgnoreCase)) return "QQ 音乐";
		if (sourceAppId.Contains("chrome", StringComparison.OrdinalIgnoreCase)) return "Chrome";
		if (sourceAppId.Contains("msedge", StringComparison.OrdinalIgnoreCase)) return "Edge";
		return "Windows 媒体";
	}

	private void SetDeviceStatus(bool success)
	{
		DeviceStatusText.Text = (success ? "设备在线" : "断开连接");
		System.Windows.Media.Brush brush = (success ? ((System.Windows.Media.Brush)FindResource("SuccessBrush")) : ((System.Windows.Media.Brush)FindResource("SecondaryText")));
		DeviceStatusText.Foreground = brush;
		DeviceStatusDot.Fill = brush;
	}

}
