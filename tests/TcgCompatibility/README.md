# Game 1.0 TCG compatibility regression harness

Run from the repository root with the normal local `GamePath` configuration:

```sh
dotnet run --project tests/TcgCompatibility/TcgCompatibility.csproj -c Release
```

The harness links the production `TcgPlayerState`, `TcgAuthority`, and
`BattleCompletion` helpers. Minimal game/Harmony/Unity stubs let their data and
permission logic run without starting Unity. Newtonsoft.Json comes from the local
game installation; game source and assemblies are not redistributed.

The 37 checks cover detached capture/apply, repeated snapshots and reconnects,
deleted decks, selected deck and cosmetic/card identity fields, nested tournament
state, the player bracket sentinel, guest denial messages, table protection,
rematches, duplicate result/exit/gift callbacks, and hosting during an existing
native interaction. Inventory methods throw if snapshot code attempts to call them.
The metadata harness in `../GameCompatibility` separately checks the installed
native save fields, card-transfer methods, reward coroutine, and Harmony targets.

## Supported scope

| Action | Host | Guest |
| --- | --- | --- |
| Create, edit, paste, import, delete, or select a deck | Native game behavior | Blocked with explanation; deck data follows host |
| Battle a customer or participate in a tournament | Native game behavior | Blocked; table props/occupancy and tournament results follow host |
| View the live playable TCG board or take a separate battle turn | Local host UI | Unsupported; entry blocked |
| Collect battle gifts | Native host hand, once per table visit | No separate gift claim; ordinary placement/opening uses existing item/card sync |
| Read rulebook and run ordinary shop tasks | Allowed | Allowed |
| Move, box up, or kick an active battle table | Protected during battle | Blocked locally when known and validated again by host |

Decks consume cards from the shared collection through native `ReduceCard` calls;
removing/deleting/pasting a deck returns cards through native `AddCard`. These calls
already use the inventory channel. The TCG snapshot only replaces deck and tournament
data, so recovery cannot repeat a card transfer or reward grant. Daily duel counts
are shared; permanent achievement counters retain the existing per-player behavior.
Tournament prize shelf contents continue to use the existing card/shelf channels.
No additional tournament reward is invented or granted by the snapshot.

Hosting cannot start while local deck editing or a battle is already underway.
Joining already requires the Title screen. A guest can join an established host
battle: the host keeps simulating it, and the guest receives the save plus periodic
TCG/table state. Guests do not own a battle coroutine that needs promotion when
someone disconnects. This flow still needs the runtime checks below.

## Pending two-player runtime checks

These have **not** been run. The harness does not execute Harmony patches, Unity
object lifecycles, native inventory transactions, network delivery, or save files.

Record both game build IDs, plugin version, installed mods, and fresh/migrated save
status for each run. Local metadata validation used Steam build **25304508**, game
assembly MVID `337b87e1-9427-48d9-aa0a-905bf505d6c0`, plugin **1.3.1**, wire **103**.
Host/guest runtime versions and mod sets remain unrecorded.

1. Create/edit/delete/paste/import decks, change the selected deck and cosmetics,
   and save/reload. Include Ascension, destiny and graded card identities. Count
   album cards plus cards in decks before/after; account separately for any trades,
   displays or held cards. Repeat while a guest opens packs or moves collection cards.
2. Try guest workbench deck editing, tournament signup/withdrawal, and right-click
   battle entry. Each should explain the restriction without removing cards, changing
   fees/signup counts, opening a battle camera, or trapping cursor/movement. Rulebook
   and ordinary workbench tasks must still work.
3. Play casual wins/losses/draws and rematches on the host. Attempt guest table kicks,
   moves and boxing during entrance, active play and exit. Verify host state rejects
   stale requests even before the guest's occupancy snapshot arrives.
4. Join/rejoin during each battle phase, finish the match, and reconnect after gifts.
   Disconnect guests during play; separately stop hosting during play and finish
   offline. Verify no duplicate gift items, lost held gifts, frozen controls or stale
   occupied tables. Place/open gifts and compare resulting items/cards on both peers.
5. Invoke duplicate result/leave/gift callbacks in a controlled development session,
   including a failure after partial item spawning. Confirm no second reward batch;
   inspect partial-failure behavior rather than assuming rollback or retry.
6. Sign up and withdraw on the host; play a complete tournament. Verify player picture,
   round results, placement, prize shelf contents, and reconnect/save persistence.
   Join while the board is unavailable; verify a later unchanged heal restores it.
7. Restart the scene/session and repeat a fresh battle. Confirm reward allowances
   reset for the new visit and never reset merely because a guest reconnects.
