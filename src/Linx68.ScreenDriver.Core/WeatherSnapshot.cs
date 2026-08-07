namespace Linx68.ScreenDriver.Core;

public sealed record WeatherSnapshot(
    bool Available,
    string LocationName,
    double TemperatureC,
    double ApparentTemperatureC,
    int RelativeHumidityPercent,
    int WeatherCode,
    bool IsDay,
    DateTimeOffset UpdatedAt,
    bool IsStale = false,
    string? ErrorMessage = null,
    IReadOnlyList<DailyWeatherForecast>? DailyForecast = null)
{
    public static WeatherSnapshot Unavailable(string? message = null) =>
        new(false, "天气", 0, 0, 0, 0, true, DateTimeOffset.MinValue, false, message);

    public string ConditionText => WeatherCondition.FromCode(WeatherCode);
}

public static class WeatherCondition
{
    public static string FromCode(int code) => code switch
    {
        0 => "晴",
        1 => "大部晴朗",
        2 => "多云",
        3 => "阴",
        45 or 48 => "雾",
        51 or 53 or 55 => "毛毛雨",
        56 or 57 => "冻毛毛雨",
        61 or 63 or 65 => "雨",
        66 or 67 => "冻雨",
        71 or 73 or 75 or 77 => "雪",
        80 or 81 or 82 => "阵雨",
        85 or 86 => "阵雪",
        95 or 96 or 99 => "雷雨",
        _ => "天气"
    };

    public static WeatherIconKind IconFromCode(int code) => code switch
    {
        0 => WeatherIconKind.Clear,
        1 or 2 => WeatherIconKind.PartlyCloudy,
        3 => WeatherIconKind.Cloudy,
        45 or 48 => WeatherIconKind.Fog,
        51 or 53 or 55 or 56 or 57 or 61 or 63 or 65 or 66 or 67 or 80 or 81 or 82 => WeatherIconKind.Rain,
        71 or 73 or 75 or 77 or 85 or 86 => WeatherIconKind.Snow,
        95 or 96 or 99 => WeatherIconKind.Thunderstorm,
        _ => WeatherIconKind.Cloudy
    };
}

public enum WeatherIconKind
{
    Clear,
    PartlyCloudy,
    Cloudy,
    Fog,
    Rain,
    Snow,
    Thunderstorm
}