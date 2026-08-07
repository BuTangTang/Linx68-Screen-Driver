using Linx68.ScreenDriver.Core;

namespace Linx68.ScreenDriver.Application;

public interface IAiQuotaSnapshotSource
{
    Task<AiQuotaSnapshot> ReadAsync(CancellationToken cancellationToken = default);
}
