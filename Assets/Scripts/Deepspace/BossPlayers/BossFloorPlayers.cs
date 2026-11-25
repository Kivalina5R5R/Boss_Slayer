using System.Collections.Generic;
using Fortal.Players;
using UnityEngine;

namespace Deepspace
{
    // Where Boss Slayer reads its players from (2026-11-20, split out of deepspace): the project's own
    // UWBPlayerSpawner and UWBPlayerController. This class only reads them; it never spawns, moves or numbers a player.
    // A player's world position (X right, Z forward, metres) is turned into a point on the gameplay floor canvas
    // through the floor area below, which should match what the floor display shows of the real floor.
    public sealed class BossFloorPlayers : MonoBehaviour
    {
        [Tooltip("The scene's UWBPlayerSpawner (found at Play when empty).")]
        [SerializeField] private UWBPlayerSpawner spawner;

        [Header("Floor Area (world metres)")]
        [Tooltip("World X/Z at the centre of the gameplay floor.")]
        [SerializeField] private Vector2 floorCenterMeters = Vector2.zero;
        [Tooltip("Width (world X) and depth (world Z) of the gameplay floor in metres.")]
        [SerializeField] private Vector2 floorSizeMeters = new Vector2(11.48f, 5.26f);

        // Used by layout code that sizes things in real metres when no instance is at hand (11 m: the old default).
        private static BossFloorPlayers active;

        // Slot k (0-based game player) is the spawner's player with this PlayerId; empty = P(k+1) is PlayerId k+1.
        private readonly List<int> rosterPlayerIds = new List<int>();

        public static float ActiveFloorWidthMeters => active != null ? active.FloorWidthMeters : 11f;

        public float FloorWidthMeters => Mathf.Max(.01f, floorSizeMeters.x);
        public float FloorDepthMeters => Mathf.Max(.01f, floorSizeMeters.y);

        // True when the spawner runs without UWB (players are placed and dragged by hand in the Scene view).
        public bool IsSimulation => Spawner() != null && spawner.IsStandaloneSimulationMode;

        public IReadOnlyList<UWBPlayerController> Players
        {
            get
            {
                var found = Spawner();
                return found != null ? found.SpawnedPlayers : System.Array.Empty<UWBPlayerController>();
            }
        }

        // A player counts while the spawner shows it: in Serial mode the spawner hides a player whose tag is silent.
        public bool IsOnline(UWBPlayerController player)
        {
            if (player == null || !player.isActiveAndEnabled) return false;
            return IsSimulation || player.TagId != UWBPlayerController.NoTag;
        }

        // Unclamped point local to the floor canvas rect, lerped over floorRect like the old UWB floor input.
        public bool TryGetFloorLocal(UWBPlayerController player, Rect floorRect, out Vector2 local)
        {
            local = default;
            if (!IsOnline(player)) return false;
            Vector2 n = Normalized(player.transform.position);
            local = new Vector2(Mathf.LerpUnclamped(floorRect.xMin, floorRect.xMax, n.x), Mathf.LerpUnclamped(floorRect.yMin, floorRect.yMax, n.y));
            return true;
        }

        // Inside the floor area, with a small tolerance for tag jitter at the edge.
        public bool IsOnFloor(UWBPlayerController player)
        {
            const float margin = .03f;
            if (!IsOnline(player)) return false;
            Vector2 n = Normalized(player.transform.position);
            return n.x >= -margin && n.x <= 1f + margin && n.y >= -margin && n.y <= 1f + margin;
        }

        // The pad select's order: rosterPlayerIds[k] plays as game player k. Empty clears it (P1 = PlayerId 1 ...).
        // A player in the roster stops showing the player prefab's own marker (its Tag Marker canvas): from here the game
        // draws that player as its P1..P6 marker, and the prefab's circle would cover it.
        public void SetRoster(IReadOnlyList<int> playerIds)
        {
            ClearRoster();
            if (playerIds != null) rosterPlayerIds.AddRange(playerIds);
            foreach (int playerId in rosterPlayerIds)
            {
                if (Spawner() != null && spawner.TryGetPlayer(playerId, out var player)) SetPrefabMarkerVisible(player, false);
            }
        }

        // Back to no roster: every player shows the prefab's marker again.
        public void ClearRoster()
        {
            rosterPlayerIds.Clear();
            foreach (var player in Players)
            {
                if (player != null) SetPrefabMarkerVisible(player, true);
            }
        }

        // 2026-11-25: one player's prefab marker (the white oval) on or off, e.g. hidden on the pad select once that
        // player has locked a card, so only their coloured P marker shows. ClearRoster brings every one back.
        public void ShowPrefabMarker(UWBPlayerController player, bool visible)
        {
            if (player != null) SetPrefabMarkerVisible(player, visible);
        }

        // The spawner's player who plays as game player slot (0-based).
        public bool TryGetSlotPlayer(int slot, out UWBPlayerController player)
        {
            player = null;
            var found = Spawner();
            if (found == null || slot < 0) return false;
            if (rosterPlayerIds.Count > 0)
            {
                return slot < rosterPlayerIds.Count && found.TryGetPlayer(rosterPlayerIds[slot], out player);
            }
            return found.TryGetPlayer(slot + 1, out player);
        }

        public bool TryGetSlotFloorLocal(int slot, Rect floorRect, out Vector2 local)
        {
            local = default;
            return TryGetSlotPlayer(slot, out var player) && TryGetFloorLocal(player, floorRect, out local);
        }

        public bool IsSlotOnFloor(int slot) => TryGetSlotPlayer(slot, out var player) && IsOnFloor(player);

        private void OnEnable() => active = this;

        private void OnDisable()
        {
            if (active == this) active = null;
        }

        private Vector2 Normalized(Vector3 world)
        {
            return new Vector2(
                (world.x - floorCenterMeters.x) / FloorWidthMeters + .5f,
                (world.z - floorCenterMeters.y) / FloorDepthMeters + .5f);
        }

        private static void SetPrefabMarkerVisible(UWBPlayerController player, bool visible)
        {
            foreach (Canvas canvas in player.GetComponentsInChildren<Canvas>(true)) canvas.enabled = visible;
        }

        private UWBPlayerSpawner Spawner()
        {
            if (spawner == null) spawner = FindAnyObjectByType<UWBPlayerSpawner>();
            return spawner;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, .8f, .3f, .9f);
            Gizmos.DrawWireCube(new Vector3(floorCenterMeters.x, 0f, floorCenterMeters.y), new Vector3(floorSizeMeters.x, 0f, floorSizeMeters.y));
        }
    }
}
