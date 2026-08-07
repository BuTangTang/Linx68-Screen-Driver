using System.IO;
using System.Net;
using System.Net.Http;
using Linx68.ScreenDriver.Application;
using Linx68.ScreenDriver.Core;
using Linx68.ScreenDriver.Infrastructure;

var defaults = new AppSettings();
Assert(defaults.SelectedThemeId == "clock-dot-matrix", "first-run theme must default to dot-matrix clock");
Assert(defaults.AccentColor == "#E4694C", "first-run accent color is incorrect");
Assert(defaults.AutoPush && defaults.RefreshSeconds == 1, "first-run automation defaults are incorrect");
Assert(defaults.MinimizeToTray && defaults.CloseToTray, "first-run tray defaults are incorrect");
Assert(defaults.Weather.UseAutomaticLocation, "first-run weather must use automatic location");
Assert(defaults.SafeArea == new ScreenInsets(10, 52, 10, 12), "first-run safe area is incorrect");
Assert(defaults.AppearanceMode == AppearanceMode.System, "first-run appearance must follow Windows");

var orderedSessions = WindowsMusicSessionSelector.Order(
[
    new MusicSessionCandidate(0, "chrome.exe", true, false),
    new MusicSessionCandidate(1, "cloudmusic.exe", false, true),
    new MusicSessionCandidate(2, "spotify.exe", false, true)
]);
Assert(orderedSessions.SequenceEqual([1, 2, 0]), "playing NetEase session must outrank paused current and other playing sessions");
orderedSessions = WindowsMusicSessionSelector.Order(
[
    new MusicSessionCandidate(0, "cloudmusic.exe", false, true),
    new MusicSessionCandidate(1, "spotify.exe", true, true),
    new MusicSessionCandidate(2, "netease.music", false, false)
]);
Assert(orderedSessions.SequenceEqual([1, 0, 2]), "playing current session must remain the first choice");
Assert(WindowsMusicSessionSelector.IsNetEase("cloudmusic.exe") && WindowsMusicSessionSelector.IsNetEase("NetEaseMusic"),
    "NetEase identifiers must be recognized case-insensitively");
Console.WriteLine("PASS Windows media session ordering and NetEase identifiers");

if (args.Contains("--music-probe", StringComparer.OrdinalIgnoreCase))
{
    var liveMusic = await new WindowsMusicSnapshotSource().ReadAsync();
    Console.WriteLine(liveMusic is null || !liveMusic.Available
        ? "PROBE music session: unavailable"
        : $"PROBE music session: source={liveMusic.SourceAppId}; playing={liveMusic.IsPlaying}; title={liveMusic.Title}; artist={liveMusic.Artist}; position={liveMusic.Position:c}; duration={liveMusic.Duration:c}; artwork={liveMusic.Artwork is { Length: > 0 }}");
}

