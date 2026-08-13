using System.Windows;
using System.Windows.Media;

namespace Linx68.ScreenDriver.Core;

public sealed class MusicCassetteTheme : IScreenTheme
{
    private readonly Func<double> _lyricOffsetSeconds;

    public MusicCassetteTheme(Func<double>? lyricOffsetSeconds = null)
    {
        _lyricOffsetSeconds = lyricOffsetSeconds ?? (static () => 0);
    }

    public string Id => "music-cassette";

    public string DisplayName => "磁带余晖";

    public string Description => "复古磁带与歌词进度";

    public string Details => "将媒体会话化为一盒会随进度转动的磁带，保留歌名、艺人和当前歌词。";

    public void Draw(ScreenCanvas canvas, SystemSnapshot snapshot)
    {
        MusicSnapshot music = snapshot.Music ?? MusicSnapshot.Unavailable;
        Rect safe = canvas.SafeBounds;
        Color coral = Color.FromRgb(244, 126, 103);
        Color mint = Color.FromRgb(116, 226, 193);
        Color primary = Color.FromRgb(246, 242, 234);
        Color secondary = Color.FromRgb(169, 174, 191);
        Color surface = Color.FromRgb(27, 29, 51);
        Color stroke = Color.FromRgb(68, 67, 96);

        canvas.Gradient(Color.FromRgb(10, 12, 31), Color.FromRgb(34, 20, 42), new Point(0, 0), new Point(1, 1));
        canvas.Ellipse(new Rect(safe.Left, safe.Top + 8, 6, 6), music.Available && music.IsPlaying ? mint : coral);
        canvas.Text(music.Available ? music.IsPlaying ? "正在播放" : "已暂停" : "等待播放器", 9.5, secondary,
            new Point(safe.Left + 11, safe.Top + 4), FontWeights.SemiBold);
        canvas.Text(snapshot.Timestamp.ToString("HH:mm"), 10.5, primary,
            new Point(safe.Left, safe.Top + 4), FontWeights.SemiBold, TextAlignment.Right, safe.Width);

        Rect cassette = new(safe.Left, safe.Top + 34, safe.Width, 145);
        canvas.RoundedGradientRect(cassette, 15, Color.FromRgb(57, 48, 76), surface, Color.FromRgb(101, 82, 111), 1.2);
        canvas.RoundedRect(new Rect(cassette.Left + 9, cassette.Top + 11, cassette.Width - 18, 39), 8,
            canvas.AccentColor, canvas.AccentColor);
        canvas.CenteredText(music.Available ? "SIDE A  ·  LINX MIX" : "READY  ·  INSERT MUSIC", 8.5,
            Colors.White, new Rect(cassette.Left + 13, cassette.Top + 16, cassette.Width - 26, 13), FontWeights.Bold);
        canvas.Line(new Point(cassette.Left + 15, cassette.Top + 39), new Point(cassette.Right - 15, cassette.Top + 39), Colors.White, 2);

        Rect window = new(cassette.Left + 14, cassette.Top + 60, cassette.Width - 28, 55);
        canvas.RoundedRect(window, 15, Color.FromRgb(13, 16, 29), Color.FromRgb(84, 84, 111));
        bool hasKnownDuration = music.Duration.TotalSeconds > 0;
        double progress = hasKnownDuration
            ? Math.Clamp(music.Position.TotalSeconds / music.Duration.TotalSeconds, 0, 1)
            : 0;
        double reelPhase = hasKnownDuration
            ? progress
            : music.IsPlaying
                ? (music.Position > TimeSpan.Zero ? music.Position.TotalSeconds : snapshot.Timestamp.TimeOfDay.TotalSeconds) / 13
                : music.Position.TotalSeconds / 13;
        DrawReel(canvas, new Point(window.Left + 26, window.Top + window.Height / 2), 15 - progress * 4, reelPhase, mint);
        DrawReel(canvas, new Point(window.Right - 26, window.Top + window.Height / 2), 11 + progress * 4, -reelPhase, coral);
        canvas.RoundedRect(new Rect(window.Left + 44, window.Top + 21, window.Width - 88, 13), 6.5,
            Color.FromRgb(224, 211, 185), Color.FromRgb(126, 116, 111));
        canvas.RoundedRect(new Rect(cassette.Left + 24, cassette.Bottom - 19, cassette.Width - 48, 12), 4,
            Color.FromRgb(16, 18, 31), Color.FromRgb(77, 76, 100));

        double titleTop = cassette.Bottom + 11;
        canvas.FittedText(music.Available ? music.Title : "没有正在播放的音乐", 14, 10.5, primary,
            new Point(safe.Left, titleTop), FontWeights.SemiBold, TextAlignment.Center, safe.Width, 34);
        canvas.Text(music.Available && !string.IsNullOrWhiteSpace(music.Artist) ? music.Artist : "Windows Media",
            9.5, secondary, new Point(safe.Left, titleTop + 35), FontWeights.Medium, TextAlignment.Center, safe.Width, 15);

        string lyric = ResolveLyric(music);
        Rect lyricCard = new(safe.Left, safe.Bottom - 91, safe.Width, 54);
        canvas.RoundedRect(lyricCard, 10, Color.FromArgb(205, 24, 26, 47), Color.FromRgb(66, 64, 91));
        string activityLabel = !music.Available
            ? "WAITING FOR MUSIC"
            : music.Lyrics.Available ? "NOW SINGING" : "NOW PLAYING";
        canvas.Text(activityLabel, 7.5, music.Lyrics.Available ? mint : coral,
            new Point(lyricCard.Left + 8, lyricCard.Top + 6), FontWeights.Bold);
        canvas.WrappedText(lyric, 11.5, primary, new Point(lyricCard.Left + 8, lyricCard.Top + 20), FontWeights.SemiBold,
            TextAlignment.Center, lyricCard.Width - 16, 2, 14);

        double progressTop = safe.Bottom - 24;
        canvas.ProgressBar(new Rect(safe.Left, progressTop, safe.Width, 5), progress * 100,
            Color.FromRgb(49, 48, 71), music.IsPlaying ? mint : coral);
        canvas.Text(FormatTime(music.Position), 8.5, secondary, new Point(safe.Left, progressTop + 9), FontWeights.Medium);
        canvas.Text(music.Duration > TimeSpan.Zero ? FormatTime(music.Duration) : "--:--", 8.5, secondary,
            new Point(safe.Left, progressTop + 9), FontWeights.Medium, TextAlignment.Right, safe.Width);
    }

