using UnityEngine;

namespace Deepspace
{
    // Per-fight state only: no asset mutation, scene lookups, or gameplay random draws.
    public sealed class BossPassiveState
    {
        // บาส 2026-10-20: AbyssalKing is the 15-minute session's final boss - never on the 9-card select (cards map index i -> Kind i+1).
        public enum Kind { None, Demon, Fish, Dragon, Jellyfish, Golem, Wraith, Sentinel, Serpent, Hydra, AbyssalKing }
        public Kind Active { get; private set; }
        float clock, quietSeconds, healingFraction;
        int healed, attacks, repeatedHits, brokenCycle = -1;
        string previousSource;
        public int AttacksLaunched => attacks;
        public bool DemonAdult {get;private set;}
        public bool DemonPhasePending {get;private set;}
        public void EnterDemonAdult(){if(Active==Kind.Demon && DemonPhasePending){DemonAdult=true;DemonPhasePending=false;}}
        public bool DragonHeated { get; private set; }
        public bool DragonPhasePending { get; private set; }
        public void ObserveHealth(float ratio) { if(Active == Kind.Dragon && !DragonHeated && ratio > 0 && ratio <= .5f) DragonPhasePending = true; if(Active==Kind.Demon && !DemonAdult && ratio>0 && ratio<=.5f)DemonPhasePending=true; }
        public void EnterDragonInferno() { if(Active == Kind.Dragon && DragonPhasePending){DragonHeated=true;DragonPhasePending=false;} }
        public int TotalHealing => healed;
        public static float HydraVulnerability(int heads) => 1f + Mathf.Clamp(heads - 3, 0, 4) * .05f;
        public float ScorchSeconds => Active == Kind.Dragon ? 1f : 0;
        // Abyssal King: awakens at 50% HP (BossSlayerGameManager.AbyssalKing.cs plays the roar) - faster and harder hitting.
        public bool KingAwakened { get; private set; }
        public void EnterKingAwakened() { if (Active == Kind.AbyssalKing) KingAwakened = true; }

        public static Kind Identify(string name)
        {
            if (string.IsNullOrEmpty(name)) return Kind.None;
            if (name.Contains("AbyssalKing")) return Kind.AbyssalKing;
            if (name.Contains("Hydra")) return Kind.Hydra;
            if (name.Contains("Demon")) return Kind.Demon;
            if (name.Contains("Giant Fish")) return Kind.Fish;
            if (name.Contains("Dragon")) return Kind.Dragon;
            if (name.Contains("JeelyFish") || name.Contains("Jellyfish")) return Kind.Jellyfish;
            if (name.Contains("CrystalGolem")) return Kind.Golem;
            if (name.Contains("VoidWraith")) return Kind.Wraith;
            if (name.Contains("Sentinel")) return Kind.Sentinel;
            if (name.Contains("SeaSerpent")) return Kind.Serpent;
            return Kind.None;
        }

        public void Reset(string name, bool enabled = true)
        {
            Active = enabled ? Identify(name) : Kind.None;
            DragonHeated = false; DragonPhasePending = false;DemonAdult=false;DemonPhasePending=false;KingAwakened=false;
            clock = quietSeconds = healingFraction = 0;
            healed = attacks = repeatedHits = 0;
            brokenCycle = -1; previousSource = null;
        }
        bool JellyShield => Active == Kind.Jellyfish && clock % 10f >= 7f && brokenCycle != Mathf.FloorToInt(clock / 10f);
        bool Mist => Active == Kind.Wraith && clock % 8f >= 6f && brokenCycle != Mathf.FloorToInt(clock / 8f);
        public bool ShieldUp => JellyShield;
        public bool MistUp => Mist;
        bool Enraged(float hpRatio) => Active == Kind.Demon && hpRatio <= .3f;
        public float AttackGapMultiplier(float hpRatio) => KingAwakened ? .85f : DemonAdult ? .8f : DragonHeated ? .75f : Enraged(hpRatio) ? .9f : 1;
        public float OutgoingDamageMultiplier(float hpRatio) => KingAwakened ? 1.15f : DemonAdult ? 1.2f : DragonHeated ? 1.3f : Enraged(hpRatio) ? 1.1f : 1;

