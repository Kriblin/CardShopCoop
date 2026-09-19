# Avatar initialization regression harness

Run from the repository root:

```sh
dotnet run --project tests/AvatarInitialization/AvatarInitialization.csproj -c Release
```

The harness links the production template preparation, cache, and optional snapshot
helpers. It uses lightweight Unity/game behavioral stubs and the game's Newtonsoft.Json
assembly, resolved through the repository's normal `GamePath` settings. No game assemblies
are distributed with these tests. It does not launch Unity or connect two players.

Coverage includes both genders, a copied initialized flag with empty private slot lists,
missing or mismatched preset lists, shared preset preservation, default reapplication,
missing presets, cleanup after injected initialization/application failures, independent
gender failure tracking, replacement prefabs, repeated joins to the cache, session reset,
and exception containment at the optional appearance snapshot boundary. Preview-owner
checks inject initialization, JSON parsing, and application failures for both genders,
verify suppression across 1,000 updates, and verify recovery and idempotent cleanup
after clearing the owner. The harness now passes 69 checks. Holder destruction is
represented by the owner callback; Unity object lifetimes still require runtime tests.

## Required in-game validation (pending)

- Record game/Unity/plugin versions and installed mods on both host and guest.
- On copies of saves and appearance files, test fresh and saved appearances for both
  genders. Join, reconnect repeatedly, then reload the scene and join again.
- Confirm normal appearance, body visibility after wardrobe changes, and saved choices
  surviving restart. Compare saved appearance files before and after failure testing.
- In a debugger, invalidate only the new co-op editor clone's `Presets` reference before
  `InitializeFresh` runs. Verify one warning with slot counts, no retained temporary
  holder, no repeated attempts for that prefab/gender, and no changes to the saved model.
  Clear the session or change the prefab to verify initialization can recover.
- Inject an exception into the appearance builder passed to `OptionalAppearanceState.Build`.
  Verify the guest receives Welcome, SaveDone, and BundleDone and loads the shop despite
  omission of the optional player-model snapshot. Verify normal shop interaction afterward.
- Open the preview with fresh and saved appearances for both genders; change presets,
  switch gender, close/reopen, and toggle the NSFW filter. Verify the selected model
  renders and shared preset assets remain unchanged.
- Inject preview initialization failure or malformed appearance JSON. Expect one failure
  report with preset/slot counts and a capsule; leave it open for 1,000 frames and
  verify no new errors, temporary holders, or customer objects accumulate. Restore
  valid input, reopen, replace the prefab, and reset the session to exercise retries.
- Verify remote male and female saved appearances separately, including repeated model
  updates, missing/malformed JSON, reconnects, and NSFW filtering. Confirm supported
  models actually render; marker fallback alone is not a successful dressing check.
- Inject remote customer dressing failure. Confirm a moving capsule marker and name tag,
  with no customer AI or collision and no partially dressed customer body.

Stub tests establish control flow and data invariants. They do not establish actual
Unity destruction, rendering, persistence, network delivery, or two-player compatibility.
