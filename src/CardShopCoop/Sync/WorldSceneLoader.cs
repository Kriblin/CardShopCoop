using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CardShopCoop.Sync
{
    internal static class WorldSceneLoader
    {
        // The current PC build loads "Start". Some Game 1.0 builds used
        // "StartOptimized" and exposed it through CGameManager.k_StartSceneName, but that
        // field is no longer part of the game API. Resolve the shipped scene directly so
        // both layouts remain joinable without a compile-time game-member dependency.
        private const string CurrentWorldSceneName = "Start";
        private const string LegacyWorldSceneName = "StartOptimized";
        private static Coroutine _loadRoutine;
        private static CGameManager _loadOwner;
        private static AsyncOperation _sceneOperation;
        private static bool _recovering;
        internal static bool LoadPending => _loadRoutine != null || _sceneOperation != null || _recovering;
        internal static string WorldSceneName
        {
            get
            {
                if (Application.CanStreamedLevelBeLoaded(CurrentWorldSceneName))
                    return CurrentWorldSceneName;
                if (Application.CanStreamedLevelBeLoaded(LegacyWorldSceneName))
                    return LegacyWorldSceneName;
                return CurrentWorldSceneName;
            }
        }

        // Unity cannot cancel a submitted scene operation. Let it finish before returning
        // to Title, and keep the borrowed-world save guard until that return completes.
        internal static void AbortWorldLoad()
        {
            if (_loadRoutine != null && _loadOwner != null)
                _loadOwner.StopCoroutine(_loadRoutine);
            _loadRoutine = null;
            _recovering = true;
        }

        internal static void TickRecovery()
        {
            if (!_recovering || (_sceneOperation != null && !_sceneOperation.isDone))
                return;
            _sceneOperation = null;
            try
            {
                // Native LoadLobbySceneAsync resets this before returning to Title.
                // Without it, starting another world can skip LoadData entirely.
                ResetInitialization();
                SceneManager.LoadScene("Title");
                _recovering = false;
            }
            catch (Exception e)
            {
                // Keep protection latched if even the title cannot be loaded.
                CoopPlugin.Log.LogError("World load recovery failed; restart the game: " + e);
                _recovering = false;
                RecoveryFailed = true;
            }
        }

        internal static bool RecoveryFailed
        {
            get; private set;
        }

        internal static void ResetInitialization()
        {
            var field = typeof(CGameManager).GetField("m_InitLoaded", BindingFlags.NonPublic | BindingFlags.Static);
            if (field == null)
                throw new MissingFieldException("CGameManager", "m_InitLoaded");
            field.SetValue(null, false);
        }

        internal static void Validate()
        {
            if (LoadPending || RecoveryFailed)
                throw new InvalidOperationException("A previous world load is still recovering. Restart the game if recovery does not finish.");
            if (CGameManager.m_Instance == null)
                throw new InvalidOperationException("The game manager is not ready for world loading.");
            string sceneName = WorldSceneName;
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
                throw new InvalidOperationException("The installed game cannot load the required shop scene ('Start' or 'StartOptimized'). Check the game build and installation.");
        }

        internal static void Start(CGameManager gm, int slot)
        {
            Validate();
            // Matches the native LoadMainLevelAsync/LoadLobbySceneAsync order, with
            // an owned coroutine and operation so failures and aborts are observable.
            gm.m_LoadGameIndex = slot;
            GameInstance.m_HasLoadingError = false;
            GameInstance.m_FinishedSavefileLoading = false;
            _loadOwner = gm;
            _loadRoutine = gm.StartCoroutine(LoadWorld());
            if (_loadRoutine == null)
                throw new InvalidOperationException("The game could not start the world loading coroutine.");
        }

        private static IEnumerator LoadWorld()
        {
            Exception failure = null;
            try
            {
                LoadingScreen.OpenScreen();
            }
            catch (Exception e) { failure = e; }
            if (failure == null)
            {
                yield return new WaitForSecondsRealtime(2f);
                try
                {
                    _sceneOperation = SceneManager.LoadSceneAsync(WorldSceneName);
                    if (_sceneOperation == null)
                        throw new InvalidOperationException("The game rejected the world scene load.");
                }
                catch (Exception e) { failure = e; }
            }
            while (failure == null && !_sceneOperation.isDone)
            {
                try
                {
                    LoadingScreen.SetPercentDone((int)(100f * _sceneOperation.progress / 0.9f));
                }
                catch (Exception e) { failure = e; }
                yield return null;
            }
            _loadRoutine = null;
            if (failure != null)
            {
                CoopPlugin.Log.LogError("World scene loading failed: " + failure);
                GameInstance.m_HasLoadingError = true;
            }
            else
                _sceneOperation = null;
        }
    }
}
