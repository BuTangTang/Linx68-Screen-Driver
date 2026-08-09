using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace Linx68.ScreenDriver.Core;

public static class BuiltInThemes
{
	private sealed class DelegateTheme(
		string id,
		string name,
		string description,
		string details,
		Action<ScreenCanvas, SystemSnapshot> draw) : IScreenTheme
	{
		public string Id { get; } = id;
		public string DisplayName { get; } = name;
		public string Description { get; } = description;
		public string Details { get; } = details;

		public void Draw(ScreenCanvas canvas, SystemSnapshot snapshot)
		{
			draw(canvas, snapshot);
		}
	}

	public static IReadOnlyList<IScreenTheme> Create(ImageTheme imageTheme, Func<double>? lyricOffsetSeconds = null)
	{
		return CreateDefinitions(imageTheme, lyricOffsetSeconds)
			.Select(definition => definition.Theme)
			.ToArray();
	}

	public static IReadOnlyList<ThemeDefinition> CreateDefinitions(ImageTheme imageTheme, Func<double>? lyricOffsetSeconds = null)
	{
		lyricOffsetSeconds ??= static () => 0;
		return new ThemeDefinition[]
		{
			Define(new SystemStatusTheme(), ThemeCategory.Monitor, ThemeDataRequirements.System, ThemeSettingsSections.System),
			Define(Make("dashboard", "状态概览", "四项系统指标集中展示", "紧凑展示 CPU、内存、下载和上传速度。", Dashboard), ThemeCategory.Monitor, ThemeDataRequirements.System, ThemeSettingsSections.System),
			Define(Make("performance", "性能条带", "纵向性能条与实时负载", "使用高对比度纵向进度条快速查看 CPU 与内存压力。", Performance), ThemeCategory.Monitor, ThemeDataRequirements.System, ThemeSettingsSections.System),
			Define(Make("network", "网络监控", "突出显示实时上下行速度", "以大号数字展示下载和上传速度，并保留 CPU 与内存摘要。", Network), ThemeCategory.Monitor, ThemeDataRequirements.System, ThemeSettingsSections.System),
			Define(Make("clock-flip", "翻页时钟", "小时与分钟分栏显示", "模拟翻页钟的双卡片布局，并显示秒钟与星期。", FlipClock), ThemeCategory.Time),
			Define(new FiveDayWeatherTheme(), ThemeCategory.Time, ThemeDataRequirements.Weather, ThemeSettingsSections.Weather),
			Define(new ClockWeatherTheme(), ThemeCategory.Time, ThemeDataRequirements.Weather, ThemeSettingsSections.Weather),
			Define(new MusicTheme(lyricOffsetSeconds), ThemeCategory.Music, ThemeDataRequirements.Music | ThemeDataRequirements.Lyrics, ThemeSettingsSections.Music),
			Define(new AiQuotaTheme(), ThemeCategory.Information, ThemeDataRequirements.AiQuota | ThemeDataRequirements.CodexTasks, ThemeSettingsSections.AiQuota),
			Define(new CodexTasksTheme(), ThemeCategory.Information, ThemeDataRequirements.CodexTasks)
		};
	}

	public static string? NormalizeThemeId(string? id) => id?.ToLowerInvariant() switch
	{
		"music-cover-focus" or "music-lyric-focus" or "music-pulse" => "music",
		"stocks" or "system-minimal" or "clock" or "clock-neon" or "clock-dot-matrix" or "clock-weather-dot" or "image" => "clock-weather",
		"codex-info" => "ai-quota",
		_ => id
	};

	private static ThemeDefinition Define(
		IScreenTheme theme,
		ThemeCategory category,
		ThemeDataRequirements requirements = ThemeDataRequirements.None,
		ThemeSettingsSections settingsSections = ThemeSettingsSections.None,
		bool isStatic = false)
	{
		return new ThemeDefinition(theme, category, requirements, settingsSections, isStatic);
	}

	private static IScreenTheme Make(string id, string name, string description, string details, Action<ScreenCanvas, SystemSnapshot> draw)
	{
		return new DelegateTheme(id, name, description, details, draw);
	}

