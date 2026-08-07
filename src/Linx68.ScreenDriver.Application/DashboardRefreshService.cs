using Linx68.ScreenDriver.Core;

namespace Linx68.ScreenDriver.Application;

public sealed class DashboardRefreshService(
    IMusicSnapshotSource musicSource,
    IDashboardSnapshotBuilder snapshotBuilder,
    IWeatherSettingsResolver weatherSettingsResolver) : IDashboardRefreshService
{
    public async Task<DashboardRefreshResult> RefreshAsync(
        DashboardRefreshRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Themes);
        ArgumentNullException.ThrowIfNull(request.Settings);
        if (request.Themes.Count == 0)
        {
            throw new ArgumentException("At least one theme is required.", nameof(request));
        }

        MusicSnapshot sourceMusic = await musicSource.ReadAsync(cancellationToken);
        ThemeDefinition selectedTheme = FindTheme(request.Themes, request.SelectedThemeId)
            ?? request.Themes[0];
        bool mediaIsPlaying = sourceMusic.Available && sourceMusic.IsPlaying;
        string effectiveThemeId = MediaThemeAutomation.ResolveThemeId(
            request.Settings,
            mediaIsPlaying,
            selectedTheme.Id,
            id => FindTheme(request.Themes, id)?.Category == ThemeCategory.Music);
        ThemeDefinition effectiveTheme = FindTheme(request.Themes, effectiveThemeId)
            ?? FindTheme(request.Themes, mediaIsPlaying ? "music" : "system")
            ?? request.Themes[0];

        bool effectiveThemeChanged = request.Settings.AutoMediaThemeSwitch
            && request.PreviousEffectiveThemeId is not null
            && !string.Equals(
                request.PreviousEffectiveThemeId,
                effectiveTheme.Id,
                StringComparison.OrdinalIgnoreCase);

        AiQuotaSnapshot? aiQuota = effectiveTheme.Requires(ThemeDataRequirements.AiQuota)
            && request.ReadAiQuotaAsync is not null
                ? await request.ReadAiQuotaAsync(cancellationToken)
                : null;

        WeatherSettingsResolution? weatherResolution = null;
        if (effectiveTheme.Requires(ThemeDataRequirements.Weather))
        {
            weatherResolution = await weatherSettingsResolver.ResolveAsync(
                request.Settings.Weather ?? new WeatherSettings(),
                cancellationToken);
        }

        SystemSnapshot snapshot = await snapshotBuilder.BuildAsync(
            effectiveTheme,
            request.Settings,
            sourceMusic,
            weatherResolution?.Settings,
            aiQuota,
            cancellationToken);

        return new DashboardRefreshResult(
            effectiveTheme,
            snapshot,
            sourceMusic,
            effectiveThemeChanged,
            weatherResolution?.UsedAutomaticLocationFallback == true);
    }

    private static ThemeDefinition? FindTheme(
        IReadOnlyList<ThemeDefinition> themes,
        string? id) => themes.FirstOrDefault(theme =>
            string.Equals(theme.Id, id, StringComparison.OrdinalIgnoreCase));
}
