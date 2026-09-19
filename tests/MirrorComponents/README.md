# Mirror component cleanup regression harness

Run from the repository root:

```sh
dotnet run --project tests/MirrorComponents/MirrorComponents.csproj -c Release
```

This harness links the production `MirrorComponents` helper against lightweight
Unity stubs. Destruction rejects components still required by another component
on the same object. It covers the reported Seeker/SimpleSmoothModifier dependency,
reverse enumeration, inherited and chained requirements, all three attribute slots,
child objects, repeated cleanup, retained cosmetic dependencies, and NPC disabling
of derived scripts and native navigation behaviours. Cosmetics remain enabled;
NPC components remain available for the existing worker UI to call.

The game metadata audit separately checks the installed pathfinding attributes,
modifier registration lifecycle, and avatar/preview/NPC cleanup call sites.
The standard compatibility validation script includes this harness.

## Required runtime validation (pending)

- Record both players' game/mod builds and installed appearance mods. Test previews,
  remote avatars, customers, and workers with both genders.
- Open/close previews, switch appearance, join/reconnect, disconnect, switch roles,
  and reload scenes. Check for orphan objects and Seeker dependency errors.
- Inspect each co-op clone: avatars retain cosmetic helpers and render correctly;
  NPC Customer/Worker, Seeker, all pathfinding modifiers, and native navigation
  behaviours stay disabled. Verify host-driven movement, animation, and wardrobe
  changes still work. Test live-worker clones with supported appearance mods.
- Confirm worker interaction colliders still open the worker screen and settings
  synchronize. Confirm no local worker/customer AI moves puppets or mutates shop state.
- Any cleanup warning names its caller (preview or remote avatar), clone, and
  retained types. Inspect the surviving dependency before changing removal rules.
- Capture caller/object evidence for any remaining Unity dependency error. The
  supplied log alone cannot establish which clone produced its 11 errors.

These tests model removal rules; they do not execute Unity lifecycle callbacks,
rendering, physics, network movement, or the worker interaction screen.
