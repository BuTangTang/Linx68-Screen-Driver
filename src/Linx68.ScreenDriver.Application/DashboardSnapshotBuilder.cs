using Linx68.ScreenDriver.Core;

namespace Linx68.ScreenDriver.Application;

public sealed class DashboardSnapshotBuilder(
    ISystemSnapshotSource systemSource,
    ILyricsSnapshotSource lyricsSource,
    IWeatherSnapshotSource weatherSource,
    IMusicSnapshotEnricher? musicEnricher = null) : IDashboardSnapshotBuilder
{
    public async Task<SystemSnapshot> BuildAsync(
        ThemeDefinition theme,
        AppSettings settings,
        MusicSnapshot music,
        WeatherSettings? effectiveWeatherSettings,
        AiQuotaSnapshot? aiQuota,
        CodexTaskSnapshot? codexTasks = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(music);

        Task<SystemSnapshot> systemTask = systemSource.ReadAsync(cancellationToken).AsTask();
        Task<MusicSnapshot> musicTask = BuildMusicAsync(theme, settings, music, cancellationToken);
        Task<WeatherSnapshot?> weatherTask = theme.Requires(ThemeDataRequirements.Weather)
            && effectiveWeatherSettings is not null
                ? ReadWeatherAsync(effectiveWeatherSettings, cancellationToken)
                : Task.FromResult<WeatherSnapshot?>(null);

        await Task.WhenAll(systemTask, musicTask, weatherTask);
        SystemSnapshot system = await systemTask;
        MusicSnapshot effectiveMusic = await musicTask;
        WeatherSnapshot? weather = await weatherTask;
        return system with
        {
            Music = effectiveMusic,
            AiQuota = aiQuota,
            Weather = weather,
            CodexTasks = codexTasks
        };
    }

    private async Task<MusicSnapshot> BuildMusicAsync(
        ThemeDefinition theme,
        AppSettings settings,
        MusicSnapshot music,
        CancellationToken cancellationToken)
    {
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

        return effectiveMusic;
    }

    private async Task<WeatherSnapshot?> ReadWeatherAsync(
        WeatherSettings settings,
        CancellationToken cancellationToken) =>
        await weatherSource.ReadAsync(settings, cancellationToken);
}
