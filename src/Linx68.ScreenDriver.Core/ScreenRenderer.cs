using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Linx68.ScreenDriver.Core;

public sealed class ScreenRenderer
{
	public ScreenProfile Profile { get; }

	public ScreenRenderer(ScreenProfile? profile = null)
	{
		Profile = profile ?? ScreenProfile.KeyboardDisplay;
	}

	public RenderedFrame Render(IScreenTheme theme, SystemSnapshot snapshot, int jpegQuality = 100, Color? accentColor = null, FontFamily? fontFamily = null, ScreenDisplayOptions? displayOptions = null)
	{
		ArgumentNullException.ThrowIfNull(theme, "theme");
		DrawingVisual drawingVisual = new DrawingVisual();
		using (DrawingContext drawing = drawingVisual.RenderOpen())
		{
			theme.Draw(new ScreenCanvas(drawing, Profile, accentColor ?? Color.FromRgb(77, 226, 174), fontFamily ?? ScreenFontOption.Default.FontFamily, displayOptions ?? ScreenDisplayOptions.Default), snapshot);
		}
		RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap(Profile.Width, Profile.Height, 96.0, 96.0, PixelFormats.Pbgra32);
		renderTargetBitmap.Render(drawingVisual);
		JpegBitmapEncoder jpegBitmapEncoder = new JpegBitmapEncoder
		{
			QualityLevel = Math.Clamp(jpegQuality, 1, 100)
		};
		jpegBitmapEncoder.Frames.Add(BitmapFrame.Create(renderTargetBitmap));
		using MemoryStream memoryStream = new MemoryStream();
		jpegBitmapEncoder.Save(memoryStream);
		byte[] array = memoryStream.ToArray();
		if (array.Length > Profile.MaxJpegBytes)
		{
			throw new InvalidOperationException($"Rendered JPEG is {array.Length} bytes, exceeding the {Profile.MaxJpegBytes} byte device limit.");
		}
		return new RenderedFrame(array, Profile.Width, Profile.Height, theme.Id, DateTimeOffset.Now);
	}
}
