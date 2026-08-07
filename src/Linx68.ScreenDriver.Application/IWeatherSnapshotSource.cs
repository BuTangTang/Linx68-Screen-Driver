using Linx68.ScreenDriver.Core;

namespace Linx68.ScreenDriver.Application;

public interface IWeatherSnapshotSource
{
    Task<WeatherSnapshot> ReadAsync(WeatherSettings settings, CancellationToken cancellationToken = default);
}
