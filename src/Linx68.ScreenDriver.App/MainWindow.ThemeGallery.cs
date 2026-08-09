using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Linx68.ScreenDriver.App.ViewModels;
using Linx68.ScreenDriver.Core;

namespace Linx68.ScreenDriver.App;

public partial class MainWindow
{
	private void BuildThemeList()
	{
		_settings.SelectedThemeId = BuiltInThemes.NormalizeThemeId(_settings.SelectedThemeId) ?? _themeDefinitions[0].Id;
		bool previousSuppression = _suppressThemeRefresh;
		_suppressThemeRefresh = true;
		try
		{
			_screenViewModel.SetThemes(
				_themeDefinitions.Select(CreateThemeCard),
				_settings.SelectedThemeId);
			_screenViewModel.SelectTheme(_settings.SelectedThemeId, notify: true);
		}
		finally
		{
			_suppressThemeRefresh = previousSuppression;
		}

		UpdateThemeGalleryCardWidth();
	}

	private ThemeCardViewModel CreateThemeCard(ThemeDefinition definition)
	{
		ImageSource? preview = null;
		try
		{
			RenderedFrame frame = _renderer.Render(
				definition.Theme,
				SystemSnapshot.DesignSample,
				86,
				GetAccentColor(),
				GetSelectedFontFamily(),
				GetScreenDisplayOptions());
			preview = LoadBitmap(frame.JpegBytes);
		}
		catch
		{
			// A failed gallery preview must not prevent the remaining themes from loading.
		}

		return new ThemeCardViewModel(definition, preview);
	}

	private ThemeDefinition? GetSelectedThemeDefinition()
	{
		return GetThemeDefinition(_settings.SelectedThemeId);
	}

	private ThemeDefinition? GetThemeDefinition(string? id)
	{
		id = BuiltInThemes.NormalizeThemeId(id);
		return _themeDefinitions.FirstOrDefault(definition =>
			string.Equals(definition.Id, id, StringComparison.OrdinalIgnoreCase));
	}

	private void SelectTheme(string id)
	{
		if (_themeDefinitions.Count == 0)
		{
			return;
		}

		_settings.SelectedThemeId = GetThemeDefinition(id)?.Id ?? _themeDefinitions[0].Id;
		_screenViewModel.SelectTheme(_settings.SelectedThemeId, notify: true);
	}

	private void ThemeScrollViewer_OnSizeChanged(object sender, SizeChangedEventArgs e)
	{
		UpdateThemeGalleryCardWidth();
	}

	private void UpdateThemeGalleryCardWidth()
	{
		double galleryWidth = ThemeScrollViewer.ViewportWidth;
		if (galleryWidth <= 0)
		{
			return;
		}

		ThemeGalleryPanel.Width = galleryWidth;
		_screenViewModel.UpdateCardWidth(galleryWidth);
	}

	private void LocateCurrent_OnClick(object sender, RoutedEventArgs e)
	{
		Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
		{
			if (_screenViewModel.SelectedTheme is { } selectedTheme)
			{
				FindElementByDataContext(ThemeGalleryPanel, selectedTheme)?.BringIntoView();
			}
		});
	}

	private static FrameworkElement? FindElementByDataContext(DependencyObject root, object dataContext)
	{
		if (root is FrameworkElement element && ReferenceEquals(element.DataContext, dataContext))
		{
			return element;
		}

		for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
		{
			FrameworkElement? descendant = FindElementByDataContext(
				VisualTreeHelper.GetChild(root, index),
				dataContext);
			if (descendant is not null)
			{
				return descendant;
			}
		}

		return null;
	}
}
