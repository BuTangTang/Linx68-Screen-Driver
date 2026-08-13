using Linx68.ScreenDriver.Core;
using Linx68.ScreenDriver.Application;
using Windows.Devices.Geolocation;

namespace Linx68.ScreenDriver.Infrastructure;

public sealed class WindowsWeatherLocationProvider : IAutomaticWeatherLocationProvider, IDisposable
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);
	private static readonly TimeSpan UnresolvedCityRetryDelay = TimeSpan.FromSeconds(45);
    private readonly BigDataCloudReverseGeocoder _reverseGeocoder = new();
	private readonly object _cacheGate = new();
    private AutomaticWeatherLocation? _cached;
    private DateTimeOffset _cachedAt;
	private bool _cachedCityResolved;
	private long _cacheGeneration;

    public async Task<AutomaticWeatherLocation?> TryGetAsync(
        CancellationToken cancellationToken = default) =>
        (await TryGetDetailsAsync(false, cancellationToken)).Location;

    public async Task<AutomaticWeatherLocationResult> TryGetDetailsAsync(
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
		AutomaticWeatherLocation? cached;
		DateTimeOffset cachedAt;
		bool cachedCityResolved;
		long cacheGeneration;
		lock (_cacheGate)
		{
			cached = _cached;
			cachedAt = _cachedAt;
			cachedCityResolved = _cachedCityResolved;
			cacheGeneration = _cacheGeneration;
		}
		TimeSpan cacheLifetime = cachedCityResolved ? CacheDuration : UnresolvedCityRetryDelay;
		if (!forceRefresh && cached is not null && DateTimeOffset.Now - cachedAt < cacheLifetime)
        {
            return new AutomaticWeatherLocationResult(
				cachedCityResolved ? DataLoadState.Ready : DataLoadState.Stale,
                cached,
				cachedCityResolved
					? $"Windows 定位 · {cached.DisplayName} · 使用缓存"
					: "Windows 定位坐标缓存 · 城市解析仍不可用",
				cachedAt,
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
			string? resolvedCity = await ResolveDisplayNameAsync(
                coordinate.Latitude,
				coordinate.Longitude,
				cancellationToken);
			cancellationToken.ThrowIfCancellationRequested();
			DateTimeOffset observedAt = DateTimeOffset.Now;
			AutomaticWeatherLocationResult result = CreateLocatedResult(
				coordinate.Latitude,
				coordinate.Longitude,
				resolvedCity,
				observedAt);
			lock (_cacheGate)
			{
				if (cacheGeneration == _cacheGeneration)
				{
					_cachedCityResolved = !string.IsNullOrWhiteSpace(resolvedCity);
					_cachedAt = observedAt;
					_cached = result.Location;
				}
			}
			return result;
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
		AutomaticWeatherLocation? cached;
		DateTimeOffset cachedAt;
		lock (_cacheGate)
		{
			cached = _cached;
			cachedAt = _cachedAt;
		}
		if (cached is not null)
        {
            return new AutomaticWeatherLocationResult(
                DataLoadState.Stale,
				cached,
				$"{message} · 使用上次位置 {cached.DisplayName}",
				cachedAt,
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

	public void InvalidateCache()
	{
		lock (_cacheGate)
		{
			_cacheGeneration++;
			_cached = null;
			_cachedAt = default;
			_cachedCityResolved = false;
		}
	}

	private async Task<string?> ResolveDisplayNameAsync(
		double latitude,
		double longitude,
		CancellationToken cancellationToken)
    {
        try
        {
			return await _reverseGeocoder.ResolveCityAsync(latitude, longitude, cancellationToken);
        }
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception)
        {
			return null;
        }
    }

	internal static AutomaticWeatherLocationResult CreateLocatedResult(
		double latitude,
		double longitude,
		string? resolvedCity,
		DateTimeOffset observedAt)
	{
		bool cityResolved = !string.IsNullOrWhiteSpace(resolvedCity);
		string displayName = cityResolved ? resolvedCity!.Trim() : "当前位置";
		return new AutomaticWeatherLocationResult(
			cityResolved ? DataLoadState.Ready : DataLoadState.Stale,
			new AutomaticWeatherLocation(latitude, longitude, displayName),
			cityResolved
				? $"Windows 定位 · {displayName}"
				: "Windows 定位成功 · 城市反向解析失败 · 已保留坐标",
			observedAt);
	}

    public void Dispose() => _reverseGeocoder.Dispose();
}
