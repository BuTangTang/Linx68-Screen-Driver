using Linx68.ScreenDriver.Core;

namespace Linx68.ScreenDriver.Application;

public interface ICodexTaskSnapshotSource
{
    Task<CodexTaskSnapshot> ReadAsync(CancellationToken cancellationToken = default);
}
