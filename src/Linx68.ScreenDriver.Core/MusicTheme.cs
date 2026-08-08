using System;
using System.Windows;
using System.Windows.Media;

namespace Linx68.ScreenDriver.Core;

public sealed class MusicTheme : IScreenTheme
{
	private readonly Func<double> _lyricOffsetSeconds;

	public MusicTheme(Func<double>? lyricOffsetSeconds = null)
	{
		_lyricOffsetSeconds = lyricOffsetSeconds ?? (static () => 0);
	}

	public string Id => "music";

	public string DisplayName => "封面歌词";

	public string Description => "封面与连续同步歌词";

	public string Details => "读取 Windows 媒体会话；显示封面、连续同步歌词与播放进度。";

	public void Draw(ScreenCanvas canvas, SystemSnapshot snapshot)
	{
		MusicSnapshot music = snapshot.Music ?? MusicSnapshot.Unavailable;
		Rect safe = canvas.SafeBounds;
		Color surface = Color.FromRgb(19, 25, 33);
		Color secondary = Color.FromRgb(155, 169, 184);
		Color accent = canvas.AccentColor;
		bool hasArtwork = music.Artwork is { Length: > 0 }
			&& canvas.TryImage(music.Artwork, new Rect(0, 0, canvas.Profile.Width, canvas.Profile.Height));
		if (hasArtwork)
		{
			canvas.RoundedRect(new Rect(0, 0, canvas.Profile.Width, canvas.Profile.Height), 0, Color.FromArgb(222, 6, 10, 15));
		}
		else
		{
			canvas.Gradient(Color.FromRgb(7, 10, 15), Color.FromRgb(14, 25, 34), new Point(0, 0), new Point(1, 1));
		}
		canvas.Ellipse(new Rect(safe.Left, safe.Top + 6, 6, 6), music.IsPlaying ? accent : secondary);
		canvas.Text(music.IsPlaying ? "正在播放" : "已暂停", 9, secondary, new Point(safe.Left + 11, safe.Top + 3), FontWeights.SemiBold);
		canvas.Text(ResolveSourceName(music.SourceAppId), 9, accent, new Point(safe.Left, safe.Top + 3), FontWeights.SemiBold,
			TextAlignment.Right, safe.Width);

		double artworkSize = Math.Min(safe.Width - 14, 112);
		Rect artwork = new(
			safe.Left + (safe.Width - artworkSize) / 2,
			safe.Top + 26,
			artworkSize,
			artworkSize);
		canvas.RoundedRect(new Rect(artwork.Left - 1, artwork.Top - 1, artwork.Width + 2, artwork.Height + 2), 15,
			Color.FromRgb(39, 50, 63), Color.FromArgb(120, 255, 255, 255));
		if (hasArtwork)
		{
			canvas.Image(music.Artwork!, artwork, 14);
		}
		else
		{
			DrawArtworkPlaceholder(canvas, artwork, accent);
		}

		double detailsTop = artwork.Bottom + 12;
		canvas.FittedText(music.Title, 14, 11, Colors.White, new Point(safe.Left, detailsTop), FontWeights.SemiBold,
			TextAlignment.Center, safe.Width, 28);
		canvas.Text(string.IsNullOrWhiteSpace(music.Artist) ? "Windows Media" : music.Artist, 10, secondary,
			new Point(safe.Left, detailsTop + 28), FontWeights.Normal, TextAlignment.Center, safe.Width, 16);

		(LyricLine? previous, LyricLine? current, LyricLine? next) = music.Lyrics.FindContextAt(music.Position, _lyricOffsetSeconds());
		bool isAwaitingFirstLyric = current is null && next is not null;
		string lyricLabel = isAwaitingFirstLyric ? "即将开始" : "当前歌词";
		string currentLyric = current?.Text ?? (isAwaitingFirstLyric ? next!.Text : "正在查找同步歌词");
		string previousLyric = current is null ? string.Empty : previous?.Text ?? string.Empty;
		string nextLyric = isAwaitingFirstLyric ? string.Empty : next?.Text ?? string.Empty;
		double lyricProgress = ResolveLyricProgress(music.Position, current, next, _lyricOffsetSeconds());
		double pulse = ResolvePulse(music.Position, music.IsPlaying);
		Rect lyrics = new(safe.Left, safe.Bottom - 150, safe.Width, 100);
		canvas.RoundedRect(lyrics, 12, surface, Blend(Color.FromRgb(44, 56, 70), accent, 0.18 + pulse * 0.14));
		DrawLyricActivity(canvas, lyrics, accent, lyricProgress, pulse, music.IsPlaying);
		canvas.Text(lyricLabel, 8.5, secondary, new Point(lyrics.Left + 9, lyrics.Top + 7), FontWeights.SemiBold);
		canvas.Text(previousLyric, 9, Color.FromRgb(116, 133, 149), new Point(lyrics.Left + 8, lyrics.Top + 20), FontWeights.Medium,
			TextAlignment.Center, lyrics.Width - 16, 13);
		canvas.WrappedText(currentLyric, 18, Colors.White, new Point(lyrics.Left + 8, lyrics.Top + 34), FontWeights.SemiBold,
			TextAlignment.Center, lyrics.Width - 16, 2, 22);
		canvas.Text(nextLyric, 9.5, accent, new Point(lyrics.Left + 8, lyrics.Top + 81), FontWeights.Medium,
			TextAlignment.Center, lyrics.Width - 16, 13);

		double progressTop = safe.Bottom - 27;
		double percent = music.Duration.TotalSeconds > 0
			? Math.Clamp(music.Position.TotalSeconds / music.Duration.TotalSeconds * 100, 0, 100)
			: music.IsPlaying ? 100 : 0;
		canvas.ProgressBar(new Rect(safe.Left, progressTop, safe.Width, 5), percent, Color.FromRgb(42, 54, 67), accent);
		canvas.Text(FormatTime(music.Position), 9, accent, new Point(safe.Left, progressTop + 9), FontWeights.SemiBold);
		canvas.Text(music.Duration > TimeSpan.Zero ? FormatTime(music.Duration) : "播放中", 9, secondary,
			new Point(safe.Left, progressTop + 9), FontWeights.Medium, TextAlignment.Right, safe.Width);
	}

