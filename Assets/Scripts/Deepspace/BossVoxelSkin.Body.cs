using UnityEngine;

namespace Deepspace
{
    // Effects that cover each new model's own body. They replace the old model's flames, lightning, mist, water, rubble
    // and jets, which followed the old shapes and are now hidden (BossVoxelSkin.Bridge HideOldRenderers).
    public sealed partial class BossVoxelSkin
    {
        float pulseClock;
        readonly float[] bodyCarry = new float[4];

        string CurrentTheme()
        {
            if (wipeAge >= 0f && wipeTarget != null) return wipeTarget;
            foreach (var theme in themes) if (theme.material == body.sharedMaterial) return theme.name;
            return themes.Length > 0 ? themes[0].name : "";
        }

        // Emits `rate` per second through one of four carry slots, so low rates still come out evenly.
        int Due(int slot, float rate, float dt)
        {
            bodyCarry[slot] += rate * dt;
            int count = Mathf.FloorToInt(bodyCarry[slot]);
            bodyCarry[slot] -= count;
            return count;
        }

        static Color Spark(Color a, Color b) => Color.Lerp(a, b, Random.value);

        void BodyFx(float dt)
        {
            float s = Scale;
            string theme = CurrentTheme();
            switch (name)
            {
                case "Boss_Dragon":
                    if (theme == "inferno")
                        for (int i = Due(0, 150f, dt); i > 0; i--)
                            Emit(RandomSurfacePoint(), (Vector3.up * 2.4f + Random.insideUnitSphere * .6f) * s, Flame(), .12f, .55f);
                    for (int side = 0; side < 2; side++)
                        for (int i = Due(1 + side, 14f, dt); i > 0; i--)
                            Emit(Tip(side == 0 ? "wing_Lb" : "wing_Rb", Vector3.down), (Vector3.down * 1.5f + Random.insideUnitSphere * .4f) * s, new Color(.85f, .95f, 1f, .7f), .06f, .5f);
                    break;

                case "Boss_Demon_RedBoy":
                case "Boss_Demon_YakshaKing":
                    float flames = theme == "ember" ? 170f : name == "Boss_Demon_YakshaKing" ? 55f : 0f;
                    for (int i = Due(0, flames, dt); i > 0; i--)
                        Emit(RandomSurfacePoint(), (Vector3.up * 2.2f + Random.insideUnitSphere * .5f) * s, Flame(), .11f, .5f);
                    for (int side = 0; side < 2; side++)
                        for (int i = Due(1 + side, 22f, dt); i > 0; i--)
                            Emit(Tip(side == 0 ? "foot_L" : "foot_R", Vector3.down), (Vector3.up * .8f + Random.insideUnitSphere * .5f) * s, Ember, .06f, .6f);
                    break;

                case "Boss_Jellyfish":
                    for (int i = Due(0, 45f, dt); i > 0; i--)
                        Emit(RandomSurfacePoint("tent"), Random.insideUnitSphere * (1.6f * s), Spark(Aqua, Color.white), .07f, .2f);
                    break;

                case "Boss_VoidWraith":
                    for (int i = Due(0, theme == "mist" ? 45f : 20f, dt); i > 0; i--)
                        Emit(RandomSurfacePoint(), (Vector3.up * .6f + Random.insideUnitSphere * .35f) * s, Spark(Violet, new Color(.3f, .15f, .5f)), .09f, 1f);
                    break;

                case "Boss_SeaSerpent":
                    for (int i = Due(0, 45f, dt); i > 0; i--)
                    {
                        var at = Random.value < .5f ? RandomSurfacePoint("seg") : RandomSurfacePoint("tail");
                        Emit(at, (Vector3.down * .7f + Random.insideUnitSphere * .5f) * s, Spark(Aqua, Color.white), .07f, .7f);
                    }
                    break;

                case "Boss_CrystalGolem":
                    for (int side = 0; side < 2; side++)
                        for (int i = Due(side, 6f, dt); i > 0; i--)
                            Emit(Tip(side == 0 ? "leg_L" : "leg_R", Vector3.down), (Vector3.down * 2f + Random.insideUnitSphere * .5f) * s, new Color(.45f, .38f, .3f), .1f, .7f);
                    if (theme == "crimson")
                        for (int i = Due(2, 30f, dt); i > 0; i--)
                            Emit(RandomSurfacePoint(), Random.insideUnitSphere * (.9f * s), Spark(new Color(1f, .15f, .25f), new Color(1f, .6f, .7f)), .08f, .45f);
                    break;

                case "Boss_Sentinel":
                    for (int side = 0; side < 2; side++)
                        for (int i = Due(side, 60f, dt); i > 0; i--)
                            Emit(Tip(side == 0 ? "foot_L" : "foot_R", Vector3.down), (Vector3.down * 3.5f + Random.insideUnitSphere * .35f) * s,
                                Random.value < .5f ? new Color(.55f, .85f, 1f) : Ember, .08f, .32f);
                    pulseClock -= dt;
                    if (pulseClock <= 0f) { pulseClock = .9f; Burst(Tip("chest", transform.forward), 10, 2.5f, Gold, .07f, .35f); }
                    break;

                case "Boss_Hydra":
                    for (int i = Due(0, 22f, dt); i > 0; i--)
                        Emit(RandomSurfacePoint("head"), (Vector3.down * 1.2f + Random.insideUnitSphere * .3f) * s, new Color(.45f, 1f, .35f), .06f, .6f);
                    break;
            }
        }

        // Called by BossHydra when an old head slot is cut off: the matching new head bursts as it disappears.
        public void SeverHead(int oldSlot)
        {
            EnsureFx();
            var head = Bone("head" + HydraOldToNew[Mathf.Clamp(oldSlot, 0, HydraOldToNew.Length - 1)]);
            Burst(head.position, 70, 9f, new Color(.4f, 1f, .35f), .14f, .9f);
        }
    }
}
