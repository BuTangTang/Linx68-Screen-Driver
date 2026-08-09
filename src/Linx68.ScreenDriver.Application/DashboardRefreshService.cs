using Linx68.ScreenDriver.Core;
using System.Diagnostics;

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

        var totalStopwatch = Stopwatch.StartNew();
        var musicStopwatch = Stopwatch.StartNew();
        MusicSnapshot sourceMusic = await musicSource.ReadAsync(cancellationToken);
        musicStopwatch.Stop();
        ThemeDefinition selectedTheme = FindTheme(request.Themes, BuiltInThemes.NormalizeThemeId(request.SelectedThemeId))
            ?? request.Themes[0];
        bool mediaIsPlaying = sourceMusic.Available && sourceMusic.IsPlaying;
        ThemeDefinition effectiveTheme = request.Settings.AutoSwitchToMusic
            && mediaIsPlaying
            && selectedTheme.Category != ThemeCategory.Music
            ? FindTheme(request.Themes, "music") ?? selectedTheme
            : selectedTheme;

        bool effectiveThemeChanged = request.Settings.AutoSwitchToMusic
            && request.PreviousEffectiveThemeId is not null
            && !string.Equals(
                request.PreviousEffectiveThemeId,
                effectiveTheme.Id,
                StringComparison.OrdinalIgnoreCase);

        Task<TimedResult<AiQuotaSnapshot?>> aiQuotaTask = effectiveTheme.Requires(ThemeDataRequirements.AiQuota)
            && request.ReadAiQuotaAsync is not null
                ? MeasureAsync(() => request.ReadAiQuotaAsync(cancellationToken))
                : Task.FromResult(new TimedResult<AiQuotaSnapshot?>(null, TimeSpan.Zero));
        Task<TimedResult<CodexTaskSnapshot?>> codexTasksTask = effectiveTheme.Requires(ThemeDataRequirements.CodexTasks)
            && request.ReadCodexTasksAsync is not null
                ? MeasureAsync(() => request.ReadCodexTasksAsync(cancellationToken))
                : Task.FromResult(new TimedResult<CodexTaskSnapshot?>(null, TimeSpan.Zero));
        Task<TimedResult<WeatherSettingsResolution?>> weatherResolutionTask = effectiveTheme.Requires(ThemeDataRequirements.Weather)
            ? MeasureAsync(() => ResolveWeatherAsync(request.Settings.Weather ?? new WeatherSettings(), cancellationToken))
            : Task.FromResult(new TimedResult<WeatherSettingsResolution?>(null, TimeSpan.Zero));

        await Task.WhenAll(aiQuotaTask, codexTasksTask, weatherResolutionTask);
        TimedResult<AiQuotaSnapshot?> aiQuotaResult = await aiQuotaTask;
        TimedResult<CodexTaskSnapshot?> codexTasksResult = await codexTasksTask;
        TimedResult<WeatherSettingsResolution?> weatherResolutionResult = await weatherResolutionTask;
        AiQuotaSnapshot? aiQuota = aiQuotaResult.Value;
        CodexTaskSnapshot? codexTasks = codexTasksResult.Value;
        WeatherSettingsResolution? weatherResolution = weatherResolutionResult.Value;

        var buildStopwatch = Stopwatch.StartNew();
        SystemSnapshot snapshot = await snapshotBuilder.BuildAsync(
            effectiveTheme,
            request.Settings,
            sourceMusic,
            weatherResolution?.Settings,
            aiQuota,
            codexTasks,
            cancellationToken);
        buildStopwatch.Stop();
        totalStopwatch.Stop();

        return new DashboardRefreshResult(
            effectiveTheme,
            snapshot,
            sourceMusic,
            effectiveThemeChanged,
            weatherResolution?.UsedAutomaticLocationFallback == true,
            weatherResolution?.LocationResult,
            new DashboardRefreshTimings(
                musicStopwatch.Elapsed,
                aiQuotaResult.Duration,
                codexTasksResult.Duration,
                weatherResolutionResult.Duration,
                buildStopwatch.Elapsed,
                totalStopwatch.Elapsed));
    }

    private static ThemeDefinition? FindTheme(
        IReadOnlyList<ThemeDefinition> themes,
        string? id) => themes.FirstOrDefault(theme =>
            string.Equals(theme.Id, id, StringComparison.OrdinalIgnoreCase));

    private async Task<WeatherSettingsResolution?> ResolveWeatherAsync(
        WeatherSettings settings,
        CancellationToken cancellationToken) =>
        await weatherSettingsResolver.ResolveAsync(settings, cancellationToken);

    private static async Task<TimedResult<T>> MeasureAsync<T>(Func<Task<T>> action)
    {
        var stopwatch = Stopwatch.StartNew();
        T value = await action();
        stopwatch.Stop();
        return new TimedResult<T>(value, stopwatch.Elapsed);
    }

    private sealed record TimedResult<T>(T Value, TimeSpan Duration);
}
