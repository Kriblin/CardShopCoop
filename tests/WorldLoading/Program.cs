using CardShopCoop.Sync;
using UnityEngine;
using UnityEngine.SceneManagement;

int checks = 0;
void Check(bool value, string label) { if (!value) throw new Exception(label); checks++; }
void Reject(Action action, string label)
{
    try { action(); } catch (InvalidOperationException) { checks++; return; }
    throw new Exception(label);
}
var monitor = new WorldLoadMonitor();
Check(monitor.Poll(999, true, false) == null, "idle ignores old errors");
monitor.Begin(10);
Check(monitor.Poll(189, false, false) == null && monitor.Active, "slow load allowed");
Check(monitor.Poll(190, false, false)?.Contains("timed out") == true, "deadline enforced");
Check(monitor.Poll(191, true, false) == null, "failure emitted once");
monitor.Begin(200);
Check(monitor.Poll(201, true, true)?.Contains("loading error") == true, "error overrides readiness");
monitor.Begin(300);
Check(monitor.Poll(301, false, true) == null && !monitor.Active, "ready stops timeout");
monitor.Begin(400);
monitor.Reset();
Check(monitor.Poll(1000, true, false) == null, "disconnect resets watchdog");
monitor.Begin(1000);
Check(monitor.Poll(1001, false, false) == null, "reconnect gets fresh deadline");
Application.Available = false;
Reject(WorldSceneLoader.Validate, "missing scene accepted");
Check(Application.CheckedScene == "Start", "missing-scene error ends on current scene");
Application.Available = true;
Application.AvailableScene = "StartOptimized";
WorldSceneLoader.Validate();
Check(WorldSceneLoader.WorldSceneName == "StartOptimized", "legacy optimized scene remains supported");
Application.AvailableScene = "Start";
var gm = CGameManager.m_Instance;
CGameManager.m_Instance = null;
Reject(WorldSceneLoader.Validate, "missing manager accepted");
CGameManager.m_Instance = gm;
WorldSceneLoader.Start(gm, 7);
Check(gm.m_LoadGameIndex == 7 && !GameInstance.m_FinishedSavefileLoading, "scratch slot and fresh completion state");
Check(gm.Last.Routine.Current is WaitForSecondsRealtime, "pause independent delay");
Reject(WorldSceneLoader.Validate, "overlapping load accepted");
gm.Step();
Check(SceneManager.Requested == "Start", "load uses current scene");
SceneManager.Operation.isDone = true;
gm.Step();
Check(!WorldSceneLoader.LoadPending && !GameInstance.m_HasLoadingError, "scene completion releases operation");
WorldSceneLoader.Start(gm, 2);
WorldSceneLoader.AbortWorldLoad();
Check(gm.Last.Stopped && WorldSceneLoader.LoadPending, "abort during delay stops coroutine and holds protection");
WorldSceneLoader.TickRecovery();
Check(SceneManager.TitleLoads == 1 && !WorldSceneLoader.LoadPending, "pre-submit abort returns to title");
Check(!CGameManager.Initialized, "recovery resets native initialization for next world");
WorldSceneLoader.Start(gm, 7);
gm.Step();
WorldSceneLoader.AbortWorldLoad();
WorldSceneLoader.TickRecovery();
Check(SceneManager.TitleLoads == 1 && WorldSceneLoader.LoadPending, "in-flight scene holds recovery and save protection");
SceneManager.Operation.isDone = true;
WorldSceneLoader.TickRecovery();
Check(SceneManager.TitleLoads == 2 && !WorldSceneLoader.LoadPending, "late scene finishes before title recovery");
foreach (string failure in new[] { "reject", "throw", "screen", "progress" })
{
    SceneManager.RejectLoad = failure == "reject";
    SceneManager.ThrowLoad = failure == "throw";
    LoadingScreen.ThrowOpen = failure == "screen";
    LoadingScreen.ThrowProgress = failure == "progress";
    WorldSceneLoader.Start(gm, 7);
    gm.Step();
    gm.Step();
    Check(GameInstance.m_HasLoadingError, failure + " is observable");
    WorldSceneLoader.AbortWorldLoad();
    if (SceneManager.Operation != null) SceneManager.Operation.isDone = true;
    WorldSceneLoader.TickRecovery();
    Check(!WorldSceneLoader.LoadPending, failure + " recovers");
}
SceneManager.RejectLoad = SceneManager.ThrowLoad = LoadingScreen.ThrowOpen = LoadingScreen.ThrowProgress = false;
WorldSceneLoader.Start(gm, 7);
Check(!GameInstance.m_HasLoadingError, "retry clears previous native error");
WorldSceneLoader.AbortWorldLoad();
SceneManager.ThrowTitle = true;
WorldSceneLoader.TickRecovery();
Check(WorldSceneLoader.RecoveryFailed, "failed title recovery keeps save protection latched");
Reject(WorldSceneLoader.Validate, "join accepted after failed recovery");
Console.WriteLine($"Passed {checks} world loading checks.");
