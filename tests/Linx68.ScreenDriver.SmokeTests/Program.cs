using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using Linx68.ScreenDriver.Application;
using Linx68.ScreenDriver.Core;
using Linx68.ScreenDriver.Infrastructure;

var defaults = new AppSettings();
Assert(defaults.SelectedThemeId == "clock-weather", "first-run theme must default to clock-and-weather");
Assert(defaults.ScreenColorMode == ScreenColorMode.Night, "first-run screen color mode must preserve night appearance");
Assert(defaults.AccentColor == "#E4694C", "first-run accent color is incorrect");
Assert(defaults.AutoPush && defaults.RefreshSeconds == 1, "first-run automation defaults are incorrect");
Assert(defaults.MinimizeToTray && defaults.CloseToTray, "first-run tray defaults are incorrect");
Assert(defaults.Weather.UseAutomaticLocation, "first-run weather must use automatic location");
Assert(defaults.SafeArea == new ScreenInsets(10, 52, 10, 12), "first-run safe area is incorrect");
Assert(defaults.AppearanceMode == AppearanceMode.System, "first-run appearance must follow Windows");
Assert(defaults.AiQuota.SourceKind == AiQuotaSourceKind.OpenAICodex,
    "first-run AI source must default to Codex rate limits");
Assert(defaults.Music.EnableOnlineLyrics,
    "first-run music settings must enable online lyrics");

string codexTestRoot = Path.Combine(Path.GetTempPath(), "Linx68ScreenDriver", "codex-setup-" + Guid.NewGuid().ToString("N"));
try
{
    var codexSetup = new CodexSetupService(Path.Combine(codexTestRoot, ".codex"));
    Assert(CodexSetupService.ResolveCodexHome(@"C:\Users\Alice", null) == Path.Combine(@"C:\Users\Alice", ".codex"),
        "Codex setup must default to the user's .codex folder");

    string portableConfig = Path.Combine(codexTestRoot, "portable-config.toml");
    Directory.CreateDirectory(codexTestRoot);
    File.WriteAllText(portableConfig, "model = \"gpt-5.6\"\n");
    string firstBackup = codexSetup.ImportConfig(portableConfig);
    Assert(string.IsNullOrEmpty(firstBackup) && File.ReadAllText(codexSetup.ConfigPath).Contains("gpt-5.6"),
        "Codex setup must import only the selected config.toml");

    string exportedConfig = Path.Combine(codexTestRoot, "exported-config.toml");
    codexSetup.ExportConfig(exportedConfig);
    Assert(File.ReadAllText(exportedConfig) == File.ReadAllText(codexSetup.ConfigPath),
        "Codex setup must export the current config.toml without credentials");

    string backupPath = codexSetup.ImportConfig(portableConfig);
    Assert(File.Exists(backupPath), "Codex setup must back up an existing config before import");
    Console.WriteLine("PASS Codex setup keeps portable config separate from per-device credentials");
}
finally
{
    if (Directory.Exists(codexTestRoot)) Directory.Delete(codexTestRoot, recursive: true);
}

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
Assert(NetEaseWindowTitleParser.TryParse("Payphone - Maroon 5/Wiz Khalifa", out string windowTitle, out string windowArtist)
       && windowTitle == "Payphone" && windowArtist == "Maroon 5/Wiz Khalifa",
    "NetEase window title must provide fallback track metadata when no Windows media session exists");
Assert(!NetEaseWindowTitleParser.TryParse("网易云音乐", out _, out _),
    "NetEase application title must not be treated as a track");
var netEaseWindowClock = new NetEaseWindowPlaybackClock();
TimeSpan firstFallbackPosition = netEaseWindowClock.GetPosition("Demo Track", "Demo Artist");
await Task.Delay(30);
TimeSpan advancedFallbackPosition = netEaseWindowClock.GetPosition("Demo Track", "Demo Artist");
TimeSpan changedTrackPosition = netEaseWindowClock.GetPosition("Next Track", "Demo Artist");
Assert(advancedFallbackPosition > firstFallbackPosition && changedTrackPosition < advancedFallbackPosition,
    "NetEase fallback playback clock must advance monotonically and reset only when the track changes");
Console.WriteLine("PASS Windows media session ordering and NetEase identifiers");

if (args.Contains("--music-probe", StringComparer.OrdinalIgnoreCase))
{
    var liveSource = new WindowsMusicSnapshotSource();
    var initialMusic = await liveSource.ReadAsync();
    await Task.Delay(TimeSpan.FromSeconds(2));
    var liveMusic = await liveSource.ReadAsync();
    if (liveMusic.Available && WindowsMusicSessionSelector.IsNetEase(liveMusic.SourceAppId))
    {
        using var enricher = new NetEaseMusicSnapshotEnricher();
        liveMusic = await enricher.EnrichAsync(liveMusic);
        using var fallback = new LrcLibLyricsSnapshotSource();
        using var lyricsSource = new NetEaseLyricsSnapshotSource(fallback);
        liveMusic = liveMusic with { Lyrics = await lyricsSource.ReadAsync(liveMusic) };
    }
    Console.WriteLine(liveMusic is null || !liveMusic.Available
        ? "PROBE music session: unavailable"
        : $"PROBE music session: source={liveMusic.SourceAppId}; playing={liveMusic.IsPlaying}; title={liveMusic.Title}; artist={liveMusic.Artist}; position={liveMusic.Position:c}; duration={liveMusic.Duration:c}; position-advanced={liveMusic.Position > initialMusic.Position}; artwork={liveMusic.Artwork is { Length: > 0 }}; lyrics={liveMusic.Lyrics.Available}; lyric-lines={liveMusic.Lyrics.Lines.Count}");
}

var profile = ScreenProfile.KeyboardDisplay;
Assert(profile.SafeArea.Top == 52, "keyboard firmware safe area must reserve the top status pills");
Assert(profile.SafeArea.Left + profile.SafeArea.Right < profile.Width, "safe area horizontal insets are invalid");
Assert(profile.SafeArea.Top + profile.SafeArea.Bottom < profile.Height, "safe area vertical insets are invalid");
var renderer = new ScreenRenderer(profile);
var themeDefinitions = BuiltInThemes.CreateDefinitions(new ImageTheme());
var themes = themeDefinitions.Select(definition => definition.Theme).ToArray();
Assert(themes.Length == 10, "built-in theme catalog should contain the 10 confirmed schemes");
Assert(themes.All(theme => theme.Id is not "calendar" and not "ambient"), "removed calendar/ambient themes must not be registered");
Assert(themes.All(theme => theme.Id != "clock-seconds"), "removed seconds progress theme must not be registered");
Assert(themes.All(theme => theme.Id != "week"), "removed week calendar theme must not be registered");
Assert(themes.All(theme => theme.Id is not "system-minimal" and not "clock" and not "clock-neon" and not "clock-dot-matrix" and not "clock-weather-dot" and not "image"),
    "removed minimal, neon, dot-matrix and image themes must not be registered");
