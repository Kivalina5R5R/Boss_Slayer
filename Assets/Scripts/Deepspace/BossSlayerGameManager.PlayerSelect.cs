using System;
using System.Collections.Generic;
using TMPro;
using Fortal.Arena;
using UnityEngine;
using FortalReplay;

namespace Deepspace
{
    // บาส 2026-10-22: before the boss select, each player picks their number on a floor pad. The pad you took becomes your
    // player number, so P1's colour, HP card and gems are yours for the session. Look: the boss select's arcane-codex skin
    // (stone cards, gold lettering, Cinzel) with the Boss Slayer logo on the wall.
    // 2026-11-20 (BossSlayer project): the players are the project's own UWBPlayerSpawner players, read through
    // BossFloorPlayers. The floor shows one pad per online player, and the game starts the moment everybody has
    // picked - no lobby countdown and no timeout. PlayerPadSelect only draws the pads.
    public sealed partial class BossSlayerGameManager
    {
        [Header("Player select (before the boss select)")]
        [SerializeField] bool playerSelect = true;
        [SerializeField, Min(.2f)] float padHoldSeconds = 2f;
        [Tooltip("The connected tags must hold still this long before the pads appear (Food Isekai: 2 s), so a group walking on together gets one row.")]
        [SerializeField, Min(0f)] float rosterSettleSeconds = 2f;
        [Tooltip("A tag that disconnects this long before picking loses its pad.")]
        [SerializeField, Min(.5f)] float dropOfflinePlayerSeconds = 3f;
        [Tooltip("After the last pick, the claim flash shows this long before the boss select.")]
        [SerializeField, Min(0f)] float allPickedStartDelay = .4f;
        // Food Isekai's PlayerPlate prefab at its scene's playerScale 0.462: the ready circle is 120 px x 0.012 x 0.462 and
        // the plate 160 px x 0.012 x 0.462, in metres on the floor.
        [Tooltip("Size on the real floor (metres) of a player who has not picked a pad yet: Food Isekai's ready circle, 0.665 m.")]
        [SerializeField, Min(.05f)] float unpickedPlayerMeters = .665f;
        [Tooltip("Size on the real floor (metres) of a player who has picked: Food Isekai's player plate, 0.887 m.")]
        [SerializeField, Min(.05f)] float pickedPlayerMeters = .887f;
        [Tooltip("Colour of the ready circle (Food Isekai: cream, 72% opaque).")]
        [SerializeField] Color unpickedPlayerColor = new Color(1f, .94f, .75f, .72f);
        [Tooltip("Reads the players of the project's UWBPlayerSpawner (found at Play when empty). With UWBManager in Simulation, drag the spawned players onto the pads in the Scene view.")]
        [SerializeField] BossFloorPlayers floorPlayers;

        PlayerPadSelect pads;
        BossPlayerReadySelection readySelection;
        Action pickDone;
        bool waitingForUwbStart;
        bool pickingPlayers => pickDone != null;
        int pickCount => readySelection != null ? readySelection.PadCount : 0;
        bool waitingForPlayers => pickingPlayers && (waitingForUwbStart || (readySelection != null && readySelection.IsWaitingForPlayers));

        PlayerPadSelect.Style BossPadStyle(TMP_FontAsset title, TMP_FontAsset body)
        {
            var style = BossPadPlainStyle(title, body);
            StylePadArt(style);   // บาส 2026-11-18: the mockup's player cards (PlayerSelectArt.cs)
            // 2026-11-18: players look and measure like Food Isekai's (ready circle -> plate size)
            style.UnpickedCursorDisc = unpickedPlayerColor;
            style.UnpickedCursorMeters = unpickedPlayerMeters;
            style.PickedCursorMeters = pickedPlayerMeters;
            style.PickedCursorSprite = BossPlayerMarkers.For;   // บาส 2026-11-18: picked = the P1..P6 marker, unpicked-disc size
            return style;
        }

