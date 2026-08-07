namespace Linx68.ScreenDriver.Core;

public enum AppearanceMode
{
	System,
	Light,
	Dark
}

public sealed class AppSettings
{
	public const int CurrentSettingsVersion = 2;

	public int SettingsVersion { get; set; } = CurrentSettingsVersion;

	public AppearanceMode AppearanceMode { get; set; } = AppearanceMode.System;

	public string DeviceEndpoint { get; set; } = string.Empty;


	public string SelectedThemeId { get; set; } = "clock-dot-matrix";


	public bool AutoPush { get; set; } = true;

	public int RefreshSeconds { get; set; } = 1;




	public string AccentColor { get; set; } = "#E4694C";


	public string SelectedFontId { get; set; } = "builtin:segoe-variable-display";


	public bool MinimizeToTray { get; set; } = true;


	public bool CloseToTray { get; set; } = true;


	public bool StartMinimized { get; set; }

	public bool LaunchAtStartup { get; set; }

	public bool HasCompletedOnboarding { get; set; }

	public bool HasAcknowledgedStockNotice { get; set; }

	public bool HasAcknowledgedMiMoNotice { get; set; }

	public bool AutoSwitchToMusic { get; set; }

	public bool AutoMediaThemeSwitch { get; set; }

	public string MediaPlayingThemeId { get; set; } = "music";

	public string MediaIdleThemeId { get; set; } = "system";

	public MusicSettings Music { get; set; } = new();

	public string? ImagePath { get; set; }

	public ScreenInsets SafeArea { get; set; } = new ScreenInsets(10, 52, 10, 12);

	public AiQuotaSettings AiQuota { get; set; } = new();

	public WeatherSettings Weather { get; set; } = new();

	public StockSettings Stocks { get; set; } = new();





	public ImageTimePlacement ImageTimePlacement { get; set; } = ImageTimePlacement.Bottom;
}
