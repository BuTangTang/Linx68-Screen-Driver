using System.IO;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Linx68.ScreenDriver.Core;
using Linx68.ScreenDriver.Infrastructure;

if (args.Length < 3)
{
    Console.Error.WriteLine("Usage: ThemePreviewExporter <output-folder> <settings.json> <fonts-folder> [--daylight]");
    return 2;
}

var outputRoot = Path.GetFullPath(args[0]);
var settingsPath = Path.GetFullPath(args[1]);
var fontsFolder = Path.GetFullPath(args[2]);
var colorMode = args.Skip(3).Any(argument => string.Equals(argument, "--daylight", StringComparison.OrdinalIgnoreCase))
    ? ScreenColorMode.Daylight
    : ScreenColorMode.Night;
var rawFolder = Path.Combine(outputRoot, "01-原始屏幕-JPEG");
var cardsFolder = Path.Combine(outputRoot, "02-展示卡-PNG");
Directory.CreateDirectory(rawFolder);
Directory.CreateDirectory(cardsFolder);

var settings = await new JsonSettingsStore(settingsPath).LoadAsync();
using var fontCatalog = new FontFolderCatalog(fontsFolder);
var fontOptions = fontCatalog.Scan();
var selectedFont = fontOptions.FirstOrDefault(option =>
    string.Equals(option.Id, settings.SelectedFontId, StringComparison.OrdinalIgnoreCase))
    ?? ScreenFontOption.Default;

var profile = new ScreenProfile(142, 428, 524288, settings.SafeArea);
var renderer = new ScreenRenderer(profile);
var imageTheme = new ImageTheme { ImagePath = settings.ImagePath };
var themes = BuiltInThemes.Create(imageTheme);
var accent = ParseColor(settings.AccentColor, Color.FromRgb(255, 91, 0));
var displayOptions = new ScreenDisplayOptions(settings.ImageTimePlacement, colorMode);
byte[]? artwork = ReadOptionalImage(settings.ImagePath);
var sample = SystemSnapshot.DesignSample with
{
    Timestamp = DateTimeOffset.Now,
    Music = SystemSnapshot.DesignSample.Music is null
        ? null
        : SystemSnapshot.DesignSample.Music with
        {
            Artwork = artwork,
            SourceAppId = "Spotify.exe",
            Lyrics = new LyricsSnapshot(true,
            [
                new LyricLine(TimeSpan.FromSeconds(80), "Drive into the midnight"),
                new LyricLine(TimeSpan.FromSeconds(105), "City lights are passing by")
            ])
        },
    AiQuota = AiQuotaSnapshot.ForSubscription(
        string.IsNullOrWhiteSpace(settings.AiQuota.DisplayName) ? "AI" : settings.AiQuota.DisplayName,
        56,
        remainingCount: 1,
        resetPeriod: AiResetPeriod.Monthly)
};

