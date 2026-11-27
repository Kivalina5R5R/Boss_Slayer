using UnityEngine;
using UnityEngine.UI;

namespace BossSlayer
{
    // 2026-11-26: keeps the Final Boss shockwave shader (BossShockwave.shader) on the centre video's clock, so its rings
    // are born exactly when the video's eye pulses. While the video isn't playing it runs on its own clock instead.
    public sealed class BossShockwaveSync : MonoBehaviour
    {
        static readonly int VideoTime = Shader.PropertyToID("_VideoTime"), Pulses = Shader.PropertyToID("_Pulses");

        RawImage image;
        BossWallCentreVideo video;

        // layer: the shockwave overlay; centreVideo: the wall's centre loop whose pulses the rings carry on.
        public static void Attach(Component layer, BossWallCentreVideo centreVideo)
        {
            if (layer == null) return;
            if (!layer.TryGetComponent(out BossShockwaveSync sync)) sync = layer.gameObject.AddComponent<BossShockwaveSync>();
            sync.image = layer.GetComponent<RawImage>();
            sync.video = centreVideo;
        }

        void Update()
        {
            var material = image != null ? image.material : null;
            if (material == null) return;
            double time = video != null ? video.PlayTime : -1;
            if (time < 0)
            {
                float loop = Mathf.Max(.01f, material.GetVector(Pulses).w);
                time = Time.unscaledTime % loop;
            }
            material.SetFloat(VideoTime, (float)time);
        }
    }
}
