using UnityEngine;

namespace Deepspace
{
    // Two reusable BGM voices. Creature calls use the existing priority-limited SFX pool.
    public sealed class BossFightScore : MonoBehaviour
    {
        [SerializeField, Range(0, 1)] float volume = 0.24f;
        BossFightScoreBank bank;
        BossFightAudio sfx;
        AudioSource[] music;
        readonly float[] weights = new float[2];
        int target = -1, theme = -1;
        float duckUntil, nextRoar;
        bool paused;
        public string CurrentCue { get; private set; }
        public int Theme => theme;
        public bool IsPaused => paused;
        public AudioClip CurrentClip => target < 0 || music == null ? null : music[target].clip;

        public void Initialize(BossFightAudio owner)
        {
            if (music != null) return;
            sfx = owner;
            bank = Resources.Load<BossFightScoreBank>("BossFightScore");
            if (bank == null) { Debug.LogError("BossFight score bank is missing.", this); return; }
            music = new AudioSource[2];
            for (int i = 0; i < 2; i++)
            {
                var go = new GameObject("BGM crossfade " + i);
                go.transform.SetParent(transform, false);
                var source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = true;
                source.spatialBlend = 0;
                source.volume = 0;
                source.outputAudioMixerGroup = bank.output;
                music[i] = source;
            }
        }

        public void Menu() { theme = -1; Switch(bank != null ? bank.menu : null, "menu"); }
        public void Result() { theme = -1; Switch(bank != null ? bank.result : null, "result"); }
        public void Boss(int value)
        {
            if (bank == null || bank.bosses == null || value < 0 || value >= bank.bosses.Length) return;
            theme = value;
            nextRoar = 0;
            Switch(bank.bosses[value].music, bank.bosses[value].bossName);
        }
        public bool Roar()
        {
            if (theme < 0 || bank == null || Time.timeScale <= 0 || Time.time < nextRoar) return false;
            var clip = bank.bosses[theme].roar;
            if (clip == null || !sfx.PlayClip("roar_" + theme, clip, 0.64f, 0, 0, 4)) return false;
            nextRoar = Time.time + clip.length + 1.5f;
            duckUntil = Time.time + clip.length;
            return true;
        }
        void Switch(AudioClip clip, string cue)
        {
            if (music == null || clip == null) return;
            if (target >= 0 && CurrentClip == clip) return;
            int slot = target < 0 ? 0 : 1 - target;
            // A rapid menu/restart can reuse the already fading track without seeking it.
            if (music[slot].clip != clip || (!music[slot].isPlaying && !paused))
            {
                music[slot].Stop();
                music[slot].clip = clip;
                weights[slot] = 0;
                music[slot].volume = 0;
                music[slot].Play();
            }
            target = slot;
            CurrentCue = cue;
            if (paused) { foreach (var source in music) source.UnPause(); paused = false; }
            duckUntil = 0;
        }
        void Update()
        {
            if (music == null) return;
            bool shouldPause = theme >= 0 && Time.timeScale <= 0;
            if (shouldPause != paused)
            {
                foreach (var source in music) { if (shouldPause) source.Pause(); else source.UnPause(); }
                paused = shouldPause;
            }
            if (paused) return;
            float duck = Time.time < duckUntil ? 0.32f : 1;
            for (int i = 0; i < music.Length; i++)
            {
                weights[i] = Mathf.MoveTowards(weights[i], i == target ? 1 : 0, Time.unscaledDeltaTime / 1.1f);
                music[i].volume = Mathf.MoveTowards(music[i].volume, weights[i] * volume * duck * Fortal.Arena.FortalArenaSettings.Music, Time.unscaledDeltaTime * 0.8f);
                if (i != target && weights[i] == 0 && music[i].isPlaying) music[i].Stop();
            }
        }
        public void StopMusic()
        {
            if (music != null) foreach (var source in music) source.Stop();
            weights[0] = weights[1] = 0;
            target = theme = -1;
            CurrentCue = null;
            paused = false;
        }
        void OnDisable() { StopMusic(); }
    }
}
