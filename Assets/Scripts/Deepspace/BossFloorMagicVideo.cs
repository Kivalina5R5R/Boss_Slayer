using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Deepspace
{
    // บาส 2026-11-18: the rune-circle floor (Resources/BossSlayer/PlayerSelectCards/Floor.png) comes alive with a faint
    // looping glow made with Higgsfield (Seedance 2.0 Mini, the still as first and last frame, so it loops). Boss Slayer's
    // floor player select and floor boss select both show it. It sits on their backdrop RawImage: the still shows until
    // the video is ready (and stays if the file is missing or fails), and the video stops while the screen is hidden.
    [RequireComponent(typeof(RawImage))]
    public sealed class BossFloorMagicVideo : MonoBehaviour
    {
        public const string FileName = "BossSlayerFloorMagic.mp4";
        const string StillPath = "BossSlayer/PlayerSelectCards/Floor";

        // 2026-11-24 "ตอนเปลี่ยนจาก select player มา select boss ฉากมันหดแล้วขยาย": the video is 2944x1262 (21:9), the still
        // 2944x1408 with the video's frame in its middle (the video trims the top and bottom). Each screen opens on the
        // still and swaps to its video when that is ready; framed on their own the two filled the floor differently, so
        // the floor visibly shrank then grew at the swap. The still is now cut to the video's frame first, so the swap
        // is seamless. (Floor.png was also imported shrunk to 2048x1024, which skewed its shape; it is full size now.)
        const float VideoAspect = 2944f / 1262f;

        // 2026-11-24 "ตอนเปลี่ยนฉากจาก Select player ไป select boss animation พื้นหลังของพื้น ต่อกันไปเลย ไม่ต้องโหลดใหม่":
        // both screens now share ONE video player. It was one per screen, so the boss select's own player prepared the
        // video again and started it from its first frame (the still showing meanwhile): the glow jumped back to the
        // start at every screen change. Now the screen that opens shows the same, already playing video, at the frame it
        // is on. The video pauses only when no screen shows it (Pause keeps its place and its decoder).
        static VideoPlayer shared;
        static bool preparing;
        static readonly System.Collections.Generic.List<BossFloorMagicVideo> showing = new();

        RawImage image;
        Texture still;
        Rect stillUv;

        // Puts the rune floor (still now, video when ready) on image, filling it without stretching.
        public static void Attach(RawImage image)
        {
            if (image == null) return;
            if (!image.TryGetComponent(out BossFloorMagicVideo video)) video = image.gameObject.AddComponent<BossFloorMagicVideo>();
            video.image = image;
            video.still = Resources.Load<Texture2D>(StillPath);
            video.stillUv = StillUv(video.still, image.rectTransform);
            if (shared == null || !shared.isPrepared) video.ShowStill();
            if (video.isActiveAndEnabled) video.Begin();
        }

        void OnEnable() { if (image != null) Begin(); }

        void OnDisable()
        {
            showing.Remove(this);
            // 2026-11-19: Pause, not Stop - Stop throws the decoder away and preparing it again stalled the screen change.
            // Only when no other screen still shows it: the screen change hides one and shows the other in the same frame.
            if (showing.Count == 0 && shared != null && shared.isPrepared) shared.Pause();
            ShowStill();
        }

        void Begin()
        {
            if (!Application.isPlaying) return;
            string path = Path.Combine(Application.streamingAssetsPath, FileName);
            if (!File.Exists(path)) return;
            if (!showing.Contains(this)) showing.Add(this);
            if (shared == null) { preparing = false; shared = CreatePlayer(new System.Uri(path).AbsoluteUri); }
            if (shared.isPrepared) { ShowVideo(); if (!shared.isPlaying) shared.Play(); }
            else if (!preparing) { preparing = true; shared.Prepare(); }   // VideoPlayer has no isPreparing: tracked here
        }

        static VideoPlayer CreatePlayer(string url)
        {
            // Its own object, so it outlives either screen's backdrop (play-mode object: gone when play stops; if the scene
            // reloads, `shared` reads null and a new one is made).
            var host = new GameObject("Boss Floor Magic Video (shared)");
            var video = host.AddComponent<VideoPlayer>();
            video.playOnAwake = false;
            video.isLooping = true;
            video.renderMode = VideoRenderMode.APIOnly;
            video.source = VideoSource.Url;
            video.url = url;
            video.audioOutputMode = VideoAudioOutputMode.None;
            video.skipOnDrop = true;
            video.prepareCompleted += OnPrepared;
            video.errorReceived += (_, message) =>
            {
                preparing = false;
                Debug.LogWarning("[Boss floor magic] " + message);
                foreach (var screen in showing) if (screen != null) screen.ShowStill();
            };
            return video;
        }

        static void OnPrepared(VideoPlayer prepared)
        {
            preparing = false;
            if (showing.Count == 0) return;   // prepared, and waits paused for the next screen
            foreach (var screen in showing) if (screen != null) screen.ShowVideo();
            prepared.Play();
        }

        void ShowVideo()
        {
            var player = shared;
            if (image == null || player == null || player.texture == null) return;
            image.texture = player.texture;
            image.uvRect = CoverUv(player.texture, image.rectTransform);
            image.color = Color.white;
        }

        void ShowStill()
        {
            if (image == null || still == null) return;
            image.texture = still;
            image.uvRect = stillUv;
            image.color = Color.white;
        }

        // Fills the rect without stretching the picture: crops its top/bottom (or sides) instead.
        public static Rect CoverUv(Texture texture, RectTransform rect)
        {
            if (texture == null || texture.height <= 0) return new Rect(0, 0, 1, 1);
            return Cover(texture.width / (float)texture.height, RectAspect(rect));
        }

        // The still framed like the video: first cut to the video's frame (centred), then filled like the video.
        static Rect StillUv(Texture texture, RectTransform rect)
        {
            if (texture == null || texture.height <= 0) return new Rect(0, 0, 1, 1);
            float stillAspect = texture.width / (float)texture.height;
            var frame = Cover(stillAspect, VideoAspect);          // the video's part of the still
            var fill = Cover(VideoAspect, RectAspect(rect));      // the part of the video the floor shows
            return new Rect(frame.x + fill.x * frame.width, frame.y + fill.y * frame.height, fill.width * frame.width, fill.height * frame.height);
        }

        static float RectAspect(RectTransform rect) =>
            rect != null && rect.rect.height > 0 ? rect.rect.width / rect.rect.height : FloorDisplaySettings.Width / (float)FloorDisplaySettings.Height;

        // The centred part (as a uv rect) of a picture with aspect artAspect that has aspect viewAspect.
        static Rect Cover(float artAspect, float viewAspect)
        {
            if (viewAspect >= artAspect)
            {
                float h = artAspect / viewAspect;
                return new Rect(0, (1 - h) / 2, 1, h);
            }
            float w = viewAspect / artAspect;
            return new Rect((1 - w) / 2, 0, w, 1);
        }
    }
}
