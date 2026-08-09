using Windows.Media.Control;
using Windows.Storage.Streams;
using System.Diagnostics;
using Linx68.ScreenDriver.Application;
using Linx68.ScreenDriver.Core;

namespace Linx68.ScreenDriver.Infrastructure;

internal sealed record MusicSessionCandidate(
    int Index,
    string SourceAppId,
    bool IsCurrent,
    bool IsPlaying)
{
    public bool IsNetEase => WindowsMusicSessionSelector.IsNetEase(SourceAppId);
}

internal static class WindowsMusicSessionSelector
{
    public static IReadOnlyList<int> Order(IEnumerable<MusicSessionCandidate> candidates) => candidates
        .OrderBy(GetPriority)
        .ThenBy(candidate => candidate.Index)
        .Select(candidate => candidate.Index)
        .ToArray();

    public static bool IsNetEase(string? sourceAppId) =>
        sourceAppId?.Contains("cloudmusic", StringComparison.OrdinalIgnoreCase) == true ||
        sourceAppId?.Contains("netease", StringComparison.OrdinalIgnoreCase) == true;

    private static int GetPriority(MusicSessionCandidate candidate)
    {
        if (candidate.IsCurrent && candidate.IsPlaying) return 0;
        if (candidate.IsPlaying && candidate.IsNetEase) return 1;
        if (candidate.IsPlaying) return 2;
        if (candidate.IsCurrent) return 3;
        if (candidate.IsNetEase) return 4;
        return 5;
    }
}

internal sealed class NetEaseWindowPlaybackClock
{
    private readonly Stopwatch _stopwatch = new();
    private string? _trackKey;

    public TimeSpan GetPosition(string title, string artist)
    {
        string trackKey = $"{title}\n{artist}";
        if (!string.Equals(_trackKey, trackKey, StringComparison.Ordinal))
        {
            _trackKey = trackKey;
            _stopwatch.Restart();
        }

        return _stopwatch.Elapsed;
    }

    public void Reset()
    {
        _trackKey = null;
        _stopwatch.Reset();
    }
}

internal sealed class MediaSessionPlaybackClock(Func<TimeSpan>? elapsedProvider = null)
{
    private readonly Func<TimeSpan> _elapsedProvider = elapsedProvider ?? ReadMonotonicElapsed;
    private string? _trackKey;
    private TimeSpan _lastRawPosition;
    private TimeSpan _effectivePosition;
    private TimeSpan _lastObservedAt;
    private TimeSpan _lastRawChangedAt;
    private bool _wasPlaying;
    private int _frozenSampleCount;

    public TimeSpan GetPosition(
        string trackKey,
        TimeSpan rawPosition,
        TimeSpan duration,
        bool isPlaying)
    {
        TimeSpan observedAt = _elapsedProvider();
        rawPosition = rawPosition < TimeSpan.Zero ? TimeSpan.Zero : rawPosition;
        if (duration > TimeSpan.Zero && rawPosition > duration)
        {
            rawPosition = duration;
        }

        if (!string.Equals(_trackKey, trackKey, StringComparison.Ordinal))
        {
            _trackKey = trackKey;
            _lastRawPosition = rawPosition;
            _effectivePosition = rawPosition;
            _lastObservedAt = observedAt;
            _lastRawChangedAt = observedAt;
            _wasPlaying = isPlaying;
            _frozenSampleCount = 0;
            return rawPosition;
        }

        TimeSpan rawDelta = rawPosition - _lastRawPosition;
        bool rawChanged = rawDelta.Duration() >= TimeSpan.FromMilliseconds(200);
        if (rawChanged)
        {
            _effectivePosition = rawPosition;
            _lastRawChangedAt = observedAt;
            _frozenSampleCount = 0;
        }
        else if (!isPlaying)
        {
            if (rawPosition > _effectivePosition)
            {
                _effectivePosition = rawPosition;
            }
            _frozenSampleCount = 0;
        }
        else if (!_wasPlaying)
        {
            if (rawPosition > _effectivePosition)
            {
                _effectivePosition = rawPosition;
            }
            _lastRawChangedAt = observedAt;
            _frozenSampleCount = 0;
        }
        else
        {
            _frozenSampleCount++;
            if (_frozenSampleCount >= 2)
            {
                TimeSpan estimated = rawPosition + (observedAt - _lastRawChangedAt);
                _effectivePosition = estimated > _effectivePosition
                    ? estimated
                    : _effectivePosition + MaxZero(observedAt - _lastObservedAt);
            }
        }

        if (duration > TimeSpan.Zero && _effectivePosition > duration)
        {
            _effectivePosition = duration;
        }
        if (_effectivePosition < TimeSpan.Zero)
        {
            _effectivePosition = TimeSpan.Zero;
        }

        _lastRawPosition = rawPosition;
        _lastObservedAt = observedAt;
        _wasPlaying = isPlaying;
        return _effectivePosition;
    }

