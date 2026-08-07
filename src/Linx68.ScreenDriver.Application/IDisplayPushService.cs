using Linx68.ScreenDriver.Core;

namespace Linx68.ScreenDriver.Application;

public interface IDisplayPushService
{
    Task<DevicePushResult> PushAsync(
        string? endpoint,
        RenderedFrame frame,
        CancellationToken cancellationToken = default);
}
