using System;
using UnityEngine;
using UnityEngine.Audio;

namespace BossSlayer
{
    public sealed class BossFightScoreBank : ScriptableObject
    {
        [Serializable] public struct BossTrack { public string bossName; public AudioClip music, roar; }
        public AudioMixerGroup output;
        public AudioClip menu, result;
        // 2026-11-25: the Abyssal King (rush finale) has its own battle track; it used to borrow the Void Wraith's.
        public AudioClip abyssalKing;
        public BossTrack[] bosses;
    }
}