Assert(themes.Single(theme => theme.Id == "clock-weather").DisplayName == "时钟天气", "clock-and-weather theme must be registered");
Assert(themes.All(theme => theme.Id != "codex-info"), "removed combined Codex information theme must not be registered");
Assert(themes.Single(theme => theme.Id == "ai-quota").DisplayName == "Codex 额度", "Codex quota theme must use its final display label");
Assert(themes.Single(theme => theme.Id == "codex-tasks").DisplayName == "Codex 任务", "Codex task theme must be registered");
Assert(themes.Any(theme => theme.Id == "weather-five-day"), "five-day weather theme must be registered");
Assert(themes.All(theme => theme.Id != "stocks"), "removed stock theme must not be registered");
Assert(BuiltInThemes.NormalizeThemeId("stocks") == "clock-weather", "legacy stock theme must migrate to clock-and-weather");
Assert(BuiltInThemes.NormalizeThemeId("codex-info") == "ai-quota", "legacy combined Codex theme must migrate to Codex quota");
Assert(themes.Where(theme => theme.Id.StartsWith("music", StringComparison.OrdinalIgnoreCase)).Select(theme => theme.Id)
           .OrderBy(id => id)
           .SequenceEqual(["music"])
       && themes.All(theme => theme.Id is not "music-vinyl" and not "music-cassette" and not "music-minimal" and not "music-poster"),
    "the music catalog must contain only the confirmed cover-and-lyrics presentation");
