using System.Reflection;

// Only the data/permission surface used by the linked production helpers.
// Native method signatures and save integration are checked by GameCompatibility.
public enum EItemType { None, TestDeckBox, TestPlaymat }
public enum ECardExpansionType { Tetramon, Ascension }
public sealed class CompactCardDataAmount
{
    public ECardExpansionType expansionType;
    public int cardSaveIndex, amount, gradedCardIndex;
    public bool isDestiny;
}
public sealed class DeckCompactCardDataList
{
    public string deckName;
    public int deckBoxIndex, playmatIndex;
    public List<CompactCardDataAmount> compactCardDataAmountList = new();
}
public sealed class CustomerTournamentData
{
    public bool m_IsTournamentCustomer, m_HasRegisteredTournamentResult;
    public int m_TournamentCustomerSortedIndex, m_TournamentWinCount, m_TournamentPlacementIndex;
    public List<int> m_TournamentOpponentIndexList = new();
    public List<TournamentPrizeData> m_PrizeDataList = new();
}
public sealed class TournamentPrizeData { public int m_Count; public EItemType m_ItemType; }
public static class CPlayerData
{
    public static int m_CurrentSelectedDeckIndex;
    public static bool m_IsPlayerRegisteredForTournament;
    public static CustomerTournamentData m_PlayerTournamentData = new();
    public static List<DeckCompactCardDataList> m_DeckCompactCardDataList = new();
    public static void AddCard(object card, int amount) => throw new Exception("Snapshot must not add inventory cards");
    public static void ReduceCard(object card, int amount) => throw new Exception("Snapshot must not remove inventory cards");
}
namespace UnityEngine
{
    public class Object
    {
        public static readonly Dictionary<Type, object> Scene = new();
        public static T FindFirstObjectByType<T>() where T : class => Scene.TryGetValue(typeof(T), out var o) ? (T)o : null;
    }
}
namespace HarmonyLib
{
    public static class AccessTools
    {
        public static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        public static MethodInfo Method(Type type, string name) => type.GetMethod(name);
    }
    public sealed class HarmonyMethod { public HarmonyMethod(Type type, string name) { } }
    public sealed class Harmony { public void Patch(MethodInfo method, HarmonyMethod prefix = null, HarmonyMethod postfix = null) { } }
}
public class InteractableObject { }
public sealed class InteractablePlayTable : InteractableObject
{
    public readonly List<bool> Seats = new();
    public bool Started;
    public List<bool> GetIsPlayerSeat() => Seats;
    public bool GetHasStartPlayerPlayCard() => Started;
}
public sealed class WorkbenchUIScreen { public bool m_IsEditingDeck; }
public sealed class InteractionPlayerController { public bool m_IsPlayTableGameMode; }
public sealed class PlayTableGame
{
    public bool Playing;
    public InteractablePlayTable m_CurrentInteractablePlayTable;
    public bool IsPlayTableGameMode() => Playing;
}
public sealed class PlayCardGameManager { }
public sealed class DeckListScreen { }
public sealed class DeckEditScreen { }
public sealed class DeckCardPlusMinusScreen { }
public sealed class DeckboxPlaymatNameEditScreen { }
public sealed class HostTournamentScreen { }
namespace CardShopCoop
{
    public enum CoopRole { None, Host, Client }
    public sealed class CoopCore
    {
        public static CoopRole Role;
        public static CoopCore Instance = new();
        public string RegisterLine;
        public float RegisterLineTimer;
    }
}
namespace CardShopCoop.Sync
{
    public sealed class PlayTableSync
    {
        public static PlayTableSync Active;
        public bool Occupied;
        internal bool IsPlayerOccupied(InteractablePlayTable table) => Occupied;
    }
}
