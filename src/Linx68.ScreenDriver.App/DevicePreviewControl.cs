using System;
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
		if (FrameSource == null)
		{
			drawingContext.DrawRectangle(emptyScreenBrush, null, rect);
		}
		else
		{
			drawingContext.DrawImage(FrameSource, rect);
		}
		double num = rect.Width / 142.0;
		double num2 = rect.Height / 428.0;
		drawingContext.DrawEllipse(firmwareOverlayBrush, null, new Point(rect.X + (FirmwareHorizontalInset + 18.0) * num, rect.Y + 23.0 * num2), 18.0 * num, 18.0 * num2);
		drawingContext.DrawRoundedRectangle(rectangle: new Rect(rect.Right - (FirmwareHorizontalInset + 77.0) * num, rect.Y + 5.0 * num2, 77.0 * num, 36.0 * num2), brush: firmwareOverlayBrush, pen: null, radiusX: 18.0 * num, radiusY: 18.0 * num2);
		drawingContext.Pop();
	}
}
