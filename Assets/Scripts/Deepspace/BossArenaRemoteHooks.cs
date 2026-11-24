using Fortal.Arena;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Deepspace
{
    // 2026-11-20 (BossSlayer project): in deepspace, its own ArenaRemote opened the tutorial slides and switched the
    // language when the web launcher asked. Here the project's FortalArenaRemote kit receives those messages, so this
    // hands it Boss Slayer's tutorial (TutorialSlides + GameShowcase) and language (Lang) while a BossSlayerGameManager
    // is in the scene. START and CONFIRM go through FortalArenaButtons from the game manager itself.
    public sealed class BossArenaRemoteHooks : MonoBehaviour
    {
        private BossSlayerGameManager game;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            AttachToGame();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => AttachToGame();

        private static void AttachToGame()
        {
            var manager = FindFirstObjectByType<BossSlayerGameManager>();
            if (manager == null || manager.GetComponent<BossArenaRemoteHooks>() != null) return;
            manager.gameObject.AddComponent<BossArenaRemoteHooks>().game = manager;
        }

        private void OnEnable()
        {
            if (game == null) game = GetComponent<BossSlayerGameManager>();
            FortalArenaRemote.IsTutorialOpen = () => TutorialSlides.Active != null;
            FortalArenaRemote.CanOpenTutorial = () => game != null && game.TutorialAllowed;
            FortalArenaRemote.OpenTutorial = OpenTutorial;
            FortalArenaRemote.CloseTutorial = CloseTutorial;
            FortalArenaRemote.GetTutorialPage = () => TutorialSlides.Active != null ? TutorialSlides.Active.Page + 1 : 0;
            FortalArenaRemote.GetTutorialPageCount = () => GameShowcase.For(gameObject.scene.name).pages.Length;
            FortalArenaRemote.GetLanguage = () => Lang.Current == Lang.Language.English ? "en" : "th";
            FortalArenaRemote.SetLanguage = SetLanguage;
            FortalArenaRemote.ReportNow();
        }

        private void OnDisable()
        {
            FortalArenaRemote.IsTutorialOpen = null;
            FortalArenaRemote.CanOpenTutorial = null;
            FortalArenaRemote.OpenTutorial = null;
            FortalArenaRemote.CloseTutorial = null;
            FortalArenaRemote.GetTutorialPage = null;
            FortalArenaRemote.GetTutorialPageCount = null;
            FortalArenaRemote.GetLanguage = null;
            FortalArenaRemote.SetLanguage = null;
        }

        private void OpenTutorial()
        {
            if (TutorialSlides.Active != null) return;
            TutorialSlides.Open(GameShowcase.For(gameObject.scene.name));
        }

        private static void CloseTutorial()
        {
            if (TutorialSlides.Active != null) TutorialSlides.Active.Close();
        }

        private static void SetLanguage(string language)
        {
            if (language != "th" && language != "en") return;
            var wanted = language == "en" ? Lang.Language.English : Lang.Language.Thai;
            if (Lang.Current != wanted) Lang.Set(wanted);
        }
    }
}
