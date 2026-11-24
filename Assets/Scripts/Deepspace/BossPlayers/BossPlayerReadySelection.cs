using System;
using System.Collections.Generic;
using Fortal.Players;
using UnityEngine;

namespace Deepspace
{
    // The floor pads before the boss select: every player the project's UWBPlayerSpawner shows gets a pad row, and each
    // picks a game number by standing on a pad. The spawner's own player numbers are left alone; the picked order is
    // handed back as the spawner's PlayerIds (pad 1 first), which BossFloorPlayers then uses as the game's P1..Pn.
    // 2026-11-20: moved onto BossSlayer's UWBPlayerSpawner (was deepspace's BossUwbPlayerSpawner); players are keyed by
    // the spawner's PlayerId instead of their UWB tag.
    public sealed class BossPlayerReadySelection
    {
        private const int MaximumPlayers = 6;

        private readonly BossFloorPlayers floorPlayers;
        private readonly IPlayerPadView view;
        private readonly Func<Rect> floorRect;
        private readonly float holdSeconds;
        private readonly float rosterSettleSeconds;
        private readonly float dropOfflineSeconds;
        private readonly float startDelaySeconds;
        private readonly Action<IReadOnlyList<int>> onAllPlayersReady;

        private readonly List<UWBPlayerController> roundPlayers = new List<UWBPlayerController>();
        private readonly HashSet<int> observedIds = new HashSet<int>();
        // How long each round player (by PlayerId) has been offline.
        private readonly Dictionary<int, float> offlineSeconds = new Dictionary<int, float>();
        // The card each player (by PlayerId) stood on last frame, so a step onto a card bounces it once.
        private readonly Dictionary<int, int> lastPadOfPlayer = new Dictionary<int, int>();
        private PlayerNumberSelection selection;
        private float rosterChangedAt;
        private float allPickedFor;
        private int shownPads;
        private bool finished;
        // At the arena the pads wait for the launcher's START, which takes the players' credit first.
        private bool waitForStartPress;
        private bool startPressed;

        // floorRect is the gameplay floor rect, local to the pad view's input space.
        // startDelaySeconds is how long the last claim's punch shows before the game starts.
        public BossPlayerReadySelection(BossFloorPlayers floorPlayers, IPlayerPadView view, Func<Rect> floorRect, float holdSeconds,
            float rosterSettleSeconds, float dropOfflineSeconds, float startDelaySeconds, Action<IReadOnlyList<int>> onAllPlayersReady)
        {
            this.floorPlayers = floorPlayers;
            this.view = view;
            this.floorRect = floorRect;
            this.holdSeconds = Mathf.Max(.1f, holdSeconds);
            this.rosterSettleSeconds = Mathf.Max(0f, rosterSettleSeconds);
            this.dropOfflineSeconds = Mathf.Max(.1f, dropOfflineSeconds);
            this.startDelaySeconds = Mathf.Max(0f, startDelaySeconds);
            this.onAllPlayersReady = onAllPlayersReady;
        }

        // No player online yet (or the online set is still settling): the pads are not shown.
        public bool IsWaitingForPlayers => !finished && selection == null;
        public bool IsSelecting => !finished && selection != null;
        public bool IsFinished => finished;
        public int PadCount => shownPads;

        // True while the pads are held back for the launcher's START.
        public bool IsWaitingForStart => !finished && waitForStartPress && !startPressed;

        // From now on the pads come up only once StartPressed has been called.
        public void WaitForStartPress() => waitForStartPress = true;

        // The launcher's START (or the staff's Enter / Space): the pads come up as soon as the players have settled.
        public void StartPressed() => startPressed = true;

        public void Tick(float deltaSeconds)
        {
            if (finished) return;
            TrackOffline(deltaSeconds);
            if (selection == null && !TryInitialize()) return;
            AddNewParticipants();
            DropLostParticipants();
            if (selection == null) return;

            TickPads(Mathf.Min(deltaSeconds, .1f));
            Draw();
            if (!selection.IsComplete) { allPickedFor = 0f; return; }
            allPickedFor += deltaSeconds;
            if (allPickedFor >= startDelaySeconds) Finish();
        }

        // Stops without starting the game (back at the menu, tests).
        public void Cancel()
        {
            finished = true;
            view.Cancel();
        }

