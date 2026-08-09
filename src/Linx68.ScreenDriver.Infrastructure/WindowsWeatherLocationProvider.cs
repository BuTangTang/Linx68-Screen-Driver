using Linx68.ScreenDriver.Core;
using Linx68.ScreenDriver.Application;
using Windows.Devices.Geolocation;

namespace Linx68.ScreenDriver.Infrastructure;

public sealed class WindowsWeatherLocationProvider : IAutomaticWeatherLocationProvider, IDisposable
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);
    private readonly BigDataCloudReverseGeocoder _reverseGeocoder = new();
    private AutomaticWeatherLocation? _cached;
    private DateTimeOffset _cachedAt;

    public async Task<AutomaticWeatherLocation?> TryGetAsync(
        CancellationToken cancellationToken = default) =>
        (await TryGetDetailsAsync(false, cancellationToken)).Location;

    public async Task<AutomaticWeatherLocationResult> TryGetDetailsAsync(
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!forceRefresh && _cached is not null && DateTimeOffset.Now - _cachedAt < CacheDuration)
        {
            return new AutomaticWeatherLocationResult(
                DataLoadState.Ready,
                _cached,
                $"Windows 定位 · {_cached.DisplayName} · 使用缓存",
                _cachedAt,
                FromCache: true);
        }

        try
        {
            GeolocationAccessStatus access = await Geolocator.RequestAccessAsync();
            if (access != GeolocationAccessStatus.Allowed)
            {
                return CreateFailure("Windows 定位权限未开启");
            }

            var locator = new Geolocator
            {
                DesiredAccuracy = PositionAccuracy.Default,
                ReportInterval = 0
            };
            Geoposition position = await locator.GetGeopositionAsync(
                maximumAge: TimeSpan.FromMinutes(10),
                timeout: TimeSpan.FromSeconds(8));
            cancellationToken.ThrowIfCancellationRequested();
            BasicGeoposition coordinate = position.Coordinate.Point.Position;
            string displayName = await ResolveDisplayNameAsync(
                coordinate.Latitude,
                coordinate.Longitude);
            _cached = new AutomaticWeatherLocation(
                coordinate.Latitude,
                coordinate.Longitude,
                displayName);
            _cachedAt = DateTimeOffset.Now;
            return new AutomaticWeatherLocationResult(
                DataLoadState.Ready,
                _cached,
                $"Windows 定位 · {displayName}",
                _cachedAt);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return CreateFailure(GetFailureMessage(ex));
        }
    }

    private AutomaticWeatherLocationResult CreateFailure(string message)
    {
        if (_cached is not null)
        {
            return new AutomaticWeatherLocationResult(
                DataLoadState.Stale,
                _cached,
                $"{message} · 使用上次位置 {_cached.DisplayName}",
                _cachedAt,
                FromCache: true);
        }

        return new AutomaticWeatherLocationResult(
            DataLoadState.Error,
            null,
            message,
            DateTimeOffset.Now);
    }

    private static string GetFailureMessage(Exception exception) => exception switch
    {
        TimeoutException => "Windows 定位超时",
        UnauthorizedAccessException => "Windows 定位权限未开启",
        _ => "Windows 定位暂不可用"
    };

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
