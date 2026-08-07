namespace Linx68.ScreenDriver.Core;

public sealed class WeatherSettings
{
    public string LocationQuery { get; set; } = "北京";

    public bool UseAutomaticLocation { get; set; } = true;

    [System.Text.Json.Serialization.JsonIgnore]
    public double? Latitude { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public double? Longitude { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public string? AutomaticLocationName { get; set; }
}