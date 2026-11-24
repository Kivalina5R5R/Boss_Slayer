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

        RawImage image;
        VideoPlayer player;
        Texture still;
        Rect stillUv;

        // Puts the rune floor (still now, video when ready) on image, filling it without stretching.
        public static void Attach(RawImage image)
        {
            if (image == null) return;
            if (!image.TryGetComponent(out BossFloorMagicVideo video)) video = image.gameObject.AddComponent<BossFloorMagicVideo>();
            video.image = image;
            video.still = Resources.Load<Texture2D>(StillPath);
            video.stillUv = CoverUv(video.still, image.rectTransform);
            if (video.player == null || !video.player.isPrepared) video.ShowStill();
            if (video.isActiveAndEnabled) video.Begin();
        }

        void OnEnable() { if (image != null) Begin(); }

        // 2026-11-19: Pause, not Stop - Stop throws the decoder away, and preparing it again on the next show stalled the
        // screen change (player select <-> boss select). Paused, it is still prepared and resumes at once.
        void OnDisable()
        {
            if (player != null && player.isPrepared) player.Pause();
            ShowStill();
        }

        void Begin()
        {
            if (!Application.isPlaying) return;
            string path = Path.Combine(Application.streamingAssetsPath, FileName);
            if (!File.Exists(path)) return;
            if (player == null) player = CreatePlayer(new System.Uri(path).AbsoluteUri);
            if (player.isPrepared) { ShowVideo(); player.Play(); }
            else player.Prepare();
        }

        VideoPlayer CreatePlayer(string url)
        {
            var video = gameObject.AddComponent<VideoPlayer>();
            video.playOnAwake = false;
            video.isLooping = true;
            video.renderMode = VideoRenderMode.APIOnly;
            video.source = VideoSource.Url;
            video.url = url;
            video.audioOutputMode = VideoAudioOutputMode.None;
            video.skipOnDrop = true;
            video.prepareCompleted += OnPrepared;
            video.errorReceived += (_, message) => { Debug.LogWarning("[Boss floor magic] " + message); ShowStill(); };
            return video;
        }

        void OnPrepared(VideoPlayer prepared)
        {
            if (!isActiveAndEnabled) return;
            ShowVideo();
            prepared.Play();
        }

        void ShowVideo()
        {
            if (image == null || player.texture == null) return;
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
            float rectAspect = rect != null && rect.rect.height > 0 ? rect.rect.width / rect.rect.height : FloorDisplaySettings.Width / (float)FloorDisplaySettings.Height;
            float artAspect = texture.width / (float)texture.height;
            if (rectAspect >= artAspect)
            {
                float h = artAspect / rectAspect;
                return new Rect(0, (1 - h) / 2, 1, h);
            }
            float w = rectAspect / artAspect;
            return new Rect((1 - w) / 2, 0, w, 1);
        }
    }
}
