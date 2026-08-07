using Linx68.ScreenDriver.Core;

namespace Linx68.ScreenDriver.Application;

public sealed class DashboardSnapshotBuilder(
    ISystemSnapshotSource systemSource,
    ILyricsSnapshotSource lyricsSource,
    IWeatherSnapshotSource weatherSource,
    IStockSnapshotSource stockSource,
    IMusicSnapshotEnricher? musicEnricher = null) : IDashboardSnapshotBuilder
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
        if (theme.Requires(ThemeDataRequirements.Music) && music.Available && musicEnricher is not null)
        {
            effectiveMusic = await musicEnricher.EnrichAsync(effectiveMusic, cancellationToken);
        }
        if (theme.Requires(ThemeDataRequirements.Lyrics)
            && settings.Music?.EnableOnlineLyrics == true
            && effectiveMusic.Available)
        {
            effectiveMusic = effectiveMusic with
            {
                Lyrics = await lyricsSource.ReadAsync(effectiveMusic, cancellationToken)
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
