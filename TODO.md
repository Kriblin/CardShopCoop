# Game 1.0 compatibility checklist

CardShopCoop version: **1.2.0**. Previously documented tested game version:
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
The local Steam manifest identifies build **25304508**; the repository plugin version
is **1.2.0**. Host/guest runtime versions and mod sets have not been collected. The exact
reported invalid index and two-player behavior remain unverified; M1 is not fully signed off.

**Exit criteria:** Guests can join reliably, including when appearance initialization fails.

## M2 — Restore existing shop compatibility

- [ ] **P1: Validate Harmony patches and reflected fields against game 1.0.** Check
  exact method signatures, overloads, and actual call paths; plugin loading alone
  does not prove that patches still work.
- [ ] **P1: Close the Ascension market coverage gap.** The game has an Ascension market
  table, but explicit vanilla market snapshots and checksums omit it, and the client
  price-generation patch blocks unrecognized expansions.
  - [ ] Determine which updates the existing generic delta path already covers.
  - [ ] Include Ascension in snapshot, apply, checksum, and recovery behavior as needed.
  - [ ] Verify prices on join, daily updates, and reconnect; check for zero or divergent prices.
  - [ ] Audit pack opening, collection operations, trading, grading, and card displays
    for assumptions about the older expansion list.
- [ ] **P1: Audit updated customer and save behavior.** This is a compatibility risk,
  not yet a reproduced defect.
  - [ ] Verify tournament customer appearance, persistence, and mirrored state.
  - [ ] Verify transferred saves, reconnects, and save/reload behavior.
- [ ] Run two-player regression checks for checkout, workers, boxes, shelves,
  furniture, trading, grading, and card displays.

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
