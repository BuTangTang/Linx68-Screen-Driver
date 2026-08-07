using System;
using System.ComponentModel;
using System.Windows;
using Linx68.ScreenDriver.App.ViewModels;
using Linx68.ScreenDriver.Core;
using Microsoft.Win32;

namespace Linx68.ScreenDriver.App;

public partial class MainWindow
{
	private void SystemEvents_OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
	{
		if (e.Category != UserPreferenceCategory.Color &&
			e.Category != UserPreferenceCategory.General &&
			e.Category != UserPreferenceCategory.VisualStyle)
		{
			return;
		}

		Dispatcher.BeginInvoke((Action)(() =>
		{
			UpdateTrayIconForSystemTheme();
			if (_settings.AppearanceMode == AppearanceMode.System)
			{
				ApplyAppearance();
			}
		}));
	}

	private void ApplyWindowBackdrop()
	{
		_ = WindowBackdrop.TryApplyMica(this, AppearanceManager.IsDark);
		WindowRoot.Background = (System.Windows.Media.Brush)FindResource("AppBackground");
		WindowRoot.BorderBrush = (System.Windows.Media.Brush)FindResource("Stroke.Default");
	}

	private void ApplyAppearance()
	{
		AppearanceManager.Apply(_settings.AppearanceMode);
		ApplyWindowBackdrop();
		DevicePreview?.InvalidateVisual();
		if (_themeDefinitions.Count > 0 && ThemeGalleryPanel is not null)
		{
			BuildThemeList();
		}
	}

	private void AppearanceViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (_updatingAppearance)
		{
			return;
		}

		switch (e.PropertyName)
		{
			case nameof(AppearanceViewModel.AppearanceMode):
				_settings.AppearanceMode = _appearanceViewModel.AppearanceMode;
				ApplyAppearance();
				ScheduleAutoCommit();
				break;
			case nameof(AppearanceViewModel.AccentColor):
				if (_appearanceViewModel.IsAccentColorValid)
				{
					_settings.AccentColor = _appearanceViewModel.AccentColor.Trim().ToUpperInvariant();
					_themeGalleryPreviewDirty = true;
					ScheduleAutoCommit();
				}
				break;
			case nameof(AppearanceViewModel.SelectedFontOption):
				if (_appearanceViewModel.SelectedFontOption is not null)
				{
					_settings.SelectedFontId = _appearanceViewModel.SelectedFontOption.Id;
					_themeGalleryPreviewDirty = true;
					if (_loaded)
					{
						_ = CommitAndPushAsync();
					}
				}
				break;
			case nameof(AppearanceViewModel.SelectedImageTimePlacement):
				if (_appearanceViewModel.SelectedImageTimePlacement is not null)
				{
					_settings.ImageTimePlacement = _appearanceViewModel.SelectedImageTimePlacement.Value;
					ScheduleAutoCommit();
				}
				break;
		}
	}
}
