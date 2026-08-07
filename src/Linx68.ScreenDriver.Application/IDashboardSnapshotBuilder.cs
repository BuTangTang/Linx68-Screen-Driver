using Linx68.ScreenDriver.Core;

namespace Linx68.ScreenDriver.Application;

public interface IDashboardSnapshotBuilder
{
    Task<SystemSnapshot> BuildAsync(
        ThemeDefinition theme,
        AppSettings settings,
        MusicSnapshot music,
        WeatherSettings? effectiveWeatherSettings,
        AiQuotaSnapshot? aiQuota,
        CancellationToken cancellationToken = default);
}
