using UnityEngine;
using UnityEngine.Audio;

namespace Deepspace
{
    public sealed class BossFightAudioBank : ScriptableObject
    {
        [System.Serializable] public struct Entry { public string id; public AudioClip clip; }
        public AudioMixerGroup output;
        public Entry[] clips;
    }
}
