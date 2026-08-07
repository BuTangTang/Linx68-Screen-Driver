using System;
using System.Windows;
using System.Windows.Media;

namespace Linx68.ScreenDriver.Core;

public enum MusicPresentationStyle
{
    CoverFocus,
    LyricFocus,
    Pulse
}

public sealed class MusicPresentationTheme(MusicPresentationStyle style, Func<double> lyricOffsetSeconds) : IScreenTheme
{
    public string Id => style switch
    {
        MusicPresentationStyle.CoverFocus => "music-cover-focus",
        MusicPresentationStyle.LyricFocus => "music-lyric-focus",
        _ => "music-pulse"
    };

    public string DisplayName => style switch
    {
        MusicPresentationStyle.CoverFocus => "封面聚焦",
        MusicPresentationStyle.LyricFocus => "歌词沉浸",
        _ => "律动播放器"
    };

    public string Description => style switch
    {
        MusicPresentationStyle.CoverFocus => "大封面与单句歌词",
        MusicPresentationStyle.LyricFocus => "放大当前歌词与下一句",
        _ => "封面、节奏条与播放信息"
    };

    public string Details => "读取 Windows 媒体会话；可显示网易云封面回退和逐行同步歌词。";

    public void Draw(ScreenCanvas canvas, SystemSnapshot snapshot)
    {
        MusicSnapshot music = snapshot.Music ?? MusicSnapshot.Unavailable;
        Rect safe = canvas.SafeBounds;
        switch (style)
        {
            case MusicPresentationStyle.CoverFocus:
                DrawCoverFocus(canvas, music, safe);
                break;
            case MusicPresentationStyle.LyricFocus:
                DrawLyricFocus(canvas, music, safe);
                break;
            default:
                DrawPulse(canvas, music, safe);
                break;
        }
    }

    private void DrawCoverFocus(ScreenCanvas canvas, MusicSnapshot music, Rect safe)
    {
        Color background = Color.FromRgb(11, 14, 20);
        Color surface = Color.FromRgb(23, 29, 38);
        Color secondary = Color.FromRgb(154, 165, 181);
        Color accent = canvas.AccentColor;
        canvas.Fill(background);
        DrawHeader(canvas, music, safe, secondary, accent);

        Rect artworkBounds = new(safe.Left, safe.Top + 27, safe.Width, 132);
        DrawArtwork(canvas, music, artworkBounds, 12, surface, accent);
        double detailsTop = artworkBounds.Bottom + 11;
        canvas.Text(music.Title, 14, Colors.White, new Point(safe.Left, detailsTop), FontWeights.SemiBold,
            TextAlignment.Left, safe.Width, 32);
        canvas.Text(ArtistText(music), 10.5, secondary, new Point(safe.Left, detailsTop + 35), FontWeights.Normal,
            TextAlignment.Left, safe.Width, 18);

        Rect lyricBounds = new(safe.Left, detailsTop + 63, safe.Width, 54);
        canvas.RoundedRect(lyricBounds, 10, surface, Color.FromRgb(46, 55, 67));
        canvas.Text(CurrentLyric(music), 11, accent, new Point(lyricBounds.Left + 8, lyricBounds.Top + 9), FontWeights.SemiBold,
            TextAlignment.Center, lyricBounds.Width - 16, 28);
        canvas.Text(NextLyric(music), 9, secondary, new Point(lyricBounds.Left + 8, lyricBounds.Top + 33), FontWeights.Normal,
            TextAlignment.Center, lyricBounds.Width - 16, 15);
        DrawProgress(canvas, music, safe, surface, accent);
    }

    private void DrawLyricFocus(ScreenCanvas canvas, MusicSnapshot music, Rect safe)
    {
        Color background = Color.FromRgb(14, 12, 19);
        Color surface = Color.FromRgb(33, 27, 42);
        Color secondary = Color.FromRgb(171, 159, 187);
        Color accent = canvas.AccentColor;
        canvas.Fill(background);
        DrawHeader(canvas, music, safe, secondary, accent);

        Rect artworkBounds = new(safe.Left + 4, safe.Top + 30, 48, 48);
        DrawArtwork(canvas, music, artworkBounds, 24, surface, accent);
        canvas.Text(music.Title, 11, Colors.White, new Point(artworkBounds.Right + 10, artworkBounds.Top + 5), FontWeights.SemiBold,
            TextAlignment.Left, safe.Right - artworkBounds.Right - 10, 22);
        canvas.Text(ArtistText(music), 9.5, secondary, new Point(artworkBounds.Right + 10, artworkBounds.Top + 29), FontWeights.Normal,
            TextAlignment.Left, safe.Right - artworkBounds.Right - 10, 16);

        Rect lyricBounds = new(safe.Left + 4, safe.Top + 104, safe.Width - 8, 136);
        canvas.RoundedRect(lyricBounds, 16, surface, Color.FromRgb(59, 49, 73));
        canvas.Text("当前歌词", 9.5, secondary, new Point(lyricBounds.Left + 12, lyricBounds.Top + 12), FontWeights.SemiBold);
        canvas.Text(CurrentLyric(music), 18, Colors.White, new Point(lyricBounds.Left + 12, lyricBounds.Top + 42), FontWeights.SemiBold,
            TextAlignment.Center, lyricBounds.Width - 24, 48);
        canvas.Text(NextLyric(music), 11, accent, new Point(lyricBounds.Left + 12, lyricBounds.Top + 100), FontWeights.Medium,
            TextAlignment.Center, lyricBounds.Width - 24, 22);
        DrawProgress(canvas, music, safe, surface, accent);
    }

