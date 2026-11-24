using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Deepspace
{
    public sealed class BossFightJuice : MonoBehaviour
    {
        public DeepspaceJuiceGraphic Floor { get; private set; }
        public DeepspaceJuiceGraphic Wall { get; private set; }
        sealed class Number { public TMP_Text text; public Vector2 origin; public float age; public Color tint; }
        readonly Number[] numbers = new Number[12];
        int next;
        TMP_FontAsset font;
        GameObject wallOverlay;
        public int ActiveNumbers { get { int n = 0; foreach (var p in numbers) if (p != null && p.text != null && p.text.gameObject.activeSelf) n++; return n; } }
        public void Initialize(RectTransform floor, RectTransform wall, TMP_FontAsset labelFont)
        {
            font = labelFont;
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
        static DeepspaceJuiceGraphic Make(RectTransform parent, string name)
        {
            if (parent == null) return null;
            var rect = BossUiRect.Make(parent, name);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.sizeDelta = Vector2.zero;
            var graphic = rect.gameObject.AddComponent<DeepspaceJuiceGraphic>(); graphic.raycastTarget = false;
            // No ambient sparks over attack telegraphs; only short event feedback.
            graphic.Ambient = false;
            return graphic;
        }
        public void Burst(bool wall, SpaceJuice kind, Vector2 point, Color color, float radius)
        {
            var graphic = wall ? Wall : Floor; if (graphic == null) return;
            graphic.rectTransform.SetAsLastSibling();
            graphic.Emit(kind, point, color, radius, graphic.rectTransform.rect);
        }
        public void Label(string text, Vector2 point, Color tint)
        {
            if (Wall == null) return;
            var slot = numbers[next];
            if (slot == null)
            {
                var rect = BossUiRect.Make(Wall.transform, "Boss damage feedback");
                rect.sizeDelta = new Vector2(1200, 240);
                var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
                if (font != null) label.font = font;
                label.fontSize = 130; label.fontStyle = FontStyles.Bold; label.alignment = TextAlignmentOptions.Center;
                label.raycastTarget = false; label.enableWordWrapping = false;
                numbers[next] = slot = new Number { text = label };
            }
            var area = Wall.rectTransform.rect;
            float x = point.x + (next % 3 - 1) * 170;
            slot.origin = new Vector2(Mathf.Clamp(x, area.xMin + 620, area.xMax - 620),
                Mathf.Clamp(point.y + 160, area.yMin + 150, area.yMax - 360));
            slot.tint = tint; slot.age = 0; slot.text.text = text; slot.text.color = tint;
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
                if (label != null && label.GetComponent<DeepspaceScorePulse>() == null)
                    label.gameObject.AddComponent<DeepspaceScorePulse>().amount = .045f;
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
            combatJuice.Burst(true, SpaceJuice.Kill, point, tint, source == "Ultimate" ? 320 : 210);
            combatJuice.Label((weak ? "WEAK!  " : "") + "-" + amount.ToString("N0"), point, Color.Lerp(tint, Color.white, .45f));
            if (weak && Time.time >= nextWeakJuice)
            {
                nextWeakJuice = Time.time + .35f;
                PunchBossVisual(.065f);
                if(combatJuice.Wall!=null) for(int i=0;i<8;i++) combatJuice.Wall.Travel(point,point+new Vector2(Mathf.Cos(i*Mathf.PI/4),Mathf.Sin(i*Mathf.PI/4))*360,tint,24,combatJuice.Wall.rectTransform.rect);
            }
            int phase = BossPhase();
            if (juicePhase >= 0 && phase > juicePhase && bossHp > 0) JuiceMilestone("PHASE " + phase, tint);
            juicePhase = phase;
        }
        void JuiceFloor(SpaceJuice kind, Vector2 position, Color tint, float radius = 125)
        { EnsureCombatJuice(); combatJuice.Burst(false, kind, position, tint, radius); }
        void JuiceMilestone(string title, Color tint)
        {
            EnsureCombatJuice(); var point = JuiceBossPoint();
            if(title == "STUN!"){PunchBossVisual(.1f);stunGaugeText?.GetComponent<DeepspaceScorePulse>()?.Kick();}
            combatJuice.Label(title, point + Vector2.up * 160, tint);
            combatJuice.Burst(true, SpaceJuice.Transition, point, tint * new Color(1, 1, 1, .65f), 620);
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

namespace Deepspace
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