	private static void Header(ScreenCanvas c, SystemSnapshot s, string title)
	{
		Rect safeBounds = c.SafeBounds;
		c.Text(title, 10.5, Color.FromRgb(154, 166, 179), new Point(safeBounds.Left, safeBounds.Top + 6.0), FontWeights.SemiBold);
		c.Text(s.Timestamp.ToString("HH:mm"), 13.0, Colors.White, new Point(safeBounds.Left, safeBounds.Top + 4.0), FontWeights.SemiBold, TextAlignment.Right, safeBounds.Width);
	}

	private static void Dashboard(ScreenCanvas c, SystemSnapshot s)
	{
		Rect safe = c.SafeBounds;
		c.Fill(Color.FromRgb(8, 11, 15));

		c.Text("状态概览", 12, Color.FromRgb(190, 201, 213),
			new Point(safe.Left, safe.Top + 7), FontWeights.SemiBold);
		c.Text(s.Timestamp.ToString("HH:mm"), 14, Colors.White,
			new Point(safe.Left, safe.Top + 5), FontWeights.SemiBold,
			TextAlignment.Right, safe.Width);

		double gap = 8;
		double tileWidth = (safe.Width - gap) / 2;
		double tileHeight = 141;
		double firstRow = safe.Top + 40;
		double secondRow = safe.Top + 190;

		DashboardTile(c, new Rect(safe.Left, firstRow, tileWidth, tileHeight),
			"CPU", $"{s.CpuPercent:0}%", "占用率", s.CpuPercent);
		DashboardTile(c, new Rect(safe.Left + tileWidth + gap, firstRow, tileWidth, tileHeight),
			"内存", $"{s.MemoryPercent:0}%", "占用率", s.MemoryPercent);
		DashboardTile(c, new Rect(safe.Left, secondRow, tileWidth, tileHeight),
			"下载", $"{s.DownloadMbps:0.0}", "Mbps", Math.Min(100.0, s.DownloadMbps * 4.0));
		DashboardTile(c, new Rect(safe.Left + tileWidth + gap, secondRow, tileWidth, tileHeight),
			"上传", $"{s.UploadMbps:0.0}", "Mbps", Math.Min(100.0, s.UploadMbps * 8.0));
	}

	private static void DashboardTile(ScreenCanvas c, Rect card, string label, string value, string unit, double percent)
	{
		c.RoundedRect(card, 12, Color.FromRgb(17, 22, 29), Color.FromRgb(31, 38, 47));
		c.RoundedRect(new Rect(card.Left + (card.Width - 22) / 2, card.Top + 12, 22, 3), 1.5, c.AccentColor);
		c.CenteredText(label, 11, Color.FromRgb(174, 186, 199),
			new Rect(card.Left + 4, card.Top + 24, card.Width - 8, 18), FontWeights.SemiBold);
		c.CenteredText(value, 22, Colors.White,
			new Rect(card.Left + 3, card.Top + 50, card.Width - 6, 34), FontWeights.SemiBold);
		c.CenteredText(unit, 9, Color.FromRgb(133, 145, 158),
			new Rect(card.Left + 4, card.Top + 88, card.Width - 8, 15), FontWeights.Medium);
		c.ProgressBar(new Rect(card.Left + 9, card.Top + 116, card.Width - 18, 7),
			percent, Color.FromRgb(37, 44, 53), c.AccentColor);
	}
	private static void Performance(ScreenCanvas c, SystemSnapshot s)
	{
		Rect safe = c.SafeBounds;
		c.Fill(Color.FromRgb(6, 9, 13));

		c.Text("性能条带", 12, Color.FromRgb(190, 201, 213),
			new Point(safe.Left, safe.Top + 7), FontWeights.SemiBold);
		c.Text(s.Timestamp.ToString("HH:mm"), 14, Colors.White,
			new Point(safe.Left, safe.Top + 5), FontWeights.SemiBold,
			TextAlignment.Right, safe.Width);

		double gap = 8;
		double columnWidth = (safe.Width - gap) / 2;
		PerformanceVerticalCard(c, new Rect(safe.Left, safe.Top + 40, columnWidth, 248),
			"CPU", s.CpuPercent);
		PerformanceVerticalCard(c, new Rect(safe.Left + columnWidth + gap, safe.Top + 40, columnWidth, 248),
			"内存", s.MemoryPercent);

		var network = new Rect(safe.Left, safe.Bottom - 59, safe.Width, 51);
		c.RoundedRect(network, 10, Color.FromRgb(17, 22, 28), Color.FromRgb(29, 36, 44));
		double half = network.Width / 2;
		c.CenteredText("下载", 9, Color.FromRgb(148, 160, 173),
			new Rect(network.Left, network.Top + 5, half, 15), FontWeights.SemiBold);
		c.CenteredText($"{s.DownloadMbps:0.0}M", 14, Colors.White,
			new Rect(network.Left, network.Top + 22, half, 23), FontWeights.SemiBold);
		c.Line(new Point(network.Left + half, network.Top + 9),
			new Point(network.Left + half, network.Bottom - 9), Color.FromRgb(43, 51, 61));
		c.CenteredText("上传", 9, Color.FromRgb(148, 160, 173),
			new Rect(network.Left + half, network.Top + 5, half, 15), FontWeights.SemiBold);
		c.CenteredText($"{s.UploadMbps:0.0}M", 14, Colors.White,
			new Rect(network.Left + half, network.Top + 22, half, 23), FontWeights.SemiBold);
	}

