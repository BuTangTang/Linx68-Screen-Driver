using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Linx68.ScreenDriver.Core;

public sealed class ScreenCanvas
{
	private readonly DrawingContext _drawing;

	public ScreenProfile Profile { get; }

	public Color AccentColor { get; }

	public FontFamily FontFamily { get; }

	public ScreenDisplayOptions DisplayOptions { get; }

	public Rect SafeBounds => new Rect(Profile.SafeArea.Left, Profile.SafeArea.Top, Profile.Width - Profile.SafeArea.Left - Profile.SafeArea.Right, Profile.Height - Profile.SafeArea.Top - Profile.SafeArea.Bottom);

	internal ScreenCanvas(DrawingContext drawing, ScreenProfile profile, Color accentColor, FontFamily fontFamily, ScreenDisplayOptions displayOptions)
	{
		_drawing = drawing;
		Profile = profile;
		AccentColor = accentColor;
		FontFamily = fontFamily;
		DisplayOptions = displayOptions;
	}

	public void Fill(Color color)
	{
		_drawing.DrawRectangle(new SolidColorBrush(color), null, new Rect(0.0, 0.0, Profile.Width, Profile.Height));
	}

	public void Gradient(Color start, Color end, Point startPoint, Point endPoint)
	{
		LinearGradientBrush brush = new LinearGradientBrush(start, end, startPoint, endPoint);
		_drawing.DrawRectangle(brush, null, new Rect(0.0, 0.0, Profile.Width, Profile.Height));
	}

	public void RoundedRect(Rect rect, double radius, Color fill, Color? stroke = null, double strokeWidth = 1.0)
	{
		Pen? pen = stroke.HasValue ? new Pen(new SolidColorBrush(stroke.Value), strokeWidth) : null;
		_drawing.DrawRoundedRectangle(new SolidColorBrush(fill), pen, rect, radius, radius);
	}

	public void Ellipse(Rect rect, Color fill, Color? stroke = null, double strokeWidth = 1.0)
	{
		Pen? pen = stroke.HasValue ? new Pen(new SolidColorBrush(stroke.Value), strokeWidth) : null;
		_drawing.DrawEllipse(new SolidColorBrush(fill), pen, new Point(rect.X + rect.Width / 2.0, rect.Y + rect.Height / 2.0), rect.Width / 2.0, rect.Height / 2.0);
	}

	public void Text(string value, double size, Color color, Point origin, FontWeight? weight = null, TextAlignment alignment = TextAlignment.Left, double maxWidth = double.PositiveInfinity, double maxHeight = double.PositiveInfinity)
	{
		FormattedText formattedText = new FormattedText(value, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(FontFamily, FontStyles.Normal, weight ?? FontWeights.Normal, FontStretches.Normal), size, new SolidColorBrush(color), 1.0);
		formattedText.TextAlignment = alignment;
		if (!double.IsPositiveInfinity(maxWidth))
		{
			formattedText.MaxTextWidth = maxWidth;
		}
		if (!double.IsPositiveInfinity(maxHeight))
		{
			formattedText.MaxTextHeight = maxHeight;
			formattedText.Trimming = TextTrimming.CharacterEllipsis;
		}
		_drawing.DrawText(formattedText, origin);
	}

	public void CenteredText(string value, double size, Color color, Rect bounds, FontWeight? weight = null)
	{
		AlignedText(value, size, color, bounds, weight, TextAlignment.Center);
	}

	public void AlignedText(string value, double size, Color color, Rect bounds, FontWeight? weight = null, TextAlignment alignment = TextAlignment.Left, FontFamily? fontFamily = null)
	{
		FormattedText formattedText = new FormattedText(value, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(fontFamily ?? FontFamily, FontStyles.Normal, weight ?? FontWeights.Normal, FontStretches.Normal), size, new SolidColorBrush(color), 1.0);
		Geometry glyphs = formattedText.BuildGeometry(new Point());
		Rect inkBounds = glyphs.Bounds;
		if (inkBounds.IsEmpty)
		{
			return;
		}

		double x = alignment switch
		{
			TextAlignment.Right => bounds.Right - inkBounds.Right,
			TextAlignment.Center => bounds.Left + (bounds.Width - inkBounds.Width) / 2.0 - inkBounds.Left,
			_ => bounds.Left - inkBounds.Left
		};
		double y = bounds.Top + (bounds.Height - inkBounds.Height) / 2.0 - inkBounds.Top;
		_drawing.DrawText(formattedText, new Point(x, y));
	}
	public void Line(Point start, Point end, Color color, double thickness = 1.0)
	{
		_drawing.DrawLine(new Pen(new SolidColorBrush(color), thickness), start, end);
	}

	public void Path(Geometry geometry, Color stroke, double thickness = 1.0, Color? fill = null)
	{
		ArgumentNullException.ThrowIfNull(geometry);
		Brush? fillBrush = fill.HasValue ? new SolidColorBrush(fill.Value) : null;
		_drawing.DrawGeometry(fillBrush, new Pen(new SolidColorBrush(stroke), thickness), geometry);
	}
	public void ProgressBar(Rect rect, double percent, Color track, Color fill)
	{
		RoundedRect(rect, rect.Height / 2.0, track);
		double width = Math.Max(rect.Height, rect.Width * Math.Clamp(percent, 0.0, 100.0) / 100.0);
		RoundedRect(new Rect(rect.X, rect.Y, width, rect.Height), rect.Height / 2.0, fill);
	}

	public void Image(byte[] bytes, Rect rect, double radius = 0.0)
	{
		using MemoryStream streamSource = new MemoryStream(bytes);
		BitmapImage bitmapImage = new BitmapImage();
		bitmapImage.BeginInit();
		bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
		bitmapImage.StreamSource = streamSource;
		bitmapImage.EndInit();
		bitmapImage.Freeze();
		ImageBrush brush = new ImageBrush(bitmapImage)
		{
			Stretch = Stretch.UniformToFill,
			AlignmentX = AlignmentX.Center,
			AlignmentY = AlignmentY.Center
		};
		_drawing.DrawRoundedRectangle(brush, null, rect, radius, radius);
	}

	public void CircularImage(byte[] bytes, Rect rect, double rotationDegrees = 0.0)
	{
		using MemoryStream streamSource = new MemoryStream(bytes);
		BitmapImage bitmapImage = new BitmapImage();
		bitmapImage.BeginInit();
		bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
		bitmapImage.StreamSource = streamSource;
		bitmapImage.EndInit();
		bitmapImage.Freeze();
		ImageBrush brush = new ImageBrush(bitmapImage)
		{
			Stretch = Stretch.UniformToFill,
			AlignmentX = AlignmentX.Center,
			AlignmentY = AlignmentY.Center,
			RelativeTransform = new RotateTransform(rotationDegrees, 0.5, 0.5)
		};
		_drawing.DrawEllipse(brush, null, new Point(rect.X + rect.Width / 2.0, rect.Y + rect.Height / 2.0), rect.Width / 2.0, rect.Height / 2.0);
	}
}
