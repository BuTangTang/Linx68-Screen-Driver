
namespace Linx68.ScreenDriver.Core;

public sealed record ScreenProfile(int Width, int Height, int MaxJpegBytes, ScreenInsets SafeArea)
{
	public static ScreenProfile KeyboardDisplay { get; } = new ScreenProfile(142, 428, 524288, new ScreenInsets(10, 52, 10, 12));

}