var cards = new List<(IScreenTheme Theme, BitmapSource Image)>();
var manifest = new StringBuilder();
manifest.AppendLine("灵犀68屏幕驱动全主题预览");
manifest.AppendLine($"生成时间：{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
manifest.AppendLine($"主题数量：{themes.Count}");
manifest.AppendLine($"强调色：{settings.AccentColor}");
manifest.AppendLine($"字体：{selectedFont.DisplayName}");
manifest.AppendLine($"设备屏外观：{(colorMode == ScreenColorMode.Daylight ? "日间" : "夜间")}");
manifest.AppendLine();

for (var index = 0; index < themes.Count; index++)
{
    var theme = themes[index];
    var frame = renderer.Render(
        theme,
        sample,
        jpegQuality: 100,
        accentColor: accent,
        fontFamily: selectedFont.FontFamily,
        displayOptions: displayOptions);
    var safeName = SanitizeFileName(theme.DisplayName);
    var prefix = (index + 1).ToString("00", CultureInfo.InvariantCulture);
    var rawPath = Path.Combine(rawFolder, $"{prefix}-{safeName}.jpg");
    await File.WriteAllBytesAsync(rawPath, frame.JpegBytes);

    var card = RenderCard(theme.DisplayName, Decode(frame.JpegBytes), selectedFont.FontFamily);
    var cardPath = Path.Combine(cardsFolder, $"{prefix}-{safeName}.png");
    SavePng(card, cardPath);
    cards.Add((theme, card));
    manifest.AppendLine($"{prefix}. {theme.DisplayName}  [{theme.Id}]");
    Console.WriteLine($"EXPORTED {prefix} {theme.DisplayName}");
}

var contactSheetPath = Path.Combine(outputRoot, "Linx68ScreenDriver-全主题总览.png");
SavePng(RenderContactSheet(cards), contactSheetPath);
await File.WriteAllTextAsync(Path.Combine(outputRoot, "主题清单.txt"), manifest.ToString(), new UTF8Encoding(false));

var zipPath = outputRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + ".zip";
if (File.Exists(zipPath)) File.Delete(zipPath);
ZipFile.CreateFromDirectory(outputRoot, zipPath, CompressionLevel.Optimal, includeBaseDirectory: false);

Console.WriteLine($"PREVIEW_ROOT={outputRoot}");
Console.WriteLine($"CONTACT_SHEET={contactSheetPath}");
Console.WriteLine($"ZIP={zipPath}");
return 0;

static BitmapSource RenderCard(string title, BitmapSource screen, FontFamily fontFamily)
{
    const int cardWidth = 236;
    const int cardHeight = 536;
    const double previewWidth = 154;
    const double previewHeight = 440;
    const double frameWidth = 6;
    const double outerRadius = 26;
    const double innerRadius = 20;
    const double firmwareHorizontalInset = 7;

    var visual = new DrawingVisual();
    using (var drawing = visual.RenderOpen())
    {
        var cardBounds = new Rect(0.5, 0.5, cardWidth - 1, cardHeight - 1);
        drawing.DrawRoundedRectangle(
            new SolidColorBrush(Color.FromRgb(247, 248, 250)),
            new Pen(new SolidColorBrush(Color.FromRgb(225, 228, 232)), 1),
            cardBounds,
            22,
            22);

        DrawInkCenteredText(drawing, title, 17, FontWeights.SemiBold,
            new SolidColorBrush(Color.FromRgb(20, 22, 25)),
            fontFamily,
            new Rect(20, 19, cardWidth - 40, 25),
            TextAlignment.Left);

        var outer = new Rect((cardWidth - previewWidth) / 2, 72, previewWidth, previewHeight);
        var inner = new Rect(outer.X + frameWidth, outer.Y + frameWidth,
            previewWidth - frameWidth * 2, previewHeight - frameWidth * 2);
        drawing.DrawRoundedRectangle(
            new SolidColorBrush(Color.FromRgb(211, 215, 220)),
            null,
            outer,
            outerRadius,
            outerRadius);

        drawing.PushClip(new RectangleGeometry(inner, innerRadius, innerRadius));
        drawing.DrawImage(screen, inner);
        var scaleX = inner.Width / 142.0;
        var scaleY = inner.Height / 428.0;
        var overlay = new SolidColorBrush(Color.FromArgb(246, 6, 10, 16));
        drawing.DrawEllipse(overlay, null,
            new Point(inner.X + (firmwareHorizontalInset + 18) * scaleX, inner.Y + 23 * scaleY),
            18 * scaleX,
            18 * scaleY);
        drawing.DrawRoundedRectangle(
            overlay,
            null,
            new Rect(inner.Right - (firmwareHorizontalInset + 77) * scaleX,
                inner.Y + 5 * scaleY,
                77 * scaleX,
                36 * scaleY),
            18 * scaleX,
            18 * scaleY);
        drawing.Pop();
    }

    return RenderVisual(visual, cardWidth, cardHeight);
}

static BitmapSource RenderContactSheet(IReadOnlyList<(IScreenTheme Theme, BitmapSource Image)> cards)
{
	// Fifteen current themes form a balanced 5x3 wall instead of leaving a single
	// orphan card on a third row.
	const int columns = 5;
    const int cardWidth = 236;
    const int cardHeight = 536;
    const int gap = 16;
    const int margin = 24;
    var rows = (int)Math.Ceiling(cards.Count / (double)columns);
    var width = margin * 2 + columns * cardWidth + (columns - 1) * gap;
    var height = margin * 2 + rows * cardHeight + (rows - 1) * gap;

    var visual = new DrawingVisual();
    using (var drawing = visual.RenderOpen())
    {
        drawing.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));
        for (var index = 0; index < cards.Count; index++)
        {
            var column = index % columns;
            var row = index / columns;
            var x = margin + column * (cardWidth + gap);
            var y = margin + row * (cardHeight + gap);
            drawing.DrawImage(cards[index].Image, new Rect(x, y, cardWidth, cardHeight));
        }
    }

    return RenderVisual(visual, width, height);
}

static void DrawInkCenteredText(DrawingContext drawing, string text, double size,
    FontWeight weight, Brush brush, FontFamily fontFamily, Rect bounds, TextAlignment alignment)
{
    var formatted = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
        new Typeface(fontFamily, FontStyles.Normal, weight, FontStretches.Normal),
        size, brush, 1.0);
    var ink = formatted.BuildGeometry(new Point()).Bounds;
    if (ink.IsEmpty) return;
    var x = alignment == TextAlignment.Right
        ? bounds.Right - ink.Right
        : alignment == TextAlignment.Center
            ? bounds.Left + (bounds.Width - ink.Width) / 2 - ink.Left
            : bounds.Left - ink.Left;
    var y = bounds.Top + (bounds.Height - ink.Height) / 2 - ink.Top;
    drawing.DrawText(formatted, new Point(x, y));
}

static BitmapSource RenderVisual(DrawingVisual visual, int width, int height)
{
    var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
    bitmap.Render(visual);
    bitmap.Freeze();
    return bitmap;
}

static BitmapSource Decode(byte[] bytes)
{
    using var stream = new MemoryStream(bytes);
    var image = BitmapFrame.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
    image.Freeze();
    return image;
}

static void SavePng(BitmapSource bitmap, string path)
{
    var encoder = new PngBitmapEncoder();
    encoder.Frames.Add(BitmapFrame.Create(bitmap));
    using var stream = File.Create(path);
    encoder.Save(stream);
}

static Color ParseColor(string? value, Color fallback)
{
    if (string.IsNullOrWhiteSpace(value)) return fallback;
    var hex = value.Trim();
    if (hex.StartsWith('#')) hex = hex[1..];
    if (hex.Length != 6) return fallback;
    return byte.TryParse(hex[0..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var red)
        && byte.TryParse(hex[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var green)
        && byte.TryParse(hex[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var blue)
        ? Color.FromRgb(red, green, blue)
        : fallback;
}

static byte[]? ReadOptionalImage(string? path)
{
    try
    {
        return !string.IsNullOrWhiteSpace(path) && File.Exists(path) ? File.ReadAllBytes(path) : null;
    }
    catch
    {
        return null;
    }
}

static string SanitizeFileName(string name)
{
    var invalid = Path.GetInvalidFileNameChars();
    var sanitized = new string(name.Select(character => invalid.Contains(character) ? '-' : character).ToArray());
    return string.IsNullOrWhiteSpace(sanitized) ? "未命名主题" : sanitized;
}
