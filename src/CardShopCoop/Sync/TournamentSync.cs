using System;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using CardShopCoop.Net;
using CardShopCoop.Net.Messages;

namespace CardShopCoop.Sync
{
    /// <summary>
    /// Mirrors tournament data and the shared player's decks host->client (MsgType.TournamentState).
    /// TCG interactions are host-only (TcgAuthority); snapshots never replay rewards or card transfers.
    /// The customer bracket only exists in the host simulation, so the joiner gets
    /// CPlayerData.m_TournamentData (schedule, fee, sign-ups, round, prize catalog)
    /// plus a per-customer digest of CustomerTournamentData - enough for the phone
    /// app (HostTournamentScreen reads m_TournamentData live on open) and for the
    /// physical pairing board, which we drive directly through TournamentPairingScreen
    /// because RefreshAllCustomerData wants live Customer objects the joiner never has.
    /// There are NO client ops: scheduling is host-only, so the joiner's confirm/cancel
    /// buttons are blocked with a "the host schedules tournaments" toast instead of
    /// being forwarded. Prize shelf CONTENTS are synced elsewhere (CardShelfSync).
    /// </summary>
    public class TournamentSync : TickableCoopModule
    {
        /// <summary>TournamentPrizeShelf.m_ScreenMesh (the shelf's little tournament display) is
        /// absent from the Game Pass Assembly-CSharp, which made a direct field access fail to
        /// COMPILE against that build - one cosmetic toggle taking the whole universal DLL down
        /// with it. Resolved once through reflection instead, so a missing or renamed field just
        /// disables the show/hide. Null when the field isn't there; every use is null-guarded.
        /// (Reflection change contributed by Jburne10 for the Game Pass build.)</summary>
        private static readonly FieldInfo FiScreenMesh =
            AccessTools.Field(typeof(TournamentPrizeShelf), "m_ScreenMesh");

        /// <summary>Set by CoopCore: host -> clients state broadcast.</summary>
        public Action<INetMessage> BroadcastState;

        /// <summary>True while ClientApplyState writes CPlayerData.m_TournamentData, so
        /// no patch mistakes the authoritative copy for a local scheduling action.</summary>
        public static bool ApplyingRemote;

        private readonly SnapshotGate _gate = new SnapshotGate(1.5f, 15f, -6.1f);
        private int _clientHash;

        // NEVER CSingleton<CustomerManager>.Instance: touched while no real manager
        // exists (client reload loading screen, host mid-session save load) the getter
        // fabricates a fake empty DontDestroyOnLoad manager that shadows the real one
        // for the rest of the run (see WorldSync.ResolveShelfManager). Static because
        // the wire writer and hash are static; fake-null re-resolves after scene loads.
        private static CustomerManager _cm;

        private static CustomerManager Cm()
        {
            if (_cm == null)
                _cm = UnityEngine.Object.FindObjectOfType<CustomerManager>();
            return _cm;
        }

        public override string Name => "tournament";

        protected override void OnHostTick(in SyncFrame frame) => HostTick(frame.Dt, frame.InGame);

        public override void Dispose()
        {
            base.Dispose();
            ApplyingRemote = false;
            _cm = null;
        }

        public override void Reset()
        {
            _gate.Reset(-6.1f);
            _clientHash = 0;
            _cm = null;
        }

        public override void ForceResend()
        {
            _gate.Force();
        }

        // ---------------- patches ----------------

        public static void ApplyPatches(Harmony h)
        {
            // Scheduling, cancelling and prize setup all mutate m_TournamentData and the
            // prize shelf plan - host-only decisions, since only the host's customer sim
            // can actually run the event. The joiner's phone screen stays readable (it
            // renders the synced data) but its buttons do nothing except explain why.
            Try(h, typeof(HostTournamentScreen), "OnPressConfirm",
                prefix: new HarmonyMethod(typeof(TournamentSync), nameof(ScheduleBlockPrefix)));
            Try(h, typeof(HostTournamentScreen), "OnPressCancel",
                prefix: new HarmonyMethod(typeof(TournamentSync), nameof(ScheduleBlockPrefix)));
            Try(h, typeof(HostTournamentScreen), "ConfirmCancelTournament",
                prefix: new HarmonyMethod(typeof(TournamentSync), nameof(ScheduleBlockPrefix)));
            Try(h, typeof(HostTournamentScreen), "OnPressPrizeSetup",
                prefix: new HarmonyMethod(typeof(TournamentSync), nameof(ScheduleBlockPrefix)));
        }

