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
            string city = string.IsNullOrWhiteSpace(settings.LocationQuery) ? "北京" : settings.LocationQuery;
            return new WeatherSettingsResolution(
                CopySavedSettings(settings),
                false,
                new AutomaticWeatherLocationResult(
                    DataLoadState.Ready,
                    null,
                    $"手动城市 · {city}",
                    DateTimeOffset.Now));
        }

        AutomaticWeatherLocationResult locationResult = await locationProvider.TryGetDetailsAsync(
            forceRefresh: false,
            cancellationToken);
        AutomaticWeatherLocation? location = locationResult.Location;
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
                true,
                locationResult);
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
            false,
            locationResult);
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
