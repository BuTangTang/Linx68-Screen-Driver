using System;
using System.Windows;
using System.Windows.Media;
using WpfBrush = System.Windows.Media.Brush;

namespace Linx68.ScreenDriver.App;

public partial class MainWindow
{
	private void SetDeviceStatus(bool success)
	{
		DeviceStatusText.Text = success ? "设备在线" : "设备离线";
		PreviewStatusText.Text = success ? "设备在线" : "本地预览";
		PreviewStatusBadge.ToolTip = success
			? "设备连接正常"
			: "设备离线时，预览仍在本机实时更新";
		WpfBrush brush = success
			? (WpfBrush)FindResource("SuccessBrush")
			: (WpfBrush)FindResource("DangerBrush");
		PreviewStatusText.Foreground = success
			? brush
			: (WpfBrush)FindResource("SecondaryText");
		SetDeviceStatusVisual(brush);
	}

	private void SetOperationFailure(string message)
	{
		DeviceStatusText.Text = message;
		PreviewStatusText.Text = "本地预览";
		PreviewStatusText.Foreground = (WpfBrush)FindResource("SecondaryText");
		PreviewStatusBadge.ToolTip = "设备推送失败；本地预览仍在本机实时更新";
		WpfBrush brush = (WpfBrush)FindResource("DangerBrush");
		SetDeviceStatusVisual(brush);
	}

	private void SetDeviceStatusVisual(WpfBrush brush)
	{
		DeviceStatusText.Foreground = brush;
		DeviceStatusDot.Fill = brush;
		DeviceStatusDot.BeginAnimation(OpacityProperty, null);
		DeviceStatusDot.Opacity = 1;
	}
}
