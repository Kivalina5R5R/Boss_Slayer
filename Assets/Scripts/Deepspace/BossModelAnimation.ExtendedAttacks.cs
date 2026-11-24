using UnityEngine;
namespace Deepspace
{
    public partial class BossModelAnimation
    {
        readonly Vector3[] extendedWind=new Vector3[6],extendedHit=new Vector3[6];
        Vector3 extendedWindBody,extendedHitBody,extendedWindTravel,extendedHitTravel;
        // Run after each specialized rig's idle so custom arms, swimming and wing code cannot overwrite the new poses.
        public void SampleChoreographedPose(float time,float charge,float release,float stun)
        {
            if(attackVariant<6){SamplePose(time,charge,release,stun);return;}
            if(!Initialize())return;
            SamplePose(time,0,0,stun);
            powerPoseVisible=charge>0||release>0;
            if(stun>0||defeated||(charge<=0&&release<=0))return;
            float age=(1-Mathf.Clamp01(release))*RecoverySeconds;
            float snap=attackVariant==8?.15f:attackVariant==10?.075f:.12f;
            float wind=release>0?1-Mathf.SmoothStep(0,1,age/snap):Mathf.SmoothStep(0,1,Mathf.Clamp01(charge/.82f));
            float hit=release>0?Mathf.SmoothStep(0,1,age/snap)*(1-Mathf.SmoothStep(0,1,(age-.27f)/(RecoverySeconds-.27f))):0;
            float follow=release>0?Mathf.Sin(Mathf.Clamp01((age-.12f)/.8f)*Mathf.PI)*Mathf.Exp(-age*2):0;
            int move=attackVariant-6;
            if(this is BossJellyfishAnimation jelly){jelly.SampleExtendedTentacles(move,wind,hit,follow,time);return;}
            for(int i=0;i<6;i++)extendedWind[i]=extendedHit[i]=Vector3.zero;
            extendedWindBody=extendedHitBody=extendedWindTravel=extendedHitTravel=Vector3.zero;
            AuthorExtendedAttack(move);
            float weight=motionAmount;
            rigRoot.localRotation*=ToMesh*Quaternion.Euler((extendedWindBody*wind+extendedHitBody*hit)*weight)*ToSculpt;
            float scale=AnimatedMesh!=null?AnimatedMesh.bounds.size.z:1;
            rigRoot.localPosition+=ToMesh*((extendedWindTravel*wind+extendedHitTravel*hit)*scale*weight);
            for(int i=0;i<6;i++)if(parts[i]!=null)
                parts[i].localRotation*=ToMesh*Quaternion.Euler((extendedWind[i]*wind+extendedHit[i]*hit)*weight)*ToSculpt;
            if(this is BossDemonAnimation demon)
            {
                // Elbows, hips, knees and the carried weapon follow the leading arm.
                ExtendedJoint(demon.joints[6],new Vector3(-wind*(move==1?65:30)+hit*20,0,hit*(move==3?28:0)),weight);
                ExtendedJoint(demon.joints[7],new Vector3(-wind*(move==0?65:35)+hit*25,0,-hit*(move==3?28:0)),weight);
                ExtendedJoint(demon.joints[8],new Vector3(wind*(move==2?42:15)-hit*15,0,-wind*8),weight);
                ExtendedJoint(demon.joints[9],new Vector3(wind*(move==4?35:12)+hit*10,0,wind*8),weight);
                ExtendedJoint(demon.joints[10],new Vector3(wind*(move==2?55:20),0,0),weight);
                ExtendedJoint(demon.joints[11],new Vector3(wind*(move==4?50:15),0,0),weight);
                if(!demon.Adult)ExtendedJoint(demon.joints[14],new Vector3(wind*(move==4?90:25),0,hit*(move==5?65:0)),weight);
            }
            if(this is BossDragonAnimation dragon)
            {
                ExtendedJoint(dragon.secondaryParts[0],extendedWind[3]*wind*.45f+extendedHit[3]*hit*.45f,weight);
                ExtendedJoint(dragon.secondaryParts[1],extendedWind[3]*wind*.3f+extendedHit[3]*hit*.3f,weight);
                for(int i=2;i<4;i++)ExtendedJoint(dragon.secondaryParts[i],new Vector3(0,(extendedWind[5].y*wind+extendedHit[5].y*hit)*(i==2?.65f:.9f)+follow*12,0),weight);
                for(int i=4;i<8;i++)ExtendedJoint(dragon.secondaryParts[i],new Vector3((move==2?wind*35-hit*22:hit*12)*(i<6?1:-1),0,0),weight);
            }
            if(this is BossPlesiothAnimation fish)
            {
                ExtendedJoint(fish.secondaryParts[0],extendedWind[3]*wind*.5f+extendedHit[3]*hit*.5f,weight);
                ExtendedJoint(fish.secondaryParts[1],extendedWind[5]*wind*.6f+extendedHit[5]*hit*.7f,weight);
                ExtendedJoint(fish.secondaryParts[2],extendedWind[5]*wind*.4f+extendedHit[5]*hit*.9f+new Vector3(0,follow*20,0),weight);
            }
            if(this is BossSerpentAnimation serpent)serpent.SampleExtendedSpine(move,wind,hit,follow);
        }
        static void ExtendedJoint(Transform joint,Vector3 angle,float weight)
        {if(joint!=null)joint.localRotation*=ToMesh*Quaternion.Euler(angle*weight)*ToSculpt;}
        void E(int joint,Vector3 wind,Vector3 hit){extendedWind[joint]=wind;extendedHit[joint]=hit;}
        void B(Vector3 wind,Vector3 hit,Vector3 prepare,Vector3 travel){extendedWindBody=wind;extendedHitBody=hit;extendedWindTravel=prepare;extendedHitTravel=travel;}
        static Vector3 V(float x=0,float y=0,float z=0)=>new Vector3(x,y,z);
        void AuthorExtendedAttack(int m)
        {
            switch(character)
            {
                case Kind.Demon:
                    switch(m){
                    case 0:B(V(12,-28,-8),V(-12,34,8),V(0,-.02f,.02f),V(0,.025f,-.025f));E(2,V(-25,-35,-35),V(-145,35,25));E(1,V(-40,0,10),V(-20,0,25));break;
                    case 1:B(V(0,38,10),V(5,-45,-12),V(.025f),V(-.035f));E(1,V(-115,40,30),V(-45,-65,-35));E(2,V(-25),V(-85,25,20));break;
                    case 2:B(V(-22),V(32),V(0,.075f,.02f),V(0,-.065f,-.04f));E(1,V(-160,-10,18),V(-40));E(2,V(-165,10,-18),V(-35));break;
                    case 3:B(V(0,-18),V(5,22),V(0,0,.025f),V(0,0,-.03f));E(1,V(-90,55,40),V(-115,-45,-20));E(2,V(-90,-55,-40),V(-115,45,20));break;
                    case 4:B(V(-15,-35),V(24,10),V(0,-.025f,.06f),V(0,-.02f,-.12f));E(1,V(-65,-30,20),V(-85,10,0));E(2,V(-55,45,-20),V(-120,-10,-8));break;
                    default:B(V(5,-65),V(-4,95),V(0,.015f),V(0,.02f));E(1,V(-75,0,55),V(-95,0,75));E(2,V(-75,0,-55),V(-95,0,-75));break;}
                    E(3,V(5,-extendedWindBody.y*.3f),V(-8,-extendedHitBody.y*.3f));break;
                case Kind.Dragon:
                    switch(m){
                    case 0:B(V(-12,-20),V(16,25),V(0,.025f),V(0,-.035f,-.045f));E(1,V(10,-20,-55),V(-15,30,15));E(2,V(0,0,20),V(0,0,40));E(3,V(12,-18),V(-24,25));break;
                    case 1:B(V(0,-45,12),V(0,48,-15),V(.025f),V(-.045f));E(5,V(0,60),V(0,-80));E(3,V(0,25),V(0,-35));E(1,V(0,0,-25),V(0,0,-48));break;
                    case 2:B(V(-28),V(32),V(0,.095f,.025f),V(0,-.065f,-.1f));E(1,V(0,15,-65),V(15,-20,-12));E(2,V(0,-15,65),V(15,20,12));E(3,V(28),V(-40));E(4,V(-35),V(-8));break;
                    case 3:B(V(-8),V(12),V(0,.025f),V(0,0,.045f));E(1,V(-20,0,-72),V(20,0,25));E(2,V(-20,0,72),V(20,0,-25));E(3,V(12),V(-15));break;
                    case 4:B(V(0,-25,-10),V(6,30,8),V(0,0,.03f),V(0,0,-.055f));E(3,V(18,-42),V(-20,45));E(4,V(-15),V(-45));E(5,V(0,25),V(0,-35));break;
                    default:B(V(28),V(-24),V(0,-.025f),V(0,.06f));E(1,V(0,0,-15),V(0,0,-60));E(2,V(0,0,15),V(0,0,60));E(3,V(-18),V(30));E(5,V(25),V(-35));break;}
                    break;
                case Kind.Fish:
                    switch(m){
                    case 0:B(V(0,-25,-32),V(0,32,40),V(-.035f,0,.02f),V(.085f,.035f,-.02f));E(5,V(0,45),V(0,-60));E(1,V(0,0,-35),V(0,0,35));break;
                    case 1:B(V(0,35,18),V(4,-40,-22),V(.02f),V(-.055f));E(2,V(0,-20,60),V(0,30,-30));E(5,V(0,-35),V(0,55));break;
                    case 2:B(V(-30),V(40),V(0,.075f,.035f),V(0,-.06f,-.09f));E(3,V(24),V(-38));E(4,V(-48),V(-6));E(5,V(-20),V(35));break;
                    case 3:B(V(0,-40,-35),V(0,60,65),V(0,.025f),V(0,.02f));E(1,V(0,0,-50),V(0,0,30));E(2,V(0,0,50),V(0,0,-30));E(5,V(0,65),V(0,-70));break;
                    case 4:B(V(12,20),V(-8,-18),V(0,0,.055f),V(0,0,-.075f));E(3,V(25,35),V(-20,-30));E(4,V(-45),V(-12));E(5,V(0,-25),V(0,40));break;
                    default:B(V(22,-10),V(-28,12),V(0,-.02f),V(0,.05f));E(5,V(38,25),V(-55,-35));E(1,V(0,0,25),V(0,0,-40));E(2,V(0,0,-25),V(0,0,40));break;}
                    break;
                case Kind.Golem:
                    switch(m){
                    case 0:B(V(0,-28,-8),V(5,35,12),V(0,-.015f),V(.035f,0,-.02f));E(1,V(75,-55,-35),V(80,60,25));E(2,V(35,0,10),V(50));break;
                    case 1:B(V(22,22,8),V(-18,-25,-8),V(0,-.045f,.02f),V(0,.03f,-.035f));E(2,V(5,35,20),V(145,-20,-10));E(1,V(65),V(35));break;
                    case 2:B(V(-12,12,-12),V(28,-8,10),V(0,.045f),V(0,-.075f));E(1,V(80,0,-35),V(25));E(2,V(135,0,20),V(35));break;
                    case 3:B(V(15),V(-8),V(0,-.035f),V(0,.015f));E(1,V(20,-35,-10),V(105,-55,-65));E(2,V(20,35,10),V(105,55,65));break;
                    case 4:B(V(-12,-38),V(25,-12),V(0,-.025f,.055f),V(0,-.025f,-.1f));E(1,V(75,-20,-15),V(50,15,-35));E(2,V(35,0,10),V(60));break;
                    default:B(V(30),V(-12),V(0,-.045f),V(0,.035f));E(1,V(20,-15,-10),V(125,-35,-55));E(2,V(20,15,10),V(125,35,55));break;}
                    E(3,V(-extendedWindBody.x*.4f),V(-extendedHitBody.x*.45f));break;
                case Kind.Wraith:
                    switch(m){
                    case 0:B(V(0,-35,-18),V(0,50,20),V(-.03f,.02f),V(.05f,0,-.03f));E(1,V(50,-60,-75),V(115,65,15));E(2,V(60,20,20),V(25,0,45));break;
                    case 1:B(V(0,45,22),V(0,-55,-25),V(.025f),V(-.05f,.025f));E(2,V(55,65,75),V(115,-65,-15));E(1,V(30),V(75));break;
                    case 2:B(V(-25),V(38),V(0,.12f,.04f),V(0,-.045f,-.1f));E(1,V(135,0,-35),V(85,0,-10));E(2,V(135,0,35),V(85,0,10));break;
                    case 3:B(V(0,-15),V(0,20),V(0,.015f),V(0,.03f));E(1,V(90,45,-60),V(95,-45,30));E(2,V(90,-45,60),V(95,45,-30));break;
                    case 4:B(V(-18,25),V(28,-15),V(0,0,.065f),V(0,0,-.12f));E(1,V(25,-20,-15),V(120,10,-25));E(2,V(25,20,15),V(120,-10,25));break;
                    default:B(V(-8),V(10),V(0,.07f),V(0,-.02f));E(1,V(65,0,-85),V(100,35,-10));E(2,V(65,0,85),V(100,-35,10));break;}
                    E(5,V(0,-extendedWindBody.y*.4f,-15),V(0,-extendedHitBody.y*.6f,20));break;
                case Kind.Sentinel:
                    switch(m){
                    case 0:B(V(0,-25,-8),V(12,20,8),V(0,0,.03f),V(0,0,-.06f));E(1,V(60,-40,-25),V(105,15,0));E(2,V(55),V(75));break;
                    case 1:B(V(-5,28),V(12,-18),V(.015f,0,-.015f),V(-.02f,0,.045f));E(2,V(80,35,10),V(105,-20,-10));E(1,V(35),V(50));break;
                    case 2:B(V(-15),V(25),V(0,.065f),V(0,-.065f));E(1,V(105,0,-35),V(35,0,-20));E(2,V(105,0,35),V(35,0,20));E(4,V(30),V(-15));E(5,V(30),V(-15));break;
                    case 3:B(V(0,-8),V(6,12),V(0,0,-.02f),V(0,0,.05f));E(1,V(88,45,25),V(98,-40,-45));E(2,V(88,-45,-25),V(98,40,45));break;
                    case 4:B(V(-12,-30),V(22,10),V(0,-.02f,.04f),V(0,-.01f,-.09f));E(1,V(65,-30,-10),V(115,5,0));E(2,V(70,25,10),V(45));E(4,V(25),V(-20));break;
                    default:B(V(20),V(-16),V(0,-.035f),V(0,.03f));E(1,V(65,35,15),V(110,-30,-70));E(2,V(65,-35,-15),V(110,30,70));break;}
                    break;
                case Kind.Serpent:
                    // The complete spine is deformed below, preserving its connection to the head.
                    switch(m){
                    case 0:B(V(0,-22),V(0,30),V(0,0,.02f),V(0,0,-.04f));break;
                    case 1:B(V(0,28,-10),V(0,-35,14),V(.02f),V(-.035f));break;
                    case 2:B(V(-15),V(22),V(0,.055f),V(0,-.03f,-.045f));break;
                    case 3:B(V(0,-25,-12),V(0,38,18),V(0,.01f),V(0,.025f));break;
                    case 4:B(V(-8),V(12),V(0,0,.045f),V(0,0,-.07f));break;
                    default:B(V(12),V(-16),V(0,-.015f),V(0,.04f));break;}
                    E(4,V(-32),V(-5));break;
            }
        }
    }
}
