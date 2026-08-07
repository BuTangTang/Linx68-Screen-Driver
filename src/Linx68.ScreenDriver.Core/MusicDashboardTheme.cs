using System;
using System.Windows;
using System.Windows.Media;

namespace Linx68.ScreenDriver.Core;

public enum MusicDashboardStyle
{
	Vinyl,
	Cassette
}

public sealed class MusicDashboardTheme(MusicDashboardStyle style, Func<double> lyricOffsetSeconds) : IScreenTheme
{
	public string Id => style == MusicDashboardStyle.Vinyl ? "music-vinyl" : "music-cassette";

	public string DisplayName => style == MusicDashboardStyle.Vinyl ? "动态黑胶" : "动态磁带";

	public string Description => style == MusicDashboardStyle.Vinyl
		? "旋转黑胶、同步歌词与播放进度"
		: "磁带转轮、同步歌词与播放进度";

	public string Details => "读取 Windows 媒体会话；可选使用 LRCLIB 获取同步歌词。建议设置为 1–2 秒刷新。";

	public void Draw(ScreenCanvas canvas, SystemSnapshot snapshot)
	{
		MusicSnapshot music = snapshot.Music ?? MusicSnapshot.Unavailable;
		Rect safe = canvas.SafeBounds;
		Color background = Color.FromRgb(9, 11, 15);
		Color surface = Color.FromRgb(21, 25, 32);
		Color secondary = Color.FromRgb(139, 149, 161);
		Color accent = canvas.AccentColor;
		canvas.Fill(background);
		canvas.Text("NOW PLAYING", 9, secondary, new Point(safe.Left + 2, safe.Top + 5), FontWeights.SemiBold);
		canvas.Text(ResolveSourceName(music.SourceAppId), 9, accent, new Point(safe.Left, safe.Top + 5), FontWeights.SemiBold, TextAlignment.Right, safe.Width);

		double visualTop = safe.Top + 26;
		if (style == MusicDashboardStyle.Vinyl)
		{
			DrawVinyl(canvas, music, visualTop, accent);
		}
		else
		{
			DrawCassette(canvas, music, visualTop, surface, accent);
		}

		double titleTop = visualTop + 122;
		canvas.Text(music.Title, 13.5, Colors.White, new Point(safe.Left, titleTop), FontWeights.SemiBold, TextAlignment.Left, safe.Width, 34);
		canvas.Text(string.IsNullOrWhiteSpace(music.Artist) ? "Windows Media" : music.Artist, 10.5, secondary, new Point(safe.Left, titleTop + 35), FontWeights.Normal, TextAlignment.Left, safe.Width, 18);
		DrawLyrics(canvas, music, new Rect(safe.Left, titleTop + 58, safe.Width, 63), surface, secondary, accent);

		bool live = music.Duration.TotalSeconds <= 0;
		double percent = live ? (music.IsPlaying ? 100 : 0) : music.Position.TotalSeconds / music.Duration.TotalSeconds * 100;
		double progressTop = titleTop + 130;
		canvas.ProgressBar(new Rect(safe.Left, progressTop, safe.Width, 5), percent, Color.FromRgb(38, 44, 53), accent);
		canvas.Text(live ? "LIVE" : FormatTime(music.Position), 10, live ? accent : secondary, new Point(safe.Left, progressTop + 11), FontWeights.SemiBold);
		canvas.Text(live ? (music.IsPlaying ? "ON AIR" : "PAUSED") : FormatTime(music.Duration), 10, secondary, new Point(safe.Left, progressTop + 11), FontWeights.SemiBold, TextAlignment.Right, safe.Width);
	}

	private static void DrawVinyl(ScreenCanvas canvas, MusicSnapshot music, double top, Color accent)
	{
		const double size = 116;
		double left = (canvas.Profile.Width - size) / 2;
		Rect disc = new Rect(left, top, size, size);
		Point center = new(disc.Left + disc.Width / 2, disc.Top + disc.Height / 2);
		canvas.Ellipse(disc, Color.FromRgb(13, 15, 19), Color.FromRgb(48, 53, 61));
		for (int inset = 8; inset <= 22; inset += 7)
		{
			canvas.Ellipse(new Rect(disc.Left + inset, disc.Top + inset, disc.Width - inset * 2, disc.Height - inset * 2), Colors.Transparent, Color.FromRgb(31, 35, 42), 0.7);
		}

		double angle = music.IsPlaying ? music.Position.TotalSeconds * 45 % 360 : 0;
		if (music.Artwork is { Length: > 0 })
		{
			canvas.CircularImage(music.Artwork, new Rect(center.X - 36, center.Y - 36, 72, 72), angle);
		}
		else
		{
			canvas.Ellipse(new Rect(center.X - 36, center.Y - 36, 72, 72), accent);
		}
		canvas.Ellipse(new Rect(center.X - 5, center.Y - 5, 10, 10), Color.FromRgb(9, 11, 15), Color.FromArgb(90, 255, 255, 255));

		double radians = angle * Math.PI / 180;
		Point markerStart = new(center.X + Math.Cos(radians) * 43, center.Y + Math.Sin(radians) * 43);
		Point markerEnd = new(center.X + Math.Cos(radians) * 52, center.Y + Math.Sin(radians) * 52);
		canvas.Line(markerStart, markerEnd, accent, 2);
	}

