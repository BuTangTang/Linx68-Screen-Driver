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
    private WeatherSnapshot? _cached;
    private string? _cachedQuery;
    private DateTimeOffset _lastFetch;

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
        if (_cached is not null
            && string.Equals(_cachedQuery, query, StringComparison.OrdinalIgnoreCase)
            && now - _lastFetch < CacheDuration)
        {
            return _cached;
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
            _cached = snapshot;
            _cachedQuery = query;
            _lastFetch = now;
            return snapshot;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            return _cached is null
                ? WeatherSnapshot.Unavailable(ex.Message)
                : _cached with { IsStale = true, ErrorMessage = ex.Message };
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
        var uri = "https://geocoding-api.open-meteo.com/v1/search?name="
            + Uri.EscapeDataString(query)
            + "&count=1&language=zh&format=json";
        using var response = await _client.GetAsync(uri, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (!document.RootElement.TryGetProperty("results", out var results)
            || results.ValueKind != JsonValueKind.Array
            || results.GetArrayLength() == 0)
        {
            throw new InvalidOperationException($"没有找到城市：{query}");
        }

        var first = results[0];
        return new LocationResult(
            first.GetProperty("name").GetString() ?? query,
            first.GetProperty("latitude").GetDouble(),
            first.GetProperty("longitude").GetDouble());
    }

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
}