var profile = ScreenProfile.KeyboardDisplay;
Assert(profile.SafeArea.Top == 52, "keyboard firmware safe area must reserve the top status pills");
Assert(profile.SafeArea.Left + profile.SafeArea.Right < profile.Width, "safe area horizontal insets are invalid");
Assert(profile.SafeArea.Top + profile.SafeArea.Bottom < profile.Height, "safe area vertical insets are invalid");
var renderer = new ScreenRenderer(profile);
var themeDefinitions = BuiltInThemes.CreateDefinitions(new ImageTheme());
var themes = themeDefinitions.Select(definition => definition.Theme).ToArray();
Assert(themes.Length == 19, "built-in theme catalog should contain the 19 supported schemes");
Assert(themes.All(theme => theme.Id is not "calendar" and not "ambient"), "removed calendar/ambient themes must not be registered");
Assert(themes.All(theme => theme.Id != "clock-seconds"), "removed seconds progress theme must not be registered");
Assert(themes.All(theme => theme.Id != "week"), "removed week calendar theme must not be registered");
Assert(themes.Single(theme => theme.Id == "clock-dot-matrix").DisplayName == "点阵时钟", "dot-matrix clock theme must be registered");
Assert(themes.Single(theme => theme.Id == "clock-weather-dot").DisplayName == "点阵时钟天气", "dot-matrix weather clock theme must be registered");
Assert(themes.Single(theme => theme.Id == "image").DisplayName == "图片时间", "image theme must be named 图片时间");
Assert(themes.Single(theme => theme.Id == "ai-quota").DisplayName == "AI用量 (Beta)", "AI quota theme must carry the Beta label");
Assert(themes.Any(theme => theme.Id == "weather-five-day"), "five-day weather theme must be registered");
Assert(themes.Any(theme => theme.Id == "stocks"), "stock theme must be registered");
Assert(themes.Single(theme => theme.Id == "music-vinyl").DisplayName == "动态黑胶", "dynamic vinyl theme must be registered");
Assert(themes.Single(theme => theme.Id == "music-cassette").DisplayName == "动态磁带", "dynamic cassette theme must be registered");
Assert(themes.Select(theme => theme.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() == themes.Length, "theme ids should be unique");
Assert(themeDefinitions.All(definition => definition.Category != ThemeCategory.Other), "every built-in theme must declare a category");
Assert(themeDefinitions.Single(definition => definition.Id == "image").IsStatic, "image theme must declare static rendering");
Assert(themeDefinitions.Single(definition => definition.Id == "weather-five-day").Requires(ThemeDataRequirements.Weather), "weather theme must declare its data requirement");
Assert(themeDefinitions.Single(definition => definition.Id == "music-vinyl").Requires(ThemeDataRequirements.Lyrics), "vinyl theme must declare its optional lyrics requirement");
Assert(themeDefinitions.Single(definition => definition.Id == "stocks").Shows(ThemeSettingsSections.Stocks), "stock theme must expose stock settings");
Console.WriteLine("PASS built-in theme metadata, requirements and settings sections");

var pipelineSystem = new StubSystemSnapshotSource();
var pipelineLyrics = new StubLyricsSnapshotSource();
var pipelineWeather = new StubWeatherSnapshotSource();
var pipelineStocks = new StubStockSnapshotSource();
var snapshotBuilder = new DashboardSnapshotBuilder(
    pipelineSystem,
    pipelineLyrics,
    pipelineWeather,
    pipelineStocks);
var pipelineSettings = new AppSettings();
var clockSnapshot = await snapshotBuilder.BuildAsync(
    themeDefinitions.Single(definition => definition.Id == "clock"),
    pipelineSettings,
    SystemSnapshot.DesignSample.Music!,
    effectiveWeatherSettings: null,
    aiQuota: null);
Assert(clockSnapshot.Music is not null, "snapshot pipeline must preserve the supplied media snapshot");
Assert(pipelineLyrics.ReadCount == 0 && pipelineWeather.ReadCount == 0 && pipelineStocks.ReadCount == 0,
    "clock theme must not invoke optional data sources");
pipelineSettings.Music.EnableOnlineLyrics = true;
var vinylSnapshot = await snapshotBuilder.BuildAsync(
    themeDefinitions.Single(definition => definition.Id == "music-vinyl"),
    pipelineSettings,
    SystemSnapshot.DesignSample.Music!,
    effectiveWeatherSettings: null,
    aiQuota: null);
Assert(pipelineLyrics.ReadCount == 1 && vinylSnapshot.Music?.Lyrics.Available == true,
    "lyrics-capable theme must invoke the lyrics source when enabled");
_ = await snapshotBuilder.BuildAsync(
    themeDefinitions.Single(definition => definition.Id == "weather-five-day"),
    pipelineSettings,
    SystemSnapshot.DesignSample.Music!,
    new WeatherSettings { LocationQuery = "北京" },
    aiQuota: null);
Assert(pipelineWeather.ReadCount == 1 && pipelineStocks.ReadCount == 0,
    "weather theme must invoke only the weather source");
Console.WriteLine("PASS metadata-driven dashboard snapshot pipeline");

var refreshMusic = new StubMusicSnapshotSource(SystemSnapshot.DesignSample.Music! with
{
    IsPlaying = true
});
var refreshLyrics = new StubLyricsSnapshotSource();
var refreshWeather = new StubWeatherSnapshotSource();
var refreshStocks = new StubStockSnapshotSource();
var refreshSnapshotBuilder = new DashboardSnapshotBuilder(
    new StubSystemSnapshotSource(),
    refreshLyrics,
    refreshWeather,
    refreshStocks);
var refreshWeatherResolver = new StubWeatherSettingsResolver(
    new WeatherSettingsResolution(new WeatherSettings { LocationQuery = "北京" }, true));
var refreshService = new DashboardRefreshService(
    refreshMusic,
    refreshSnapshotBuilder,
    refreshWeatherResolver);
var refreshSettings = new AppSettings
{
    AutoMediaThemeSwitch = true,
    MediaPlayingThemeId = "music-vinyl",
    MediaIdleThemeId = "system"
};
refreshSettings.Music.EnableOnlineLyrics = true;
var refreshResult = await refreshService.RefreshAsync(new DashboardRefreshRequest(
    themeDefinitions,
    refreshSettings,
    "clock",
    "clock",
    _ => throw new InvalidOperationException("AI source must not be read for a music theme.")));
Assert(refreshResult.EffectiveTheme.Id == "music-vinyl" && refreshResult.EffectiveThemeChanged,
    "refresh service must resolve the configured playing-media theme");
Assert(refreshLyrics.ReadCount == 1 && refreshWeatherResolver.ReadCount == 0,
    "refresh service must request only the metadata-required sources");

refreshSettings.AutoMediaThemeSwitch = false;
int aiReadCount = 0;
refreshResult = await refreshService.RefreshAsync(new DashboardRefreshRequest(
    themeDefinitions,
    refreshSettings,
    "ai-quota",
    refreshResult.EffectiveTheme.Id,
    _ =>
    {
        aiReadCount++;
        return Task.FromResult<AiQuotaSnapshot?>(AiQuotaSnapshot.ForSubscription("Test", 50));
    }));
Assert(refreshResult.EffectiveTheme.Id == "ai-quota" && aiReadCount == 1,
    "refresh service must request AI data only for an AI theme");

refreshResult = await refreshService.RefreshAsync(new DashboardRefreshRequest(
    themeDefinitions,
    refreshSettings,
    "weather-five-day",
    refreshResult.EffectiveTheme.Id));
Assert(refreshResult.EffectiveTheme.Id == "weather-five-day"
       && refreshWeatherResolver.ReadCount == 1
       && refreshResult.UsedAutomaticWeatherLocationFallback,
    "refresh service must surface automatic weather-location fallback state");
Console.WriteLine("PASS application refresh service resolves theme and requests data on demand");

var unavailableLocationProvider = new StubAutomaticWeatherLocationProvider(null);
var weatherSettingsResolver = new WeatherSettingsResolver(unavailableLocationProvider);
var weatherResolution = await weatherSettingsResolver.ResolveAsync(new WeatherSettings
{
    LocationQuery = "上海",
    UseAutomaticLocation = true
});
Assert(weatherResolution.UsedAutomaticLocationFallback
       && weatherResolution.Settings.LocationQuery == "上海"
       && !weatherResolution.Settings.UseAutomaticLocation,
    "weather resolver must fall back to the saved city when automatic location is unavailable");
var availableLocationProvider = new StubAutomaticWeatherLocationProvider(
    new AutomaticWeatherLocation(31.2304, 121.4737, "当前位置"));
weatherSettingsResolver = new WeatherSettingsResolver(availableLocationProvider);
weatherResolution = await weatherSettingsResolver.ResolveAsync(new WeatherSettings());
Assert(!weatherResolution.UsedAutomaticLocationFallback
       && weatherResolution.Settings.Latitude == 31.2304
       && weatherResolution.Settings.AutomaticLocationName == "当前位置",
    "weather resolver must retain automatic coordinates and display name");
Console.WriteLine("PASS weather settings resolver handles automatic-location fallback");

var recordingTransport = new StubDeviceTransport();
var displayPushService = new DisplayPushService(recordingTransport);
RenderedFrame pushFrame = renderer.Render(themes[0], SystemSnapshot.DesignSample);
DevicePushResult invalidEndpointResult = await displayPushService.PushAsync("not an IP", pushFrame);
Assert(!invalidEndpointResult.Success && recordingTransport.PushCount == 0,
    "invalid display endpoint must not invoke transport");
DevicePushResult validEndpointResult = await displayPushService.PushAsync("http://192.168.1.8/other", pushFrame);
Assert(validEndpointResult.Success
       && recordingTransport.LastEndpoint?.AbsoluteUri == "http://192.168.1.8/image/upload"
       && recordingTransport.PushCount == 1,
    "display push service must normalize a valid IPv4 endpoint");
Console.WriteLine("PASS display push service validates and normalizes endpoints");

var aiQuotaTheme = themes.Single(theme => theme.Id == "ai-quota");
var subscriptionQuota = AiQuotaSnapshot.ForSubscription(
    "ChatGPT",
    56,
    remainingCount: 1,
    resetPeriod: AiResetPeriod.Weekly);
Assert(subscriptionQuota.RemainingDisplay == "56% / 1次", "subscription quota display is incorrect");
Assert(subscriptionQuota.ResetPeriod == AiResetPeriod.Weekly, "subscription reset period was not retained");

var tokenBalance = new AiQuotaBalance(
    AiQuotaMetric.Token,
    Used: 440_000,
    Limit: 1_000_000,
    UnitLabel: "token");
var apiKeyQuota = AiQuotaSnapshot.ForApiKey(
    "OpenAI API",
    tokenBalance,
    AiResetPeriod.BillingCycle);
Assert(Math.Abs(apiKeyQuota.ClampedRemainingPercent - 56) < 0.001, "API Key token balance percentage is incorrect");
Assert(apiKeyQuota.Balance?.Metric == AiQuotaMetric.Token, "API Key metric was not retained");
Assert(apiKeyQuota.RemainingDisplay == "56%", "API Key quota display is incorrect");
Assert(AiQuotaSnapshot.ForSubscription("Test", 140).ClampedRemainingPercent == 100, "quota percentage must clamp to 100");

foreach (var theme in themes)
{
    var frame = renderer.Render(theme, SystemSnapshot.DesignSample);
    Assert(frame.Width == 142 && frame.Height == 428, $"{theme.Id}: resolution mismatch");
    Assert(frame.JpegBytes.Length <= profile.MaxJpegBytes, $"{theme.Id}: JPEG exceeds device limit");
    Assert(frame.JpegBytes is [0xFF, 0xD8, ..], $"{theme.Id}: missing JPEG SOI marker");
    Assert(FindStartOfFrame(frame.JpegBytes) == 0xC0, $"{theme.Id}: JPEG is not baseline SOF0");
    Console.WriteLine($"PASS render {theme.Id,-8} {frame.JpegBytes.Length,7} bytes, baseline JPEG 142x428");
}

var subscriptionFrame = renderer.Render(
    aiQuotaTheme,
    SystemSnapshot.DesignSample with { AiQuota = subscriptionQuota });
Assert(subscriptionFrame.JpegBytes is [0xFF, 0xD8, ..], "subscription AI quota theme did not render");

var apiKeyFrame = renderer.Render(
    aiQuotaTheme,
    SystemSnapshot.DesignSample with { AiQuota = apiKeyQuota });
Assert(apiKeyFrame.JpegBytes is [0xFF, 0xD8, ..], "API Key AI quota theme did not render");
Console.WriteLine("PASS AI quota model and single-platform theme for subscription/API Key data");

var timedLyrics = LrcLibLyricsSnapshotSource.ParseSyncedLyrics("[00:01.20] First line\n[00:03.450] 第二行\ninvalid");
Assert(timedLyrics.Count == 2, "synchronized lyrics parser must ignore invalid lines");
Assert(timedLyrics[0].Timestamp == TimeSpan.FromMilliseconds(1200), "two-digit lyric fraction was not normalized");
Assert(timedLyrics[1].Text == "第二行", "Unicode lyric text was not retained");
var lyricSnapshot = new LyricsSnapshot(true, timedLyrics);
var lyricPosition = lyricSnapshot.FindAt(TimeSpan.FromSeconds(2));
Assert(lyricPosition.Current?.Text == "First line" && lyricPosition.Next?.Text == "第二行", "active lyric lookup failed");
Console.WriteLine("PASS LRCLIB synchronized lyrics parser and active-line lookup");

var lyricResponses = new Queue<string>(new[]
{
    """[{"trackName":"Midnight Drive","artistName":"Keyboard Studio","duration":225,"syncedLyrics":"[00:01.20] First line\n[00:03.45] Second line"}]"""
});
var lyricHandler = new SequenceHandler(lyricResponses);
using (var lyricClient = new HttpClient(lyricHandler))
using (var lyricSource = new LrcLibLyricsSnapshotSource(lyricClient))
{
    var fetchedLyrics = await lyricSource.ReadAsync(SystemSnapshot.DesignSample.Music!);
    Assert(fetchedLyrics.Available && fetchedLyrics.Lines.Count == 2, "LRCLIB response was not parsed");
    var cachedLyrics = await lyricSource.ReadAsync(SystemSnapshot.DesignSample.Music!);
    Assert(ReferenceEquals(fetchedLyrics, cachedLyrics), "LRCLIB result should be cached per track");
    Assert(lyricHandler.RequestCount == 1, "cached lyrics read must not call LRCLIB again");
}
Console.WriteLine("PASS LRCLIB search response and per-track cache");

var animatedMusic = SystemSnapshot.DesignSample.Music! with
{
    Position = TimeSpan.FromSeconds(10),
    Lyrics = lyricSnapshot,
    SourceAppId = "Spotify.exe"
};
var vinylTheme = themes.Single(theme => theme.Id == "music-vinyl");
var vinylFrameA = renderer.Render(vinylTheme, SystemSnapshot.DesignSample with { Music = animatedMusic });
var vinylFrameB = renderer.Render(vinylTheme, SystemSnapshot.DesignSample with
{
    Music = animatedMusic with { Position = TimeSpan.FromSeconds(11) }
});
Assert(!vinylFrameA.JpegBytes.SequenceEqual(vinylFrameB.JpegBytes), "dynamic vinyl frames must change with playback position");
Console.WriteLine("PASS dynamic vinyl frame changes with playback position");

var weatherResponses = new Queue<string>(new[]
{
    """{"results":[{"name":"北京","latitude":39.9042,"longitude":116.4074}]}""",
    """{"current":{"temperature_2m":31.4,"apparent_temperature":34.2,"relative_humidity_2m":58,"weather_code":2,"is_day":1},"daily":{"time":["2026-07-28","2026-07-29","2026-07-30","2026-07-31","2026-08-01"],"weather_code":[2,3,61,1,0],"temperature_2m_max":[32,31,29,33,34],"temperature_2m_min":[24,23,22,24,25]}}"""
});
var weatherHandler = new SequenceHandler(weatherResponses);
using (var weatherClient = new HttpClient(weatherHandler))
using (var weatherSource = new OpenMeteoWeatherSnapshotSource(weatherClient))
{
    var weatherSnapshot = await weatherSource.ReadAsync(new WeatherSettings { LocationQuery = "北京" });
    Assert(weatherSnapshot.Available, "Open-Meteo weather snapshot must be available");
    Assert(weatherSnapshot.LocationName == "北京", "weather location was not parsed");
    Assert(Math.Abs(weatherSnapshot.TemperatureC - 31.4) < 0.001, "weather temperature was not parsed");
    Assert(weatherSnapshot.RelativeHumidityPercent == 58, "weather humidity was not parsed");
    Assert(weatherSnapshot.ConditionText == "多云", "WMO weather condition mapping failed");
    Assert(weatherSnapshot.DailyForecast?.Count == 5, "five-day weather forecast was not parsed");
    Assert(weatherSnapshot.DailyForecast![2].ConditionText == "雨", "daily WMO weather condition mapping failed");
    var cachedWeather = await weatherSource.ReadAsync(new WeatherSettings { LocationQuery = "北京" });
    Assert(ReferenceEquals(weatherSnapshot, cachedWeather), "weather snapshot should use the ten-minute cache");
    Assert(weatherHandler.RequestCount == 2, "cached weather read must not call the APIs again");
    var fiveDayFrame = renderer.Render(themes.Single(theme => theme.Id == "weather-five-day"), SystemSnapshot.DesignSample with { Weather = weatherSnapshot });
    Assert(fiveDayFrame.JpegBytes is [0xFF, 0xD8, ..], "five-day weather data view did not render");
}
Console.WriteLine("PASS Open-Meteo geocoding/current weather parser and cache");

var automaticWeatherResponses = new Queue<string>(new[]
{
    """{"current":{"temperature_2m":27.2,"apparent_temperature":28.1,"relative_humidity_2m":64,"weather_code":1,"is_day":1},"daily":{"time":["2026-07-29"],"weather_code":[1],"temperature_2m_max":[30],"temperature_2m_min":[24]}}"""
});
var automaticWeatherHandler = new SequenceHandler(automaticWeatherResponses);
using (var automaticWeatherClient = new HttpClient(automaticWeatherHandler))
using (var automaticWeatherSource = new OpenMeteoWeatherSnapshotSource(automaticWeatherClient))
{
    var automaticWeather = await automaticWeatherSource.ReadAsync(new WeatherSettings
    {
        LocationQuery = "上海",
        UseAutomaticLocation = true,
        Latitude = 22.5431,
        Longitude = 114.0579,
        AutomaticLocationName = "当前位置"
    });
    Assert(automaticWeather.Available, "automatic-location weather snapshot must be available");
    Assert(automaticWeather.LocationName == "当前位置", "automatic-location display name was not retained");
    Assert(automaticWeatherHandler.RequestCount == 1, "automatic coordinates must bypass city geocoding");
}
Console.WriteLine("PASS automatic weather coordinates and geocoding bypass");

var reverseGeocodeResponses = new Queue<string>(new[]
{
    """{"city":"深圳市","locality":"福田区","principalSubdivision":"广东省"}"""
});
var reverseGeocodeHandler = new SequenceHandler(reverseGeocodeResponses);
using (var reverseGeocodeClient = new HttpClient(reverseGeocodeHandler))
using (var reverseGeocoder = new BigDataCloudReverseGeocoder(reverseGeocodeClient))
{
    string? city = await reverseGeocoder.ResolveCityAsync(22.5431, 114.0579);
    Assert(city == "深圳市", "reverse geocoder did not prefer the city name");
    Assert(reverseGeocodeHandler.RequestCount == 1, "reverse geocoder should issue one request");
}
Console.WriteLine("PASS automatic-location city reverse geocoding");
var stockResponses = new Queue<string>(new[]
{
    """{"chart":{"result":[{"meta":{"symbol":"AAPL","regularMarketPrice":105.0,"chartPreviousClose":100.0,"regularMarketTime":1785200000}}],"error":null}}"""
});
var stockHandler = new SequenceHandler(stockResponses);
using (var stockClient = new HttpClient(stockHandler))
using (var stockSource = new YahooStockSnapshotSource(stockClient))
{
    var stockSettings = new StockSettings
    {
        RedForGain = false,
        Items = [new StockItemSettings { Symbol = "aapl", Alias = "苹果" }]
    };
    var stockSnapshot = await stockSource.ReadAsync(stockSettings);
    Assert(stockSnapshot.Quotes.Count == 1, "stock quote was not parsed");
    Assert(stockSnapshot.Quotes[0].Symbol == "AAPL", "stock symbol must be normalized");
    Assert(stockSnapshot.Quotes[0].DisplayName == "苹果", "stock alias was not applied");
    Assert(Math.Abs(stockSnapshot.Quotes[0].ChangePercent - 5.0) < 0.001, "stock change percent is incorrect");
    Assert(!stockSnapshot.RedForGain, "stock color preference was not preserved");
    var cachedStocks = await stockSource.ReadAsync(stockSettings);
    Assert(ReferenceEquals(stockSnapshot, cachedStocks), "stock snapshot should use the fifteen-minute cache");
    Assert(stockHandler.RequestCount == 1, "cached stock read must not call the API again");
    var stockFrame = renderer.Render(themes.Single(theme => theme.Id == "stocks"), SystemSnapshot.DesignSample with { Stocks = stockSnapshot });
    Assert(stockFrame.JpegBytes is [0xFF, 0xD8, ..], "stock data view did not render");
}
Console.WriteLine("PASS Yahoo chart parser, aliases, color preference and cache");

var customLayoutOptions = new ScreenDisplayOptions(ImageTimePlacement.Top);
var imageTimeFrame = renderer.Render(themes.Single(theme => theme.Id == "image"), SystemSnapshot.DesignSample, displayOptions: customLayoutOptions);
Assert(imageTimeFrame.JpegBytes is [0xFF, 0xD8, ..], "image time placement option did not render");
Console.WriteLine("PASS configurable image time placement render");

var maxQualityFrame = renderer.Render(themes[0], SystemSnapshot.DesignSample, jpegQuality: 100);
Assert(maxQualityFrame.JpegBytes.Length <= profile.MaxJpegBytes, "highest-quality JPEG exceeded device limit");
Console.WriteLine("PASS fixed highest JPEG quality stays within device limit");

var mimoSnapshot = XiaomiMiMoTokenPlanParser.Parse(
    """
    {"code":0,"data":{"planCode":"lite","planName":"Lite","currentPeriodEnd":"2026-07-28 23:59:59","expired":false}}
    """,
    """
    {"code":0,"data":{"usage":{"items":[{"name":"total_token","used":1944778516,"limit":4100000000,"percent":0.47},{"name":"compensation_total_token","used":0,"limit":0,"percent":0}]}}}
    """);
Assert(mimoSnapshot.PlatformName == "Xiaomi MiMo · Lite", "MiMo plan name was not parsed");
Assert(Math.Abs(mimoSnapshot.ClampedRemainingPercent - 52.5663776585) < 0.001, "MiMo remaining Credits percentage is incorrect");
Assert(mimoSnapshot.Balance?.Used == 1_944_778_516m, "MiMo used Credits were not parsed");
Assert(mimoSnapshot.Balance?.Limit == 4_100_000_000m, "MiMo Credits limit was not parsed");
Assert(mimoSnapshot.ResetsAt is not null, "MiMo period end was not parsed");
Console.WriteLine("PASS Xiaomi MiMo Token Plan detail/usage parser");

var aiPreviewArgumentIndex = Array.IndexOf(args, "--ai-preview");
if (aiPreviewArgumentIndex >= 0)
{
    Assert(aiPreviewArgumentIndex + 1 < args.Length, "--ai-preview requires an output path");
    var previewPath = Path.GetFullPath(args[aiPreviewArgumentIndex + 1]);
    Directory.CreateDirectory(Path.GetDirectoryName(previewPath)!);
    await File.WriteAllBytesAsync(previewPath, subscriptionFrame.JpegBytes);
    Console.WriteLine($"PASS wrote AI quota preview to {previewPath}");
}

var handler = new RecordingHandler();
using var client = new HttpClient(handler);
using var transport = new HttpImageDeviceTransport(client);
var testFrame = renderer.Render(themes[0], SystemSnapshot.DesignSample);
var pushResult = await transport.PushAsync(new Uri("http://device.test/image/upload"), testFrame);
Assert(pushResult.Success, "transport should report success");
Assert(handler.LastMethod == HttpMethod.Post, "transport must use POST");
Assert(handler.LastContentType == "image/jpeg", "transport must send image/jpeg");
Assert(handler.LastBody?.SequenceEqual(testFrame.JpegBytes) == true, "transport body must contain exact JPEG bytes");
Console.WriteLine("PASS transport POST image/jpeg with exact frame bytes");

var settingsPath = Path.Combine(Path.GetTempPath(), $"keyboard-screen-settings-{Guid.NewGuid():N}.json");
try
{
    var settingsStore = new JsonSettingsStore(settingsPath);
    var settings = new AppSettings { AppearanceMode = AppearanceMode.Dark, SelectedThemeId = "music", RefreshSeconds = 17, AccentColor = "#A23BFF", SelectedFontId = "file:test.ttf|test", SafeArea = new ScreenInsets(11, 53, 9, 13), AiQuota = new AiQuotaSettings { DisplayName = "MiMo Pro" }, Weather = new WeatherSettings { LocationQuery = "上海", UseAutomaticLocation = true }, Stocks = new StockSettings { RedForGain = false, Items = [new StockItemSettings { Symbol = "0700.HK", Alias = "腾讯" }] }, Music = new MusicSettings { EnableOnlineLyrics = true, LyricOffsetSeconds = 1.5 }, ImageTimePlacement = ImageTimePlacement.Top, LaunchAtStartup = true, AutoMediaThemeSwitch = true, MediaPlayingThemeId = "music-vinyl", MediaIdleThemeId = "clock-neon" , HasCompletedOnboarding = true, HasAcknowledgedStockNotice = true, HasAcknowledgedMiMoNotice = true };
    await settingsStore.SaveAsync(settings);
    var loadedSettings = await settingsStore.LoadAsync();
    Assert(loadedSettings.SelectedThemeId == "music", "settings theme did not persist");
    Assert(loadedSettings.AppearanceMode == AppearanceMode.Dark, "appearance mode did not persist");
    Assert(loadedSettings.RefreshSeconds == 17, "settings refresh interval did not persist");
    Assert(loadedSettings.AccentColor == "#A23BFF", "settings accent color did not persist");
    Assert(loadedSettings.SelectedFontId == "file:test.ttf|test", "settings font did not persist");
    Assert(loadedSettings.SafeArea == settings.SafeArea, "settings safe area did not persist");
    Assert(loadedSettings.AiQuota.DisplayName == "MiMo Pro", "AI display name did not persist");
    Assert(loadedSettings.Weather.LocationQuery == "上海" && loadedSettings.Weather.UseAutomaticLocation, "weather location settings did not persist");
    Assert(loadedSettings.LaunchAtStartup, "launch-at-startup setting did not persist");
    Assert(loadedSettings.HasCompletedOnboarding, "onboarding completion did not persist");
    Assert(loadedSettings.HasAcknowledgedStockNotice && loadedSettings.HasAcknowledgedMiMoNotice,
        "feature notice acknowledgements did not persist");
    Assert(!loadedSettings.Stocks.RedForGain && loadedSettings.Stocks.Items[0].Alias == "腾讯", "stock settings did not persist");
    Assert(loadedSettings.ImageTimePlacement == ImageTimePlacement.Top, "image time placement did not persist");
    Assert(loadedSettings.AutoMediaThemeSwitch, "media theme automation flag did not persist");
    Assert(loadedSettings.MediaPlayingThemeId == "music-vinyl", "playing theme did not persist");
    Assert(loadedSettings.Music.EnableOnlineLyrics && loadedSettings.Music.LyricOffsetSeconds == 1.5, "music settings did not persist");
    Assert(loadedSettings.MediaIdleThemeId == "clock-neon", "idle theme did not persist");
    Assert(loadedSettings.SettingsVersion == AppSettings.CurrentSettingsVersion, "settings schema version did not persist");
    string settingsDirectory = Path.GetDirectoryName(settingsPath)!;
    string settingsFileName = Path.GetFileName(settingsPath);
    Assert(Directory.GetFiles(settingsDirectory, $"{settingsFileName}.*.tmp").Length == 0,
        "atomic settings save left a temporary file behind");

    await File.WriteAllTextAsync(settingsPath, "{ invalid json");
    var recoveredSettings = await settingsStore.LoadAsync();
    Assert(recoveredSettings.SettingsVersion == AppSettings.CurrentSettingsVersion
           && recoveredSettings.SelectedThemeId == "clock-dot-matrix",
        "invalid settings must recover to current defaults");
    Assert(Directory.GetFiles(settingsDirectory, $"{settingsFileName}.invalid-*.json").Length == 1,
        "invalid settings file must be preserved for diagnostics");
    Console.WriteLine("PASS versioned atomic settings save, round-trip and invalid-file recovery");
}
finally
{
    if (File.Exists(settingsPath)) File.Delete(settingsPath);
    string? settingsDirectory = Path.GetDirectoryName(settingsPath);
    if (settingsDirectory is not null)
    {
        foreach (string invalidBackup in Directory.GetFiles(
                     settingsDirectory,
                     $"{Path.GetFileName(settingsPath)}.invalid-*.json"))
        {
            File.Delete(invalidBackup);
        }
    }
}

var mediaAutomation = new AppSettings
{
    AutoMediaThemeSwitch = true,
    MediaPlayingThemeId = "music-minimal",
    MediaIdleThemeId = "dashboard"
};
Assert(MediaThemeAutomation.IsMusicThemeId("music-poster"), "music poster must be classified as a music theme");
Assert(MediaThemeAutomation.IsMusicThemeId("music-vinyl"), "dynamic vinyl must be classified as a music theme");
Assert(MediaThemeAutomation.IsMusicThemeId("music-cassette"), "dynamic cassette must be classified as a music theme");
Assert(!MediaThemeAutomation.IsMusicThemeId("dashboard"), "dashboard must not be classified as a music theme");
Assert(MediaThemeAutomation.ResolveThemeId(mediaAutomation, true, "clock") == "music-minimal", "playing media theme resolution failed");
Assert(MediaThemeAutomation.ResolveThemeId(mediaAutomation, false, "clock") == "dashboard", "idle media theme resolution failed");
mediaAutomation.MediaPlayingThemeId = "clock";
mediaAutomation.MediaIdleThemeId = "music";
Assert(MediaThemeAutomation.ResolveThemeId(mediaAutomation, true, "clock") == "music", "invalid playing theme must fall back to music");
Assert(MediaThemeAutomation.ResolveThemeId(mediaAutomation, false, "clock") == "system", "invalid idle theme must fall back to system");
Console.WriteLine("PASS bidirectional media theme automation");
var fontTestFolder = Path.Combine(Path.GetTempPath(), $"keyboard-screen-fonts-{Guid.NewGuid():N}");
Directory.CreateDirectory(fontTestFolder);
try
{
    using var emptyCatalog = new FontFolderCatalog(fontTestFolder);
    var initialFonts = emptyCatalog.Scan();
    Assert(initialFonts.Count == 1 && initialFonts[0].IsBuiltIn, "empty font folder must expose the built-in fallback");

    if (OperatingSystem.IsWindows())
    {
        var systemFont = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts", "segoeui.ttf");
        if (File.Exists(systemFont))
        {
            var fontChangeDetected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            emptyCatalog.FontsChanged += (_, _) => fontChangeDetected.TrySetResult();
            File.Copy(systemFont, Path.Combine(fontTestFolder, "test-font.ttf"));
            var watcherResult = await Task.WhenAny(fontChangeDetected.Task, Task.Delay(3000));
            Assert(watcherResult == fontChangeDetected.Task, "font folder watcher did not report the new font");
            var discoveredFonts = emptyCatalog.Scan();
            Assert(discoveredFonts.Count >= 2, "TTF file was not discovered");
            var customFont = discoveredFonts.First(font => !font.IsBuiltIn);
            var customFontFrame = renderer.Render(themes[0], SystemSnapshot.DesignSample, fontFamily: customFont.FontFamily);
            Assert(customFontFrame.JpegBytes is [0xFF, 0xD8, ..], "custom font preview did not render");
            var customFontNetworkFrame = renderer.Render(themes.Single(theme => theme.Id == "network"), SystemSnapshot.DesignSample, fontFamily: customFont.FontFamily);
            Assert(customFontNetworkFrame.JpegBytes is [0xFF, 0xD8, ..], "ink-centered network summary did not render with a custom font");
            Console.WriteLine($"PASS font folder watch, scan and ink-centered network render {customFont.DisplayName}");
        }
    }
}
finally
{
    Directory.Delete(fontTestFolder, true);
}

var endpointArgumentIndex = Array.IndexOf(args, "--endpoint");
if (endpointArgumentIndex >= 0)
{
    Assert(endpointArgumentIndex + 1 < args.Length, "--endpoint requires a URL");
    Assert(Uri.TryCreate(args[endpointArgumentIndex + 1], UriKind.Absolute, out var parsedEndpoint), "--endpoint must be an absolute URL");
    var endpoint = parsedEndpoint!;
    using var liveTransport = new HttpImageDeviceTransport();
    var liveFrame = renderer.Render(themes[2], SystemSnapshot.DesignSample);
    var liveResult = await liveTransport.PushAsync(endpoint, liveFrame);
    Assert(liveResult.Success, $"live device push failed: {liveResult.Message}");
    Console.WriteLine($"PASS live device HTTP {liveResult.StatusCode} in {liveResult.Elapsed.TotalMilliseconds:0} ms");
}

if (OperatingSystem.IsWindows())
{
    var source = new WindowsSystemSnapshotSource();
    _ = await source.ReadAsync();
    await Task.Delay(120);
    var snapshot = await source.ReadAsync();
    Assert(snapshot.CpuPercent is >= 0 and <= 100, "CPU must be within 0..100");
    Assert(snapshot.MemoryPercent is >= 0 and <= 100, "memory must be within 0..100");
    Console.WriteLine($"PASS system source CPU={snapshot.CpuPercent:0.0}% MEM={snapshot.MemoryPercent:0.0}%");
}

Console.WriteLine("All smoke tests passed.");
return;

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static byte FindStartOfFrame(byte[] jpeg)
{
    for (var index = 2; index + 3 < jpeg.Length;)
    {
        if (jpeg[index] != 0xFF)
        {
            index++;
            continue;
        }

        while (index < jpeg.Length && jpeg[index] == 0xFF)
        {
            index++;
        }

        if (index >= jpeg.Length)
        {
            break;
        }

        var marker = jpeg[index++];
        if (marker is >= 0xC0 and <= 0xCF and not 0xC4 and not 0xC8 and not 0xCC)
        {
            return marker;
        }

        if (marker is 0xD8 or 0xD9 || index + 1 >= jpeg.Length)
        {
            continue;
        }

        var length = (jpeg[index] << 8) | jpeg[index + 1];
        if (length < 2)
        {
            break;
        }

        index += length;
    }

    return 0;
}

sealed class SequenceHandler : HttpMessageHandler
{
    private readonly Queue<string> _responses;
    public int RequestCount { get; private set; }

    public SequenceHandler(Queue<string> responses)
    {
        _responses = responses;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestCount++;
        if (_responses.Count == 0)
        {
            throw new InvalidOperationException("No mocked weather response remains.");
        }
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(_responses.Dequeue())
        });
    }
}

