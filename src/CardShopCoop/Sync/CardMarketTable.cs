using System.Collections.Generic;
using CardShopCoop.Net.Messages;
using UnityEngine;

namespace CardShopCoop.Sync
{
    // Shared by every vanilla expansion, including Ascension. Keep existing list/row aliases.
    internal static class CardMarketTable
    {
        /// <summary>Vanilla expansions are owned by the dense GenCardMarketPriceList sync;
        /// only modded (EPL) expansions go through the delta hook.</summary>
        internal static bool IsVanillaExpansion(ECardExpansionType expansion)
        {
            switch (expansion)
            {
                case ECardExpansionType.Tetramon:
                case ECardExpansionType.Destiny:
                case ECardExpansionType.Ghost:
                case ECardExpansionType.Megabot:
                case ECardExpansionType.FantasyRPG:
                case ECardExpansionType.CatJob:
                case ECardExpansionType.Ascension:
                    return true;
                default:
                    return false;
            }
        }

        internal static int HashWire(int h, List<MarketCardEntry> list)
        {
            if (list == null)
                return h;
            for (int i = 0; i < list.Count; i++)
                h = h * 31 + list[i].Percent;
            return h;
        }

        internal static int HashLocal(int h, List<MarketPrice> list)
        {
            if (list == null)
                return h;
            for (int i = 0; i < list.Count; i++)
            {
                var m = list[i];
                h = h * 31 + (m != null ? (int)Mathf.Clamp(Mathf.RoundToInt(m.pricePercentChangeList * 100f), short.MinValue, short.MaxValue) : 0);
            }
            return h;
        }

        internal static void Capture(List<MarketCardEntry> out_list, List<MarketPrice> list)
        {
            int n = Mathf.Min(list?.Count ?? 0, ushort.MaxValue);
            for (int i = 0; i < n; i++)
            {
                out_list.Add(new MarketCardEntry
                {
                    Percent = (short)Mathf.Clamp(Mathf.RoundToInt((list[i] != null ? list[i].pricePercentChangeList : 0f) * 100f), short.MinValue, short.MaxValue),
                    // The card BASE, for the same reason the item bases ride along above: a save
                    // whose card price block failed to restore leaves every base at 0, and the
                    // percent alone multiplies 0 into $0.00 cards forever. Full float - unlike the
                    // percent these are raw prices with no game clamp.
                    GeneratedMarketPrice = list[i] != null ? list[i].generatedMarketPrice : 0f,
                });
            }
        }

        internal static void Apply(List<MarketCardEntry> entries, List<MarketPrice> list)
        {
            if (list == null)
                return;
            for (int i = 0; i < entries.Count; i++)
            {
                float v = entries[i].Percent / 100f;
                float gen = entries[i].GeneratedMarketPrice;
                // A skipped price block during save load can leave this table short.
                // Ascension has its own load gate in game 1.0. Grow rows in place so
                // PriceChangeManager's aliases to the list remain valid.
                while (list.Count <= i)
                    list.Add(new MarketPrice { pastPricePercentChangeList = new List<float>() });
                var row = list[i];
                if (row == null)
                {
                    row = new MarketPrice { pastPricePercentChangeList = new List<float>() };
                    list[i] = row;
                }
                row.pricePercentChangeList = v; // in place: consumers hold the object
                // The snapshot is authoritative, including rows the host legitimately leaves at
                // zero; copying the value makes a stale client row converge to the host state.
                row.generatedMarketPrice = gen;
            }
        }

        internal static bool HasBase(System.Collections.Generic.List<MarketPrice> list)
        {
            if (list == null)
                return false;
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null && list[i].generatedMarketPrice != 0f)
                    return true;
            return false;
        }

    }
}
