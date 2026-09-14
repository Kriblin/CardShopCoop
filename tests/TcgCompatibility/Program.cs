using CardShopCoop;
using CardShopCoop.Sync;
using Newtonsoft.Json;

int checks = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception(name);
    checks++;
}
var row = new CompactCardDataAmount { expansionType = ECardExpansionType.Ascension, cardSaveIndex = 512, amount = 3, gradedCardIndex = 7, isDestiny = true };
var deck = new DeckCompactCardDataList { deckName = "Saved deck", deckBoxIndex = 1, playmatIndex = 2, compactCardDataAmountList = new() { row } };
CPlayerData.m_DeckCompactCardDataList.Add(deck);
CPlayerData.m_IsPlayerRegisteredForTournament = true;
CPlayerData.m_PlayerTournamentData = new()
{
    m_IsTournamentCustomer = true, m_TournamentCustomerSortedIndex = 3,
    m_HasRegisteredTournamentResult = true, m_TournamentWinCount = 2,
    m_TournamentPlacementIndex = 1, m_TournamentOpponentIndexList = new() { 4, 5 },
    m_PrizeDataList = new() { new() { m_Count = 2, m_ItemType = EItemType.TestDeckBox } }
};
var snapshot = TcgPlayerState.Capture();
string saved = JsonConvert.SerializeObject(snapshot);
Check(!ReferenceEquals(snapshot.Decks[0].Cards[0], row), "capture detaches card rows");
Check(!ReferenceEquals(snapshot.Tournament, CPlayerData.m_PlayerTournamentData), "capture detaches player tournament data");
Check(!ReferenceEquals(snapshot.Tournament.m_PrizeDataList[0], CPlayerData.m_PlayerTournamentData.m_PrizeDataList[0]), "capture detaches prize rows");
row.amount = 1;
Check(snapshot.Decks[0].Cards[0].amount == 3, "host edits do not alter a queued snapshot");
for (int i = 0; i < 3; i++) snapshot.Apply();
Check(JsonConvert.SerializeObject(TcgPlayerState.Capture()) == saved, "repeated snapshots converge without inventory callbacks");
Check(CPlayerData.m_CurrentSelectedDeckIndex == 0, "selected deck preserved");
Check(CPlayerData.m_DeckCompactCardDataList[0].deckBoxIndex == 1 && CPlayerData.m_DeckCompactCardDataList[0].playmatIndex == 2, "cosmetic IDs preserved");
Check(CPlayerData.m_DeckCompactCardDataList[0].compactCardDataAmountList[0].gradedCardIndex == 7, "graded card index preserved");
Check(TcgPlayerState.PlayerBracket(3) != null, "player null sentinel resolved");
Check(TcgPlayerState.PlayerBracket(0) == null, "other empty slots are not players");
CPlayerData.m_DeckCompactCardDataList[0].compactCardDataAmountList[0].amount = 0;
CPlayerData.m_PlayerTournamentData.m_TournamentOpponentIndexList.Clear();
Check(JsonConvert.SerializeObject(snapshot) == saved, "apply does not alias snapshot data");
new TcgPlayerState().Apply();
Check(CPlayerData.m_DeckCompactCardDataList.Count == 0, "deleted decks removed by snapshot");
Check(!CPlayerData.m_IsPlayerRegisteredForTournament && TcgPlayerState.PlayerBracket(0) == null, "withdrawal resets participation");
snapshot.Apply();
Check(JsonConvert.SerializeObject(TcgPlayerState.Capture()) == saved, "reconnect restores saved state");

CoopCore.Role = CoopRole.Client;
Check(!TcgAuthority.HostOnly(), "guest mutation denied");
Check(CoopCore.Instance.RegisterLine.Contains("host-only") && CoopCore.Instance.RegisterLineTimer > 0, "guest denial explained");
var battle = new PlayTableGame();
Check(!TcgAuthority.Begin(battle, out _), "guest battle entry denied");
Check(!TcgAuthority.ReportResult(battle) && !TcgAuthority.Rematch(battle) && !TcgAuthority.Leave(battle) && !TcgAuthority.Gift(battle), "guest completion callbacks denied");
CoopCore.Role = CoopRole.Host;
Check(TcgAuthority.HostOnly(), "host mutations allowed");
Check(TcgAuthority.Begin(battle, out bool began), "host battle entry allowed");
battle.Playing = true;
TcgAuthority.Began(battle, began);
Check(!TcgAuthority.Gift(battle) && !TcgAuthority.Leave(battle), "no gift or exit before result");
Check(TcgAuthority.ReportResult(battle) && !TcgAuthority.ReportResult(battle), "one result per round");
Check(TcgAuthority.Rematch(battle) && !TcgAuthority.Rematch(battle), "one rematch coroutine per result");
Check(TcgAuthority.ReportResult(battle), "rematch accepts new result");
Check(TcgAuthority.Leave(battle) && !TcgAuthority.Leave(battle), "one exit coroutine per visit");
Check(TcgAuthority.Gift(battle) && !TcgAuthority.Gift(battle), "one gift per visit");
Check(!TcgAuthority.Rematch(battle) && !TcgAuthority.ReportResult(battle), "ending visit cannot restart or count result");
battle.Playing = false;
TcgAuthority.Begin(battle, out began);
battle.Playing = true;
TcgAuthority.Began(battle, began);
Check(TcgAuthority.ReportResult(battle) && TcgAuthority.Leave(battle) && TcgAuthority.Gift(battle), "new visit resets reward allowance");

var table = new InteractablePlayTable();
Check(!TcgAuthority.PlayerAtTable(table) && TcgAuthority.ProtectTable(table), "idle table usable");
table.Seats.Add(true);
Check(TcgAuthority.PlayerAtTable(table) && !TcgAuthority.ProtectTable(table), "entrance/exit seat protects table before battle flag");
table.Seats.Clear(); table.Started = true;
Check(TcgAuthority.PlayerAtTable(table), "running battle protects table");
table.Started = false;
CoopCore.Role = CoopRole.Client;
PlayTableSync.Active = new() { Occupied = true };
Check(!TcgAuthority.ProtectTable(table), "remote host battle protects guest table");
PlayTableSync.Active.Occupied = false;
Check(TcgAuthority.ProtectTable(table), "received idle state releases table");
CoopCore.Role = CoopRole.None;
Check(TcgAuthority.HostOnly() && TcgAuthority.Gift(battle), "offline behavior stays native");
Check(TcgAuthority.CanStartHosting(), "empty scene does not fabricate managers");
UnityEngine.Object.Scene[typeof(WorkbenchUIScreen)] = new WorkbenchUIScreen { m_IsEditingDeck = true };
Check(!TcgAuthority.CanStartHosting(), "hosting cannot start during deck callbacks");
UnityEngine.Object.Scene.Clear();
UnityEngine.Object.Scene[typeof(InteractionPlayerController)] = new InteractionPlayerController { m_IsPlayTableGameMode = true };
Check(!TcgAuthority.CanStartHosting(), "hosting cannot start during final exit animation");
UnityEngine.Object.Scene.Clear();
Console.WriteLine($"Passed {checks} TCG compatibility checks.");
