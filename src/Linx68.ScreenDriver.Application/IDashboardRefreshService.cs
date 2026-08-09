using Linx68.ScreenDriver.Core;

namespace Linx68.ScreenDriver.Application;

public interface IDashboardRefreshService
{
    Task<DashboardRefreshResult> RefreshAsync(
        DashboardRefreshRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record DashboardRefreshRequest(
    IReadOnlyList<ThemeDefinition> Themes,
    AppSettings Settings,
    string? SelectedThemeId,
    string? PreviousEffectiveThemeId,
    Func<CancellationToken, Task<AiQuotaSnapshot?>>? ReadAiQuotaAsync = null,
    Func<CancellationToken, Task<CodexTaskSnapshot?>>? ReadCodexTasksAsync = null);

public sealed record DashboardRefreshResult(
    ThemeDefinition EffectiveTheme,
    SystemSnapshot Snapshot,
    MusicSnapshot SourceMusic,
    bool EffectiveThemeChanged,
    bool UsedAutomaticWeatherLocationFallback,
    AutomaticWeatherLocationResult? WeatherLocation = null,
    DashboardRefreshTimings? Timings = null);

public sealed record DashboardRefreshTimings(
    TimeSpan MusicSession,
    TimeSpan AiQuota,
    TimeSpan CodexTasks,
    TimeSpan WeatherLocation,
    TimeSpan SnapshotBuild,
    TimeSpan Total);
