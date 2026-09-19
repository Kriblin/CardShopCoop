using CardShopCoop.Sync;
using CardShopCoop.Net.Messages;

int passed = 0;
void Check(bool result, string description)
{
    if (!result)
        throw new Exception(description);
    Console.WriteLine("PASS " + description);
    passed++;
}
Check(CardMarketTable.IsVanillaExpansion(ECardExpansionType.Ascension), "Ascension uses full snapshots, not modded deltas");
Check(!CardMarketTable.IsVanillaExpansion((ECardExpansionType)9000), "modded expansions retain the delta path");
Check(!CardMarketTable.IsVanillaExpansion(ECardExpansionType.FoodieGO), "unimplemented expansion has no vanilla table");
var host = new List<MarketPrice>
{
    new() { generatedMarketPrice = 19.125f, pricePercentChangeList = 1.234f },
    new() { generatedMarketPrice = 0f, pricePercentChangeList = -0.5f }
};
var snapshot = new List<MarketCardEntry>();
CardMarketTable.Capture(snapshot, host);
Check(snapshot[0].GeneratedMarketPrice == 19.125f && snapshot[0].Percent == 123, "base prices retain precision while percentages use wire rounding");
var original = new MarketPrice { generatedMarketPrice = 999f, pastPricePercentChangeList = new() { 0.75f } };
var guest = new List<MarketPrice> { original };
CardMarketTable.Apply(snapshot, guest);
Check(guest.Count == 2 && ReferenceEquals(guest[0], original), "join repair grows short tables without replacing existing rows");
Check(guest[0].generatedMarketPrice == 19.125f && guest[1].generatedMarketPrice == 0f, "host bases replace stale guest values including authoritative zero");
Check(original.pastPricePercentChangeList.SequenceEqual(new[] { 0.75f }), "snapshot application preserves saved history");
Check(CardMarketTable.HashWire(17, snapshot) == CardMarketTable.HashLocal(17, guest), "host wire and repaired guest checksums match");
int checksum = CardMarketTable.HashLocal(17, guest);
ApplyDailySnapshot();
void ApplyDailySnapshot()
{
    host[0].pricePercentChangeList = 1.9f;
    snapshot.Clear();
    CardMarketTable.Capture(snapshot, host);
    CardMarketTable.Apply(snapshot, guest);
}
Check(checksum != CardMarketTable.HashLocal(17, guest) && guest[0].pricePercentChangeList == 1.9f, "daily percentage changes affect state and checksum");
CardMarketTable.Apply(snapshot, guest);
Check(original.pastPricePercentChangeList.Count == 1, "repeated snapshots do not append price history");
guest.Clear();
guest.Add(null);
CardMarketTable.Apply(snapshot, guest);
Check(guest.Count == 2 && guest[0] != null && guest[0].generatedMarketPrice == host[0].generatedMarketPrice, "reconnect repair replaces null rows and rebuilds missing rows");
Check(CardMarketTable.HasBase(guest) && !CardMarketTable.HasBase(new()), "price guard distinguishes restored bases from empty tables");
Check(!CardMarketTable.HasBase(null), "missing price table does not claim restored bases");
snapshot.Clear();
CardMarketTable.Capture(snapshot, new List<MarketPrice> { null });
Check(snapshot.Count == 1 && snapshot[0].Percent == 0 && snapshot[0].GeneratedMarketPrice == 0, "null host rows serialize as zero values");
Console.WriteLine($"{passed} regression checks passed.");

public enum ECardExpansionType
{
    None = -1, Tetramon, Destiny, Ghost, Megabot, FantasyRPG, CatJob, FoodieGO, Ascension, MAX
}
public class MarketPrice
{
    public float generatedMarketPrice, pricePercentChangeList;
    public List<float> pastPricePercentChangeList = new();
}
namespace UnityEngine
{
    public static class Mathf
    {
        public static int Min(int a, int b) => Math.Min(a, b);
        public static int RoundToInt(float value) => (int)Math.Round(value);
        public static int Clamp(int value, int min, int max) => Math.Clamp(value, min, max);
    }
}