	private static void PerformanceVerticalCard(ScreenCanvas c, Rect card, string label, double value)
	{
		c.RoundedRect(card, 12, Color.FromRgb(14, 19, 25), Color.FromRgb(30, 37, 46));
		c.CenteredText(label, 11, Color.FromRgb(174, 186, 199),
			new Rect(card.Left + 4, card.Top + 12, card.Width - 8, 18), FontWeights.SemiBold);
		c.CenteredText($"{value:0}%", 22, Colors.White,
			new Rect(card.Left + 3, card.Top + 37, card.Width - 6, 34), FontWeights.SemiBold);

		var track = new Rect(card.Left + (card.Width - 24) / 2, card.Top + 82, 24, 146);
		c.RoundedRect(track, 12, Color.FromRgb(35, 42, 51), Color.FromRgb(45, 53, 63));
		double fillHeight = track.Height * Math.Clamp(value, 0, 100) / 100.0;
		if (fillHeight > 0)
		{
			fillHeight = Math.Max(6, fillHeight);
			c.RoundedRect(new Rect(track.Left, track.Bottom - fillHeight, track.Width, fillHeight),
				Math.Min(12, fillHeight / 2), c.AccentColor);
		}
	}
	private static void Network(ScreenCanvas c, SystemSnapshot s)
	{
		Rect safe = c.SafeBounds;
		c.Fill(Color.FromRgb(7, 10, 14));

		c.Text("网络监控", 12, Color.FromRgb(190, 201, 213),
			new Point(safe.Left, safe.Top + 7), FontWeights.SemiBold);
		c.Text(s.Timestamp.ToString("HH:mm"), 14, Colors.White,
			new Point(safe.Left, safe.Top + 5), FontWeights.SemiBold,
			TextAlignment.Right, safe.Width);

		NetworkMetricCard(c, new Rect(safe.Left, safe.Top + 40, safe.Width, 118),
			"下载速度", s.DownloadMbps);
		NetworkMetricCard(c, new Rect(safe.Left, safe.Top + 169, safe.Width, 118),
			"上传速度", s.UploadMbps);

		var summary = new Rect(safe.Left, safe.Bottom - 59, safe.Width, 51);
		c.RoundedRect(summary, 10, Color.FromRgb(17, 22, 28), Color.FromRgb(29, 36, 44));
		double half = summary.Width / 2;
		c.CenteredText("CPU", 10, Color.FromRgb(148, 160, 173),
			new Rect(summary.Left, summary.Top + 5, half, 15), FontWeights.SemiBold);
		c.CenteredText($"{s.CpuPercent:0}%", 15, Colors.White,
			new Rect(summary.Left, summary.Top + 23, half, 22), FontWeights.SemiBold);
		c.Line(new Point(summary.Left + half, summary.Top + 9),
			new Point(summary.Left + half, summary.Bottom - 9), Color.FromRgb(43, 51, 61));
		c.CenteredText("内存", 10, Color.FromRgb(148, 160, 173),
			new Rect(summary.Left + half, summary.Top + 5, half, 15), FontWeights.SemiBold);
		c.CenteredText($"{s.MemoryPercent:0}%", 15, Colors.White,
			new Rect(summary.Left + half, summary.Top + 23, half, 22), FontWeights.SemiBold);
	}

