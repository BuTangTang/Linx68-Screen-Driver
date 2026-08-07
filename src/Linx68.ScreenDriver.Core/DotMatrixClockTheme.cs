using System.IO;
using System.Windows;
using System.Windows.Media;

namespace Linx68.ScreenDriver.Core;

/// <summary>
/// A restrained Nothing-inspired clock for the 142 x 428 keyboard display.
/// Doto is deliberately local to this theme so user-selected fonts cannot
/// destroy the dot-matrix numeral construction.
/// </summary>
public sealed class DotMatrixClockTheme : IScreenTheme
{
    private static readonly FontFamily Doto = new(
        new Uri(
            Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts") + Path.DirectorySeparatorChar,
            UriKind.Absolute),
        "./#Doto");

    public string Id => "clock-dot-matrix";
    public string DisplayName => "点阵时钟";
    public string Description => "小时、分钟、秒钟纵向点阵显示";
    public string Details => "使用三段式纵向层级，在小屏上清晰区分小时、分钟与秒钟。";

    public void Draw(ScreenCanvas canvas, SystemSnapshot snapshot)
    {
        Rect safe = canvas.SafeBounds;
        Color label = Color.FromRgb(153, 153, 153);
        Color divider = Color.FromRgb(34, 34, 34);

        canvas.Fill(Colors.Black);

        DrawTimeSection(canvas, safe, safe.Top + 10, "小时", snapshot.Timestamp.ToString("HH"), 67, Colors.White, divider);
        DrawTimeSection(canvas, safe, safe.Top + 128, "分钟", snapshot.Timestamp.ToString("mm"), 67, Colors.White, divider);

        canvas.AlignedText("秒", 10, label,
            new Rect(safe.Left, safe.Top + 250, safe.Width, 16),
            FontWeights.Medium, TextAlignment.Left);
        canvas.AlignedText(snapshot.Timestamp.ToString("ss"), 59, canvas.AccentColor,
            new Rect(safe.Left, safe.Top + 270, safe.Width, 74),
            FontWeights.Medium, TextAlignment.Left, Doto);

        DrawSecondTicks(canvas, safe, snapshot.Timestamp.Second, divider);
    }

    private static void DrawTimeSection(ScreenCanvas canvas, Rect safe, double top,
        string label, string value, double valueSize, Color valueColor, Color dividerColor)
    {
        canvas.AlignedText(label, 10, Color.FromRgb(153, 153, 153),
            new Rect(safe.Left, top, safe.Width, 16),
            FontWeights.Medium, TextAlignment.Left);
        canvas.AlignedText(value, valueSize, valueColor,
            new Rect(safe.Left, top + 18, safe.Width, 80),
            FontWeights.Medium, TextAlignment.Left, Doto);
        canvas.Line(new Point(safe.Left, top + 106),
            new Point(safe.Left + 24, top + 106), dividerColor, 2);
    }

    private static void DrawSecondTicks(ScreenCanvas canvas, Rect safe, int second, Color emptyColor)
    {
        const int segmentCount = 10;
        const double gap = 3;
        double width = (safe.Width - gap * (segmentCount - 1)) / segmentCount;
        int filled = (int)Math.Ceiling((second + 1) / 60.0 * segmentCount);
        double top = safe.Bottom - 13;

        for (int index = 0; index < segmentCount; index++)
        {
            Color color = index < filled ? canvas.AccentColor : emptyColor;
            canvas.RoundedRect(new Rect(safe.Left + index * (width + gap), top, width, 4), 0, color);
        }
    }
}