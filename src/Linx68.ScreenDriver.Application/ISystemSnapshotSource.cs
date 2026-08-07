using System.Threading;
using System.Threading.Tasks;
using Linx68.ScreenDriver.Core;

namespace Linx68.ScreenDriver.Application;

public interface ISystemSnapshotSource
{
	ValueTask<SystemSnapshot> ReadAsync(CancellationToken cancellationToken = default(CancellationToken));
}
