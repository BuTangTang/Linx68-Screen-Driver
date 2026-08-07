using Linx68.ScreenDriver.Core;

namespace Linx68.ScreenDriver.Application;

/// <summary>
/// Supplements media-session information when a player exposes incomplete metadata.
/// </summary>
public interface IMusicSnapshotEnricher
{
    Task<MusicSnapshot> EnrichAsync(MusicSnapshot music, CancellationToken cancellationToken = default);
}