sealed class RecordingHandler : HttpMessageHandler
{
    public HttpMethod? LastMethod { get; private set; }
    public string? LastContentType { get; private set; }
    public byte[]? LastBody { get; private set; }
    public string ResponseBody { get; init; } = string.Empty;
    public string? LastAuthorization { get; private set; }
    public Uri? LastUri { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastMethod = request.Method;
        LastContentType = request.Content?.Headers.ContentType?.MediaType;
        LastBody = request.Content is null ? null : await request.Content.ReadAsByteArrayAsync(cancellationToken);
        LastAuthorization = request.Headers.Authorization?.ToString();
        LastUri = request.RequestUri;
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(ResponseBody)
        };
    }
}

sealed class StubSystemSnapshotSource : ISystemSnapshotSource
{
    public ValueTask<SystemSnapshot> ReadAsync(CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(SystemSnapshot.DesignSample with
        {
            Music = null,
            AiQuota = null,
            Weather = null,
            Stocks = null
        });
}

sealed class StubLyricsSnapshotSource : ILyricsSnapshotSource
{
    public int ReadCount { get; private set; }

    public Task<LyricsSnapshot> ReadAsync(MusicSnapshot music, CancellationToken cancellationToken = default)
    {
        ReadCount++;
        return Task.FromResult(new LyricsSnapshot(true,
        [
            new LyricLine(TimeSpan.Zero, "Test lyric")
        ]));
    }
}

sealed class StubWeatherSnapshotSource : IWeatherSnapshotSource
{
    public int ReadCount { get; private set; }

