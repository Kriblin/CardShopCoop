using System.Collections;
namespace UnityEngine
{
    public class Coroutine { public IEnumerator Routine; public bool Stopped; }
    public class AsyncOperation { public bool isDone; public float progress; }
    public class WaitForSecondsRealtime { public WaitForSecondsRealtime(float seconds) { } }
    public static class Application
    {
        public static bool Available = true;
        public static string AvailableScene = "Start";
        public static string CheckedScene;
        public static bool CanStreamedLevelBeLoaded(string name)
        {
            CheckedScene = name;
            return Available && name == AvailableScene;
        }
    }
}
namespace UnityEngine.SceneManagement
{
    public static class SceneManager
    {
        public static string Requested;
        public static bool ThrowLoad, RejectLoad, ThrowTitle;
        public static UnityEngine.AsyncOperation Operation;
        public static int TitleLoads;
        public static UnityEngine.AsyncOperation LoadSceneAsync(string scene)
        {
            Requested = scene;
            if (ThrowLoad) throw new InvalidOperationException("scene failed");
            return RejectLoad ? null : Operation = new UnityEngine.AsyncOperation();
        }
        public static void LoadScene(string scene)
        {
            if (ThrowTitle) throw new InvalidOperationException("title failed");
            if (scene != "Title") throw new Exception("unexpected recovery scene");
            TitleLoads++;
        }
    }
}
public class CGameManager
{
    public static CGameManager m_Instance = new();
    private static bool m_InitLoaded = true;
    public static bool Initialized => m_InitLoaded;
    public int m_LoadGameIndex;
    public UnityEngine.Coroutine Last;
    public UnityEngine.Coroutine StartCoroutine(IEnumerator routine)
    {
        Last = new UnityEngine.Coroutine { Routine = routine };
        routine.MoveNext();
        return Last;
    }
    public void StopCoroutine(UnityEngine.Coroutine coroutine) { coroutine.Stopped = true; }
    public bool Step() => !Last.Stopped && Last.Routine.MoveNext();
}
public static class GameInstance { public static bool m_HasLoadingError, m_FinishedSavefileLoading; }
public static class LoadingScreen
{
    public static bool ThrowOpen, ThrowProgress;
    public static void OpenScreen() { if (ThrowOpen) throw new Exception("screen failed"); }
    public static void SetPercentDone(int percent) { if (ThrowProgress) throw new Exception("progress failed"); }
}
namespace CardShopCoop
{
    public static class CoopPlugin
    {
        public static Logger Log = new();
        public class Logger { public void LogError(object message) { } }
    }
}
