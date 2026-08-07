using Linx68.ScreenDriver.Core;

namespace Linx68.ScreenDriver.Application;

public interface IStockSnapshotSource
{
    Task<StockSnapshot> ReadAsync(StockSettings settings, CancellationToken cancellationToken = default);
}
