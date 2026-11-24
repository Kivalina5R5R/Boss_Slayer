using UnityEngine;
using UnityEngine.UI;

namespace Deepspace
{
    // Separate presentation clock; never offsets the tracked play area or hit zones.
    public sealed class BossUltimateImpactFrame : MonoBehaviour
    {
        // Length of one impact frame (was Duration, the space shooter's frame).
        public const float Duration = .62f;

        public DeepspaceImpactGraphic Wall { get; private set; }
        public DeepspaceImpactGraphic Floor { get; private set; }
        public bool Playing { get; private set; }
        public int TriggerCount { get; private set; }
        public float Age { get; private set; }
        public Vector2 ShakeOffset { get; private set; }
        RectTransform background;
        Vector3 restPosition, restScale;
        bool hasRest, beam;
        public bool IsUltimateFrame {get;private set;}
        Vector2 floorFocus;
        public bool CanPlay(bool ultimate)=>!Playing || Age>=.12f || (ultimate&&!IsUltimateFrame);
        public bool ManualPlayback {get;set;}
        public bool ExternalShake {get;private set;}
        public void SetExternalShake(bool value){if(value&&!ExternalShake)RestoreBackground();ExternalShake=value;}
        float amplitude;
        Vector2 focus;

        public void Initialize(RectTransform wall, RectTransform floor, RectTransform floorBackground)
        {
            background = floorBackground;
            Wall = Create(wall, "Boss ultimate wall impact");
            Floor = Create(floor, "Boss ultimate floor shockwave");
            Clear();
        }
        static DeepspaceImpactGraphic Create(RectTransform area, string name)
        {
            if (area == null) return null;
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasRenderer), typeof(DeepspaceImpactGraphic));
            go.layer = area.gameObject.layer;
            go.transform.SetParent(area, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 205; // Keep the ultimate title (210) legible.
            var graphic = go.GetComponent<DeepspaceImpactGraphic>();
            graphic.raycastTarget = false;
            var rect = graphic.rectTransform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return graphic;
        }
        public void Play(Vector2 wallFocus, bool isBeam, float strength, bool ultimate=true, Vector2? floorOrigin=null)
        {
            // Several hazards released in one beat produce one flash and one shake.
            if (!CanPlay(ultimate)) return;
            RestoreBackground();
            if (background != null && !ExternalShake)
            {
                restPosition = background.localPosition;
                restScale = background.localScale;
                hasRest = true;
            }
            focus = wallFocus; beam = isBeam; IsUltimateFrame=ultimate; floorFocus=floorOrigin??new Vector2(.5f,.9f);
            amplitude = Mathf.Clamp(strength, .2f, 1.5f);
            Age = 0; Playing = true; TriggerCount++;
            if (Wall != null) Wall.gameObject.SetActive(true);
            if (Floor != null) Floor.gameObject.SetActive(true);
            Refresh();
        }
        public void Advance(float dt)
        {
            if (!Playing || dt <= 0) return;
            Age += dt;
            if (Age >= Duration/(IsUltimateFrame?1f:2f)) { Clear(); return; }
            Refresh();
        }
        void Refresh()
        {
            float visualAge=Age*(IsUltimateFrame?1f:2f);
            if (Wall != null) { Wall.SetFrame(visualAge, focus, beam, true); Wall.canvasRenderer.SetAlpha(IsUltimateFrame?1f:.42f); }
            // No full-screen flash or dark frame on the floor: warnings stay readable.
            if (Floor != null) { Floor.SetFrame(visualAge, floorFocus, beam, false); Floor.canvasRenderer.SetAlpha(IsUltimateFrame?1f:.5f); }
            float decay = Mathf.Pow(Mathf.Clamp01(1 - Age / (IsUltimateFrame?.55f:.25f)), 2);
            float pixels = background != null ? Mathf.Min(background.rect.width, background.rect.height) * .012f : 0;
            ShakeOffset = new Vector2(Mathf.Sin(Age * 137f + .8f), Mathf.Sin(Age * 181f)) * (pixels * amplitude * decay);
            if (background != null && hasRest && !ExternalShake)
            {
                background.localPosition = restPosition + (Vector3)ShakeOffset;
                background.localScale = Vector3.Scale(restScale, new Vector3(IsUltimateFrame?1.04f:1.012f, IsUltimateFrame?1.04f:1.012f, 1));
            }
        }
        void RestoreBackground()
        {
            if (background != null && hasRest)
            {
                background.localPosition = restPosition;
                background.localScale = restScale;
            }
            hasRest = false; ShakeOffset = Vector2.zero;
        }
        public void Clear()
        {
            Playing = false;
            RestoreBackground();
            if (Wall != null) Wall.gameObject.SetActive(false);
            if (Floor != null) Floor.gameObject.SetActive(false);
        }
        void LateUpdate() { if(!ManualPlayback) Advance(Time.deltaTime); }
        void OnDisable() { Clear(); }
        void OnDestroy()
        {
            Clear();
            if (Wall != null) Destroy(Wall.gameObject);
            if (Floor != null) Destroy(Floor.gameObject);
        }
    }

    public sealed partial class BossSlayerGameManager
    {
        BossUltimateImpactFrame ultimateImpactFrame;
        void PlayBossAttackImpact(AttackState attack)
        {
            if (attack.IsHydraSevenHeadStrike && attack.HydraHeadIndex != 3) return;
            if (!gameStarted || resultShown || bossHp <= 0 || stunTimer > 0 || ultimateActive) return;
            if (ultimateImpactFrame == null)
            {
                ultimateImpactFrame = gameObject.AddComponent<BossUltimateImpactFrame>();
                ultimateImpactFrame.Initialize(cutsceneArea, playArea,
                    bossFloorBackgroundImage != null ? bossFloorBackgroundImage.rectTransform : null);
            }
            if(!ultimateImpactFrame.CanPlay(attack.IsBossUltimate))return;
            var camera = bossDisplayCamera != null ? bossDisplayCamera : wallDisplayCamera;
            Vector2 center = new Vector2(.5f, .55f);
            if (camera != null && bossModel != null)
            {
                var point = camera.WorldToViewportPoint(BossWorldBounds().center);
                center = new Vector2(Mathf.Clamp(point.x, .15f, .85f), Mathf.Clamp(point.y, .15f, .85f));
            }
            Vector2 floorPoint=new Vector2(.5f,.5f);
            if(attack.View!=null && playArea!=null)
            {
                var local=playArea.InverseTransformPoint(attack.View.position);var rect=playArea.rect;
                floorPoint=new Vector2(Mathf.Clamp01((local.x-rect.xMin)/Mathf.Max(1,rect.width)),Mathf.Clamp01((local.y-rect.yMin)/Mathf.Max(1,rect.height)));
            }
            var kind = BossPassiveState.Identify(bossModel != null ? bossModel.name : "");
            if(ultimateImpactFrame.Wall!=null)ultimateImpactFrame.Wall.HydraFangs=attack.IsHydraSevenHeadStrike;
            if(ultimateImpactFrame.Floor!=null)ultimateImpactFrame.Floor.HydraFangs=attack.IsHydraSevenHeadStrike;
            float force = attack.IsHydraSevenHeadStrike ? 1.4f : kind == BossPassiveState.Kind.AbyssalKing ? 1.5f : kind == BossPassiveState.Kind.Golem || kind == BossPassiveState.Kind.Dragon ? 1.35f : 1f;
            if(ultimateImpactFrame.Wall!=null)ultimateImpactFrame.Wall.SwordCross=kind==BossPassiveState.Kind.Demon;
            if(ultimateImpactFrame.Wall!=null)ultimateImpactFrame.Wall.ElectricBurst=kind==BossPassiveState.Kind.Jellyfish;
            if(ultimateImpactFrame.Floor!=null)ultimateImpactFrame.Floor.ElectricBurst=kind==BossPassiveState.Kind.Jellyfish;
            if(ultimateImpactFrame.Wall!=null)ultimateImpactFrame.Wall.VoidCollapse=kind==BossPassiveState.Kind.Wraith || kind==BossPassiveState.Kind.AbyssalKing;
            if(ultimateImpactFrame.Floor!=null)ultimateImpactFrame.Floor.VoidCollapse=kind==BossPassiveState.Kind.Wraith || kind==BossPassiveState.Kind.AbyssalKing;
            if(ultimateImpactFrame.Wall!=null)ultimateImpactFrame.Wall.GoldenCore=kind==BossPassiveState.Kind.Sentinel;
            if(ultimateImpactFrame.Floor!=null)ultimateImpactFrame.Floor.GoldenCore=kind==BossPassiveState.Kind.Sentinel;
            if(ultimateImpactFrame.Wall!=null)ultimateImpactFrame.Wall.OceanSurge=kind==BossPassiveState.Kind.Serpent || kind==BossPassiveState.Kind.Fish || kind==BossPassiveState.Kind.Hydra;
            if(ultimateImpactFrame.Floor!=null)ultimateImpactFrame.Floor.OceanSurge=kind==BossPassiveState.Kind.Serpent || kind==BossPassiveState.Kind.Fish || kind==BossPassiveState.Kind.Hydra;
            ultimateImpactFrame.Play(center, attack.Shape == Shape.Line || attack.Shape==Shape.Beam || kind==BossPassiveState.Kind.Demon, attack.IsBossUltimate?(kind==BossPassiveState.Kind.Demon?1.3f:force):.35f, attack.IsBossUltimate, floorPoint);
        }
    }
}
