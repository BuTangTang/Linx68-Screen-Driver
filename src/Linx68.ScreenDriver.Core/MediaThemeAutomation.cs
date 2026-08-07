namespace Linx68.ScreenDriver.Core;

public static class MediaThemeAutomation
{
    private static readonly HashSet<string> MusicThemeIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "music",
		"music-vinyl",
		"music-cassette",
        "music-minimal",
        "music-poster"
    };

    public static bool IsMusicThemeId(string? themeId) =>
        !string.IsNullOrWhiteSpace(themeId) && MusicThemeIds.Contains(themeId);

    public static string ResolveThemeId(AppSettings settings, bool isPlaying, string? selectedThemeId)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (settings.AutoMediaThemeSwitch)
        {
            if (isPlaying)
            {
                return IsMusicThemeId(settings.MediaPlayingThemeId)
                    ? settings.MediaPlayingThemeId
                    : "music";
            }

            return !string.IsNullOrWhiteSpace(settings.MediaIdleThemeId) &&
                   !IsMusicThemeId(settings.MediaIdleThemeId)
                ? settings.MediaIdleThemeId
                : "system";
        }

        if (settings.AutoSwitchToMusic && isPlaying)
        {
            return "music";
        }

        return string.IsNullOrWhiteSpace(selectedThemeId) ? "system" : selectedThemeId;
    }
}