	private static void NetworkMetricCard(ScreenCanvas c, Rect card, string label, double value)
	{
		c.RoundedRect(card, 11, Color.FromRgb(13, 18, 24), Color.FromRgb(27, 34, 43));
		c.RoundedRect(new Rect(card.Left + 10, card.Top + 13, 4, 20), 2, c.AccentColor);
		c.Text(label, 12, Color.FromRgb(183, 194, 206),
			new Point(card.Left + 22, card.Top + 14), FontWeights.SemiBold);
		c.CenteredText($"{value:0.0}", 33, Colors.White,
			new Rect(card.Left + 8, card.Top + 42, card.Width - 16, 40), FontWeights.SemiBold);
		c.CenteredText("Mbps", 11, c.AccentColor,
			new Rect(card.Left + 8, card.Top + 88, card.Width - 16, 17), FontWeights.SemiBold);
	}
	private static void MinimalSystem(ScreenCanvas c, SystemSnapshot s)
	{
		Rect safe = c.SafeBounds;
		c.Fill(Color.FromRgb(5, 7, 10));
		c.Text(s.Timestamp.ToString("HH:mm"), 19, c.AccentColor,
			new Point(safe.Left, safe.Top + 4), FontWeights.SemiBold);

		MinimalMetric(c, safe, safe.Top + 76, "处理器", s.CpuPercent);
		MinimalMetric(c, safe, safe.Top + 194, "内存", s.MemoryPercent);

		c.Line(new Point(safe.Left, safe.Bottom - 69),
			new Point(safe.Right, safe.Bottom - 69), Color.FromRgb(27, 33, 40));
		c.Text("下载", 11, Color.FromRgb(154, 166, 179),
			new Point(safe.Left, safe.Bottom - 51), FontWeights.SemiBold);
		c.Text($"{s.DownloadMbps:0.0} Mbps", 11, Colors.White,
			new Point(safe.Left, safe.Bottom - 51), FontWeights.SemiBold,
			TextAlignment.Right, safe.Width);
		c.Text("上传", 11, Color.FromRgb(154, 166, 179),
			new Point(safe.Left, safe.Bottom - 27), FontWeights.SemiBold);
		c.Text($"{s.UploadMbps:0.0} Mbps", 11, Colors.White,
			new Point(safe.Left, safe.Bottom - 27), FontWeights.SemiBold,
			TextAlignment.Right, safe.Width);
	}