        PlayerPadSelect.Style BossPadPlainStyle(TMP_FontAsset title, TMP_FontAsset body) => new PlayerPadSelect.Style
        {
            TitleFont = title, BodyFont = body,
            Ground = UiThemeKit.Alpha(BossFantasyTheme.Night, .96f), Plate = BossFantasyTheme.Stone, Ink = BossFantasyTheme.Parchment, Accent = BossFantasyTheme.Gold,
            PadColor = PlayerColor, Shape = UiThemeKit.Shape.Chamfer, InputSpace = () => InputArea,
            WallBackground = Resources.Load<Texture2D>("BossSlayer/BossSlayerWallBackground"), WallLogo = Resources.Load<Texture2D>("BossSlayer/BossSlayerLogo"),
            StyleText = (t, heading, face) =>
            {
                if (heading && face == BossFantasyTheme.Gold) BossFantasyTheme.GoldText(t, title, 10);
                else if (heading) BossFantasyTheme.Outlined(t, title, face, .22f, 6);
                else BossFantasyTheme.Letter(t, body, face, 4);
            },
        };

        PlayerPadSelect Pads(TMP_FontAsset title, TMP_FontAsset body)
        {
            if (pads == null && playArea != null) pads = PlayerPadSelect.Install(this, null, playArea, BossPadStyle(title, body));   // floor only (บาส 2026-10-22)
            return pads;
        }

        // Back at the menu: a new group joins and picks. `done` runs once everybody connected has a pad.
        void ShowPlayerSelect(Action done)
        {
            readySelection?.Cancel();
            readySelection = null;
            var view = playerSelect ? Pads(CodexTitleFont, BossUiFont) : null;
            if (view == null) { FinishWithoutUwb(done); return; }
            foreach (var p in players) if (p.Cursor != null) p.Cursor.gameObject.SetActive(false);   // the pad screen draws its own cursors
            pickDone = done;
            waitingForUwbStart = true;
            arenaStartPressed = false;
            FortalArenaButtons.AddStart(PressArenaStart, () => IsWaitingForArenaStart);
        }

        // 2026-11-19: "ยังไม่ต้องแสดงช่องเหยียบถ้าไม่ได้กด Start ผ่าน launcher" - like Food Isekai and Bounty Hunter, the
        // pads stay hidden (the Start wall and the empty rune floor show, MenuIntro.cs) until the web launcher's START, which takes the players' credit and then
        // presses START here through FortalArenaRemote. Enter / Space are the staff's own START (and the Editor's).
        [Tooltip("Hide the player pads until the web launcher's START (or Enter / Space) is pressed.")]
        [SerializeField] bool waitForArenaStart = true;
        bool arenaStartPressed;

        bool IsWaitingForArenaStart => waitForArenaStart && pickingPlayers && !arenaStartPressed;

        void PressArenaStart()
        {
            if (!IsWaitingForArenaStart) return;
            arenaStartPressed = true;
            readySelection?.StartPressed();
            Debug.Log("[BossSlayer] START from the launcher: the player pads come up", this);
        }

        void HidePlayerSelect()
        {
            pickDone = null;
            waitingForUwbStart = false;
            readySelection?.Cancel();
            readySelection = null;
            if (pads != null) { pads.Cancel(); return; }
            // บาส 2026-11-18: the scene keeps the baked screen shown (so the Editor shows the floor's player select);
            // the pad screen is only installed when it is first needed, so hide the baked one here until then
            var baked = playArea != null ? playArea.Find("Panel == Player Select") : null;
            if (baked != null) baked.gameObject.SetActive(false);
        }

        // Every frame while pickingPlayers (from Update).
        void UpdatePlayerSelect(float dt)
        {
            if (!pickingPlayers) return;
            if (IsWaitingForArenaStart && StartKeyPressed(FloorPlayers() is BossFloorPlayers source && source.IsSimulation))
                PressArenaStart();
            if (readySelection == null && !TryStartReadySelection()) return;
            readySelection.Tick(Time.unscaledDeltaTime);
        }

