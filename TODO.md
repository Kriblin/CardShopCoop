# Game 1.0 compatibility checklist

CardShopCoop version: **1.3.0**. Previously documented tested game version:
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
Two-player runtime checks remain pending; M2 is not fully signed off. Playable TCG,
deck editing, player tournament participation, and their rewards remain M3 work.

**Exit criteria:** Host and guest agree after joining, shop interactions, daily price
updates, reconnecting, and saving/reloading. No card loss or duplication is observed.

## M3 — Define and implement new 1.0 feature support

These are unverified compatibility risks, not confirmed runtime failures.

- [ ] Trace the game's playable TCG, deck editing, reward, and tournament flows.
- [ ] Define host authority and guest permissions for each supported action.
- [ ] Synchronize customer battles and relevant play-table state where supported.
- [ ] Verify deck persistence and card ownership during deck editing.
- [ ] Synchronize rewards and player tournament participation where supported.
- [ ] Clearly gate interactions that remain unsupported.
- [ ] Test simultaneous interactions, joining during play, and disconnects during play.
- [ ] Verify that rewards cannot duplicate and that cards cannot disappear.

**Exit criteria:** Supported features produce matching persistent outcomes; unsupported
interactions are clearly identified and safely gated.

## M4 — Validate and release

- [ ] Run two-player regression tests on fresh 1.0 saves and migrated 0.70.3 saves.
- [ ] Test the supported baseline mod set, then supported optional mod combinations.
- [ ] Resolve all P0/P1 defects within the declared support scope.
- [ ] Document remaining limitations and the exact tested game version.
- [ ] Run `dotnet restore src/CardShopCoop/CardShopCoop.csproj`.
- [ ] Run `dotnet format src/CardShopCoop/CardShopCoop.csproj whitespace --verify-no-changes --no-restore`;
  fix formatting and repeat verification if necessary.
- [ ] Build with `dotnet build src/CardShopCoop/CardShopCoop.csproj -c Release`.
  Use `-p:Deploy=true` only when local deployment is intended and the game is closed.
- [ ] Choose the release version according to the repository rules: patch for fixes
  without wire-contract changes; minor for new messages or substantial changes to
  message layout, encoding, semantics, or routing; major for a deliberate breaking
  protocol transition.
- [ ] Bump the version only in `Directory.Build.props`.
- [ ] Add player-facing release notes to `CHANGELOG.md`. If the wire version or
  required mod set changes, end the release section with **Both players must update.**
- [ ] Verify that both players use the identical plugin version.
- [ ] Keep local release ZIPs in `dist/release/` and reuse the changelog section for
  release descriptions.

**Exit criteria:** Build and formatting checks pass, two-player validation passes,
and the release declares its tested compatibility and limitations.