        // Ends now: whoever has not picked yet gets the free pads in order. Tests / previews only.
        public void ForceFinish()
        {
            if (finished) return;
            if (selection == null) { finished = true; view.Cancel(); onAllPlayersReady?.Invoke(Array.Empty<int>()); return; }
            Finish();
        }

        private List<UWBPlayerController> OnlinePlayers()
        {
            var online = new List<UWBPlayerController>();
            foreach (UWBPlayerController player in floorPlayers.Players)
            {
                if (floorPlayers.IsOnline(player)) online.Add(player);
            }
            return online;
        }

        private void TrackOffline(float deltaSeconds)
        {
            foreach (UWBPlayerController player in roundPlayers)
            {
                if (player == null) continue;
                offlineSeconds.TryGetValue(player.PlayerId, out float seconds);
                offlineSeconds[player.PlayerId] = floorPlayers.IsOnline(player) ? 0f : seconds + Mathf.Max(0f, deltaSeconds);
            }
        }

        private bool TryInitialize()
        {
            List<UWBPlayerController> online = OnlinePlayers();
            var ids = new int[online.Count];
            for (int i = 0; i < ids.Length; i++) ids[i] = online[i].PlayerId;
            if (!observedIds.SetEquals(ids))
            {
                observedIds.Clear();
                observedIds.UnionWith(ids);
                rosterChangedAt = Time.unscaledTime;
                if (rosterSettleSeconds > 0f) return false;
            }
            if (ids.Length == 0 || Time.unscaledTime - rosterChangedAt < rosterSettleSeconds) return false;
            if (IsWaitingForStart) return false;   // the players keep being watched, but no pad shows before START

            selection = new PlayerNumberSelection(ids, ids.Length, holdSeconds);
            roundPlayers.Clear();
            roundPlayers.AddRange(online);
            offlineSeconds.Clear();
            allPickedFor = 0f;
            shownPads = RequiredPads();
            view.ShowExternal(shownPads);
            Debug.Log($"[BossPlayerReadySelection] {ids.Length} player(s) online: showing {shownPads} pad(s)");
            return true;
        }

        private void AddNewParticipants()
        {
            if (selection == null) return;
            foreach (UWBPlayerController player in OnlinePlayers())
            {
                if (roundPlayers.Count >= MaximumPlayers) break;
                if (roundPlayers.Contains(player)) continue;
                if (!selection.TryAddParticipant(player.PlayerId)) continue;
                roundPlayers.Add(player);
                Debug.Log($"[BossPlayerReadySelection] Player {player.PlayerId} came online late: pad added");
            }
            ResizePads();
        }

        private void DropLostParticipants()
        {
            if (selection == null) return;
            for (int i = roundPlayers.Count - 1; i >= 0; i--)
            {
                UWBPlayerController player = roundPlayers[i];
                if (player == null)
                {
                    roundPlayers.RemoveAt(i);
                    continue;
                }
                offlineSeconds.TryGetValue(player.PlayerId, out float seconds);
                if (selection.HasAssignedNumber(player.PlayerId) || seconds < dropOfflineSeconds) continue;
                selection.RemoveParticipant(player.PlayerId);
                roundPlayers.RemoveAt(i);
                Debug.Log($"[BossPlayerReadySelection] Player {player.PlayerId} went offline before picking: pad removed");
            }

            if (roundPlayers.Count > 0) { ResizePads(); return; }
            // Everyone left: back to waiting for players.
            selection = null;
            observedIds.Clear();
            shownPads = 0;
            view.Cancel();
        }

        // One pad per participant, and never fewer than the highest pad already claimed.
        private int RequiredPads()
        {
            int pads = selection.ParticipantCount;
            for (int number = 1; number <= selection.NumberCount; number++)
            {
                if (selection.GetOwner(number).HasValue) pads = Mathf.Max(pads, number);
            }
            return Mathf.Clamp(pads, 1, MaximumPlayers);
        }

        private void ResizePads()
        {
            int pads = RequiredPads();
            if (pads == shownPads) return;
            shownPads = pads;
            view.ResizeExternal(shownPads);
        }

