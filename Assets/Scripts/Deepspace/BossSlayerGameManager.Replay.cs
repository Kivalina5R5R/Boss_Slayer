using System.Collections.Generic;
using UnityEngine;
using FortalReplay;

namespace Deepspace
{
    // Replay/highlight logging (ReplayLog). Read-only observers of fight state — no gameplay changes.
    public sealed partial class BossSlayerGameManager
    {
        readonly List<ReplayPlayer> replayFrame = new();
        readonly HashSet<AttackState> replayMultiHitLogged = new();
        int[] replayLastCollectors;
        float replayFightStart;
        bool replayUltOpen;
        int replayUltGems, replayUltDamage, replayUltPending;

        string ReplayBoss()
        {
            if (bossModel == null) return "";
            var kind = BossPassiveState.Identify(bossModel.name);
            return kind != BossPassiveState.Kind.None ? kind.ToString() : bossModel.name;
        }

        int[] ReplayScores()
        {
            var scores = new int[Mathf.Max(playerCount, players.Count)];
            foreach (var p in players) if (p.Index < scores.Length) scores[p.Index] = p.Damage;
            return scores;
        }

        static int[] ReplayIndexes(List<PlayerState> list)
        {
            var result = new int[list.Count];
            for (int i = 0; i < list.Count; i++) result[i] = list[i].Index;
            return result;
        }

        bool ReplayPlayerUp(PlayerState p) => p.DownTimer <= 0f && p.Cursor != null && p.Cursor.gameObject.activeSelf;

        // The whole boss session is one replay round ("BossFight"), opened at the first fight's start (BeginBossSession
        // sets this; the fight start opens it) and closed at the session's result. Logged through the Fortal Replay
        // Signal package (Assets/FortalReplay), the same ReplayLog the Main Arena recorder reads.
        bool replaySessionPending;

        void ReplayEnd(string reason) { replaySessionPending = false; if (ReplayLog.CurrentGame == "BossFight") ReplayLog.EndSession(reason); }

        void ReplayBeginSession(int bossCount)
        {
            var all = new int[players.Count];
            for (int i = 0; i < players.Count; i++) all[i] = players[i].Index;
            ReplayLog.BeginSession("BossFight", players.Count, ReplayLog.Data().Add("bossCount", bossCount).Add("playerSlots", all));
        }

        void ReplayRoundStart()
        {
            replayUltOpen = false; replayUltPending = 0; replayLastCollectors = null; replayMultiHitLogged.Clear();
            ReplayLog.Event("round_start", 0, 0, null,
                ReplayLog.Data().Add("boss", ReplayBoss()).Add("round", sessionBossIndex).Add("scores", ReplayScores()));
        }

        void ReplayBossKilled()
        {
            string boss = ReplayBoss();
            ReplayLog.Event("boss_kill", 5, 10, replayLastCollectors, ReplayLog.Data().Add("boss", boss)
                .Add("fightSeconds", replayFightStart > 0f ? ReplayLog.SessionTime - replayFightStart : 0f).Add("bossIndexInSession", sessionBossIndex));
            ReplayLog.Event("round_end", 0, 0, null,
                ReplayLog.Data().Add("boss", boss).Add("round", sessionBossIndex).Add("scores", ReplayScores()));
        }

        void ReplaySessionCompleted(PlayerState mvp)
        {
            var winners = new int[players.Count];
            for (int i = 0; i < players.Count; i++) winners[i] = players[i].Index;
            ReplayLog.EndSession("completed", winners, ReplayScores(),
                ReplayLog.Data().Add("bossesDefeated", sessionBossIndex).Add("mvp", mvp != null ? mvp.Index : -1));
        }

        void ReplayUltimateStart(List<PlayerState> contributors)
        {
            replayUltOpen = true; replayUltGems = 0; replayUltDamage = 0; replayUltPending = 0;
            ReplayLog.Event("ultimate_start", 4, 2, ReplayIndexes(contributors), ReplayLog.Data().Add("boss", ReplayBoss()));
        }