    public void Reset()
    {
        _trackKey = null;
        _lastRawPosition = TimeSpan.Zero;
        _effectivePosition = TimeSpan.Zero;
        _lastObservedAt = TimeSpan.Zero;
        _lastRawChangedAt = TimeSpan.Zero;
        _wasPlaying = false;
        _frozenSampleCount = 0;
    }

    private static TimeSpan MaxZero(TimeSpan value) => value < TimeSpan.Zero ? TimeSpan.Zero : value;

    private static TimeSpan ReadMonotonicElapsed() =>
        TimeSpan.FromSeconds((double)Stopwatch.GetTimestamp() / Stopwatch.Frequency);
}

public sealed class WindowsMusicSnapshotSource : IMusicSnapshotSource
{
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private readonly NetEaseWindowPlaybackClock _netEaseWindowPlaybackClock = new();
    private readonly MediaSessionPlaybackClock _sessionPlaybackClock = new();

    public async ValueTask<MusicSnapshot> ReadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            _manager ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            GlobalSystemMediaTransportControlsSession? current = _manager.GetCurrentSession();
            var sessions = _manager.GetSessions().ToList();
            if (current is not null && !sessions.Any(session => IsSameSession(session, current)))
            {
                sessions.Insert(0, current);
            }

            var candidates = sessions.Select((session, index) => new MusicSessionCandidate(
                index,
                ReadSourceAppId(session),
                current is not null && IsSameSession(session, current),
                ReadIsPlaying(session)));

            foreach (int index in WindowsMusicSessionSelector.Order(candidates))
            {
                cancellationToken.ThrowIfCancellationRequested();
                MusicSnapshot? snapshot = await TryReadSessionAsync(sessions[index], cancellationToken);
                if (snapshot is not null)
                {
                    return snapshot;
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Windows may briefly invalidate the session collection while players open or close.
        }

        return TryReadNetEaseWindow() ?? MusicSnapshot.Unavailable;
    }

    private MusicSnapshot? TryReadNetEaseWindow()
    {
        foreach (Process process in Process.GetProcessesByName("cloudmusic"))
        {
            using (process)
            {
                try
                {
                    process.Refresh();
                    if (process.MainWindowHandle == IntPtr.Zero ||
                        !NetEaseWindowTitleParser.TryParse(process.MainWindowTitle, out string title, out string artist))
                    {
                        continue;
                    }

                    return new MusicSnapshot(
                        Available: true,
                        title,
                        artist,
                        Position: _netEaseWindowPlaybackClock.GetPosition(title, artist),
                        Duration: TimeSpan.Zero,
                        IsPlaying: true,
                        Artwork: null)
                    {
                        SourceAppId = "cloudmusic.exe"
                    };
                }
                catch
                {
                    // A Cloud Music process can exit or replace its main window while it is being queried.
                }
            }
        }

		_netEaseWindowPlaybackClock.Reset();
        return null;
    }