        public static bool ScheduleBlockPrefix()
        {
            if (CoopCore.Role != CoopRole.Client)
                return true;
            if (CoopCore.Instance != null)
            {
                CoopCore.Instance.RegisterLine = "the host schedules tournaments";
                CoopCore.Instance.RegisterLineTimer = 3f;
            }
            return false;
        }

        private static void Try(Harmony h, Type type, string method,
            HarmonyMethod prefix = null, HarmonyMethod postfix = null)
        {
            try
            {
                var original = AccessTools.Method(type, method);
                if (original == null)
                {
                    CoopPlugin.Log.LogWarning($"Patch target missing: {type.Name}.{method}");
                    return;
                }
                h.Patch(original, prefix: prefix, postfix: postfix);
            }
            catch (Exception e)
            {
                CoopPlugin.Log.LogWarning($"Patch failed for {type.Name}.{method}: {e.Message}");
            }
        }

        // ---------------- host ----------------

        public void HostTick(float dt, bool inGame)
        {
            if (!inGame)
                return;
            if (!_gate.Due(dt))
                return;
            Guarded("host", () =>
            {
                var td = CPlayerData.m_TournamentData;
                if (td == null)
                    return;
                var message = BuildState(td);
                if (!_gate.ShouldSend(JsonConvert.SerializeObject(message).GetHashCode()))
                    return;
                BroadcastState?.Invoke(message);
            });
        }

        // No HostApplyOp / SendOp: the joiner never sends tournament ops - scheduling
        // is blocked client-side with a toast rather than forwarded.

        // ---------------- client ----------------

        public void ClientApplyState(TournamentStateMessage message)
        {
            ApplyingRemote = true;
            try
            {
                Guarded("apply", () => ClientApplyInner(message));
            }
            finally { ApplyingRemote = false; }
        }

        private void ClientApplyInner(TournamentStateMessage message)
        {
            message.Player.Apply();
            var td = CPlayerData.m_TournamentData;
            if (td == null)
            {
                CPlayerData.m_TournamentData = td = new TournamentData();
            }

            byte flags = message.Flags;
            td.m_IsHostingTournament = (flags & 1) != 0;
            td.m_IsTournamentDay = (flags & 2) != 0;
            td.m_IsTournamentDayOver = (flags & 4) != 0;
            td.m_TournamentMaxPlayerCount = message.MaxPlayerCount;
            td.m_TournamentSignedUpCustomerCount = message.SignedUpCustomerCount;
            td.m_TournamentFinishedCurrentRoundCustomerCount = message.FinishedCurrentRoundCustomerCount;
            td.m_TournamentCurrentRound = message.CurrentRound;
            td.m_TournamentMaxRound = message.MaxRound;
            td.m_TournamentFee = message.Fee;
            td.m_TournamentTotalValue = message.TotalValue;

            // prize catalog: mutate the vanilla 4-slot list in place so screens that
            // index m_PrizeDataList[i] never see it shorter than they expect
            if (td.m_PrizeDataList == null)
                td.m_PrizeDataList = new List<TournamentPrizeDataList>();
            int lists = message.PrizeSlots.Count;
            while (td.m_PrizeDataList.Count < lists)
                td.m_PrizeDataList.Add(new TournamentPrizeDataList { m_PrizeDataList = new List<TournamentPrizeData>() });
            for (int i = 0; i < lists; i++)
            {
                var slot = td.m_PrizeDataList[i];
                if (slot.m_PrizeDataList == null)
                    slot.m_PrizeDataList = new List<TournamentPrizeData>();
                slot.m_PrizeDataList.Clear();
                var dtoSlot = message.PrizeSlots[i];
                for (int j = 0; j < dtoSlot.Prizes.Count; j++)
                {
                    var pe = dtoSlot.Prizes[j];
                    var p = new TournamentPrizeData();
                    if (pe.HasCard)
                        p.m_CardData = pe.Card;
                    // host id -> ours (already translated by the DTO deserialize); a prize
                    // from a pack only the host has becomes EItemType.None and the prize slot
                    // just shows nothing, which is what an unresolvable prize did before
                    // translation existed
                    p.m_ItemType = pe.ItemType;
                    p.m_Count = pe.Count;
                    slot.m_PrizeDataList.Add(p);
                }
            }

            // bracket digest
            int n = message.Bracket.Count;
            var digest = new List<PairingEntry>(n);
            for (int i = 0; i < n; i++)
            {
                var be = message.Bracket[i];
                var e = new PairingEntry();
                e.SortedIndex = be.SortedIndex;
                e.ModelIndex = be.ModelIndex;
                byte f = be.Flags;
                e.IsFemale = (f & 1) != 0;
                e.IsWin = (f & 2) != 0;
                e.HasResult = (f & 4) != 0;
                e.WinCount = be.WinCount;
                e.WinPoints = be.WinPoints;
                e.OMW = be.OMW;
                e.OOMW = be.OOMW;
                digest.Add(e);
            }

            // the heal broadcast repeats unchanged state every 15s; skip the UI churn
            // (ShowPairingScreen resets every panel) when nothing actually moved
            int hash = JsonConvert.SerializeObject(message).GetHashCode();
            if (hash == _clientHash)
                return;
            // A join snapshot may arrive before the board exists. Keep retrying the
            // same payload on heal until the scene can actually display it.
            if (RefreshBoards(td, digest))
                _clientHash = hash;
        }

