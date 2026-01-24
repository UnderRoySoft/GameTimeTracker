using System;

namespace Tracker.Core.Models
{
    public sealed record ForegroundAppInfo(
        int ProcessId,
        string ProcessName,
        string? FilePath,
        string WindowTitle,
        DateTimeOffset ObservedAtUtc
    );
}
