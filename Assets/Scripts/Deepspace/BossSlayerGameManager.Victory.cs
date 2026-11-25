using System.Collections;
using UnityEngine;
namespace Deepspace
{
    public sealed partial class BossSlayerGameManager
    {
        BossVictoryPanel floorVictory,wallVictory;BossVictoryGraphic floorDefeatBurst;
        public bool IsShowingBossVictory{get;private set;}
        void PlayBossDefeatBurst()=>PlayBossDefeatBurst(true);
        void PlayBossDefeatBurst(bool wallVoxels)
        {
            if(bossModel==null)return;
            if(wallVoxels&&deathVfx==null)deathVfx=gameObject.AddComponent<BossDeathVfx>();
            if(wallVoxels)deathVfx.Play(bossModel,bossModel.GetComponentsInChildren<Renderer>(true)); // the new models break into their own voxels
            sfx?.Play("death_final",.8f,0,.2f,5);
            // 2026-11-25 "เวลาบอสตาย ให้กระจายที่จอกำแพงพอ จอพื้นที่กระจายเอาออกเลย": the floor no longer plays its own
            // voxel burst; the boss breaks apart on the wall only.
            if(floorDefeatBurst!=null)floorDefeatBurst.gameObject.SetActive(false);
        }
        IEnumerator ShowBossVictory()
        {
            if(floorDefeatBurst!=null)floorDefeatBurst.gameObject.SetActive(false);
            SetGameplayHudVisible(false);IsShowingBossVictory=true;
            // 2026-11-19: "หน้าตอนปราบบอสเสร็จตัวคนตรงพื้นหาย" - the HUD switch above also hid the floor's players; they stay
            // and keep walking (FollowPlayersOutsideFight runs while resultShown) under the wall's VICTORY card.
            if(characterPanel!=null)characterPanel.gameObject.SetActive(true);
            var font=BossUiFont; // บาส 2026-09-29: same Outfit SDF as select/ready/result (was the TMP default via statusText)
            // บาส 2026-11-18: the VICTORY card shows on the wall only; the floor keeps the arena (was a second copy there).
            if(floorVictory!=null)floorVictory.Hide();
            if(wallVictory==null&&cutsceneArea!=null)wallVictory=BossVictoryPanel.Create(cutsceneArea,font,false);
            string modelName=bossModel!=null?bossModel.name:"";Color accent=BossAttackVfx.ElementColor(BossAttackVfx.ThemeFor(modelName));
            bool next=HasNextSessionBoss()&&!(IsRushActive&&rushKingDefeated);int round=Mathf.Max(1,sessionBossIndex),total=sessionActive&&!IsRushActive?sessionBossTargetCount:round;
            // บาส 2026-11-04: the rush showed "ROUND 1 / 1", "2 / 2" (total was the round itself). The rush is counted in
            // halves of rushBossesPerHalf bosses (3 -> perk vote -> 3 -> perk vote -> king), so the card reads the boss within
            // its half: 1 / 3, 2 / 3, 3 / 3. The Abyssal King has no count: total 0 = "FINAL BOSS CLEARED".
            if(IsRushActive){if(rushKingDefeated){round=1;total=0;}else{total=Mathf.Max(1,rushBossesPerHalf);round=Mathf.Clamp(rushHalfKills,1,total);}}
            ThemeBossVictory(wallVictory);
            wallVictory?.Show(modelName,round,total,next,accent,rushBossEscaped);
            if(!rushBossEscaped)sfx?.Play("victory",.65f,0,.5f,5);
            try
            {
                for(float age=0;age<BossVictoryPanel.Duration;age+=Time.deltaTime){wallVictory?.Sample(age);yield return null;}
            }
            finally{HideBossVictory();}
        }
        void HideBossVictory()
        {
            IsShowingBossVictory=false;if(floorVictory)floorVictory.Hide();if(wallVictory)wallVictory.Hide(); // Unity null-check: panels may already be destroyed when this runs from OnDisable at play-mode exit
            if(floorDefeatBurst!=null)floorDefeatBurst.gameObject.SetActive(false); // บาส 2026-10-02: was stuck on the end of the comment above, so it never ran
        }
    }
}