    public Task<WeatherSnapshot> ReadAsync(WeatherSettings settings, CancellationToken cancellationToken = default)
    {
        ReadCount++;
        return Task.FromResult(SystemSnapshot.DesignSample.Weather!);
    }
}

sealed class StubStockSnapshotSource : IStockSnapshotSource
{
    public int ReadCount { get; private set; }

    public Task<StockSnapshot> ReadAsync(StockSettings settings, CancellationToken cancellationToken = default)
    {
        ReadCount++;
        return Task.FromResult(StockSnapshot.Empty);
    }
}

sealed class StubMusicSnapshotSource(MusicSnapshot snapshot) : IMusicSnapshotSource
{
    public MusicSnapshot Snapshot { get; set; } = snapshot;

    public ValueTask<MusicSnapshot> ReadAsync(CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(Snapshot);
}

sealed class StubWeatherSettingsResolver(WeatherSettingsResolution resolution) : IWeatherSettingsResolver
{
    public int ReadCount { get; private set; }

    public Task<WeatherSettingsResolution> ResolveAsync(
        WeatherSettings settings,
        CancellationToken cancellationToken = default)
    {
        ReadCount++;
        return Task.FromResult(resolution);
    }
}

sealed class StubAutomaticWeatherLocationProvider(
    AutomaticWeatherLocation? location) : IAutomaticWeatherLocationProvider
{
    public Task<AutomaticWeatherLocation?> TryGetAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(location);
}

sealed class StubDeviceTransport : IDeviceTransport
{
    public int PushCount { get; private set; }

    public Uri? LastEndpoint { get; private set; }

    public Task<DevicePushResult> PushAsync(
        Uri endpoint,
        RenderedFrame frame,
        CancellationToken cancellationToken = default)
    {
        PushCount++;
        LastEndpoint = endpoint;
        return Task.FromResult(new DevicePushResult(true, 200, "OK", TimeSpan.Zero));
    }
}