        public int Advance(float dt, int hp, int maxHp, bool suspended)
        {
            if (dt <= 0 || suspended || hp <= 0 || Active == Kind.None) return 0;
            float previousQuiet = quietSeconds;
            clock += dt; quietSeconds += dt;
            if (Active != Kind.Fish || maxHp <= 0 || hp >= maxHp || healed >= Mathf.FloorToInt(maxHp * .04f)) return 0;
            // Only count the part of this frame after five uninterrupted quiet seconds.
            float recoveryTime = Mathf.Max(0, quietSeconds - 5) - Mathf.Max(0, previousQuiet - 5);
            healingFraction += recoveryTime * maxHp * .003f;
            int amount = Mathf.Min(Mathf.FloorToInt(healingFraction), Mathf.Min(maxHp - hp, Mathf.FloorToInt(maxHp * .04f) - healed));
            healingFraction -= amount; healed += amount;
            return amount;
        }

        public float IncomingMultiplier(string source, bool stunned)
        {
            quietSeconds = healingFraction = 0;
            if (Active == Kind.Sentinel)
            {
                if (stunned) { previousSource = null; repeatedHits = 0; return 1; }
                if (previousSource == source) repeatedHits++; else { previousSource = source; repeatedHits = 1; }
                return repeatedHits >= 3 ? .75f : 1;
            }
            if (Active == Kind.Golem) return stunned ? 1.2f : .9f;
            if (Active == Kind.Jellyfish) return !stunned && JellyShield ? .75f : 1;
            if (Active == Kind.Wraith) return !stunned && Mist ? .7f : 1.15f;
            return 1;
        }

        public void OnStun()
        {
            previousSource = null; repeatedHits = 0;
            if (Active == Kind.Jellyfish && JellyShield) brokenCycle = Mathf.FloorToInt(clock / 10f);
            if (Active == Kind.Wraith && Mist) brokenCycle = Mathf.FloorToInt(clock / 8f);
        }

        public int ChoosePattern(int ordinaryPattern)
        {
            attacks++;
            // Replaces this cast with the existing sequential, fully telegraphed double wave.
            return Active == Kind.Serpent && attacks % 4 == 0 ? 8 : ordinaryPattern;
        }

