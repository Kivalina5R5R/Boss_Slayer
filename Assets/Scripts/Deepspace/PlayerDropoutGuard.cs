using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Deepspace
{
    // บาส 2026-10-02: one "player dropped out" guard for Deepspace, Boss Fight, Bounty Hunter and Mirror Path (Memory Farm
    // has the same rules built into its READY flow; VolleyBall pauses on its own).
    //   * Who counts: every UWB player slot (P1..Pn, n = the game's player count) seen on the floor during play.
    //   * Lost: such a player's tag sends no position for graceSeconds -> the game freezes (Time.timeScale = 0, which
    //     every game already honours: timers, coroutines, tweens and floor input stop) and a card on the wall and the
    //     floor names who is missing.
    //   * Back: once every counted player reports again, a 3-2-1 and play continues exactly where it stopped.
    //   * Gone: still missing after giveUpSeconds -> that player stops counting and the rest play on.
    // Only runs with a live UWB receiver (in the Editor without hardware it stays out of the way) and only while the
    // game says it is in play; leaving play (result, menu) or this object going away always restores time.
    public sealed class PlayerDropoutGuard : MonoBehaviour
    {
        [Tooltip("A player with no position for this long freezes the game (shorter tag blips are ignored).")]
        [SerializeField, Min(.1f)] float graceSeconds = 1.5f;
        [Tooltip("A lost player still missing after this long is dropped and the others play on.")]
        [SerializeField, Min(1f)] float giveUpSeconds = 20f;
        const float ResumeSeconds = 3f;
        const int MaxPlayers = 6;

        Func<bool> inPlay;
        Func<int> playerCount;
        // 2026-11-20 (BossSlayer project): the game says whether a player slot reports a position and whether live UWB
        // is running, instead of this guard reading deepspace's UWB floor input.
        Func<int, bool> isPresent;
        Func<bool> isLive;
        readonly bool[] counted = new bool[MaxPlayers], dropped = new bool[MaxPlayers], present = new bool[MaxPlayers];
        readonly float[] missingFor = new float[MaxPlayers];
        bool paused, wasInPlay;
        float pausedFor, resumeTimer, timeScaleBefore = 1f;
        Card wallCard, floorCard;
        RectTransform wallParent, floorParent;
        TMP_FontAsset font;

        public bool Paused => paused;
        // isPresent(slot): game player slot (0-based) reports a position now. isLive: a live UWB receiver is running.
        public static PlayerDropoutGuard Install(MonoBehaviour game, RectTransform wall, RectTransform floor, TMP_FontAsset font, Func<bool> inPlay, Func<int> playerCount,
            Func<int, bool> isPresent, Func<bool> isLive)
        {
            if (!game.TryGetComponent(out PlayerDropoutGuard guard)) guard = game.gameObject.AddComponent<PlayerDropoutGuard>();
            guard.wallParent = wall; guard.floorParent = floor; guard.font = font;
            guard.inPlay = inPlay; guard.playerCount = playerCount;
            guard.isPresent = isPresent; guard.isLive = isLive;
            return guard;
        }

        bool Present(int zone) => isPresent != null && isPresent(zone);

        void Update()
        {
            bool playing = inPlay != null && inPlay() && isLive != null && isLive();
            if (!playing)
            {
                if (paused) Resume("left play");
                if (wasInPlay) { Array.Clear(counted, 0, MaxPlayers); Array.Clear(dropped, 0, MaxPlayers); Array.Clear(missingFor, 0, MaxPlayers); }
                wasInPlay = false;
                return;
            }
            wasInPlay = true;
            float dt = Time.unscaledDeltaTime;
            int count = Mathf.Clamp(playerCount != null ? playerCount() : 0, 0, MaxPlayers);
            bool anyLost = false, allBack = true;
            for (int i = 0; i < MaxPlayers; i++)
            {
                present[i] = i < count && Present(i);
                if (i >= count) continue;
                if (present[i] && !dropped[i]) counted[i] = true;
                if (!counted[i]) continue;
                missingFor[i] = present[i] ? 0f : missingFor[i] + dt;
                if (missingFor[i] >= graceSeconds) anyLost = true;
                if (!present[i]) allBack = false;
            }

            if (!paused)
            {
                if (!anyLost) return;
                paused = true; pausedFor = resumeTimer = 0f;
                timeScaleBefore = Time.timeScale > 0f ? Time.timeScale : 1f;
                Time.timeScale = 0f;
                ReplayLog.Event("signal_lost", 2f, 0f, Missing(), ReplayLog.Data());
            }

            pausedFor += dt;
            if (!allBack && pausedFor >= giveUpSeconds)
            {
                var gone = Missing();
                foreach (int i in gone) { counted[i] = false; dropped[i] = true; missingFor[i] = 0f; }
                ReplayLog.Event("player_dropped", 0f, 0f, gone, ReplayLog.Data().Add("seconds", pausedFor));
                allBack = true;
                for (int i = 0; i < count; i++) if (counted[i] && !present[i]) allBack = false;
            }

            resumeTimer = allBack ? resumeTimer + dt : 0f;
            if (resumeTimer >= ResumeSeconds) { Resume("back"); return; }

            int left = Mathf.CeilToInt(Mathf.Max(0f, giveUpSeconds - pausedFor));
            ShowCards(allBack ? "RESUMING" : "PLAYER LOST",
                allBack ? Mathf.Clamp(Mathf.CeilToInt(ResumeSeconds - resumeTimer), 1, 3).ToString() : MissingNames() + "  RETURN TO THE FLOOR",
                allBack ? "Everyone is back" : $"The game goes on without them in {left}s", count);
        }

        void Resume(string why)
        {
            paused = false;
            Time.timeScale = timeScaleBefore;
            wallCard?.Hide(); floorCard?.Hide();
            if (why == "back") ReplayLog.Event("signal_back", 0f, 2f, null, ReplayLog.Data().Add("pausedSeconds", pausedFor));
        }

        void OnDisable() { if (paused) { paused = false; Time.timeScale = timeScaleBefore; } }

        int[] Missing()
        {
            var list = new List<int>();
            int count = Mathf.Clamp(playerCount != null ? playerCount() : 0, 0, MaxPlayers);
            for (int i = 0; i < count; i++) if (counted[i] && !present[i]) list.Add(i);
            return list.ToArray();
        }

        string MissingNames()
        {
            var text = "";
            foreach (int i in Missing()) text += (text.Length > 0 ? " " : "") + "P" + (i + 1);
            return text;
        }

        void ShowCards(string title, string status, string hint, int count)
        {
            if (wallCard == null && wallParent != null) wallCard = Card.Create(wallParent, true, font);
            if (floorCard == null && floorParent != null) floorCard = Card.Create(floorParent, false, font);
            wallCard?.Show(title, status, hint, present, counted, dropped, count);
            floorCard?.Show(title, status, hint, present, counted, dropped, count);
        }

        // Plain dark card (each game keeps its own look elsewhere; this one only has to read instantly). Also used by
        // PlayerLobby for the "how many players" screen.
        public sealed class Card
        {
            static readonly Color Panel = new Color(.05f, .06f, .09f, .96f), Title = new Color(1f, .78f, .25f), Here = new Color(.32f, .85f, .5f),
                Lost = new Color(1f, .35f, .3f), Idle = new Color(.4f, .42f, .48f);
            GameObject root; TMP_Text title, status, hint; RectTransform row; readonly List<(Image, TMP_Text)> chips = new();
            TMP_FontAsset font;

            // Find-or-create by name (บาส 2026-10-27: Boss Slayer bakes its lobby card into the scene; Play reuses it).
            public static Card Create(RectTransform parent, bool wall, TMP_FontAsset font, string name = "Panel == Player Lost")
            {
                var card = new Card { font = font };
                var rootRect = Rect(parent, name, Vector2.zero, Vector2.one);
                card.root = rootRect.gameObject;
                if (!rootRect.TryGetComponent(out Image veil)) veil = rootRect.gameObject.AddComponent<Image>();
                veil.color = new Color(0, 0, 0, .6f); veil.raycastTarget = false;
                var box = Rect(rootRect, "Card", wall ? new Vector2(.28f, .18f) : new Vector2(.16f, .14f), wall ? new Vector2(.72f, .82f) : new Vector2(.84f, .86f));
                if (!box.TryGetComponent(out Image back)) back = box.gameObject.AddComponent<Image>();
                back.raycastTarget = false;
                UiThemeKit.Skin(back, UiThemeKit.Shape.Rounded, Panel, Mathf.Max(8f, parent.rect.height * .03f));
                card.title = card.Label(box, "Title", new Vector2(.05f, .76f), new Vector2(.95f, .95f), Title);
                card.status = card.Label(box, "Status", new Vector2(.05f, .46f), new Vector2(.95f, .76f), Color.white);
                card.row = Rect(box, "Players", new Vector2(.08f, .22f), new Vector2(.92f, .42f));
                card.hint = card.Label(box, "Hint", new Vector2(.05f, .05f), new Vector2(.95f, .2f), new Color(.78f, .8f, .86f));
                card.root.SetActive(false);
                return card;
            }

            public void Show(string titleText, string statusText, string hintText, bool[] present, bool[] counted, bool[] dropped, int count)
            {
                root.SetActive(true); root.transform.SetAsLastSibling();
                title.text = titleText; status.text = statusText; hint.text = hintText;
                while (chips.Count < count)
                {
                    var rect = Rect(row, "P" + (chips.Count + 1), Vector2.zero, Vector2.one);
                    if (!rect.TryGetComponent(out Image image)) image = rect.gameObject.AddComponent<Image>();
                    image.raycastTarget = false;
                    UiThemeKit.Skin(image, UiThemeKit.Shape.Rounded, Idle, 12f);
                    var label = Label(rect, "Label", Vector2.zero, Vector2.one, Color.white);
                    label.text = "P" + (chips.Count + 1);
                    chips.Add((image, label));
                }
                float width = 1f / Mathf.Max(1, count);
                for (int i = 0; i < chips.Count; i++)
                {
                    var (image, label) = chips[i];
                    image.gameObject.SetActive(i < count);
                    if (i >= count) continue;
                    var rect = image.rectTransform;
                    rect.anchorMin = new Vector2(i * width + width * .06f, 0); rect.anchorMax = new Vector2((i + 1) * width - width * .06f, 1);
                    rect.offsetMin = rect.offsetMax = Vector2.zero;
                    image.color = dropped[i] || !counted[i] ? Idle : present[i] ? Here : Lost;
                }
            }

            public void Hide() { if (root != null && root.activeSelf) root.SetActive(false); }

            TMP_Text Label(Transform parent, string name, Vector2 min, Vector2 max, Color color)
            {
                var rect = Rect(parent, name, min, max);
                if (!rect.TryGetComponent(out CanvasRenderer _)) rect.gameObject.AddComponent<CanvasRenderer>();
                if (!rect.TryGetComponent(out TextMeshProUGUI text)) text = rect.gameObject.AddComponent<TextMeshProUGUI>();
                if (font != null) text.font = font;
                text.color = color; text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
                text.enableAutoSizing = true; text.fontSizeMin = 8; text.fontSizeMax = 1000; text.fontStyle = FontStyles.Bold;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                return text;
            }

            static RectTransform Rect(Transform parent, string name, Vector2 min, Vector2 max)
            {
                var rect = parent.Find(name) as RectTransform;
                if (rect == null)
                {
                    rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
                    rect.SetParent(parent, false); rect.gameObject.layer = parent.gameObject.layer;
                }
                rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
                return rect;
            }
        }
    }
}
