using UnityEngine;

namespace Deepspace
{
    // Articulated, rigid meshes: all performance comes from joint transforms.
    [DisallowMultipleComponent]
    public partial class BossModelAnimation : MonoBehaviour
    {
        public enum Kind { Dragon, Demon, Fish, Golem, Wraith, Sentinel, Serpent, Jellyfish }
        public Kind character;
        public Vector3 sculptCenter;
        public float sculptHeight = 36f;
        [Range(0f, 2f)] public float motionAmount = 1f;
        public Transform rigRoot;
        public Transform[] parts = new Transform[6];
        protected virtual float RecoverySeconds => 1.05f;
        [Min(0f)] public float poseLerpSpeed = 18f;
        Transform[] blendTransforms;
        Vector3[] blendPositions;
        Vector3[] blendScales;
        Quaternion[] blendRotations;
        float elapsed, castElapsed, castDuration, recovery, stunned;
        int castId;
        bool casting, defeated, powerPoseVisible;
        Transform cachedRig;
        Vector3 restPosition;
        float height;
        readonly Vector3[] idle = new Vector3[6];
        readonly Vector3[] gather = new Vector3[6];
        readonly Vector3[] fire = new Vector3[6];
        static readonly Quaternion ToSculpt = Quaternion.Euler(-90, 0, 0);
        static readonly Quaternion ToMesh = Quaternion.Euler(90, 0, 0);
        public bool IsCasting => casting;
        public bool IsStunned => stunned > 0;
        public bool IsDefeated => defeated;
        public bool IsAttackMotionActive => casting || recovery > 0 || powerPoseVisible;
        public Mesh AnimatedMesh => GetComponent<MeshFilter>()?.sharedMesh;

        protected void OnEnable() { ResetMotion(); }
        protected void OnDisable() { ResetMotion(); }
        public virtual bool Initialize()
        {
            if (rigRoot == null || parts == null || parts.Length != 6) return false;
            if (cachedRig == rigRoot) return true;
            cachedRig = rigRoot;
            restPosition = rigRoot.localPosition;
            var filter = GetComponent<MeshFilter>();
            height = filter != null && filter.sharedMesh != null ? filter.sharedMesh.bounds.size.z : 1f;
            return true;
        }
        public virtual void ResetMotion()
        {
            blendTransforms = null;
            elapsed = castElapsed = recovery = stunned = stunDuration = stunElapsed = 0; locomotion = 0; attackVariant = 0;
            casting = defeated = powerPoseVisible = false;
            castId++;
            if (Initialize() && rigRoot != null)
            {
                rigRoot.localPosition = restPosition;
                rigRoot.localRotation = Quaternion.identity;
            }
            if (parts != null)
                foreach (var part in parts)
                    if (part != null) part.localRotation = Quaternion.identity;
        }
        public int BeginCast(float duration)
        {
            if (defeated || stunned > 0) return -1;
            castId++; casting = true; castElapsed = 0;
            castDuration = Mathf.Max(.01f, duration); recovery = 0;
            return castId;
        }
        public void ReleaseAttack(int id)
        {
            if (!casting || id != castId || defeated || stunned > 0) return;
            casting = false; recovery = RecoverySeconds;
        }
        public void Stun(float duration)
        {
            stunDuration = stunned = Mathf.Max(0, duration); stunElapsed = 0; casting = false; recovery = 0; castId++;
        }
        public void Defeat() { defeated = true; casting = false; recovery = 0; }
        protected void LateUpdate() { AdvancePose(Time.deltaTime); }
        public static float PoseBlendFactor(float speed, float dt) => speed <= 0 ? 1 : 1 - Mathf.Exp(-speed * Mathf.Max(0,dt));
        protected virtual Transform[] GetPoseTransforms()
        {
            var list = new System.Collections.Generic.List<Transform>();
            if (rigRoot != null) list.Add(rigRoot);
            if (parts != null) foreach (var part in parts) if (part != null && !list.Contains(part)) list.Add(part);
            return list.ToArray();
        }
        public void AdvancePose(float dt)
        {
            if (defeated || !Initialize() || dt <= 0) return;
            if (blendTransforms == null)
            {
                blendTransforms = GetPoseTransforms();
                blendPositions = new Vector3[blendTransforms.Length];
                blendScales = new Vector3[blendTransforms.Length];
                blendRotations = new Quaternion[blendTransforms.Length];
            }
            for (int i=0;i<blendTransforms.Length;i++) if (blendTransforms[i]!=null)
            { blendScales[i]=blendTransforms[i].localScale; blendPositions[i]=blendTransforms[i].localPosition; blendRotations[i]=blendTransforms[i].localRotation; }
            elapsed += dt;
            if (casting) castElapsed += dt;
            recovery = Mathf.Max(0, recovery - dt);
            if(stunned>0)stunElapsed+=dt;
            stunned = Mathf.Max(0, stunned - dt);
            SampleChoreographedPose(elapsed, casting ? Mathf.Clamp01(castElapsed / castDuration) : 0,
                recovery / RecoverySeconds, StunWeight(stunned,stunDuration));
            if (character != Kind.Jellyfish) PoseStunned(elapsed, StunWeight(stunned,stunDuration));
            float blend = PoseBlendFactor(recovery > 0 ? poseLerpSpeed * 2 : poseLerpSpeed, dt);
            for (int i=0;i<blendTransforms.Length;i++)
            {
                var part=blendTransforms[i]; if(part==null)continue;
                part.localPosition=Vector3.Lerp(blendPositions[i],part.localPosition,blend);
                part.localScale=Vector3.Lerp(blendScales[i],part.localScale,blend);
                part.localRotation=Quaternion.Slerp(blendRotations[i],part.localRotation,blend);
            }
        }
        static float Ease(float start, float end, float value)
        {
            return Mathf.SmoothStep(0, 1, Mathf.InverseLerp(start, end, value));
        }

