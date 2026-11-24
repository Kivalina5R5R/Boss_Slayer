using System.Collections.Generic;
using UnityEngine;

namespace Deepspace
{
    public sealed partial class BossHydra
    {
        const int TailRings = 48, TailSides = 14;
        Transform bodyRig;
        Mesh tailMesh;
        Vector3[] tailCoordinates, tailVertices;
        readonly Vector3[] tailCenters = new Vector3[TailRings + 1];
        readonly Vector3[] tailRights = new Vector3[TailRings + 1];
        readonly Vector3[] tailUps = new Vector3[TailRings + 1];
        float swimPhase, swimSpeed;
        static Vector3 FrontNeckSocket(int i) => new Vector3((i-3)*.25f,-1.12f,-.42f+Mathf.Abs(i-3)*.035f);

        void BuildSerpentineTail(Color skin, Color belly, Color fin)
        {
            var coordinates = new List<Vector3>(); var colors = new List<Color>(); var triangles = new List<int>();
            // บาส 2026-10-02: voxel blocks like the neck (was a smooth 14-sided tube + flat crest).
            for (int ring = 0; ring < TailRings; ring++)
            {
                float a = ring / (float)TailRings, b = (ring + 1f) / TailRings;
                float s = ring % 2 == 0 ? .84f : .74f, shade = ring % 2 == 0 ? 1f : .84f;
                RingBlock(coordinates, colors, triangles, a, b, -s, s, -s, s, skin * shade, skin * shade * 1.06f, belly * shade);
                if (ring % 3 == 0) RingBlock(coordinates, colors, triangles, a, b, -.24f, .24f, s - .05f, ring % 6 == 0 ? 1.6f : 1.3f, fin, fin * 1.15f, fin);
            }
            tailMesh = new Mesh { name = "Hydra continuous serpentine tail" }; tailMesh.MarkDynamic();
            tailCoordinates = coordinates.ToArray(); tailVertices = new Vector3[coordinates.Count];
            tailMesh.vertices = tailVertices; tailMesh.SetColors(colors); tailMesh.SetTriangles(triangles, 0); meshes.Add(tailMesh);
            var tail = new GameObject("Serpentine tail", typeof(MeshFilter), typeof(MeshRenderer)); tail.transform.SetParent(bodyRig, false);
            tail.GetComponent<MeshFilter>().sharedMesh = tailMesh; tail.GetComponent<MeshRenderer>().sharedMaterial = voxelMaterial;
        }

        Vector3 TailPoint(float t, float quiet)
        {
            float motion = Mathf.Abs(swimSpeed);
            // A trailing S curve exposes the long silhouette; the wave travels out from the hips.
            float envelope = t * Mathf.SmoothStep(0, 1, t * 2);
            float wave = Mathf.Sin(swimPhase - t * 6.4f) * (.65f + motion * 1.15f)
                + Mathf.Sin(swimPhase * .53f - t * 9.2f) * .25f;
            var point = new Vector3(t * 2.2f + Mathf.Sin(t * 4.2f) * 2.2f + envelope * (wave - swimSpeed * .85f) * quiet,
                -1.08f - Mathf.Sin(t * Mathf.PI) * .26f + envelope * Mathf.Sin(swimPhase * .72f - t * 4.8f) * .18f * quiet,
                1.1f + t * 5.8f);
            if(!defeated && stunMotion>0){float age=stunMotionDuration-stunMotion;float w=BossModelAnimation.StunWeight(stunMotion,stunMotionDuration);
                point.x+=Mathf.Sin(age*10-t*11)*Mathf.Exp(-age*1.2f)*.38f*t*w;
                point.y-=Mathf.Sin(t*Mathf.PI)*.14f*w;}
            var hip=new Vector3(0,-1.08f,1.1f);
            point=BendBodyPoint(hip)+BodySectionRotation(hip.z)*(point-hip);
            if(defeated){float release=Mathf.SmoothStep(0,1,Mathf.Clamp01(deathTime/2.6f));point.y-=Mathf.Sin(t*Mathf.PI)*.25f*release;point.x+=Mathf.Sin(deathTime*6-t*5)*Mathf.Sin(Mathf.PI*Mathf.Clamp01(deathTime/1.3f))*.32f*t;}
            return point;
        }