Assert(themes.Select(theme => theme.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() == themes.Length, "theme ids should be unique");
Assert(themeDefinitions.All(definition => definition.Category != ThemeCategory.Other), "every built-in theme must declare a category");
Assert(themeDefinitions.Single(definition => definition.Id == "weather-five-day").Requires(ThemeDataRequirements.Weather)
       && themeDefinitions.Single(definition => definition.Id == "clock-weather").Requires(ThemeDataRequirements.Weather),
    "weather presentations must declare their data requirement");
Assert(themeDefinitions.Single(definition => definition.Id == "weather-five-day").Category == ThemeCategory.Time
       && themeDefinitions.Where(definition => definition.Category == ThemeCategory.Information)
           .Select(definition => definition.Id)
           .OrderBy(id => id)
           .SequenceEqual(["ai-quota", "codex-tasks"])
       && themeDefinitions.All(definition => definition.Category != ThemeCategory.Matrix),
    "five-day weather must belong to time-and-weather while confirmed Codex presentations share the information category");
Assert(themeDefinitions.Single(definition => definition.Id == "music").Requires(ThemeDataRequirements.Lyrics), "music theme must declare its optional lyrics requirement");
Assert(themeDefinitions.Single(definition => definition.Id == "ai-quota").Requires(ThemeDataRequirements.CodexTasks)
       && themeDefinitions.Single(definition => definition.Id == "codex-tasks").Requires(ThemeDataRequirements.CodexTasks),
    "Codex task presentations must declare their task-data requirement");
Assert(themes.Single(theme => theme.Id == "music").Description == "封面与连续同步歌词",
    "the single music card must use a compact, non-wrapping description");
Assert(themeDefinitions.Where(definition => definition.Category == ThemeCategory.Music)
           .All(definition => definition.Requires(ThemeDataRequirements.Music | ThemeDataRequirements.Lyrics)),
    "all music presentation variants must request media and lyrics data");
Console.WriteLine("PASS built-in theme metadata, requirements and settings sections");

var pipelineSystem = new StubSystemSnapshotSource();
var pipelineLyrics = new StubLyricsSnapshotSource();
var pipelineWeather = new StubWeatherSnapshotSource();
var pipelineEnricher = new StubMusicSnapshotEnricher();
var snapshotBuilder = new DashboardSnapshotBuilder(
    pipelineSystem,
    pipelineLyrics,
    pipelineWeather,
    pipelineEnricher);
var pipelineSettings = new AppSettings();
var dashboardSnapshot = await snapshotBuilder.BuildAsync(
    themeDefinitions.Single(definition => definition.Id == "dashboard"),
    pipelineSettings,
    SystemSnapshot.DesignSample.Music!,
    effectiveWeatherSettings: null,
    aiQuota: null);
Assert(dashboardSnapshot.Music is not null, "snapshot pipeline must preserve the supplied media snapshot");
Assert(pipelineLyrics.ReadCount == 0 && pipelineEnricher.ReadCount == 0 && pipelineWeather.ReadCount == 0,
    "dashboard theme must not invoke optional data sources");
pipelineSettings.Music.EnableOnlineLyrics = false;
var musicArtworkSnapshot = await snapshotBuilder.BuildAsync(
    themeDefinitions.Single(definition => definition.Id == "music"),
    pipelineSettings,
    SystemSnapshot.DesignSample.Music!,
    effectiveWeatherSettings: null,
    aiQuota: null);
Assert(pipelineEnricher.ReadCount == 1 && musicArtworkSnapshot.Music?.Artwork is { Length: > 0 }
       && pipelineLyrics.ReadCount == 0,
    "music theme must enrich album artwork even when lyrics are disabled");
pipelineSettings.Music.EnableOnlineLyrics = true;
var musicLyricsSnapshot = await snapshotBuilder.BuildAsync(
    themeDefinitions.Single(definition => definition.Id == "music"),
    pipelineSettings,
    SystemSnapshot.DesignSample.Music!,
    effectiveWeatherSettings: null,
    aiQuota: null);
Assert(pipelineEnricher.ReadCount == 2 && pipelineLyrics.ReadCount == 1 && musicLyricsSnapshot.Music?.Lyrics.Available == true,
    "lyrics-capable theme must invoke the lyrics source when enabled");
_ = await snapshotBuilder.BuildAsync(
    themeDefinitions.Single(definition => definition.Id == "weather-five-day"),
    pipelineSettings,
    SystemSnapshot.DesignSample.Music!,
    new WeatherSettings { LocationQuery = "北京" },
    aiQuota: null);
Assert(pipelineWeather.ReadCount == 1,
    "weather theme must invoke the weather source");
Console.WriteLine("PASS metadata-driven dashboard snapshot pipeline");

var refreshMusic = new StubMusicSnapshotSource(SystemSnapshot.DesignSample.Music! with
{
    IsPlaying = true
});
var refreshLyrics = new StubLyricsSnapshotSource();
var refreshWeather = new StubWeatherSnapshotSource();
var refreshSnapshotBuilder = new DashboardSnapshotBuilder(
    new StubSystemSnapshotSource(),
    refreshLyrics,
    refreshWeather);
var refreshWeatherResolver = new StubWeatherSettingsResolver(
    new WeatherSettingsResolution(new WeatherSettings { LocationQuery = "北京" }, true));
var refreshService = new DashboardRefreshService(
    refreshMusic,
    refreshSnapshotBuilder,
    refreshWeatherResolver);
var refreshSettings = new AppSettings
{
    AutoSwitchToMusic = true
};
refreshSettings.Music.EnableOnlineLyrics = true;
var refreshResult = await refreshService.RefreshAsync(new DashboardRefreshRequest(
    themeDefinitions,
    refreshSettings,
    "dashboard",
    "dashboard",
    _ => throw new InvalidOperationException("AI source must not be read for a music theme.")));
Assert(refreshResult.EffectiveTheme.Id == "music" && refreshResult.EffectiveThemeChanged,
    "refresh service must switch to the music theme while media is playing");
Assert(refreshLyrics.ReadCount == 1 && refreshWeatherResolver.ReadCount == 0,
    "refresh service must request only the metadata-required sources");

refreshResult = await refreshService.RefreshAsync(new DashboardRefreshRequest(
    themeDefinitions,
    refreshSettings,
    "music-lyric-focus",
    refreshResult.EffectiveTheme.Id));
Assert(refreshResult.EffectiveTheme.Id == "music",
    "a saved legacy music presentation must migrate to the confirmed cover-and-lyrics theme");

refreshSettings.AutoSwitchToMusic = false;
int aiReadCount = 0;
int codexTaskReadCount = 0;
var refreshTasks = new CodexTaskSnapshot(
    true,
    [new CodexTaskItem("修复小屏任务卡", CodexTaskStatus.Active, DateTimeOffset.UtcNow)],
    DateTimeOffset.UtcNow);
refreshResult = await refreshService.RefreshAsync(new DashboardRefreshRequest(
    themeDefinitions,
    refreshSettings,
    "ai-quota",
    refreshResult.EffectiveTheme.Id,
    _ =>
    {
        aiReadCount++;
        return Task.FromResult<AiQuotaSnapshot?>(AiQuotaSnapshot.ForSubscription("Test", 50));
    },
    _ =>
    {
        codexTaskReadCount++;
        return Task.FromResult<CodexTaskSnapshot?>(refreshTasks);
    }));
Assert(refreshResult.EffectiveTheme.Id == "ai-quota" && aiReadCount == 1 && codexTaskReadCount == 1
       && refreshResult.Snapshot.CodexTasks == refreshTasks,
    "quota theme must request AI and Codex task data only when required");

refreshResult = await refreshService.RefreshAsync(new DashboardRefreshRequest(
    themeDefinitions,
    refreshSettings,
    "codex-tasks",
    refreshResult.EffectiveTheme.Id,
    _ => throw new InvalidOperationException("AI source must not be read for the tasks-only theme."),
    _ => Task.FromResult<CodexTaskSnapshot?>(refreshTasks)));
Assert(refreshResult.EffectiveTheme.Id == "codex-tasks" && refreshResult.Snapshot.AiQuota is null
       && refreshResult.Snapshot.CodexTasks == refreshTasks,
    "tasks-only theme must request Codex task data without requesting quota data");

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
using var codexRateLimitDocument = JsonDocument.Parse(
    """
    {
      "rateLimitsByLimitId": {
        "codex": {
          "limitId": "codex",
          "primary": {
            "usedPercent": 25,
            "windowDurationMins": 10080,
            "resetsAt": 1781654400
          }
        }
      }
    }
    """);
var codexQuota = CodexQuotaSnapshotSource.ParseRateLimits(codexRateLimitDocument.RootElement);
Assert(codexQuota.PlatformName == "Codex"
       && Math.Abs(codexQuota.ClampedRemainingPercent - 75) < 0.001
       && codexQuota.ResetPeriod == AiResetPeriod.Weekly
       && codexQuota.ResetsAt == DateTimeOffset.FromUnixTimeSeconds(1781654400),
    "Codex rate-limit response must produce the remaining percentage and reset window");
Console.WriteLine("PASS Codex rate-limit quota parsing");

using var codexTaskDocument = JsonDocument.Parse(
    """
    {
      "data": [
        {
          "id": "task-approval",
          "name": "等待确认的设备帧验收",
          "preview": "this prompt must not reach the device screen",
          "updatedAt": 1781654000,
          "status": { "type": "active", "activeFlags": ["waitingOnApproval"] }
        },
        {
          "id": "task-active",
          "name": "进行中的歌词优化",
          "updatedAt": 1781654100,
          "status": { "type": "active" }
        },
        {
          "id": "task-recent",
          "name": "最近更新的额度主题",
          "updatedAt": 1781654200,
          "status": { "type": "idle" }
        },
        {
          "id": "task-unnamed",
          "preview": "never use this preview as a task title",
          "updatedAt": 1781654300,
          "status": { "type": "idle" }
        }
      ]
    }
    """);
var parsedTasks = CodexTaskSnapshotSource.ParseThreadList(
    codexTaskDocument.RootElement,
    DateTimeOffset.FromUnixTimeSeconds(1781654400));
IReadOnlyList<CodexTaskItem> displayTasks = parsedTasks.GetDisplayTasks(4);
Assert(parsedTasks.Available && parsedTasks.Tasks.Count == 4
       && parsedTasks.Tasks.All(task => !task.Title.Contains("preview", StringComparison.OrdinalIgnoreCase))
       && displayTasks[0].Status == CodexTaskStatus.WaitingForApproval
       && displayTasks[0].Title == "等待确认的设备帧验收"
       && displayTasks.Count == 2
       && displayTasks[1].Status == CodexTaskStatus.Active
       && displayTasks.All(task => task.Status != CodexTaskStatus.Recent),
    "Codex task parser must retain only safe active or confirmed-completed tasks");
using var activeTurnDocument = JsonDocument.Parse("""{ "data": [{ "status": "inProgress" }] }""");
using var completedTurnDocument = JsonDocument.Parse("""{ "data": [{ "status": "completed", "completedAt": 1786176900 }] }""");
var codexTaskStartInfo = CodexTaskSnapshotSource.CreateStartInfo();
Assert(CodexTaskSnapshotSource.IsInProgressTurn(activeTurnDocument.RootElement)
       && CodexTaskSnapshotSource.GetLatestTurnStatus(completedTurnDocument.RootElement) == CodexTaskStatus.Completed
       && CodexTaskSnapshotSource.GetLatestTurnCompletedAt(completedTurnDocument.RootElement) == DateTimeOffset.FromUnixTimeSeconds(1786176900)
       && CodexTaskSnapshotSource.ReadOnlyRequestMethods.SequenceEqual(
           ["initialize", "initialized", "thread/list", "thread/turns/list"])
       && codexTaskStartInfo.StandardInputEncoding?.GetPreamble().Length == 0
       && codexTaskStartInfo.StandardOutputEncoding == System.Text.Encoding.UTF8
       && codexTaskStartInfo.StandardErrorEncoding == System.Text.Encoding.UTF8,
    "Codex task source must recognize a real active turn through the approved read-only method");
string directPlanInput = JsonSerializer.Serialize(new
{
    plan = new[]
    {
        new { step = "读取计划", status = "completed" },
        new { step = "渲染进度", status = "completed" },
        new { step = "验证界面", status = "in_progress" }
    }
});
string directPlanLine = JsonSerializer.Serialize(new
{
    type = "response_item",
    payload = new { type = "custom_tool_call", name = "update_plan", input = directPlanInput }
});
string nestedPlanLine = JsonSerializer.Serialize(new
{
    type = "response_item",
    payload = new
    {
        type = "custom_tool_call",
        name = "exec",
        input = "const result = await tools.update_plan({plan:[{step:\"读取计划\",status:\"completed\"},{step:\"渲染进度\",status:\"completed\"},{step:\"验证界面\",status:\"in_progress\"}]}); text(result);"
    }
});
Assert(CodexTaskSnapshotSource.TryParsePlanProgressFromRolloutLine(directPlanLine, out int directCompleted, out int directTotal)
       && directCompleted == 2 && directTotal == 3
       && CodexTaskSnapshotSource.TryParsePlanProgressFromRolloutLine(nestedPlanLine, out int nestedCompleted, out int nestedTotal)
       && nestedCompleted == 2 && nestedTotal == 3
       && !CodexTaskSnapshotSource.TryParsePlanProgressFromRolloutLine("not json", out _, out _),
    "Codex task source must count only plan statuses from direct and nested update-plan calls");
DateTimeOffset renderedCompletedAt = new(2026, 8, 8, 16, 15, 0, TimeSpan.FromHours(8));
DateTimeOffset renderedUpdatedAt = renderedCompletedAt.AddMinutes(-3);
Assert(CodexTaskCardRenderer.GetDetail(
           new CodexTaskItem("有计划", CodexTaskStatus.Active, renderedUpdatedAt, 2, 3),
           CodexTaskStatus.Active) == "2/3"
       && CodexTaskCardRenderer.GetDetail(
           new CodexTaskItem("无计划", CodexTaskStatus.Completed, renderedUpdatedAt, CompletedAt: renderedCompletedAt),
           CodexTaskStatus.Completed) == renderedCompletedAt.ToLocalTime().ToString("HH:mm")
       && CodexTaskCardRenderer.GetDetail(
           new CodexTaskItem("运行中无计划", CodexTaskStatus.Active, renderedUpdatedAt),
           CodexTaskStatus.Active) == renderedUpdatedAt.ToLocalTime().ToString("HH:mm"),
    "task detail must prefer real plan progress, then completion time, then active update time");
Assert(CodexTaskSnapshotSource.TryParseProtocolMessage(
           "diagnostic prefix {\"id\":2,\"result\":{\"data\":[]}}thread/status noise",
           out JsonElement noisyProtocolMessage)
       && noisyProtocolMessage.GetProperty("id").GetInt32() == 2,
    "Codex task source must tolerate non-JSON text around one complete protocol message");
Assert(CodexTaskSnapshotSource.TryParseProtocolResponse(
           "{\"method\":\"thread/status/changed\",\"params\":{}}{\"id\":2,\"result\":{\"data\":[]}}",
           2,
           out JsonElement concatenatedProtocolResponse)
       && concatenatedProtocolResponse.GetProperty("id").GetInt32() == 2,
    "Codex task source must retain a response concatenated after a notification on the same line");
string localRolloutPath = Path.Combine(Path.GetTempPath(), $"linx68-codex-rollout-{Guid.NewGuid():N}.jsonl");
DateTimeOffset localActivityObservedAt = DateTimeOffset.UtcNow;
try
{
    File.WriteAllText(localRolloutPath,
        "{\"type\":\"event_msg\",\"payload\":{\"type\":\"task_started\"}}\n");
    File.SetLastWriteTimeUtc(localRolloutPath, localActivityObservedAt.UtcDateTime);
    Assert(CodexTaskSnapshotSource.IsLocallyActiveRollout(localRolloutPath, localActivityObservedAt),
        "a recently written rollout without a completion marker must represent cross-process active work");

    File.WriteAllText(localRolloutPath,
        "{\"type\":\"response_item\",\"payload\":{\"type\":\"custom_tool_call_output\"}}\n");
    File.SetLastWriteTimeUtc(localRolloutPath, localActivityObservedAt.UtcDateTime);
    Assert(CodexTaskSnapshotSource.IsLocallyActiveRollout(localRolloutPath, localActivityObservedAt),
        "continued recent rollout writes must keep a long-running turn active after its start marker leaves the bounded tail");

    File.AppendAllText(localRolloutPath,
        "{\"timestamp\":\"2026-08-08T08:15:00Z\",\"type\":\"event_msg\",\"payload\":{\"type\":\"task_complete\"}}\n");
    File.SetLastWriteTimeUtc(localRolloutPath, localActivityObservedAt.UtcDateTime);
    Assert(!CodexTaskSnapshotSource.IsLocallyActiveRollout(localRolloutPath, localActivityObservedAt)
           && CodexTaskSnapshotSource.GetLocalRolloutStatus(localRolloutPath, localActivityObservedAt) == CodexTaskStatus.Completed
           && CodexTaskSnapshotSource.GetLocalRolloutCompletedAt(localRolloutPath, localActivityObservedAt)
               == new DateTimeOffset(2026, 8, 8, 8, 15, 0, TimeSpan.Zero),
        "a rollout completion marker must immediately produce a confirmed-completed state");

    File.WriteAllText(localRolloutPath,
        "{\"type\":\"event_msg\",\"payload\":{\"type\":\"task_started\"}}\n");
    File.SetLastWriteTimeUtc(localRolloutPath, localActivityObservedAt.AddMinutes(-3).UtcDateTime);
    Assert(!CodexTaskSnapshotSource.IsLocallyActiveRollout(localRolloutPath, localActivityObservedAt),
        "an abandoned rollout must not remain active after its local activity window expires");
}
finally
{
    File.Delete(localRolloutPath);
}
Console.WriteLine("PASS Codex task parsing, prioritization and read-only request boundary");

if (args.Contains("--codex-rate-probe", StringComparer.OrdinalIgnoreCase))
{
    var liveCodexQuota = await new CodexQuotaSnapshotSource().ReadAsync();
    Assert(liveCodexQuota.Available && liveCodexQuota.PlatformName == "Codex",
        "live Codex App Server rate-limit read must return a Codex quota snapshot");
    Console.WriteLine("PASS live Codex rate-limit read");
}

if (args.Contains("--codex-task-probe", StringComparer.OrdinalIgnoreCase))
{
    var liveCodexTasks = await new CodexTaskSnapshotSource().ReadAsync();
    Assert(liveCodexTasks.Available && liveCodexTasks.Tasks.All(task => !string.IsNullOrWhiteSpace(task.Title)),
        "live Codex App Server task read must return display-safe task summaries");
    Console.WriteLine($"PASS live Codex task read count={liveCodexTasks.Tasks.Count}; statuses={string.Join(',', liveCodexTasks.Tasks.Select(task => task.Status).Distinct())}; plans={string.Join(',', liveCodexTasks.Tasks.Where(task => task.HasPlanProgress).Select(task => $"{task.CompletedPlanSteps}/{task.TotalPlanSteps}"))}");
}

int codexRolloutProbeIndex = Array.IndexOf(args, "--codex-rollout-probe");
if (codexRolloutProbeIndex >= 0)
{
    Assert(codexRolloutProbeIndex + 1 < args.Length, "--codex-rollout-probe requires a rollout path");
    string rolloutPath = Path.GetFullPath(args[codexRolloutProbeIndex + 1]);
    bool hasPlan = CodexTaskSnapshotSource.TryReadLatestPlanProgress(rolloutPath, out int completedSteps, out int totalSteps);
    int planMentions = 0;
    int parsedPlans = 0;
    using var rolloutStream = new FileStream(rolloutPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
    using var rolloutReader = new StreamReader(rolloutStream);
    while (rolloutReader.ReadLine() is { } line)
    {
        if (!line.Contains("update_plan", StringComparison.Ordinal))
        {
            continue;
        }

        planMentions++;
        if (CodexTaskSnapshotSource.TryParsePlanProgressFromRolloutLine(line, out _, out _))
        {
            parsedPlans++;
        }
    }
    Console.WriteLine($"PASS live Codex rollout plan read hasPlan={hasPlan}; progress={completedSteps}/{totalSteps}; mentions={planMentions}; parsed={parsedPlans}");
}

var subscriptionQuota = AiQuotaSnapshot.ForSubscription(
    "Codex",
    56,
    remainingCount: 1,
    resetPeriod: AiResetPeriod.Weekly,
    resetsAt: new DateTimeOffset(2026, 8, 9, 18, 42, 0, TimeSpan.Zero));
Assert(subscriptionQuota.RemainingDisplay == "56% / 1次", "subscription quota display is incorrect");
Assert(subscriptionQuota.ResetPeriod == AiResetPeriod.Weekly, "subscription reset period was not retained");
Assert(SystemSnapshot.DesignSample.AiQuota?.PlatformName == "Codex",
    "the default device-preview sample must use the confirmed Codex label");

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

var longTitleFrame = renderer.Render(
    themes.Single(theme => theme.Id == "music"),
    SystemSnapshot.DesignSample with
    {
        Music = SystemSnapshot.DesignSample.Music! with
        {
            Title = "一首很甜的歌，刚好在夜晚想起你"
        }
    });
Assert(longTitleFrame.Width == 142 && longTitleFrame.Height == 428
       && longTitleFrame.JpegBytes.Length <= profile.MaxJpegBytes
       && longTitleFrame.JpegBytes is [0xFF, 0xD8, ..]
       && !longTitleFrame.JpegBytes.SequenceEqual(renderer.Render(themes.Single(theme => theme.Id == "music"), SystemSnapshot.DesignSample).JpegBytes),
    "long music titles must render a distinct device-compatible frame");
Console.WriteLine("PASS long music title renders within the device frame");

int musicPreviewArgumentIndex = Array.IndexOf(args, "--music-preview");
if (musicPreviewArgumentIndex >= 0)
{
    Assert(musicPreviewArgumentIndex + 1 < args.Length, "--music-preview requires an output path");
    var liveMusicSource = new WindowsMusicSnapshotSource();
    var liveMusic = await liveMusicSource.ReadAsync();
    Assert(liveMusic.Available && !string.IsNullOrWhiteSpace(liveMusic.Title),
        "live music preview requires an available media session with a track title");

    if (WindowsMusicSessionSelector.IsNetEase(liveMusic.SourceAppId))
    {
        using var enricher = new NetEaseMusicSnapshotEnricher();
        liveMusic = await enricher.EnrichAsync(liveMusic);
        using var fallback = new LrcLibLyricsSnapshotSource();
        using var lyricsSource = new NetEaseLyricsSnapshotSource(fallback);
        liveMusic = liveMusic with { Lyrics = await lyricsSource.ReadAsync(liveMusic) };
        Assert(liveMusic.Artwork is { Length: > 0 } && liveMusic.Lyrics.Available && liveMusic.Lyrics.Lines.Count > 0,
            "NetEase live music preview requires artwork and synchronized lyrics");
    }

    var liveMusicFrame = renderer.Render(
        themes.Single(theme => theme.Id == "music"),
        SystemSnapshot.DesignSample with { Music = liveMusic });
    Assert(liveMusicFrame.Width == 142 && liveMusicFrame.Height == 428
           && liveMusicFrame.JpegBytes.Length <= profile.MaxJpegBytes
           && liveMusicFrame.JpegBytes is [0xFF, 0xD8, ..],
        "live music preview must render a device-compatible JPEG");

    var previewPath = Path.GetFullPath(args[musicPreviewArgumentIndex + 1]);
    Directory.CreateDirectory(Path.GetDirectoryName(previewPath)!);
    await File.WriteAllBytesAsync(previewPath, liveMusicFrame.JpegBytes);
    Assert(File.Exists(previewPath) && new FileInfo(previewPath).Length == liveMusicFrame.JpegBytes.Length,
        "live music preview file was not written completely");
    Console.WriteLine($"PASS wrote live music preview to {previewPath}");
}

int musicMotionPreviewArgumentIndex = Array.IndexOf(args, "--music-motion-preview");
if (musicMotionPreviewArgumentIndex >= 0)
{
    Assert(musicMotionPreviewArgumentIndex + 1 < args.Length, "--music-motion-preview requires an output directory");
    var liveMusicSource = new WindowsMusicSnapshotSource();
    var initial = await liveMusicSource.ReadAsync();
    Assert(initial.Available && initial.IsPlaying && !string.IsNullOrWhiteSpace(initial.Title),
        "live music motion preview requires an actively playing media session");
    MusicSnapshot enrichedInitial = initial;
    LyricsSnapshot? synchronizedLyrics = null;
    if (WindowsMusicSessionSelector.IsNetEase(initial.SourceAppId))
    {
        using var enricher = new NetEaseMusicSnapshotEnricher();
        enrichedInitial = await enricher.EnrichAsync(initial);
        using var fallback = new LrcLibLyricsSnapshotSource();
        using var lyricsSource = new NetEaseLyricsSnapshotSource(fallback);
        synchronizedLyrics = await lyricsSource.ReadAsync(enrichedInitial);
        Assert(enrichedInitial.Artwork is { Length: > 0 } && synchronizedLyrics.Available && synchronizedLyrics.Lines.Count > 0,
            "NetEase live music motion preview requires artwork and synchronized lyrics");
    }

    TimeSpan firstLyricTime = synchronizedLyrics?.Lines[0].Timestamp ?? TimeSpan.Zero;
    double warmupSeconds = Math.Clamp((firstLyricTime - initial.Position).TotalSeconds + 1, 6, 60);
    await Task.Delay(TimeSpan.FromSeconds(warmupSeconds));
    var before = await liveMusicSource.ReadAsync();
    Assert(before.Available && string.Equals(initial.Title, before.Title, StringComparison.Ordinal)
           && before.Position > initial.Position,
        "live music motion preview requires the track to advance past its opening preview state");
    await Task.Delay(TimeSpan.FromSeconds(4));
    var after = await liveMusicSource.ReadAsync();
    Assert(after.Available && string.Equals(before.Title, after.Title, StringComparison.Ordinal)
           && after.Position > before.Position,
        "live music motion preview requires the same track to advance");

    if (synchronizedLyrics is not null)
    {
        before = before with
        {
            Artwork = enrichedInitial.Artwork,
            ProviderTrackId = enrichedInitial.ProviderTrackId,
            AlbumTitle = enrichedInitial.AlbumTitle,
            Lyrics = synchronizedLyrics
        };
        after = after with
        {
            Artwork = enrichedInitial.Artwork,
            ProviderTrackId = enrichedInitial.ProviderTrackId,
            AlbumTitle = enrichedInitial.AlbumTitle,
            Lyrics = synchronizedLyrics
        };
    }

    var musicThemeForMotion = themes.Single(theme => theme.Id == "music");
    var beforeFrame = renderer.Render(musicThemeForMotion, SystemSnapshot.DesignSample with { Music = before });
    var afterFrame = renderer.Render(musicThemeForMotion, SystemSnapshot.DesignSample with { Music = after });
    Assert(!beforeFrame.JpegBytes.SequenceEqual(afterFrame.JpegBytes),
        "live music motion preview frames must change as the active track advances");

    string outputDirectory = Path.GetFullPath(args[musicMotionPreviewArgumentIndex + 1]);
    Directory.CreateDirectory(outputDirectory);
    string beforePath = Path.Combine(outputDirectory, "music-live-before.jpg");
    string afterPath = Path.Combine(outputDirectory, "music-live-after.jpg");
    await File.WriteAllBytesAsync(beforePath, beforeFrame.JpegBytes);
    await File.WriteAllBytesAsync(afterPath, afterFrame.JpegBytes);
    var beforeContext = before.Lyrics.FindContextAt(before.Position);
    var afterContext = after.Lyrics.FindContextAt(after.Position);
    Console.WriteLine($"PASS wrote live music motion previews after {warmupSeconds:0.0}s at {before.Position:c} ({beforeContext.Current?.Text ?? "即将开始"}) and {after.Position:c} ({afterContext.Current?.Text ?? "即将开始"}) to {outputDirectory}");
}

var quotaTaskSnapshot = new CodexTaskSnapshot(
    true,
    [
        new CodexTaskItem("设计四条 Codex 任务小屏界面", CodexTaskStatus.Active, DateTimeOffset.UtcNow, 2, 4),
        new CodexTaskItem("完成歌词连续刷新与视觉验收", CodexTaskStatus.Completed, DateTimeOffset.UtcNow.AddMinutes(-12)),
        new CodexTaskItem("校准额度圆环与具体重置时间", CodexTaskStatus.Completed, DateTimeOffset.UtcNow.AddMinutes(-28)),
        new CodexTaskItem("处理一个很长很长很长的任务标题，确认小屏不会让右侧状态与标题发生重叠", CodexTaskStatus.Completed, DateTimeOffset.UtcNow.AddHours(-1))
    ],
    DateTimeOffset.UtcNow);
var subscriptionFrame = renderer.Render(
    aiQuotaTheme,
    SystemSnapshot.DesignSample with { AiQuota = subscriptionQuota, CodexTasks = quotaTaskSnapshot });
Assert(subscriptionFrame.JpegBytes is [0xFF, 0xD8, ..]
       && subscriptionFrame.JpegBytes.Length <= profile.MaxJpegBytes,
    "subscription AI quota theme with tasks did not render");

var alternatePlatformFrame = renderer.Render(
    aiQuotaTheme,
    SystemSnapshot.DesignSample with { AiQuota = subscriptionQuota with { PlatformName = "MiMo" }, CodexTasks = null });
Assert(!subscriptionFrame.JpegBytes.SequenceEqual(alternatePlatformFrame.JpegBytes),
    "the quota device frame must render its current platform label instead of a hard-coded Codex name");

var fullQuotaFrame = renderer.Render(
    aiQuotaTheme,
    SystemSnapshot.DesignSample with
    {
        AiQuota = AiQuotaSnapshot.ForSubscription("Codex", 100, remainingCount: 3, resetPeriod: AiResetPeriod.Weekly,
            resetsAt: new DateTimeOffset(2026, 8, 9, 18, 42, 0, TimeSpan.Zero)),
        CodexTasks = quotaTaskSnapshot
    });
Assert(fullQuotaFrame.JpegBytes is [0xFF, 0xD8, ..]
       && fullQuotaFrame.JpegBytes.Length <= profile.MaxJpegBytes
       && !fullQuotaFrame.JpegBytes.SequenceEqual(subscriptionFrame.JpegBytes),
    "full quota must render a complete device-compatible circular gauge");

var autoRenewQuotaFrame = renderer.Render(
    aiQuotaTheme,
    SystemSnapshot.DesignSample with
    {
        AiQuota = AiQuotaSnapshot.ForSubscription("Codex", 0, resetPeriod: AiResetPeriod.Weekly),
        CodexTasks = CodexTaskSnapshot.Unavailable(DateTimeOffset.UtcNow)
    });
Assert(autoRenewQuotaFrame.JpegBytes is [0xFF, 0xD8, ..]
       && autoRenewQuotaFrame.JpegBytes.Length <= profile.MaxJpegBytes,
    "unavailable Codex task state must render without affecting the quota gauge");

var apiKeyFrame = renderer.Render(
    aiQuotaTheme,
    SystemSnapshot.DesignSample with { AiQuota = apiKeyQuota, CodexTasks = null });
Assert(apiKeyFrame.JpegBytes is [0xFF, 0xD8, ..], "API Key AI quota theme did not render");
var codexTasksTheme = themes.Single(theme => theme.Id == "codex-tasks");
var tasksFrame = renderer.Render(
    codexTasksTheme,
    SystemSnapshot.DesignSample with { CodexTasks = quotaTaskSnapshot });
var emptyTasksFrame = renderer.Render(
    codexTasksTheme,
    SystemSnapshot.DesignSample with { CodexTasks = CodexTaskSnapshot.Empty(DateTimeOffset.UtcNow) });
var unavailableTasksFrame = renderer.Render(
    codexTasksTheme,
    SystemSnapshot.DesignSample with { CodexTasks = CodexTaskSnapshot.Unavailable(DateTimeOffset.UtcNow) });
Assert(tasksFrame.JpegBytes is [0xFF, 0xD8, ..]
       && tasksFrame.JpegBytes.Length <= profile.MaxJpegBytes
       && emptyTasksFrame.JpegBytes is [0xFF, 0xD8, ..]
       && unavailableTasksFrame.JpegBytes is [0xFF, 0xD8, ..]
       && !tasksFrame.JpegBytes.SequenceEqual(emptyTasksFrame.JpegBytes),
    "four-task Codex theme must render task, empty and unavailable states within the device limit");
Console.WriteLine("PASS AI quota tasks and four-row Codex task theme rendering");

int codexTaskPreviewIndex = Array.IndexOf(args, "--codex-task-preview");
if (codexTaskPreviewIndex >= 0)
{
    Assert(codexTaskPreviewIndex + 1 < args.Length, "--codex-task-preview requires an output directory");
    string outputDirectory = Path.GetFullPath(args[codexTaskPreviewIndex + 1]);
    Directory.CreateDirectory(outputDirectory);
    string quotaPath = Path.Combine(outputDirectory, "codex-quota-tasks.jpg");
    string tasksPath = Path.Combine(outputDirectory, "codex-tasks-four-rows.jpg");
    await File.WriteAllBytesAsync(quotaPath, subscriptionFrame.JpegBytes);
    await File.WriteAllBytesAsync(tasksPath, tasksFrame.JpegBytes);
    Assert(File.Exists(quotaPath) && File.Exists(tasksPath),
        "Codex task device previews must be written completely");
    Console.WriteLine($"PASS wrote Codex task previews to {outputDirectory}");
}

var timedLyrics = LrcLibLyricsSnapshotSource.ParseSyncedLyrics("[00:01.20] First line\n[00:03.450] 第二行\ninvalid");
Assert(timedLyrics.Count == 2, "synchronized lyrics parser must ignore invalid lines");
Assert(timedLyrics[0].Timestamp == TimeSpan.FromMilliseconds(1200), "two-digit lyric fraction was not normalized");
Assert(timedLyrics[1].Text == "第二行", "Unicode lyric text was not retained");
var lyricSnapshot = new LyricsSnapshot(true, timedLyrics);
var lyricPosition = lyricSnapshot.FindAt(TimeSpan.FromSeconds(2));
Assert(lyricPosition.Current?.Text == "First line" && lyricPosition.Next?.Text == "第二行", "active lyric lookup failed");
var lyricContext = new LyricsSnapshot(true,
[
    new LyricLine(TimeSpan.Zero, "上一句"),
    new LyricLine(TimeSpan.FromSeconds(2), "当前句"),
    new LyricLine(TimeSpan.FromSeconds(4), "下一句")
]).FindContextAt(TimeSpan.FromSeconds(3));
Assert(lyricContext.Previous?.Text == "上一句" && lyricContext.Current?.Text == "当前句" && lyricContext.Next?.Text == "下一句",
    "lyric context lookup must include the previous, current and next lines");
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

var netEaseSearchHandler = new NetEaseArtworkHandler();
using (var netEaseSearchClient = new HttpClient(netEaseSearchHandler))
using (var netEaseEnricher = new NetEaseMusicSnapshotEnricher(netEaseSearchClient))
{
    var netEaseMusic = SystemSnapshot.DesignSample.Music! with
    {
        SourceAppId = "cloudmusic.exe",
        Title = "Demo Track",
        Artist = "Demo Artist"
    };
    var enrichedMusic = await netEaseEnricher.EnrichAsync(netEaseMusic);
    Assert(enrichedMusic.ProviderTrackId == 987654 && enrichedMusic.AlbumTitle == "Demo Album"
           && enrichedMusic.Artwork is { Length: > 0 },
        "NetEase search must resolve canonical metadata, cover artwork and its track ID");
    var cachedMusic = await netEaseEnricher.EnrichAsync(netEaseMusic);
    Assert(cachedMusic.ProviderTrackId == 987654 && cachedMusic.Artwork is { Length: > 0 }
           && netEaseSearchHandler.RequestCount == 3,
        "NetEase metadata and artwork lookups must be cached per track");

    var netEaseLyricResponses = new Queue<string>(new[]
    {
        """{"lrc":{"lyric":"[00:00.00]作词：Demo Writer\n[00:00.20]作曲: Demo Composer\n[00:00.40]人声处理：Demo Vocal\n[00:01.20] First line\n[00:03.45] Second line\n[00:05.00]作词：这是正式歌词"}}"""
    });
    var netEaseLyricHandler = new SequenceHandler(netEaseLyricResponses);
    using var netEaseLyricClient = new HttpClient(netEaseLyricHandler);
    using var netEaseFallback = new LrcLibLyricsSnapshotSource();
    using var netEaseLyrics = new NetEaseLyricsSnapshotSource(netEaseFallback, netEaseLyricClient);
    var netEaseSnapshot = await netEaseLyrics.ReadAsync(enrichedMusic);
    Assert(netEaseSnapshot.Available && netEaseSnapshot.Lines.Count == 3
           && netEaseSnapshot.Lines[0].Text == "First line"
           && netEaseSnapshot.Lines[2].Text == "作词：这是正式歌词"
           && netEaseLyricHandler.RequestCount == 1,
        "NetEase lyrics must remove only leading credits while preserving timed lyrics");
}
Console.WriteLine("PASS NetEase metadata resolution and timed lyrics");

var animatedMusic = SystemSnapshot.DesignSample.Music! with
{
    Position = TimeSpan.FromSeconds(10),
    Lyrics = lyricSnapshot,
    SourceAppId = "Spotify.exe"
};
var musicTheme = themes.Single(theme => theme.Id == "music");
var musicFrameA = renderer.Render(musicTheme, SystemSnapshot.DesignSample with
{
    Music = animatedMusic with { Lyrics = new LyricsSnapshot(true, [new LyricLine(TimeSpan.Zero, "第一句歌词")]) }
});
var musicFrameB = renderer.Render(musicTheme, SystemSnapshot.DesignSample with
{
    Music = animatedMusic with { Lyrics = new LyricsSnapshot(true, [new LyricLine(TimeSpan.Zero, "第二句歌词")]) }
});
Assert(!musicFrameA.JpegBytes.SequenceEqual(musicFrameB.JpegBytes), "music theme must render the current lyric below the progress bar");
var firstLyricPreview = renderer.Render(musicTheme, SystemSnapshot.DesignSample with
{
    Music = animatedMusic with
    {
        Position = TimeSpan.Zero,
        Lyrics = new LyricsSnapshot(true, [new LyricLine(TimeSpan.FromSeconds(5), "首句即将开始")])
    }
});
var unavailableLyricsPreview = renderer.Render(musicTheme, SystemSnapshot.DesignSample with
{
    Music = animatedMusic with { Position = TimeSpan.Zero, Lyrics = LyricsSnapshot.Unavailable }
});
Assert(firstLyricPreview.JpegBytes.Length <= profile.MaxJpegBytes
       && !firstLyricPreview.JpegBytes.SequenceEqual(unavailableLyricsPreview.JpegBytes),
    "music theme must show a distinct first-lyric preview before its timestamp");
var timedMusicLyrics = new LyricsSnapshot(true,
[
    new LyricLine(TimeSpan.Zero, "第一句歌词"),
    new LyricLine(TimeSpan.FromSeconds(5), "第二句歌词"),
    new LyricLine(TimeSpan.FromSeconds(10), "第三句歌词")
]);
var positionFrameA = renderer.Render(musicTheme, SystemSnapshot.DesignSample with
{
    Music = animatedMusic with { Position = TimeSpan.FromSeconds(1), Lyrics = timedMusicLyrics }
});
var positionFrameB = renderer.Render(musicTheme, SystemSnapshot.DesignSample with
{
    Music = animatedMusic with { Position = TimeSpan.FromSeconds(6), Lyrics = timedMusicLyrics }
});
Assert(!positionFrameA.JpegBytes.SequenceEqual(positionFrameB.JpegBytes),
    "music theme must render a new lyric frame after playback crosses the next lyric timestamp");
var lyricMotionMusic = animatedMusic with
{
    Duration = TimeSpan.Zero,
    Lyrics = timedMusicLyrics
};
var lyricMotionFrameA = renderer.Render(musicTheme, SystemSnapshot.DesignSample with
{
    Music = lyricMotionMusic with { Position = TimeSpan.FromSeconds(6.1) }
});
var lyricMotionFrameB = renderer.Render(musicTheme, SystemSnapshot.DesignSample with
{
    Music = lyricMotionMusic with { Position = TimeSpan.FromSeconds(6.7) }
});
Assert(!lyricMotionFrameA.JpegBytes.SequenceEqual(lyricMotionFrameB.JpegBytes),
    "music theme must animate the lyric activity indicator within the same lyric line");
var continuousLyricsFrame = renderer.Render(musicTheme, SystemSnapshot.DesignSample with
{
    Music = animatedMusic with
    {
        Position = TimeSpan.FromSeconds(6),
        Lyrics = new LyricsSnapshot(true,
        [
            new LyricLine(TimeSpan.Zero, "上一句同步歌词"),
            new LyricLine(TimeSpan.FromSeconds(5), "这一句很长，会在音乐屏幕中稳定换成两行显示"),
            new LyricLine(TimeSpan.FromSeconds(10), "下一句同步歌词")
        ])
    }
});
var noArtworkFrame = renderer.Render(musicTheme, SystemSnapshot.DesignSample with
{
    Music = animatedMusic with { Artwork = null, Position = TimeSpan.FromSeconds(6), Lyrics = timedMusicLyrics }
});
var invalidArtworkFrame = renderer.Render(musicTheme, SystemSnapshot.DesignSample with
{
    Music = animatedMusic with { Artwork = [0x00, 0x01, 0x02], Position = TimeSpan.FromSeconds(6), Lyrics = timedMusicLyrics }
});
Assert(continuousLyricsFrame.JpegBytes.Length <= profile.MaxJpegBytes
       && noArtworkFrame.JpegBytes.SequenceEqual(invalidArtworkFrame.JpegBytes),
    "continuous lyrics and invalid artwork fallback must render stable device frames");
int musicDesignPreviewArgumentIndex = Array.IndexOf(args, "--music-design-preview");
if (musicDesignPreviewArgumentIndex >= 0)
{
    Assert(musicDesignPreviewArgumentIndex + 1 < args.Length, "--music-design-preview requires an output path");
    var previewPath = Path.GetFullPath(args[musicDesignPreviewArgumentIndex + 1]);
    Directory.CreateDirectory(Path.GetDirectoryName(previewPath)!);
    await File.WriteAllBytesAsync(previewPath, continuousLyricsFrame.JpegBytes);
    Assert(File.Exists(previewPath) && new FileInfo(previewPath).Length == continuousLyricsFrame.JpegBytes.Length,
        "music design preview file was not written completely");
    Console.WriteLine($"PASS wrote continuous-lyrics design preview to {previewPath}");
}
Console.WriteLine("PASS cover music theme renders continuous lyrics and stable artwork fallback");

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
var daylightOptions = new ScreenDisplayOptions(ColorMode: ScreenColorMode.Daylight);
var daylightFrame = renderer.Render(themes.Single(theme => theme.Id == "clock-weather"), SystemSnapshot.DesignSample, displayOptions: daylightOptions);
Assert(daylightFrame.JpegBytes is [0xFF, 0xD8, ..], "daylight clock-and-weather appearance did not render");
Console.WriteLine("PASS configurable device-screen daylight render");

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

int aiAutoPreviewArgumentIndex = Array.IndexOf(args, "--ai-auto-preview");
if (aiAutoPreviewArgumentIndex >= 0)
{
    Assert(aiAutoPreviewArgumentIndex + 1 < args.Length, "--ai-auto-preview requires an output path");
    var previewPath = Path.GetFullPath(args[aiAutoPreviewArgumentIndex + 1]);
    Directory.CreateDirectory(Path.GetDirectoryName(previewPath)!);
    await File.WriteAllBytesAsync(previewPath, autoRenewQuotaFrame.JpegBytes);
    Console.WriteLine($"PASS wrote automatic-renewal quota preview to {previewPath}");
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
    var settings = new AppSettings { AppearanceMode = AppearanceMode.Dark, ScreenColorMode = ScreenColorMode.Daylight, SelectedThemeId = "music", RefreshSeconds = 17, AccentColor = "#A23BFF", SelectedFontId = "file:test.ttf|test", SafeArea = new ScreenInsets(11, 53, 9, 13), AiQuota = new AiQuotaSettings { SourceKind = AiQuotaSourceKind.OpenAICodex, DisplayName = "Codex Pro" }, Weather = new WeatherSettings { LocationQuery = "上海", UseAutomaticLocation = true }, Music = new MusicSettings { EnableOnlineLyrics = true, LyricOffsetSeconds = 1.5 }, ImageTimePlacement = ImageTimePlacement.Top, LaunchAtStartup = true, AutoSwitchToMusic = true, HasCompletedOnboarding = true, HasAcknowledgedCodexNotice = true };
    await settingsStore.SaveAsync(settings);
    var loadedSettings = await settingsStore.LoadAsync();
    Assert(loadedSettings.SelectedThemeId == "music", "settings theme did not persist");
    Assert(loadedSettings.AppearanceMode == AppearanceMode.Dark, "appearance mode did not persist");
    Assert(loadedSettings.ScreenColorMode == ScreenColorMode.Daylight, "screen color mode did not persist");
    Assert(loadedSettings.RefreshSeconds == 17, "settings refresh interval did not persist");
    Assert(loadedSettings.AccentColor == "#A23BFF", "settings accent color did not persist");
    Assert(loadedSettings.SelectedFontId == "file:test.ttf|test", "settings font did not persist");
    Assert(loadedSettings.SafeArea == settings.SafeArea, "settings safe area did not persist");
    Assert(loadedSettings.AiQuota.SourceKind == AiQuotaSourceKind.OpenAICodex
           && loadedSettings.AiQuota.DisplayName == "Codex Pro", "Codex AI display settings did not persist");
    Assert(loadedSettings.Weather.LocationQuery == "上海" && loadedSettings.Weather.UseAutomaticLocation, "weather location settings did not persist");
    Assert(loadedSettings.LaunchAtStartup, "launch-at-startup setting did not persist");
    Assert(loadedSettings.HasCompletedOnboarding, "onboarding completion did not persist");
    Assert(loadedSettings.HasAcknowledgedCodexNotice,
        "feature notice acknowledgement did not persist");
    Assert(loadedSettings.ImageTimePlacement == ImageTimePlacement.Top, "image time placement did not persist");
    Assert(loadedSettings.AutoSwitchToMusic, "auto switch to music setting did not persist");
    Assert(loadedSettings.Music.EnableOnlineLyrics && loadedSettings.Music.LyricOffsetSeconds == 1.5, "music settings did not persist");
    Assert(loadedSettings.SettingsVersion == AppSettings.CurrentSettingsVersion, "settings schema version did not persist");
    string settingsDirectory = Path.GetDirectoryName(settingsPath)!;
    string settingsFileName = Path.GetFileName(settingsPath);
    Assert(Directory.GetFiles(settingsDirectory, $"{settingsFileName}.*.tmp").Length == 0,
        "atomic settings save left a temporary file behind");

    await File.WriteAllTextAsync(settingsPath, """
    { "SettingsVersion": 3, "SelectedThemeId": "music-vinyl", "MediaPlayingThemeId": "music-minimal", "MediaIdleThemeId": "music-poster", "AiQuota": { "SourceKind": 0, "DisplayName": "MiMo" } }
    """);
    var migratedSettings = await settingsStore.LoadAsync();
    Assert(migratedSettings.AiQuota.SourceKind == AiQuotaSourceKind.OpenAICodex
           && migratedSettings.AiQuota.DisplayName == "Codex"
           && migratedSettings.SelectedThemeId == "music",
        "former MiMo and removed music themes must migrate to supported defaults");
    using var persistedMigration = JsonDocument.Parse(await File.ReadAllTextAsync(settingsPath));
    Assert(persistedMigration.RootElement.GetProperty("SettingsVersion").GetInt32() == AppSettings.CurrentSettingsVersion
           && persistedMigration.RootElement.GetProperty("AiQuota").GetProperty("SourceKind").GetInt32() == (int)AiQuotaSourceKind.OpenAICodex,
        "the Codex quota migration must be saved atomically for the next startup");

    await File.WriteAllTextAsync(settingsPath, """
    { "SettingsVersion": 7, "SelectedThemeId": "stocks", "Stocks": { "RedForGain": false, "Items": [{ "Symbol": "AAPL" }] } }
    """);
    var stockMigration = await settingsStore.LoadAsync();
    Assert(stockMigration.SelectedThemeId == "clock-weather",
        "legacy stock selection must migrate to clock-and-weather");
    using var persistedStockMigration = JsonDocument.Parse(await File.ReadAllTextAsync(settingsPath));
    Assert(!persistedStockMigration.RootElement.TryGetProperty("Stocks", out _),
        "legacy stock settings must be removed when migrated settings are persisted");

    await File.WriteAllTextAsync(settingsPath, "{ invalid json");
    var recoveredSettings = await settingsStore.LoadAsync();
    Assert(recoveredSettings.SettingsVersion == AppSettings.CurrentSettingsVersion
           && recoveredSettings.SelectedThemeId == "clock-weather",
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

sealed class NetEaseArtworkHandler : HttpMessageHandler
{
    public int RequestCount { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestCount++;
        string requestUri = request.RequestUri?.AbsoluteUri ?? string.Empty;
        if (requestUri.Contains("/api/search/get/web", StringComparison.Ordinal))
        {
            return Task.FromResult(Json("""{"result":{"songs":[{"id":987654,"name":"Demo Track","artists":[{"name":"Demo Artist"}],"album":{"name":"Demo Album"},"duration":225000}]}}"""));
        }

        if (requestUri.Contains("/api/song/detail", StringComparison.Ordinal))
        {
            return Task.FromResult(Json("""{"songs":[{"album":{"picUrl":"https://cover.test/demo.jpg"}}]}"""));
        }

        if (request.RequestUri?.Host == "cover.test")
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([0xFF, 0xD8, 0xFF, 0xD9])
            };
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
            return Task.FromResult(response);
        }

        throw new InvalidOperationException("Unexpected NetEase request: " + requestUri);
    }

    private static HttpResponseMessage Json(string content) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(content)
    };
}

sealed class StubSystemSnapshotSource : ISystemSnapshotSource
{
    public ValueTask<SystemSnapshot> ReadAsync(CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(SystemSnapshot.DesignSample with
        {
            Music = null,
            AiQuota = null,
            Weather = null
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

sealed class StubMusicSnapshotEnricher : IMusicSnapshotEnricher
{
    public int ReadCount { get; private set; }

    public Task<MusicSnapshot> EnrichAsync(MusicSnapshot music, CancellationToken cancellationToken = default)
    {
        ReadCount++;
        return Task.FromResult(music with { Artwork = [0xFF, 0xD8, 0xFF, 0xD9] });
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
