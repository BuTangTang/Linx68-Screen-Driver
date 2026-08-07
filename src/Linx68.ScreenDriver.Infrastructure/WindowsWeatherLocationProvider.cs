using Linx68.ScreenDriver.Core;
using Windows.Devices.Geolocation;

namespace Linx68.ScreenDriver.Infrastructure;

public sealed class WindowsWeatherLocationProvider : IDisposable
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);
    private readonly BigDataCloudReverseGeocoder _reverseGeocoder = new();
    private AutomaticWeatherLocation? _cached;
    private DateTimeOffset _cachedAt;

    public async Task<AutomaticWeatherLocation?> TryGetAsync()
    {
        if (_cached is not null && DateTimeOffset.Now - _cachedAt < CacheDuration)
        {
            return _cached;
        }

        try
        {
            GeolocationAccessStatus access = await Geolocator.RequestAccessAsync();
            if (access != GeolocationAccessStatus.Allowed)
            {
                return null;
            }

            var locator = new Geolocator
            {
                DesiredAccuracy = PositionAccuracy.Default,
                ReportInterval = 0
            };
            Geoposition position = await locator.GetGeopositionAsync(
                maximumAge: TimeSpan.FromMinutes(10),
                timeout: TimeSpan.FromSeconds(8));
            BasicGeoposition coordinate = position.Coordinate.Point.Position;
            string displayName = await ResolveDisplayNameAsync(
                coordinate.Latitude,
                coordinate.Longitude);
            _cached = new AutomaticWeatherLocation(
                coordinate.Latitude,
                coordinate.Longitude,
                displayName);
            _cachedAt = DateTimeOffset.Now;
            return _cached;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private async Task<string> ResolveDisplayNameAsync(double latitude, double longitude)
    {
        try
        {
            return await _reverseGeocoder.ResolveCityAsync(latitude, longitude)
                ?? "当前位置";
        }
        catch (Exception)
        {
            return "当前位置";
        }
    }

    public void Dispose() => _reverseGeocoder.Dispose();
}

public sealed record AutomaticWeatherLocation(
    double Latitude,
    double Longitude,
    string DisplayName);
