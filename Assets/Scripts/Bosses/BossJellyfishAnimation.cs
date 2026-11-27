using UnityEngine;

namespace BossSlayer
{
    // Imported pivots plus reversible bell and tentacle deformation.
    public sealed partial class BossJellyfishAnimation : BossModelAnimation
    {
        public Transform[] tentacles = new Transform[9];
        public Transform[] visualRoots = new Transform[0];
        public Vector3 bodyCenter;
        public float bodyHeight = 1;
        public Vector3 bodyUp = Vector3.up;
        public Vector3[] tentacleDown = new Vector3[9];
        public Vector3[] tentacleOutward = new Vector3[9];
        Vector3[] rootPositions;
        Quaternion[] rootRotations, tentacleRotations;
        Vector3[] radial, tangent;
        bool initialized;
        static readonly float[] Lengths = { .625f, .875f, .625f, .75f, 1, .75f, .625f, .875f, .625f };

        public override bool Initialize()
        {
            if (initialized) return true;
            if (tentacles == null || tentacles.Length != 9 || visualRoots == null || visualRoots.Length == 0
                || tentacleDown == null || tentacleDown.Length != 9 || tentacleOutward == null || tentacleOutward.Length != 9) return false;
            foreach (var pivot in tentacles) if (pivot == null) return false;
            foreach (var root in visualRoots) if (root == null) return false;
            rootPositions = new Vector3[visualRoots.Length];
            rootRotations = new Quaternion[visualRoots.Length];
            tentacleRotations = new Quaternion[9]; radial = new Vector3[9]; tangent = new Vector3[9];
            for (int i = 0; i < visualRoots.Length; i++)
            {
                rootPositions[i] = visualRoots[i].localPosition;
                rootRotations[i] = visualRoots[i].localRotation;
            }
            for (int i = 0; i < 9; i++)
            {
                tentacleRotations[i] = tentacles[i].localRotation;
                var down = tentacleDown[i].normalized;
                radial[i] = Vector3.ProjectOnPlane(tentacleOutward[i], down).normalized;
                if (radial[i].sqrMagnitude < .01f)
                    radial[i] = Vector3.Cross(down, Mathf.Abs(down.x) < .8f ? Vector3.right : Vector3.forward).normalized;
                tangent[i] = Vector3.Cross(down, radial[i]).normalized;
            }
            initialized = true;
            return true;
        }
        protected override Transform[] GetPoseTransforms()
        {
            var list = new System.Collections.Generic.List<Transform>();
            foreach (var part in visualRoots) if (part != null && !list.Contains(part)) list.Add(part);
            foreach (var part in tentacles) if (part != null && !list.Contains(part)) list.Add(part);
            return list.ToArray();
        }
        public override void ResetMotion()
        {
            ReleaseDeathMeshes();ReleaseSoftBody();
            base.ResetMotion();
            if (!Initialize()) return;
            for (int i = 0; i < visualRoots.Length; i++)
            {
                visualRoots[i].localPosition = rootPositions[i];
                visualRoots[i].localRotation = rootRotations[i];
            }
            for (int i = 0; i < 9; i++) tentacles[i].localRotation = tentacleRotations[i];
        }
        static float Ease(float a, float b, float t) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(a, b, t));
        public override void SamplePose(float time, float charge, float release, float stun)
        {
            if (!Initialize()) return;
            EnsureSoftSurfaces();
            charge = Mathf.Clamp01(charge); release = Mathf.Clamp01(release);
            if (stun > 0) charge = release = 0;
            float motion = motionAmount * Mathf.Lerp(1f,.12f,stun);
            float releaseTime = (1 - release) * 1.05f;
            float snap = Ease(0, .085f, releaseTime);
            float gather = release > 0 ? 1 - snap : Ease(0, .72f, charge);
            float burst = release > 0 ? snap * (1 - Ease(.22f, 1.05f, releaseTime)) : 0;
            float focus = release > 0 ? gather : Ease(.55f, .95f, charge);
            float recoil = release > 0 ? Mathf.Sin(releaseTime * 20) * Mathf.Exp(-releaseTime * 4.5f) * snap : 0;
            float idleWeight = 1 - Mathf.Max(gather, burst) * .9f;
            Vector3 up = bodyUp.normalized;
            Vector3 right = Vector3.ProjectOnPlane(Vector3.right, up).normalized;
            if (right.sqrMagnitude < .01f) right = Vector3.ProjectOnPlane(Vector3.forward, up).normalized;
            Vector4 profile = AttackVariant switch {
                1 => new Vector4(-14,20,.03f,-.01f), 2 => new Vector4(-5,15,.065f,-.06f),
                3 => new Vector4(-10,8,-.015f,.02f), 4 => new Vector4(0,-4,.10f,.025f),
                5 => new Vector4(12,-22,.025f,-.07f), _ => new Vector4(-5,9,.055f,-.04f) };
            Vector3 fan = AttackVariant switch {
                1 => new Vector3(15,58,30), 2 => new Vector3(35,65,12), 3 => new Vector3(45,20,8),
                4 => new Vector3(12,75,45), 5 => new Vector3(18,15,-10), _ => new Vector3(23,48,9) };
            Quaternion bodyRotation = Quaternion.AngleAxis((gather * profile.x + burst * profile.y + recoil * 3) * motion, right)
                * Quaternion.AngleAxis((gather * 8 - burst * 12) * motion, up);
            if (stun > 0) bodyRotation *= Quaternion.AngleAxis(Mathf.Sin(time * 20) * 1.5f * motionAmount, right);
            float swim = Mathf.Abs(Locomotion);
            float bob = Mathf.Sin(time * 2.3271f-.8f) * (.022f + swim * .01f) * idleWeight;
            bodyRotation *= Quaternion.AngleAxis((-Locomotion * 9f + Mathf.Sin(time * .9f) * 3f) * idleWeight * motion, right);
            if (AttackVariant == 1) bodyRotation *= Quaternion.AngleAxis((gather * -14 + burst * 18) * motion, up);
            if (AttackVariant == 3) bob += (gather * .012f - burst * .02f) * motion;
            Vector3 offset = up * ((bob + gather * profile.z + burst * profile.w + recoil * .013f) * bodyHeight * motion);
            if(stun>0)
            {
                var dazed=Quaternion.AngleAxis(42+Mathf.Sin(stunElapsed*4)*10*Mathf.Exp(-stunElapsed*.5f),right)*Quaternion.AngleAxis(Mathf.Sin(stunElapsed*1.8f)*20,up);
                bodyRotation=Quaternion.Slerp(bodyRotation,dazed,stun*motionAmount);
                offset-=up*(bodyHeight*.13f*stun*motionAmount);
            }
            // Pose all imported top-level visual nodes as one rigid body. The
            // manager still owns the boss root, entrance, and horizontal flight.
            for (int i = 0; i < visualRoots.Length; i++)
            {
                visualRoots[i].localPosition = bodyCenter + bodyRotation * (rootPositions[i] - bodyCenter) + offset;
                visualRoots[i].localRotation = bodyRotation * rootRotations[i];
            }
            for (int i = 0; i < 9; i++)
            {
                float phase = i * .9f;
                float idleSway = Mathf.Sin(time * (.85f - .16f * Lengths[i]) + phase) * (3 + Lengths[i] * 4 + swim * 5) * idleWeight;
                float tremble = Mathf.Sin(time * 25 + phase) * focus * .8f;
                // Inner pull while charging, then a radial fan. Delayed secondary
                // ripples make the nine tentacles settle independently.
                float ripple = release > 0 ? Mathf.Sin(releaseTime * 16 - i * .3f) * Ease(0, .09f, releaseTime)
                    * Mathf.Exp(-releaseTime * 4) * 6 : 0;
                float angle = (idleSway - gather * fan.x + burst * (fan.y + Lengths[i] * 9) + tremble + ripple) * motion * Mathf.Deg2Rad;
                Vector3 down = tentacleDown[i].normalized;
                Vector3 direction = down * Mathf.Cos(angle) + radial[i] * Mathf.Sin(angle);
                float swirl = (Mathf.Sin(time * 1.1f + phase) * 3 * idleWeight + gather * fan.z - burst * fan.z * .8f) * motion;
                direction = Quaternion.AngleAxis(swirl, radial[i]) * direction;
                Quaternion swing = Quaternion.FromToRotation(down, direction.normalized);
                Quaternion rest = tentacleRotations[i];
                // A direct child needs the body rotation here as well; nested
                // pivots already inherit it from their imported visual parent.
                if (tentacles[i].parent == transform) rest = bodyRotation * rest;
                if(stun>0) swing=Quaternion.Slerp(swing,Quaternion.AngleAxis(38+Mathf.Sin(stunElapsed*9-i*.7f)*18*Mathf.Exp(-stunElapsed*.4f),radial[i]),stun*motionAmount);
                tentacles[i].localRotation = rest * swing;
            }
            SampleSoftBody(time,charge,release);
        }
    }
}
