namespace Linx68.ScreenDriver.Core;

public enum ImageTimePlacement
{
    Top,
    Bottom
}

public enum ScreenColorMode
{
    Night,
    Daylight
}

public sealed record ScreenDisplayOptions(
    ImageTimePlacement ImageTimePlacement = ImageTimePlacement.Bottom,
    ScreenColorMode ColorMode = ScreenColorMode.Night)
{
    public static ScreenDisplayOptions Default { get; } = new();
}