        // Enter, keypad Enter or Space stands in for the launcher's START button. This project reads input only through
        // the Input System package, so the legacy Input class would throw here.
        // The staff's START: P in Simulation mode, Enter / Space otherwise.
        static bool StartKeyPressed(bool simulation)
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null) return false;
            if (simulation) return keyboard.pKey.wasPressedThisFrame;
            return keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame;
        }

        // 2026-11-20: the players come from the project's UWBPlayerSpawner (BossFloorPlayers reads them). Nobody reaches
        // the boss select without a pad: a build without UWB keeps waiting for players. With useUwbInput off (Editor mouse
        // testing) the pads are skipped.
        bool TryStartReadySelection()
        {
            if (!useUwbInput) { var d = pickDone; HidePlayerSelect(); FinishWithoutUwb(d); return false; }
            var source = FloorPlayers();
            if (source == null) return false;   // no BossFloorPlayers in the scene: keep the empty rune floor up
            bool simulate = source.IsSimulation;
            source.ClearRoster();
            waitingForUwbStart = false;
            readySelection = new BossPlayerReadySelection(source, pads, () => FloorRect, padHoldSeconds, simulate ? 0f : rosterSettleSeconds,
                dropOfflinePlayerSeconds, allPickedStartDelay, OnAllPlayersPicked);
            // 2026-11-25 "ไม่ต้องขึ้นปุ่มเลือก player จนกว่าจะกด start ที่ launcher ใน mode simulation ให้การกด start เป็นปุ่ม P":
            // Simulation waits for START too (it used to press it by itself); P is START there (StartKeyPressed).
            if (waitForArenaStart && !arenaStartPressed) readySelection.WaitForStartPress();
            return true;
        }

        BossFloorPlayers FloorPlayers()
        {
            if (floorPlayers == null) floorPlayers = FindFirstObjectByType<BossFloorPlayers>();
            return floorPlayers;
        }

        // The pad order is the player order: playerIds[0] (a UWBPlayerSpawner PlayerId) is P1 until the next menu.
        void OnAllPlayersPicked(IReadOnlyList<int> playerIds)
        {
            int count = playerIds.Count == 0 ? inspectorPlayerCount : playerIds.Count;
            if (playerIds.Count > 0) FloorPlayers()?.SetRoster(playerIds);
            ReplayLog.Event("players_locked", 0f, 0f, null, ReplayLog.Data().Add("players", count).Add("paired", false));
            ApplyPlayerCount(count);
            var d = pickDone;
            pickDone = null;
            readySelection = null;
            d?.Invoke();
        }

        void FinishWithoutUwb(Action done)
        {
            ApplyPlayerCount(inspectorPlayerCount);
            done?.Invoke();
        }

        // Tests / previews: end the pads now; whoever has not picked gets the free pads in order.
        void FinishPlayerSelect(bool renumber)
        {
            if (!pickingPlayers) return;
            if (readySelection != null) { readySelection.ForceFinish(); return; }
            var d = pickDone;
            HidePlayerSelect();
            FinishWithoutUwb(d);
        }

#if UNITY_EDITOR
        // Editor bake: the player select screen as real (hidden) scene objects, laid out for 4 players.
        public void BakePlayerSelectUi(TMP_FontAsset title, TMP_FontAsset body)
        {
            // Only the two canvases: ResolveSceneReferences would also build the boss models into the saved scene.
            playArea = playArea != null ? playArea : FindRect("Canvas == Gameplay");
            cutsceneArea = cutsceneArea != null ? cutsceneArea : FindRect("Canvas == Cutscene");
            var oldWall = cutsceneArea != null ? cutsceneArea.Find("Panel == Player Select Wall") : null;   // the pads are floor-only now
            if (oldWall != null) DestroyImmediate(oldWall.gameObject);
            var pick = Pads(title, body);
            if (pick == null) return;
            pick.Build();
            pick.Layout(6);   // six cards, like the art team's mockup (Play lays them out for the real count)
            BakeMenuIntroUi(title, body);   // บาส 2026-10-27: the lobby's videos + cards too ("ทำทุกอย่างเป็น Object จริงๆ")
        }
#endif
    }
}
