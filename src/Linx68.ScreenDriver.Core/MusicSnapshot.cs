using System;

namespace Linx68.ScreenDriver.Core;

public sealed record MusicSnapshot(bool Available, string Title, string Artist, TimeSpan Position, TimeSpan Duration, bool IsPlaying, byte[]? Artwork)
{
	public static MusicSnapshot Unavailable { get; } = new MusicSnapshot(Available: false, "没有正在播放的音乐", "", TimeSpan.Zero, TimeSpan.Zero, IsPlaying: false, null);

	public string SourceAppId { get; init; } = string.Empty;

	public string AlbumTitle { get; init; } = string.Empty;

	public long? ProviderTrackId { get; init; }

	public LyricsSnapshot Lyrics { get; init; } = LyricsSnapshot.Unavailable;
}
