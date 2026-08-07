using System.IO;
using System.Windows;
using System.Windows.Media;

namespace Linx68.ScreenDriver.Core;

public sealed class ImageTheme : IScreenTheme
{
    public string Id => "image";
    public string DisplayName => "图片时间";
    public string Description => "图片背景叠加时间与中文日期";
    public string Details => "图片按 142×428 居中裁切；时间信息可选择显示在上方或下方。";
    public string? ImagePath { get; set; }

    public void Draw(ScreenCanvas canvas, SystemSnapshot snapshot)
    {
        canvas.Fill(Color.FromRgb(8, 10, 14));
        var hasImage = false;
        if (!string.IsNullOrWhiteSpace(ImagePath) && File.Exists(ImagePath))
        {
            try
            {
                canvas.Image(File.ReadAllBytes(ImagePath), new Rect(0, 0, canvas.Profile.Width, canvas.Profile.Height));
                hasImage = true;
            }
            catch
            {
                hasImage = false;
            }
        }

        if (!hasImage)
        {
            DrawPlaceholder(canvas);
            return;
        }

        DrawTimeOverlay(canvas, snapshot);
    }

    private static void DrawPlaceholder(ScreenCanvas canvas)
    {
        Rect safe = canvas.SafeBounds;
        var card = new Rect(safe.Left, 126, safe.Width, 162);
        canvas.RoundedRect(card, 12, Color.FromRgb(17, 21, 27), Color.FromRgb(48, 57, 67));
        canvas.Text("图片时间", 15, canvas.AccentColor, new Point(card.Left, card.Top + 48),
            FontWeights.SemiBold, TextAlignment.Center, card.Width);
        canvas.Text("请选择一张图片", 14, Colors.White, new Point(card.Left, card.Top + 88),
            FontWeights.SemiBold, TextAlignment.Center, card.Width);
    }

    private static void DrawTimeOverlay(ScreenCanvas canvas, SystemSnapshot snapshot)
    {
        Rect safe = canvas.SafeBounds;
        var y = canvas.DisplayOptions.ImageTimePlacement == ImageTimePlacement.Top
            ? safe.Top + 12
            : safe.Bottom - 82;
        var card = new Rect(safe.Left, y, safe.Width, 70);
        canvas.RoundedRect(card, 11, Color.FromArgb(190, 5, 8, 12), Color.FromArgb(80, 255, 255, 255));
        canvas.CenteredText(snapshot.Timestamp.ToString("HH:mm"), 27, Colors.White,
            new Rect(card.Left + 8, card.Top + 7, card.Width - 16, 34), FontWeights.SemiBold);
        canvas.CenteredText($"{snapshot.Timestamp.Month}月{snapshot.Timestamp.Day}日  {snapshot.Timestamp:dddd}", 11,
            Color.FromRgb(215, 222, 230), new Rect(card.Left + 8, card.Top + 44, card.Width - 16, 17),
            FontWeights.Medium);
    }
}