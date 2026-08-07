using Linx68.ScreenDriver.Core;

namespace Linx68.ScreenDriver.Application;

public sealed class DisplayPushService(IDeviceTransport transport) : IDisplayPushService
{
    public Task<DevicePushResult> PushAsync(
        string? endpoint,
        RenderedFrame frame,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (!DeviceEndpoint.TryCreate(endpoint, out Uri uri))
        {
            return Task.FromResult(new DevicePushResult(
                false,
                null,
                "设备地址无效",
                TimeSpan.Zero));
        }

        return transport.PushAsync(uri, frame, cancellationToken);
    }
}
