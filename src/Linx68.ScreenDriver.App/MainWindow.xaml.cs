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

	private readonly CodexSetupService _codexSetupService = new();

	private readonly IAiQuotaSnapshotSource _codexQuotaSource = new CodexQuotaSnapshotSource();

	private readonly ICodexTaskSnapshotSource _codexTaskSource = new CodexTaskSnapshotSource();

	private AiQuotaSnapshot? _latestAiQuota;

	private CodexTaskSnapshot? _latestCodexTasks;

	private DateTimeOffset _nextCodexQuotaReadAt = DateTimeOffset.MinValue;

	private DateTimeOffset _nextCodexTaskReadAt = DateTimeOffset.MinValue;

	private SystemSnapshot? _latestSnapshot;

	private bool _busy;

	private bool _pushing;

	private DateTimeOffset _nextDevicePushAt = DateTimeOffset.MinValue;

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
		_snapshotBuilder = snapshotBuilder ?? new DashboardSnapshotBuilder(
			systemSource,
			lyricsSource,
			weatherSource);
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
		LoadAutomationSettings();
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

	private async void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
	{
		_settings = _initialSettings ?? await _settingsStore.LoadAsync();
		ApplyAppearance();
		ApplySettingsToControls();
		_ = Dispatcher.BeginInvoke(DispatcherPriority.Loaded, UpdateThemeGalleryCardWidth);
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
		SetDeviceStatus(success: false);
		_timer.Start();
		UpdateTrayVisibility();
		if ((_settings.StartMinimized || Environment.GetCommandLineArgs().Any(argument => string.Equals(argument, "--startup", StringComparison.OrdinalIgnoreCase))) && !_startMinimizeApplied)
		{
			_startMinimizeApplied = true;
			HideToTray();
		}
		else
		{
			RestoreFromTray();
			InteractionMotion.Reveal(WindowRoot, 10.0, 0.994);
		}
		await RefreshPreviewAsync();
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
		LoadAutomationSettings();
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
		AiQuotaSourceKind aiSourceKind = _settings.AiQuota.SourceKind;
		SelectComboByTag(AiSourceComboBox, aiSourceKind == AiQuotaSourceKind.OpenAICodex ? "Codex" : "MiMo");
		AiDisplayNameTextBox.Text = string.IsNullOrWhiteSpace(_settings.AiQuota.DisplayName)
			? GetDefaultAiDisplayName(aiSourceKind)
			: _settings.AiQuota.DisplayName;
		UpdateAiSourceUi();
		_settings.Weather ??= new WeatherSettings();
		WeatherAutomaticLocationCheckBox.IsChecked = _settings.Weather.UseAutomaticLocation;
		WeatherLocationTextBox.Text = string.IsNullOrWhiteSpace(_settings.Weather.LocationQuery) ? "北京" : _settings.Weather.LocationQuery;
		WeatherLocationTextBox.IsEnabled = !_settings.Weather.UseAutomaticLocation;
		_settings.Music ??= new MusicSettings();
		OnlineLyricsCheckBox.IsChecked = _settings.Music.EnableOnlineLyrics;
		LyricOffsetSlider.Value = Math.Clamp(_settings.Music.LyricOffsetSeconds, -3, 3);
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
			SourceKind = ReadAiSourceKind(),
			DisplayName = ReadAiDisplayName()
		};
		_settings.Weather = new WeatherSettings
		{
			LocationQuery = ReadWeatherLocation(),
			UseAutomaticLocation = WeatherAutomaticLocationCheckBox.IsChecked == true
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

	private void UpdateRenderer()
	{
		ScreenProfile profile = new ScreenProfile(142, 428, 524288, _settings.SafeArea);
		_renderer = new ScreenRenderer(profile);
	}

	private async void Timer_OnTick(object? sender, EventArgs e)
	{
		bool selectedThemeIsStatic = GetSelectedThemeDefinition()?.IsStatic == true;
		if (!_busy && (!selectedThemeIsStatic || _settings.AutoSwitchToMusic))
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

	private async Task CommitAndPushAsync(bool preferCachedPreview = false)
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
			bool pushedCachedPreview = preferCachedPreview && TryRenderLatestSnapshot();
			if (pushedCachedPreview)
			{
				await PushLatestAsync(force: true);
			}
			await RefreshPreviewAsync();
			if (_themeGalleryPreviewDirty)
			{
				_themeGalleryPreviewDirty = false;
				BuildThemeList();
			}
			_mediaAutomationThemeChanged = false;
			await PushLatestAsync(force: true);
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

	private bool TryRenderLatestSnapshot()
	{
		if (_latestSnapshot is null || _themeDefinitions.Count == 0)
		{
			return false;
		}

		try
		{
			ThemeDefinition selectedDefinition = GetSelectedThemeDefinition() ?? _themeDefinitions[0];
			_latestFrame = _renderer.Render(
				selectedDefinition.Theme,
				_latestSnapshot,
				100,
				GetAccentColor(),
				GetSelectedFontFamily(),
				GetScreenDisplayOptions());
			DevicePreview.FrameSource = LoadBitmap(_latestFrame.JpegBytes);
			return true;
		}
		catch (Exception ex)
		{
			Trace.TraceWarning($"Failed to render cached display preview: {ex}");
			return false;
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
					ReadAiQuotaAsync,
					ReadCodexTasksAsync));
			ThemeDefinition effectiveDefinition = refresh.EffectiveTheme;
			IScreenTheme theme2 = effectiveDefinition.Theme;
			_mediaAutomationThemeChanged = refresh.EffectiveThemeChanged;
			_lastEffectiveThemeId = theme2.Id;
			_automaticLocationFallback = refresh.UsedAutomaticWeatherLocationFallback;
			bool needsLyrics = effectiveDefinition.Requires(ThemeDataRequirements.Lyrics);
			bool needsWeather = effectiveDefinition.Requires(ThemeDataRequirements.Weather);
			_latestSnapshot = refresh.Snapshot;
			SystemSnapshot system = _latestSnapshot;
			MusicSnapshot musicSnapshot = system.Music ?? refresh.SourceMusic;
			WeatherSnapshot? weather = system.Weather;
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
			string album = string.IsNullOrWhiteSpace(musicSnapshot.AlbumTitle) ? string.Empty : $" · 专辑：{musicSnapshot.AlbumTitle}";
			MusicSourceText.Text = (musicSnapshot.Available ? $"{musicSource} · {(musicSnapshot.IsPlaying ? "正在播放" : "已暂停")} · {musicSnapshot.Title}  —  {musicSnapshot.Artist}{album}{lyricStatus}" : "当前没有可用的 Windows 媒体会话");
			if (weather is { Available: true })
			{
				WeatherSourceStatusText.Text = $"{(_automaticLocationFallback ? "自动定位不可用，已使用 " : string.Empty)}{weather.LocationName} · {weather.TemperatureC:0}° · {weather.ConditionText}{(weather.IsStale ? " · 上次数据" : string.Empty)}";
			}
			else if (needsWeather)
			{
				WeatherSourceStatusText.Text = weather?.ErrorMessage ?? "暂时无法获取天气数据";
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
		if (ReadAiSourceKind() == AiQuotaSourceKind.OpenAICodex)
		{
			return await ReadCodexQuotaAsync(force: false, cancellationToken);
		}

		if (_miMoWindow == null)
		{
			AiSourceStatusText.Text = "请先登录小米控制台";
			return _latestAiQuota is null ? AiQuotaSnapshot.Empty : ApplyAiDisplayName(_latestAiQuota);
		}
		try
		{
			return UpdateAiQuotaUsage(await _miMoWindow.ReadAsync(cancellationToken));
		}
		catch (Exception ex)
		{
			AiSourceStatusText.Text = ex.Message;
			AiSourceStatusText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 74, 84));
			return _latestAiQuota is null ? AiQuotaSnapshot.Empty : ApplyAiDisplayName(_latestAiQuota);
		}
	}

	private async Task<CodexTaskSnapshot?> ReadCodexTasksAsync(CancellationToken cancellationToken = default)
	{
		bool isAiQuotaTheme = string.Equals(
			GetSelectedThemeDefinition()?.Id,
			"ai-quota",
			StringComparison.OrdinalIgnoreCase);
		if (isAiQuotaTheme && ReadAiSourceKind() != AiQuotaSourceKind.OpenAICodex)
		{
			return null;
		}

		return await ReadCodexTasksAsync(force: false, cancellationToken);
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
				UpdateAiQuotaUsage(snapshot);
				ScheduleAutoCommit();
			});
		};
		return _miMoWindow;
	}

	private async Task<AiQuotaSnapshot> ReadCodexQuotaAsync(bool force, CancellationToken cancellationToken = default)
	{
		if (!force && _latestAiQuota is { Available: true } cached && DateTimeOffset.UtcNow < _nextCodexQuotaReadAt)
		{
			return ApplyAiDisplayName(cached);
		}

		AiSourceStatusText.Text = "正在读取 Codex 额度…";
		try
		{
			AiQuotaSnapshot snapshot = await _codexQuotaSource.ReadAsync(cancellationToken);
			_nextCodexQuotaReadAt = DateTimeOffset.UtcNow.AddSeconds(30);
			return UpdateAiQuotaUsage(snapshot);
		}
		catch (Exception ex)
		{
			_nextCodexQuotaReadAt = DateTimeOffset.UtcNow.AddSeconds(10);
			AiSourceStatusText.Text = ex.Message;
			AiSourceStatusText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 74, 84));
			return _latestAiQuota is null
				? AiQuotaSnapshot.Unavailable(ReadAiDisplayName())
				: ApplyAiDisplayName(_latestAiQuota);
		}
	}

	private async Task<CodexTaskSnapshot> ReadCodexTasksAsync(bool force, CancellationToken cancellationToken = default)
	{
		if (!force && _latestCodexTasks is not null && DateTimeOffset.UtcNow < _nextCodexTaskReadAt)
		{
			return _latestCodexTasks;
		}

		try
		{
			CodexTaskSnapshot snapshot = await _codexTaskSource.ReadAsync(cancellationToken);
			_latestCodexTasks = snapshot;
			_nextCodexTaskReadAt = DateTimeOffset.UtcNow.AddSeconds(12);
			return snapshot;
		}
		catch (Exception ex)
		{
			_nextCodexTaskReadAt = DateTimeOffset.UtcNow.AddSeconds(12);
			return _latestCodexTasks ?? CodexTaskSnapshot.Unavailable(DateTimeOffset.UtcNow, ex.Message);
		}
	}

	private AiQuotaSnapshot UpdateAiQuotaUsage(AiQuotaSnapshot snapshot)
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
		else if (snapshot.Available)
		{
			MiMoCreditsValueText.Text = $"{100d - snapshot.ClampedRemainingPercent:0}% 已使用";
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
		return GetDefaultAiDisplayName(ReadAiSourceKind());
	}

	private AiQuotaSourceKind ReadAiSourceKind() =>
		ReadComboTag(AiSourceComboBox, "MiMo") == "Codex"
			? AiQuotaSourceKind.OpenAICodex
			: AiQuotaSourceKind.XiaomiMiMoTokenPlanChina;

	private static string GetDefaultAiDisplayName(AiQuotaSourceKind sourceKind) =>
		sourceKind == AiQuotaSourceKind.OpenAICodex ? "Codex" : "MiMo";

	private void AiSourceComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (_updatingSettingsPage)
		{
			return;
		}

		AiQuotaSourceKind sourceKind = ReadAiSourceKind();
		if (string.IsNullOrWhiteSpace(AiDisplayNameTextBox.Text) ||
			AiDisplayNameTextBox.Text.Trim() is "MiMo" or "Codex")
		{
			AiDisplayNameTextBox.Text = GetDefaultAiDisplayName(sourceKind);
		}
		_latestAiQuota = null;
		_latestCodexTasks = null;
		_nextCodexQuotaReadAt = DateTimeOffset.MinValue;
		_nextCodexTaskReadAt = DateTimeOffset.MinValue;
		ResetAiQuotaDisplay();
		UpdateAiSourceUi();
		ScheduleAutoCommit();
	}

	private void UpdateAiSourceUi()
	{
		bool isCodex = ReadAiSourceKind() == AiQuotaSourceKind.OpenAICodex;
		MiMoActionsPanel.Visibility = isCodex ? Visibility.Collapsed : Visibility.Visible;
		CodexActionsPanel.Visibility = isCodex ? Visibility.Visible : Visibility.Collapsed;
		AiSourceDescriptionText.Text = isCodex
			? "通过本机 Codex App Server 读取当前 ChatGPT Codex 额度窗口与下次重置时间。身份认证仍由 Codex 管理，本应用不会读取、复制或导出 auth.json。"
			: "China · 读取订阅套餐的真实 Credits，用量信息仅保存在本机。";
	}

	private void ResetAiQuotaDisplay()
	{
		MiMoRemainingValueText.Text = "—";
		MiMoCreditsValueText.Text = "—";
		MiMoExpiryValueText.Text = "—";
		MiMoRemainingProgress.Value = 0;
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
			UpdateAiQuotaUsage(await _miMoWindow.ReadAsync(force: true));
			await CommitAndPushAsync();
		}
		catch (Exception ex)
		{
			AiSourceStatusText.Text = ex.Message;
			AiSourceStatusText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 74, 84));
		}
	}

	private async Task PushLatestAsync(bool force = false)
	{
		if (_pushing || (!force && DateTimeOffset.UtcNow < _nextDevicePushAt))
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
		_pushing = true;
		try
		{
			bool success = (await _pushService.PushAsync(_settingsViewModel.EndpointIp, _latestFrame)).Success;
			SetDeviceStatus(success);
			_nextDevicePushAt = success
				? DateTimeOffset.MinValue
				: DateTimeOffset.UtcNow.AddSeconds(5);
		}
		catch (Exception ex)
		{
			Trace.TraceWarning($"Failed to push display frame: {ex}");
			SetDeviceStatus(success: false);
			_nextDevicePushAt = DateTimeOffset.UtcNow.AddSeconds(5);
		}
		finally
		{
			_pushing = false;
		}
	}


	private IScreenTheme? GetSelectedTheme()
	{
		return GetSelectedThemeDefinition()?.Theme;
	}

	private void CodexInstallButton_OnClick(object sender, RoutedEventArgs e)
	{
		try
		{
			_codexSetupService.StartInstaller();
			AiSourceStatusText.Text = "已打开官方安装窗口，完成后点击“检查状态”";
		}
		catch (Exception ex)
		{
			AiSourceStatusText.Text = "无法启动 Codex 安装：" + ex.Message;
		}
	}

	private void CodexLoginButton_OnClick(object sender, RoutedEventArgs e)
	{
		try
		{
			_codexSetupService.StartLogin();
			AiSourceStatusText.Text = "已打开 Codex 登录窗口，完成授权后点击“检查状态”";
		}
		catch (Exception ex)
		{
			AiSourceStatusText.Text = "无法启动 Codex 登录：" + ex.Message;
		}
	}

	private async void CodexStatusButton_OnClick(object sender, RoutedEventArgs e)
	{
		await ReadCodexQuotaAsync(force: true);
		await RefreshPreviewAsync();
	}

	private void CodexOpenConfigButton_OnClick(object sender, RoutedEventArgs e)
	{
		try
		{
			_codexSetupService.OpenConfig();
			AiSourceStatusText.Text = "已打开本机 Codex 配置";
		}
		catch (Exception ex)
		{
			AiSourceStatusText.Text = "无法打开 Codex 配置：" + ex.Message;
		}
	}

	private void CodexExportConfigButton_OnClick(object sender, RoutedEventArgs e)
	{
		var dialog = new Microsoft.Win32.SaveFileDialog
		{
			Title = "导出可迁移的 Codex 配置",
			Filter = "Codex 配置 (*.toml)|*.toml",
			FileName = "codex-config.toml",
			AddExtension = true
		};
		if (dialog.ShowDialog(this) != true)
		{
			return;
		}

		try
		{
			_codexSetupService.ExportConfig(dialog.FileName);
			AiSourceStatusText.Text = "已导出 config.toml；新电脑导入后仍需单独登录";
		}
		catch (Exception ex)
		{
			AiSourceStatusText.Text = "导出配置失败：" + ex.Message;
		}
	}

	private void CodexImportConfigButton_OnClick(object sender, RoutedEventArgs e)
	{
		var dialog = new Microsoft.Win32.OpenFileDialog
		{
			Title = "导入 Codex 配置",
			Filter = "Codex 配置 (*.toml)|*.toml",
			CheckFileExists = true,
			Multiselect = false
		};
		if (dialog.ShowDialog(this) != true ||
			System.Windows.MessageBox.Show(this, "将覆盖本机 config.toml，并自动保留备份。登录信息不会导入。是否继续？", "导入 Codex 配置", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
		{
			return;
		}

		try
		{
			string backupPath = _codexSetupService.ImportConfig(dialog.FileName);
			AiSourceStatusText.Text = string.IsNullOrEmpty(backupPath)
				? "配置已导入；请在本机单独登录 Codex"
				: "配置已导入，原配置已备份；请在本机单独登录 Codex";
		}
		catch (Exception ex)
		{
			AiSourceStatusText.Text = "导入配置失败：" + ex.Message;
		}
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
		CurrentThemeDescription.Text = screenTheme.Description;
		CurrentThemeDetailsText.Text = screenTheme.Details;
		UpdateContextualDataCards(definition);
		if (_loaded)
		{
			InteractionMotion.Reveal(CurrentThemeNameText, 4.0, 0.996);
			InteractionMotion.Reveal(CurrentThemeDescription, 4.0, 0.996);
			InteractionMotion.Reveal(CurrentThemeDetailsText, 4.0, 0.996);
		}
		if (_loaded && !_suppressThemeRefresh)
		{
			if (_settings.AutoSwitchToMusic && definition.Category != ThemeCategory.Music)
			{
				_settings.AutoSwitchToMusic = false;
				_updatingAutomation = true;
				try
				{
					_automationViewModel.AutoSwitchToMusic = false;
				}
				finally
				{
					_updatingAutomation = false;
				}
			}
			await ShowOneTimeFeatureNoticeAsync(id);
			_settings.SelectedThemeId = id;
			await CommitAndPushAsync(preferCachedPreview: true);
		}
	}

	private async Task ShowOneTimeFeatureNoticeAsync(string themeId)
	{
		FeatureNoticeWindow? notice = null;
		if (themeId == "ai-quota" && ReadAiSourceKind() == AiQuotaSourceKind.OpenAICodex && !_settings.HasAcknowledgedCodexNotice)
		{
			notice = FeatureNoticeWindow.CreateCodexNotice();
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
		if (ReadAiSourceKind() == AiQuotaSourceKind.OpenAICodex)
		{
			_settings.HasAcknowledgedCodexNotice = true;
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
		Dispatcher.BeginInvoke(DispatcherPriority.Loaded, ThemeScrollViewer.ScrollToTop);
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

	private async void TestConnectionButton_OnClick(object sender, RoutedEventArgs e)
	{
		await PushLatestAsync(force: true);
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
		return new ScreenDisplayOptions(_settings.ImageTimePlacement, _settings.ScreenColorMode);
	}

	private System.Windows.Media.Color GetAccentColor()
	{
		if (!AppearanceViewModel.TryParseAccentColor(_appearanceViewModel.AccentColor, out var color))
		{
			return System.Windows.Media.Color.FromRgb(228, 105, 76);
		}
		return color;
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

}
