using System;
using System.Windows;
using System.Windows.Media;
using Linx68.ScreenDriver.Application;
using Linx68.ScreenDriver.Core;
using WpfBrush = System.Windows.Media.Brush;

namespace Linx68.ScreenDriver.App;

public partial class MainWindow
{
	private void SetDeviceStatus(bool success)
	{
		bool configured = DeviceEndpoint.TryCreate(_settingsViewModel.EndpointIp, out _);
		string idleSummary = configured ? "尚未连接" : "设备未配置";
		DeviceStatusText.Text = success ? "设备在线" : idleSummary;
		PreviewStatusText.Text = success ? "设备在线" : "本地预览";
		PreviewStatusBadge.ToolTip = success
			? "设备连接正常"
			: configured ? "尚未尝试连接；本地预览可正常使用" : "配置设备地址后可启用自动推送";
		WpfBrush brush = success
			? (WpfBrush)FindResource("SuccessBrush")
			: (WpfBrush)FindResource("SecondaryText");
		PreviewStatusText.Foreground = success
			? brush
			: (WpfBrush)FindResource("SecondaryText");
		SetDeviceStatusVisual(brush);
		_dataServicesViewModel.Device.Set(
			success ? Linx68.ScreenDriver.Application.DataLoadState.Ready : Linx68.ScreenDriver.Application.DataLoadState.Empty,
			success ? "设备在线" : idleSummary,
			success ? _settingsViewModel.EndpointIp : configured ? "等待首次推送" : "请先配置 Linx68 设备地址");
	}

	private void SetDeviceStatus(DevicePushResult result)
	{
		string detail = string.IsNullOrWhiteSpace(result.Message)
			? (result.Success ? "推送成功" : "设备连接失败")
			: result.Message;
		DeviceStatusText.Text = result.Success ? "设备在线" : "设备离线";
		PreviewStatusText.Text = result.Success ? "设备在线" : "本地预览";
		PreviewStatusBadge.ToolTip = result.Success
			? $"设备连接正常 · {detail}"
			: $"{detail}；本地预览仍可使用";
		WpfBrush brush = result.Success
			? (WpfBrush)FindResource("SuccessBrush")
			: (WpfBrush)FindResource("DangerBrush");
		PreviewStatusText.Foreground = result.Success
			? brush
			: (WpfBrush)FindResource("SecondaryText");
		SetDeviceStatusVisual(brush);
		_dataServicesViewModel.Device.Set(
			result.Success
				? Linx68.ScreenDriver.Application.DataLoadState.Ready
				: Linx68.ScreenDriver.Application.DataLoadState.Error,
			result.Success ? "设备在线" : "设备离线",
			detail,
			result.Elapsed);
	}

	private void SetOperationFailure(string message)
	{
		DeviceStatusText.Text = message;
		PreviewStatusText.Text = "本地预览";
		PreviewStatusText.Foreground = (WpfBrush)FindResource("SecondaryText");
		PreviewStatusBadge.ToolTip = "设备推送失败；本地预览仍在本机实时更新";
		WpfBrush brush = (WpfBrush)FindResource("DangerBrush");
		SetDeviceStatusVisual(brush);
		_dataServicesViewModel.Device.Set(
			Linx68.ScreenDriver.Application.DataLoadState.Error,
			message,
			"本地预览仍可使用，请检查设置保存或设备连接");
	}

	private void SetDeviceStatusVisual(WpfBrush brush)
	{
		DeviceStatusText.Foreground = brush;
		DeviceStatusDot.Fill = brush;
		DeviceStatusDot.BeginAnimation(OpacityProperty, null);
		DeviceStatusDot.Opacity = 1;
	}
}
