using UnityEngine;
namespace BossSlayer
{
 public sealed partial class BossSlayerGameManager
 {
  const float SerpentWarning=SignatureWarning;
  void SpawnSerpentSurge(string title)
  {
   for(int beat=0;beat<6;beat++){
    // บาส 2026-10-06 (B2): wave warning .95 -> 1.5 s; beat 2.15 -> 2.7 keeps the same gap between waves.
    float delay=beat*(2.15f+SerpentWarning-.95f);bool horizontal=beat%2==0;int direction=(beat/2)%2==0?1:-1;
    SignatureWave(title,horizontal,direction,delay);
    var wave=attacks[attacks.Count-1];wave.IsSerpentSurge=true;wave.TelegraphTotal=SerpentWarning;wave.TelegraphTimer=delay+SerpentWarning;wave.ActiveTimer=delay+SerpentWarning+wave.MoveSeconds;wave.AnimationVariant=4;
   }
  }
  void UpdateSerpentWave(AttackState attack)
  {
   if(!attack.IsSerpentSurge)return;
   if(attack.SerpentCrests.Count==0)foreach(var section in attack.WaveDangerVfx){
    var go=new GameObject("VFX == Serpent rolling crest");go.transform.SetParent(section.transform,false);
    var vfx=go.AddComponent<BossSerpentWaveVfx>();vfx.Initialize(section.GetComponent<RectTransform>(),floorDisplayCamera,attack.WaveHorizontal);attack.SerpentCrests.Add(vfx);
   }
   foreach(var section in attack.WaveDangerVfx)section.GetComponent<UnityEngine.UI.Image>().enabled=attack.TelegraphTimer>0;
   foreach(var crest in attack.SerpentCrests)crest.Sample(attack.TelegraphTimer,attack.ActiveTimer);
  }
 }
}