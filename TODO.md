# Game 1.0 compatibility checklist

CardShopCoop version: **1.3.5**. Previously documented tested game version:
**0.70.3**. Target: **TCG Card Shop Simulator 1.0**.

This checklist records a source-based assessment. Two-player runtime verification
is still required. P0 blocks joining; P1 blocks release within the declared support
scope. The official [1.0 release notes](https://steamcommunity.com/app/3070070/allnews/)
describe the new cards, playable TCG, and customer/save changes.

## M1 — Reproduce and restore joining

- [ ] **P0: Fix avatar initialization aborting the join (runtime sign-off pending).**
  The reported pre-fix `Hello` exception came from `GetEditorCustomization()` calling
  `Initialize()`, failing in `setHair()` and preventing `SendWorldTo()` from sending
  the welcome and world data.
  - [ ] Record exact host/guest game builds, plugin versions, and installed mods.
  - [ ] Reproduce with fresh and existing appearances and both character genders.
  - [x] Log preset name, initialization state, hair/apparel table and object counts,
    and stored appearance list counts to identify the exact invalid index.
  - [x] Verify the prefab initialization sequence against the installed game's
    decompiled methods before implementing the fix.
  - [x] Correct the cloned initialization flag/runtime-list mismatch and normalize
    copied presets (verified with a behavioral test fixture). The trace alone does not establish
    which list is invalid or prove that an old saved hairstyle caused the error.
- [x] **P1: Discard failed editor templates.** Cache only fully initialized templates,
  destroy failed candidates, and suppress repeated attempts until prefab change or
  session reset. Regression harness passes; Unity destruction remains a runtime check.
- [x] **P1: Protect joining from optional appearance failures.** Default initialization
  failure preserves saved appearance; failed optional snapshot preparation returns no
  model message so world transfer can proceed. Failed remote dressing uses a capsule
  marker. Exception containment is covered by the harness; actual transfer and visuals
  remain runtime checks.
- [ ] Verify first join, repeated joins, and reconnects with fresh and saved appearances.
- [ ] Inject an appearance initialization failure and verify that world transfer still
  completes with a safe fallback and no partially initialized template is reused.

**Implementation validation:** Restore, Release build without deployment, and required
whitespace verification passed (build: 0 errors, 87 warnings). All 37 avatar regression
checks passed. The harness covers copied presets,
slot normalization, failed-candidate cleanup, retry suppression/reset, and optional
snapshot exceptions. See [the harness instructions](tests/AvatarInitialization/README.md).
The local Steam manifest identifies build **25304508**; M1 validation used plugin
version **1.2.0**. Host/guest runtime versions and mod sets have not been collected. The exact
reported invalid index and two-player behavior remain unverified; M1 is not fully signed off.

**Exit criteria:** Guests can join reliably, including when appearance initialization fails.

## M2 — Restore existing shop compatibility

- [x] **P1: Audit Harmony patches and reflected fields against game 1.0.** The metadata
  harness checks 326 literal member references and 172 hooks, including signatures,
  parameter names/types, and instance/result types. No failures on the installed build.
  - [x] Review dynamic base-game lookups: native save aliases and optional object tags;
    patch helpers resolve the literal registrations checked above.
  - [ ] Verify live Harmony installation and gameplay call paths in a two-player session.
    Optional TV/Grading Overhaul integration testing remains part of the mod matrix in M4.
- [x] **P1: Implement Ascension market synchronization.** Added its full snapshot table,
  capture/apply paths, checksums, and guest base-price generation guard.
  - [x] Confirm existing generic deltas captured only writes observed by the host; they
    were not a complete join/recovery snapshot for Ascension.
  - [x] Include Ascension in the full market state and keep it out of the modded-delta path.
  - [x] Cover wire rounding, base prices (including zero), null/short rows, aliases,
    repeated updates, and history preservation with 14 production-helper checks.
  - [x] Audit pack opening, collection operations, trading, grading, and displays:
    these use the game's expansion-aware CardData / CPlayerData APIs. Card identity
    copies include expansion, foil, destiny, champion, and grading fields. No additional
    fixed expansion list was found in those paths.
  - [ ] Verify Ascension pack pulls, prices, sales, grading, and collection counts on
    both players after join, a daily price change, reconnect, and save/reload.
- [x] **P1: Audit updated customer and save behavior.** Native save/load retains the
  Ascension tables and customer tournament data; transferred saves use the complete
  native save object. No custom migration was needed for these fields.
  - [x] Trace tournament load through saved gender/model data and UpdateCharacterModel.
  - [x] Prepare customer mirrors before activation and apply the host's named preset;
    normalize detached presets for later wardrobe updates as well.
  - [x] Resolve the shared material bank normally found by Start before dressing an
    inactive clone, while preserving a bank already assigned to that clone.
  - [x] Extend avatar regressions to named tournament presets and material initialization
    (45 avatar checks now pass, including the earlier M1 coverage).
  - [ ] Verify tournament customer appearance, pairing-board state, persistence, and
    mirrored visibility after joining and reconnecting.
  - [ ] Verify transferred fresh and migrated saves in-game; verify save/reload and
    confirm the guest's personal save slots remain unchanged.
- [ ] Run two-player regression checks for checkout, workers, boxes, shelves,
  furniture, trading, grading, and card displays. Check for lost/duplicated cards,
  divergent prices, stale customer bodies, and register or worker lockups.

**Implementation validation:** Build, required whitespace verification, market/avatar
regressions, and the existing shelf-box regression harness pass. The metadata audit
also passes 61 save/market integration checks, covering all eight native market tables.
Game assembly MVID: `337b87e1-9427-48d9-aa0a-905bf505d6c0` (matches the reported crash).
Plugin version is now **1.3.0**, with wire version **103**, because the market message
contract changed. **Both players must update.**

Harness instructions: [metadata audit](tests/GameCompatibility/README.md),
[market tests](tests/MarketCompatibility/README.md), and
[avatar tests](tests/AvatarInitialization/README.md).
Two-player runtime checks remain pending; M2 is not fully signed off. These M2
results predate the playable TCG implementation recorded in M3 below.

**Exit criteria:** Host and guest agree after joining, shop interactions, daily price
updates, reconnecting, and saving/reloading. No card loss or duplication is observed.

## M3 — Define and implement new 1.0 feature support

Implementation uses host-controlled TCG play. Independent guest battles, deck editing,
and tournament participation are unsupported and show an explanation before entry.
Guests can keep running the shop and reading the rulebook.

- [x] Trace the game's playable TCG, deck editing, reward, and tournament flows.
  Inspected `PlayTableGame`, `InteractablePlayTable`, deck/workbench screens,
  `CustomerManager`, and native save/load; metadata checks validate the installed targets.
- [x] Define host authority and guest permissions for each supported action.
  Guest entry and mutation callbacks are guarded; hosting cannot begin halfway through
  local deck editing or a battle. Joining already requires the Title screen.
- [x] Implement synchronization of relevant play-table state within the supported scope.
  Shared table props and customer visuals retain their existing channels; player battle
  occupancy is now mirrored. Host-side kick/move/box requests reject active battle tables.
  The live playable card board and independent guest turns remain unsupported.
  - [ ] Verify table visuals and protection through entrance, battle, exit, and recovery.
- [ ] Verify deck persistence and card ownership during deck editing in-game.
  - [x] Capture/apply decks, selected deck, cosmetic IDs, and compact card identity data.
    Copies are detached; repeated snapshots never call inventory mutations.
  - [x] Verify native save/load contains these fields and native deck changes use the
    existing shared card-add/remove channel. Regression checks cover snapshot recovery.
  - [ ] Exercise create/edit/delete/paste/import, save/reload, and simultaneous guest
    inventory actions; confirm total card counts and identities remain correct.
- [x] Implement host-authoritative rewards and player tournament state within scope.
  Tournament snapshots include participation, player data and the null-customer player
  bracket entry. Daily duel counts are shared; prize shelves retain existing sync.
  Battle gifts remain in the host's hand and use normal item/card channels when placed
  or opened. Duplicate result/exit/gift callbacks are guarded, including rematches.
  - [ ] Verify actual gifts, placement/opening, tournament outcomes and prize contents.
- [x] Clearly gate unsupported interactions, with guest-facing explanations.
- [ ] Test simultaneous interactions, joining during play, and disconnects during play.
- [ ] Verify end-to-end that rewards cannot duplicate and cards cannot disappear.
  - [x] Pass helper regressions for duplicate callbacks and idempotent state replacement.
  - [ ] Verify Unity/Harmony execution, partial reward failures, scene reset, and two-player
    inventory conservation using the runtime matrix linked below.

**Implementation validation:** Restore, Release build without deployment, and required
whitespace verification pass (0 errors, 90 obsolete-API warnings). The new TCG harness
passes 37 checks; existing avatar, market, and shelf-box harnesses pass 71 checks.
The metadata audit passes 358 literal member lookups, 202 hooks, and 92 game integration
contracts. These are automated helper/metadata results, not two-player runtime results.

Plugin **1.3.1**, wire **103**, reflects the changed tournament/table/report contract.
Local metadata target: Steam build **25304508**, assembly MVID
`337b87e1-9427-48d9-aa0a-905bf505d6c0`. Host/guest runtime build IDs and mod sets remain
unrecorded. **Both players must update.** No deployment or publishing was performed.

See [TCG harness and pending runtime matrix](tests/TcgCompatibility/README.md) and
[game metadata audit](tests/GameCompatibility/README.md). **M3 runtime sign-off remains pending.**

**Exit criteria:** Supported features produce matching persistent outcomes; unsupported
interactions are clearly identified and safely gated.

## M4 — Validate and release

**Additional release dependency:** Resolve and verify the confirmed P0 join blocker
in [M5](#m5--restore-scene-loading-after-save-transfer--p0) before sign-off. Triage
M6–M8 separately; the new 1.3.3 findings in M9–M13 also require triage, with M9 and
M11 treated as P1 release dependencies. Any further confirmed P0/P1 defects also block release. The automated
results below remain historical evidence, not validation of the newly reported failure.

- [ ] Run two-player regression tests on fresh 1.0 saves and migrated 0.70.3 saves.
- [ ] Test the supported baseline mod set, then supported optional mod combinations.
- [ ] Resolve all P0/P1 defects within the declared support scope.
- [x] Document remaining limitations and validation targets in `README.md`: previously
  documented gameplay version **0.70.3**, automated target **1.0 / Steam build 25304508**.
  Game 1.0 two-player sign-off and optional mod combinations are explicitly pending.
- [x] Run `dotnet restore src/CardShopCoop/CardShopCoop.csproj`.
- [x] Run `dotnet format src/CardShopCoop/CardShopCoop.csproj whitespace --verify-no-changes --no-restore`;
  fix formatting and repeat verification if necessary.
- [x] Build with `dotnet build src/CardShopCoop/CardShopCoop.csproj -c Release`.
  Use `-p:Deploy=true` only when local deployment is intended and the game is closed.
- [x] Choose the release version according to the repository rules: patch for fixes
  without wire-contract changes; minor for new messages or substantial changes to
  message layout, encoding, semantics, or routing; major for a deliberate breaking
  protocol transition.
- [x] Bump the version only in `Directory.Build.props`. M3 already selected **1.3.1**
  for the changed tournament/table/report contract; tooling/docs require no further bump.
- [x] Add player-facing release notes to `CHANGELOG.md`. The **1.3.1** section exists. If the wire version or
  required mod set changes, end the release section with **Both players must update.**
- [ ] Verify that both players use the identical plugin version.
- [x] Keep local candidate ZIPs in `dist/release/`; package the exact current changelog
  section for reuse in release descriptions. Candidate packaging is verified; no release
  was deployed or published.

**Local validation tooling:** `python tools/validate_compatibility.py` runs the complete
local check sequence and stops at the first failure. Add `--package` to produce an
explicitly unverified candidate ZIP after successful checks. It never deploys or publishes.
See [contributor instructions](CONTRIBUTING.md#checks-before-a-pull-request).

**Verified on 2026-09-14:** Restore, required formatting check, Release build, 108 helper
checks, and the game metadata audit (358 member lookups, 202 hooks, 92 integration
contracts) pass. Four validation-tool regressions also pass, covering early failure,
changed binaries, archive contents, and exact changelog extraction. The local run
records SDK **10.0.111**, Steam build **25304508**, game/plugin hashes, source commit
and checkout status, and an on-disk plugin binary inventory in ignored
`diag/compatibility/`. This inventory does not establish loaded mods, content-pack
parity, or the state of another PC.

**Remaining blocker:** All unchecked gameplay items in M1–M4 require an actual host/guest
session, fresh and migrated saves, and the intended mod matrix. Those gameplay runs
have not been performed here. No live result has been inferred from the helper
tests, metadata audit, or candidate build. **Release sign-off remains pending.**

**Exit criteria:** Build and formatting checks pass, two-player validation passes,
and the release declares its tested compatibility and limitations.

## New evidence — Supplied 1.3.2 log

The supplied guest log reports CardShopCoop **1.3.2**, game **1.00**, Unity
**6000.0.66f2**, Windows x64, BepInEx **5.4.23.5**, and Configuration Manager **19.0**.
Host build and mod details remain unverified. Player names and Steam IDs are omitted.

The save arrives before scene loading fails; this is evidence of transfer progress,
not a successful join. The log does not establish another avatar initialization
failure or complete any previously pending gameplay checks.

## M5 — Restore scene loading after save transfer · P0

**Confirmed:** The guest receives the save, then loading `Start` fails because the
scene is unavailable. The native loading coroutine subsequently throws a
null-reference exception. The current native startup constant and title-screen flow
use `StartOptimized`.

- [x] Replace the legacy scene name in `SaveTransfer.ForceLoadSlot` with the game's
  current startup scene, verified against its native constant and title-screen flow:
  `StartOptimized`.
- [x] Validate scene availability before starting the load.
- [x] Handle rejected, failed, or stalled loads with a clear error and session
  cleanup while preserving guest-save protection.
- [x] Extend regression coverage to scene names and failure recovery; the existing
  method-signature audit missed this mismatch.
- [ ] Verify first join, reconnect, automatic hosting, and fresh/migrated saves in-game.

**Implementation validation (1.3.3, wire 103 unchanged):** The loader uses the
native startup constant and checks availability before sidecar/save application.
An owned coroutine follows the native loading-screen/scene sequence and exposes
rejections and exceptions. A 180-second unscaled deadline covers world application
and shop readiness, including automatic hosting. Failure ends the session and
returns to Title once any submitted scene operation completes; protection stays
active during recovery. A stuck Unity operation or failed Title recovery requires
a restart. Reconnects are blocked until recovery finishes.

The complete local validation sequence passes: restore, required whitespace check,
Release build (0 errors, 90 obsolete-API warnings), 32 new world-loading helper
checks, 108 existing helper checks, four validation-tool checks, and the metadata
audit (358 member references, 202 hooks, 106 integration contracts). The audit now
checks the native startup constant and title-screen scene names, not just method
signatures. See [world-loading tests and runtime matrix](tests/WorldLoading/README.md).
No deployment or two-player testing was performed; **M5 runtime sign-off remains
pending**, including personal-save preservation in an actual guest session.

This milestone contributes to [M1's join acceptance criteria](#m1--reproduce-and-restore-joining)
but addresses a separate scene-loading failure, not another confirmed avatar failure.

**Exit criteria:** The guest reaches a usable shop, or receives a recoverable failure
without damaging personal saves.

## M6 — Handle Steam initialization correctly · P2

**Observed:** Persona lookup throws “Steamworks is not initialized,” although an
invite and connection succeed later. This caught startup error is distinct from the
confirmed scene-loading blocker.

- [ ] Distinguish Steam assembly presence, a running Steam client, and initialized
  Steamworks APIs.
- [ ] Defer persona, lobby, and invitation API calls until initialization completes;
  retry without repeated warning noise.
- [ ] Make startup status accurately describe current readiness.
- [ ] Test delayed initialization, unavailable Steam, invitation handling after
  readiness, and LAN fallback.

**Exit criteria:** Expected startup delays do not generate exceptions or prevent
later connections.

## M7 — Make optional-mod diagnostics actionable · P2

**Observed:** Grading Overhaul and TV integration emit numerous missing-type/member
warnings despite those mods not being loaded.

- [ ] Detect absent optional plugins before probing their members.
- [ ] Report an absent integration once at informational level.
- [ ] Preserve actionable warnings when an installed integration has incompatible APIs.
- [ ] Avoid warning about an absent enum registry when both peers have no modded IDs;
  retain warnings for failed registry transfer.
- [ ] Test absent, compatible, and incompatible optional integrations.

**Exit criteria:** Vanilla startup is quiet, while genuine integration failures
remain visible.

## M8 — Verify platform assumptions and attribute remaining warnings

**Unverified:** The log reports Xbox-container saving; that label alone does not
establish the actual save backend. The HTTP 404 and `DontDestroyOnLoad` warnings lack
sufficient attribution.

- [ ] Verify the active save backend against native behavior and save-completion
  evidence; distinguish detection heuristics from confirmed results.
- [ ] Track outdated Unity 2021.3 documentation against the observed Unity 6 runtime.
- [ ] Obtain host-side evidence and compare save-transfer behavior across the intended
  Steam/Game Pass matrix.
- [ ] Identify the HTTP request and object-lifetime warning sources before assigning fixes.
- [ ] Record whether those warnings affect gameplay; do not classify them as join
  blockers without evidence.

**Exit criteria:** Platform/save claims are supported, and remaining warnings have
an identified owner and impact or an explicit unresolved status.

## New evidence — Supplied 1.3.3 log

Reviewed `LogOutput.log` on **2026-09-15**. It records CardShopCoop **1.3.3**,
game **1.00**, Unity **6000.0.66f2**, Windows x64, BepInEx **5.4.23.5**, and
Configuration Manager **19.0**. BepInEx skips an older **1.3.1** copy. The same
process first acts as a guest, then as a host; the later handshake reports both
games as 1.00 with the same Unity version. Exact peer plugin inventory and Steam
build IDs still need collecting. Player names, addresses, and Steam IDs are omitted.

`Join world load completed in 1.25s; resuming co-op sync` and subsequent client
state traffic provide runtime evidence that M5's scene-loading fix works in this
session. This does not complete its fresh/migrated-save, reconnect, recovery, or
personal-save preservation matrix. Historical M1–M5 validation above remains scoped
to its original runs.

The new failures are tracked below. Existing M6–M8 remain open: Steam errors follow
the bridge-ready message, absent optional integrations still warn (including EPL),
and the save-backend label still needs verification. Wrong-password rejections are
expected authentication behavior; failed UPnP discovery alone does not prove a
connection defect because connections subsequently succeed.

## M9 — Restore appearance previews and remote dressing · P1

**Confirmed:** 747 identical `ArgumentOutOfRangeException` traces follow
`CoopCore.Update → UpdatePreview → SpawnPreview → Initialize → LoadFromJSON →
ApplyCharacterVars → setHairByName → setHair`. Remote dressing also falls back to
a basic marker twice, for male and female models. The precise invalid list/index
is unproven; this is a preview-path failure beyond M1's editor-template coverage.

- [ ] Reproduce preview opening, preset changes, gender switches, closing/reopening,
  and remote appearance updates with fresh and saved appearances on both peers.
- [x] Capture initialization flags, selected preset/slot, and hair/apparel table,
  runtime-object, and stored-data counts for preview and remote clones.
- [x] Apply the existing detached-preset and clone-preparation protections to every
  affected path. Preview and remote clones now initialize fresh while inactive;
  default reapplication uses detached, normalized presets.
- [x] Contain preview initialization/deserialization failures, destroy failed clones
  and temporary holders, and prevent repeated attempts every update for unchanged
  failing input. Provide a usable fallback and deliberate retry/reset behavior.
- [x] Extend avatar regressions to preview failure cleanup and retry/reset behavior.
- [ ] Verify in Unity that no orphan customer objects or repeated errors accumulate.
- [ ] Verify remote dressing separately; a working capsule fallback does not establish
  that supported appearances render correctly.

**Implementation validation:** Plugin **1.3.4**, wire **103** (unchanged).
Restore, Release build without deployment (0 errors, 91 obsolete-API warnings),
required whitespace verification, all 69 avatar helper checks, and the metadata
audit (358 member references, 202 hooks, 106 integration contracts) pass. The preview
owner contains initialization, JSON parsing, and dressing failures and destroys its
holder and child clone. A capsule replaces a failed preview. Changed appearance,
gender, prefab, or NSFW setting, reopening the editor, and session reset allow retry.
Remote clones use the same fresh preparation and retain a detached clothed preset
when appearance JSON is absent or unreadable. Failure logs include selected model,
initialization flags, and before/after slot counts.

The native `Initialize`, `LoadFromJSON`, `ApplyCharacterVars`, `setHair`, and
`Customer.RandomizeCharacterMesh` bodies were inspected. The inherited flag can skip
runtime-slot creation; randomization also skips dressing when that flag is already
set. These source findings explain the unsafe paths but do not identify the exact
invalid index in the supplied log. See the expanded
[avatar regression and runtime matrix](tests/AvatarInitialization/README.md).
No deployment or two-player testing was performed. **M9 runtime sign-off remains pending.**

**Exit criteria:** Supported presets render locally and remotely; invalid appearance
data produces a controlled fallback without interrupting updates or leaking clones.

## M10 — Respect component dependencies when creating mirrors · P2, impact pending

**Confirmed:** Unity rejects removing `Seeker` because `SimpleSmoothModifier`
depends on it 11 times. The log has no caller stack for these errors. Avatar clone
cleanup currently removes behaviours by enumeration order, making it a candidate
to inspect alongside customer/worker mirror cleanup.

- [x] Inspect installed pathfinding dependencies and native customer/worker lifecycle.
  `SimpleSmoothModifier` and `AIBase` require `Seeker`; `MonoModifier` registers and
  unregisters through enable/disable callbacks. Customer `Start` reads its Seeker.
- [ ] Attribute the supplied log's 11 errors to exact runtime clones/callers. The
  avatar removal order is a confirmed unsafe path, but the log has no caller stack.
- [x] Remove dependent modifiers before their required components, and keep clones
  inactive until preparation is complete where their lifecycle requires it.
- [ ] Verify preview, remote-player, customer, and worker creation/destruction through
  joins, appearance changes, disconnects, and scene reloads.
- [ ] Confirm mirrored objects retain no active local AI/pathfinding and preserve
  host-controlled movement; escalate to P1 if gameplay interference is reproduced.

**Implementation validation:** Plugin **1.3.5**, wire **103** (unchanged).
Preview and remote-avatar cleanup now reads inherited `RequireComponent` dependencies
and removes dependents first. If a retained cosmetic component still needs a removable
component, the prerequisite remains disabled and a contextual warning identifies it.
NPC mirrors stay inactive through preparation; local logic is disabled using base types,
including all Pathfinding behaviours and native navigation components. Worker interaction
controls and cosmetic helpers retain their existing roles.

Release build (0 warnings, 0 errors), restore, required whitespace verification,
20 new mirror-cleanup checks, 69 avatar checks, four validation-tool checks, and the
metadata audit (358 member references, 202 hooks, 113 integration contracts) pass.
The new harness models Unity dependency rejection and is included in the standard
validation script. See [mirror cleanup tests and runtime matrix](tests/MirrorComponents/README.md).
No deployment or two-player testing was performed. **M10 runtime sign-off remains pending**,
including appearance-mod compatibility, worker UI, and host-controlled movement.

**Exit criteria:** Clone cleanup emits no dependency errors and leaves only the
components needed for mirrored behavior.