	private static string ResolveSourceName(string sourceAppId)
	{
		if (sourceAppId.Contains("cloudmusic", StringComparison.OrdinalIgnoreCase)
			|| sourceAppId.Contains("netease", StringComparison.OrdinalIgnoreCase)) return "网易云";
		if (sourceAppId.Contains("spotify", StringComparison.OrdinalIgnoreCase)) return "Spotify";
		if (sourceAppId.Contains("qqmusic", StringComparison.OrdinalIgnoreCase)) return "QQ 音乐";
		return "音乐";
	}

	private static void DrawArtworkPlaceholder(ScreenCanvas canvas, Rect artwork, Color accent)
	{
		canvas.RoundedGradientRect(artwork, 14, Color.FromRgb(20, 61, 74), Color.FromRgb(13, 35, 59),
			Color.FromArgb(116, 118, 221, 202));
		double centerX = artwork.Left + artwork.Width / 2.0;
		double centerY = artwork.Top + artwork.Height / 2.0;
		Color note = Color.FromRgb(190, 247, 232);
		canvas.Ellipse(new Rect(centerX - 31, centerY - 31, 62, 62), Color.FromArgb(18, 185, 245, 229), Color.FromArgb(88, 185, 245, 229));

		double stemX = centerX + 8;
		double stemTop = artwork.Top + artwork.Height * 0.3;
		double stemBottom = artwork.Top + artwork.Height * 0.66;
		canvas.Line(new Point(stemX, stemTop), new Point(stemX, stemBottom), note, 2.5);
		canvas.Line(new Point(stemX, stemTop), new Point(stemX + 20, stemTop - 6), note, 2.5);
		canvas.Line(new Point(stemX + 20, stemTop - 6), new Point(stemX + 20, stemBottom - 16), note, 2.5);
		canvas.Ellipse(new Rect(stemX - 10, stemBottom - 5, 15, 11), note);
		canvas.Ellipse(new Rect(stemX + 10, stemBottom - 21, 15, 11), note);

		double[] waveformHeights = [6, 13, 9, 18, 11, 15, 7];
		double waveformLeft = artwork.Left + 20;
		double waveformWidth = artwork.Width - 40;
		double waveformY = artwork.Bottom - 17;
		for (int index = 0; index < waveformHeights.Length; index++)
		{
			double x = waveformLeft + waveformWidth * index / (waveformHeights.Length - 1);
			double halfHeight = waveformHeights[index] / 2.0;
			canvas.Line(new Point(x, waveformY - halfHeight), new Point(x, waveformY + halfHeight), accent, 1.5);
		}
	}

	private static void DrawLyricActivity(ScreenCanvas canvas, Rect lyrics, Color accent, double lyricProgress, double pulse, bool isPlaying)
	{
		double railTop = lyrics.Top + 33;
		const double railHeight = 43;
		double railLeft = lyrics.Left + 4;
		Color track = Color.FromRgb(42, 59, 71);
		Color active = Color.FromArgb((byte)(166 + pulse * 80), accent.R, accent.G, accent.B);
		canvas.RoundedRect(new Rect(railLeft, railTop, 2, railHeight), 1, track);
		if (isPlaying)
		{
			double indicatorY = railTop + Math.Clamp(lyricProgress, 0, 1) * railHeight;
			canvas.RoundedRect(new Rect(railLeft, railTop, 2, Math.Max(3, indicatorY - railTop)), 1, active);
			canvas.Ellipse(new Rect(railLeft - 2, indicatorY - 2, 6, 6), active);
		}

		double barsLeft = lyrics.Right - 23;
		for (int index = 0; index < 3; index++)
		{
			double wave = isPlaying ? (Math.Sin(lyrics.Left + index * 1.7 + lyricProgress * 7 + pulse * 4) + 1) / 2 : 0;
			double height = 3 + wave * 6;
			canvas.RoundedRect(new Rect(barsLeft + index * 6, lyrics.Top + 16 - height, 3, height), 1.5, active);
		}
	}

	private static double ResolveLyricProgress(TimeSpan position, LyricLine? current, LyricLine? next, double offsetSeconds)
	{
		if (current is null)
		{
			return 0;
		}

		TimeSpan adjusted = position + TimeSpan.FromSeconds(offsetSeconds);
		if (next is null || next.Timestamp <= current.Timestamp)
		{
			return 0.5;
		}

		return Math.Clamp((adjusted - current.Timestamp).TotalSeconds / (next.Timestamp - current.Timestamp).TotalSeconds, 0, 1);
	}

	private static double ResolvePulse(TimeSpan position, bool isPlaying)
	{
		if (!isPlaying)
		{
			return 0;
		}

		return (Math.Sin(position.TotalSeconds * 3.7) + 1) / 2;
	}

	private static Color Blend(Color baseColor, Color overlay, double amount)
	{
		amount = Math.Clamp(amount, 0, 1);
		return Color.FromRgb(
			(byte)(baseColor.R + (overlay.R - baseColor.R) * amount),
			(byte)(baseColor.G + (overlay.G - baseColor.G) * amount),
			(byte)(baseColor.B + (overlay.B - baseColor.B) * amount));
	}

	private static string FormatTime(TimeSpan value) => $"{(int)value.TotalMinutes}:{value.Seconds:00}";
}