        // charge: progress toward the gameplay telegraph's release event.
        // release: remaining recovery, from 1 at the event to 0 at rest.
        public virtual void SamplePose(float time, float charge, float release, float stun)
        {
            if (!Initialize()) return;
            float motion = motionAmount * Mathf.Lerp(1f,.12f,stun);
            charge = Mathf.Clamp01(charge);
            release = Mathf.Clamp01(release);
            if (stun > 0) charge = release = 0;
            powerPoseVisible = charge > 0 || release > 0;
            float releaseTime = (1 - release) * RecoverySeconds;
            float launch = Ease(0, .075f, releaseTime);
            float settle = 1 - Ease(.24f, RecoverySeconds, releaseTime);
            float wind = release > 0 ? 1 - launch : Ease(0, .72f, charge);
            float blast = release > 0 ? launch * settle : 0;
            float focus = release > 0 ? wind : Ease(.55f, .95f, charge);
            float tension = Mathf.Sin(time * 29f) * .65f * focus;
            float aftershock = release > 0 ? Mathf.Sin(releaseTime * 24f) * Mathf.Exp(-releaseTime * 5f) * launch : 0;
            float quiet = 1 - Mathf.Max(wind, blast) * .92f;
            float wave = Mathf.Sin(time * 2.6f);
            for (int i = 0; i < 6; i++) idle[i] = gather[i] = fire[i] = Vector3.zero;
            Vector3 windBody = Vector3.zero, fireBody = Vector3.zero;
            Vector3 windTravel = Vector3.zero, fireTravel = Vector3.zero;

            for (int side = -1; side <= 1; side += 2)
            {
                int j = side < 0 ? 1 : 2;
                switch (character)
                {
                    case Kind.Dragon:
                        idle[j] = new Vector3(0, 0, side * wave * 23);
                        gather[j] = new Vector3(-8, side * 8, side * 52);
                        fire[j] = new Vector3(12, -side * 10, -side * 40);
                        break;
                    case Kind.Demon:
                        idle[j] = new Vector3(Mathf.Sin(time * 2 + side * .7f) * 5, 0, side * 3);
                        gather[j] = new Vector3(60, -side * 12, -side * 18);
                        fire[j] = new Vector3(102, side * 8, side * 20);
                        break;
                    case Kind.Fish:
                        idle[j] = new Vector3(0, 0, side * wave * 13);
                        gather[j] = new Vector3(-8, side * 10, side * 38);
                        fire[j] = new Vector3(10, -side * 12, -side * 32);
                        break;
                    case Kind.Golem:
                        idle[j] = new Vector3(Mathf.Sin(time * 1.4f + side * .7f) * 3, 0, 0);
                        gather[j] = new Vector3(132, 0, side * 12);
                        fire[j] = new Vector3(36, -side * 8, -side * 8);
                        break;
                    case Kind.Wraith:
                        idle[j] = new Vector3(Mathf.Sin(time * 2 + side * .7f) * 7, 0, side * 8);
                        gather[j] = new Vector3(52, side * 12, side * 48);
                        fire[j] = new Vector3(96, -side * 10, -side * 14);
                        break;
                    case Kind.Sentinel:
                        idle[j] = new Vector3(Mathf.Sin(time * 2 + side * .7f) * 2, 0, 0);
                        gather[j] = new Vector3(78, -side * 6, -side * 8);
                        fire[j] = new Vector3(94, 0, side * 6);
                        break;
                    case Kind.Serpent:
                        idle[j] = new Vector3(0, 0, side * wave * 12);
                        gather[j] = new Vector3(-10, side * 8, side * 36);
                        fire[j] = new Vector3(15, -side * 12, -side * 26);
                        break;
                }
            }
            switch (character)
            {
                case Kind.Dragon:
                    idle[3].x = Mathf.Sin(time * 1.6f) * 3;
                    idle[5].y = Mathf.Sin(time * 2) * 20;
                    gather[3].x = 22; gather[4].x = -30; gather[5].y = 12;
                    fire[3].x = -22; fire[4].x = -42; fire[5].y = -16;
                    windBody = new Vector3(6, -4, 0); fireBody = new Vector3(-8, 3, 0);
                    windTravel = new Vector3(0, .025f, .035f); fireTravel = new Vector3(0, -.018f, -.065f);
                    break;
                case Kind.Demon:
                    idle[3].x = Mathf.Sin(time * 1.3f) * 2; idle[5].y = Mathf.Sin(time * 2.6f) * 22;
                    gather[3] = new Vector3(-12, -8, 0); fire[3].x = 12;
                    gather[5].y = 26; fire[5].y = -24;
                    windBody = new Vector3(7, -10, -3); fireBody = new Vector3(-10, 7, 2);
                    windTravel = new Vector3(0, -.02f, .03f); fireTravel = new Vector3(0, .015f, -.045f);
                    break;
                case Kind.Fish:
                    idle[5].y = Mathf.Sin(time * 3) * 23;
                    gather[4].x = -30; fire[4].x = -46;
                    gather[5].y = 24; fire[5].y = -30;
                    windBody = new Vector3(10, -6, 0); fireBody = new Vector3(-13, 4, 0);
                    windTravel = new Vector3(0, .015f, .04f); fireTravel = new Vector3(0, -.02f, -.075f);
                    break;
                case Kind.Golem:
                    idle[3].x = Mathf.Sin(time * 1.3f);
                    gather[3].x = 12; fire[3].x = -14;
                    windBody = new Vector3(9, 0, 0); fireBody = new Vector3(-14, 0, 0);
                    windTravel = new Vector3(0, .035f, .02f); fireTravel = new Vector3(0, -.06f, -.02f);
                    break;
                case Kind.Wraith:
                    idle[3].x = Mathf.Sin(time * 1.3f) * 2; idle[5].z = Mathf.Sin(time * 2) * 8;
                    gather[3].x = 18; fire[3].x = -16;
                    gather[5] = new Vector3(-8, 0, 8); fire[5] = new Vector3(15, 0, -10);
                    windBody = new Vector3(6, -8, -4); fireBody = new Vector3(-8, 6, 3);
                    windTravel = new Vector3(0, .065f, .015f); fireTravel = new Vector3(0, -.025f, -.04f);
                    break;
                case Kind.Sentinel:
                    idle[3].y = Mathf.Sin(time * 1.3f) * 2;
                    gather[3].x = -5; fire[3].x = 7;
                    windBody = new Vector3(3, 0, 0); fireBody = new Vector3(7, 0, 0);
                    windTravel = new Vector3(0, -.012f, .015f); fireTravel = new Vector3(0, 0, .045f);
                    break;
                case Kind.Serpent:
                    idle[5].y = Mathf.Sin(time * 2.5f) * 18;
                    gather[3] = new Vector3(26, -12, 0); fire[3] = new Vector3(-30, 8, 0);
                    gather[5].y = 26; fire[5].y = -24;
                    windBody = new Vector3(8, -6, 0); fireBody = new Vector3(-11, 4, 0);
                    windTravel = new Vector3(0, .04f, .03f); fireTravel = new Vector3(0, -.025f, -.065f);
                    break;
            }

            LivingPose(time, out var idleBody, out var idleTravel);
            StyleAttackPose(ref windBody, ref fireBody, ref windTravel, ref fireTravel);
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == null) continue;
                Vector3 angles = (idle[i] * quiet + gather[i] * wind + fire[i] * blast) * motion;
                if (i == 1 || i == 2) angles.x += (tension + aftershock * 4) * motion;
                if (i == 3) angles.x += aftershock * 2 * motion;
                parts[i].localRotation = ToMesh * Quaternion.Euler(angles) * ToSculpt;
            }
            Vector3 body = (idleBody * quiet + windBody * wind + fireBody * blast) * motion;
            body.x += aftershock * 3 * motion;
            body.z += tension * .3f * motion;
            if (stun > 0) body.z += Mathf.Sin(time * 20) * 1.5f * motionAmount;
            rigRoot.localRotation = ToMesh * Quaternion.Euler(body) * ToSculpt;
            Vector3 travel = (idleTravel * quiet + windTravel * wind + fireTravel * blast) * motion;
            travel.z += aftershock * .012f * motion;
            rigRoot.localPosition = restPosition + ToMesh * (travel * height);
            PoseStunned(time,stun);
        }
    }
}
