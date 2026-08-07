namespace Linx68.ScreenDriver.Core;

public sealed record DailyWeatherForecast(
    DateOnly Date,
    int WeatherCode,
    double MaximumTemperatureC,
    double MinimumTemperatureC)
{
    public string ConditionText => WeatherCondition.FromCode(WeatherCode);
}
