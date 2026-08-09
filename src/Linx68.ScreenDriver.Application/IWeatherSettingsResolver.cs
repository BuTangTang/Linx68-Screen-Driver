using Linx68.ScreenDriver.Core;

namespace Linx68.ScreenDriver.Application;

public interface IWeatherSettingsResolver
{
    Task<WeatherSettingsResolution> ResolveAsync(
        WeatherSettings settings,
        CancellationToken cancellationToken = default);
}

public sealed record WeatherSettingsResolution(
    WeatherSettings Settings,
    bool UsedAutomaticLocationFallback,
    AutomaticWeatherLocationResult? LocationResult = null);
