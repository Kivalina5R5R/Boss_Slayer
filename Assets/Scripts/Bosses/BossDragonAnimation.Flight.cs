using UnityEngine;
namespace BossSlayer
{
    public sealed partial class BossDragonAnimation
    {
        [Header("Suspended Flight")]
        [Range(0f,1f)] public float flightPoseWeight=1f;
        [Range(.8f,3f)] public float hoverWingSpeed=1.8f;
        Transform flightRig;
        readonly Vector3[] hangingAxes=new Vector3[7];
        void CacheFlightAxes()
        {
            if(flightRig==rigRoot)return;
            flightRig=rigRoot;
            hangingAxes[0]=secondaryParts[2].localPosition.normalized;
            hangingAxes[1]=secondaryParts[3].localPosition.normalized;
            hangingAxes[2]=(ToDragonMesh*new Vector3(.55f,-.35f,1.85f)).normalized;
            for(int i=0;i<4;i++)
            {
                var filter=secondaryParts[i+4].GetComponent<MeshFilter>();
                hangingAxes[i+3]=filter!=null && filter.sharedMesh!=null
                    ?filter.sharedMesh.bounds.center.normalized:ToDragonMesh*Vector3.down;
            }
        }
        void SampleSuspendedFlight(float time,float charge,float release,float stun)
        {
            if(rigRoot==null || secondaryParts==null || secondaryParts.Length!=8)return;
            CacheFlightAxes();
            float weight=flightPoseWeight*motionAmount;
            float beat=time*hoverWingSpeed;
            float action=Mathf.Max(charge,release);
            // Gravity determines the hanging direction, even while the presentation root banks.
            var sculptUp=rigRoot.TransformDirection(ToDragonMesh*Vector3.up);
            var correction=Quaternion.FromToRotation(sculptUp,Vector3.up);
            rigRoot.rotation=Quaternion.Slerp(rigRoot.rotation,correction*rigRoot.rotation,Mathf.Clamp01(weight*.82f));
            float height=AnimatedMesh!=null?AnimatedMesh.bounds.size.z:1;
            float lift=(-.024f+Mathf.Sin(beat-.7f)*.015f)*height*weight;
            rigRoot.localPosition+=transform.InverseTransformVector(Vector3.up*lift*transform.lossyScale.magnitude/Mathf.Sqrt(3));
            float flap=Mathf.Sin(beat)*19*weight*(1-action*.55f)*(1-stun*.8f);
            for(int side=-1;side<=1;side+=2)
            {
                var wing=parts[side<0?1:2];
                if(wing!=null)wing.localRotation=ToDragonMesh*Quaternion.Euler(0,side*Mathf.Sin(beat-.35f)*3,side*flap)*FromDragonMesh*wing.localRotation;
            }
            Vector3 drift=Vector3.right*(Mathf.Sin(beat-.9f)*.09f-Locomotion*.07f);
            // Three tail sections settle progressively; the tip trails the body's lift.
            Hang(parts[5],hangingAxes[0],Vector3.down+drift+Vector3.forward*.15f,weight*.92f);
            Hang(secondaryParts[2],hangingAxes[1],Vector3.down+Vector3.right*Mathf.Sin(beat-1.45f)*.13f,weight);
            Hang(secondaryParts[3],hangingAxes[2],Vector3.down+Vector3.right*Mathf.Sin(beat-2f)*.18f-Vector3.forward*.14f,weight);
            for(int i=0;i<4;i++)
            {
                float side=i%2==0?-1:1;
                Vector3 sway=Vector3.right*(Mathf.Sin(beat-.65f-i*.15f)*.055f+side*.07f)
                    +Vector3.forward*(.06f+Mathf.Sin(beat-1.1f)*.05f);
                Hang(secondaryParts[i+4],hangingAxes[i+3],Vector3.down+sway,weight*.96f);
            }
        }
        static void Hang(Transform part,Vector3 restAxis,Vector3 worldDirection,float weight)
        {
            if(part==null || part.parent==null || restAxis.sqrMagnitude<.01f)return;
            var target=Quaternion.FromToRotation(restAxis,part.parent.InverseTransformDirection(worldDirection.normalized));
            part.localRotation=Quaternion.Slerp(part.localRotation,target,Mathf.Clamp01(weight));
        }
    }
}
