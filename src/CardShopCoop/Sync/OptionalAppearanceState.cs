using System;

namespace CardShopCoop.Sync
{
    internal static class OptionalAppearanceState
    {
        // Appearance is optional: returning null omits its message, not the world transfer.
        internal static T Build<T>(Func<T> build, Action<Exception> report) where T : class
        {
            try { return build(); }
            catch (Exception e)
            {
                report(e);
                return null;
            }
        }
    }
}
