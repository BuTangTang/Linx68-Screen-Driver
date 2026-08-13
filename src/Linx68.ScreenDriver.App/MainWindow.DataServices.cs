using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Linx68.ScreenDriver.Application;
using Linx68.ScreenDriver.Core;

namespace Linx68.ScreenDriver.App;

public partial class MainWindow
{
    private void SyncDataServiceControlsFromSettings()
    {
        _updatingDataServices = true;
        try
        {
            DataOnlineLyricsCheckBox.IsChecked = OnlineLyricsCheckBox.IsChecked == true;
            bool automatic = WeatherAutomaticLocationCheckBox.IsChecked == true;
            DataWeatherAutoRadio.IsChecked = automatic;
            DataWeatherManualRadio.IsChecked = !automatic;
            DataWeatherCityTextBox.Text = ReadWeatherLocation();
            DataWeatherCityTextBox.IsEnabled = !automatic;
			DataWeatherCityLabel.Text = automatic ? "定位失败时回退" : "手动城市";
            RelocateWeatherButton.IsEnabled = automatic;
        }
        finally
        {
            _updatingDataServices = false;
        }
    }

    private void DataOnlineLyricsCheckBox_OnChanged(object sender, RoutedEventArgs e)
    {
        if (_updatingDataServices || OnlineLyricsCheckBox is null)
        {
            return;
        }

        OnlineLyricsCheckBox.IsChecked = DataOnlineLyricsCheckBox.IsChecked == true;
        ScheduleAutoCommit();
    }

    private void DataWeatherMode_OnChanged(object sender, RoutedEventArgs e)
    {
        if (_updatingDataServices
            || WeatherAutomaticLocationCheckBox is null
            || DataWeatherAutoRadio is null
            || DataWeatherCityTextBox is null
            || RelocateWeatherButton is null)
        {
            return;
        }

		bool automatic = DataWeatherAutoRadio.IsChecked == true;
		Interlocked.Increment(ref _weatherSettingsVersion);
		_refreshCancellation?.Cancel();
		CancelDataServicesRefresh();
		if (!automatic)
		{
			_weatherLocationProvider.InvalidateCache();
			_automaticLocationFallback = false;
		}
        WeatherAutomaticLocationCheckBox.IsChecked = automatic;
        DataWeatherCityTextBox.IsEnabled = !automatic;
		DataWeatherCityLabel.Text = automatic ? "定位失败时回退" : "手动城市";
        RelocateWeatherButton.IsEnabled = automatic;
		_weatherDataServiceRefreshPending = true;
        ScheduleAutoCommit();
    }

    private void DataWeatherCityTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
		if (_updatingDataServices || WeatherLocationTextBox is null)
        {
            return;
		}

