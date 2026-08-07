using Linx68.ScreenDriver.Core;

namespace Linx68.ScreenDriver.Application;

public sealed class DashboardSnapshotBuilder(
    ISystemSnapshotSource systemSource,
    ILyricsSnapshotSource lyricsSource,
    IWeatherSnapshotSource weatherSource,
    IStockSnapshotSource stockSource) : IDashboardSnapshotBuilder
{
    public async Task<SystemSnapshot> BuildAsync(
        ThemeDefinition theme,
        AppSettings settings,
        MusicSnapshot music,
        WeatherSettings? effectiveWeatherSettings,
        AiQuotaSnapshot? aiQuota,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(music);

        SystemSnapshot system = await systemSource.ReadAsync(cancellationToken);
        MusicSnapshot effectiveMusic = music;
        if (theme.Requires(ThemeDataRequirements.Lyrics)
            && settings.Music?.EnableOnlineLyrics == true
            && music.Available)
        {
            effectiveMusic = music with
            {
                Lyrics = await lyricsSource.ReadAsync(music, cancellationToken)
            };
        }

        WeatherSnapshot? weather = theme.Requires(ThemeDataRequirements.Weather)
            && effectiveWeatherSettings is not null
                ? await weatherSource.ReadAsync(effectiveWeatherSettings, cancellationToken)
                : null;
        StockSnapshot? stocks = theme.Requires(ThemeDataRequirements.Stocks)
            ? await stockSource.ReadAsync(settings.Stocks ?? new StockSettings(), cancellationToken)
            : null;

        return system with
        {
            Music = effectiveMusic,
            AiQuota = aiQuota,
            Weather = weather,
            Stocks = stocks
        };
    }
}
