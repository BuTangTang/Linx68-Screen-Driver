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

    public static string ResolveThemeId(
        AppSettings settings,
        bool isPlaying,
        string? selectedThemeId,
        Func<string?, bool>? isMusicTheme = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        isMusicTheme ??= IsMusicThemeId;

        if (settings.AutoMediaThemeSwitch)
        {
            if (isPlaying)
            {
                return isMusicTheme(settings.MediaPlayingThemeId)
                    ? settings.MediaPlayingThemeId
                    : "music";
            }

            return !string.IsNullOrWhiteSpace(settings.MediaIdleThemeId) &&
                   !isMusicTheme(settings.MediaIdleThemeId)
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