	private static void MinimalMetric(ScreenCanvas c, Rect bounds, double y, string label, double value)
	{
		c.Text(label, 12, Color.FromRgb(174, 185, 197),
			new Point(bounds.Left, y), FontWeights.SemiBold);
		c.Text($"{value:0}%", 42, Colors.White,
			new Point(bounds.Left, y + 21), FontWeights.SemiBold);
		c.ProgressBar(new Rect(bounds.Left, y + 82, bounds.Width, 7),
			value, Color.FromRgb(31, 37, 45), c.AccentColor);
	}
	private static void NeonClock(ScreenCanvas c, SystemSnapshot s)
	{
		Rect safe = c.SafeBounds;
		Color accent = c.AccentColor;
		c.Gradient(Color.FromRgb(4, 6, 11), Darken(accent, 0.82), new Point(0, 0), new Point(1, 1));

		c.AlignedText("霓虹时钟", 13, Color.FromRgb(213, 221, 230),
			new Rect(safe.Left, safe.Top + 6, safe.Width, 24), FontWeights.SemiBold, TextAlignment.Left);

		c.AlignedText(s.Timestamp.ToString("HH:mm"), 39, Colors.White,
			new Rect(safe.Left, safe.Top + 72, safe.Width, 56), FontWeights.SemiBold, TextAlignment.Left);
		c.AlignedText(s.Timestamp.ToString("ss"), 22, accent,
			new Rect(safe.Left, safe.Top + 142, safe.Width, 31), FontWeights.SemiBold, TextAlignment.Left);

		var track = new Rect(safe.Left, safe.Top + 188, safe.Width, 5);
		c.ProgressBar(track, s.Timestamp.Second / 59.0 * 100.0,
			Color.FromRgb(28, 34, 42), accent);

		double dateTop = safe.Top + 238;
		c.AlignedText(s.Timestamp.ToString("dddd"), 16, accent,
			new Rect(safe.Left, dateTop + 14, safe.Width, 27), FontWeights.SemiBold, TextAlignment.Left);
		c.AlignedText(ChineseDate(s.Timestamp), 15, Colors.White,
			new Rect(safe.Left, dateTop + 50, safe.Width, 27), FontWeights.Medium, TextAlignment.Left);
	}
	private static void FlipClock(ScreenCanvas c, SystemSnapshot s)
	{
		Rect safe = c.SafeBounds;
		c.Fill(Color.FromRgb(8, 10, 13));
		c.Text("翻页时钟", 13, Color.FromRgb(181, 192, 204),
			new Point(safe.Left, safe.Top + 7), FontWeights.SemiBold);

		double gap = 6;
		double width = (safe.Width - gap) / 2;
		double cardTop = safe.Top + 68;
		c.CenteredText("小时", 10, Color.FromRgb(138, 150, 164),
			new Rect(safe.Left, safe.Top + 134, width, 17), FontWeights.SemiBold);
		c.CenteredText("分钟", 10, Color.FromRgb(138, 150, 164),
			new Rect(safe.Left + width + gap, safe.Top + 134, width, 17), FontWeights.SemiBold);
		FlipTile(c, new Rect(safe.Left, cardTop, width, width), s.Timestamp.ToString("HH"));
		FlipTile(c, new Rect(safe.Left + width + gap, cardTop, width, width), s.Timestamp.ToString("mm"));

		var seconds = new Rect(safe.Left, safe.Top + 169, safe.Width, 44);
		c.RoundedRect(seconds, 10, Color.FromRgb(16, 20, 26), Color.FromRgb(31, 38, 47));
		c.AlignedText("秒钟", 11, Color.FromRgb(157, 169, 182),
			new Rect(seconds.Left + 12, seconds.Top, seconds.Width - 24, seconds.Height),
			FontWeights.SemiBold, TextAlignment.Left);
		c.AlignedText(s.Timestamp.ToString("ss"), 21, c.AccentColor,
			new Rect(seconds.Left + 12, seconds.Top, seconds.Width - 24, seconds.Height),
			FontWeights.SemiBold, TextAlignment.Right);

		c.AlignedText(s.Timestamp.ToString("dddd"), 14, c.AccentColor,
			new Rect(safe.Left, safe.Top + 269, safe.Width, 24),
			FontWeights.SemiBold, TextAlignment.Left);
		c.AlignedText(ChineseDate(s.Timestamp), 15, Colors.White,
			new Rect(safe.Left, safe.Top + 300, safe.Width, 25),
			FontWeights.Medium, TextAlignment.Left);
	}

	private static void FlipTile(ScreenCanvas c, Rect card, string value)
	{
		c.RoundedRect(card, 10, Color.FromRgb(20, 24, 30), Color.FromRgb(40, 48, 58));
		c.CenteredText(value, 31, Colors.White,
			new Rect(card.Left + 3, card.Top + 4, card.Width - 6, card.Height - 8), FontWeights.Medium);
		double split = card.Top + card.Height / 2;
		c.Line(new Point(card.Left + 5, split), new Point(card.Right - 5, split),
			Color.FromRgb(7, 9, 12), 2);
		c.Ellipse(new Rect(card.Left + 2, split - 2, 4, 4), Color.FromRgb(7, 9, 12));
		c.Ellipse(new Rect(card.Right - 6, split - 2, 4, 4), Color.FromRgb(7, 9, 12));
	}
	private static void MusicMinimal(ScreenCanvas c, SystemSnapshot s)
	{
		MusicSnapshot musicSnapshot = s.Music ?? MusicSnapshot.Unavailable;
		Rect safeBounds = c.SafeBounds;
		c.Fill(Color.FromRgb(7, 9, 13));
		c.Text(musicSnapshot.IsPlaying ? "正在播放" : "已暂停", 9.0, c.AccentColor, new Point(safeBounds.Left, safeBounds.Top + 7.0), FontWeights.SemiBold);
		c.Text(musicSnapshot.Title, 22.0, Colors.White, new Point(safeBounds.Left, safeBounds.Top + 73.0), FontWeights.SemiBold, TextAlignment.Left, safeBounds.Width, 92.0);
		c.Text(string.IsNullOrWhiteSpace(musicSnapshot.Artist) ? "Windows Media" : musicSnapshot.Artist, 11.0, Color.FromRgb(126, 139, 153), new Point(safeBounds.Left, safeBounds.Top + 181.0), FontWeights.Medium, TextAlignment.Left, safeBounds.Width, 24.0);
		bool flag = musicSnapshot.Duration.TotalSeconds <= 0.0;
		double percent = (flag ? ((double)(musicSnapshot.IsPlaying ? 100 : 0)) : (musicSnapshot.Position.TotalSeconds / musicSnapshot.Duration.TotalSeconds * 100.0));
		c.ProgressBar(new Rect(safeBounds.Left, safeBounds.Top + 247.0, safeBounds.Width, 8.0), percent, Color.FromRgb(35, 42, 51), c.AccentColor);
		c.Text(flag ? "直播" : Time(musicSnapshot.Position), 10.0, c.AccentColor, new Point(safeBounds.Left, safeBounds.Top + 271.0), FontWeights.SemiBold);
		c.Text(flag ? "ON AIR" : Time(musicSnapshot.Duration), 10.0, Color.FromRgb(126, 139, 153), new Point(safeBounds.Left, safeBounds.Top + 271.0), FontWeights.SemiBold, TextAlignment.Right, safeBounds.Width);
		c.Text(s.Timestamp.ToString("HH:mm"), 18.0, Colors.White, new Point(safeBounds.Left, safeBounds.Bottom - 45.0), FontWeights.SemiBold);
	}

