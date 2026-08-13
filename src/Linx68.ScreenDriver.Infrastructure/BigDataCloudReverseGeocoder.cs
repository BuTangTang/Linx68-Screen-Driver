using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace Linx68.ScreenDriver.Infrastructure;

public sealed class BigDataCloudReverseGeocoder : IDisposable
{
    private readonly HttpClient _client;
    private readonly bool _ownsClient;

    public BigDataCloudReverseGeocoder(HttpClient? client = null)
    {
        _ownsClient = client is null;
        _client = client ?? new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("Linx68ScreenDriver/1.0");
    }

    public async Task<string?> ResolveCityAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default)
    {
		// City-level reverse geocoding does not require precise device coordinates.
		string latitudeText = latitude.ToString("0.###", CultureInfo.InvariantCulture);
		string longitudeText = longitude.ToString("0.###", CultureInfo.InvariantCulture);
        string uri = "https://api.bigdatacloud.net/data/reverse-geocode-client"
            + $"?latitude={latitudeText}&longitude={longitudeText}&localityLanguage=zh";

        using HttpResponseMessage response = await _client.GetAsync(uri, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        JsonElement root = document.RootElement;

        return ReadNonEmptyString(root, "city")
            ?? ReadNonEmptyString(root, "locality")
            ?? ReadNonEmptyString(root, "principalSubdivision");
    }

    private static string? ReadNonEmptyString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement value)
            || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        string? text = value.GetString()?.Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    public void Dispose()
    {
        if (_ownsClient)
        {
            _client.Dispose();
        }
    }
}
