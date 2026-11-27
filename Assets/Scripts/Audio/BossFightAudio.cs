using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace BossSlayer
{
    [DefaultExecutionOrder(200)]
    public sealed class BossFightAudio : MonoBehaviour
    {
        [SerializeField, Range(0,1)] float volume = 0.72f;
        const int VoiceCount = 16;
        readonly Dictionary<string, AudioClip> clips = new();
        readonly Dictionary<string, float> lastPlayed = new();
        readonly List<AudioListener> disabledListeners = new();
        readonly List<(Button button, UnityAction action)> buttons = new();
        AudioSource[] voices;
        int[] priorities;
        bool[] uiVoice;
        bool[] suspended;
        float[] levels;
        AudioSource charge, hold, bigCharge;
        // Balance Note v3: the big move's 6 s wind-up sound, on its own source so a BREAK! can cut it.
        const float BigChargeGain = .55f;
        BossFightAudioBank bank;
        int holdFrame = -1, phase = 1;
        float holdProgress, holdPan, holdLevel;
        bool paused;
        public int PlayedCount { get; private set; }
        public string LastPlayed { get; private set; }
        public BossFightScore Score { get; private set; }
        public int ClipCount => clips.Count;
        public bool ChargePlaying => charge != null && charge.isPlaying;

        // 2026-11-25: the one live player, for screens that have no game manager reference (statues, wall crumble).
        public static BossFightAudio Current { get; private set; }

        // UI-side one-shots (they play while the game is paused too); a missing id is silent.
        public static void PlayUi(string id, float gain = 0.5f, float cooldown = 0.05f, int priority = 2)
        {
            if (Current != null) Current.Play(id, gain, 0, cooldown, priority, true);
        }

        // True while any voice is still playing the clip registered as id.
        public bool IsPlaying(string id)
        {
            if (voices == null || !clips.TryGetValue(id, out var clip)) return false;
            foreach (var voice in voices) if (voice != null && voice.isPlaying && voice.clip == clip) return true;
            return false;
        }

        public void Initialize()
        {
            Current = this;
            if (voices != null) return;
            bank = GameArt.Load<BossFightAudioBank>("Audio/BossFightAudio");
            if (bank == null) { Debug.LogError("BossFightAudio bank is missing.", this); return; }
            foreach (var entry in bank.clips) if (entry.clip != null) clips[entry.id] = entry.clip;
            voices = new AudioSource[VoiceCount];
            priorities = new int[VoiceCount];
            uiVoice = new bool[VoiceCount];
            suspended = new bool[VoiceCount];
            levels = new float[VoiceCount];
            for (int i = 0; i < VoiceCount; i++) voices[i] = MakeSource("SFX voice " + i);
            charge = MakeSource("Boss charge");
            bigCharge = MakeSource("Boss big charge");
            hold = MakeSource("Gem gathering");
            hold.loop = true;
            Score = GetComponent<BossFightScore>() ?? gameObject.AddComponent<BossFightScore>();
            Score.Initialize(this);
            // The two display cameras share one 2D sound field.
            AudioListener keeper = null;
            foreach (var listener in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
            {
                if (listener.gameObject.scene != gameObject.scene || !listener.enabled) continue;
                if (keeper == null) keeper = listener;
                else { listener.enabled = false; disabledListeners.Add(listener); }
            }
        }

        AudioSource MakeSource(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0;
            source.outputAudioMixerGroup = bank.output;
            source.dopplerLevel = 0;
            return source;
        }

        public bool Play(string id, float gain = 0.55f, float pan = 0, float cooldown = 0.08f, int priority = 1, bool ui = false)
        {
            if (!ui && Time.timeScale <= 0) return false;
            if (voices == null) Initialize();
            if (voices == null || !clips.TryGetValue(id, out var clip)) return false;
            return PlayClip(id, clip, gain, pan, cooldown, priority, ui);
        }

        public bool PlayClip(string id, AudioClip clip, float gain = 0.55f, float pan = 0, float cooldown = 0.08f, int priority = 1, bool ui = false)
        {
            if ((!ui && Time.timeScale <= 0) || clip == null) return false;
            if (voices == null) Initialize();
            if (voices == null) return false;
            float now = Time.unscaledTime;
            if (lastPlayed.TryGetValue(id, out var last) && now-last < cooldown) return false;
            int slot = -1;
            for (int i = 0; i < voices.Length; i++) if (!voices[i].isPlaying && !suspended[i]) { slot = i; break; }
            if (slot < 0)
                for (int i = 0; i < voices.Length; i++)
                    if (priorities[i] < priority && (slot < 0 || priorities[i] < priorities[slot])) slot = i;
            if (slot < 0) return false;
            lastPlayed[id] = now;
            var source = voices[slot];
            source.Stop();
            suspended[slot] = false;
            source.clip = clip;
            source.pitch = 1;
            source.panStereo = Mathf.Clamp(pan, -0.3f, 0.3f);
            levels[slot] = volume * Mathf.Clamp01(gain);
            source.volume = levels[slot] * Fortal.Arena.FortalArenaSettings.Sfx;
            priorities[slot] = priority;
            uiVoice[slot] = ui;
            source.Play();
            UpdateMix();
            PlayedCount++;
            LastPlayed = id;
            return true;
        }

        public void StartCharge(int theme, float seconds)
        {
            if (Time.timeScale <= 0) return;
            if (voices == null) Initialize();
            if (charge == null || !clips.TryGetValue("charge_" + theme, out var clip)) return;
            charge.Stop();
            charge.clip = clip;
            charge.volume = volume * 0.48f;
            charge.pitch = Mathf.Clamp(clip.length / Mathf.Max(0.1f, seconds), 0.25f, 3f);
            charge.Play();
            UpdateMix();
            PlayedCount++;
            LastPlayed = "charge_" + theme;
        }
        public void StopCharge() { if (charge != null) charge.Stop(); }

        // Plays boss_big_charge once at its own length (it is cut to the 6 s wind-up); a missing clip is silent.
        public void StartBigCharge()
        {
            if (Time.timeScale <= 0) return;
            if (voices == null) Initialize();
            if (bigCharge == null || !clips.TryGetValue("boss_big_charge", out var clip)) return;
            bigCharge.Stop();
            bigCharge.clip = clip;
            bigCharge.pitch = 1;
            bigCharge.Play();
            UpdateMix();
            PlayedCount++;
            LastPlayed = "boss_big_charge";
        }
        public void StopBigCharge() { if (bigCharge != null) bigCharge.Stop(); }
        /// <summary>True when this theme has its own charge clip (Hydra = 8 got its set 2026-10-06; BossSlayerGameManager.SoundTheme falls back if a theme is missing).</summary>
        public bool HasTheme(int theme) { if (voices == null) Initialize(); return clips.ContainsKey("charge_" + theme); }
        public void Release(int theme, float pan) { StopCharge(); Play("release_" + theme, 0.68f, pan, 0.10f, 3); }
        public void Hold(float progress, float pan)
        {
            if (holdFrame != Time.frameCount) holdProgress = 0;
            holdFrame = Time.frameCount;
            if (progress >= holdProgress) { holdProgress = Mathf.Clamp01(progress); holdPan = pan; }
        }
        public void ObservePhase(int value)
        {
            if (value > phase) { Play("phase", 0.6f, 0, 1, 4); Score?.Roar(); }
            phase = value;
        }
        public void StopCombat()
        {
            if (voices != null) for (int i = 0; i < voices.Length; i++) if (!uiVoice[i]) { voices[i].Stop(); suspended[i] = false; }
            StopCharge();
            StopBigCharge();
            if (hold != null) hold.Stop();
            holdFrame = -1;
            holdLevel = 0;
            phase = 1;
            lastPlayed.Clear();
        }

        void LateUpdate()
        {
            if (voices == null) return;
            bool shouldPause = Time.timeScale <= 0;
            if (shouldPause != paused)
            {
                for (int i = 0; i < voices.Length; i++) if (!uiVoice[i])
                {
                    if (shouldPause) { suspended[i] = voices[i].isPlaying; if (suspended[i]) voices[i].Pause(); }
                    else { if (suspended[i]) voices[i].UnPause(); suspended[i] = false; }
                }
                if (shouldPause) { charge.Pause(); hold.Pause(); bigCharge.Pause(); } else { charge.UnPause(); hold.UnPause(); bigCharge.UnPause(); }
                paused = shouldPause;
            }
            if (paused) return;
            float target = holdFrame == Time.frameCount ? 0.1f + holdProgress * 0.15f : 0;
            holdLevel = Mathf.MoveTowards(holdLevel, target, Time.unscaledDeltaTime * 1.5f);
            if (holdLevel > 0.001f)
            {
                if (!hold.isPlaying && clips.TryGetValue("gem_hold", out var loop)) { hold.clip = loop; hold.Play(); }
                hold.volume = volume * holdLevel;
                hold.pitch = 0.9f + holdProgress * 0.8f;
                hold.panStereo = Mathf.Clamp(holdPan,-0.3f,0.3f);
            }
            else if (hold.isPlaying) hold.Stop();
            UpdateMix();
        }

        void UpdateMix()
        {
            // All bank clips are mastered to a 0.68 peak; bound simultaneous summed levels.
            float sum = charge != null && charge.isPlaying ? volume * 0.48f : 0;
            if (hold != null && hold.isPlaying) sum += volume * holdLevel;
            if (bigCharge != null && bigCharge.isPlaying) sum += volume * BigChargeGain;
            for (int i = 0; i < voices.Length; i++) if (voices[i].isPlaying) sum += levels[i];
            float headroom = Mathf.Min(1, 1.15f / Mathf.Max(0.001f, sum));
            headroom *= Fortal.Arena.FortalArenaSettings.Sfx;   // the web launcher's effects level (2026-11-12)
            for (int i = 0; i < voices.Length; i++) voices[i].volume = levels[i] * headroom;
            if (charge != null) charge.volume = volume * 0.48f * headroom;
            if (hold != null) hold.volume = volume * holdLevel * headroom;
            if (bigCharge != null) bigCharge.volume = volume * BigChargeGain * headroom;
        }

        public void WireButtons()
        {
            if (buttons.Count > 0) return;
            foreach (var button in Resources.FindObjectsOfTypeAll<Button>())
            {
                if (button.gameObject.scene != gameObject.scene) continue;
                string name = button.name.ToLowerInvariant();
                string cue = name.Contains("back") || name.Contains("restart") || name.Contains("exit") ? "ui_back" : "ui_click";
                UnityAction action = () => Play(cue, 0.4f, 0, 0.12f, 5, true);
                button.onClick.AddListener(action);
                buttons.Add((button, action));
                var hover = button.GetComponent<BossFightButtonAudio>() ?? button.gameObject.AddComponent<BossFightButtonAudio>();
                hover.Bind(this, button);
            }
        }
        void OnDisable() { StopCombat(); }
        void OnDestroy()
        {
            foreach (var pair in buttons) if (pair.button != null) pair.button.onClick.RemoveListener(pair.action);
            foreach (var listener in disabledListeners) if (listener != null) listener.enabled = true;
        }
    }
}
