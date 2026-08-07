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

	private readonly IAutomaticWeatherLocationProvider _weatherLocationProvider;

	private readonly IStockSnapshotSource _stockSource;

	private readonly IDashboardSnapshotBuilder _snapshotBuilder;

	private readonly IDashboardRefreshService _refreshService;

	private readonly IDeviceTransport _transport;

	private readonly IDisplayPushService _pushService;

	private readonly ISettingsStore _settingsStore;

	private readonly ImageTheme _imageTheme;

	private readonly FontFolderCatalog _fontCatalog;

	private readonly ScreenViewModel _screenViewModel;

	private readonly AppearanceViewModel _appearanceViewModel;

	private readonly AutomationViewModel _automationViewModel;

	private readonly SettingsViewModel _settingsViewModel;

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

	private bool _updatingAppearance;

	private bool _updatingAutomation;

	private bool _updatingSettingsPage;

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
			null,
			new HttpImageDeviceTransport(),
			null,
			new JsonSettingsStore(),
			new ImageTheme(),
			new FontFolderCatalog(System.IO.Path.Combine(AppContext.BaseDirectory, "Fonts")),
			new ShellViewModel(),
			ownsServices: true)
	{
	}

	public MainWindow(AppSettings initialSettings, ISettingsStore settingsStore)
		: this(
			initialSettings,
			new WindowsSystemSnapshotSource(),
			new WindowsMusicSnapshotSource(),
			new LrcLibLyricsSnapshotSource(),
			new OpenMeteoWeatherSnapshotSource(),
			new WindowsWeatherLocationProvider(),
			new YahooStockSnapshotSource(),
			null,
			null,
			new HttpImageDeviceTransport(),
			null,
			settingsStore,
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
		IAutomaticWeatherLocationProvider weatherLocationProvider,
		IStockSnapshotSource stockSource,
		IDashboardSnapshotBuilder snapshotBuilder,
		IDashboardRefreshService refreshService,
		IDeviceTransport transport,
		IDisplayPushService pushService,
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
			refreshService,
			transport,
			pushService,
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
		IAutomaticWeatherLocationProvider weatherLocationProvider,
		IStockSnapshotSource stockSource,
		IDashboardSnapshotBuilder? snapshotBuilder,
		IDashboardRefreshService? refreshService,
		IDeviceTransport transport,
		IDisplayPushService? pushService,
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
		_refreshService = refreshService ?? new DashboardRefreshService(
			musicSource,
			_snapshotBuilder,
			new WeatherSettingsResolver(weatherLocationProvider));
		_transport = transport;
		_pushService = pushService ?? new DisplayPushService(transport);
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
		_screenViewModel = shellViewModel.Screen;
		_appearanceViewModel = shellViewModel.Appearance;
		_automationViewModel = shellViewModel.Automation;
		_settingsViewModel = shellViewModel.Settings;
		_screenViewModel.ThemeSelected += ScreenViewModel_OnThemeSelected;
		_appearanceViewModel.PropertyChanged += AppearanceViewModel_OnPropertyChanged;
		_automationViewModel.PropertyChanged += AutomationViewModel_OnPropertyChanged;
		_settingsViewModel.PropertyChanged += SettingsViewModel_OnPropertyChanged;
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
				(_weatherLocationProvider as IDisposable)?.Dispose();
				(_stockSource as IDisposable)?.Dispose();
				(_lyricsSource as IDisposable)?.Dispose();
				(_musicSource as IDisposable)?.Dispose();
				(_systemSource as IDisposable)?.Dispose();
			_fontCatalog.Dispose();
			}
			_screenViewModel.ThemeSelected -= ScreenViewModel_OnThemeSelected;
			_appearanceViewModel.PropertyChanged -= AppearanceViewModel_OnPropertyChanged;
			_automationViewModel.PropertyChanged -= AutomationViewModel_OnPropertyChanged;
			_settingsViewModel.PropertyChanged -= SettingsViewModel_OnPropertyChanged;
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
		bool previousSuppression = _suppressThemeRefresh;
		_suppressThemeRefresh = true;
		try
		{
			_screenViewModel.SetThemes(
				_themeDefinitions.Select(CreateThemeCard),
				_settings.SelectedThemeId);
			_screenViewModel.SelectTheme(_settings.SelectedThemeId, notify: true);
		}
		finally
		{
			_suppressThemeRefresh = previousSuppression;
		}
		_screenViewModel.UpdateCardWidth(ThemeListPanel.ActualWidth);
	}

	private ThemeCardViewModel CreateThemeCard(ThemeDefinition definition)
	{
		ImageSource? preview = null;
		try
		{
			RenderedFrame frame = _renderer.Render(
				definition.Theme,
				SystemSnapshot.DesignSample,
				86,
				GetAccentColor(),
				GetSelectedFontFamily(),
				GetScreenDisplayOptions());
			preview = LoadBitmap(frame.JpegBytes);
		}
		catch
		{
			// A failed gallery preview must not prevent the remaining themes from loading.
		}

		return new ThemeCardViewModel(definition, preview);
	}

	private void PopulateMediaAutomationThemeSelectors()
	{
		_updatingAutomation = true;
		try
		{
			_automationViewModel.Load(_settings, _themeDefinitions);
		}
		finally
		{
			_updatingAutomation = false;
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
		ApplySettingsToControls();
		if (!_settings.HasCompletedOnboarding)
		{
			var guide = new FirstRunGuideWindow(DeviceEndpoint.ExtractIp(_settings.DeviceEndpoint))
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
		_updatingAppearance = true;
		try
		{
			_appearanceViewModel.Load(_settings, _fontCatalog.Scan());
		}
		finally
		{
			_updatingAppearance = false;
		}
		PopulateMediaAutomationThemeSelectors();
		_updatingSettingsPage = true;
		try
		{
			_settingsViewModel.Load(_settings);
		}
		finally
		{
			_updatingSettingsPage = false;
		}
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
		_automationViewModel.ApplyTo(_settings);
		_settingsViewModel.ApplyTo(_settings);
		StartupRegistration.TrySetEnabled(_settings.LaunchAtStartup);
		_appearanceViewModel.ApplyTo(_settings);
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
		catch (Exception ex)
		{
			Trace.TraceError($"Failed to save or apply display settings: {ex}");
			SetOperationFailure("保存失败");
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
			ThemeDefinition selectedDefinition = GetSelectedThemeDefinition() ?? _themeDefinitions[0];
			DashboardRefreshResult refresh = await _refreshService.RefreshAsync(
				new DashboardRefreshRequest(
					_themeDefinitions,
					_settings,
					selectedDefinition.Id,
					_lastEffectiveThemeId,
					ReadAiQuotaAsync));
			ThemeDefinition effectiveDefinition = refresh.EffectiveTheme;
			IScreenTheme theme2 = effectiveDefinition.Theme;
			_mediaAutomationThemeChanged = refresh.EffectiveThemeChanged;
			_lastEffectiveThemeId = theme2.Id;
			_automaticLocationFallback = refresh.UsedAutomaticWeatherLocationFallback;
			bool needsLyrics = effectiveDefinition.Requires(ThemeDataRequirements.Lyrics);
			bool needsWeather = effectiveDefinition.Requires(ThemeDataRequirements.Weather);
			bool needsStocks = effectiveDefinition.Requires(ThemeDataRequirements.Stocks);
			_latestSnapshot = refresh.Snapshot;
			SystemSnapshot system = _latestSnapshot;
			MusicSnapshot musicSnapshot = system.Music ?? refresh.SourceMusic;
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
		catch (Exception ex)
		{
			Trace.TraceWarning($"Failed to refresh display preview: {ex}");
		}
		finally
		{
			_busy = false;
		}
	}

	private async Task<AiQuotaSnapshot?> ReadAiQuotaAsync(CancellationToken cancellationToken = default)
	{
		if (_miMoWindow == null)
		{
			AiSourceStatusText.Text = "请先登录小米控制台";
			return _latestAiQuota is null ? AiQuotaSnapshot.Empty : ApplyAiDisplayName(_latestAiQuota);
		}
		try
		{
			return UpdateMiMoUsage(await _miMoWindow.ReadAsync(cancellationToken));
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
		if (_latestFrame is null)
		{
			SetDeviceStatus(success: false);
			return;
		}
		_busy = true;
		try
		{
			SetDeviceStatus((await _pushService.PushAsync(_settingsViewModel.EndpointIp, _latestFrame)).Success);
		}
		finally
		{
			_busy = false;
		}
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
		if (_themeDefinitions.Count == 0)
		{
			return;
		}

		_settings.SelectedThemeId = GetThemeDefinition(id)?.Id ?? _themeDefinitions[0].Id;
		_screenViewModel.SelectTheme(_settings.SelectedThemeId, notify: true);
	}

	private void ThemeListPanel_OnSizeChanged(object sender, SizeChangedEventArgs e) =>
		_screenViewModel.UpdateCardWidth(e.NewSize.Width);

	private void LocateCurrent_OnClick(object sender, RoutedEventArgs e)
	{
		_screenViewModel.SelectCategory("all");

		Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
		{
			(ThemeListPanel.ItemContainerGenerator.ContainerFromItem(_screenViewModel.SelectedTheme) as FrameworkElement)
				?.BringIntoView();
		});
	}

	private void AppearanceViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (_updatingAppearance)
		{
			return;
		}

		switch (e.PropertyName)
		{
			case nameof(AppearanceViewModel.AppearanceMode):
				_settings.AppearanceMode = _appearanceViewModel.AppearanceMode;
				ApplyAppearance();
				ScheduleAutoCommit();
				break;
			case nameof(AppearanceViewModel.AccentColor):
				if (_appearanceViewModel.IsAccentColorValid)
				{
					_settings.AccentColor = _appearanceViewModel.AccentColor.Trim().ToUpperInvariant();
					_themeGalleryPreviewDirty = true;
					ScheduleAutoCommit();
				}
				break;
			case nameof(AppearanceViewModel.SelectedFontOption):
				if (_appearanceViewModel.SelectedFontOption is not null)
				{
					_settings.SelectedFontId = _appearanceViewModel.SelectedFontOption.Id;
					_themeGalleryPreviewDirty = true;
					if (_loaded)
					{
						_ = CommitAndPushAsync();
					}
				}
				break;
			case nameof(AppearanceViewModel.SelectedImageTimePlacement):
				if (_appearanceViewModel.SelectedImageTimePlacement is not null)
				{
					_settings.ImageTimePlacement = _appearanceViewModel.SelectedImageTimePlacement.Value;
					ScheduleAutoCommit();
				}
				break;
		}
	}

	private void AutomationViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (_updatingAutomation)
		{
			return;
		}

		switch (e.PropertyName)
		{
			case nameof(AutomationViewModel.AutoPush):
				_settings.AutoPush = _automationViewModel.AutoPush;
				break;
			case nameof(AutomationViewModel.RefreshSeconds):
				_settings.RefreshSeconds = _automationViewModel.RefreshSeconds;
				_timer.Interval = TimeSpan.FromSeconds(_settings.RefreshSeconds);
				break;
			case nameof(AutomationViewModel.AutoSwitchToMusic):
				_settings.AutoSwitchToMusic = _automationViewModel.AutoSwitchToMusic;
				break;
			case nameof(AutomationViewModel.AutoMediaThemeSwitch):
				_settings.AutoMediaThemeSwitch = _automationViewModel.AutoMediaThemeSwitch;
				break;
			case nameof(AutomationViewModel.SelectedIdleTheme):
				_settings.MediaIdleThemeId = _automationViewModel.SelectedIdleTheme?.Id ?? "system";
				break;
			case nameof(AutomationViewModel.SelectedPlayingTheme):
				_settings.MediaPlayingThemeId = _automationViewModel.SelectedPlayingTheme?.Id ?? "music";
				break;
			default:
				return;
		}

		ScheduleAutoCommit();
	}

	private void SettingsViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (_updatingSettingsPage)
		{
			return;
		}

		if (e.PropertyName == nameof(SettingsViewModel.EndpointIp))
		{
			UpdateEndpointSummary();
			ScheduleAutoCommit();
			return;
		}

		if (e.PropertyName is nameof(SettingsViewModel.SafeLeft)
			or nameof(SettingsViewModel.SafeTop)
			or nameof(SettingsViewModel.SafeRight)
			or nameof(SettingsViewModel.SafeBottom)
			or nameof(SettingsViewModel.MinimizeToTray)
			or nameof(SettingsViewModel.CloseToTray)
			or nameof(SettingsViewModel.StartMinimized)
			or nameof(SettingsViewModel.LaunchAtStartup))
		{
			ScheduleAutoCommit();
		}
	}

	private void UpdateEndpointSummary()
	{
		string ipString = _settingsViewModel.EndpointIp;
		EndpointSummaryText.Text = ((IPAddress.TryParse(ipString, out IPAddress? address) && address.AddressFamily == AddressFamily.InterNetwork) ? address.ToString() : "地址未配置");
	}

	private void ScreenViewModel_OnThemeSelected(ThemeDefinition definition)
	{
		_ = ApplyThemeSelectionAsync(definition);
	}

	private async Task ApplyThemeSelectionAsync(ThemeDefinition definition)
	{
		string id = definition.Id;
		IScreenTheme screenTheme = definition.Theme;
		CurrentThemeNameText.Text = screenTheme.DisplayName;
		CurrentThemeDescription.Text = (definition.Shows(ThemeSettingsSections.Image) && !string.IsNullOrWhiteSpace(_imageTheme.ImagePath))
			? _imageTheme.ImagePath
			: screenTheme.Description;
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

	private void EndpointShortcutButton_OnClick(object sender, RoutedEventArgs e)
	{
		if (DataContext is ShellViewModel shellViewModel)
		{
			shellViewModel.NavigateCommand.Execute("settings");
			if (_loaded)
			{
				InteractionMotion.Reveal(SettingsPanel, 8.0, 0.994);
			}
		}

		Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
		{
			ThemeScrollViewer.ScrollToTop();
			EndpointIpPart1.Focus();
			EndpointIpPart1.SelectAll();
		});
	}

	private void ZCat95Link_OnClick(object sender, RoutedEventArgs e)
	{
		Process.Start(new ProcessStartInfo
		{
			FileName = "https://github.com/zcat95",
			UseShellExecute = true
		});
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

	private async void ChooseAccentColorButton_OnClick(object sender, RoutedEventArgs e)
	{
		AccentColorDialog accentColorDialog = new AccentColorDialog(GetAccentColor())
		{
			Owner = this
		};
		if (accentColorDialog.ShowDialog() == true)
		{
			_appearanceViewModel.AccentColor = $"#{accentColorDialog.SelectedColor.R:X2}{accentColorDialog.SelectedColor.G:X2}{accentColorDialog.SelectedColor.B:X2}";
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

	private void FontCatalog_OnFontsChanged(object? sender, EventArgs e)
	{
		Dispatcher.BeginInvoke(async () =>
		{
			string preferredId = _appearanceViewModel.SelectedFontOption?.Id ?? _settings.SelectedFontId;
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
		_updatingAppearance = true;
		try
		{
			_appearanceViewModel.SetFontOptions(readOnlyList, preferredId);
		}
		finally
		{
			_updatingAppearance = false;
		}
		_settings.SelectedFontId = _appearanceViewModel.SelectedFontOption?.Id ?? ScreenFontOption.Default.Id;
	}

	private System.Windows.Media.FontFamily GetSelectedFontFamily()
	{
		return _appearanceViewModel.SelectedFontOption?.FontFamily ?? ScreenFontOption.Default.FontFamily;
	}

	private ScreenDisplayOptions GetScreenDisplayOptions()
	{
		return new ScreenDisplayOptions(_settings.ImageTimePlacement);
	}

	private System.Windows.Media.Color GetAccentColor()
	{
		if (!AppearanceViewModel.TryParseAccentColor(_appearanceViewModel.AccentColor, out var color))
		{
			return System.Windows.Media.Color.FromRgb(228, 105, 76);
		}
		return color;
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
		if (sender is not System.Windows.Controls.TextBox textBox)
		{
			return;
		}

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
		_ = _settingsViewModel.SetEndpoint(value);
		UpdateEndpointSummary();
	}

	private bool TryPopulateEndpointParts(string? value)
	{
		bool isValid = _settingsViewModel.SetEndpoint(value);
		if (isValid)
		{
			UpdateEndpointSummary();
		}
		return isValid;
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
			if (_settingsViewModel.MinimizeToTray)
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
		else if (_loaded && _settingsViewModel.MinimizeToTray)
		{
			HideToTray();
		}
	}

	private void MainWindow_OnClosing(object? sender, CancelEventArgs e)
	{
		if (!_explicitExit && _loaded && _settingsViewModel.CloseToTray)
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

	private void SetOperationFailure(string message)
	{
		DeviceStatusText.Text = message;
		System.Windows.Media.Brush brush = (System.Windows.Media.Brush)FindResource("DangerBrush");
		DeviceStatusText.Foreground = brush;
		DeviceStatusDot.Fill = brush;
	}

}
