using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace BossSlayer
{
    // 2026-11-19: "ถ้าตีบอสตัวนั้นเสร็จกลับมาหน้าเลือกบอส แล้วรูปปั้นตัวบอสที่เราตีเสร็จไปแตก" - on the boss select wall, the
    // statue of a boss the players have just beaten cracks and shatters into rubble; beaten bosses' statues stay rubble
    // for the rest of the session. One Higgsfield clip (Seedance 2.0) broke all nine statues at once on a grid of
    // SelectBossBG_v7 crops; each statue was cut out, AI-upscaled and blended into its own crop of the hall (soft edges,
    // the first half second fades from the still), so the clip sits on the picture without a seam.
    // Clips: StreamingAssets/BossSlayerShatter/<Name>.mp4. Rubble (the clip's last frame):
    // Resources/BossSlayer/SelectBoss/Shatter/<Name>.png. Lives in the hall picture (BossHallSpace view), so it zooms
    // and floats with the wall camera.
    public sealed class BossStatueShatter : MonoBehaviour
    {
        const string ClipFolder = "BossSlayerShatter", RubbleFolder = "Art/UI/SelectBoss/Shatter/";
        // Seconds after the boss select shows before a fresh statue breaks, so players see it whole first.
        const float BreakDelay = .8f;
        // When in the clip the statue bursts and falls (it cracks before that), and how long the camera shakes from then.
        const float CollapseAt = 2.1f, CollapseShakeSeconds = 1.1f;

        // 0..1 while a statue is collapsing (strongest as it bursts, fading out); the wall camera shakes by it
        // (BossSelectionPanel.WallZoom.cs).
        public float ShakeStrength { get; private set; }

        // The boss kind whose statue is breaking now (waiting to start included), or 0; the wall camera eases a little
        // closer to it meanwhile (BossSelectionPanel.WallZoom.cs).
        public int BreakingKind { get; private set; }

        // By boss kind 1..9 (Demon, Fish, Dragon, Jellyfish, Golem, Wraith, Sentinel, Serpent, Hydra): the file name and
        // the statue's crop in SelectBossBG_v7 as 0..1 of the picture (bottom = 0). Remake both if the picture changes.
        static readonly string[] Names = { "Demon", "Fish", "Dragon", "Jellyfish", "Golem", "Wraith", "Sentinel", "Serpent", "Hydra" };
        static readonly Rect[] Crops =
        {
            new Rect(.1321f, .3340f, .0867f, .6387f), new Rect(.2239f, .2520f, .0631f, .4629f), new Rect(.4345f, 0f, .1359f, 1f),
            new Rect(.2866f, .2432f, .0755f, .5566f), new Rect(.3545f, .1787f, .0777f, .5723f), new Rect(.5685f, .1973f, .0709f, .5234f),
            new Rect(.6436f, .2217f, .0709f, .5234f), new Rect(.7036f, .2080f, .0867f, .6387f), new Rect(.7873f, .3281f, .0912f, .6719f),
        };

        // 2026-11-25 "ตัวของปลา ยังเห็นจุดที่โผล่ออกมาอยู่": a statue that reaches past its crop (the Fish's lure and
        // antenna) left that part standing beside the rubble. A cleaned patch of the hall (<Name>_Patch.png, the
        // statue's leftovers painted out from the pillar and haze above and below) covers it, fading in during the
        // collapse shake. Box as 0..1 of the picture (bottom = 0); an empty box means no patch.
        static readonly Rect[] PatchBoxes =
        {
            Rect.zero, new Rect(.2774f, .4082f, .01858f, .24805f), Rect.zero, Rect.zero, Rect.zero,
            Rect.zero, Rect.zero, Rect.zero, Rect.zero,
        };
        const float PatchFadeFrom = .3f, PatchFadeSeconds = .8f;   // seconds after CollapseAt

        RectTransform layer;
        Func<Rect> view;
        readonly RawImage[] pieces = new RawImage[Names.Length];
        readonly RawImage[] patches = new RawImage[Names.Length];
        readonly VideoPlayer[] players = new VideoPlayer[Names.Length];
        readonly float[] breakAt = new float[Names.Length];
        readonly bool[] preparing = new bool[Names.Length];
        // 2026-11-25 "ใส่เสียงตอนหุ่นถล่มด้วย": whether statue i's collapse sound has played in this break.
        readonly bool[] collapseHeard = new bool[Names.Length];
        readonly bool[] slamHeard = new bool[Names.Length];
        // statue_collapse is 1.6 s long: started this early it has finished when the shake (and the slam) begins.
        const float CollapseLeadIn = 1.5f;
        readonly HashSet<int> broken = new HashSet<int>();   // kinds whose statue has broken (or is breaking) this session

        // The statue's box in SelectBossBG_v7 (0..1, bottom = 0) for boss kind 1..9 (BossStatueAura uses it too).
        public static Rect StatueBox(int kind) => kind >= 1 && kind <= Crops.Length ? Crops[kind - 1] : new Rect(0, 0, 0, 0);

        // 2026-11-27 "รูปปั้นอันไหนที่พังก็ให้เห็นพังด้วย": for other screens showing the same hall (the final boss
        // intro): boss kind 1..9's rubble picture, and its cleaned patch with the patch's box (null / empty when none).
        public static Texture2D RubbleArt(int kind) => kind >= 1 && kind <= Names.Length ? GameArt.Load<Texture2D>(RubbleFolder + Names[kind - 1]) : null;
        public static Rect PatchBox(int kind) => kind >= 1 && kind <= PatchBoxes.Length ? PatchBoxes[kind - 1] : Rect.zero;
        public static Texture2D PatchArt(int kind) => PatchBox(kind).width > 0f ? GameArt.Load<Texture2D>(RubbleFolder + Names[kind - 1] + "_Patch") : null;

        // This boss's statue has broken (or is breaking) this session.
        public bool IsBroken(int kind) => broken.Contains(kind);

        public static BossStatueShatter Attach(RectTransform parent, string name, Func<Rect> hallView)
        {
            var layer = BossHallSpace.Layer(parent, name);
            if (layer == null) return null;
            if (!layer.TryGetComponent(out BossStatueShatter shatter)) shatter = layer.gameObject.AddComponent<BossStatueShatter>();
            shatter.layer = layer; shatter.view = hallView;
            for (int i = 0; i < Names.Length; i++)
            {
                if (shatter.pieces[i] == null) shatter.pieces[i] = BossHallSpace.Sprite(layer, "Statue " + Names[i]);
                if (shatter.players[i] == null && !shatter.broken.Contains(i + 1)) shatter.pieces[i].enabled = false;
            }
            // after every piece, so a patch draws over its neighbour's crop too
            for (int i = 0; i < Names.Length; i++)
            {
                if (PatchBoxes[i].width <= 0f || shatter.patches[i] != null) continue;
                var patch = BossHallSpace.Sprite(layer, "Statue " + Names[i] + " Patch");
                patch.texture = GameArt.Load<Texture2D>(RubbleFolder + Names[i] + "_Patch");
                patch.enabled = false;
                shatter.patches[i] = patch;
            }
            return shatter;
        }

        // Called with the session's beaten bosses whenever the boss select shows them. A newly beaten one breaks (after
        // BreakDelay); earlier ones show as rubble; an empty set (new session) puts every statue back.
        public void Show(HashSet<int> cleared)
        {
            for (int kind = 1; kind <= Names.Length; kind++)
            {
                int i = kind - 1;
                if (!cleared.Contains(kind))
                {
                    if (broken.Remove(kind)) Restore(i);
                    continue;
                }
                if (broken.Contains(kind)) { if (players[i] == null) ShowRubble(i); continue; }
                broken.Add(kind);
                breakAt[i] = Time.unscaledTime + BreakDelay;
                PrepareBreak(i);
            }
        }

        void PrepareBreak(int i)
        {
            string path = Path.Combine(Application.streamingAssetsPath, ClipFolder, Names[i] + ".mp4");
            if (!Application.isPlaying || !File.Exists(path)) { ShowRubble(i); return; }
            var player = gameObject.AddComponent<VideoPlayer>();
            player.playOnAwake = false;
            player.isLooping = false;
            player.renderMode = VideoRenderMode.APIOnly;
            player.source = VideoSource.Url;
            player.url = new Uri(path).AbsoluteUri;
            player.audioOutputMode = VideoAudioOutputMode.None;
            player.skipOnDrop = true;
            int index = i;
            player.loopPointReached += _ => FinishBreak(index);
            player.errorReceived += (_, message) => { Debug.LogWarning("[Boss statue shatter] " + message); FinishBreak(index); };
            players[i] = player;
            // 2026-11-19: Show can run while the wall is still hidden, and a disabled VideoPlayer cannot Prepare
            // ("Cannot Prepare a disabled VideoPlayer": the statue then never broke). Update prepares it once shown.
            preparing[i] = false;
        }

        void Update()
        {
            float shake = 0f;
            int breaking = 0;
            for (int i = 0; i < Names.Length; i++)
            {
                var player = players[i];
                if (player == null) continue;
                breaking = i + 1;
                if (!preparing[i]) { preparing[i] = true; breakAt[i] = Mathf.Max(breakAt[i], Time.unscaledTime + BreakDelay); player.Prepare(); }
                if (!player.isPlaying && player.isPrepared && Time.unscaledTime >= breakAt[i])
                {
                    pieces[i].texture = player.texture;
                    pieces[i].color = Color.white;
                    pieces[i].enabled = true;
                    player.Play();
                    collapseHeard[i] = false; slamHeard[i] = false;
                    BossFightAudio.PlayUi("statue_crumble", .65f, .3f, 3);   // 2026-11-25: a beaten boss's statue cracking
                }
                if (!player.isPlaying) continue;
                float sinceCollapse = (float)player.time - CollapseAt;
                // 2026-11-25 "ตอนกล้องสั่นรอบที่หินร่วงมาหมดแล้วต้องมีเสียงตึงเหมือนอะไรถล่มดังๆ": the three crashes of the
                // pieces coming down lead in so they end as the shake starts, and the shake itself lands with one big slam.
                if (sinceCollapse >= -CollapseLeadIn && !collapseHeard[i]) { collapseHeard[i] = true; BossFightAudio.PlayUi("statue_collapse", .7f, .3f, 3); }
                if (sinceCollapse >= 0f && !slamHeard[i]) { slamHeard[i] = true; BossFightAudio.PlayUi("statue_slam", .95f, .3f, 4); }
                SetPatch(i, (sinceCollapse - PatchFadeFrom) / PatchFadeSeconds);
                if (sinceCollapse >= 0f && sinceCollapse < CollapseShakeSeconds) shake = Mathf.Max(shake, 1f - sinceCollapse / CollapseShakeSeconds);
            }
            ShakeStrength = shake;
            BreakingKind = breaking;
        }

        // After the wall camera has set this frame's view (BossSelectionPanel's Update).
        void LateUpdate()
        {
            var shown = view != null ? view() : new Rect(0, 0, 1, 1);
            for (int i = 0; i < Names.Length; i++)
            {
                if (!pieces[i].enabled) continue;
                ClampEdges(pieces[i].texture);
                Place(pieces[i].rectTransform, Crops[i], shown);
            }
            for (int i = 0; i < Names.Length; i++)
            {
                if (patches[i] == null || !patches[i].enabled) continue;
                ClampEdges(patches[i].texture);
                Place(patches[i].rectTransform, PatchBoxes[i], shown);
            }
        }

        // 2026-11-25 "ตอนกล้องสั่นภาพมันเห็นเส้นขาวๆ": a Repeat-wrapped texture drawn at sub-pixel spots during the shake
        // blends its edge with the opposite edge, which showed as thin light lines round the crop. The video's texture
        // is made at runtime (and may be remade), so it is clamped here; the rubble PNGs are Clamp in their import settings.
        static void ClampEdges(Texture texture)
        {
            if (texture != null && texture.wrapMode != TextureWrapMode.Clamp) texture.wrapMode = TextureWrapMode.Clamp;
        }

        // The clip ended (or failed): its last frame stays as the rubble picture.
        void FinishBreak(int i)
        {
            if (players[i] != null) { Destroy(players[i]); players[i] = null; }
            if (broken.Contains(i + 1)) ShowRubble(i);
        }

        void ShowRubble(int i)
        {
            var rubble = GameArt.Load<Texture2D>(RubbleFolder + Names[i]);
            pieces[i].texture = rubble;
            pieces[i].color = Color.white;
            pieces[i].enabled = rubble != null;
            SetPatch(i, 1f);
        }

        // Shows statue i's patch (if it has one) at this opacity, 0 hiding it.
        void SetPatch(int i, float alpha)
        {
            var patch = patches[i];
            if (patch == null || patch.texture == null) return;
            alpha = Mathf.Clamp01(alpha);
            patch.color = new Color(1f, 1f, 1f, alpha);
            patch.enabled = alpha > 0f;
        }

        void Restore(int i)
        {
            if (players[i] != null) { Destroy(players[i]); players[i] = null; }
            pieces[i].enabled = false;
            SetPatch(i, 0f);
        }

        // A picture crop (0..1 of the picture) on screen through the hall view.
        static void Place(RectTransform rect, Rect crop, Rect shown)
        {
            rect.anchorMin = new Vector2((crop.xMin - shown.x) / shown.width, (crop.yMin - shown.y) / shown.height);
            rect.anchorMax = new Vector2((crop.xMax - shown.x) / shown.width, (crop.yMax - shown.y) / shown.height);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.localEulerAngles = Vector3.zero;
        }

        void OnDisable()
        {
            // A clip cut off by the screen closing ends as rubble, like a finished one (one not started yet waits).
            for (int i = 0; i < Names.Length; i++) if (players[i] != null && preparing[i]) FinishBreak(i);
            ShakeStrength = 0f; BreakingKind = 0;
        }
    }
}