    private static void DrawReel(ScreenCanvas canvas, Point center, double radius, double phase, Color accent)
    {
        canvas.Ellipse(new Rect(center.X - radius, center.Y - radius, radius * 2, radius * 2),
            Color.FromRgb(224, 214, 193), Color.FromRgb(111, 104, 105), 1.2);
        canvas.Ellipse(new Rect(center.X - 4, center.Y - 4, 8, 8), Color.FromRgb(20, 22, 35), accent);
        for (int index = 0; index < 4; index++)
        {
            double angle = phase * Math.PI * 8 + index * Math.PI / 2;
            double x = center.X + Math.Cos(angle) * Math.Max(5, radius - 5) - 2;
            double y = center.Y + Math.Sin(angle) * Math.Max(5, radius - 5) - 2;
            canvas.Ellipse(new Rect(x, y, 4, 4), Color.FromRgb(63, 59, 69));
        }
    }

    private string ResolveLyric(MusicSnapshot music)
    {
        if (!music.Available)
        {
            return "打开音乐播放器后，磁带会自动转动";
        }

        (LyricLine? _, LyricLine? current, LyricLine? next) = music.Lyrics.FindContextAt(music.Position, _lyricOffsetSeconds());
        return current?.Text ?? next?.Text ?? "歌词准备中";
    }

    private static string FormatTime(TimeSpan value) => $"{(int)value.TotalMinutes}:{value.Seconds:00}";
}
