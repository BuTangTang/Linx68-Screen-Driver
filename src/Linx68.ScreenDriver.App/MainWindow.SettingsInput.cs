using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfKey = System.Windows.Input.Key;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;
using WpfTextBox = System.Windows.Controls.TextBox;
using WpfDataFormats = System.Windows.DataFormats;

namespace Linx68.ScreenDriver.App;

public partial class MainWindow
{
	private void ThemeOptionComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		ScheduleAutoCommit();
	}

	private void SettingToggle_OnChanged(object sender, RoutedEventArgs e)
	{
		ScheduleAutoCommit();
	}

	private void WeatherAutomaticLocationCheckBox_OnChanged(object sender, RoutedEventArgs e)
	{
		if (WeatherLocationTextBox is not null)
		{
			WeatherLocationTextBox.IsEnabled = WeatherAutomaticLocationCheckBox.IsChecked != true;
		}
		ScheduleAutoCommit();
	}

	private void SettingTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
	{
		ScheduleAutoCommit();
	}

	private void EndpointIpPart_OnPreviewTextInput(object sender, TextCompositionEventArgs e)
	{
		e.Handled = !e.Text.All(char.IsDigit);
	}

	private void EndpointIpPart_OnTextChanged(object sender, TextChangedEventArgs e)
	{
		if (sender is not WpfTextBox textBox)
		{
			return;
		}

		if (textBox.Text.Length == 3 && int.TryParse(textBox.Text, out int value) && value <= 255)
		{
			int index = Array.IndexOf(_endpointParts, textBox);
			if (index >= 0 && index < _endpointParts.Length - 1)
			{
				_endpointParts[index + 1].Focus();
				_endpointParts[index + 1].SelectAll();
			}
		}
	}

	private void EndpointIpPart_OnPreviewKeyDown(object sender, WpfKeyEventArgs e)
	{
		if (sender is not WpfTextBox textBox)
		{
			return;
		}

		int index = Array.IndexOf(_endpointParts, textBox);
		if (e.Key == WpfKey.Back && textBox.Text.Length == 0 && index > 0)
		{
			_endpointParts[index - 1].Focus();
			_endpointParts[index - 1].CaretIndex = _endpointParts[index - 1].Text.Length;
			e.Handled = true;
		}
		else if ((e.Key == WpfKey.OemPeriod || e.Key == WpfKey.Decimal) &&
			index >= 0 && index < _endpointParts.Length - 1)
		{
			_endpointParts[index + 1].Focus();
			_endpointParts[index + 1].SelectAll();
			e.Handled = true;
		}
	}

	private void EndpointIpPart_OnPasting(object sender, DataObjectPastingEventArgs e)
	{
		if (!e.SourceDataObject.GetDataPresent(WpfDataFormats.UnicodeText))
		{
			e.CancelCommand();
			return;
		}

		string pasted = (e.SourceDataObject.GetData(WpfDataFormats.UnicodeText) as string ?? string.Empty).Trim();
		if (TryPopulateEndpointParts(pasted))
		{
			e.CancelCommand();
			EndpointIpPart4.Focus();
			EndpointIpPart4.CaretIndex = EndpointIpPart4.Text.Length;
			return;
		}

		if (pasted.Length is < 1 or > 3 || !pasted.All(char.IsDigit))
		{
			e.CancelCommand();
		}
	}

	private void PopulateEndpointParts(string? value)
	{
		_ = _settingsViewModel.SetEndpoint(value);
		UpdateEndpointSummary();
	}

	private bool TryPopulateEndpointParts(string? value)
	{
		bool isValid = _settingsViewModel.SetEndpoint(value);
		if (isValid)
		{
			UpdateEndpointSummary();
		}
		return isValid;
	}
}
