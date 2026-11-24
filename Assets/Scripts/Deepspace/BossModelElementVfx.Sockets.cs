using UnityEngine;

namespace Deepspace
{
    public sealed partial class BossModelElementVfx
    {
        readonly System.Collections.Generic.Dictionary<Transform, Vector3> socketDirections = new System.Collections.Generic.Dictionary<Transform, Vector3>();
        readonly System.Collections.Generic.Dictionary<Transform, float> socketWidths = new System.Collections.Generic.Dictionary<Transform, float>();

        bool IsMouth(Transform anchor) => hydra != null || ((Theme == 1 || Theme == 3 || Theme == 5) && (anchor == Part(4) || anchor == Part(3)));

        void BindSocket(Transform anchor)
        {
            var skin = BossVoxelSkin.ActiveOn(boss);
            if (skin != null && skin.OwnsBone(anchor))
            {
                // New model: the socket is the part's farthest point toward the camera (its mouth, hand or head front).
                var toward = skin.transform.forward;
                sockets[anchor] = skin.LocalTip(anchor, toward, out float partWidth);
                socketDirections[anchor] = anchor.InverseTransformDirection(toward).normalized;
                socketWidths[anchor] = partWidth;
                return;
            }
            // Articulated sculpt meshes use +Y as forward and +Z as up.
            Vector3 axis = Vector3.up;
            if (hydra != null) axis = Vector3.back;
            else if (jelly != null)
            {
                int index = System.Array.IndexOf(jelly.tentacles, anchor);
                if (index >= 0) axis = Quaternion.Inverse(anchor.localRotation) * jelly.tentacleDown[index].normalized;
            }
            else if (!IsMouth(anchor) && Theme != 6)
            {
                axis = anchor == Part(1) ? Vector3.left : anchor == Part(2) ? Vector3.right : Vector3.back;
                if (Theme == 0 || Theme == 4 || Theme == 7) axis = Vector3.back;
            }
            Bounds bounds = default; bool found = false;
            foreach (var filter in anchor.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null) continue;
                var b = filter.sharedMesh.bounds;
                for (int i=0;i<8;i++)
                {
                    var p=b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                    p=anchor.InverseTransformPoint(filter.transform.TransformPoint(p));
                    if(!found){bounds=new Bounds(p,Vector3.zero);found=true;}else bounds.Encapsulate(p);
                }
            }
            float extent=Mathf.Abs(axis.x)*bounds.extents.x+Mathf.Abs(axis.y)*bounds.extents.y+Mathf.Abs(axis.z)*bounds.extents.z;
            Vector3 point=bounds.center+axis*extent;
            if (IsMouth(anchor) && hydra == null && anchor == Part(4))
                point.z=bounds.max.z; // upper lip of the lower jaw, not the skull or forehead
            if (hydra != null) point=new Vector3(0,-.32f,-.80f); // generated open-mouth geometry
            sockets[anchor]=point;
            socketDirections[anchor]=axis.normalized;
            socketWidths[anchor]=Mathf.Max(.001f,bounds.size.x);
        }

        public void EmissionFrame(Transform anchor, out Vector3 origin, out Quaternion rotation)
        {
            if(!sockets.ContainsKey(anchor))BindSocket(anchor);
            origin=anchor.TransformPoint(sockets[anchor]);
            var direction=anchor.TransformDirection(socketDirections[anchor]).normalized;
            var up=anchor.TransformDirection(hydra!=null?Vector3.up:Vector3.forward);
            if(Mathf.Abs(Vector3.Dot(direction,up))>.95f)up=anchor.TransformDirection(Vector3.right);
            rotation=Quaternion.LookRotation(direction,up);
        }
    }
}
