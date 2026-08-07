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

	public string DisplayName => "音乐";

	public string Description => "封面、曲名、歌手与播放进度";

	public string Details => "读取 Windows 媒体会话；进度条下方显示可选同步歌词。";

	public void Draw(ScreenCanvas canvas, SystemSnapshot snapshot)
	{
		MusicSnapshot musicSnapshot = snapshot.Music ?? MusicSnapshot.Unavailable;
		Rect safeBounds = canvas.SafeBounds;
		Color color = Color.FromRgb(8, 10, 14);
		Color fill = Color.FromRgb(17, 21, 27);
		Color color2 = Color.FromRgb(125, 137, 150);
		Color accentColor = canvas.AccentColor;
		canvas.Fill(color);
		canvas.Text("正在播放", 9.0, color2, new Point(safeBounds.Left + 2.0, safeBounds.Top + 6.0), FontWeights.SemiBold);
		Rect rect = new Rect(safeBounds.Left, safeBounds.Top + 30.0, safeBounds.Width, 150.0);
		byte[]? artwork = musicSnapshot.Artwork;
		if (artwork != null && artwork.Length > 0)
		{
			canvas.Image(artwork, rect, 8.0);
		}
		else
		{
			canvas.RoundedRect(rect, 8.0, fill, Color.FromRgb(35, 42, 50));
			canvas.Text("音乐", 14.0, accentColor, new Point(safeBounds.Left + 48.0, safeBounds.Top + 96.0), FontWeights.SemiBold);
		}
		canvas.Text(musicSnapshot.Title, 13.5, Colors.White, new Point(safeBounds.Left, 247.0), FontWeights.SemiBold, TextAlignment.Left, safeBounds.Width, 40.0);
		canvas.Text(string.IsNullOrWhiteSpace(musicSnapshot.Artist) ? "Windows Media" : musicSnapshot.Artist, 10.5, color2, new Point(safeBounds.Left, 294.0), FontWeights.Normal, TextAlignment.Left, safeBounds.Width, 18.0);
		bool hasDuration = musicSnapshot.Duration.TotalSeconds > 0.0;
		double percent = hasDuration
			? musicSnapshot.Position.TotalSeconds / musicSnapshot.Duration.TotalSeconds * 100.0
			: (musicSnapshot.IsPlaying ? 100 : 0);
		canvas.ProgressBar(new Rect(safeBounds.Left, 329.0, safeBounds.Width, 6.0), percent, Color.FromRgb(36, 43, 51), accentColor);
		canvas.Text("歌词", 10.5, accentColor, new Point(safeBounds.Left, 345.0), FontWeights.SemiBold);
		canvas.Text(GetCurrentLyric(musicSnapshot), 9.5, color2, new Point(safeBounds.Left + 26.0, 346.0), FontWeights.Medium, TextAlignment.Left, safeBounds.Width - 26.0, 16.0);
	}

	private string GetCurrentLyric(MusicSnapshot music)
	{
		if (!music.Lyrics.Available)
		{
			return "暂无同步歌词";
		}

		(LyricLine? current, _) = music.Lyrics.FindAt(music.Position, _lyricOffsetSeconds());
		string lyric = current?.Text?.Trim() ?? "前奏";
		return lyric.Length > 13 ? lyric[..12] + "…" : lyric;
	}
}