        /// <summary>Client: the pairing board and shelf screen mesh are normally driven
        /// by day-start events, which the mod suppresses on the joiner - so we gate them
        /// here, exactly the way TournamentPrizeShelf.CheckTournamentScreenVisibility does.</summary>
        private bool RefreshBoards(TournamentData td, List<PairingEntry> digest)
        {
            var cm = Cm();
            if (cm == null || cm.m_TournamentPairingScreen == null)
                return false;
            var screen = cm.m_TournamentPairingScreen;
            bool showBoard = td.m_IsTournamentDay || td.m_IsTournamentDayOver;

            try
            {
                screen.gameObject.SetActive(showBoard);
                var shelves = ShelfManager.GetTournamentPrizeShelfList();
                for (int i = 0; i < shelves.Count; i++)
                {
                    if (shelves[i] == null || FiScreenMesh == null)
                        continue;
                    var mesh = FiScreenMesh.GetValue(shelves[i]) as GameObject;
                    if (mesh != null)
                        mesh.SetActive(showBoard);
                }
            }
            catch (Exception e)
            {
                CoopPlugin.Log.LogWarning("TournamentSync board vis: " + e.Message);
                return false;
            }
            if (!showBoard)
            {
                screen.ShowPairingScreen(isShow: false, 0);
                return true;
            }

            // full repaint: ShowPairingScreen resets the panels, then we repopulate from
            // the digest with fabricated CustomerTournamentData - UpdateCustomerData only
            // reads the scalar fields we carry
            screen.ShowPairingScreen(isShow: true, td.m_TournamentMaxPlayerCount);
            screen.UpdateCurrentRound(td.m_TournamentCurrentRound, td.m_TournamentMaxRound);
            if (td.m_IsTournamentDayOver)
                screen.OnTournamentEnded();
            int panels = screen.m_TournamentPairingUIGrpList != null ? screen.m_TournamentPairingUIGrpList.Count : 0;
            for (int i = 0; i < digest.Count; i++)
            {
                var e = digest[i];
                if (e.SortedIndex / 2 >= panels)
                    continue;
                screen.OnCustomerRegisterStart(e.SortedIndex, e.ModelIndex, e.IsFemale);
                var ctd = new CustomerTournamentData
                {
                    m_TournamentCustomerSortedIndex = e.SortedIndex,
                    m_IsTournamentWin = e.IsWin,
                    m_HasRegisteredTournamentResult = e.HasResult,
                    m_TournamentWinCount = e.WinCount,
                    m_TournamentWinPoints = e.WinPoints,
                    m_TournamentOMW = e.OMW,
                    m_TournamentOOMW = e.OOMW,
                };
                screen.m_TournamentPairingUIGrpList[e.SortedIndex / 2].UpdateCustomerData(ctd);
            }
            return true;
        }

