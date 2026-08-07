namespace Linx68.ScreenDriver.Core;

public interface IScreenTheme
{
	string Id { get; }

	string DisplayName { get; }

	string Description { get; }

	string Details { get; }

	void Draw(ScreenCanvas canvas, SystemSnapshot snapshot);
}
