namespace Linx68.ScreenDriver.Core;

public enum ImageTimePlacement
{
    Top,
    Bottom
}

public sealed record ScreenDisplayOptions(
    ImageTimePlacement ImageTimePlacement = ImageTimePlacement.Bottom)
{
    public static ScreenDisplayOptions Default { get; } = new();
}