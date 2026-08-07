using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Resources;
using Microsoft.Win32;

namespace Linx68.ScreenDriver.App;

public partial class MainWindow
{
	private async void MinimizeButton_OnClick(object sender, RoutedEventArgs e)
	{
		if (_windowTransitionRunning)
		{
			return;
		}

		_windowTransitionRunning = true;
		try
		{
			await InteractionMotion.HideAsync(WindowRoot, 4.0);
			if (_settingsViewModel.MinimizeToTray)
			{
				HideToTray();
				return;
			}

			_revealAfterMinimize = true;
			base.WindowState = WindowState.Minimized;
		}
		finally
		{
			InteractionMotion.Reset(WindowRoot);
			_windowTransitionRunning = false;
		}
	}

	private void MaximizeButton_OnClick(object sender, RoutedEventArgs e)
	{
		base.WindowState = base.WindowState != WindowState.Maximized
			? WindowState.Maximized
			: WindowState.Normal;
	}

	private async void CloseButton_OnClick(object sender, RoutedEventArgs e)
	{
		if (_windowTransitionRunning)
		{
			return;
		}

		_windowTransitionRunning = true;
		try
		{
			await InteractionMotion.HideAsync(WindowRoot, 4.0);
			Close();
		}
		finally
		{
			InteractionMotion.Reset(WindowRoot);
			_windowTransitionRunning = false;
		}
	}

	private void MainWindow_OnStateChanged(object? sender, EventArgs e)
	{
		if (base.WindowState != WindowState.Minimized)
		{
			_restoreWindowState = base.WindowState;
			if (_revealAfterMinimize)
			{
				_revealAfterMinimize = false;
				InteractionMotion.Reveal(WindowRoot, 8.0, 0.994);
			}
		}
	}

	private void MainWindow_OnClosing(object? sender, CancelEventArgs e)
	{
		if (!_explicitExit && _loaded && _settingsViewModel.CloseToTray)
		{
			e.Cancel = true;
			HideToTray();
		}
	}

	private void HideToTray()
	{
		WindowState windowState = base.WindowState;
		if (windowState is WindowState.Normal or WindowState.Maximized)
		{
			_restoreWindowState = base.WindowState;
		}

		_trayIcon.Visible = true;
		base.ShowInTaskbar = false;
		Hide();
	}

	private void RestoreFromTray()
	{
		base.ShowInTaskbar = true;
		Show();
		base.WindowState = _restoreWindowState != WindowState.Minimized
			? _restoreWindowState
			: WindowState.Normal;
		Activate();
		base.Topmost = true;
		base.Topmost = false;
		Focus();
		InteractionMotion.Reveal(WindowRoot, 8.0, 0.994);
	}

	private void ExitApplication()
	{
		_explicitExit = true;
		_trayIcon.Visible = false;
		Close();
	}

	internal void RestoreFromExternalActivation()
	{
		RestoreFromTray();
	}

	private void Application_OnSessionEnding(object sender, SessionEndingCancelEventArgs e)
	{
		_explicitExit = true;
	}

	private void UpdateTrayVisibility()
	{
		if (_loaded)
		{
			_trayIcon.Visible = !base.IsVisible || _settings.MinimizeToTray || _settings.CloseToTray;
		}
	}

	private NotifyIcon CreateTrayIcon()
	{
		ContextMenuStrip contextMenuStrip = new ContextMenuStrip();
		contextMenuStrip.Items.Add("打开 灵犀68屏幕驱动", null, delegate
		{
			Dispatcher.BeginInvoke(RestoreFromTray);
		});
		contextMenuStrip.Items.Add("刷新并推送", null, delegate
		{
			Dispatcher.BeginInvoke(async () =>
			{
				await CommitAndPushAsync();
			});
		});
		contextMenuStrip.Items.Add(new ToolStripSeparator());
		contextMenuStrip.Items.Add("退出", null, delegate
		{
			Dispatcher.BeginInvoke(ExitApplication);
		});
		NotifyIcon notifyIcon = new NotifyIcon();
		notifyIcon.Text = "灵犀68屏幕驱动";
		notifyIcon.Icon = LoadTrayIcon(IsWindowsSystemDarkMode());
		notifyIcon.ContextMenuStrip = contextMenuStrip;
		notifyIcon.Visible = false;
		notifyIcon.DoubleClick += delegate
		{
			Dispatcher.BeginInvoke(RestoreFromTray);
		};
		return notifyIcon;
	}

	private void UpdateTrayIconForSystemTheme()
	{
		Icon nextIcon = LoadTrayIcon(IsWindowsSystemDarkMode());
		Icon? previousIcon = _trayIcon.Icon;
		_trayIcon.Icon = nextIcon;
		previousIcon?.Dispose();
	}

	private static bool IsWindowsSystemDarkMode() => AppearanceManager.IsSystemDarkMode();

	private static Icon LoadTrayIcon(bool useWhiteIcon)
	{
		string iconName = useWhiteIcon ? "TrayIcon.White.ico" : "TrayIcon.ico";
		StreamResourceInfo resourceStream = System.Windows.Application.GetResourceStream(
			new Uri($"pack://application:,,,/Linx68.ScreenDriver.App;component/Assets/{iconName}", UriKind.Absolute));
		if (resourceStream == null)
		{
			return (Icon)SystemIcons.Application.Clone();
		}
		using (resourceStream.Stream)
		{
			using Icon icon = new Icon(resourceStream.Stream);
			return (Icon)icon.Clone();
		}
	}
}
