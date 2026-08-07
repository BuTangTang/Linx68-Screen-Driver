using System;
using System.Windows;
using System.Windows.Media;

namespace Linx68.ScreenDriver.Core;

public sealed class SystemStatusTheme : IScreenTheme
{
	public string Id => "system";

	public string DisplayName => "电脑状态";

	public string Description => "CPU、内存与网络状态";

	public string Details => "双卡片展示 CPU 和内存占用，并在底部显示实时上下行速度。";

	public void Draw(ScreenCanvas canvas, SystemSnapshot snapshot)
	{
		Rect safe = canvas.SafeBounds;
		Color background = Color.FromRgb(7, 9, 12);
		Color panel = Color.FromRgb(15, 20, 26);
		Color border = Color.FromRgb(30, 38, 47);
		Color muted = Color.FromRgb(153, 165, 178);
		Color accent = canvas.AccentColor;
		canvas.Fill(background);

		canvas.Text("电脑状态", 12, Color.FromRgb(190, 201, 213),
			new Point(safe.Left, safe.Top + 7), FontWeights.SemiBold);
		canvas.Text(snapshot.Timestamp.ToString("HH:mm"), 14, Colors.White,
			new Point(safe.Left, safe.Top + 5), FontWeights.SemiBold,
			TextAlignment.Right, safe.Width);

		DrawMetric(canvas, new Rect(safe.Left, safe.Top + 40, safe.Width, 112),
			"处理器占用", "CPU", snapshot.CpuPercent, accent, panel, border, muted);
		DrawMetric(canvas, new Rect(safe.Left, safe.Top + 164, safe.Width, 112),
			"内存占用", "MEM", snapshot.MemoryPercent, accent, panel, border, muted);

		var network = new Rect(safe.Left, safe.Bottom - 59, safe.Width, 51);
		canvas.RoundedRect(network, 10, Color.FromRgb(17, 22, 28), Color.FromRgb(29, 36, 44));
		double half = network.Width / 2;
		canvas.CenteredText("下载", 9, muted,
			new Rect(network.Left, network.Top + 5, half, 15), FontWeights.SemiBold);
		canvas.CenteredText($"{snapshot.DownloadMbps:0.0}M", 14, Colors.White,
			new Rect(network.Left, network.Top + 22, half, 23), FontWeights.SemiBold);
		canvas.Line(new Point(network.Left + half, network.Top + 9),
			new Point(network.Left + half, network.Bottom - 9), Color.FromRgb(43, 51, 61));
		canvas.CenteredText("上传", 9, muted,
			new Rect(network.Left + half, network.Top + 5, half, 15), FontWeights.SemiBold);
		canvas.CenteredText($"{snapshot.UploadMbps:0.0}M", 14, Colors.White,
			new Rect(network.Left + half, network.Top + 22, half, 23), FontWeights.SemiBold);
	}

	private static void DrawMetric(ScreenCanvas canvas, Rect card, string label, string shortLabel, double value, Color accent, Color panel, Color border, Color muted)
	{
		canvas.RoundedRect(card, 11, panel, border);
		canvas.RoundedRect(new Rect(card.Left + 10, card.Top + 13, 4, 20), 2, accent);
		canvas.Text(label, 12, Color.FromRgb(185, 196, 208),
			new Point(card.Left + 22, card.Top + 14), FontWeights.SemiBold);
		canvas.Text(shortLabel, 9, muted,
			new Point(card.Left, card.Top + 15), FontWeights.SemiBold,
			TextAlignment.Right, card.Width - 10);
		canvas.CenteredText($"{value:0}%", 34, Colors.White,
			new Rect(card.Left + 10, card.Top + 38, card.Width - 20, 41), FontWeights.SemiBold);
		canvas.ProgressBar(new Rect(card.Left + 10, card.Top + 91, card.Width - 20, 8),
			value, Color.FromRgb(35, 42, 51), accent);
	}
}