        public string Feedback(int hp, int maxHp, bool stunned, int heads, int pending, out bool active, out Color color)
        {
            active = false; color = new Color(.3f, .85f, 1f);
            switch (Active)
            {
                case Kind.Demon:
                    active = Enraged(hp / (float)Mathf.Max(1, maxHp)); color = new Color(1f, .25f, .08f);
                    active=DemonAdult;return DemonAdult ? "PHASE 2: YAKSHA KING\nBlade waves | Damage +20%" : "PHASE 1: RED BOY\nTransforms at 50% HP";
                case Kind.Fish:
                    active = !stunned && hp < maxHp && quietSeconds >= 5f && healed < Mathf.FloorToInt(maxHp * .04f);
                    color = new Color(.2f, 1f, .5f);
                    return active ? "RECOVERING HP\nHit the boss to interrupt!" : healed >= Mathf.FloorToInt(maxHp * .04f) ? "RECOVERY EXHAUSTED\nHealing limit reached" : "TIDAL RECOVERY\nHeals after 5s without a hit";
                case Kind.Dragon:
                    active = !stunned; color = new Color(1f, .5f, .05f);
                    return DragonHeated ? "PHASE 2: INFERNO\nDamage +30% | Attack gaps -25%" : "SCORCHED EARTH\nInferno phase at 50% HP";
                case Kind.Jellyfish:
                    active = JellyShield && !stunned; color = new Color(.3f, .9f, 1f);
                    return active ? "SHIELD ACTIVE\nBlocks 25% damage | Stun to break!" : "SHIELD DOWN\nAttack now!";
                case Kind.Golem:
                    active = true; color = stunned ? new Color(1f, .8f, .25f) : new Color(.55f, .5f, 1f);
                    return stunned ? "ARMOR BROKEN\nTakes +20% damage!" : "CRYSTAL ARMOR ACTIVE\nBlocks 10% damage | Stun to break!";
                case Kind.Wraith:
                    active = Mist && !stunned; color = new Color(.75f, .4f, 1f);
                    return active ? "MIST FORM ACTIVE\nBlocks 30% damage | Stun to reveal!" : "SOLID FORM\nTakes +15% damage!";
                case Kind.Sentinel:
                    active = repeatedHits >= 3 && !stunned; color = new Color(1f, .8f, .2f);
                    return active ? "ADAPTIVE ARMOR ACTIVE\nBlocks 25% | Change gem type!" : "ADAPTIVE ARMOR\nSame gem hits: " + repeatedHits + "/3";
                case Kind.Serpent:
                    active = attacks > 0 && attacks % 4 == 0; color = new Color(.15f, .8f, 1f);
                    return "STORM TIDE\n" + ((attacks + 1) % 4 == 0 ? "Next attack: DOUBLE WAVE!" : "Double wave every 4th attack");
                case Kind.Hydra:
                    active = pending > 0; color = new Color(.3f, 1f, .55f);
                    return (active ? "HEADS REGROWING!" : "HYDRA HEADS " + heads + "/7") + "\n" + heads + " breaths | Takes +" + Mathf.RoundToInt((HydraVulnerability(heads) - 1f) * 100f) + "% damage";
                case Kind.AbyssalKing:
                    active = KingAwakened && !stunned; color = KingAwakened ? new Color(1f, .35f, .85f) : new Color(.7f, .45f, 1f);
                    return KingAwakened ? "PHASE 2: THE CROWN IGNITES\nDamage +15% | Attack gaps -15%" : "SOVEREIGN OF THE ABYSS\nAwakens at 50% HP";
                default: return string.Empty;
            }
        }
        public string Describe(float hpRatio, bool stunned, int headCount)
        {
            switch (Active)
            {
                case Kind.Demon: return DemonAdult ? "YAKSHA KING | Blade waves / +20% damage" : "RED BOY | Transforms at 50% HP";
                case Kind.Fish: return "TIDAL RECOVERY | Hit within 5s to stop healing (4% cap)";
                case Kind.Dragon: return DragonHeated ? "INFERNO PHASE | +30% damage / 25% shorter attack gaps" : "SCORCHED EARTH | Inferno phase at 50% HP";
                case Kind.Jellyfish: return JellyShield && !stunned ? "PULSE SHIELD | Damage -25% - use stun!" : "PULSE SHIELD | Shield down - attack now";
                case Kind.Golem: return stunned ? "CRYSTAL ARMOR BROKEN | +20% damage taken" : "CRYSTAL ARMOR | Damage -10% - break with stun";
                case Kind.Wraith: return Mist && !stunned ? "MIST FORM | Damage -30% - stun to reveal" : "SOLID FORM | +15% damage taken - attack now";
                case Kind.Sentinel: return repeatedHits >= 3 ? "ADAPTIVE ARMOR | Swap gem type or use stun!" : "ADAPTIVE ARMOR | Third same-type hit deals 25% less damage";
                case Kind.Serpent: return "STORM TIDE | " + ((attacks + 1) % 4 == 0 ? "Next cast: DOUBLE WAVE" : "Double wave every fourth cast");
                case Kind.Hydra: return "REGENERATION | " + headCount + "/7 heads | 1 breath/head | Takes +" + Mathf.RoundToInt((HydraVulnerability(headCount) - 1) * 100) + "% damage";
                case Kind.AbyssalKing: return KingAwakened ? "THE CROWN IGNITES | +15% damage / 15% shorter attack gaps" : "SOVEREIGN OF THE ABYSS | Awakens at 50% HP";
                default: return string.Empty;
            }
        }
    }
}
namespace Deepspace
{
    // Dedicated, high-contrast wall feedback; never changes boss materials or gameplay.
    public sealed class BossPassiveFeedback : UnityEngine.MonoBehaviour
    {
        UnityEngine.RectTransform panel;
        UnityEngine.UI.Image background;
        UnityEngine.UI.Outline border;
        TMPro.TextMeshProUGUI label, notice;
        UnityEngine.LineRenderer aura;
        UnityEngine.Material auraMaterial;
        UnityEngine.Canvas overlayCanvas;

