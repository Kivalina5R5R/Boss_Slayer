using UnityEngine;
namespace Deepspace
{
    public sealed class BossFightReadyGate
    {
        float elapsed;
        public int Countdown => Mathf.Clamp(Mathf.CeilToInt(3f-elapsed),1,3);
        public bool AllPresent { get; private set; }
        public bool Completed { get; private set; }
        public void Reset(){elapsed=0;AllPresent=Completed=false;}
        public bool Step(bool[] present,int required,float dt)
        {
            if(Completed)return false;
            AllPresent=required>0&&required<=present.Length;
            for(int i=0;i<required&&i<present.Length;i++)AllPresent&=present[i];
            if(!AllPresent){elapsed=0;return false;}
            elapsed+=Mathf.Max(0,dt);
            if(elapsed+0.00001f<3)return false;
            Completed=true;return true;
        }
    }
}
