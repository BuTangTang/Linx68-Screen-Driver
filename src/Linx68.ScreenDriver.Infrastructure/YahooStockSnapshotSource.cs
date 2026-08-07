using System.Net.Http;
using System.Text.Json;
using Linx68.ScreenDriver.Application;
using Linx68.ScreenDriver.Core;

namespace Linx68.ScreenDriver.Infrastructure;

public sealed class YahooStockSnapshotSource : IStockSnapshotSource, IDisposable
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(15);
    private readonly HttpClient _client;
    private readonly bool _ownsClient;
    private StockSnapshot? _cached;
    private string? _cachedKey;
    private DateTimeOffset _lastFetch;

    public YahooStockSnapshotSource(HttpClient? client = null)
    {
        _ownsClient = client is null;
        _client = client ?? new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 Linx68ScreenDriver/1.0");
    }

    public async Task<StockSnapshot> ReadAsync(StockSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var items = (settings.Items ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item.Symbol))
            .Take(3)
            .Select(item => new StockItemSettings
            {
                Symbol = item.Symbol.Trim().ToUpperInvariant(),
                Alias = item.Alias?.Trim() ?? string.Empty
            })
            .ToArray();
        var key = string.Join("|", items.Select(item => $"{item.Symbol}:{item.Alias}")) + $"|{settings.RedForGain}";
        var now = DateTimeOffset.Now;

        if (_cached is not null && string.Equals(_cachedKey, key, StringComparison.Ordinal)
            && now - _lastFetch < CacheDuration)
        {
            return _cached;
        }

        if (items.Length == 0)
        {
            return new StockSnapshot([], now, settings.RedForGain);
        }

        try
        {
            var quotes = new List<StockQuoteSnapshot>(items.Length);
            foreach (var item in items)
            {
                quotes.Add(await ReadQuoteAsync(item, cancellationToken));
            }

            _cached = new StockSnapshot(quotes, now, settings.RedForGain);
            _cachedKey = key;
            _lastFetch = now;
            return _cached;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            return _cached is null
                ? new StockSnapshot([], now, settings.RedForGain, false, ex.Message)
                : _cached with { IsStale = true, ErrorMessage = ex.Message };
        }
    }

    private async Task<StockQuoteSnapshot> ReadQuoteAsync(StockItemSettings item, CancellationToken cancellationToken)
    {
        var uri = "https://query1.finance.yahoo.com/v8/finance/chart/"
            + Uri.EscapeDataString(item.Symbol) + "?interval=1d&range=5d";
        using var response = await _client.GetAsync(uri, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var chart = document.RootElement.GetProperty("chart");
        if (chart.TryGetProperty("error", out var error) && error.ValueKind is not JsonValueKind.Null)
        {
            throw new InvalidOperationException($"无法读取 {item.Symbol}");
        }

        var result = chart.GetProperty("result");
        if (result.GetArrayLength() == 0)
        {
            throw new InvalidOperationException($"没有找到代码：{item.Symbol}");
        }

        var meta = result[0].GetProperty("meta");
        var current = ReadNumber(meta, "regularMarketPrice");
        var previous = meta.TryGetProperty("chartPreviousClose", out var chartPreviousClose)
            ? chartPreviousClose.GetDouble()
            : ReadNumber(meta, "previousClose");
        if (previous == 0)
        {
            throw new InvalidOperationException($"{item.Symbol} 缺少昨收数据");
        }

        var timestamp = meta.TryGetProperty("regularMarketTime", out var marketTime)
            ? DateTimeOffset.FromUnixTimeSeconds(marketTime.GetInt64()).ToLocalTime()
            : DateTimeOffset.Now;
        var displayName = string.IsNullOrWhiteSpace(item.Alias) ? item.Symbol : item.Alias;
        return new StockQuoteSnapshot(item.Symbol, displayName, current,
            (current - previous) / previous * 100.0, timestamp);
    }

    private static double ReadNumber(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || !property.TryGetDouble(out var value))
        {
            throw new InvalidOperationException($"行情缺少 {propertyName}");
        }
        return value;
    }

    public void Dispose()
    {
        if (_ownsClient) _client.Dispose();
    }
}