        // Burst gems still flying at the boss when the window closes land afterwards — wait for them so totalDamage is complete.
        void ReplayTryEndUltimate()
        {
            if (!replayUltOpen || ultimateActive || replayUltPending > 0) return;
            replayUltOpen = false;
            ReplayLog.Event("ultimate_end", 10, 2, null, ReplayLog.Data().Add("boss", ReplayBoss())
                .Add("burstGemsTaken", replayUltGems).Add("totalDamage", replayUltDamage));
        }

        void ReplayBossTransform() => ReplayLog.Event("boss_transform", 2, 5, null, ReplayLog.Data().Add("boss", ReplayBoss()));

        void ReplayHeal(List<PlayerState> contributors)
        {
            int lowest = int.MaxValue;
            foreach (var p in players) if (ReplayPlayerUp(p)) lowest = Mathf.Min(lowest, p.Hp);
            if (lowest <= playerMaxHp * .25f)
                ReplayLog.Event("clutch_heal", 3, 2, ReplayIndexes(contributors), ReplayLog.Data().Add("lowestHp", lowest));
        }

        void ReplayAttackHits(AttackState attack)
        {
            int hits = attack.HitPlayers.Count;
            if (hits < 2 || replayMultiHitLogged.Contains(attack)) return;
            int active = 0;
            foreach (var p in players) if (ReplayPlayerUp(p) || attack.HitPlayers.Contains(p.Index)) active++;
            if (hits < (active >= 3 ? 3 : active)) return;
            replayMultiHitLogged.Add(attack);
            ReplayLog.Event("boss_hit_multi", 3, 2, new List<int>(attack.HitPlayers).ToArray(), ReplayLog.Data().Add("boss", ReplayBoss())
                .Add("attack", attack.View != null ? attack.View.name : attack.Shape.ToString()).Add("count", hits));
        }

        void ReplayPlayerDown(PlayerState p)
        {
            string boss = ReplayBoss();
            ReplayLog.Event("player_down", 3, 2, p.Index, ReplayLog.Data().Add("boss", boss));
            int down = 0;
            foreach (var other in players)
            {
                if (ReplayPlayerUp(other)) return;
                if (other.DownTimer > 0f) down++;
            }
            if (down >= 2) ReplayLog.Event("team_wipe", 3, 3, null, ReplayLog.Data().Add("boss", boss).Add("down", down));
        }

        void WriteReplayFrame()
        {
            if (!ReplayLog.ShouldSampleFrame()) return;
            replayFrame.Clear();
            var r = FloorRect;
            foreach (var p in players)
            {
                bool tracked = p.Cursor != null && (p.Cursor.gameObject.activeSelf || p.DownTimer > 0f);
                Vector2 local = p.Cursor != null ? p.Cursor.anchoredPosition : Vector2.zero;
                replayFrame.Add(new ReplayPlayer(p.Index, tracked, Mathf.InverseLerp(r.xMin, r.xMax, local.x), Mathf.InverseLerp(r.yMin, r.yMax, local.y),
                    p.Damage, p.DownTimer > 0f ? "down" : null));
            }
            string phase = selectingBoss ? "select" : awaitingPlayers ? "ready" : IsShowingBossVictory ? "victory" : resultShown ? (sessionActive ? "boss_death" : "result")
                : !gameStarted ? "entrance" : ultimateActive ? "ultimate" : stunTimer > 0f ? "stunned" : "fight";
            var hp = new int[players.Count];
            for (int i = 0; i < players.Count; i++) hp[i] = players[i].Hp;
            ReplayLog.WriteFrame(phase, replayFrame, ReplayLog.Data().Add("boss", ReplayBoss()).Add("round", sessionBossIndex)
                .Add("bossHp", bossHp).Add("bossMaxHp", bossMaxHp).Add("bossPhase", BossPhase()).Add("bossX", Mathf.InverseLerp(0f, arenaWidthMeters, bossX))
                .Add("stunGauge", Mathf.RoundToInt(stunGauge)).Add("ultimateCharge", ultimateCharge).Add("ultimateTimeLeft", ultimateActive ? ultimateTimer : 0f)
                .Add("playerHp", hp));
        }
    }
}
