using System.Windows;
using System.Windows.Media;

namespace Linx68.ScreenDriver.Core;

public sealed class ClockTheme : IScreenTheme
{
    public string Id => "clock";
    public string DisplayName => "极简时钟";
    public string Description => "时间、秒钟、中文日期与可定制底部文字";
    public string Details => "秒钟与分钟基线对齐；日期使用无前导零的中文格式。";

    public void Draw(ScreenCanvas canvas, SystemSnapshot snapshot)
    {
        var background = Color.FromRgb(7, 9, 12);
        var muted = Color.FromRgb(154, 165, 177);
        var accent = canvas.AccentColor;
        var safe = canvas.SafeBounds;
        canvas.Fill(background);

        canvas.Text("本地时间", 13, muted,
            new Point(safe.Left, safe.Top + 8), FontWeights.SemiBold);
        canvas.Line(new Point(safe.Left, safe.Top + 38),
            new Point(safe.Left + 48, safe.Top + 38), Color.FromRgb(43, 50, 58), 2);

        canvas.AlignedText(snapshot.Timestamp.ToString("HH:mm"), 39, Colors.White,
            new Rect(safe.Left + 12, safe.Top + 64, safe.Width - 12, 58),
            FontWeights.SemiBold, TextAlignment.Right);
        canvas.AlignedText(snapshot.Timestamp.ToString("ss"), 20, accent,
            new Rect(safe.Left, safe.Top + 132, safe.Width, 28),
            FontWeights.SemiBold, TextAlignment.Right);

        var secondsTrack = new Rect(safe.Left + 12, safe.Top + 174, safe.Width - 12, 5);
        canvas.ProgressBar(secondsTrack, snapshot.Timestamp.Second / 59.0 * 100.0,
            Color.FromRgb(29, 35, 42), accent);

        canvas.Line(new Point(safe.Left, safe.Top + 230),
            new Point(safe.Left + 48, safe.Top + 230), Color.FromRgb(34, 41, 49), 2);
        canvas.AlignedText(snapshot.Timestamp.ToString("dddd"), 15, accent,
            new Rect(safe.Left, safe.Top + 216, safe.Width, 27),
            FontWeights.SemiBold, TextAlignment.Right);
        canvas.AlignedText(ChineseDate(snapshot.Timestamp), 16, Colors.White,
            new Rect(safe.Left, safe.Top + 254, safe.Width, 27),
            FontWeights.Medium, TextAlignment.Right);


    }
    private static string ChineseDate(DateTimeOffset value) =>
        $"{value.Year}年{value.Month}月{value.Day}日";
}