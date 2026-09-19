namespace CardShopCoop.Sync
{
    // One native table visit may contain several rematches, but only one exit/gift.
    internal sealed class BattleCompletion
    {
        public BattleCompletion()
        {
        }

        private bool _resolved;
        private bool _leaving;
        private bool _gift;

        public bool ReportResult()
        {
            if (_resolved || _leaving)
                return false;
            _resolved = true;
            return true;
        }

        public bool Rematch()
        {
            if (!_resolved || _leaving)
                return false;
            _resolved = false;
            return true;
        }

        public bool Leave()
        {
            if (!_resolved || _leaving)
                return false;
            _leaving = true;
            return true;
        }

        public bool GrantGift()
        {
            if (!_leaving || _gift)
                return false;
            // Claim before native spawning: a failure must not replay partial rewards.
            _gift = true;
            return true;
        }
    }
}
