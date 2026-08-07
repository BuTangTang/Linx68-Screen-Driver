using Linx68.ScreenDriver.Core;

namespace Linx68.ScreenDriver.Application;

public interface ILyricsSnapshotSource
{
	Task<LyricsSnapshot> ReadAsync(MusicSnapshot music, CancellationToken cancellationToken = default);
}
