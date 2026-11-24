using UnityEngine;

namespace Deepspace
{
    public sealed partial class BossSlayerGameManager
    {
        void ConfigureFloorEffect(AttackState attack, string title)
        {
            attack.FloorTheme = BossAttackVfx.ThemeFor(bossModel != null ? bossModel.name : "");
            attack.FloorForm = BossAttackVfx.FormFor(title, attack.Shape == Shape.Circle, attack.Shape == Shape.Zone, attack.Shape == Shape.Wave);
            string upper = title.ToUpperInvariant();
            bool spreadingFire = attack.FloorTheme == 3 || (attack.FloorTheme == 0 &&
                (upper.Contains("HELLFIRE") || upper.Contains("INFERNAL")));
            if (!spreadingFire || attack.Shape == Shape.Wave) return;
            attack.FireSpreadSeconds = attack.Shape == Shape.Circle ? .5f : .7f;
            attack.Lingering = true;
            // Keep the full footprint warned; actual fire and damage advance together after release.
            float burnSeconds = attack.FloorTheme == 3 ? 1.45f : 1.1f;
            attack.ActiveTimer = Mathf.Max(attack.ActiveTimer, attack.TelegraphTimer + burnSeconds);
        }

        static float FireFront(AttackState attack) => attack.FireSpreadSeconds > 0
            ? Mathf.Clamp01(-attack.TelegraphTimer / attack.FireSpreadSeconds) : 1f;

        bool IsBehindFireFront(AttackState attack, Vector2 point)
        {
            if (attack.FireSpreadSeconds <= 0) return true;
            float progress = FireFront(attack);
            if (progress <= 0 || attack.ActiveTimer <= 0) return false;
            if (attack.Shape == Shape.Circle)
                return Vector2.Distance(point, new Vector2(attack.X,attack.Z)) < attack.Radius * progress;
            // Floor UI Y is reversed: z=0 is the edge nearest the boss.
            return point.y >= 0 && point.y < arenaDepthMeters * progress;
        }
    }
}