    private void DrawPulse(ScreenCanvas canvas, MusicSnapshot music, Rect safe)
    {
        Color background = Color.FromRgb(8, 18, 21);
        Color surface = Color.FromRgb(20, 38, 42);
        Color secondary = Color.FromRgb(143, 171, 177);
        Color accent = canvas.AccentColor;
        canvas.Fill(background);
        DrawHeader(canvas, music, safe, secondary, accent);

        Rect artworkBounds = new(safe.Left + 22, safe.Top + 42, safe.Width - 44, 98);
        DrawArtwork(canvas, music, artworkBounds, 14, surface, accent);
        DrawPulseBars(canvas, music, safe, accent, secondary);
        double detailsTop = artworkBounds.Bottom + 14;
        canvas.Text(music.Title, 14, Colors.White, new Point(safe.Left, detailsTop), FontWeights.SemiBold,
            TextAlignment.Center, safe.Width, 28);
        canvas.Text(ArtistText(music), 10, secondary, new Point(safe.Left, detailsTop + 29), FontWeights.Normal,
            TextAlignment.Center, safe.Width, 18);

        Rect lyricBounds = new(safe.Left, detailsTop + 58, safe.Width, 43);
        canvas.RoundedRect(lyricBounds, 9, surface);
        canvas.Text(CurrentLyric(music), 10.5, accent, new Point(lyricBounds.Left + 8, lyricBounds.Top + 12), FontWeights.SemiBold,
            TextAlignment.Center, lyricBounds.Width - 16, 20);
        DrawProgress(canvas, music, safe, surface, accent);
    }

    private static void DrawHeader(ScreenCanvas canvas, MusicSnapshot music, Rect safe, Color secondary, Color accent)
    {
        canvas.Text("正在播放", 9, secondary, new Point(safe.Left, safe.Top + 5), FontWeights.SemiBold);
        canvas.Text(SourceName(music.SourceAppId), 9, accent, new Point(safe.Left, safe.Top + 5), FontWeights.SemiBold,
            TextAlignment.Right, safe.Width);
    }

    private static void DrawArtwork(ScreenCanvas canvas, MusicSnapshot music, Rect bounds, double radius, Color surface, Color accent)
    {
        if (music.Artwork is { Length: > 0 })
        {
            canvas.Image(music.Artwork, bounds, radius);
            return;
        }

        canvas.RoundedRect(bounds, radius, surface, Color.FromArgb(130, 255, 255, 255));
        canvas.CenteredText("音乐", 13, accent, bounds, FontWeights.SemiBold);
    }

    private void DrawProgress(ScreenCanvas canvas, MusicSnapshot music, Rect safe, Color surface, Color accent)
    {
        double top = safe.Bottom - 21;
        canvas.ProgressBar(new Rect(safe.Left, top, safe.Width, 5), ProgressPercent(music), surface, accent);
        canvas.Text(FormatTime(music.Position), 9.5, canvas.AccentColor, new Point(safe.Left, top + 10), FontWeights.SemiBold);
        canvas.Text(music.Duration > TimeSpan.Zero ? FormatTime(music.Duration) : "播放中", 9.5, Color.FromRgb(157, 168, 180),
            new Point(safe.Left, top + 10), FontWeights.Medium, TextAlignment.Right, safe.Width);
    }

    private static void DrawPulseBars(ScreenCanvas canvas, MusicSnapshot music, Rect safe, Color accent, Color secondary)
    {
        double phase = music.IsPlaying ? music.Position.TotalSeconds * 5 : 0;
        for (int index = 0; index < 5; index++)
        {
            double wave = 0.35 + 0.65 * Math.Abs(Math.Sin(phase + index * 0.86));
            double height = 20 + wave * 58;
            double x = index < 2 ? safe.Left + index * 7 : safe.Right - (5 - index) * 7;
            double y = safe.Top + 138 - height;
            canvas.RoundedRect(new Rect(x, y, 4, height), 2, index % 2 == 0 ? accent : secondary);
        }
    }

    private string CurrentLyric(MusicSnapshot music)
    {
        (LyricLine? current, _) = music.Lyrics.FindAt(music.Position, lyricOffsetSeconds());
        return current?.Text?.Trim() is { Length: > 0 } lyric ? lyric : "暂无同步歌词";
    }

    private string NextLyric(MusicSnapshot music)
    {
        (_, LyricLine? next) = music.Lyrics.FindAt(music.Position, lyricOffsetSeconds());
        return next?.Text?.Trim() is { Length: > 0 } lyric ? lyric : "等待下一句";
    }

    private static double ProgressPercent(MusicSnapshot music) => music.Duration.TotalSeconds > 0
        ? Math.Clamp(music.Position.TotalSeconds / music.Duration.TotalSeconds * 100, 0, 100)
        : music.IsPlaying ? 100 : 0;

    private static string ArtistText(MusicSnapshot music) => string.IsNullOrWhiteSpace(music.Artist)
        ? "Windows Media"
        : music.Artist;

    private static string SourceName(string sourceAppId)
    {
        if (sourceAppId.Contains("cloudmusic", StringComparison.OrdinalIgnoreCase)
            || sourceAppId.Contains("netease", StringComparison.OrdinalIgnoreCase)) return "网易云";
        if (sourceAppId.Contains("spotify", StringComparison.OrdinalIgnoreCase)) return "Spotify";
        if (sourceAppId.Contains("qqmusic", StringComparison.OrdinalIgnoreCase)) return "QQ 音乐";
        return "音乐";
    }

    private static string FormatTime(TimeSpan value) => $"{(int)value.TotalMinutes}:{value.Seconds:00}";
}
