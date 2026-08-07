namespace Linx68.ScreenDriver.Core;

public sealed record StockSnapshot(
    IReadOnlyList<StockQuoteSnapshot> Quotes,
    DateTimeOffset UpdatedAt,
    bool RedForGain,
    bool IsStale = false,
    string? ErrorMessage = null)
{
    public static StockSnapshot Empty { get; } = new([], DateTimeOffset.MinValue, true);
}

public sealed record StockQuoteSnapshot(
    string Symbol,
    string DisplayName,
    double CurrentPrice,
    double ChangePercent,
    DateTimeOffset UpdatedAt);