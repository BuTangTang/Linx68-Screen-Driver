namespace Linx68.ScreenDriver.Application;

public interface IAutomaticWeatherLocationProvider
{
    Task<AutomaticWeatherLocation?> TryGetAsync(CancellationToken cancellationToken = default);

	void InvalidateCache()
	{
	}

    async Task<AutomaticWeatherLocationResult> TryGetDetailsAsync(
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        AutomaticWeatherLocation? location = await TryGetAsync(cancellationToken);
        return location is null
            ? new AutomaticWeatherLocationResult(
                DataLoadState.Empty,
                null,
                "Windows 定位暂不可用",
                DateTimeOffset.Now)
            : new AutomaticWeatherLocationResult(
                DataLoadState.Ready,
                location,
                $"Windows 定位 · {location.DisplayName}",
                DateTimeOffset.Now);
    }
}

public sealed record AutomaticWeatherLocation(
    double Latitude,
    double Longitude,
    string DisplayName);

public sealed record AutomaticWeatherLocationResult(
    DataLoadState State,
    AutomaticWeatherLocation? Location,
    string Message,
    DateTimeOffset ObservedAt,
    bool FromCache = false);
