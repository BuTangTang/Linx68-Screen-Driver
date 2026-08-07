using System.Windows.Media;

namespace Linx68.ScreenDriver.Core;

public sealed record ScreenFontOption(string Id, string DisplayName, string FamilyName, string? FileName, FontFamily FontFamily, bool IsBuiltIn = false)
{
	public static ScreenFontOption Default { get; } = new ScreenFontOption("builtin:segoe-variable-display", "Segoe UI Variable Display（默认）", "Segoe UI Variable Display", null, new FontFamily("Segoe UI Variable Display"), IsBuiltIn: true);


	public const string DefaultId = "builtin:segoe-variable-display";
}
