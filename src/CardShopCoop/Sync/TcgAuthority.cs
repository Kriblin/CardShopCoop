using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace CardShopCoop.Sync
{
    // The native TCG uses a single local player, deck collection and battle controller.
    // Keep mutations on the host; guests receive data and ordinary shop item/card updates.
    internal static class TcgAuthority
    {
        private static ConditionalWeakTable<PlayTableGame, BattleCompletion> _battles = new ConditionalWeakTable<PlayTableGame, BattleCompletion>();

        public static bool HostOnly()
        {
            if (CoopCore.Role != CoopRole.Client)
                return true;
            return Deny("Deck editing, battles and tournament participation are host-only.");
        }

        private static bool Deny(string reason)
        {
            if (CoopCore.Instance != null)
            {
                CoopCore.Instance.RegisterLine = reason;
                CoopCore.Instance.RegisterLineTimer = 3f;
            }
            return false;
        }

        internal static bool PlayerAtTable(InteractableObject obj)
        {
            var table = obj as InteractablePlayTable;
            if (table == null)
                return false;
            // Includes the entrance/exit animations, before/after GetHasStartPlayerPlayCard.
            var seats = table.GetIsPlayerSeat();
            return table.GetHasStartPlayerPlayCard() || (seats != null && seats.Contains(true));
        }

        public static bool ProtectTable(InteractablePlayTable __instance)
        {
            if (CoopCore.Role == CoopRole.None || (!PlayerAtTable(__instance)
                && !(CoopCore.Role == CoopRole.Client && PlayTableSync.Active != null
                    && PlayTableSync.Active.IsPlayerOccupied(__instance))))
                return true;
            return Deny("This table is in use by the host's battle.");
        }

        // Hooks are installed when hosting starts. Do not switch authority halfway
        // through native deck callbacks or an already running battle coroutine.
        internal static bool CanStartHosting()
        {
            var workbench = UnityEngine.Object.FindObjectOfType<WorkbenchUIScreen>();
            var battle = UnityEngine.Object.FindObjectOfType<PlayTableGame>();
            var player = UnityEngine.Object.FindObjectOfType<InteractionPlayerController>();
            var table = battle == null ? null : AccessTools.Field(typeof(PlayTableGame), "m_CurrentInteractablePlayTable")?.GetValue(battle);
            return (player == null || !(bool)AccessTools.Field(typeof(InteractionPlayerController), "m_IsPlayTableGameMode").GetValue(player))
                && (workbench == null || !(bool)AccessTools.Field(typeof(WorkbenchUIScreen), "m_IsEditingDeck").GetValue(workbench))
                && (battle == null || (!battle.IsPlayTableGameMode() && table == null));
        }

        public static void ApplyPatches(Harmony h)
        {
            _battles = new ConditionalWeakTable<PlayTableGame, BattleCompletion>();
            Try(h, typeof(WorkbenchUIScreen), "OnPressEditDeckButton", new HarmonyMethod(typeof(TcgAuthority), nameof(HostOnly)));
            Try(h, typeof(PlayCardGameManager), "OpenDeckListScreen", new HarmonyMethod(typeof(TcgAuthority), nameof(HostOnly)));
            Try(h, typeof(PlayCardGameManager), "SetPlayTable", new HarmonyMethod(typeof(TcgAuthority), nameof(HostOnly)));
            Try(h, typeof(DeckListScreen), "DeleteDeck", new HarmonyMethod(typeof(TcgAuthority), nameof(HostOnly)));
            Try(h, typeof(DeckListScreen), "OnPressEmptySlot", new HarmonyMethod(typeof(TcgAuthority), nameof(HostOnly)));
            Try(h, typeof(DeckListScreen), "OnPressDeckEdit", new HarmonyMethod(typeof(TcgAuthority), nameof(HostOnly)));
            Try(h, typeof(DeckListScreen), "OnPressDeckSetActive", new HarmonyMethod(typeof(TcgAuthority), nameof(HostOnly)));
            Try(h, typeof(DeckEditScreen), "OpenDeckEditScreen", new HarmonyMethod(typeof(TcgAuthority), nameof(HostOnly)));
            Try(h, typeof(DeckEditScreen), "OnPressPasteButton", new HarmonyMethod(typeof(TcgAuthority), nameof(HostOnly)));
            Try(h, typeof(DeckEditScreen), "OnPressImportDeckDataUsingText", new HarmonyMethod(typeof(TcgAuthority), nameof(HostOnly)));
            Try(h, typeof(DeckEditScreen), "UpdateSelectedCardData", new HarmonyMethod(typeof(TcgAuthority), nameof(HostOnly)));
            Try(h, typeof(DeckEditScreen), "UpdateDeckCompactData", new HarmonyMethod(typeof(TcgAuthority), nameof(HostOnly)));
            Try(h, typeof(DeckCardPlusMinusScreen), "OnPressAddBtn", new HarmonyMethod(typeof(TcgAuthority), nameof(HostOnly)));
            Try(h, typeof(DeckCardPlusMinusScreen), "OnPressMinusBtn", new HarmonyMethod(typeof(TcgAuthority), nameof(HostOnly)));
            Try(h, typeof(DeckCardPlusMinusScreen), "OnPressRemoveAllBtn", new HarmonyMethod(typeof(TcgAuthority), nameof(HostOnly)));
            Try(h, typeof(DeckCardPlusMinusScreen), "OnInputTextUpdated", new HarmonyMethod(typeof(TcgAuthority), nameof(HostOnly)));
            Try(h, typeof(DeckboxPlaymatNameEditScreen), "OnPressDone", new HarmonyMethod(typeof(TcgAuthority), nameof(HostOnly)));
            Try(h, typeof(HostTournamentScreen), "OnPressPlayerSignUpTournament", new HarmonyMethod(typeof(TcgAuthority), nameof(HostOnly)));
            Try(h, typeof(HostTournamentScreen), "OnPressPlayerSignOutTournament", new HarmonyMethod(typeof(TcgAuthority), nameof(HostOnly)));
            Try(h, typeof(InteractablePlayTable), "OnRightMouseButtonUp", new HarmonyMethod(typeof(TcgAuthority), nameof(HostOnly)));
            Try(h, typeof(InteractablePlayTable), "StartPlayerCardGame", new HarmonyMethod(typeof(TcgAuthority), nameof(HostOnly)));
            Try(h, typeof(InteractablePlayTable), "ExitPlayerCardGame", new HarmonyMethod(typeof(TcgAuthority), nameof(HostOnly)));
            Try(h, typeof(PlayTableGame), "SetPlayTable", new HarmonyMethod(typeof(TcgAuthority), nameof(Begin)),
                new HarmonyMethod(typeof(TcgAuthority), nameof(Began)));
            Try(h, typeof(PlayTableGame), "EvaluateEndGameGift", new HarmonyMethod(typeof(TcgAuthority), nameof(Gift)));
            Try(h, typeof(PlayTableGame), "TakeEndGameGiftItem", new HarmonyMethod(typeof(TcgAuthority), nameof(HostOnly)));
            Try(h, typeof(PlayTableGame), "ReportWinner", new HarmonyMethod(typeof(TcgAuthority), nameof(ReportResult)));
            Try(h, typeof(PlayTableGame), "FinishLeaveGame", new HarmonyMethod(typeof(TcgAuthority), nameof(Leave)));
            Try(h, typeof(PlayTableGame), "Rematch", new HarmonyMethod(typeof(TcgAuthority), nameof(Rematch)));
            Try(h, typeof(InteractablePlayTable), "BoxUpObject", new HarmonyMethod(typeof(TcgAuthority), nameof(ProtectTable)));
        }

        public static bool Begin(PlayTableGame __instance, out bool __state)
        {
            __state = !__instance.IsPlayTableGameMode();
            return HostOnly();
        }

        public static void Began(PlayTableGame __instance, bool __state)
        {
            if (CoopCore.Role == CoopRole.Host && __state && __instance.IsPlayTableGameMode())
            {
                _battles.Remove(__instance);
                _battles.Add(__instance, new BattleCompletion());
            }
        }

        public static bool ReportResult(PlayTableGame __instance) => HostOnly()
            && (CoopCore.Role != CoopRole.Host || _battles.GetOrCreateValue(__instance).ReportResult());
        public static bool Rematch(PlayTableGame __instance) => HostOnly()
            && (CoopCore.Role != CoopRole.Host || _battles.GetOrCreateValue(__instance).Rematch());
        public static bool Leave(PlayTableGame __instance) => HostOnly()
            && (CoopCore.Role != CoopRole.Host || _battles.GetOrCreateValue(__instance).Leave());
        public static bool Gift(PlayTableGame __instance) => HostOnly()
            && (CoopCore.Role != CoopRole.Host || _battles.GetOrCreateValue(__instance).GrantGift());

        private static void Try(Harmony h, Type type, string method, HarmonyMethod prefix, HarmonyMethod postfix = null)
        {
            var original = AccessTools.Method(type, method);
            if (original == null)
                throw new MissingMethodException(type.FullName, method);
            h.Patch(original, prefix: prefix, postfix: postfix);
        }
    }
}
