using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using Linx68.ScreenDriver.Application;
using Linx68.ScreenDriver.Core;

namespace Linx68.ScreenDriver.Infrastructure;

/// <summary>
/// Resolves a NetEase media-session track to its canonical song metadata and song ID.
/// The ID is subsequently used to request the provider's timed lyrics.
/// </summary>
public sealed class NetEaseMusicSnapshotEnricher : IMusicSnapshotEnricher, IDisposable
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(12);
    private readonly HttpClient _client;
    private readonly bool _ownsClient;
    private readonly Dictionary<string, CacheEntry> _cache = new(StringComparer.OrdinalIgnoreCase);

    public NetEaseMusicSnapshotEnricher(HttpClient? client = null)
    {
        _ownsClient = client is null;
        _client = client ?? new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("Linx68ScreenDriver/1.0");
        _client.DefaultRequestHeaders.Referrer = new Uri("https://music.163.com/");
    }

    public async Task<MusicSnapshot> EnrichAsync(MusicSnapshot music, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(music);
        if (!music.Available || !WindowsMusicSessionSelector.IsNetEase(music.SourceAppId)
            || string.IsNullOrWhiteSpace(music.Title) || music.ProviderTrackId is not null)
        {
            return music;
        }

        string key = $"{music.Title.Trim()}\n{music.Artist.Trim()}\n{Math.Round(music.Duration.TotalSeconds)}";
        if (_cache.TryGetValue(key, out CacheEntry? cached) && DateTimeOffset.Now < cached.ExpiresAt)
        {
            return Apply(music, cached.Match);
        }

        try
        {
            string uri = "https://music.163.com/api/search/get/web?csrf_token=&s="
                + Uri.EscapeDataString(music.Title.Trim())
                + "&type=1&offset=0&total=true&limit=10";
            using HttpResponseMessage response = await _client.GetAsync(uri, cancellationToken);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                _cache[key] = new CacheEntry(null, DateTimeOffset.Now + TimeSpan.FromMinutes(1));
                return music;
            }
            response.EnsureSuccessStatusCode();
            await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            SongMatch? match = SelectBest(document.RootElement, music);
            _cache[key] = new CacheEntry(match, DateTimeOffset.Now + (match is null ? TimeSpan.FromMinutes(10) : CacheDuration));
            return Apply(music, match);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _cache[key] = new CacheEntry(null, DateTimeOffset.Now + TimeSpan.FromMinutes(1));
            return music;
        }
    }

    private static SongMatch? SelectBest(JsonElement root, MusicSnapshot music)
    {
        if (!root.TryGetProperty("result", out JsonElement result)
            || !result.TryGetProperty("songs", out JsonElement songs)
            || songs.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        SongMatch? best = songs.EnumerateArray()
            .Select(ParseSong)
            .Where(song => song is not null)
            .Cast<SongMatch>()
            .OrderBy(song => Score(song, music))
            .FirstOrDefault();
        return best is not null && Score(best, music) <= 30 ? best : null;
    }

    private static SongMatch? ParseSong(JsonElement song)
    {
        if (!song.TryGetProperty("id", out JsonElement id) || !id.TryGetInt64(out long trackId)
            || !song.TryGetProperty("name", out JsonElement name) || string.IsNullOrWhiteSpace(name.GetString()))
        {
            return null;
        }

        string artist = song.TryGetProperty("artists", out JsonElement artists) && artists.ValueKind == JsonValueKind.Array
            ? string.Join(" / ", artists.EnumerateArray()
                .Select(item => item.TryGetProperty("name", out JsonElement artistName) ? artistName.GetString() : null)
                .Where(value => !string.IsNullOrWhiteSpace(value)))
            : string.Empty;
        string album = song.TryGetProperty("album", out JsonElement albumObject)
            && albumObject.TryGetProperty("name", out JsonElement albumName)
                ? albumName.GetString() ?? string.Empty
                : string.Empty;
        TimeSpan duration = song.TryGetProperty("duration", out JsonElement durationValue)
            && durationValue.TryGetInt64(out long milliseconds)
                ? TimeSpan.FromMilliseconds(milliseconds)
                : TimeSpan.Zero;
        return new SongMatch(trackId, name.GetString()!, artist, album, duration);
    }

    private static double Score(SongMatch song, MusicSnapshot music)
    {
        double score = NormalizedEquals(song.Title, music.Title) ? 0 : 100;
        if (!string.IsNullOrWhiteSpace(music.Artist))
        {
            score += NormalizedContains(song.Artist, music.Artist) ? 0 : 25;
        }
        if (music.Duration > TimeSpan.Zero && song.Duration > TimeSpan.Zero)
        {
            score += Math.Min(25, Math.Abs((song.Duration - music.Duration).TotalSeconds));
        }
        return score;
    }

    private static MusicSnapshot Apply(MusicSnapshot music, SongMatch? match) => match is null
        ? music
        : music with
        {
            Title = match.Title,
            Artist = string.IsNullOrWhiteSpace(match.Artist) ? music.Artist : match.Artist,
            AlbumTitle = string.IsNullOrWhiteSpace(match.AlbumTitle) ? music.AlbumTitle : match.AlbumTitle,
            Duration = match.Duration > TimeSpan.Zero ? match.Duration : music.Duration,
            ProviderTrackId = match.Id
        };

    private static bool NormalizedEquals(string left, string right) =>
        string.Equals(Normalize(left), Normalize(right), StringComparison.OrdinalIgnoreCase);

    private static bool NormalizedContains(string left, string right)
    {
        string normalizedLeft = Normalize(left);
        string normalizedRight = Normalize(right);
        if (normalizedLeft.Length == 0 || normalizedRight.Length == 0)
        {
            return false;
        }
        return normalizedLeft.Contains(normalizedRight, StringComparison.OrdinalIgnoreCase)
            || normalizedRight.Contains(normalizedLeft, StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string value) => new(value
        .Where(character => char.IsLetterOrDigit(character))
        .Select(char.ToLowerInvariant)
        .ToArray());

    public void Dispose()
    {
        if (_ownsClient) _client.Dispose();
    }

    private sealed record SongMatch(long Id, string Title, string Artist, string AlbumTitle, TimeSpan Duration);
    private sealed record CacheEntry(SongMatch? Match, DateTimeOffset ExpiresAt);
}
