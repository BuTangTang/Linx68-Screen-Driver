namespace Linx68.ScreenDriver.Core;

public enum AiAccessType
{
    ApiKey,
    Subscription
}

public enum AiQuotaMetric
{
    Token,
    Cost,
    Credit,
    Request,
    Custom
}

public enum AiResetPeriod
{
    None,
    Hourly,
    Daily,
    Weekly,
    Monthly,
    BillingCycle,
    Custom
}

public sealed record AiQuotaBalance(
    AiQuotaMetric Metric,
    decimal Used,
    decimal Limit,
    decimal? Remaining = null,
    string? UnitLabel = null)
{
    public decimal EffectiveRemaining =>
        Math.Clamp(Remaining ?? Limit - Used, 0m, Math.Max(0m, Limit));

    public double RemainingPercent =>
        Limit <= 0m ? 0d : (double)(EffectiveRemaining / Limit * 100m);
}

public sealed record AiQuotaSnapshot(
    bool Available,
    string PlatformName,
    AiAccessType AccessType,
    double RemainingPercent,
    AiQuotaBalance? Balance = null,
    int? RemainingCount = null,
    AiResetPeriod ResetPeriod = AiResetPeriod.None,
    DateTimeOffset? ResetsAt = null)
{
    public static AiQuotaSnapshot Empty { get; } =
        new(false, "AI", AiAccessType.Subscription, 0);

    public double ClampedRemainingPercent =>
        Math.Clamp(RemainingPercent, 0d, 100d);

    public string RemainingDisplay
    {
        get
        {
            var percent = $"{Math.Round(ClampedRemainingPercent):0}%";
            return AccessType == AiAccessType.Subscription && RemainingCount is >= 0
                ? $"{percent} / {RemainingCount}次"
                : percent;
        }
    }

    public static AiQuotaSnapshot ForApiKey(
        string platformName,
        AiQuotaBalance balance,
        AiResetPeriod resetPeriod = AiResetPeriod.None,
        DateTimeOffset? resetsAt = null) =>
        new(
            true,
            platformName,
            AiAccessType.ApiKey,
            balance.RemainingPercent,
            balance,
            null,
            resetPeriod,
            resetsAt);

    public static AiQuotaSnapshot ForSubscription(
        string platformName,
        double remainingPercent,
        int? remainingCount = null,
        AiResetPeriod resetPeriod = AiResetPeriod.None,
        DateTimeOffset? resetsAt = null) =>
        new(
            true,
            platformName,
            AiAccessType.Subscription,
            remainingPercent,
            null,
            remainingCount,
            resetPeriod,
            resetsAt);
}
