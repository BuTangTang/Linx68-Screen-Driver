using System;

namespace Linx68.ScreenDriver.Core;

public sealed record RenderedFrame(byte[] JpegBytes, int Width, int Height, string ThemeId, DateTimeOffset RenderedAt);
