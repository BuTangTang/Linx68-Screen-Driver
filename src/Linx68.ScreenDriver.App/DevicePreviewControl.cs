using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Point = System.Windows.Point;

namespace Linx68.ScreenDriver.App;

public sealed class DevicePreviewControl : FrameworkElement
{
	private const double PreviewWidth = 154.0;

	private const double PreviewHeight = 440.0;

	private const double FrameWidth = 6.0;

	private const double OuterRadius = 26.0;

	private const double InnerRadius = 20.0;

	private const double FirmwareHorizontalInset = 7.0;

	public static readonly DependencyProperty FrameSourceProperty = DependencyProperty.Register("FrameSource", typeof(ImageSource), typeof(DevicePreviewControl), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

	public ImageSource? FrameSource
	{
		get
		{
			return (ImageSource)GetValue(FrameSourceProperty);
		}
		set
		{
			SetValue(FrameSourceProperty, value);
		}
	}

	public DevicePreviewControl()
	{
		base.Width = 154.0;
		base.Height = 440.0;
		base.UseLayoutRounding = false;
		base.SnapsToDevicePixels = false;
		RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
		RenderOptions.SetEdgeMode(this, EdgeMode.Unspecified);
	}

	protected override void OnRender(DrawingContext drawingContext)
	{
		base.OnRender(drawingContext);
		Brush frameBrush = (Brush)FindResource("PreviewFrameBrush");
		Brush emptyScreenBrush = (Brush)FindResource("Surface.Screen");
		Brush firmwareOverlayBrush = (Brush)FindResource("PreviewOverlayBrush");
		Rect rectangle = new Rect(0.0, 0.0, base.ActualWidth, base.ActualHeight);
		Rect rect = new Rect(6.0, 6.0, Math.Max(0.0, base.ActualWidth - 12.0), Math.Max(0.0, base.ActualHeight - 12.0));
		drawingContext.DrawRoundedRectangle(frameBrush, null, rectangle, 26.0, 26.0);
		RectangleGeometry clipGeometry = new RectangleGeometry(rect, 20.0, 20.0);
		drawingContext.PushClip(clipGeometry);
		double horizontalScale = rect.Width / 142.0;
		double verticalScale = rect.Height / 428.0;
		if (FrameSource == null)
		{
			drawingContext.DrawRectangle(emptyScreenBrush, null, rect);
			DrawPlaceholder(drawingContext, rect, Math.Min(horizontalScale, verticalScale));
		}
		else
		{
			drawingContext.DrawImage(FrameSource, rect);
		}
		drawingContext.DrawEllipse(firmwareOverlayBrush, null, new Point(rect.X + (FirmwareHorizontalInset + 18.0) * horizontalScale, rect.Y + 23.0 * verticalScale), 18.0 * horizontalScale, 18.0 * verticalScale);
		drawingContext.DrawRoundedRectangle(rectangle: new Rect(rect.Right - (FirmwareHorizontalInset + 77.0) * horizontalScale, rect.Y + 5.0 * verticalScale, 77.0 * horizontalScale, 36.0 * verticalScale), brush: firmwareOverlayBrush, pen: null, radiusX: 18.0 * horizontalScale, radiusY: 18.0 * verticalScale);
		drawingContext.Pop();
	}

	private void DrawPlaceholder(DrawingContext drawingContext, Rect bounds, double scale)
	{
		var typeface = new Typeface((System.Windows.Media.FontFamily)FindResource("UiFontFamily"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
		var text = new FormattedText(
			"等待预览数据",
			CultureInfo.CurrentUICulture,
			System.Windows.FlowDirection.LeftToRight,
			typeface,
			Math.Max(7, 9 * scale),
			new SolidColorBrush(System.Windows.Media.Color.FromRgb(148, 160, 174)),
			VisualTreeHelper.GetDpi(this).PixelsPerDip);
		drawingContext.DrawText(text, new Point(
			bounds.Left + (bounds.Width - text.Width) / 2,
			bounds.Top + (bounds.Height - text.Height) / 2));
	}
}
