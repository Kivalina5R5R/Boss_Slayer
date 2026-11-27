using UnityEngine;

namespace BossSlayer
{
    public sealed partial class BossHydra
    {
        public const float DeathPoseSeconds = 3.4f;
        public float DeathAge => deathTime;
        public bool ManualDeathPlayback { get; set; }
        Mesh bodyMesh;
        Vector3[] bodyRestVertices, bodyLiveVertices;
        float bodyMotionWeight;

        public void BeginDeathPose()
        {
            if (defeated) return;
            CancelSevenHeadStrike();
            defeated = true; deathTime = 0; pending.Clear(); locomotion = 0;
            System.Array.Clear(chargeLeft, 0, MaximumHeads);
            System.Array.Clear(strike, 0, MaximumHeads);
        }

        Quaternion BodySectionRotation(float z)
        {
            float along = Mathf.InverseLerp(-.6f, 2.2f, z);
            float phase = swimPhase - along * 2.8f;
            float strength = 4 + Mathf.Abs(swimSpeed) * 9;
            return Quaternion.Euler(0, Mathf.Sin(phase) * strength * bodyMotionWeight,
                Mathf.Sin(phase - .65f) * strength * .45f * bodyMotionWeight);
        }

        Vector3 BendBodyPoint(Vector3 point)
        {
            float along = Mathf.InverseLerp(-.6f, 2.2f, point.z);
            float phase = swimPhase - along * 2.8f;
            float breath = 1 + Mathf.Sin(elapsed * 1.15f - along * .7f) * .035f * bodyMotionWeight;
            var section = new Vector3(point.x * breath, (point.y + 1.1f) * breath, 0);
            var center = new Vector3(Mathf.Sin(phase) * (.12f + Mathf.Abs(swimSpeed) * .24f) * bodyMotionWeight,
                -1.1f + Mathf.Sin(phase - .8f) * .055f * bodyMotionWeight, point.z);
            return center + BodySectionRotation(point.z) * section;
        }

        void DeformLivingBody(float quiet)
        {
            bodyMotionWeight = quiet;
            if (bodyMesh == null)
            {
                bodyMesh = bodyRig.GetComponent<MeshFilter>().sharedMesh;
                bodyRestVertices = bodyMesh.vertices; bodyLiveVertices = new Vector3[bodyRestVertices.Length];
                bodyMesh.MarkDynamic();
            }
            for (int i = 0; i < bodyLiveVertices.Length; i++) bodyLiveVertices[i] = BendBodyPoint(bodyRestVertices[i]);
            bodyMesh.vertices = bodyLiveVertices; bodyMesh.RecalculateNormals(); bodyMesh.RecalculateBounds();
            if (!defeated) return;
            float collapse = Mathf.SmoothStep(0, 1, Mathf.Clamp01((deathTime - .25f) / 2.35f));
            float settle = Mathf.Sin(Mathf.Max(0, deathTime - 1.5f) * 8) * Mathf.Exp(-Mathf.Max(0, deathTime - 1.5f) * 3) * .035f;
            bodyRig.localPosition += new Vector3(.18f * collapse, -.5f * collapse + settle, .2f * collapse);
            bodyRig.localRotation *= Quaternion.Euler(8 * collapse, -9 * collapse, -16 * collapse);
        }
    }
}
