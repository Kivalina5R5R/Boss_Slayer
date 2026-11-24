using UnityEngine;
namespace Deepspace
{
    public sealed partial class BossHydra
    {
        public Transform AttackEffectAnchor(int index) => IsHeadReady(index) ? skulls[index] : null;
        readonly int[] headAttackStyles = new int[MaximumHeads];
        public void SetHeadAttackStyle(int index,int style)
        {
            if(index>=0 && index<MaximumHeads) headAttackStyles[index]=Mathf.Clamp(style,0,11);
        }
    }
}
