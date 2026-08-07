using System.IO;
using System.Windows;
using System.Windows.Media;

namespace Linx68.ScreenDriver.Core;

public sealed class DotMatrixWeatherClockTheme : IScreenTheme
{
    private static readonly FontFamily Doto = new(
        new Uri(Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts") + Path.DirectorySeparatorChar, UriKind.Absolute),
        "./#Doto");

    public string Id => "clock-weather-dot";
    public string DisplayName => "点阵时钟天气";
    public string Description => "点阵时间与当前天气信息";
    public string Details => "纵向展示时间、温度、天气状态、体感温度和湿度。";

    public void Draw(ScreenCanvas canvas, SystemSnapshot snapshot)
    {
        var safe = canvas.SafeBounds;
        var weather = snapshot.Weather;
        var primary = Colors.White;
        var secondary = Color.FromRgb(153, 153, 153);
        var disabled = Color.FromRgb(102, 102, 102);
        var border = Color.FromRgb(34, 34, 34);

        canvas.Fill(Colors.Black);
        canvas.AlignedText(weather?.LocationName ?? "天气", 11, secondary,
            new Rect(safe.Left, safe.Top + 7, safe.Width, 18), FontWeights.Medium, TextAlignment.Left);
        DrawDotMatrixTime(canvas, new Rect(safe.Left, safe.Top + 33, safe.Width, 54),
            snapshot.Timestamp, primary);

        if (weather is not { Available: true })
        {
            canvas.AlignedText("--°", 43, primary,
                new Rect(safe.Left, safe.Top + 123, safe.Width, 58), FontWeights.Medium, TextAlignment.Left);
            canvas.AlignedText("等待天气数据", 12, secondary,
                new Rect(safe.Left, safe.Top + 190, safe.Width, 20), FontWeights.Medium, TextAlignment.Left);
            canvas.AlignedText("请在屏幕配置中填写城市", 10, disabled,
                new Rect(safe.Left, safe.Bottom - 42, safe.Width, 18), FontWeights.Normal, TextAlignment.Left);
            return;
        }

        canvas.AlignedText($"{weather.TemperatureC:0}°", 43, primary,
            new Rect(safe.Left, safe.Top + 116, safe.Width - 42, 60), FontWeights.Medium, TextAlignment.Left);
        DrawWeatherIcon(canvas, WeatherCondition.IconFromCode(weather.WeatherCode),
            new Rect(safe.Right - 38, safe.Top + 126, 36, 36), primary, canvas.AccentColor);
        canvas.AlignedText(weather.ConditionText, 15, canvas.AccentColor,
            new Rect(safe.Left, safe.Top + 182, safe.Width, 24), FontWeights.SemiBold, TextAlignment.Left);

        canvas.Line(new Point(safe.Left, safe.Top + 228), new Point(safe.Right, safe.Top + 228), border);
        DrawStatRow(canvas, safe, safe.Top + 248, "体感", $"{weather.ApparentTemperatureC:0}°", secondary, primary);
        DrawStatRow(canvas, safe, safe.Top + 291, "湿度", $"{weather.RelativeHumidityPercent}%", secondary, primary);

        var updateText = weather.IsStale ? "上次天气数据" : $"更新 {weather.UpdatedAt:HH:mm}";
        canvas.AlignedText(updateText, 9, disabled,
            new Rect(safe.Left, safe.Bottom - 24, safe.Width, 15), FontWeights.Normal, TextAlignment.Left);
    }

    private static void DrawDotMatrixTime(ScreenCanvas canvas, Rect bounds,
        DateTimeOffset timestamp, Color color)
    {
        const double colonWidth = 14;
        const double dotDiameter = 3.4;
        const double dotOffset = 6;
        var numberWidth = (bounds.Width - colonWidth) / 2;

        canvas.AlignedText(timestamp.ToString("HH"), 39, color,
            new Rect(bounds.Left, bounds.Top, numberWidth, bounds.Height),
            FontWeights.Medium, TextAlignment.Left, Doto);
        canvas.AlignedText(timestamp.ToString("mm"), 39, color,
            new Rect(bounds.Right - numberWidth, bounds.Top, numberWidth, bounds.Height),
            FontWeights.Medium, TextAlignment.Right, Doto);

        var centerX = bounds.Left + (bounds.Width / 2);
        var centerY = bounds.Top + (bounds.Height / 2);
        var radius = dotDiameter / 2;
        canvas.Ellipse(new Rect(centerX - radius, centerY - dotOffset - radius,
            dotDiameter, dotDiameter), color);
        canvas.Ellipse(new Rect(centerX - radius, centerY + dotOffset - radius,
            dotDiameter, dotDiameter), color);
    }
    private static void DrawStatRow(ScreenCanvas canvas, Rect safe, double top,
        string label, string value, Color labelColor, Color valueColor)
    {
        canvas.AlignedText(label, 11, labelColor,
            new Rect(safe.Left, top, safe.Width, 22), FontWeights.Medium, TextAlignment.Left);
        canvas.AlignedText(value, 19, valueColor,
            new Rect(safe.Left, top - 2, safe.Width, 25), FontWeights.Medium, TextAlignment.Right);
    }

    internal static void DrawWeatherIcon(ScreenCanvas canvas, WeatherIconKind kind,
        Rect bounds, Color color, Color accent)
    {
        if (kind == WeatherIconKind.Clear)
        {
            DrawSun(canvas, bounds, color);
            return;
        }
        if (kind == WeatherIconKind.Fog)
        {
            for (var index = 0; index < 3; index++)
            {
                var y = bounds.Top + bounds.Height * (0.30 + index * 0.20);
                var inset = bounds.Width * (index == 1 ? 0.18 : 0.08);
                canvas.Line(new Point(bounds.Left + inset, y), new Point(bounds.Right - inset, y), color, 1.8);
            }
            return;
        }
        if (kind == WeatherIconKind.PartlyCloudy)
        {
            DrawSun(canvas, new Rect(bounds.Left + bounds.Width * 0.48, bounds.Top,
                bounds.Width * 0.46, bounds.Height * 0.46), Color.FromRgb(153, 153, 153));
        }

        var cloudBounds = kind is WeatherIconKind.Cloudy or WeatherIconKind.PartlyCloudy
            ? new Rect(bounds.Left, bounds.Top + bounds.Height * 0.16, bounds.Width, bounds.Height * 0.82)
            : bounds;
        canvas.Path(CreateCloudGeometry(cloudBounds), color, 1.8);
        if (kind == WeatherIconKind.Rain)
        {
            DrawRainDrop(canvas, bounds, 0.30, accent);
            DrawRainDrop(canvas, bounds, 0.52, accent);
            DrawRainDrop(canvas, bounds, 0.74, accent);
        }
        else if (kind == WeatherIconKind.Snow)
        {
            var radius = Math.Min(bounds.Width, bounds.Height) * 0.08;
            DrawSnowMark(canvas, new Point(bounds.Left + bounds.Width * 0.36, bounds.Top + bounds.Height * 0.82), accent, radius);
            DrawSnowMark(canvas, new Point(bounds.Left + bounds.Width * 0.68, bounds.Top + bounds.Height * 0.82), accent, radius);
        }
        else if (kind == WeatherIconKind.Thunderstorm)
        {
            canvas.Line(new Point(bounds.Left + bounds.Width * 0.58, bounds.Top + bounds.Height * 0.66),
                new Point(bounds.Left + bounds.Width * 0.43, bounds.Top + bounds.Height * 0.82), accent, 2.2);
            canvas.Line(new Point(bounds.Left + bounds.Width * 0.43, bounds.Top + bounds.Height * 0.82),
                new Point(bounds.Left + bounds.Width * 0.60, bounds.Top + bounds.Height * 0.82), accent, 2.2);
            canvas.Line(new Point(bounds.Left + bounds.Width * 0.60, bounds.Top + bounds.Height * 0.82),
                new Point(bounds.Left + bounds.Width * 0.47, bounds.Top + bounds.Height * 0.96), accent, 2.2);
        }
    }
    private static void DrawSun(ScreenCanvas canvas, Rect bounds, Color color)
    {
        var size = Math.Min(bounds.Width, bounds.Height);
        var center = new Point(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);
        var radius = size * 0.22;
        canvas.Ellipse(new Rect(center.X - radius, center.Y - radius, radius * 2, radius * 2), Colors.Black, color, 1.8);
        for (var index = 0; index < 8; index++)
        {
            var angle = index * Math.PI / 4;
            var inner = radius + 3;
            var outer = radius + 6;
            canvas.Line(new Point(center.X + Math.Cos(angle) * inner, center.Y + Math.Sin(angle) * inner),
                new Point(center.X + Math.Cos(angle) * outer, center.Y + Math.Sin(angle) * outer), color, 1.5);
        }
    }

    private static Geometry CreateCloudGeometry(Rect bounds)
    {
        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        context.BeginFigure(new Point(bounds.Left + bounds.Width * 0.18, bounds.Top + bounds.Height * 0.62), false, false);
        context.BezierTo(
            new Point(bounds.Left + bounds.Width * 0.04, bounds.Top + bounds.Height * 0.62),
            new Point(bounds.Left + bounds.Width * 0.04, bounds.Top + bounds.Height * 0.38),
            new Point(bounds.Left + bounds.Width * 0.22, bounds.Top + bounds.Height * 0.36), true, false);
        context.BezierTo(
            new Point(bounds.Left + bounds.Width * 0.27, bounds.Top + bounds.Height * 0.12),
            new Point(bounds.Left + bounds.Width * 0.63, bounds.Top + bounds.Height * 0.10),
            new Point(bounds.Left + bounds.Width * 0.70, bounds.Top + bounds.Height * 0.35), true, false);
        context.BezierTo(
            new Point(bounds.Left + bounds.Width * 0.92, bounds.Top + bounds.Height * 0.35),
            new Point(bounds.Left + bounds.Width * 0.96, bounds.Top + bounds.Height * 0.62),
            new Point(bounds.Left + bounds.Width * 0.78, bounds.Top + bounds.Height * 0.62), true, false);
        context.LineTo(new Point(bounds.Left + bounds.Width * 0.18, bounds.Top + bounds.Height * 0.62), true, false);
        geometry.Freeze();
        return geometry;
    }

    private static void DrawRainDrop(ScreenCanvas canvas, Rect bounds, double xFactor, Color color)
    {
        canvas.Line(
            new Point(bounds.Left + bounds.Width * xFactor, bounds.Top + bounds.Height * 0.72),
            new Point(bounds.Left + bounds.Width * (xFactor - 0.07), bounds.Top + bounds.Height * 0.92), color, 2);
    }

    private static void DrawSnowMark(ScreenCanvas canvas, Point center, Color color, double radius)
    {
        canvas.Line(new Point(center.X - radius, center.Y), new Point(center.X + radius, center.Y), color, 1.5);
        canvas.Line(new Point(center.X, center.Y - radius), new Point(center.X, center.Y + radius), color, 1.5);
    }
}