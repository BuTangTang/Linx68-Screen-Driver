using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using Linx68.ScreenDriver.Application;
using Linx68.ScreenDriver.Core;

namespace Linx68.ScreenDriver.Infrastructure;

/// <summary>
/// Reads timed lyrics from NetEase for an already resolved NetEase song, then falls back to LRCLIB.
/// </summary>
public sealed class NetEaseLyricsSnapshotSource : ILyricsSnapshotSource, IDisposable
{
    private readonly ILyricsSnapshotSource _fallback;
    private readonly HttpClient _client;
    private readonly bool _ownsClient;
    private readonly Dictionary<long, CacheEntry> _cache = [];

    public NetEaseLyricsSnapshotSource(LrcLibLyricsSnapshotSource fallback, HttpClient? client = null)
    {
        _fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));
        _ownsClient = client is null;
        _client = client ?? new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("Linx68ScreenDriver/1.0");
        _client.DefaultRequestHeaders.Referrer = new Uri("https://music.163.com/");
    }

    public async Task<LyricsSnapshot> ReadAsync(MusicSnapshot music, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(music);
        if (!WindowsMusicSessionSelector.IsNetEase(music.SourceAppId) || music.ProviderTrackId is not long trackId)
        {
            return await _fallback.ReadAsync(music, cancellationToken);
        }
        if (_cache.TryGetValue(trackId, out CacheEntry? cached) && DateTimeOffset.Now < cached.ExpiresAt)
        {
            return cached.Snapshot.Available
                ? cached.Snapshot
                : await _fallback.ReadAsync(music, cancellationToken);
        }

        try
        {
            string uri = $"https://music.163.com/api/song/lyric?id={trackId}&lv=-1&kv=-1&tv=-1";
            using HttpResponseMessage response = await _client.GetAsync(uri, cancellationToken);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                _cache[trackId] = new CacheEntry(LyricsSnapshot.Unavailable, DateTimeOffset.Now + TimeSpan.FromMinutes(1));
                return await _fallback.ReadAsync(music, cancellationToken);
            }
            response.EnsureSuccessStatusCode();
            await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            string? lyrics = document.RootElement.TryGetProperty("lrc", out JsonElement lrc)
                && lrc.TryGetProperty("lyric", out JsonElement value)
                    ? value.GetString()
                    : null;
            IReadOnlyList<LyricLine> lines = LrcLibLyricsSnapshotSource.ParseSyncedLyrics(lyrics);
            var snapshot = lines.Count == 0 ? LyricsSnapshot.Unavailable : new LyricsSnapshot(true, lines);
            _cache[trackId] = new CacheEntry(snapshot, DateTimeOffset.Now + (snapshot.Available ? TimeSpan.FromHours(12) : TimeSpan.FromHours(1)));
            return snapshot.Available ? snapshot : await _fallback.ReadAsync(music, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _cache[trackId] = new CacheEntry(LyricsSnapshot.Unavailable, DateTimeOffset.Now + TimeSpan.FromMinutes(1));
            return await _fallback.ReadAsync(music, cancellationToken);
        }
    }

    public void Dispose()
    {
        if (_ownsClient) _client.Dispose();
    }

    private sealed record CacheEntry(LyricsSnapshot Snapshot, DateTimeOffset ExpiresAt);
}