        private void TickPads(float delta)
        {
            var padOf = new int[roundPlayers.Count];
            for (int p = 0; p < roundPlayers.Count; p++) padOf[p] = PadUnder(roundPlayers[p]);
            BounceSteppedCards(padOf);

            for (int number = 1; number <= shownPads; number++)
            {
                if (selection.GetOwner(number).HasValue) continue;
                UWBPlayerController candidate = null;
                int occupants = 0;
                for (int p = 0; p < roundPlayers.Count; p++)
                {
                    if (padOf[p] != number - 1 || selection.HasAssignedNumber(roundPlayers[p].PlayerId)) continue;
                    occupants++;
                    candidate = roundPlayers[p];
                }

                int? soleId = occupants == 1 ? candidate.PlayerId : (int?)null;
                if (!selection.TickNumber(number, soleId, delta)) continue;
                Debug.Log($"[BossPlayerReadySelection] Player {candidate.PlayerId} picked pad {number}");
            }
        }

        // A card bounces when a player steps onto it (BossPlayerCards.Bounce), free or taken, so every step gets an
        // answer. Standing still on it does not bounce it again.
        private void BounceSteppedCards(int[] padOf)
        {
            for (int p = 0; p < roundPlayers.Count; p++)
            {
                int id = roundPlayers[p].PlayerId;
                int last = lastPadOfPlayer.TryGetValue(id, out int was) ? was : -1;
                lastPadOfPlayer[id] = padOf[p];
                if (padOf[p] >= 0 && padOf[p] != last) BossPlayerCards.Bounce(padOf[p]);
            }
        }

        // The pad (0-based) a player stands on, or -1 (no position, or between pads).
        private int PadUnder(UWBPlayerController player)
        {
            Vector2? at = FloorPosition(player);
            return at.HasValue ? view.PadAtNormalized(at.Value) : -1;
        }

        private Vector2? FloorPosition(UWBPlayerController player)
        {
            if (!floorPlayers.TryGetFloorLocal(player, floorRect(), out Vector2 local)) return null;
            Vector2? at = view.InputLocalToFloorNormalized(local);
            if (!at.HasValue) return null;
            Vector2 n = at.Value;
            return n.x >= 0f && n.x <= 1f && n.y >= 0f && n.y <= 1f ? n : (Vector2?)null;
        }

        private void Draw()
        {
            int count = roundPlayers.Count;
            var cursorAt = new Vector2?[count];
            var cursorPad = new int[count];
            var holdPadOf = new int[count];
            var holdProgress = new float[count];
            var padOwners = new int[shownPads];
            for (int k = 0; k < shownPads; k++) padOwners[k] = -1;

            int picked = 0;
            for (int p = 0; p < count; p++)
            {
                UWBPlayerController player = roundPlayers[p];
                cursorAt[p] = FloorPosition(player);
                int number = selection.GetAssignedNumber(player.PlayerId);
                cursorPad[p] = number - 1;
                holdPadOf[p] = -1;
                if (number > 0)
                {
                    picked++;
                    if (number <= shownPads) padOwners[number - 1] = p;
                    continue;
                }
                for (int n = 1; n <= shownPads; n++)
                {
                    if (selection.GetOwner(n).HasValue || selection.GetCandidate(n) != player.PlayerId) continue;
                    holdPadOf[p] = n - 1;
                    holdProgress[p] = selection.GetProgress(n);
                }
            }

            string status = picked == count ? "ALL READY" : $"{picked} / {count} READY";
            view.DrawExternal(cursorAt, cursorPad, holdPadOf, holdProgress, padOwners, status);
        }

        // The pads decide the order: picked players by pad number, then anyone unpicked (ForceFinish) in join order.
        // The result is packed to P1..Pn so a removed pad leaves no gap.
        private void Finish()
        {
            finished = true;
            var ordered = new List<UWBPlayerController>(roundPlayers);
            ordered.Sort((left, right) => PadOrder(left).CompareTo(PadOrder(right)));
            var ids = new int[ordered.Count];
            for (int i = 0; i < ordered.Count; i++) ids[i] = ordered[i].PlayerId;
            view.Cancel();
            Debug.Log($"[BossPlayerReadySelection] All {ids.Length} player(s) ready: spawner players {string.Join(", ", ids)}");
            onAllPlayersReady?.Invoke(ids);
        }

        private int PadOrder(UWBPlayerController player)
        {
            int number = selection != null ? selection.GetAssignedNumber(player.PlayerId) : 0;
            return number > 0 ? number : MaximumPlayers + 1 + roundPlayers.IndexOf(player);
        }
    }
}
