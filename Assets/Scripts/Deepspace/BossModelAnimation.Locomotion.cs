using UnityEngine;

namespace Deepspace
{
    public partial class BossModelAnimation
    {
        float locomotion;
        int attackVariant;
        public float Locomotion => locomotion;
        public int AttackVariant => attackVariant;
        public void SetLocomotion(float signedSpeed) { locomotion = Mathf.Clamp(signedSpeed,-1,1); }
        public void SetAttackVariant(int variant) { attackVariant = Mathf.Clamp(variant,0,13); } // 12/13: Demon ultimate cut / spear throw

        void LivingPose(float time, out Vector3 body, out Vector3 travel)
        {
            float speed=Mathf.Abs(locomotion), direction=Mathf.Sign(locomotion);
            float breath=Mathf.Sin(time*1.7f), step=Mathf.Sin(time*(2.5f+speed*2.5f));
            body=Vector3.zero; travel=Vector3.zero;
            idle[3].y += Mathf.Sin(time*.73f)*4;
            switch(character)
            {
                case Kind.Dragon:
                    float flap=Mathf.Sin(time*1.25f);
                    idle[1].z=-flap*(4+speed*4); idle[2].z=flap*(4+speed*4);
                    idle[3].y+=Mathf.Sin(time*.65f)*3;
                    idle[5].y=Mathf.Sin(time*1.15f)*7;
                    body=new Vector3(breath*1.2f+speed*2,locomotion*3,-locomotion*2);
                    travel.y=breath*.004f+Mathf.Abs(step)*speed*.006f;
                    break;
                case Kind.Demon:
                    idle[1].x+=step*speed*13; idle[2].x-=step*speed*13;
                    idle[3].x+=breath*3; idle[5].y+=Mathf.Sin(time*1.3f)*9;
                    body=new Vector3(speed*5,Mathf.Sin(time*1.2f)*2,step*speed*3);
                    travel.y=Mathf.Abs(step)*speed*.012f+breath*.005f;
                    break;
                case Kind.Fish:
                    idle[5].y=Mathf.Sin(time*(3+speed*3))*(20+speed*16);
                    idle[1].z-=step*speed*9; idle[2].z+=step*speed*9;
                    idle[4].x-=Mathf.Max(0,breath)*5;
                    body=new Vector3(breath*3,step*speed*4,-locomotion*7);
                    travel.z=step*speed*.018f; travel.y=breath*.013f;
                    break;
                case Kind.Golem:
                    idle[1].x+=step*speed*11; idle[2].x-=step*speed*11;
                    idle[3].y*=.5f;
                    body=new Vector3(breath*1.2f,step*speed*2,step*speed*3.5f);
                    travel.y=(1-Mathf.Abs(step))*speed*.008f;
                    break;
                case Kind.Wraith:
                    idle[1].z+=Mathf.Sin(time*1.3f)*9; idle[2].z-=Mathf.Sin(time*1.3f+.5f)*9;
                    idle[5].z+=Mathf.Sin(time*1.7f-1)*10;
                    body=new Vector3(breath*3,Mathf.Sin(time*.8f)*5,-locomotion*8);
                    travel.y=breath*.025f; travel.x=Mathf.Sin(time*.9f)*.008f;
                    break;
                case Kind.Sentinel:
                    idle[3].y=Mathf.Sin(time*.85f)*12;
                    idle[1].x+=speed*8; idle[2].x+=speed*8;
                    body=new Vector3(speed*4,0,-locomotion*3);
                    travel.y=breath*.006f;
                    break;
                case Kind.Serpent:
                    idle[3].y+=Mathf.Sin(time*(2+speed))*8;
                    idle[5].y=Mathf.Sin(time*(2.5f+speed*2)-1)*(20+speed*13);
                    body=new Vector3(breath*3,Mathf.Sin(time*(2+speed))*4,-direction*speed*5);
                    travel.y=breath*.016f; travel.x=step*speed*.008f;
                    break;
            }
        }

        void StyleAttackPose(ref Vector3 windBody, ref Vector3 fireBody, ref Vector3 windTravel, ref Vector3 fireTravel)
        {
            ChoreographAttack(ref windBody, ref fireBody, ref windTravel, ref fireTravel);
        }
    }
}