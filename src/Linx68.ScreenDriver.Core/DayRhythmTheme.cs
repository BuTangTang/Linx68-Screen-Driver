using System.Windows;
using System.Windows.Media;

namespace Linx68.ScreenDriver.Core;

public sealed class DayRhythmTheme : IScreenTheme
{
    public string Id => "day-rhythm";

    public string DisplayName => "今日节奏";

    public string Description => "今日与本周进度一眼掌握";

    public string Details => "按当前本地时间展示今日进度、星期进度和当前时段提示，不需要任何在线服务。";

    public void Draw(ScreenCanvas canvas, SystemSnapshot snapshot)
    {
        Rect safe = canvas.SafeBounds;
        DateTimeOffset now = snapshot.Timestamp;
        Color primary = Colors.White;
        Color secondary = Color.FromRgb(157, 166, 188);
        Color surface = Color.FromRgb(25, 24, 43);
        Color stroke = Color.FromRgb(57, 54, 83);
        double dayProgress = now.TimeOfDay.TotalSeconds / TimeSpan.FromDays(1).TotalSeconds * 100;
        int mondayIndex = ((int)now.DayOfWeek + 6) % 7;
        double weekProgress = (mondayIndex + now.TimeOfDay.TotalDays) / 7d * 100;

        canvas.Gradient(Color.FromRgb(18, 15, 34), Color.FromRgb(31, 24, 48), new Point(0, 0), new Point(1, 1));
        canvas.Ellipse(new Rect(safe.Left, safe.Top + 8, 6, 6), canvas.AccentColor);
        canvas.Text("今日节奏", 10.5, secondary, new Point(safe.Left + 11, safe.Top + 4), FontWeights.SemiBold);
        canvas.Text(now.ToString("ddd"), 10.5, primary, new Point(safe.Left, safe.Top + 4), FontWeights.SemiBold, TextAlignment.Right, safe.Width);

        canvas.AlignedText(now.ToString("HH:mm"), 43, primary,
            new Rect(safe.Left, safe.Top + 43, safe.Width, 61), FontWeights.SemiBold, TextAlignment.Center);
        canvas.AlignedText(now.ToString("M月d日  dddd"), 12, secondary,
            new Rect(safe.Left, safe.Top + 108, safe.Width, 20), FontWeights.Medium, TextAlignment.Center);

        Rect phaseCard = new(safe.Left, safe.Top + 138, safe.Width, 72);
        canvas.RoundedGradientRect(phaseCard, 13, Color.FromRgb(46, 35, 68), surface, stroke);
        canvas.CenteredText(GetPhaseTitle(now.Hour), 16, canvas.AccentColor,
            new Rect(phaseCard.Left + 8, phaseCard.Top + 10, phaseCard.Width - 16, 23), FontWeights.Bold);
        canvas.CenteredText(GetPhaseHint(now.Hour), 10, secondary,
            new Rect(phaseCard.Left + 8, phaseCard.Top + 39, phaseCard.Width - 16, 17), FontWeights.Medium);

        DrawProgressCard(canvas, new Rect(safe.Left, safe.Top + 222, safe.Width, 58), "今天", dayProgress, $"{dayProgress:0}%", primary, secondary, surface, stroke);
        DrawProgressCard(canvas, new Rect(safe.Left, safe.Top + 292, safe.Width, 58), "本周", weekProgress, $"第 {mondayIndex + 1} 天", primary, secondary, surface, stroke);
    }

    private static void DrawProgressCard(ScreenCanvas canvas, Rect card, string label, double progress, string value, Color primary, Color secondary, Color surface, Color stroke)
    {
        canvas.RoundedRect(card, 11, surface, stroke);
        canvas.Text(label, 10, secondary, new Point(card.Left + 11, card.Top + 8), FontWeights.SemiBold);
        canvas.Text(value, 11, primary, new Point(card.Left, card.Top + 7), FontWeights.Bold, TextAlignment.Right, card.Width - 11);
        canvas.ProgressBar(new Rect(card.Left + 11, card.Top + 35, card.Width - 22, 7), progress, Color.FromRgb(57, 54, 77), canvas.AccentColor);
    }

    private static string GetPhaseTitle(int hour) => hour switch
    {
        < 5 => "安静时刻",
        < 9 => "晨间启动",
        < 12 => "专注高峰",
        < 14 => "午间缓冲",
        < 18 => "稳定推进",
        < 22 => "晚间收束",
        _ => "准备休息"
    };

    private static string GetPhaseHint(int hour) => hour switch
    {
        < 5 => "给睡眠留足空间",
        < 9 => "先完成一件最重要的事",
        < 12 => "保护这段不被打断的时间",
        < 14 => "吃饭、走动，也让大脑透气",
        < 18 => "按节奏推进，不必一次做完",
        < 22 => "复盘今天，给明天留个起点",
        _ => "把屏幕放下，慢慢收心"
    };
}
