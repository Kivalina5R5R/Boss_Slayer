using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace BossSlayer
{
    // 2026-11-25 "รูปฉากหลังจอกำแพงทั้ง3ฉากเอาไปทำ animation": the element walls (Fire, Water, Land) are 3.79:1, wider
    // than any video model makes, so only their middle - the 21:9 band around the portal (x 1576..6616 of 8192) - is a
    // quiet 5 s loop (MiniMax H3, the still band as first and last frame). It plays over the still wall in that band,
    // its left and right edges fading into the still (BossEdgeFeather), so the pillars stay the painted picture.
    // Files: StreamingAssets/BossBackgroundMovies/<wall art name>_Centre.mp4. Nothing shows until the video is ready,
    // and nothing at all when the file is missing.
    public sealed class BossWallCentreVideo : MonoBehaviour
    {
        const float BandLeft = 1576f / 8192f, BandRight = 6616f / 8192f, Feather = .12f;

        RawImage image;
        VideoPlayer player;

        // Where the loop is now (seconds), or -1 while it isn't playing. The Final Boss shockwaves follow its pulses.
        public double PlayTime => player != null && player.isPlaying && image != null && image.color.a > 0f ? player.time : -1;
        string url;
        Texture art;

        public static BossWallCentreVideo Attach(RawImage wall)
        {
            if (wall == null) return null;
            var layer = wall.rectTransform.Find("Wall Centre Video") as RectTransform;
            if (layer == null)
            {
                layer = new GameObject("Wall Centre Video", typeof(RectTransform), typeof(RawImage)).GetComponent<RectTransform>();
                layer.SetParent(wall.rectTransform, false);
            }
            layer.anchorMin = new Vector2(BandLeft, 0f);
            layer.anchorMax = new Vector2(BandRight, 1f);
            layer.offsetMin = layer.offsetMax = Vector2.zero;
            layer.SetAsFirstSibling();   // under the glow and particles (BossFireWall)
            if (!layer.TryGetComponent(out BossWallCentreVideo view)) view = layer.gameObject.AddComponent<BossWallCentreVideo>();
            view.Setup();
            return view;
        }

        void Setup()
        {
            if (image != null) return;
            image = GetComponent<RawImage>();
            image.raycastTarget = false;
            image.color = Color.clear;
            var shader = GameArt.Load<Shader>("Art/Shaders/BossEdgeFeather");
            if (shader != null && shader.isSupported)
            {
                image.material = new Material(shader) { name = "Wall Centre Feather", hideFlags = HideFlags.HideAndDontSave };
                image.material.SetFloat("_Feather", Feather);
            }
            player = gameObject.AddComponent<VideoPlayer>();
            player.playOnAwake = false;
            player.isLooping = true;
            player.renderMode = VideoRenderMode.APIOnly;
            player.source = VideoSource.Url;
            player.audioOutputMode = VideoAudioOutputMode.None;
            player.skipOnDrop = true;
            player.prepareCompleted += OnPrepared;
            player.errorReceived += (_, message) => { Debug.LogWarning("[BossWallCentreVideo] " + message, this); Hide(); };
        }

        // Plays the centre loop made for wallArt, or hides when there is none (any other wall picture).
        public void Show(Texture wallArt)
        {
            art = wallArt;
            // A hidden wall (e.g. during the boss select) cannot prepare a video: OnEnable starts it.
            if (!isActiveAndEnabled) { url = null; Hide(); return; }
            string path = wallArt == null ? null : Path.Combine(Application.streamingAssetsPath, "BossBackgroundMovies", wallArt.name + "_Centre.mp4");
            if (path == null || !File.Exists(path)) { Hide(); player?.Stop(); url = null; return; }
            string wanted = new System.Uri(path).AbsoluteUri;
            if (wanted == url && player.isPrepared)
            {
                OnPrepared(player);
                return;
            }
            url = wanted;
            Hide();
            player.Stop();
            player.url = url;
            player.Prepare();
        }

        void OnEnable()
        {
            if (player != null && art != null && url == null) Show(art);
        }

        void OnPrepared(VideoPlayer source)
        {
            if (image == null || source.url != url) return;
            image.texture = source.texture;
            image.color = Color.white;
            if (!source.isPlaying) source.Play();
        }

        void Hide()
        {
            if (image != null) image.color = Color.clear;
        }
    }
}
