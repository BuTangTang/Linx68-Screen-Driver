namespace Linx68.ScreenDriver.Core;

public sealed class StockSettings
{
    public bool RedForGain { get; set; } = true;

    public List<StockItemSettings> Items { get; set; } =
    [
        new(),
        new(),
        new()
    ];
}

public sealed class StockItemSettings
{
    public string Symbol { get; set; } = string.Empty;

    public string Alias { get; set; } = string.Empty;
}
