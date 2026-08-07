namespace Linx68.ScreenDriver.Core;

public sealed record LyricLine(TimeSpan Timestamp, string Text);

public sealed record LyricsSnapshot(bool Available, IReadOnlyList<LyricLine> Lines, string? ErrorMessage = null)
{
	public static LyricsSnapshot Unavailable { get; } = new(false, []);

	public (LyricLine? Current, LyricLine? Next) FindAt(TimeSpan position, double offsetSeconds = 0)
	{
		if (!Available || Lines.Count == 0)
		{
			return (null, null);
		}

		TimeSpan adjusted = position + TimeSpan.FromSeconds(offsetSeconds);
		int currentIndex = -1;
		for (int index = 0; index < Lines.Count; index++)
		{
			if (Lines[index].Timestamp > adjusted)
			{
				break;
			}
			currentIndex = index;
		}

		return (
			currentIndex >= 0 ? Lines[currentIndex] : null,
			currentIndex + 1 < Lines.Count ? Lines[currentIndex + 1] : null);
	}
}
