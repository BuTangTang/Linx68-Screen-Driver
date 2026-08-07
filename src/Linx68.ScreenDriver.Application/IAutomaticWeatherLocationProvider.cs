namespace Linx68.ScreenDriver.Application;

public interface IAutomaticWeatherLocationProvider
{
    Task<AutomaticWeatherLocation?> TryGetAsync(CancellationToken cancellationToken = default);
}

public sealed record AutomaticWeatherLocation(
    double Latitude,
    double Longitude,
    string DisplayName);