        private struct PairingEntry
        {
            public int SortedIndex;
            public int ModelIndex;
            public bool IsFemale;
            public bool IsWin;
            public bool HasResult;
            public int WinCount;
            public int WinPoints;
            public int OMW;
            public int OOMW;
        }

        // ---------------- wire / hash ----------------

        private static TournamentStateMessage BuildState(TournamentData td)
        {
            var msg = new TournamentStateMessage
            {
                Player = TcgPlayerState.Capture(),
                Flags = (byte)((td.m_IsHostingTournament ? 1 : 0)
                             | (td.m_IsTournamentDay ? 2 : 0)
                             | (td.m_IsTournamentDayOver ? 4 : 0)),
                MaxPlayerCount = td.m_TournamentMaxPlayerCount,
                SignedUpCustomerCount = td.m_TournamentSignedUpCustomerCount,
                FinishedCurrentRoundCustomerCount = td.m_TournamentFinishedCurrentRoundCustomerCount,
                CurrentRound = td.m_TournamentCurrentRound,
                MaxRound = td.m_TournamentMaxRound,
                Fee = td.m_TournamentFee,
                TotalValue = td.m_TournamentTotalValue,
            };

            var lists = td.m_PrizeDataList;
            int lc = lists != null ? Mathf.Min(lists.Count, 8) : 0;
            for (int i = 0; i < lc; i++)
            {
                var slot = new TournamentPrizeSlot();
                var inner = lists[i] != null ? lists[i].m_PrizeDataList : null;
                int ec = inner != null ? Mathf.Min(inner.Count, 64) : 0;
                for (int j = 0; j < ec; j++)
                {
                    var p = inner[j];
                    bool hasCard = p != null && p.m_CardData != null;
                    slot.Prizes.Add(new TournamentPrizeEntry
                    {
                        HasCard = hasCard,
                        Card = hasCard ? p.m_CardData : null,
                        // item prizes are EItemTypes (a modded id space) - the card above
                        // already goes through the WriteCard chokepoint
                        ItemType = p != null ? p.m_ItemType : (EItemType)0,
                        Count = p != null ? p.m_Count : 0,
                    });
                }
                msg.PrizeSlots.Add(slot);
            }

            // bracket digest straight from the host's live sorted list (the same list
            // the vanilla pairing board renders from)
            var cm = Cm();
            var sorted = cm != null ? cm.m_TournamentSortedCustomerList : null;
            int n = sorted != null ? Mathf.Min(sorted.Count, 64) : 0;
            for (int i = 0; i < n; i++)
            {
                var c = sorted[i];
                var ctd = c != null ? c.GetCustomerTournamentData() : TcgPlayerState.PlayerBracket(i);
                if (ctd == null)
                {
                    continue;
                }
                msg.Bracket.Add(new TournamentBracketEntry
                {
                    SortedIndex = (byte)Mathf.Clamp(ctd.m_TournamentCustomerSortedIndex, 0, 255),
                    ModelIndex = c != null ? c.GetCustomerModelIndex() : -1,
                    Flags = (byte)(((c != null && c.m_IsFemale) ? 1 : 0)
                                 | (ctd.m_IsTournamentWin ? 2 : 0)
                                 | (ctd.m_HasRegisteredTournamentResult ? 4 : 0)),
                    WinCount = ctd.m_TournamentWinCount,
                    WinPoints = ctd.m_TournamentWinPoints,
                    OMW = ctd.m_TournamentOMW,
                    OOMW = ctd.m_TournamentOOMW,
                });
            }
            return msg;
        }

    }
}
