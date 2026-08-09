using System.Windows;
using System.Windows.Media;

namespace Linx68.ScreenDriver.Core;

public sealed class PixelCompanionTheme : IScreenTheme
{
    public string Id => "pixel-companion";

    public string DisplayName => "像素管家";

    public string Description => "会看系统负载变表情的像素伙伴";

    public string Details => "像素表情随 CPU、内存和网络状态变化，同时保留三项关键指标。";

    public void Draw(ScreenCanvas canvas, SystemSnapshot snapshot)
    {
        Rect safe = canvas.SafeBounds;
        Color primary = Colors.White;
        Color secondary = Color.FromRgb(145, 170, 177);
        Color surface = Color.FromRgb(10, 31, 34);
        Color stroke = Color.FromRgb(32, 70, 70);
        double pressure = Math.Max(snapshot.CpuPercent, snapshot.MemoryPercent);
        Mood mood = pressure >= 88 ? Mood.Stressed
            : pressure >= 68 ? Mood.Busy
            : snapshot.DownloadMbps + snapshot.UploadMbps >= 20 ? Mood.Excited
            : Mood.Happy;

        canvas.Gradient(Color.FromRgb(3, 17, 20), Color.FromRgb(6, 38, 38), new Point(0, 0), new Point(1, 1));
        canvas.Ellipse(new Rect(safe.Left, safe.Top + 8, 6, 6), GetMoodColor(canvas, mood));
        canvas.Text("像素管家", 10.5, secondary, new Point(safe.Left + 11, safe.Top + 4), FontWeights.SemiBold);
        canvas.Text(snapshot.Timestamp.ToString("HH:mm"), 10.5, primary, new Point(safe.Left, safe.Top + 4), FontWeights.SemiBold, TextAlignment.Right, safe.Width);

        Rect faceCard = new(safe.Left, safe.Top + 38, safe.Width, 135);
        canvas.RoundedGradientRect(faceCard, 15, Color.FromRgb(17, 54, 53), surface, stroke);
        DrawFace(canvas, new Rect(faceCard.Left + 28, faceCard.Top + 16, faceCard.Width - 56, 82), mood);
        canvas.CenteredText(GetMoodTitle(mood), 14, primary,
            new Rect(faceCard.Left + 8, faceCard.Top + 101, faceCard.Width - 16, 21), FontWeights.Bold);

        DrawMetric(canvas, new Rect(safe.Left, safe.Top + 188, safe.Width, 52), "CPU", snapshot.CpuPercent, $"{snapshot.CpuPercent:0}%", surface, stroke, secondary, primary);
        DrawMetric(canvas, new Rect(safe.Left, safe.Top + 250, safe.Width, 52), "内存", snapshot.MemoryPercent, $"{snapshot.MemoryPercent:0}%", surface, stroke, secondary, primary);

        Rect network = new(safe.Left, safe.Top + 312, safe.Width, 44);
        canvas.RoundedRect(network, 10, surface, stroke);
        canvas.Text("网络", 9.5, secondary, new Point(network.Left + 10, network.Top + 7), FontWeights.SemiBold);
        canvas.Text($"↓ {snapshot.DownloadMbps:0.0}M", 10.5, primary, new Point(network.Left + 10, network.Top + 22), FontWeights.SemiBold);
        canvas.Text($"↑ {snapshot.UploadMbps:0.0}M", 10.5, primary, new Point(network.Left, network.Top + 22), FontWeights.SemiBold, TextAlignment.Right, network.Width - 10);
    }

    private static void DrawMetric(ScreenCanvas canvas, Rect card, string label, double value, string display, Color surface, Color stroke, Color secondary, Color primary)
    {
        canvas.RoundedRect(card, 10, surface, stroke);
        canvas.Text(label, 9.5, secondary, new Point(card.Left + 10, card.Top + 7), FontWeights.SemiBold);
        canvas.Text(display, 12, primary, new Point(card.Left, card.Top + 6), FontWeights.Bold, TextAlignment.Right, card.Width - 10);
        canvas.ProgressBar(new Rect(card.Left + 10, card.Top + 33, card.Width - 20, 7), value, Color.FromRgb(34, 62, 62), canvas.AccentColor);
    }

    private static void DrawFace(ScreenCanvas canvas, Rect bounds, Mood mood)
    {
        const double pixel = 10;
        Color color = GetMoodColor(canvas, mood);
        double left = bounds.Left + (bounds.Width - pixel * 6) / 2;
        double top = bounds.Top + 4;
        void Pixel(int x, int y) => canvas.RoundedRect(new Rect(left + x * pixel, top + y * pixel, pixel - 2, pixel - 2), 2, color);

        Pixel(1, 1); Pixel(4, 1);
        if (mood == Mood.Stressed)
        {
            Pixel(0, 0); Pixel(2, 2); Pixel(3, 2); Pixel(5, 0);
            Pixel(1, 5); Pixel(2, 4); Pixel(3, 4); Pixel(4, 5);
        }
        else if (mood == Mood.Busy)
        {
            Pixel(0, 1); Pixel(2, 1); Pixel(3, 1); Pixel(5, 1);
            Pixel(1, 4); Pixel(2, 4); Pixel(3, 4); Pixel(4, 4);
        }
        else if (mood == Mood.Excited)
        {
            Pixel(0, 0); Pixel(2, 2); Pixel(3, 2); Pixel(5, 0);
            Pixel(1, 4); Pixel(1, 5); Pixel(2, 6); Pixel(3, 6); Pixel(4, 5); Pixel(4, 4);
        }
        else
        {
            Pixel(1, 5); Pixel(2, 6); Pixel(3, 6); Pixel(4, 5);
        }
    }

    private static Color GetMoodColor(ScreenCanvas canvas, Mood mood) => mood switch
    {
        Mood.Stressed => Color.FromRgb(246, 112, 103),
        Mood.Busy => Color.FromRgb(247, 185, 77),
        _ => canvas.AccentColor
    };

    private static string GetMoodTitle(Mood mood) => mood switch
    {
        Mood.Stressed => "有点忙，先喘口气",
        Mood.Busy => "正在认真干活",
        Mood.Excited => "网络跑得很开心",
        _ => "一切都很稳"
    };

    private enum Mood
    {
        Happy,
        Excited,
        Busy,
        Stressed
    }
}
