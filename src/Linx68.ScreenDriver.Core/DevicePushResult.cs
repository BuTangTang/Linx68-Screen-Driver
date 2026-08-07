using System;

namespace Linx68.ScreenDriver.Core;

public sealed record DevicePushResult(bool Success, int? StatusCode, string Message, TimeSpan Elapsed);
