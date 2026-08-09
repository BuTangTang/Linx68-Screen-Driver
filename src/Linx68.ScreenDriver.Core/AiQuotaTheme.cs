using System.Windows;
using System.Windows.Media;

namespace Linx68.ScreenDriver.Core;

public sealed class AiQuotaTheme : IScreenTheme
{
    private static readonly Color QuotaColor = Color.FromRgb(108, 140, 255);

    public string Id => "ai-quota";
    public string DisplayName => "Codex 额度";
    public string Description => "额度圆环、重置与任务状态";
    public string Details => "显示当前 Codex 可用额度、具体重置时间以及运行中或已经完成的任务。";

    public void Draw(ScreenCanvas canvas, SystemSnapshot snapshot)
    {
        var quota = snapshot.AiQuota ?? AiQuotaSnapshot.Empty;
        var safe = canvas.SafeBounds;
        var percent = quota.Available ? quota.ClampedRemainingPercent : 0d;
        var platformName = !string.IsNullOrWhiteSpace(quota.PlatformName)
            ? quota.PlatformName.Trim()
            : "Codex";

        canvas.Gradient(Color.FromRgb(19, 17, 38), Color.FromRgb(10, 18, 30), new Point(0, 0), new Point(1, 1));

        canvas.Ellipse(new Rect(safe.Left, safe.Top + 8, 6, 6), quota.Available ? QuotaColor : Color.FromRgb(108, 121, 136));
        canvas.FittedText(
            $"{platformName} 额度",
            12.5,
            10.5,
            Colors.White,
            new Point(safe.Left + 11, safe.Top + 4),
            FontWeights.SemiBold,
            TextAlignment.Left,
            safe.Width - 44,
            24);

        Point center = new Point(safe.Left + safe.Width / 2, safe.Top + 84);
        const double radius = 46;
        canvas.Ellipse(new Rect(center.X - radius, center.Y - radius, radius * 2, radius * 2), Color.FromArgb(0, 0, 0, 0), Color.FromRgb(55, 62, 92), 10);
        DrawArc(canvas, center, radius, percent, quota.Available ? QuotaColor : Color.FromRgb(82, 93, 110));
        canvas.Text(quota.Available ? $"{Math.Round(percent):0}%" : "--", 28, Colors.White,
            new Point(center.X - radius, center.Y - 17), FontWeights.Bold, TextAlignment.Center, radius * 2, 38);
        canvas.Text(quota.Available ? "可用额度" : "等待额度数据", 11, Color.FromRgb(173, 186, 204),
            new Point(center.X - radius, center.Y + 16), FontWeights.Medium, TextAlignment.Center, radius * 2, 18);

        Rect resetCard = new Rect(safe.Left, safe.Top + 142, safe.Width, 54);
        canvas.RoundedGradientRect(resetCard, 13, Color.FromRgb(38, 35, 66), Color.FromRgb(24, 30, 53), Color.FromRgb(72, 76, 119));
        canvas.Text("下一次重置", 10.5, Color.FromRgb(161, 174, 203), new Point(resetCard.Left + 10, resetCard.Top + 6), FontWeights.SemiBold);
        string resetText = FormatReset(quota);
        canvas.FittedText(string.IsNullOrEmpty(resetText) ? "暂未提供重置时间" : resetText, 16, 12, Colors.White,
            new Point(resetCard.Left + 10, resetCard.Top + 24), FontWeights.SemiBold, TextAlignment.Left, resetCard.Width - 20, 21);

        DrawCodexTasks(canvas, safe, snapshot.CodexTasks);
    }

    private static void DrawArc(ScreenCanvas canvas, Point center, double radius, double percent, Color color)
    {
        double clampedPercent = Math.Clamp(percent, 0d, 100d);
        if (clampedPercent <= 0)
        {
            return;
        }

        if (clampedPercent >= 100)
        {
            canvas.Ellipse(new Rect(center.X - radius, center.Y - radius, radius * 2, radius * 2), Color.FromArgb(0, 0, 0, 0), color, 10);
            return;
        }

        double sweepDegrees = Math.Max(3d, 360d * clampedPercent / 100d);
        Point start = PointOnCircle(center, radius, -90);
        Point end = PointOnCircle(center, radius, -90 + sweepDegrees);
        var geometry = new StreamGeometry();
        using (StreamGeometryContext context = geometry.Open())
        {
            context.BeginFigure(start, false, false);
            context.ArcTo(end, new Size(radius, radius), 0, sweepDegrees > 180, SweepDirection.Clockwise, true, false);
        }
        geometry.Freeze();
        canvas.Path(geometry, color, 10);
    }

    private static Point PointOnCircle(Point center, double radius, double degrees)
    {
        double radians = degrees * Math.PI / 180d;
        return new Point(center.X + radius * Math.Cos(radians), center.Y + radius * Math.Sin(radians));
    }

    private static void DrawCodexTasks(ScreenCanvas canvas, Rect safe, CodexTaskSnapshot? tasks)
    {
        IReadOnlyList<CodexTaskItem> displayTasks = tasks?.GetDisplayTasks(2) ?? [];
        const double cardHeight = 68;
        const double cardGap = 8;
        double firstCardTop = safe.Bottom - (cardHeight * 2 + cardGap);
        for (int index = 0; index < 2; index++)
        {
            CodexTaskItem? task = CodexTaskCardRenderer.GetTask(displayTasks, index);
            if (tasks is null)
            {
                task = new CodexTaskItem("正在获取 Codex 任务", CodexTaskStatus.Loading, DateTimeOffset.MinValue);
            }
            else if (!tasks.Available)
            {
                task = new CodexTaskItem("任务状态暂不可用", CodexTaskStatus.Unavailable, DateTimeOffset.MinValue);
            }

            var card = new Rect(safe.Left, firstCardTop + index * (cardHeight + cardGap), safe.Width, cardHeight);
            CodexTaskCardRenderer.Draw(canvas, card, task);
        }
    }

    private static string FormatReset(AiQuotaSnapshot quota)
    {
        if (!quota.Available)
        {
            return string.Empty;
        }

        return quota.ResetsAt is { } resetsAt
            ? resetsAt.ToLocalTime().ToString("MM月dd日 HH:mm")
            : string.Empty;
    }

}
