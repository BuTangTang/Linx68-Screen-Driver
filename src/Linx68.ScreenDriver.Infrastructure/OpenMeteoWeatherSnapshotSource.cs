using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using Linx68.ScreenDriver.Application;
using Linx68.ScreenDriver.Core;

namespace Linx68.ScreenDriver.Infrastructure;

public sealed class OpenMeteoWeatherSnapshotSource : IWeatherSnapshotSource, IDisposable
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);
    private readonly HttpClient _client;
    private readonly bool _ownsClient;
	private CacheEntry? _cache;

    public OpenMeteoWeatherSnapshotSource(HttpClient? client = null)
    {
        _ownsClient = client is null;
        _client = client ?? new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("Linx68ScreenDriver/1.0");
    }

    public async Task<WeatherSnapshot> ReadAsync(WeatherSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var query = BuildCacheKey(settings);
        var now = DateTimeOffset.Now;
		CacheEntry? cached = Volatile.Read(ref _cache);
		if (cached is not null
			&& string.Equals(cached.Query, query, StringComparison.OrdinalIgnoreCase)
			&& now - cached.FetchedAt < CacheDuration)
        {
			return cached.Snapshot;
        }

        try
        {
            var location = settings.UseAutomaticLocation
                && settings.Latitude is double latitude
                && settings.Longitude is double longitude
                    ? new LocationResult(
                        string.IsNullOrWhiteSpace(settings.AutomaticLocationName)
                            ? "当前位置"
                            : settings.AutomaticLocationName,
                        latitude,
                        longitude)
                    : await ResolveLocationAsync(
                        string.IsNullOrWhiteSpace(settings.LocationQuery)
                            ? "北京"
                            : settings.LocationQuery.Trim(),
                        cancellationToken);
            var snapshot = await ReadCurrentAsync(location, cancellationToken);
			cancellationToken.ThrowIfCancellationRequested();
			Volatile.Write(ref _cache, new CacheEntry(query, snapshot, DateTimeOffset.Now));
            return snapshot;
        }
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
			cached = Volatile.Read(ref _cache);
			bool cachedLocationMatches = cached is not null
				&& string.Equals(cached.Query, query, StringComparison.OrdinalIgnoreCase);
            return cachedLocationMatches
				? cached!.Snapshot with { IsStale = true, ErrorMessage = ex.Message }
                : WeatherSnapshot.Unavailable(ex.Message);
        }
    }

    private static string BuildCacheKey(WeatherSettings settings)
    {
        if (settings.UseAutomaticLocation
            && settings.Latitude is double latitude
            && settings.Longitude is double longitude)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"auto:{latitude:0.####},{longitude:0.####}");
        }

        return string.IsNullOrWhiteSpace(settings.LocationQuery)
            ? "北京"
            : settings.LocationQuery.Trim();
    }
    private async Task<LocationResult> ResolveLocationAsync(string query, CancellationToken cancellationToken)
    {
		List<LocationCandidate> candidates = await ReadLocationCandidatesAsync(query, cancellationToken);
		bool hasAdministrativeCity = candidates.Any(candidate => IsAdministrativeCityCandidate(query, candidate));
		if (!hasAdministrativeCity && IsUnsuffixedChineseCityQuery(query))
		{
			try
			{
				candidates.AddRange(await ReadLocationCandidatesAsync(query + "市", cancellationToken));
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				throw;
			}
			catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
			{
				// The optional suffix lookup timed out. Keep the original candidates
				// so an otherwise unambiguous city can still resolve.
			}
			catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException)
			{
				// The original candidates remain usable when they identify one clear place.
			}
			hasAdministrativeCity = candidates.Any(candidate => IsAdministrativeCityCandidate(query, candidate));
			int exactCandidateCount = candidates.Count(candidate => NamesMatch(query, candidate.Name));
			if (!hasAdministrativeCity && exactCandidateCount > 1)
			{
				throw new InvalidOperationException($"城市名称存在多个候选，请输入“{query}市”或补充省份");
			}
		}
		LocationCandidate? selected = candidates
			.OrderByDescending(candidate => ScoreCandidate(query, candidate))
			.FirstOrDefault();
		if (selected is null)
		{
			throw new InvalidOperationException($"没有找到城市：{query}");
		}

		return new LocationResult(selected.Name, selected.Latitude, selected.Longitude);
	}

	private async Task<List<LocationCandidate>> ReadLocationCandidatesAsync(
		string query,
		CancellationToken cancellationToken)
	{
		var uri = "https://geocoding-api.open-meteo.com/v1/search?name="
			+ Uri.EscapeDataString(query)
			+ "&count=10&language=zh&format=json";
        using var response = await _client.GetAsync(uri, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (!document.RootElement.TryGetProperty("results", out var results)
            || results.ValueKind != JsonValueKind.Array
            || results.GetArrayLength() == 0)
        {
			return [];
        }

		var candidates = new List<LocationCandidate>(results.GetArrayLength());
		foreach (JsonElement item in results.EnumerateArray())
		{
			if (!item.TryGetProperty("latitude", out JsonElement latitude)
				|| !item.TryGetProperty("longitude", out JsonElement longitude))
			{
				continue;
			}
			candidates.Add(new LocationCandidate(
				item.TryGetProperty("name", out JsonElement name) ? name.GetString() ?? query : query,
				item.TryGetProperty("country_code", out JsonElement countryCode) ? countryCode.GetString() : null,
				item.TryGetProperty("feature_code", out JsonElement featureCode) ? featureCode.GetString() : null,
				item.TryGetProperty("population", out JsonElement population) && population.TryGetInt64(out long value) ? value : 0,
				latitude.GetDouble(),
				longitude.GetDouble()));
		}
		return candidates;
    }

	private static int ScoreCandidate(string query, LocationCandidate candidate)
	{
		int score = NamesMatch(query, candidate.Name) ? 10_000 : 0;
		if (string.Equals(query, candidate.Name, StringComparison.OrdinalIgnoreCase)) score += 1_000;
		if (string.Equals(candidate.CountryCode, "CN", StringComparison.OrdinalIgnoreCase)) score += 3_000;
		if (candidate.FeatureCode?.Equals("PPLC", StringComparison.OrdinalIgnoreCase) == true) score += 2_000;
		else if (candidate.FeatureCode?.StartsWith("PPLA", StringComparison.OrdinalIgnoreCase) == true) score += 1_500;
		score += (int)Math.Min(candidate.Population / 1_000, 1_000);
		return score;
	}

	private static bool IsAdministrativeCityCandidate(string query, LocationCandidate candidate) =>
		NamesMatch(query, candidate.Name)
		&& (candidate.FeatureCode?.Equals("PPLC", StringComparison.OrdinalIgnoreCase) == true
			|| candidate.FeatureCode?.StartsWith("PPLA", StringComparison.OrdinalIgnoreCase) == true);

	private static bool NamesMatch(string query, string candidateName) =>
		string.Equals(NormalizeCityName(query), NormalizeCityName(candidateName), StringComparison.OrdinalIgnoreCase);

	private static string NormalizeCityName(string value) =>
		value.Trim().TrimEnd('市');

	private static bool IsUnsuffixedChineseCityQuery(string query) =>
		!query.EndsWith('市')
		&& query.Length is >= 2 and <= 6
		&& query.All(character => character is >= '\u3400' and <= '\u9fff');

    private async Task<WeatherSnapshot> ReadCurrentAsync(LocationResult location, CancellationToken cancellationToken)
    {
        var latitude = location.Latitude.ToString("0.####", CultureInfo.InvariantCulture);
        var longitude = location.Longitude.ToString("0.####", CultureInfo.InvariantCulture);
        var uri = $"https://api.open-meteo.com/v1/forecast?latitude={latitude}&longitude={longitude}"
            + "&current=temperature_2m,apparent_temperature,relative_humidity_2m,weather_code,is_day"
            + "&daily=weather_code,temperature_2m_max,temperature_2m_min&forecast_days=5&timezone=auto";
        using var response = await _client.GetAsync(uri, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (!document.RootElement.TryGetProperty("current", out var current))
        {
            throw new InvalidOperationException("天气接口没有返回当前天气");
        }

        return new WeatherSnapshot(
            true,
            location.Name,
            current.GetProperty("temperature_2m").GetDouble(),
            current.GetProperty("apparent_temperature").GetDouble(),
            current.GetProperty("relative_humidity_2m").GetInt32(),
            current.GetProperty("weather_code").GetInt32(),
            current.GetProperty("is_day").GetInt32() == 1,
            DateTimeOffset.Now,
            DailyForecast: ReadDailyForecast(document.RootElement));
    }

    private static IReadOnlyList<DailyWeatherForecast> ReadDailyForecast(JsonElement root)
    {
        if (!root.TryGetProperty("daily", out var daily)) return [];
        var dates = daily.GetProperty("time");
        var codes = daily.GetProperty("weather_code");
        var maximums = daily.GetProperty("temperature_2m_max");
        var minimums = daily.GetProperty("temperature_2m_min");
        var count = new[] { dates.GetArrayLength(), codes.GetArrayLength(), maximums.GetArrayLength(), minimums.GetArrayLength() }.Min();
        var result = new List<DailyWeatherForecast>(Math.Min(count, 5));
        for (var index = 0; index < count && index < 5; index++)
        {
            result.Add(new DailyWeatherForecast(
                DateOnly.ParseExact(dates[index].GetString()!, "yyyy-MM-dd", CultureInfo.InvariantCulture),
                codes[index].GetInt32(), maximums[index].GetDouble(), minimums[index].GetDouble()));
        }
        return result;
    }
    public void Dispose()
    {
        if (_ownsClient) _client.Dispose();
    }

    private sealed record LocationResult(string Name, double Latitude, double Longitude);

	private sealed record LocationCandidate(
		string Name,
		string? CountryCode,
		string? FeatureCode,
		long Population,
		double Latitude,
		double Longitude);

	private sealed record CacheEntry(string Query, WeatherSnapshot Snapshot, DateTimeOffset FetchedAt);
}
