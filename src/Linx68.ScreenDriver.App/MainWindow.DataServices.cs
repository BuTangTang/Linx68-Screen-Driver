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
        WeatherAutomaticLocationCheckBox.IsChecked = automatic;
        DataWeatherCityTextBox.IsEnabled = !automatic;
        RelocateWeatherButton.IsEnabled = automatic;
        ScheduleAutoCommit();
    }

    private void DataWeatherCityTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_updatingDataServices || WeatherLocationTextBox is null)
        {
            return;
        }

        WeatherLocationTextBox.Text = DataWeatherCityTextBox.Text;
        ScheduleAutoCommit();
    }

    private async void RefreshAllDataButton_OnClick(object sender, RoutedEventArgs e)
    {
        await RefreshAllDataServicesAsync(forceLocation: false);
    }

    private async void RefreshScreenDataButton_OnClick(object sender, RoutedEventArgs e)
    {
        HeaderRefreshTimingText.Text = "正在刷新…";
        var stopwatch = Stopwatch.StartNew();
        bool refreshed = await RefreshPreviewAsync();
        stopwatch.Stop();
        HeaderRefreshTimingText.Text = refreshed
            ? $"更新于 {DateTime.Now:HH:mm:ss} · {stopwatch.Elapsed.TotalMilliseconds:0} ms"
            : "刷新已由更新的请求替代";
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
        _dataServicesViewModel.Weather.BeginRefresh("正在请求 Windows 位置服务");
        var stopwatch = Stopwatch.StartNew();
        try
        {
            AutomaticWeatherLocationResult result = await _weatherLocationProvider.TryGetDetailsAsync(forceRefresh: true);
            stopwatch.Stop();
            _lastWeatherLocationResult = result;
            UpdateWeatherDataServiceStatus(_latestSnapshot?.Weather, result, weatherWasRequested: false);
            _dataServicesViewModel.Weather.Timing = $"{stopwatch.Elapsed.TotalMilliseconds:0} ms";
            if (GetSelectedThemeDefinition()?.Requires(ThemeDataRequirements.Weather) == true)
            {
                await RefreshPreviewAsync();
            }
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
    }

    private async Task RefreshAllDataServicesAsync(bool forceLocation)
    {
        var stopwatch = Stopwatch.StartNew();
        _dataServicesViewModel.BeginRefresh();
        HeaderRefreshTimingText.Text = "正在并行刷新…";

        Task codexTask = RefreshCodexSourcesAsync(force: true);
        Task<AutomaticWeatherLocationResult> locationTask = _weatherLocationProvider.TryGetDetailsAsync(
            forceRefresh: forceLocation);

        try
        {
            await Task.WhenAll(codexTask, locationTask);
            _lastWeatherLocationResult = await locationTask;
            await RefreshPreviewAsync();
            UpdateCodexDataServiceStatus();
            UpdateWeatherDataServiceStatus(_latestSnapshot?.Weather, _lastWeatherLocationResult, weatherWasRequested: false);
            stopwatch.Stop();
            _dataServicesViewModel.CompleteRefresh(stopwatch.Elapsed, DateTimeOffset.Now);
            HeaderRefreshTimingText.Text = $"更新于 {DateTime.Now:HH:mm:ss} · {stopwatch.Elapsed.TotalMilliseconds:0} ms";
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            HeaderRefreshTimingText.Text = "部分数据刷新失败";
            Trace.TraceWarning($"Failed to refresh all data services: {ex}");
            _dataServicesViewModel.CompleteRefresh(stopwatch.Elapsed, DateTimeOffset.Now);
        }
    }

    private async Task RefreshCodexSourcesAsync(bool force)
    {
        Task<AiQuotaSnapshot> quotaTask = ReadCodexQuotaAsync(force);
        Task<CodexTaskSnapshot> tasksTask = ReadCodexTasksAsync(force);
        await Task.WhenAll(quotaTask, tasksTask);
    }
}
