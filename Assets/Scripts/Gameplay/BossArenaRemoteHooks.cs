using Fortal.Arena;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BossSlayer
{
    // 2026-11-20 (BossSlayer project): in deepspace, its own ArenaRemote opened the tutorial slides and switched the
    // language when the web launcher asked. Here the project's FortalArenaRemote kit receives those messages, so this
    // hands it Boss Slayer's tutorial (TutorialSlides + GameShowcase) and language (Lang) while a BossSlayerGameManager
    // is in the scene. START and CONFIRM go through FortalArenaButtons from the game manager itself.
    // 2026-11-27 "ทำ How To Play เหมือนเกมอื่นๆ": like Memory Farm, the launcher's BACK / NEXT turn the pages
    // (ShowTutorialPage). "HOW TO PLAY จะไม่ขึ้นมาก่อน ต้องกดที่ launcher ถึงจะขึ้น": the slides never open by
    // themselves - only the launcher's TUTORIAL (or T in the Editor / build) opens them.
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
            FortalArenaRemote.ShowTutorialPage = ShowTutorialPage;
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
            FortalArenaRemote.ShowTutorialPage = null;
            FortalArenaRemote.GetLanguage = null;
            FortalArenaRemote.SetLanguage = null;
        }

        // Only the T key is watched here; nothing opens the tutorial by itself.
        private void Update()
        {
            SimulateLauncherToggle();
        }

        // 2026-11-27 "ให้การกด T เป็นการจำลองเปิดปิดของ launcher": T sends the same "tutorial" message the launcher's
        // TUTORIAL button sends, so it opens only where the launcher could (TutorialAllowed) and closes it when open.
        private static void SimulateLauncherToggle()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null || !keyboard.tKey.wasPressedThisFrame) return;

            bool open = !(FortalArenaRemote.IsTutorialOpen != null && FortalArenaRemote.IsTutorialOpen());
            FortalArenaRemote.HandleMessage("tutorial", open ? "{\"open\":true}" : "{\"open\":false}");
            Debug.Log($"[BossArena] T: launcher TUTORIAL {(open ? "open" : "close")}");
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

        // The launcher's BACK / NEXT (pages start at 1); it opens the slides first if they are closed.
        private void ShowTutorialPage(int page)
        {
            if (TutorialSlides.Active == null)
            {
                if (game == null || !game.TutorialAllowed) return;
                OpenTutorial();
            }
            if (TutorialSlides.Active != null) TutorialSlides.Active.GoToPage(page);
        }

        private static void SetLanguage(string language)
        {
            if (language != "th" && language != "en") return;
            var wanted = language == "en" ? Lang.Language.English : Lang.Language.Thai;
            if (Lang.Current != wanted) Lang.Set(wanted);
        }
    }
}
