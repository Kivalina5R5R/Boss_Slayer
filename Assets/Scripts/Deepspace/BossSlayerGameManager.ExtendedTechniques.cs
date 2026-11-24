using UnityEngine;
namespace Deepspace
{
    public sealed partial class BossSlayerGameManager
    {
        static string ExtendedTechniqueName(BossPassiveState.Kind kind,int move)
        {
            switch(kind){
                case BossPassiveState.Kind.Demon:return new[]{"RISING CLEAVE","REVERSE SLASH","VAULT SLAM","CROSS CUT","SHOULDER RUSH","SPEAR CYCLONE"}[move];
                case BossPassiveState.Kind.Fish:return new[]{"SIDE BREACH","FIN SLICE","DIVING BITE","SPIRAL WAKE","SNAP JET","TAIL UPPERCUT"}[move];
                case BossPassiveState.Kind.Dragon:return new[]{"TALON RAKE","TAIL WHIP","DIVING CRASH","WING GUST","SIDE BREATH","REARING FLARE"}[move];
                case BossPassiveState.Kind.Jellyfish:return new[]{"SPIRAL LASH","TENTACLE SCISSORS","BELL DROP","ELECTRIC BLOOM","SPEAR TENTACLE","INVERTED PULSE"}[move];
                case BossPassiveState.Kind.Golem:return new[]{"CRYSTAL BACKHAND","STONE UPPERCUT","HEAVY STOMP","RAKING CRYSTALS","SHOULDER CHARGE","GROUND RIP"}[move];
                case BossPassiveState.Kind.Wraith:return new[]{"REAPING ARC","REVERSE SCYTHE","PHANTOM DIVE","RIFT FOLD","SHADOW LUNGE","SOUL PULL"}[move];
                case BossPassiveState.Kind.Sentinel:return new[]{"SHIELD BASH","ARM CANNON","LANDING SHOCK","CROSSFIRE","SPEAR DRIVE","REACTOR DISCHARGE"}[move];
                case BossPassiveState.Kind.Serpent:return new[]{"HOOK BITE","REVERSE TAIL","WATERFALL DIVE","SPIRAL WAKE","LONG FANG","COIL BURST"}[move];
                case BossPassiveState.Kind.Hydra:return new[]{"LEFT HOOK FANGS","REVERSE FANGS","HIGH NECK DIVE","CORKSCREW BITE","RECOIL LUNGE","RISING VENOM"}[move];
                default:return "";
            }
        }
        bool SpawnExtendedBossTechnique(BossPassiveState.Kind kind,int move)
        {
            string title=ExtendedTechniqueName(kind,move);if(title.Length==0)return false;
            int variant=move+6,first=attacks.Count,side=Random.value<.5f?-1:1;
            if(kind==BossPassiveState.Kind.Hydra)
            {
                var hydra=bossModel.GetComponent<BossHydra>();if(hydra==null)return false;
                int beat=0;
                for(int head=0;head<BossHydra.MaximumHeads;head++)
                {
                    int index=move%2==0?head:BossHydra.MaximumHeads-1-head;if(!hydra.IsHeadReady(index))continue;
                    float x=move==4?.5f:Mathf.Lerp(.2f,.8f,index/6f),z=move==2?.7f:move==5?.3f:beat%2==0?.35f:.65f;
                    if(move==5)TechniqueLine(title,x,beat,variant);else TechniqueCircle(title,x,z,.17f,beat,variant);
                    var attack=attacks[attacks.Count-1];attack.HydraOwner=hydra;attack.HydraHeadIndex=index;beat++;
                }
            }
            else if(move==0)
            {
                if(kind==BossPassiveState.Kind.Jellyfish)TechniqueWave(title,false,side);
                else if(kind==BossPassiveState.Kind.Wraith||kind==BossPassiveState.Kind.Fish||kind==BossPassiveState.Kind.Golem)TechniqueZone(title,side,0,variant);
                else TechniqueCircle(title,side<0?.3f:.7f,.5f,.22f,0,variant);
            }
            else if(move==1)
            {
                if(kind==BossPassiveState.Kind.Jellyfish){TechniqueLine(title,.25f,0,variant);TechniqueLine(title,.75f,0,variant);}
                else if(kind==BossPassiveState.Kind.Sentinel||kind==BossPassiveState.Kind.Fish)TechniqueLine(title,side<0?.3f:.7f,0,variant);
                else if(kind==BossPassiveState.Kind.Golem)TechniqueCircle(title,.5f,.4f,.22f,0,variant);
                else TechniqueZone(title,side,0,variant);
            }
            else if(move==2)TechniqueCircle(title,.5f,.55f,.26f,0,variant);
            else if(move==3)
            {
                if(kind==BossPassiveState.Kind.Dragon||kind==BossPassiveState.Kind.Fish||kind==BossPassiveState.Kind.Serpent)TechniqueWave(title,true,side);
                else if(kind==BossPassiveState.Kind.Jellyfish)TechniqueCircle(title,.5f,.5f,.27f,0,variant);
                else {TechniqueLine(title,.25f,0,variant);TechniqueLine(title,.75f,0,variant);}
            }
            else if(move==4)TechniqueLine(title,side<0?.35f:.65f,0,variant);
            else
            {
                if(kind==BossPassiveState.Kind.Demon||kind==BossPassiveState.Kind.Golem||kind==BossPassiveState.Kind.Sentinel)TechniqueWave(title,false,side);
                else if(kind==BossPassiveState.Kind.Dragon||kind==BossPassiveState.Kind.Jellyfish)TechniqueZone(title,side,0,variant);
                else TechniqueCircle(title,.5f,.45f,.25f,0,variant);
            }
            for(int i=first;i<attacks.Count;i++)attacks[i].AnimationVariant=variant;
            return attacks.Count>first;
        }
    }
}
