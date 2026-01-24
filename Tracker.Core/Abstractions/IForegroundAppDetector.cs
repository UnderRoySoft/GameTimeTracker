using Tracker.Core.Models;

namespace Tracker.Core.Abstractions
{
    public interface IForegroundAppDetector
    {
        ForegroundAppInfo? GetCurrentForegroundApp();
    }
}
