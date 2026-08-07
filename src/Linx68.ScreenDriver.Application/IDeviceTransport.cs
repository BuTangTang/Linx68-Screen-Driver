using System;
using System.Threading;
using System.Threading.Tasks;
using Linx68.ScreenDriver.Core;

namespace Linx68.ScreenDriver.Application;

public interface IDeviceTransport
{
	Task<DevicePushResult> PushAsync(Uri endpoint, RenderedFrame frame, CancellationToken cancellationToken = default(CancellationToken));
}
