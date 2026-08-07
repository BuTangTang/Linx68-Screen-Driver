using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using Linx68.ScreenDriver.Application;
using Linx68.ScreenDriver.Core;

namespace Linx68.ScreenDriver.Infrastructure;

public sealed partial class LrcLibLyricsSnapshotSource : ILyricsSnapshotSource, IDisposable
{
	private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(12);
	private readonly HttpClient _client;
	private readonly bool _ownsClient;
	private readonly Dictionary<string, CacheEntry> _cache = new(StringComparer.OrdinalIgnoreCase);

	public LrcLibLyricsSnapshotSource(HttpClient? client = null)
	{
		_ownsClient = client is null;
		_client = client ?? new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
		_client.DefaultRequestHeaders.UserAgent.ParseAdd("Linx68ScreenDriver/1.0 (based on https://github.com/zcat95/Keyboard-Screen-Studio)");
	}

	public async Task<LyricsSnapshot> ReadAsync(MusicSnapshot music, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(music);
		if (!music.Available || string.IsNullOrWhiteSpace(music.Title))
		{
			return LyricsSnapshot.Unavailable;
		}

		string key = $"{music.Title.Trim()}\n{music.Artist.Trim()}\n{Math.Round(music.Duration.TotalSeconds)}";
		if (_cache.TryGetValue(key, out CacheEntry? cached) && DateTimeOffset.Now < cached.ExpiresAt)
		{
			return cached.Snapshot;
		}

		try
		{
			string uri = "https://lrclib.net/api/search?track_name=" + Uri.EscapeDataString(music.Title.Trim());
			if (!string.IsNullOrWhiteSpace(music.Artist))
			{
				uri += "&artist_name=" + Uri.EscapeDataString(music.Artist.Trim());
			}

			using HttpResponseMessage response = await _client.GetAsync(uri, cancellationToken);
			if (response.StatusCode == HttpStatusCode.TooManyRequests)
			{
				return Cache(key, new LyricsSnapshot(false, [], "LRCLIB 请求过于频繁"), TimeSpan.FromMinutes(1));
			}
			response.EnsureSuccessStatusCode();
			await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
			using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
			LyricsSnapshot snapshot = SelectBest(document.RootElement, music);
			return Cache(key, snapshot, snapshot.Available ? CacheDuration : TimeSpan.FromHours(1));
		}
		catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
		{
			return Cache(key, new LyricsSnapshot(false, [], ex.Message), TimeSpan.FromMinutes(1));
		}
	}

	private LyricsSnapshot Cache(string key, LyricsSnapshot snapshot, TimeSpan duration)
	{
		_cache[key] = new CacheEntry(snapshot, DateTimeOffset.Now + duration);
		return snapshot;
	}

	private static LyricsSnapshot SelectBest(JsonElement root, MusicSnapshot music)
	{
		if (root.ValueKind != JsonValueKind.Array)
		{
			return LyricsSnapshot.Unavailable;
		}

		JsonElement[] matches = root.EnumerateArray()
			.Where(item => item.TryGetProperty("syncedLyrics", out JsonElement lyrics)
				&& !string.IsNullOrWhiteSpace(lyrics.GetString()))
			.OrderBy(item => MatchScore(item, music))
			.ToArray();
		if (matches.Length == 0)
		{
			return LyricsSnapshot.Unavailable;
		}

		string? value = matches[0].GetProperty("syncedLyrics").GetString();
		IReadOnlyList<LyricLine> lines = ParseSyncedLyrics(value);
		return lines.Count == 0 ? LyricsSnapshot.Unavailable : new LyricsSnapshot(true, lines);
	}

	private static double MatchScore(JsonElement item, MusicSnapshot music)
	{
		double score = 0;
		string track = item.TryGetProperty("trackName", out JsonElement trackName) ? trackName.GetString() ?? "" : "";
		string artist = item.TryGetProperty("artistName", out JsonElement artistName) ? artistName.GetString() ?? "" : "";
		if (!string.Equals(track.Trim(), music.Title.Trim(), StringComparison.OrdinalIgnoreCase)) score += 30;
		if (!string.IsNullOrWhiteSpace(music.Artist)
			&& (string.IsNullOrWhiteSpace(artist)
				|| (!artist.Contains(music.Artist.Trim(), StringComparison.OrdinalIgnoreCase)
					&& !music.Artist.Contains(artist.Trim(), StringComparison.OrdinalIgnoreCase)))) score += 20;
		if (music.Duration.TotalSeconds > 0
			&& item.TryGetProperty("duration", out JsonElement duration)
			&& duration.TryGetDouble(out double seconds)) score += Math.Abs(seconds - music.Duration.TotalSeconds);
		return score;
	}

	public static IReadOnlyList<LyricLine> ParseSyncedLyrics(string? value)
	{
		if (string.IsNullOrWhiteSpace(value)) return [];
		var lines = new List<LyricLine>();
		foreach (string rawLine in value.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
		{
			Match match = TimestampPattern().Match(rawLine);
			if (!match.Success || string.IsNullOrWhiteSpace(match.Groups[4].Value)) continue;
			if (!int.TryParse(match.Groups[1].Value, out int minutes)
				|| !int.TryParse(match.Groups[2].Value, out int seconds)) continue;
			string fraction = match.Groups[3].Value.PadRight(3, '0')[..3];
			if (!int.TryParse(fraction, NumberStyles.None, CultureInfo.InvariantCulture, out int milliseconds)) continue;
			lines.Add(new LyricLine(
				TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds) + TimeSpan.FromMilliseconds(milliseconds),
				match.Groups[4].Value.Trim()));
		}
		return lines.OrderBy(line => line.Timestamp).ToArray();
	}

	public void Dispose()
	{
		if (_ownsClient) _client.Dispose();
	}

	[GeneratedRegex(@"^\[(\d{1,3}):(\d{2})(?:\.(\d{1,3}))?\]\s*(.+)$", RegexOptions.CultureInvariant)]
	private static partial Regex TimestampPattern();

	private sealed record CacheEntry(LyricsSnapshot Snapshot, DateTimeOffset ExpiresAt);
}