		CancelDataServicesRefresh();
		Interlocked.Increment(ref _weatherSettingsVersion);
		_refreshCancellation?.Cancel();
		WeatherLocationTextBox.Text = DataWeatherCityTextBox.Text;
		_weatherDataServiceRefreshPending = true;
        ScheduleAutoCommit();
    }

    private async void RefreshAllDataButton_OnClick(object sender, RoutedEventArgs e)
    {
        await RefreshAllDataServicesAsync(forceLocation: false);
    }

    private async void RefreshScreenDataButton_OnClick(object sender, RoutedEventArgs e)
    {
		// RefreshPreviewAsync owns the global activity text and rejects stale requests.
		// The button must not overwrite a newer automatic/theme refresh after awaiting.
		await RefreshPreviewAsync();
    }

    private async void RunAutomationNowButton_OnClick(object sender, RoutedEventArgs e)
    {
        HeaderRefreshTimingText.Text = "正在执行自动化…";
        var stopwatch = Stopwatch.StartNew();
        await CommitAndPushAsync();
        stopwatch.Stop();
        HeaderRefreshTimingText.Text = $"执行完成 · {stopwatch.Elapsed.TotalMilliseconds:0} ms";
    }

    private async void RefreshCodexDataButton_OnClick(object sender, RoutedEventArgs e)
    {
        _dataServicesViewModel.Codex.BeginRefresh();
        await RefreshCodexSourcesAsync(force: true);
        UpdateCodexDataServiceStatus();
        HeaderRefreshTimingText.Text = _dataServicesViewModel.Codex.Timing;
    }

    private async void RelocateWeatherButton_OnClick(object sender, RoutedEventArgs e)
    {
		CancellationTokenSource refreshCancellation = BeginDataServicesRefresh(out long refreshVersion);
        _dataServicesViewModel.Weather.BeginRefresh("正在请求 Windows 位置服务");
        var stopwatch = Stopwatch.StartNew();
        try
        {
			AutomaticWeatherLocationResult result = await _weatherLocationProvider.TryGetDetailsAsync(
				forceRefresh: true,
				refreshCancellation.Token);
			EnsureCurrentDataServicesRefresh(refreshCancellation.Token, refreshVersion);
            stopwatch.Stop();
            _lastWeatherLocationResult = result;
			// Relocation is location-only until a weather theme refreshes. Never mix a
			// newly resolved city with weather retained from the previous location.
			UpdateWeatherDataServiceStatus(null, result, weatherWasRequested: true);
            _dataServicesViewModel.Weather.Timing = $"{stopwatch.Elapsed.TotalMilliseconds:0} ms";
            if (GetSelectedThemeDefinition()?.Requires(ThemeDataRequirements.Weather) == true)
            {
                await RefreshPreviewAsync();
				EnsureCurrentDataServicesRefresh(refreshCancellation.Token, refreshVersion);
            }
        }
		catch (OperationCanceledException) when (refreshCancellation.IsCancellationRequested
			|| refreshVersion != Volatile.Read(ref _dataServicesRefreshVersion))
		{
			// A newer mode, city or relocation request owns the visible state.
		}
        catch (Exception ex)
        {
            stopwatch.Stop();
            _dataServicesViewModel.Weather.Set(
                DataLoadState.Error,
                "重新定位失败",
                ex.Message,
                stopwatch.Elapsed);
        }
		finally
		{
			CompleteDataServicesRefresh(refreshCancellation);
		}
    }

    private async Task RefreshAllDataServicesAsync(bool forceLocation)
    {
		CancellationTokenSource refreshCancellation = BeginDataServicesRefresh(out long refreshVersion);
        var stopwatch = Stopwatch.StartNew();
        _dataServicesViewModel.BeginRefresh();
        HeaderRefreshTimingText.Text = "正在并行刷新…";

		Task codexTask = RefreshCodexSourcesAsync(force: true, refreshCancellation.Token);
		WeatherSettings requestedWeatherSettings = new()
		{
			LocationQuery = ReadWeatherLocation(),
			UseAutomaticLocation = WeatherAutomaticLocationCheckBox.IsChecked == true
		};
		// Refresh All is an explicit flush of the visible controls. Use the same
		// immutable values for the status read and the preview refresh, even when
		// the normal 450 ms settings commit has not fired yet.
		_settings.Weather = requestedWeatherSettings;
		bool useAutomaticLocation = requestedWeatherSettings.UseAutomaticLocation;
		string manualCity = requestedWeatherSettings.LocationQuery;
		var locationStopwatch = Stopwatch.StartNew();
		Task<AutomaticWeatherLocationResult> locationTask = ReadLocationForDataServicesAsync(
			_weatherLocationProvider,
			useAutomaticLocation,
			forceLocation,
			manualCity,
			refreshCancellation.Token);

        try
        {
			AutomaticWeatherLocationResult location = await locationTask;
			EnsureCurrentDataServicesRefresh(refreshCancellation.Token, refreshVersion);
			locationStopwatch.Stop();
			var weatherStopwatch = Stopwatch.StartNew();
			Task<WeatherSnapshot> weatherTask = ReadWeatherForDataServicesAsync(
				useAutomaticLocation,
				location,
				manualCity,
				refreshCancellation.Token);
			await Task.WhenAll(codexTask, weatherTask);
			WeatherSnapshot weather = await weatherTask;
			EnsureCurrentDataServicesRefresh(refreshCancellation.Token, refreshVersion);
			weatherStopwatch.Stop();
			bool previewRefreshed = await RefreshPreviewAsync();
			EnsureCurrentDataServicesRefresh(refreshCancellation.Token, refreshVersion);
			_lastWeatherLocationResult = location;
			_automaticLocationFallback = useAutomaticLocation && location.Location is null;
			if (previewRefreshed && _latestSnapshot is not null)
			{
				_latestSnapshot = _latestSnapshot with { Weather = weather };
			}
            UpdateCodexDataServiceStatus();
			UpdateWeatherDataServiceStatus(weather, location, weatherWasRequested: true);
			_dataServicesViewModel.Weather.Timing =
				$"{(locationStopwatch.Elapsed + weatherStopwatch.Elapsed).TotalMilliseconds:0} ms";
            stopwatch.Stop();
            _dataServicesViewModel.CompleteRefresh(stopwatch.Elapsed, DateTimeOffset.Now);
			HeaderRefreshTimingText.Text = $"数据源更新 {DateTime.Now:HH:mm:ss} · {stopwatch.Elapsed.TotalMilliseconds:0} ms";
        }
		catch (OperationCanceledException) when (refreshCancellation.IsCancellationRequested
			|| refreshVersion != Volatile.Read(ref _dataServicesRefreshVersion))
		{
			// The newer request or settings state will publish the next result.
		}
        catch (Exception ex)
        {
            stopwatch.Stop();
            HeaderRefreshTimingText.Text = "部分数据刷新失败";
            Trace.TraceWarning($"Failed to refresh all data services: {ex}");
            _dataServicesViewModel.CompleteRefresh(stopwatch.Elapsed, DateTimeOffset.Now);
        }
		finally
		{
			CompleteDataServicesRefresh(refreshCancellation);
		}
    }

	private async Task RefreshWeatherDataServiceAfterSettingsChangeAsync(
		WeatherSettings requestedSettings,
		long requestedWeatherVersion)
	{
		CancellationTokenSource refreshCancellation = BeginDataServicesRefresh(out long refreshVersion);
		var stopwatch = Stopwatch.StartNew();
		_dataServicesViewModel.Weather.BeginRefresh("正在应用新的天气位置");
		try
		{
			bool useAutomaticLocation = requestedSettings.UseAutomaticLocation;
			string manualCity = string.IsNullOrWhiteSpace(requestedSettings.LocationQuery)
				? "北京"
				: requestedSettings.LocationQuery.Trim();
			AutomaticWeatherLocationResult location = await ReadLocationForDataServicesAsync(
				_weatherLocationProvider,
				useAutomaticLocation,
				forceLocation: false,
				manualCity,
				refreshCancellation.Token);
			EnsureCurrentDataServicesRefresh(refreshCancellation.Token, refreshVersion);
			EnsureCurrentWeatherSettings(requestedWeatherVersion);
			WeatherSnapshot weather = await ReadWeatherForDataServicesAsync(
				useAutomaticLocation,
				location,
				manualCity,
				refreshCancellation.Token);
			EnsureCurrentDataServicesRefresh(refreshCancellation.Token, refreshVersion);
			EnsureCurrentWeatherSettings(requestedWeatherVersion);
			stopwatch.Stop();
			_lastWeatherLocationResult = location;
			_automaticLocationFallback = useAutomaticLocation && location.Location is null;
			if (_latestSnapshot is not null)
			{
				_latestSnapshot = _latestSnapshot with { Weather = weather };
			}
			UpdateWeatherDataServiceStatus(weather, location, weatherWasRequested: true);
			_dataServicesViewModel.Weather.Timing = $"{stopwatch.Elapsed.TotalMilliseconds:0} ms";
			_dataServicesViewModel.UpdateOverallStatus();
		}
		catch (OperationCanceledException) when (refreshCancellation.IsCancellationRequested
			|| refreshVersion != Volatile.Read(ref _dataServicesRefreshVersion))
		{
			// The next debounced settings commit owns the visible weather state.
		}
		catch (Exception ex)
		{
			stopwatch.Stop();
			_dataServicesViewModel.Weather.Set(
				DataLoadState.Error,
				"天气设置已保存，刷新失败",
				ex.Message,
				stopwatch.Elapsed);
			_dataServicesViewModel.UpdateOverallStatus();
		}
		finally
		{
			CompleteDataServicesRefresh(refreshCancellation);
		}
	}

	private static Task<AutomaticWeatherLocationResult> ReadLocationForDataServicesAsync(
		IAutomaticWeatherLocationProvider provider,
		bool useAutomaticLocation,
		bool forceLocation,
		string manualCity,
		CancellationToken cancellationToken = default) => useAutomaticLocation
		? provider.TryGetDetailsAsync(forceRefresh: forceLocation, cancellationToken)
		: Task.FromResult(new AutomaticWeatherLocationResult(
			DataLoadState.Ready,
			null,
			$"手动城市 · {manualCity}",
			DateTimeOffset.Now));

	private Task<WeatherSnapshot> ReadWeatherForDataServicesAsync(
		bool useAutomaticLocation,
		AutomaticWeatherLocationResult location,
		string manualCity,
		CancellationToken cancellationToken)
	{
		WeatherSettings weatherSettings = useAutomaticLocation && location.Location is { } automatic
			? new WeatherSettings
			{
				LocationQuery = manualCity,
				UseAutomaticLocation = true,
				Latitude = automatic.Latitude,
				Longitude = automatic.Longitude,
				AutomaticLocationName = automatic.DisplayName
			}
			: new WeatherSettings
			{
				LocationQuery = manualCity,
				UseAutomaticLocation = false
			};
		return _weatherSource.ReadAsync(weatherSettings, cancellationToken);
	}

    private async Task RefreshCodexSourcesAsync(
		bool force,
		CancellationToken cancellationToken = default)
    {
		Task<AiQuotaSnapshot> quotaTask = ReadCodexQuotaAsync(force, cancellationToken);
		Task<CodexTaskSnapshot> tasksTask = ReadCodexTasksAsync(force, cancellationToken);
        await Task.WhenAll(quotaTask, tasksTask);
    }

	private CancellationTokenSource BeginDataServicesRefresh(out long refreshVersion)
	{
		var refreshCancellation = new CancellationTokenSource();
		CancellationTokenSource? previous = Interlocked.Exchange(
			ref _dataServicesRefreshCancellation,
			refreshCancellation);
		previous?.Cancel();
		refreshVersion = Interlocked.Increment(ref _dataServicesRefreshVersion);
		return refreshCancellation;
	}

	private void CancelDataServicesRefresh()
	{
		Interlocked.Increment(ref _dataServicesRefreshVersion);
		CancellationTokenSource? active = Interlocked.Exchange(
			ref _dataServicesRefreshCancellation,
			null);
		active?.Cancel();
	}

	private void EnsureCurrentDataServicesRefresh(
		CancellationToken cancellationToken,
		long refreshVersion)
	{
		cancellationToken.ThrowIfCancellationRequested();
		if (refreshVersion != Volatile.Read(ref _dataServicesRefreshVersion))
		{
			throw new OperationCanceledException(cancellationToken);
		}
	}

	private void EnsureCurrentWeatherSettings(long requestedWeatherVersion)
	{
		if (requestedWeatherVersion != Volatile.Read(ref _weatherSettingsVersion))
		{
			throw new OperationCanceledException("Weather settings were replaced by a newer edit.");
		}
	}

	private void CompleteDataServicesRefresh(CancellationTokenSource refreshCancellation)
	{
		Interlocked.CompareExchange(
			ref _dataServicesRefreshCancellation,
			null,
			refreshCancellation);
		refreshCancellation.Dispose();
	}

    private void ShowPreviewRefreshStarted()
    {
        if (_lastRefreshCompletedAt is null)
        {
            HeaderRefreshTimingText.Text = "正在刷新…";
        }
    }

    private void ShowPreviewRefreshCompleted()
    {
        if (_lastRefreshCompletedAt is not { } completedAt)
        {
            return;
        }

        HeaderRefreshTimingText.Text =
			$"预览更新 {completedAt:HH:mm:ss} · {_lastRefreshDuration.TotalMilliseconds:0} ms";
    }

    private void ShowPreviewRefreshFailed()
    {
        HeaderRefreshTimingText.Text = _lastRefreshCompletedAt is { } completedAt
			? $"上次预览 {completedAt:HH:mm:ss} · 刷新失败"
            : "刷新失败";
    }
}
