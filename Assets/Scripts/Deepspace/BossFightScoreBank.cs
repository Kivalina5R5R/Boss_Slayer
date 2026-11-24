using System;
using UnityEngine;
using UnityEngine.Audio;

namespace Deepspace
{
    public sealed class BossFightScoreBank : ScriptableObject
    {
        [Serializable] public struct BossTrack { public string bossName; public AudioClip music, roar; }
        public AudioMixerGroup output;
        public AudioClip menu, result;
        public BossTrack[] bosses;
    }
}
