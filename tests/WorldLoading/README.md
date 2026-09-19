# World loading regressions

Run `dotnet run --project tests/WorldLoading/WorldLoading.csproj -c Release`.
The harness links the production scene loader and deadline monitor against small
Unity/game stubs. It covers missing scenes/managers, the startup scene, slot
selection, paused loading, overlapping loads, rejected operations, coroutine errors,
timeouts, retry/reset, and recovery before/after scene submission. Recovery failure
must keep the save-protection condition latched. It does not run Unity or network
session teardown. The game metadata harness separately checks the installed native
startup constant, title flow, load index and loading coroutine APIs.

## Pending two-player verification

Record both game builds, plugin versions and loaded mods. Use matching 1.3.3 peers.
For fresh Game 1.0 and migrated 0.70.3 saves:

- Join for the first time, disconnect and reconnect; confirm the shop is usable
  and both players agree on inventory and customers.
- Start through automatic hosting; confirm hosting waits for save loading.
- Exercise unavailable scenes, native save errors and a load exceeding 180 seconds.
  Confirm an error, disconnected transport, and return to Title when Unity can finish.
- Disconnect during the initial delay, scene loading and shop reconstruction.
  Confirm no stale callback starts a new load or resumes co-op synchronization.
- Hold a scene operation past the timeout, then let it complete. Confirm it returns
  to Title and saving stays blocked throughout the late scene's lifetime.
- Retry after recovery and load a personal solo save. Compare personal save files
  before/after failed joins; confirm none were replaced by the host's shop.
- If the title scene also fails, confirm the error requests a restart, saving stays
  blocked, and no new session can start.

Automated results do not constitute these runtime checks or release sign-off.