    private async Task<MusicSnapshot?> TryReadSessionAsync(
        GlobalSystemMediaTransportControlsSession session,
        CancellationToken cancellationToken)
    {
        try
        {
            GlobalSystemMediaTransportControlsSessionMediaProperties properties = await session.TryGetMediaPropertiesAsync();
            cancellationToken.ThrowIfCancellationRequested();
            GlobalSystemMediaTransportControlsSessionTimelineProperties timeline = session.GetTimelineProperties();
            GlobalSystemMediaTransportControlsSessionPlaybackInfo playback = session.GetPlaybackInfo();
            byte[]? artwork = null;
            if (properties.Thumbnail is not null)
            {
                try
                {
                    artwork = await ReadArtworkAsync(properties.Thumbnail, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    // Metadata remains useful when a player exposes an invalid thumbnail stream.
                }
            }

            TimeSpan duration = timeline.EndTime > timeline.StartTime
                ? timeline.EndTime - timeline.StartTime
                : TimeSpan.Zero;
            TimeSpan rawPosition = timeline.Position < TimeSpan.Zero ? TimeSpan.Zero : timeline.Position;
            string sourceAppId = ReadSourceAppId(session);
            bool isPlaying = playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            string title = string.IsNullOrWhiteSpace(properties.Title) ? "未知曲目" : properties.Title;
            string artist = properties.Artist ?? string.Empty;
            string trackKey = $"{sourceAppId}\n{title}\n{artist}\n{properties.AlbumTitle}";
            TimeSpan position = _sessionPlaybackClock.GetPosition(
                trackKey,
                rawPosition,
                duration,
                isPlaying);

            return new MusicSnapshot(
                Available: true,
                title,
                artist,
                position,
                duration,
                isPlaying,
                artwork)
            {
                SourceAppId = sourceAppId,
                AlbumTitle = properties.AlbumTitle ?? string.Empty
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private static bool ReadIsPlaying(GlobalSystemMediaTransportControlsSession session)
    {
        try
        {
            return session.GetPlaybackInfo().PlaybackStatus ==
                GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
        }
        catch
        {
            return false;
        }
    }

    private static string ReadSourceAppId(GlobalSystemMediaTransportControlsSession session)
    {
        try
        {
            return session.SourceAppUserModelId ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool IsSameSession(
        GlobalSystemMediaTransportControlsSession left,
        GlobalSystemMediaTransportControlsSession right)
    {
        if (ReferenceEquals(left, right)) return true;
        string leftId = ReadSourceAppId(left);
        string rightId = ReadSourceAppId(right);
        return !string.IsNullOrWhiteSpace(leftId) && string.Equals(leftId, rightId, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<byte[]?> ReadArtworkAsync(
        IRandomAccessStreamReference reference,
        CancellationToken cancellationToken)
    {
        using IRandomAccessStreamWithContentType stream = await reference.OpenReadAsync();
        ulong size = stream.Size;
        if (size is 0 or > 5242880)
        {
            return null;
        }
        using DataReader reader = new DataReader(stream.GetInputStreamAt(0uL));
        uint length = (uint)size;
        await reader.LoadAsync(length);
        cancellationToken.ThrowIfCancellationRequested();
        byte[] data = new byte[length];
        reader.ReadBytes(data);
        return data;
    }
}

internal static class NetEaseWindowTitleParser
{
    public static bool TryParse(string? value, out string title, out string artist)
    {
        title = string.Empty;
        artist = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string normalized = value.Trim();
        if (string.Equals(normalized, "网易云音乐", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "NetEase Cloud Music", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        int separator = normalized.LastIndexOf(" - ", StringComparison.Ordinal);
        if (separator <= 0 || separator >= normalized.Length - 3)
        {
            return false;
        }

        title = normalized[..separator].Trim();
        artist = normalized[(separator + 3)..].Trim();
        return title.Length > 0 && artist.Length > 0;
    }
}
