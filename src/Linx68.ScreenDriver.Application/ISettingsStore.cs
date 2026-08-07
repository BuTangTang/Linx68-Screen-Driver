using Linx68.ScreenDriver.Core;

namespace Linx68.ScreenDriver.Application;

public interface ISettingsStore
{
    Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}
