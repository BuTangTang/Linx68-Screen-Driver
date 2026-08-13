using System.Windows;
using System.Windows.Media;

namespace Linx68.ScreenDriver.Core;

public sealed class SignalGardenTheme : IScreenTheme
{
    public string Id => "signal-garden";

    public string DisplayName => "信号花园";

    public string Description => "会随系统负载生长的数据植物";

    public string Details => "CPU 控制花芯状态，内存改变叶片形态，上下行流量以双色信号水线呈现。";

    public void Draw(ScreenCanvas canvas, SystemSnapshot snapshot)
    {
        Rect safe = canvas.SafeBounds;
        Color primary = Color.FromRgb(239, 244, 226);
        Color secondary = Color.FromRgb(151, 180, 166);
        Color leaf = Color.FromRgb(78, 184, 137);
        Color mint = Color.FromRgb(118, 229, 185);
        Color water = Color.FromRgb(77, 190, 223);
        Color upload = Color.FromRgb(244, 175, 91);
        bool stressed = Math.Max(snapshot.CpuPercent, snapshot.MemoryPercent) >= 85;
        Color core = stressed ? Color.FromRgb(246, 114, 91) : Color.FromRgb(244, 196, 85);

        canvas.Gradient(Color.FromRgb(4, 20, 20), Color.FromRgb(8, 45, 35), new Point(0, 0), new Point(1, 1));
        canvas.Text("信号花园", 10.5, secondary, new Point(safe.Left, safe.Top + 4), FontWeights.SemiBold);
        canvas.Text(snapshot.Timestamp.ToString("HH:mm"), 10.5, primary,
            new Point(safe.Left, safe.Top + 4), FontWeights.SemiBold, TextAlignment.Right, safe.Width);

        Rect garden = new(safe.Left, safe.Top + 34, safe.Width, 211);
        canvas.RoundedGradientRect(garden, 16, Color.FromRgb(10, 53, 43), Color.FromRgb(7, 35, 33), Color.FromRgb(37, 94, 75));
        double centerX = garden.Left + garden.Width / 2;
        double flowerY = garden.Top + 73;
        double stemBottom = garden.Bottom - 25;
        canvas.Line(new Point(centerX, flowerY + 14), new Point(centerX, stemBottom), Color.FromRgb(69, 163, 113), 4);

        double memoryScale = 0.65 + Math.Clamp(snapshot.MemoryPercent, 0, 100) / 250;
        DrawLeaf(canvas, new Point(centerX - 2, flowerY + 61), -1, 24 * memoryScale, leaf);
        DrawLeaf(canvas, new Point(centerX + 2, flowerY + 95), 1, 21 * memoryScale, Color.FromRgb(58, 155, 121));

        double petalRadius = 8 + Math.Clamp(snapshot.CpuPercent, 0, 100) * 0.035;
        for (int index = 0; index < 6; index++)
        {
            double angle = index * Math.PI / 3;
            double x = centerX + Math.Cos(angle) * 18 - petalRadius;
            double y = flowerY + Math.Sin(angle) * 18 - petalRadius;
            canvas.Ellipse(new Rect(x, y, petalRadius * 2, petalRadius * 2),
                stressed ? Color.FromRgb(203, 91, 89) : Color.FromRgb(104, 199, 145));
        }
        double coreRadius = 12 + Math.Clamp(snapshot.CpuPercent, 0, 100) * 0.045;
        canvas.Ellipse(new Rect(centerX - coreRadius, flowerY - coreRadius, coreRadius * 2, coreRadius * 2), core,
            Color.FromRgb(255, 220, 134), 1.2);
        canvas.CenteredText($"{snapshot.CpuPercent:0}", 10.5, Color.FromRgb(53, 45, 38),
            new Rect(centerX - 16, flowerY - 10, 32, 20), FontWeights.Bold);

        double flowWidth = garden.Width - 24;
        double downloadLength = Math.Clamp(snapshot.DownloadMbps / 30, 0, 1) * flowWidth;
        double uploadLength = Math.Clamp(snapshot.UploadMbps / 15, 0, 1) * flowWidth;
        Color flowTrack = Color.FromRgb(29, 70, 61);
        canvas.RoundedRect(new Rect(garden.Left + 12, garden.Bottom - 18, flowWidth, 4), 2, flowTrack);
        canvas.RoundedRect(new Rect(garden.Left + 12, garden.Bottom - 10, flowWidth, 4), 2, flowTrack);
        if (downloadLength >= 0.5)
        {
            canvas.RoundedRect(new Rect(garden.Left + 12, garden.Bottom - 18, downloadLength, 4), 2, water);
        }
        if (uploadLength >= 0.5)
        {
            canvas.RoundedRect(new Rect(garden.Right - 12 - uploadLength, garden.Bottom - 10, uploadLength, 4), 2, upload);
        }

        double metricTop = garden.Bottom + 11;
        double gap = 7;
        double half = (safe.Width - gap) / 2;
        DrawMetric(canvas, new Rect(safe.Left, metricTop, half, 53), "CPU", $"{snapshot.CpuPercent:0}%", core);
        DrawMetric(canvas, new Rect(safe.Left + half + gap, metricTop, half, 53), "内存", $"{snapshot.MemoryPercent:0}%", mint);

        Rect network = new(safe.Left, safe.Bottom - 53, safe.Width, 45);
        canvas.RoundedRect(network, 10, Color.FromRgb(10, 38, 35), Color.FromRgb(35, 81, 68));
        canvas.Text("信号流", 8.5, secondary, new Point(network.Left + 9, network.Top + 6), FontWeights.SemiBold);
        canvas.Text($"↓ {snapshot.DownloadMbps:0.0}M", 10.5, water,
            new Point(network.Left + 9, network.Top + 22), FontWeights.SemiBold);
        canvas.Text($"↑ {snapshot.UploadMbps:0.0}M", 10.5, upload,
            new Point(network.Left, network.Top + 22), FontWeights.SemiBold, TextAlignment.Right, network.Width - 9);
    }

    private static void DrawLeaf(ScreenCanvas canvas, Point origin, int direction, double length, Color fill)
    {
        var geometry = new StreamGeometry();
        using (StreamGeometryContext context = geometry.Open())
        {
            Point tip = new(origin.X + direction * length, origin.Y - length * 0.45);
            context.BeginFigure(origin, true, true);
            context.BezierTo(
                new Point(origin.X + direction * length * 0.35, origin.Y - length * 0.75),
                new Point(origin.X + direction * length * 0.85, origin.Y - length * 0.75),
                tip,
                true,
                false);
            context.BezierTo(
                new Point(origin.X + direction * length * 0.8, origin.Y - length * 0.05),
                new Point(origin.X + direction * length * 0.25, origin.Y + length * 0.05),
                origin,
                true,
                false);
        }
        geometry.Freeze();
        canvas.Path(geometry, Color.FromRgb(96, 205, 154), 1, fill);
    }

    private static void DrawMetric(ScreenCanvas canvas, Rect card, string label, string value, Color accent)
    {
        canvas.RoundedRect(card, 10, Color.FromRgb(10, 38, 35), Color.FromRgb(35, 81, 68));
        canvas.Text(label, 8.5, Color.FromRgb(141, 174, 158), new Point(card.Left + 8, card.Top + 7), FontWeights.SemiBold);
        canvas.CenteredText(value, 15, accent, new Rect(card.Left + 4, card.Top + 23, card.Width - 8, 22), FontWeights.Bold);
    }
}
