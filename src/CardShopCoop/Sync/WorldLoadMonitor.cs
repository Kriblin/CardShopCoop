using System;

namespace CardShopCoop.Sync
{
    // Uses unscaled time so a paused loading screen cannot disable recovery.
    internal sealed class WorldLoadMonitor
    {
        internal const float TimeoutSeconds = 180f;
        private float _started;
        internal bool Active
        {
            get; private set;
        }
        internal void Begin(float now)
        {
            _started = now;
            Active = true;
        }
        internal void Reset()
        {
            Active = false;
        }
        internal string Poll(float now, bool loadingError, bool ready)
        {
            if (!Active)
                return null;
            string error = loadingError ? "The game reported a save loading error."
                : now - _started >= TimeoutSeconds ? "World loading timed out after 180 seconds." : null;
            if (error != null || ready)
                Active = false;
            return error;
        }
    }
}
