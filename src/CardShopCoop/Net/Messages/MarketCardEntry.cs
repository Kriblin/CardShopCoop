namespace CardShopCoop.Net.Messages
{
    /// <summary>One card market row: percent (x100) and full-precision base price.</summary>
    public sealed class MarketCardEntry
    {
        public short Percent;
        public float GeneratedMarketPrice;
    }
}