        void PoseSerpentineBody(float dt)
        {
            if (bodyRig == null || tailMesh == null) return;
            float quiet = defeated ? 1 - Mathf.SmoothStep(0, 1, deathTime / 2.6f)
                : Mathf.Lerp(1, .08f, BossModelAnimation.StunWeight(stunMotion, stunMotionDuration));
            swimSpeed = dt == 0 ? 0 : Mathf.Lerp(swimSpeed, locomotion, 1 - Mathf.Exp(-2.4f * dt));
            swimPhase += dt * (.85f + Mathf.Abs(swimSpeed) * 1.45f) * quiet;
            float amplitude = .07f + Mathf.Abs(swimSpeed) * .14f;
            bodyRig.localPosition = new Vector3(Mathf.Sin(swimPhase) * amplitude, Mathf.Sin(elapsed * 1.15f) * .035f, Mathf.Cos(swimPhase * .7f) * .035f) * quiet;
            bodyRig.localRotation = Quaternion.Euler(Mathf.Sin(swimPhase * .65f) * 1.2f * quiet,
                (Mathf.Sin(swimPhase) * (1.5f + Mathf.Abs(swimSpeed) * 3.5f) + swimSpeed * 5) * quiet,
                -Mathf.Sin(swimPhase - .45f) * (1 + Mathf.Abs(swimSpeed) * 1.8f) * quiet);
            if(!defeated && stunMotion>0)
            {
                float age=stunMotionDuration-stunMotion, w=BossModelAnimation.StunWeight(stunMotion,stunMotionDuration);
                bodyRig.localPosition+=new Vector3(Mathf.Sin(age*13)*Mathf.Exp(-age*2)*.08f,-.16f,0)*w;
                bodyRig.localRotation*=Quaternion.Euler((5+Mathf.Sin(age*2)*1.5f)*w,0,Mathf.Sin(age*5)*2*w);
            }
            float breath = Mathf.Sin(elapsed * 1.15f) * .012f * quiet;
            bodyRig.localScale = new Vector3(1 + breath, 1 + breath * .65f, 1);
            ApplyExtendedBodyMotion(quiet);
            DeformLivingBody(quiet);
            for (int i = 0; i < MaximumHeads; i++)
            {
                if (necks[i] == null) continue;
                var socket = BendBodyPoint(FrontNeckSocket(i));
                necks[i].localPosition = bodyRig.localPosition + bodyRig.localRotation * Vector3.Scale(socket, bodyRig.localScale);
                necks[i].localRotation = bodyRig.localRotation * BodySectionRotation(FrontNeckSocket(i).z);
            }
            for (int ring = 0; ring <= TailRings; ring++)
            {
                float t = ring / (float)TailRings; tailCenters[ring] = TailPoint(t, quiet);
                var tangent = (TailPoint(Mathf.Min(1, t + .004f), quiet) - TailPoint(Mathf.Max(0, t - .004f), quiet)).normalized;
                tailRights[ring] = Vector3.Cross(Vector3.up, tangent).normalized;
                tailUps[ring] = Vector3.Cross(tangent, tailRights[ring]).normalized;
            }
            for (int v = 0; v < tailVertices.Length; v++)
            {
                var q = tailCoordinates[v]; int ring = Mathf.RoundToInt(q.x * TailRings);
                float radius = .68f * Mathf.Pow(1 - q.x, 1.15f) + .018f;
                tailVertices[v] = tailCenters[ring] + (tailRights[ring] * q.y + tailUps[ring] * q.z * .78f) * radius;
            }
            tailMesh.vertices = tailVertices; tailMesh.RecalculateNormals(); tailMesh.RecalculateBounds();
        }
    }
}
