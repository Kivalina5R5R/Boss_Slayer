using UnityEngine;

namespace BossSlayer
{
    public partial class BossModelAnimation
    {
        // Five authored attack silhouettes per rigid rig. Values are in the sculpt's joint axes.
        // 1 sweep, 2 impact, 3 projectile, 4 ultimate summon, 5 signature thrust.
        void ChoreographAttack(ref Vector3 windBody,ref Vector3 fireBody,ref Vector3 windTravel,ref Vector3 fireTravel)
        {
            if(attackVariant==0)return;
            for(int i=0;i<6;i++)gather[i]=fire[i]=Vector3.zero;
            windBody=fireBody=windTravel=fireTravel=Vector3.zero;
            float side=castId%2==0?1:-1;
            int leading=side<0?1:2,other=side<0?2:1;
            switch(character)
            {
                case Kind.Dragon:
                    if(attackVariant==1){gather[3].y=-side*26;fire[3].y=side*34;gather[4].x=-14;fire[4].x=-32;gather[1].z=-18;gather[2].z=18;fire[1].z=-28;fire[2].z=28;windBody.y=-side*8;fireBody.y=side*12;gather[5].y=side*14;fire[5].y=-side*18;}
                    else if(attackVariant==2){gather[1].z=-24;gather[2].z=24;fire[1].z=-42;fire[2].z=42;gather[3].x=14;fire[3].x=-18;windTravel.y=.025f;fireTravel.y=-.035f;fireBody.x=8;}
                    else if(attackVariant==3){gather[1].z=-12;gather[2].z=12;gather[3].x=20;gather[4].x=-15;fire[3].x=-22;fire[4].x=-34;fireTravel.z=-.025f;windBody.x=4;fireBody.x=-5;}
                    else if(attackVariant==4){gather[1].z=-55;gather[2].z=55;gather[3].x=34;gather[4].x=-24;fire[1].z=-38;fire[2].z=38;fire[3].x=-18;fire[4].x=-38;windTravel.y=.04f;fireTravel.y=.015f;windBody.x=-6;fireBody.x=5;}
                    else{gather[1].z=-10;gather[2].z=10;gather[3].x=15;gather[4].x=-22;fire[1].z=-30;fire[2].z=30;fire[3].x=-24;fire[4].x=-6;windBody.x=6;fireBody.x=-10;windTravel.z=.018f;fireTravel.z=-.04f;}
                    break;
                case Kind.Demon:
                    if(attackVariant==1){gather[leading]=new Vector3(75,-side*28,-side*25);fire[leading]=new Vector3(90,side*40,side*35);gather[other].x=30;fire[other].x=20;windBody.y=-side*18;fireBody.y=side*23;}
                    else if(attackVariant==2){gather[1]=new Vector3(145,0,-15);gather[2]=new Vector3(145,0,15);fire[1].x=25;fire[2].x=25;windBody.x=-8;fireBody.x=18;windTravel.y=.03f;fireTravel.y=-.045f;}
                    else if(attackVariant==3){gather[1]=new Vector3(65,-25,-20);gather[2]=new Vector3(65,25,20);fire[1]=new Vector3(100,5,12);fire[2]=new Vector3(100,-5,-12);windTravel.z=.025f;fireTravel.z=-.055f;}
                    else if(attackVariant==4){gather[1]=new Vector3(125,-10,-60);gather[2]=new Vector3(125,10,60);fire[1]=new Vector3(100,0,-80);fire[2]=new Vector3(100,0,80);gather[3].x=-20;fire[3].x=15;windTravel.y=.04f;fireBody.x=-8;}
                    else{gather[leading]=new Vector3(30,-side*35,-side*12);fire[leading]=new Vector3(110,side*10,0);gather[other].x=70;fire[other].x=35;windBody.y=-side*20;fireBody.y=side*12;fireTravel.z=-.065f;}
                    gather[5].y=side*22;fire[5].y=-side*25;
                    break;
                case Kind.Fish:
                    if(attackVariant==1){gather[5].y=side*48;fire[5].y=-side*50;gather[leading].z=-side*30;fire[other].z=side*35;windBody.y=-side*22;fireBody.y=side*25;}
                    else if(attackVariant==2){gather[1].z=-45;gather[2].z=45;fire[1].z=30;fire[2].z=-30;gather[4].x=-18;fire[4].x=-28;windTravel.y=.06f;fireTravel.y=-.05f;fireBody.x=-18;}
                    else if(attackVariant==3){gather[4].x=-38;fire[4].x=-12;gather[5].y=20;fire[5].y=-30;windBody.x=12;fireBody.x=-7;fireTravel.z=.04f;}
                    else if(attackVariant==4){gather[1].z=-65;gather[2].z=65;gather[4].x=-45;fire[1].z=-30;fire[2].z=30;fire[4].x=-50;windTravel.y=.055f;fireBody.y=side*15;}
                    else{gather[4].x=-52;fire[4].x=-4;gather[5].y=-side*38;fire[5].y=side*42;windTravel.z=.045f;fireTravel.z=-.095f;fireBody.x=-16;}
                    break;
                case Kind.Golem:
                    if(attackVariant==1){gather[leading]=new Vector3(135,0,-side*20);fire[leading]=new Vector3(20,side*15,0);gather[other].x=30;fire[other].x=45;windBody.z=-side*6;fireBody.z=side*8;}
                    else if(attackVariant==2){gather[1].x=gather[2].x=155;fire[1].x=fire[2].x=15;gather[3].x=-8;fire[3].x=20;windTravel.y=.04f;fireTravel.y=-.065f;fireBody.x=16;}
                    else if(attackVariant==3){gather[1]=new Vector3(45,-25,-18);gather[2]=new Vector3(45,25,18);fire[1]=new Vector3(85,-10,5);fire[2]=new Vector3(85,10,-5);windBody.x=8;fireTravel.z=-.03f;}
                    else if(attackVariant==4){gather[1]=new Vector3(110,0,-50);gather[2]=new Vector3(110,0,50);fire[1]=new Vector3(135,-15,-30);fire[2]=new Vector3(135,15,30);gather[3].x=-18;fire[3].x=-8;windTravel.y=.025f;}
                    else{gather[leading].x=65;gather[other].x=15;fire[leading].x=110;fire[other].x=65;windBody.y=-side*15;fireBody.y=side*12;fireTravel.z=-.045f;fireTravel.y=-.02f;}
                    break;
                case Kind.Wraith:
                    if(attackVariant==1){gather[leading]=new Vector3(40,-side*25,-side*65);fire[leading]=new Vector3(95,side*45,side*20);gather[other].x=60;fire[other].z=-side*35;windBody.y=-side*22;fireBody.y=side*25;}
                    else if(attackVariant==2){gather[1]=new Vector3(65,-30,-15);gather[2]=new Vector3(65,30,15);fire[1]=new Vector3(80,0,-75);fire[2]=new Vector3(80,0,75);windTravel.y=.035f;fireTravel.y=-.015f;}
                    else if(attackVariant==3){gather[leading].x=40;gather[other].x=80;fire[leading].x=115;fire[other].x=30;windBody.z=-side*10;fireBody.z=side*12;fireTravel.z=-.04f;}
                    else if(attackVariant==4){gather[1]=new Vector3(120,0,-45);gather[2]=new Vector3(120,0,45);fire[1]=new Vector3(70,0,-90);fire[2]=new Vector3(70,0,90);gather[3].x=-20;windTravel.y=.09f;fireTravel.y=.035f;}
                    else{gather[1].x=15;gather[2].x=15;fire[1]=new Vector3(95,0,-35);fire[2]=new Vector3(95,0,35);windBody.y=-side*28;fireBody.y=side*28;windTravel.z=.04f;fireTravel.z=-.07f;}
                    gather[5].z=-side*12;fire[5].z=side*18;
                    break;
                case Kind.Sentinel:
                    if(attackVariant==1){gather[1]=new Vector3(88,-25,0);gather[2]=new Vector3(88,-25,0);fire[1]=new Vector3(92,30,0);fire[2]=new Vector3(92,30,0);windBody.y=-side*18;fireBody.y=side*20;}
                    else if(attackVariant==2){gather[1]=new Vector3(85,0,-45);gather[2]=new Vector3(85,0,45);fire[1]=new Vector3(100,0,-65);fire[2]=new Vector3(100,0,65);fireTravel.z=.035f;}
                    else if(attackVariant==3){gather[leading]=new Vector3(125,0,-side*12);gather[other].x=60;fire[leading].x=100;fire[other].x=85;gather[3].x=-15;fireBody.x=12;fireTravel.z=.04f;}
                    else if(attackVariant==4){gather[1]=new Vector3(150,0,-18);gather[2]=new Vector3(150,0,18);fire[1]=new Vector3(110,0,-35);fire[2]=new Vector3(110,0,35);gather[3].x=-22;windTravel.y=.045f;fireTravel.z=.025f;}
                    else{gather[1]=new Vector3(90,-12,-8);gather[2]=new Vector3(90,12,8);fire[1]=new Vector3(97,-4,-4);fire[2]=new Vector3(97,4,4);gather[3].x=5;fireBody.x=16;windTravel.z=-.018f;fireTravel.z=.07f;}
                    break;
                case Kind.Serpent:
                    if(attackVariant==1){gather[5].y=side*52;fire[5].y=-side*58;gather[3].y=-side*20;fire[3].y=side*25;windBody.y=-side*15;fireBody.y=side*18;}
                    else if(attackVariant==2){gather[3].x=32;fire[3].x=-35;gather[5].y=25;fire[5].y=-28;windTravel.y=.065f;fireTravel.y=-.045f;}
                    else if(attackVariant==3){gather[3]=new Vector3(22,-side*10,0);fire[3]=new Vector3(-25,side*8,0);gather[5].y=-side*20;fireTravel.z=-.05f;}
                    else if(attackVariant==4){gather[3].x=45;gather[1].z=-45;gather[2].z=45;fire[3].x=15;fire[1].z=-60;fire[2].z=60;windTravel.y=.08f;fireBody.y=side*20;}
                    else{gather[3].x=38;fire[3].x=-45;gather[5].y=side*40;fire[5].y=-side*35;windTravel.z=.05f;fireTravel.z=-.09f;}
                    break;
            }
        }
    }
}
