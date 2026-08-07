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
    Func<CancellationToken, Task<AiQuotaSnapshot?>>? ReadAiQuotaAsync = null);

public sealed record DashboardRefreshResult(
    ThemeDefinition EffectiveTheme,
    SystemSnapshot Snapshot,
    MusicSnapshot SourceMusic,
    bool EffectiveThemeChanged,
    bool UsedAutomaticWeatherLocationFallback);
