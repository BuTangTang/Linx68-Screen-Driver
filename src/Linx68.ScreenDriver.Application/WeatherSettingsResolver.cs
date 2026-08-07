using Linx68.ScreenDriver.Core;

namespace Linx68.ScreenDriver.Application;

public sealed class WeatherSettingsResolver(
    IAutomaticWeatherLocationProvider locationProvider) : IWeatherSettingsResolver
{
    public async Task<WeatherSettingsResolution> ResolveAsync(
        WeatherSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (!settings.UseAutomaticLocation)
        {
            return new WeatherSettingsResolution(CopySavedSettings(settings), false);
        }

        AutomaticWeatherLocation? location = await locationProvider.TryGetAsync(cancellationToken);
        if (location is null)
        {
            return new WeatherSettingsResolution(
                new WeatherSettings
                {
                    LocationQuery = string.IsNullOrWhiteSpace(settings.LocationQuery)
                        ? "北京"
                        : settings.LocationQuery,
                    UseAutomaticLocation = false
                },
                true);
        }

        return new WeatherSettingsResolution(
            new WeatherSettings
            {
                LocationQuery = settings.LocationQuery,
                UseAutomaticLocation = true,
                Latitude = location.Latitude,
                Longitude = location.Longitude,
                AutomaticLocationName = location.DisplayName
            },
            false);
    }

    private static WeatherSettings CopySavedSettings(WeatherSettings settings) => new()
    {
        LocationQuery = settings.LocationQuery,
        UseAutomaticLocation = settings.UseAutomaticLocation,
        Latitude = settings.Latitude,
        Longitude = settings.Longitude,
        AutomaticLocationName = settings.AutomaticLocationName
    };
}
