using System.Collections.Generic;

namespace BossSlayer
{
    // 2026-11-26: the wall's end-of-session result (normal sessions and the rush): RAID COMPLETE, then the BOSS DEFEATED
    // panel with the team's damage and every player's damage, best first (BossRaidResultScreen).
    public sealed partial class BossSlayerGameManager
    {
        BossRaidResultScreen raidResult;

        void ShowRaidResult()
        {
            if (cutsceneArea == null) return;
            if (raidResult == null) raidResult = BossRaidResultScreen.Create(cutsceneArea, transform, BossUiFont);

            var ranked = new List<PlayerState>();
            for (int i = 0; i < players.Count && i < playerCount; i++) ranked.Add(players[i]);
            ranked.Sort((a, b) => a.Damage != b.Damage ? b.Damage.CompareTo(a.Damage) : a.Index.CompareTo(b.Index));
            var rows = new List<BossRaidResultScreen.Row>(ranked.Count);
            foreach (var p in ranked) rows.Add(new BossRaidResultScreen.Row(ArenaPlayerLabel(p.Index), p.Damage));

            // a rush that ran out with the king still standing is not a completed raid
            bool complete = !IsRushActive || rushKingDefeated;
            raidResult.Show(complete, rows);
        }

        void HideRaidResult()
        {
            if (raidResult != null) raidResult.Hide();
        }
    }
}
