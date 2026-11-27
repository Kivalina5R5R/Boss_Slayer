using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BossSlayer
{
    public sealed class BossFightJuice : MonoBehaviour
    {
        public BossJuiceGraphic Floor { get; private set; }
        public BossJuiceGraphic Wall { get; private set; }
        sealed class Number { public TMP_Text text; public Vector2 origin; public float age; public Color tint; }
        readonly Number[] numbers = new Number[12];
        int next;
        // 2026-11-27 "ปรับขนาดตัวเลขให้ใหญ่ขึ้นอีกนิด": was 130.
        const float LabelSize = 165;
        TMP_FontAsset font;
        GameObject wallOverlay;
        public int ActiveNumbers { get { int n = 0; foreach (var p in numbers) if (p != null && p.text != null && p.text.gameObject.activeSelf) n++; return n; } }
        public void Initialize(RectTransform floor, RectTransform wall, TMP_FontAsset labelFont)
        {
            // 2026-11-27: damage numbers and boss labels use the game's pixel font (Jersey 15); the passed font is the fallback.
            font = BossPlayerCards.Jersey() ?? labelFont;
            Floor = Make(floor, "VFX == BossFight floor feedback");
            if (wall != null)
            {
                wallOverlay = new GameObject("Canvas == BossFight damage feedback", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                wallOverlay.transform.SetParent(transform, false); wallOverlay.layer = Visual3DFollower.UILayer;
                var canvas = wallOverlay.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.targetDisplay = wall.GetComponentInParent<Canvas>().rootCanvas.targetDisplay; canvas.sortingOrder = 190;
                var scaler = wallOverlay.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = wall.rect.size; scaler.matchWidthOrHeight = .5f;
                Wall = Make((RectTransform)wallOverlay.transform, "VFX == BossFight wall feedback");
            }
        }
        static BossJuiceGraphic Make(RectTransform parent, string name)
        {
            if (parent == null) return null;
            var rect = BossUiRect.Make(parent, name);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.sizeDelta = Vector2.zero;
            var graphic = rect.gameObject.AddComponent<BossJuiceGraphic>(); graphic.raycastTarget = false;
            // No ambient sparks over attack telegraphs; only short event feedback.
            graphic.Ambient = false;
            return graphic;
        }
        public void Burst(bool wall, SpaceJuice kind, Vector2 point, Color color, float radius)
        {
            // 2026-11-27 "effect เส้นที่บังบอส เอาออกให้หมดจากบอสทุกตัว": no ring/ray/diamond bursts on the wall over the
            // boss any more; the floor keeps its feedback and the wall keeps the damage numbers.
            if (wall) return;
            var graphic = Floor; if (graphic == null) return;
            graphic.rectTransform.SetAsLastSibling();
            graphic.Emit(kind, point, color, radius, graphic.rectTransform.rect);
        }
        // size: 1 is the usual damage number; Balance Note v3 uses bigger ones for BREAK!, BOSS ESCAPED and Attack Up.
        public void Label(string text, Vector2 point, Color tint, float size = 1f)
        {
            if (Wall == null) return;
            var slot = numbers[next];
            if (slot == null)
            {
                var rect = BossUiRect.Make(Wall.transform, "Boss damage feedback");
                rect.sizeDelta = new Vector2(1200, 240);
                var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
                if (font != null) label.font = font;
                label.fontSize = LabelSize; label.fontStyle = FontStyles.Normal; label.alignment = TextAlignmentOptions.Center;
                label.raycastTarget = false; label.enableWordWrapping = false;
                numbers[next] = slot = new Number { text = label };
            }
            var area = Wall.rectTransform.rect;
            float x = point.x + (next % 3 - 1) * 170;
            slot.origin = new Vector2(Mathf.Clamp(x, area.xMin + 620, area.xMax - 620),
                Mathf.Clamp(point.y + 160, area.yMin + 150, area.yMax - 360));
            slot.tint = tint; slot.age = 0; slot.text.text = text; slot.text.color = tint; slot.text.fontSize = LabelSize * size;
            slot.text.rectTransform.anchoredPosition = slot.origin; slot.text.transform.localScale = Vector3.one * 1.22f;
            slot.text.gameObject.SetActive(true); Wall.transform.SetAsLastSibling(); next = (next + 1) % numbers.Length;
        }
        void Update() { Advance(Time.deltaTime); }
        public void Advance(float dt)
        {
            if (dt <= 0) return;
            foreach (var slot in numbers)
            {
                if (slot == null || slot.text == null || !slot.text.gameObject.activeSelf) continue;
                slot.age += dt; float t = slot.age / .9f;
                if (t >= 1) { slot.text.gameObject.SetActive(false); continue; }
                slot.text.rectTransform.anchoredPosition = slot.origin + Vector2.up * (180 * (1 - Mathf.Pow(1 - t, 2)));
                slot.text.transform.localScale = Vector3.one * (1 + .22f * Mathf.Exp(-slot.age * 18));
                var c = slot.tint; c.a *= 1 - Mathf.SmoothStep(.3f, 1, t); slot.text.color = c;
            }
        }
        public void Clear()
        {
            if (Floor != null) Floor.Clear(); if (Wall != null) Wall.Clear();
            foreach (var slot in numbers) if (slot != null && slot.text != null) slot.text.gameObject.SetActive(false);
        }
        void OnDisable() { Clear(); }
        void OnDestroy() { if (Floor != null) Destroy(Floor.gameObject); if (wallOverlay != null) Destroy(wallOverlay); }
    }

    public sealed partial class BossSlayerGameManager
    {
        BossFightJuice combatJuice;
        int juicePhase = -1;
        float nextWeakJuice;
        void EnsureCombatJuice()
        {
            if (combatJuice != null) return;
            combatJuice = gameObject.AddComponent<BossFightJuice>();
            combatJuice.Initialize(InputArea, cutsceneArea, bossHpText != null ? bossHpText.font : statusText != null ? statusText.font : null);
            if (bossHpFill != null && bossHpFill.GetComponent<BossHpDamageEcho>() == null) bossHpFill.gameObject.AddComponent<BossHpDamageEcho>();
            foreach (var label in new[] { bossHpText, stunGaugeText, ultimateGaugeText })
                if (label != null && label.GetComponent<BossScorePulse>() == null)
                    label.gameObject.AddComponent<BossScorePulse>().amount = .045f;
        }
        Vector2 JuiceBossPoint()
        {
            var camera = bossDisplayCamera != null ? bossDisplayCamera : wallDisplayCamera;
            if (bossModel == null || camera == null || cutsceneArea == null) return Vector2.zero;
            Vector3 point = camera.WorldToViewportPoint(BossWorldBounds().center);
            var area = cutsceneArea.rect;
            return new Vector2(Mathf.Lerp(area.xMin, area.xMax, point.x), Mathf.Lerp(area.yMin, area.yMax, point.y));
        }
        void JuiceBossDamage(int amount, bool weak, string source)
        {
            if (amount <= 0) return;
            EnsureCombatJuice(); var point = JuiceBossPoint();
            Color tint = weak ? new Color(1, .84f, .25f) : bossModel != null ? BossThemePalette.Accent(bossModel.name) : Color.cyan;
            int chips = Mathf.Clamp(amount / 25, 6, 28);
            if (!BossHitChips(tint, weak ? chips + 10 : chips, source == "Ultimate" ? 1.3f : 1f))
                combatJuice.Burst(true, SpaceJuice.Kill, point, tint, source == "Ultimate" ? 320 : 210);
            combatJuice.Label((weak ? "WEAK!  " : "") + "-" + amount.ToString("N0"), point, DamageNumberColor(source), RushDamageNumberScale(source));
            if (weak && Time.time >= nextWeakJuice)
            {
                nextWeakJuice = Time.time + .35f;
                PunchBossVisual(.065f);
            }
            int phase = BossPhase();
            if (juicePhase >= 0 && phase > juicePhase && bossHp > 0) JuiceMilestone("PHASE " + phase, tint);
            juicePhase = phase;
        }
        // 2026-11-27 "เปลี่ยนสีดาเมจที่บอสโดนไปตามประเภทที่โดน": the number takes the colour of what hit the boss - the gem's
        // own colour (red attack, green heal, yellow stun, blue ultimate) or the Rush perk's, lightened to stay readable.
        Color DamageNumberColor(string source)
        {
            foreach (var def in spotDefs)
                if (def.GemModelName == source) return Color.Lerp(new Color(def.Color.r, def.Color.g, def.Color.b), Color.white, .25f);
            switch (source)
            {
                case "Chain": return new Color(.6f, .85f, 1f);
                case "Doom": return new Color(1f, .45f, .75f);
                case "Dodge": return new Color(.45f, 1f, .95f);
                default: return Color.white;
            }
        }
        void JuiceFloor(SpaceJuice kind, Vector2 position, Color tint, float radius = 125)
        { EnsureCombatJuice(); combatJuice.Burst(false, kind, position, tint, radius); }
        void JuiceMilestone(string title, Color tint, float size = 1f)
        {
            EnsureCombatJuice(); var point = JuiceBossPoint();
            if(title == "STUN!" || title == "BREAK!"){PunchBossVisual(.1f);stunGaugeText?.GetComponent<BossScorePulse>()?.Kick();}
            combatJuice.Label(title, point + Vector2.up * 160, tint, size);
            if (!BossHitChips(tint, Mathf.RoundToInt(40 * size), 1.5f))
                combatJuice.Burst(true, SpaceJuice.Transition, point, tint * new Color(1, 1, 1, .65f), 620 * size);
        }
        // Hits on the boss show as voxel chips off its skin, matching the voxel art; false when the boss has no skin
        // (the caller then keeps the old flat wall burst).
        bool BossHitChips(Color tint, int count, float strength)
        {
            var skin = ActiveBossSkin;
            if (skin == null) return false;
            skin.HitChips(tint, count, strength);
            return true;
        }
        // The Lightning perk's strike: a flash on the boss and a burst of arcs out from it.
        void JuiceBossLightning()
        {
            EnsureCombatJuice(); var point = JuiceBossPoint();
            var tint = new Color(.75f, .9f, 1f);
            PunchBossVisual(.08f);
            if (BossHitChips(tint, 36, 1.6f)) return;
            combatJuice.Burst(true, SpaceJuice.Kill, point, tint, 420);
            SpawnBossExplosionVfx(BossWorldBounds().center, 1.2f);
        }
        void PunchBossVisual(float duration)
        {
            if (bossModel == null) return;
            var pause = bossModel.GetComponent<BossVisualHitPause>() ?? bossModel.gameObject.AddComponent<BossVisualHitPause>();
            pause.Play(duration);
        }
        void ClearCombatJuice() { combatJuice?.Clear(); juicePhase = -1; nextWeakJuice = 0; if(bossModel!=null)bossModel.GetComponent<BossVisualHitPause>()?.Clear(); }
    }
}

namespace BossSlayer
{
    // Freeze only the rendered pose; attack timers, player input and global time keep running.
    [DefaultExecutionOrder(32000)]
    public sealed class BossVisualHitPause : MonoBehaviour
    {
        Transform[] bones; Vector3[] positions, scales; Quaternion[] rotations; float remaining;
        public void Play(float duration)
        {
            if (bones == null) { bones = GetComponentsInChildren<Transform>(true); positions = new Vector3[bones.Length]; scales = new Vector3[bones.Length]; rotations = new Quaternion[bones.Length]; }
            if (remaining > 0) return;
            for(int i=0;i<bones.Length;i++) if(bones[i]!=null){positions[i]=bones[i].localPosition; scales[i]=bones[i].localScale; rotations[i]=bones[i].localRotation;}
            remaining = duration;
        }
        public void Clear() { remaining = 0; }
        void LateUpdate()
        {
            if (remaining <= 0) return;
            remaining -= Time.deltaTime;
            for(int i=0;i<bones.Length;i++) if(bones[i]!=null){bones[i].localPosition=positions[i];bones[i].localRotation=rotations[i];bones[i].localScale=scales[i];}
        }
        void OnDisable() { Clear(); }
    }
}
