namespace Linx68.ScreenDriver.Core;

public sealed record SystemSnapshot(
    DateTimeOffset Timestamp,
    double CpuPercent,
    double MemoryPercent,
    double DownloadMbps = 0.0,
    double UploadMbps = 0.0,
    MusicSnapshot? Music = null,
    AiQuotaSnapshot? AiQuota = null,
    WeatherSnapshot? Weather = null,
    CodexTaskSnapshot? CodexTasks = null)
{
    public static SystemSnapshot DesignSample { get; } = new(
        DateTimeOffset.Now,
        38.4,
        62.7,
        12.8,
        1.6,
        new MusicSnapshot(
            Available: true,
            "Midnight Drive",
            "Keyboard Studio",
            TimeSpan.FromSeconds(94),
            TimeSpan.FromSeconds(225),
            IsPlaying: true,
            null),
        AiQuotaSnapshot.ForSubscription(
            "Codex",
            56,
            remainingCount: 1,
            resetPeriod: AiResetPeriod.Weekly),
        new WeatherSnapshot(true, "北京", 26, 28, 61, 2, true, DateTimeOffset.Now),
        new CodexTaskSnapshot(
            true,
            [
                new CodexTaskItem("优化小屏歌词连续展示", CodexTaskStatus.Active, DateTimeOffset.Now, 2, 4),
                new CodexTaskItem("校验 Codex 额度主题", CodexTaskStatus.Recent, DateTimeOffset.Now.AddMinutes(-12))
            ],
            DateTimeOffset.Now));
}
