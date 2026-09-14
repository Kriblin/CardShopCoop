using System.Collections.Generic;
using Newtonsoft.Json;

namespace CardShopCoop.Sync
{
    // Persistent TCG data only. Applying this snapshot must never call AddCard,
    // ReduceCard or spawn rewards: the existing card/item channels own those changes.
    public sealed class TcgPlayerState
    {
        public List<TcgDeckState> Decks = new List<TcgDeckState>();
        public int SelectedDeck;
        public bool Registered;
        public CustomerTournamentData Tournament = new CustomerTournamentData();

        public static TcgPlayerState Capture()
        {
            var state = new TcgPlayerState
            {
                SelectedDeck = CPlayerData.m_CurrentSelectedDeckIndex,
                Registered = CPlayerData.m_IsPlayerRegisteredForTournament,
                Tournament = CPlayerData.m_PlayerTournamentData,
            };
            if (CPlayerData.m_DeckCompactCardDataList != null)
                foreach (var deck in CPlayerData.m_DeckCompactCardDataList)
                    state.Decks.Add(new TcgDeckState
                    {
                        Name = deck.deckName,
                        DeckBox = (EItemType)deck.deckBoxIndex,
                        Playmat = (EItemType)deck.playmatIndex,
                        Cards = deck.compactCardDataAmountList,
                    });
            return Copy(state);
        }

        public void Apply()
        {
            // Finish preparing detached data before replacing any live field. A repeated
            // heal or reconnect replaces state, without moving cards a second time.
            var state = Copy(this);
            var decks = new List<DeckCompactCardDataList>();
            foreach (var deck in state.Decks)
                decks.Add(new DeckCompactCardDataList
                {
                    deckName = deck.Name,
                    deckBoxIndex = (int)deck.DeckBox,
                    playmatIndex = (int)deck.Playmat,
                    compactCardDataAmountList = deck.Cards,
                });
            CPlayerData.m_DeckCompactCardDataList = decks;
            CPlayerData.m_CurrentSelectedDeckIndex = state.SelectedDeck;
            CPlayerData.m_IsPlayerRegisteredForTournament = state.Registered;
            CPlayerData.m_PlayerTournamentData = state.Tournament ?? new CustomerTournamentData();
        }

        private static TcgPlayerState Copy(TcgPlayerState state) =>
            JsonConvert.DeserializeObject<TcgPlayerState>(JsonConvert.SerializeObject(state));

        // A null customer is the native player sentinel, but only at the player's
        // recorded position. Do not turn arbitrary empty slots into phantom players.
        internal static CustomerTournamentData PlayerBracket(int sortedIndex)
        {
            var player = CPlayerData.m_PlayerTournamentData;
            return CPlayerData.m_IsPlayerRegisteredForTournament && player != null
                && player.m_IsTournamentCustomer && player.m_TournamentCustomerSortedIndex == sortedIndex
                ? player : null;
        }
    }

    public sealed class TcgDeckState
    {
        public string Name;
        // Native deck cosmetics are integer item IDs; expose enums so WireSettings
        // translates these just like table props and tournament prizes.
        public EItemType DeckBox;
        public EItemType Playmat;
        public List<CompactCardDataAmount> Cards = new List<CompactCardDataAmount>();
    }
}
