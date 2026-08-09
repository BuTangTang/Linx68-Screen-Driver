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
		_drawing.DrawRectangle(new SolidColorBrush(ResolveColor(color)), null, new Rect(0.0, 0.0, Profile.Width, Profile.Height));
	}

	public void Gradient(Color start, Color end, Point startPoint, Point endPoint)
	{
		LinearGradientBrush brush = new LinearGradientBrush(ResolveColor(start), ResolveColor(end), startPoint, endPoint);
		_drawing.DrawRectangle(brush, null, new Rect(0.0, 0.0, Profile.Width, Profile.Height));
	}

	public void RoundedRect(Rect rect, double radius, Color fill, Color? stroke = null, double strokeWidth = 1.0)
	{
		Pen? pen = stroke.HasValue ? new Pen(new SolidColorBrush(ResolveColor(stroke.Value)), strokeWidth) : null;
		_drawing.DrawRoundedRectangle(new SolidColorBrush(ResolveColor(fill)), pen, rect, radius, radius);
	}

	public void RoundedGradientRect(Rect rect, double radius, Color start, Color end, Color? stroke = null, double strokeWidth = 1.0)
	{
		Pen? pen = stroke.HasValue ? new Pen(new SolidColorBrush(ResolveColor(stroke.Value)), strokeWidth) : null;
		LinearGradientBrush brush = new LinearGradientBrush(ResolveColor(start), ResolveColor(end), new Point(0, 0), new Point(1, 1));
		_drawing.DrawRoundedRectangle(brush, pen, rect, radius, radius);
	}

	public void Ellipse(Rect rect, Color fill, Color? stroke = null, double strokeWidth = 1.0)
	{
		Pen? pen = stroke.HasValue ? new Pen(new SolidColorBrush(ResolveColor(stroke.Value)), strokeWidth) : null;
		_drawing.DrawEllipse(new SolidColorBrush(ResolveColor(fill)), pen, new Point(rect.X + rect.Width / 2.0, rect.Y + rect.Height / 2.0), rect.Width / 2.0, rect.Height / 2.0);
	}

	public void Text(string value, double size, Color color, Point origin, FontWeight? weight = null, TextAlignment alignment = TextAlignment.Left, double maxWidth = double.PositiveInfinity, double maxHeight = double.PositiveInfinity)
	{
		FormattedText formattedText = new FormattedText(value, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(FontFamily, FontStyles.Normal, weight ?? FontWeights.Normal, FontStretches.Normal), size, new SolidColorBrush(ResolveColor(color)), 1.0);
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

	public void FittedText(string value, double preferredSize, double minimumSize, Color color, Point origin, FontWeight? weight = null, TextAlignment alignment = TextAlignment.Left, double maxWidth = double.PositiveInfinity, double maxHeight = double.PositiveInfinity)
	{
		double size = preferredSize;
		if (!double.IsPositiveInfinity(maxWidth))
		{
			while (size > minimumSize)
			{
				FormattedText formattedText = new FormattedText(value, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(FontFamily, FontStyles.Normal, weight ?? FontWeights.Normal, FontStretches.Normal), size, Brushes.Transparent, 1.0);
				if (formattedText.WidthIncludingTrailingWhitespace <= maxWidth)
				{
					break;
				}

				size -= 0.5;
			}
		}

		Text(value, Math.Max(minimumSize, size), color, origin, weight, alignment, maxWidth, maxHeight);
	}

	public void WrappedText(string value, double size, Color color, Point origin, FontWeight? weight, TextAlignment alignment, double maxWidth, int maxLines, double lineHeight)
	{
		if (string.IsNullOrEmpty(value) || maxWidth <= 0 || maxLines <= 0)
		{
			return;
		}

		List<string> lines = new();
		int start = 0;
		while (start < value.Length && lines.Count < maxLines)
		{
			int end = FindLineBreak(value, start, size, weight, maxWidth);
			bool isLastVisibleLine = lines.Count == maxLines - 1;
			if (isLastVisibleLine && end < value.Length)
			{
				lines.Add(Ellipsize(value[start..], size, weight, maxWidth));
				break;
			}

			lines.Add(value[start..end]);
			start = end;
		}

		for (int index = 0; index < lines.Count; index++)
		{
			Text(lines[index], size, color, new Point(origin.X, origin.Y + index * lineHeight), weight, alignment, maxWidth);
		}
	}

	public void CenteredText(string value, double size, Color color, Rect bounds, FontWeight? weight = null)
	{
		AlignedText(value, size, color, bounds, weight, TextAlignment.Center);
	}

	public void AlignedText(string value, double size, Color color, Rect bounds, FontWeight? weight = null, TextAlignment alignment = TextAlignment.Left, FontFamily? fontFamily = null)
	{
		FormattedText formattedText = new FormattedText(value, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(fontFamily ?? FontFamily, FontStyles.Normal, weight ?? FontWeights.Normal, FontStretches.Normal), size, new SolidColorBrush(ResolveColor(color)), 1.0);
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
		_drawing.DrawLine(new Pen(new SolidColorBrush(ResolveColor(color)), thickness), start, end);
	}

	public void Path(Geometry geometry, Color stroke, double thickness = 1.0, Color? fill = null)
	{
		ArgumentNullException.ThrowIfNull(geometry);
		Brush? fillBrush = fill.HasValue ? new SolidColorBrush(ResolveColor(fill.Value)) : null;
		_drawing.DrawGeometry(fillBrush, new Pen(new SolidColorBrush(ResolveColor(stroke)), thickness), geometry);
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

	public bool TryImage(byte[] bytes, Rect rect, double radius = 0.0)
	{
		try
		{
			Image(bytes, rect, radius);
			return true;
		}
		catch (Exception)
		{
			return false;
		}
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

	private int FindLineBreak(string value, int start, double size, FontWeight? weight, double maxWidth)
	{
		int low = start + 1;
		int high = value.Length;
		int best = start + 1;
		while (low <= high)
		{
			int middle = low + (high - low) / 2;
			FormattedText formattedText = new FormattedText(value[start..middle], CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
				new Typeface(FontFamily, FontStyles.Normal, weight ?? FontWeights.Normal, FontStretches.Normal), size, Brushes.Transparent, 1.0);
			if (formattedText.WidthIncludingTrailingWhitespace <= maxWidth)
			{
				best = middle;
				low = middle + 1;
			}
			else
			{
				high = middle - 1;
			}
		}

		return best;
	}

	private string Ellipsize(string value, double size, FontWeight? weight, double maxWidth)
	{
		const string ellipsis = "…";
		if (MeasureWidth(value, size, weight) <= maxWidth)
		{
			return value;
		}

		int low = 0;
		int high = value.Length;
		int best = 0;
		while (low <= high)
		{
			int middle = low + (high - low) / 2;
			if (MeasureWidth(value[..middle] + ellipsis, size, weight) <= maxWidth)
			{
				best = middle;
				low = middle + 1;
			}
			else
			{
				high = middle - 1;
			}
		}

		return value[..best] + ellipsis;
	}

	private double MeasureWidth(string value, double size, FontWeight? weight)
	{
		FormattedText formattedText = new FormattedText(value, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
			new Typeface(FontFamily, FontStyles.Normal, weight ?? FontWeights.Normal, FontStretches.Normal), size, Brushes.Transparent, 1.0);
		return formattedText.WidthIncludingTrailingWhitespace;
	}

	private Color ResolveColor(Color color)
	{
		if (DisplayOptions.ColorMode != ScreenColorMode.Daylight || color.A == 0)
		{
			return color;
		}

		if (IsAccent(color))
		{
			return color;
		}

		double luminance = (0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B) / 255.0;
		Color mapped = luminance switch
		{
			<= 0.045 => Color.FromRgb(248, 247, 243),
			<= 0.10 => Colors.White,
			<= 0.22 => Color.FromRgb(220, 226, 232),
			<= 0.58 => Color.FromRgb(112, 118, 125),
			_ => Color.FromRgb(23, 25, 28)
		};

		return Color.FromArgb(color.A, mapped.R, mapped.G, mapped.B);
	}

	private bool IsAccent(Color color)
	{
		return Math.Abs(color.R - AccentColor.R) <= 3
			&& Math.Abs(color.G - AccentColor.G) <= 3
			&& Math.Abs(color.B - AccentColor.B) <= 3;
	}
}