	private static void MusicPoster(ScreenCanvas c, SystemSnapshot s)
	{
		MusicSnapshot music = s.Music ?? MusicSnapshot.Unavailable;
		c.Fill(Color.FromRgb(9, 11, 15));
		if (music.Artwork is { Length: > 0 }) c.Image(music.Artwork, new Rect(0, 0, c.Profile.Width, c.Profile.Height));
		Rect safe = c.SafeBounds;
		var card = new Rect(safe.Left, safe.Bottom - 151, safe.Width, 133);
		c.RoundedRect(card, 12, Color.FromArgb(224, 5, 8, 12), Color.FromArgb(80, 255, 255, 255));
		c.Text(music.Title, 14, Colors.White, new Point(card.Left + 12, card.Top + 14), FontWeights.SemiBold,
			TextAlignment.Left, card.Width - 24, 42);
		c.Text(string.IsNullOrWhiteSpace(music.Artist) ? "Windows Media" : music.Artist, 11.5,
			Color.FromRgb(190, 199, 209), new Point(card.Left + 12, card.Top + 59), FontWeights.Medium,
			TextAlignment.Left, card.Width - 24, 20);
		bool live = music.Duration.TotalSeconds <= 0;
		double percent = live ? (music.IsPlaying ? 100 : 0) : music.Position.TotalSeconds / music.Duration.TotalSeconds * 100;
		c.ProgressBar(new Rect(card.Left + 12, card.Bottom - 40, card.Width - 24, 6), percent, Color.FromRgb(45, 51, 60), c.AccentColor);
		c.Text(live ? "直播" : Time(music.Position), 9.5, Colors.White, new Point(card.Left + 12, card.Bottom - 27), FontWeights.SemiBold);
		c.Text(live ? "ON AIR" : Time(music.Duration), 9.5, Color.FromRgb(190, 199, 209),
			new Point(card.Left + 12, card.Bottom - 27), FontWeights.SemiBold, TextAlignment.Right, card.Width - 24);
	}

		private static string ChineseDate(DateTimeOffset value) =>
		$"{value.Year}年{value.Month}月{value.Day}日";
	private static Color Darken(Color c, double n)
	{
		return Color.FromRgb((byte)((double)(int)c.R * (1.0 - n)), (byte)((double)(int)c.G * (1.0 - n)), (byte)((double)(int)c.B * (1.0 - n)));
	}

	private static Color Lighten(Color c, double n)
	{
		return Color.FromRgb((byte)((double)(int)c.R + (double)(255 - c.R) * n), (byte)((double)(int)c.G + (double)(255 - c.G) * n), (byte)((double)(int)c.B + (double)(255 - c.B) * n));
	}

	private static string Time(TimeSpan value)
	{
		return $"{(int)value.TotalMinutes}:{value.Seconds:00}";
	}
}
