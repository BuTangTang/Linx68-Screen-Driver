namespace Linx68.ScreenDriver.Core;

public enum AppearanceMode
{
	System,
	Light,
	Dark
}

public sealed class AppSettings
{
	public const int CurrentSettingsVersion = 10;

	public int SettingsVersion { get; set; } = CurrentSettingsVersion;

	public AppearanceMode AppearanceMode { get; set; } = AppearanceMode.System;

	public string DeviceEndpoint { get; set; } = string.Empty;


	public string SelectedThemeId { get; set; } = "clock-weather";


	public bool AutoPush { get; set; } = true;

	public int RefreshSeconds { get; set; } = 1;




	public string AccentColor { get; set; } = "#E4694C";


	public string SelectedFontId { get; set; } = "builtin:segoe-variable-display";


	public bool MinimizeToTray { get; set; } = true;


	public bool CloseToTray { get; set; } = true;


	public bool StartMinimized { get; set; }

	public bool LaunchAtStartup { get; set; }

	public bool HasCompletedOnboarding { get; set; }

	public bool HasAcknowledgedCodexNotice { get; set; }

	public bool AutoSwitchToMusic { get; set; }

	public MusicSettings Music { get; set; } = new();

	public string? ImagePath { get; set; }

	public ScreenInsets SafeArea { get; set; } = new ScreenInsets(10, 52, 10, 12);

	public AiQuotaSettings AiQuota { get; set; } = new();

	public WeatherSettings Weather { get; set; } = new();

	public ImageTimePlacement ImageTimePlacement { get; set; } = ImageTimePlacement.Bottom;

	public ScreenColorMode ScreenColorMode { get; set; } = ScreenColorMode.Night;
}
