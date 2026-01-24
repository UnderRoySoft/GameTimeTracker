using System;
using Vanara.PInvoke;
using static Vanara.PInvoke.User32;

namespace Tracker.Core.Services
{
    public sealed class InputIdleDetector
    {
        public TimeSpan GetIdleTime()
        {
            var info = new LASTINPUTINFO
            {
                cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<LASTINPUTINFO>()
            };

            if (!GetLastInputInfo(ref info))
                return TimeSpan.Zero;

            // Environment.TickCount64 = milliseconds since system start
            var idleMs = Environment.TickCount64 - info.dwTime;
            if (idleMs < 0) idleMs = 0;

            return TimeSpan.FromMilliseconds(idleMs);
        }
    }
}
