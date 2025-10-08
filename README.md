# GameTimeTracker

Windows desktop app (WPF, .NET 8) that tracks per-game playtime with idle detection and focus awareness.

## Projects
- Tracker.Core — detection, session engine, rules
- Tracker.Data — SQLite access
- Tracker.IPC — contracts for UI <-> service
- Tracker.Service — background host
- Tracker.UI — WPF app + tray
- Tracker.Tests — xUnit

## Build
- `dotnet build`
- Run UI: `dotnet run --project Tracker.UI`
- Run Service: `dotnet run --project Tracker.Service`

## Next
1. Define DB schema + repositories (Section 2)
2. Implement Process/Window watchers (Section 3)
3. Session state machine + idle (Section 4)
4. Basic dashboard UI (Section 5)
5. Installer (MSIX/MSI) (Section 6)
