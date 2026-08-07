using System;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using Linx68.ScreenDriver.App.ViewModels;

namespace Linx68.ScreenDriver.App;

public partial class MainWindow
{
	private void LoadAutomationSettings()
	{
		_updatingAutomation = true;
		try
		{
			_automationViewModel.Load(_settings);
		}
		finally
		{
			_updatingAutomation = false;
		}
	}

	private void AutomationViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (_updatingAutomation)
		{
			return;
		}

		switch (e.PropertyName)
		{
			case nameof(AutomationViewModel.AutoPush):
				_settings.AutoPush = _automationViewModel.AutoPush;
				break;
			case nameof(AutomationViewModel.RefreshSeconds):
				_settings.RefreshSeconds = _automationViewModel.RefreshSeconds;
				_timer.Interval = TimeSpan.FromSeconds(_settings.RefreshSeconds);
				break;
			case nameof(AutomationViewModel.AutoSwitchToMusic):
				_settings.AutoSwitchToMusic = _automationViewModel.AutoSwitchToMusic;
				break;
			default:
				return;
		}

		ScheduleAutoCommit();
	}

	private void SettingsViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (_updatingSettingsPage)
		{
			return;
		}

		if (e.PropertyName == nameof(SettingsViewModel.EndpointIp))
		{
			UpdateEndpointSummary();
			ScheduleAutoCommit();
			return;
		}

		if (e.PropertyName is nameof(SettingsViewModel.SafeLeft)
			or nameof(SettingsViewModel.SafeTop)
			or nameof(SettingsViewModel.SafeRight)
			or nameof(SettingsViewModel.SafeBottom)
			or nameof(SettingsViewModel.MinimizeToTray)
			or nameof(SettingsViewModel.CloseToTray)
			or nameof(SettingsViewModel.StartMinimized)
			or nameof(SettingsViewModel.LaunchAtStartup))
		{
			ScheduleAutoCommit();
		}
	}

	private void UpdateEndpointSummary()
	{
		string ipString = _settingsViewModel.EndpointIp;
		EndpointSummaryText.Text = ((IPAddress.TryParse(ipString, out IPAddress? address) && address.AddressFamily == AddressFamily.InterNetwork) ? address.ToString() : "地址未配置");
	}
}
