using System.Windows;
using System.Windows.Media;

namespace Linx68.ScreenDriver.Core;

public sealed class StockTheme : IScreenTheme
{
    public string Id => "stocks";
    public string DisplayName => "股票（测试版）";
    public string Description => "最多三项市场行情";
    public string Details => "支持美股、港股、A 股与虚拟币代码，可配置别名和涨跌颜色。";

    public void Draw(ScreenCanvas canvas, SystemSnapshot snapshot)
    {
        var safe = canvas.SafeBounds;
        var stocks = snapshot.Stocks;
        var secondary = Color.FromRgb(150, 160, 172);
        var card = Color.FromRgb(15, 20, 27);
        var stroke = Color.FromRgb(29, 36, 45);

        canvas.Fill(Color.FromRgb(5, 9, 14));
        canvas.AlignedText("股票", 11, secondary,
            new Rect(safe.Left, safe.Top + 7, safe.Width, 18), FontWeights.SemiBold, TextAlignment.Left);
        canvas.AlignedText(snapshot.Timestamp.ToString("HH:mm"), 13, Colors.White,
            new Rect(safe.Left, safe.Top + 5, safe.Width, 20), FontWeights.SemiBold, TextAlignment.Right);

        var quotes = stocks?.Quotes.Take(3).ToArray() ?? [];
        if (quotes.Length == 0)
        {
            canvas.AlignedText("暂无行情", 17, Colors.White,
                new Rect(safe.Left, safe.Top + 145, safe.Width, 26), FontWeights.SemiBold, TextAlignment.Center);
            canvas.AlignedText("请在屏幕配置中添加代码", 10, secondary,
                new Rect(safe.Left, safe.Top + 180, safe.Width, 18), FontWeights.Normal, TextAlignment.Center);
            return;
        }

        const double rowHeight = 78;
        const double gap = 10;
        var top = safe.Top + 45;
        for (var index = 0; index < 3; index++)
        {
            var row = new Rect(safe.Left, top + index * (rowHeight + gap), safe.Width, rowHeight);
            canvas.RoundedRect(row, 11, card, stroke);
            if (index >= quotes.Length)
            {
                canvas.AlignedText("未添加", 11, secondary,
                    new Rect(row.Left + 12, row.Top, row.Width - 24, row.Height),
                    FontWeights.Medium, TextAlignment.Left);
                continue;
            }

            var quote = quotes[index];
            var gain = quote.ChangePercent >= 0;
            var redForGain = stocks?.RedForGain ?? true;
            var gainColor = redForGain ? Color.FromRgb(238, 78, 88) : Color.FromRgb(47, 194, 116);
            var lossColor = redForGain ? Color.FromRgb(47, 194, 116) : Color.FromRgb(238, 78, 88);
            var changeColor = gain ? gainColor : lossColor;
            var marker = gain ? "▲" : "▼";

            canvas.AlignedText(quote.DisplayName, 12, Colors.White,
                new Rect(row.Left + 12, row.Top + 10, row.Width - 24, 19),
                FontWeights.SemiBold, TextAlignment.Left);
            canvas.AlignedText($"{marker} {Math.Abs(quote.ChangePercent):0.00}%", 22, changeColor,
                new Rect(row.Left + 12, row.Top + 35, row.Width - 24, 30),
                FontWeights.SemiBold, TextAlignment.Left);
        }

        var footer = stocks is { IsStale: true } ? "上次行情数据" : $"更新 {stocks?.UpdatedAt:HH:mm}";
        canvas.AlignedText(footer, 9, secondary,
            new Rect(safe.Left, safe.Bottom - 21, safe.Width, 14), FontWeights.Normal, TextAlignment.Center);
    }
}
