using System.Collections.Generic;
using System.Linq;
using Fortal.Arena;
using UnityEngine;

namespace Deepspace
{
    // 2026-11-25 "เอา Add_on ไปใส่ให้เหมือน FoodIsekai ทุกระบบ": the Fortal launcher systems Food Isekai uses that Boss
    // Slayer did not have yet (its ArenaPlayerNames and ArenaScoreReport):
    //  - seats: which UWB tag plays as Player 1, 2, ... goes to the launcher (FortalArenaPlayers.SetSeats), and
    //  - names: each paying customer's name (sent by the launcher per tag) replaces "PLAYER N" on the wall HUD plates;
    //  - scores: when the session ends, each player's damage goes to the launcher by tag, the MVP starred as winner.
    // Tutorial, language, START and the volume levels were already connected (BossArenaRemoteHooks, PlayerSelect.cs,
    // BossFightAudio). Console lines start with [BossArena].
    public sealed partial class BossSlayerGameManager
    {
        const float ArenaSeatCheckSeconds = .5f;
        float nextArenaSeatCheck;
        int[] arenaSeats = new int[0];

        // The launcher's name for player slot index, or "PLAYER n" when it sent none.
        string ArenaPlayerLabel(int index)
        {
            string name = FortalArenaPlayers.NameForTag(ArenaTagForSlot(index));
            return string.IsNullOrEmpty(name) ? "PLAYER " + (index + 1) : name;
        }

        int ArenaTagForSlot(int slot)
        {
            var source = FloorPlayers();
            return source != null && source.TryGetSlotPlayer(slot, out var player) && player != null && player.TagId > 0 ? player.TagId : 0;
        }

        // Twice a second, from Update: report the seats when they change and keep the HUD names current.
        void TickArenaPlayers()
        {
            if (Time.unscaledTime < nextArenaSeatCheck) return;
            nextArenaSeatCheck = Time.unscaledTime + ArenaSeatCheckSeconds;

            int count = players.Count;
            var seats = new int[count];
            for (int slot = 0; slot < count; slot++) seats[slot] = ArenaTagForSlot(slot);
            if (!seats.SequenceEqual(arenaSeats))
            {
                arenaSeats = seats;
                FortalArenaPlayers.SetSeats(seats);
                Debug.Log($"[BossArena] Seats by player: {string.Join(", ", seats)}", this);
            }

            foreach (var p in players)
            {
                if (p.Index < 0 || p.Index >= hudCards.Length || hudCards[p.Index]?.Label == null) continue;
                string label = ArenaPlayerLabel(p.Index);
                if (hudCards[p.Index].Label.text != label) hudCards[p.Index].Label.text = label;
            }
        }

        // When the session ends (one call per session): damage by tag in player order, the MVP as the winner.
        // Players without a tag (Simulation, mouse testing) are left out.
        void ReportArenaScores(PlayerState mvp)
        {
            var tags = new List<int>();
            var scores = new List<int>();
            var wins = new List<bool>();
            foreach (var p in players.OrderBy(p => p.Index))
            {
                int tag = ArenaTagForSlot(p.Index);
                if (tag <= 0) continue;
                tags.Add(tag);
                scores.Add(p.Damage);
                wins.Add(mvp != null && p.Index == mvp.Index);
            }
            if (tags.Count == 0)
            {
                Debug.Log("[BossArena] No player has a UWB tag: no scores sent", this);
                return;
            }
            FortalArenaPlayers.ReportScoresByTag(tags.ToArray(), scores.ToArray(), wins.ToArray());
            Debug.Log($"[BossArena] Scores sent for tags {string.Join(", ", tags)}", this);
        }
    }
}
