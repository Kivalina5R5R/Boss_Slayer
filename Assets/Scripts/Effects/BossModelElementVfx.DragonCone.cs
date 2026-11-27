using UnityEngine;

namespace BossSlayer
{
    public sealed partial class BossModelElementVfx
    {
        static void DragonFireCone(int index,float clock,float charge,float release,
            out Vector3 offset,out Vector3 stretch,out float block,out Color tint)
        {
            float seed=Mathf.Repeat(index*.618034f,1);
            float radialSeed=Mathf.Repeat(index*.754878f+.13f,1);
            float flow=Mathf.Repeat(seed+clock*1.6f,1);
            bool firing=release>=0;
            float length=firing?Mathf.Lerp(13,22,Mathf.SmoothStep(0,1,release)):1.8f+charge*2.4f;
            float along=firing?flow:1-flow;
            float z=.12f+along*length;
            float coneRadius=.12f+z*(firing?.43f:.17f);
            float angle=index*2.399963f+clock*(index%2==0?2.4f:-1.8f);
            int layer=index%6;
            float radius;
            if(layer<2)
            {
                // Dense yellow-white core fills the cone instead of leaving a hollow tube.
                radius=coneRadius*Mathf.Sqrt(radialSeed)*.38f;
                tint=Color.Lerp(new Color(1,.95f,.55f),new Color(1,.5f,.025f),along);
                block=(.38f+along*.7f)*(firing?1:.65f);
            }
            else if(layer<5)
            {
                radius=coneRadius*(.3f+.7f*Mathf.Sqrt(radialSeed));
                tint=Color.Lerp(new Color(1,.48f,.015f),new Color(.85f,.055f,.004f),along*.7f+radialSeed*.3f);
                block=(.35f+along*.85f)*(firing?1:.6f);
            }
            else
            {
                // Smaller hot fragments peel off the widening edge.
                radius=coneRadius*(1+radialSeed*.28f);
                tint=Color.Lerp(new Color(1,.85f,.18f),new Color(1,.16f,.005f),radialSeed);
                block=.15f+radialSeed*.22f;
            }
            float ripple=1+Mathf.Sin(z*2.7f-clock*16+angle)*.07f;
            offset=new Vector3(Mathf.Cos(angle)*radius*ripple,Mathf.Sin(angle)*radius*ripple,z);
            stretch=new Vector3(1,1,layer==5?2.8f:1.6f);
        }
    }
}