	private static void DrawCassette(ScreenCanvas canvas, MusicSnapshot music, double top, Color surface, Color accent)
	{
		Rect shell = new Rect(11, top + 9, 120, 96);
		canvas.RoundedRect(shell, 8, Color.FromRgb(29, 32, 39), Color.FromRgb(72, 78, 88));
		Rect label = new Rect(shell.Left + 10, shell.Top + 9, shell.Width - 20, 36);
		if (music.Artwork is { Length: > 0 })
		{
			canvas.Image(music.Artwork, label, 4);
		}
		else
		{
			canvas.RoundedRect(label, 4, surface);
		}
		canvas.RoundedRect(new Rect(shell.Left + 15, shell.Top + 52, shell.Width - 30, 31), 14, Color.FromRgb(15, 17, 22), Color.FromRgb(67, 72, 82));
		double angle = music.IsPlaying ? music.Position.TotalSeconds * 90 % 360 : 0;
		DrawReel(canvas, new Point(shell.Left + 38, shell.Top + 67.5), angle, accent);
		DrawReel(canvas, new Point(shell.Right - 38, shell.Top + 67.5), -angle, accent);
		canvas.Line(new Point(shell.Left + 48, shell.Top + 67.5), new Point(shell.Right - 48, shell.Top + 67.5), Color.FromRgb(92, 98, 108), 2);
		canvas.Text("LINX 68", 7.5, Color.FromArgb(170, 255, 255, 255), new Point(label.Left + 4, label.Top + 4), FontWeights.SemiBold);
	}

	private static void DrawReel(ScreenCanvas canvas, Point center, double angle, Color accent)
	{
		canvas.Ellipse(new Rect(center.X - 11, center.Y - 11, 22, 22), Color.FromRgb(35, 39, 47), Color.FromRgb(109, 116, 128));
		for (int index = 0; index < 3; index++)
		{
			double radians = (angle + index * 120) * Math.PI / 180;
			Point end = new(center.X + Math.Cos(radians) * 8, center.Y + Math.Sin(radians) * 8);
			canvas.Line(center, end, index == 0 ? accent : Color.FromRgb(130, 137, 149), 1.5);
		}
		canvas.Ellipse(new Rect(center.X - 2, center.Y - 2, 4, 4), Color.FromRgb(12, 14, 18));
	}

	private void DrawLyrics(ScreenCanvas canvas, MusicSnapshot music, Rect bounds, Color surface, Color secondary, Color accent)
	{
		canvas.RoundedRect(bounds, 8, surface, Color.FromRgb(38, 44, 53));
		(LyricLine? current, LyricLine? next) = music.Lyrics.FindAt(music.Position, lyricOffsetSeconds());
		if (current is null && next is null)
		{
			canvas.CenteredText(music.Lyrics.ErrorMessage is null ? "暂无同步歌词" : "歌词暂时不可用", 10.5, secondary, bounds, FontWeights.Medium);
			return;
		}

		string currentText = current?.Text ?? "…";
		canvas.Text(currentText, 11.5, accent, new Point(bounds.Left + 8, bounds.Top + 8), FontWeights.SemiBold, TextAlignment.Center, bounds.Width - 16, 25);
		if (next is not null)
		{
			canvas.Text(next.Text, 9.5, secondary, new Point(bounds.Left + 8, bounds.Top + 36), FontWeights.Normal, TextAlignment.Center, bounds.Width - 16, 18);
		}
	}

	private static string ResolveSourceName(string sourceAppId)
	{
		if (string.IsNullOrWhiteSpace(sourceAppId)) return "WINDOWS";
		string value = sourceAppId.ToUpperInvariant();
		if (value.Contains("SPOTIFY")) return "SPOTIFY";
		if (value.Contains("CLOUDMUSIC") || value.Contains("NETEASE")) return "网易云";
		if (value.Contains("QQMUSIC")) return "QQ 音乐";
		if (value.Contains("KUGOU")) return "酷狗";
		if (value.Contains("CHROME")) return "CHROME";
		if (value.Contains("MSEDGE")) return "EDGE";
		if (value.Contains("FIREFOX")) return "FIREFOX";
		return "WINDOWS";
	}

	private static string FormatTime(TimeSpan value) => $"{(int)value.TotalMinutes}:{value.Seconds:00}";
}