        readonly System.Collections.Generic.List<UnityEngine.LineRenderer> glyphs = new();
        readonly System.Collections.Generic.List<UnityEngine.LineRenderer> glyphBacks = new();
        UnityEngine.Transform cachedBoss;
        UnityEngine.Renderer[] cachedRenderers;
        UnityEngine.Vector3 effectCenter, effectRight, effectUp;
        float effectRadius;
        public int VisibleGlyphCount { get; private set; }
        public BossPassiveState.Kind VisualKind { get; private set; }
        sealed class MistMaterials
        {
            public UnityEngine.Renderer Renderer;
            public UnityEngine.Material[] Original, Ghost;
        }
        readonly System.Collections.Generic.List<MistMaterials> mistMaterials = new();
        UnityEngine.Transform mistBoss;
        public bool IsMistTransparent => mistBoss != null && mistMaterials.Count > 0;
        string lastMessage;
        float flashUntil;
        public void Initialize(UnityEngine.RectTransform canvas, TMPro.TMP_FontAsset font)
        {
            var go = new UnityEngine.GameObject("Passive Status Banner", typeof(UnityEngine.RectTransform), typeof(UnityEngine.UI.Image));
            panel = go.GetComponent<UnityEngine.RectTransform>(); panel.SetParent(canvas, false);
            var sourceCanvas = canvas.GetComponentInParent<UnityEngine.Canvas>().rootCanvas;
            var root = new UnityEngine.GameObject("Passive Overlay Canvas", typeof(UnityEngine.RectTransform), typeof(UnityEngine.Canvas), typeof(UnityEngine.UI.CanvasScaler));
            root.transform.SetParent(transform, false); root.layer = Visual3DFollower.UILayer;
            overlayCanvas = root.GetComponent<UnityEngine.Canvas>();
            overlayCanvas.renderMode = UnityEngine.RenderMode.ScreenSpaceOverlay;
            overlayCanvas.targetDisplay = sourceCanvas.targetDisplay;
            overlayCanvas.sortingOrder = 200;
            var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = canvas.rect.size; scaler.matchWidthOrHeight = .5f;
            panel.SetParent(root.transform, false);
            panel.anchorMin = panel.anchorMax = new UnityEngine.Vector2(.975f, .04f);
            panel.pivot = new UnityEngine.Vector2(1f, 0f);
            panel.sizeDelta = new UnityEngine.Vector2(canvas.rect.width * .29f, canvas.rect.height * .24f);
            background = go.GetComponent<UnityEngine.UI.Image>(); background.raycastTarget = false;
            border = go.AddComponent<UnityEngine.UI.Outline>(); border.effectDistance = new UnityEngine.Vector2(4f, -4f);
            label = Text("Passive State", font, new UnityEngine.Vector2(.025f, .05f), new UnityEngine.Vector2(.975f, .7f));
            notice = Text("Passive Trigger", font, new UnityEngine.Vector2(.025f, .71f), new UnityEngine.Vector2(.975f, .98f));
            var ring = new UnityEngine.GameObject("VFX == Passive Aura", typeof(UnityEngine.LineRenderer));
            ring.transform.SetParent(transform, false); ring.layer = Visual3DFollower.BossLayer;
            aura = ring.GetComponent<UnityEngine.LineRenderer>(); aura.useWorldSpace = true; aura.loop = true; aura.positionCount = 64;
            aura.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; aura.receiveShadows = false;
            var shader = UnityEngine.Shader.Find("Sprites/Default");
            if (shader != null) { auraMaterial = new UnityEngine.Material(shader); aura.sharedMaterial = auraMaterial; }
            aura.enabled = false;
            Visual3DFollower.SetLayerRecursive(panel.gameObject, Visual3DFollower.UILayer);
        }
        TMPro.TextMeshProUGUI Text(string name, TMPro.TMP_FontAsset font, UnityEngine.Vector2 min, UnityEngine.Vector2 max)
        {
            var go = new UnityEngine.GameObject(name, typeof(UnityEngine.RectTransform), typeof(TMPro.TextMeshProUGUI));
            var rect = go.GetComponent<UnityEngine.RectTransform>(); rect.SetParent(panel, false);
            rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = UnityEngine.Vector2.zero;
            var text = go.GetComponent<TMPro.TextMeshProUGUI>(); if (font != null) text.font = font;
            text.alignment = TMPro.TextAlignmentOptions.Center; text.fontStyle = TMPro.FontStyles.Bold;
            text.enableAutoSizing = true; text.fontSizeMin = 12; text.fontSizeMax = panel.sizeDelta.y * .29f;
            text.color = UnityEngine.Color.white; text.raycastTarget = false;
            return text;
        }
        public UnityEngine.Color ThemePanel, ThemeEdge; public float ThemeWeight;
        // บาส 2026-11-18: "เอาคำแนะนำระหว่างเล่นออกไป" - the wall's passive tip box (PASSIVE ACTIVE / how to beat it) is
        // no longer shown. The aura and ability symbols around the boss stay. Set true to bring the box back.
        public static bool ShowBanner = false;
        public void Notify(string message) { if (notice == null) return; notice.text = message; flashUntil = UnityEngine.Time.time + 1.5f; }
        public void Show(string message, bool active, UnityEngine.Color color, UnityEngine.Transform boss, UnityEngine.Camera camera, BossPassiveState.Kind kind, bool stunned)
        {
            if (panel.gameObject.activeSelf != ShowBanner) panel.gameObject.SetActive(ShowBanner);

            if (message != lastMessage)
            {
                if (lastMessage != null && UnityEngine.Time.time >= flashUntil) Notify(message.Split('\n')[0]);
                lastMessage = message;
            }
            label.text = message;
            bool flash = UnityEngine.Time.time < flashUntil;
            if (!flash) notice.text = active ? "PASSIVE ACTIVE" : "BOSS PASSIVE";
            notice.color = color;
            float pulse = .5f + .5f * UnityEngine.Mathf.Sin(UnityEngine.Time.time * 7f);
            // บาส 2026-09-29: resting plate/border wear the boss UI theme; active/flash keep the semantic passive colour.
            var restDark = UnityEngine.Color.Lerp(new UnityEngine.Color(.015f, .02f, .045f, .96f), new UnityEngine.Color(ThemePanel.r, ThemePanel.g, ThemePanel.b, .96f), ThemeWeight);
            background.color = UnityEngine.Color.Lerp(restDark, new UnityEngine.Color(color.r * .22f, color.g * .22f, color.b * .22f, .96f), active || flash ? .6f + pulse * .4f : .12f);
            border.effectColor = active || flash ? color : UnityEngine.Color.Lerp(color * .55f, ThemeEdge, ThemeWeight);
            panel.localScale = UnityEngine.Vector3.one * (flash ? 1f + .018f * pulse : 1f);
            SetMistAppearance(boss, kind == BossPassiveState.Kind.Wraith && active);
            VisualKind = kind;
            ClearGlyphs();
            aura.enabled = (active || flash) && boss != null && boss.gameObject.activeInHierarchy && camera != null;
            if (!aura.enabled) return;
            if (cachedBoss != boss || BossVoxelSkin.ActiveOn(boss) != null) // the Demon changes model mid-fight
            {
                cachedBoss = boss;
                cachedRenderers = BossVoxelSkin.VisibleRenderers(boss);
            }
            bool hasBounds = false;
            var bounds = new UnityEngine.Bounds();
            foreach (var renderer in cachedRenderers)
            {
                if (!(renderer is UnityEngine.MeshRenderer) && !(renderer is UnityEngine.SkinnedMeshRenderer)) continue;
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; } else bounds.Encapsulate(renderer.bounds);
            }
            if (!hasBounds) { aura.enabled = false; return; }
            effectRadius = UnityEngine.Mathf.Max(bounds.extents.x, bounds.extents.y) * 1.03f;
            effectCenter = bounds.center - camera.transform.forward * (bounds.extents.magnitude + .05f);
            effectRight = camera.transform.right; effectUp = camera.transform.up;
            // Keep passive symbols inside the boss area, away from HP and ultimate gauges.
            if (camera.orthographic)
            {
                float centerY = camera.WorldToViewportPoint(effectCenter).y;
                float room = UnityEngine.Mathf.Max(.05f, UnityEngine.Mathf.Min(centerY - .23f, .80f - centerY));
                effectRadius = UnityEngine.Mathf.Min(effectRadius, camera.orthographicSize * 2f * room / 1.3f);
            }
            aura.widthMultiplier = UnityEngine.Mathf.Max(.01f, effectRadius * .035f);
            var tint = active ? color : UnityEngine.Color.Lerp(color, UnityEngine.Color.white, .7f);
            tint.a = active ? .85f + .15f * pulse : .35f;
            aura.startColor = aura.endColor = tint;
            for (int i = 0; i < 64; i++)
            {
                float angle = i * UnityEngine.Mathf.PI * 2f / 64f;
                float radius = 1.03f + .035f * pulse;
                if (kind == BossPassiveState.Kind.Demon && active) radius += i % 4 == 0 ? .12f : 0f;
                aura.SetPosition(i, Point(new UnityEngine.Vector2(UnityEngine.Mathf.Cos(angle), UnityEngine.Mathf.Sin(angle)) * radius));
            }
            // A finished passive never keeps showing its healing/shield/attack symbols.
            if (active) DrawAbility(kind, color, stunned, UnityEngine.Time.time);
            else if (flash && (kind == BossPassiveState.Kind.Jellyfish || kind == BossPassiveState.Kind.Golem || kind == BossPassiveState.Kind.Sentinel))
                DrawShards(color, UnityEngine.Time.time);
        }
        UnityEngine.Vector3 Point(UnityEngine.Vector2 p) => effectCenter + (effectRight * p.x + effectUp * p.y) * effectRadius;
        UnityEngine.LineRenderer MakeGlyph(string name)
        {
            var go = new UnityEngine.GameObject(name, typeof(UnityEngine.LineRenderer));
            go.transform.SetParent(transform, false); go.layer = Visual3DFollower.BossLayer;
            var line = go.GetComponent<UnityEngine.LineRenderer>(); line.useWorldSpace = true;
            line.sharedMaterial = auraMaterial; line.numCapVertices = 3; line.numCornerVertices = 3;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; line.receiveShadows = false;
            return line;
        }
        void Stroke(UnityEngine.Color color, float width, params UnityEngine.Vector2[] points)
        {
            int index = VisibleGlyphCount++;
            if (index == glyphs.Count)
            {
                glyphBacks.Add(MakeGlyph("Passive glyph contrast"));
                glyphs.Add(MakeGlyph("Passive ability glyph"));
            }
            var line = glyphs[index]; var back = glyphBacks[index];
            line.enabled = back.enabled = true; line.positionCount = back.positionCount = points.Length;
            line.widthMultiplier = effectRadius * width; back.widthMultiplier = line.widthMultiplier * 1.8f;
            color.a = 1f; line.startColor = line.endColor = color;
            back.startColor = back.endColor = new UnityEngine.Color(.01f,.015f,.03f,.95f);
            var behind = UnityEngine.Vector3.Cross(effectRight, effectUp) * effectRadius * .003f;
            for (int i = 0; i < points.Length; i++) { line.SetPosition(i, Point(points[i])); back.SetPosition(i, Point(points[i]) + behind); }
        }
        void Polygon(UnityEngine.Color color, UnityEngine.Vector2 center, float radius, int sides, float turn)
        {
            var points = new UnityEngine.Vector2[sides + 1];
            for (int i = 0; i <= sides; i++) { float a = turn + i * UnityEngine.Mathf.PI * 2 / sides; points[i] = center + new UnityEngine.Vector2(UnityEngine.Mathf.Cos(a),UnityEngine.Mathf.Sin(a)) * radius; }
            Stroke(color,.035f,points);
        }
        void DrawShards(UnityEngine.Color color, float time)
        {
            for (int i = 0; i < 8; i++)
            {
                float a = i * UnityEngine.Mathf.PI / 4;
                var direction = new UnityEngine.Vector2(UnityEngine.Mathf.Cos(a),UnityEngine.Mathf.Sin(a));
                var side = new UnityEngine.Vector2(-direction.y,direction.x);
                var center = direction * (1.03f + .12f * (.5f + .5f * UnityEngine.Mathf.Sin(time * 5)));
                Stroke(color,.035f,center-side*.1f,center+direction*.16f,center+side*.1f);
            }
        }
        void DrawAbility(BossPassiveState.Kind kind, UnityEngine.Color color, bool stunned, float time)
        {
            switch (kind)
            {
                case BossPassiveState.Kind.Fish:
                    for (int i = 0; i < 6; i++)
                    {
                        var c = new UnityEngine.Vector2(i % 2 == 0 ? -.8f : .8f, -.65f + UnityEngine.Mathf.Repeat(time * .55f + i / 3f,1.4f));
                        Stroke(color,.055f,c+UnityEngine.Vector2.left*.11f,c+UnityEngine.Vector2.right*.11f);
                        Stroke(color,.055f,c+UnityEngine.Vector2.down*.11f,c+UnityEngine.Vector2.up*.11f);
                    }
                    break;
                case BossPassiveState.Kind.Jellyfish:
                    Polygon(color,UnityEngine.Vector2.zero,.97f,6,UnityEngine.Mathf.PI/6);
                    Polygon(UnityEngine.Color.white,UnityEngine.Vector2.zero,1.03f,6,UnityEngine.Mathf.PI/6);
                    for(int i=0;i<6;i++){float a=i*UnityEngine.Mathf.PI/3;Polygon(color,new UnityEngine.Vector2(UnityEngine.Mathf.Cos(a),UnityEngine.Mathf.Sin(a))*.83f,.16f,6,0);}
                    break;
                case BossPassiveState.Kind.Golem:
                    if(stunned){DrawShards(new UnityEngine.Color(1,.8f,.3f),time);break;}
                    for(int i=0;i<8;i++){float a=i*UnityEngine.Mathf.PI/4;Polygon(color,new UnityEngine.Vector2(UnityEngine.Mathf.Cos(a),UnityEngine.Mathf.Sin(a))*.96f,.17f,4,a);}
                    break;
                case BossPassiveState.Kind.Sentinel:
                    for(int i=0;i<3;i++) Polygon(color,new UnityEngine.Vector2((i-1)*.48f,.78f),.24f,6,UnityEngine.Mathf.PI/6);
                    Stroke(UnityEngine.Color.white,.035f,new UnityEngine.Vector2(-.65f,-.6f),new UnityEngine.Vector2(-.85f,-.6f),new UnityEngine.Vector2(-.85f,.6f),new UnityEngine.Vector2(-.65f,.6f));
                    Stroke(UnityEngine.Color.white,.035f,new UnityEngine.Vector2(.65f,-.6f),new UnityEngine.Vector2(.85f,-.6f),new UnityEngine.Vector2(.85f,.6f),new UnityEngine.Vector2(.65f,.6f));
                    break;
                case BossPassiveState.Kind.Wraith:
                    for(int j=0;j<3;j++)
                    {
                        var points=new UnityEngine.Vector2[40];
                        for(int i=0;i<40;i++){float a=i*.11f+time*(j%2==0?1:-1)+j*2.1f;float r=.72f+i*.007f;points[i]=new UnityEngine.Vector2(UnityEngine.Mathf.Cos(a)*r,UnityEngine.Mathf.Sin(a)*r*.8f);}
                        Stroke(UnityEngine.Color.Lerp(color,UnityEngine.Color.white,j*.2f),.045f,points);
                    }
                    break;
                case BossPassiveState.Kind.Serpent:
                    for(int j=0;j<2;j++)
                    {
                        var points=new UnityEngine.Vector2[40];
                        for(int i=0;i<40;i++){float x=-.95f+i*1.9f/39f;points[i]=new UnityEngine.Vector2(x,-.45f+j*.75f+UnityEngine.Mathf.Sin(x*9-time*5)*.12f);}
                        Stroke(j==0?color:UnityEngine.Color.white,.06f,points);
                    }
                    break;
                case BossPassiveState.Kind.Hydra:
                    for(int i=0;i<3;i++)
                    {
                        float x=(i-1)*.65f; float rise=.08f*UnityEngine.Mathf.Sin(time*5+i);
                        Stroke(color,.05f,new UnityEngine.Vector2(x,-.6f),new UnityEngine.Vector2(x,.4f+rise),new UnityEngine.Vector2(x-.18f,.65f+rise));
                        Stroke(color,.05f,new UnityEngine.Vector2(x,.4f+rise),new UnityEngine.Vector2(x+.18f,.65f+rise));
                        Polygon(UnityEngine.Color.white,new UnityEngine.Vector2(x-.18f,.69f+rise),.075f,4,0);
                        Polygon(UnityEngine.Color.white,new UnityEngine.Vector2(x+.18f,.69f+rise),.075f,4,0);
                    }
                    break;
                default: // Demon rage and Dragon fire: animated tongues around the body.
                    for(int i=0;i<8;i++)
                    {
                        float a=i*UnityEngine.Mathf.PI/4; var d=new UnityEngine.Vector2(UnityEngine.Mathf.Cos(a),UnityEngine.Mathf.Sin(a));var side=new UnityEngine.Vector2(-d.y,d.x);
                        float reach=1.13f+.1f*UnityEngine.Mathf.Sin(time*8+i*2);
                        Stroke(color,.05f,d*.86f-side*.11f,d*reach-side*.03f,d*1.04f+side*.05f,d*.89f+side*.12f);
                    }
                    break;
            }
        }
        void SetMistAppearance(UnityEngine.Transform boss, bool active)
        {
            if (!active || boss == null || mistBoss != boss)
            {
                if(mistBoss!=null) mistBoss.GetComponent<BossWraithAnimation>()?.SetMistForm(false);
                foreach (var slot in mistMaterials)
                {
                    if (slot.Renderer != null) slot.Renderer.sharedMaterials = slot.Original;
                    for (int i = 0; i < slot.Ghost.Length; i++)
                        if (slot.Ghost[i] != null && slot.Ghost[i] != slot.Original[i])
                        {
                            if (UnityEngine.Application.isPlaying) Destroy(slot.Ghost[i]); else DestroyImmediate(slot.Ghost[i]);
                        }
                }
                mistMaterials.Clear(); mistBoss = null;
            }
            if(boss!=null) boss.GetComponent<BossWraithAnimation>()?.SetMistForm(active);
            if (!active || boss == null || mistBoss == boss) return;
            mistBoss = boss;
            foreach (var renderer in boss.GetComponentsInChildren<UnityEngine.Renderer>(true))
            {
                if (!(renderer is UnityEngine.MeshRenderer) && !(renderer is UnityEngine.SkinnedMeshRenderer)) continue;
                if (renderer.GetComponentInParent<BossVoxelSkin>(true) != null) continue; // the new model shows mist with its own theme
                var original = renderer.sharedMaterials;
                var ghost = (UnityEngine.Material[])original.Clone();
                bool changed = false;
                for (int i = 0; i < original.Length; i++)
                {
                    var material = original[i];
                    if (material == null || !material.HasProperty("_Alpha") || !material.HasProperty("_SrcBlend")) continue;
                    var clone = new UnityEngine.Material(material) { name = material.name + " (Mist Form runtime)" };
                    clone.SetFloat("_Alpha", .28f);
                    clone.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    clone.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    clone.SetFloat("_ZWrite", 0f);
                    clone.SetOverrideTag("RenderType", "Transparent");
                    clone.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    ghost[i] = clone; changed = true;
                }
                if (!changed) continue;
                mistMaterials.Add(new MistMaterials { Renderer = renderer, Original = original, Ghost = ghost });
                renderer.sharedMaterials = ghost;
            }
        }
        void ClearGlyphs()
        {
            foreach(var glyph in glyphs) glyph.enabled = false;
            foreach(var glyph in glyphBacks) glyph.enabled = false;
            VisibleGlyphCount = 0;
        }        public void Hide() { SetMistAppearance(null, false); ClearGlyphs(); if (panel != null) panel.gameObject.SetActive(false); if (aura != null) aura.enabled = false; lastMessage = null; flashUntil = 0f; }
        void OnDisable() { Hide(); }
        void OnDestroy()
        {
            if (overlayCanvas != null) { if (UnityEngine.Application.isPlaying) Destroy(overlayCanvas.gameObject); else DestroyImmediate(overlayCanvas.gameObject); }
            if (panel != null) { if (UnityEngine.Application.isPlaying) Destroy(panel.gameObject); else DestroyImmediate(panel.gameObject); }
            if (auraMaterial != null) { if (UnityEngine.Application.isPlaying) Destroy(auraMaterial); else DestroyImmediate(auraMaterial); }
        }
    }
}