using System;
using System.Windows;
using System.Windows.Media;

namespace Linx68.ScreenDriver.Core;

public sealed class MusicTheme : IScreenTheme
{
	public string Id => "music";

	public string DisplayName => "音乐";

	public string Description => "封面、曲名、歌手与播放进度";

	public string Details => "读取 Windows 媒体会话；直播内容自动显示 LIVE 状态。";

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
		bool flag = musicSnapshot.Duration.TotalSeconds <= 0.0;
		double percent = (flag ? ((double)(musicSnapshot.IsPlaying ? 100 : 0)) : (musicSnapshot.Position.TotalSeconds / musicSnapshot.Duration.TotalSeconds * 100.0));
		canvas.ProgressBar(new Rect(safeBounds.Left, 329.0, safeBounds.Width, 6.0), percent, Color.FromRgb(36, 43, 51), accentColor);
		canvas.Text(flag ? "直播" : FormatTime(musicSnapshot.Position), 11.5, flag ? accentColor : color2, new Point(safeBounds.Left, 345.0), FontWeights.SemiBold);
		canvas.Text((!flag) ? FormatTime(musicSnapshot.Duration) : (musicSnapshot.IsPlaying ? "直播中" : "已暂停"), 11.5, color2, new Point(safeBounds.Left, 345.0), FontWeights.SemiBold, TextAlignment.Right, safeBounds.Width);
	}

	private static string FormatTime(TimeSpan value)
	{
		return $"{(int)value.TotalMinutes}:{value.Seconds:00}";
	}
}
