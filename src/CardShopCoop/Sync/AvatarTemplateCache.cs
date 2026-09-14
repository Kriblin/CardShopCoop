using System;

namespace CardShopCoop.Sync
{
    // Owns one prepared template and remembers failures independently for each gender.
    // The Unity boundary supplies creation, initialization, and destruction.
    internal sealed class AvatarTemplateCache<TSource, TTemplate>
        where TSource : class
        where TTemplate : class, new()
    {
        private readonly Action<TTemplate> _destroy;
        private readonly TSource[] _failed = new TSource[2];
        private TSource _source;
        private bool _female;
        private TTemplate _current;

        internal AvatarTemplateCache(Action<TTemplate> destroy) { _destroy = destroy; }

        internal TTemplate Get(TSource source, bool female, Func<TTemplate, bool> alive,
            Action<TTemplate> initialize, Action<TTemplate, Exception> failed)
        {
            if (source == null)
                return null;
            if (_current != null && ReferenceEquals(_source, source) && _female == female && alive(_current))
                return _current;
            Release();
            int slot = female ? 1 : 0;
            if (ReferenceEquals(_failed[slot], source))
                return null;
            // A changed prefab starts a new attempt, including if the previous prefab returns later.
            _failed[slot] = null;
            var candidate = new TTemplate();
            try
            {
                initialize(candidate);
                _current = candidate;
                _source = source;
                _female = female;
                return candidate;
            }
            catch (Exception e)
            {
                _failed[slot] = source;
                try { failed(candidate, e); }
                finally { _destroy(candidate); }
                return null;
            }
        }

        private void Release()
        {
            var old = _current;
            _current = null;
            _source = null;
            if (old != null)
                _destroy(old);
        }

        internal void Clear()
        {
            Release();
            Array.Clear(_failed, 0, _failed.Length);
        }

        internal void RejectCurrent()
        {
            if (_source != null)
                _failed[_female ? 1 : 0] = _source;
            Release();
        }
    }
}
