using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace BossSlayer
{
    // 2026-11-19: "เปลี่ยนเอา transition ฟันดาบออก เปลี่ยนเป็นทั้ง logo และ particle ถล่มลงเหมือนหินแตก ... ตอนถล่มก็เห็น
    // อีกฉากข้างหลัง". Replaces BossWallSlash: the wall's Start screen (logo, embers, everything on it) cracks like stone
    // from the middle out while the wall shakes, then collapses piece by piece in puffs of grey dust, like the boss
    // statues (BossStatueShatter), showing the boss select behind. ("ไม่สวยเลย มันแตกเป็นไฟ" - the first, glowing lava
    // cracks were replaced by plain stone ones.)
    // The screen is photographed once (the wall camera into a texture) and cut into the pieces listed in
    // Resources/BossSlayer/Transition/Shards.json (a Voronoi layout, 0..1 of the wall, y up). Cracks.png draws the same
    // layout as thin dark fissures with a pale chipped lip; each piece carries its part. Removes itself at the end.
    public sealed class BossWallCrumble : MonoBehaviour
    {
        const string ShardsPath = "Art/UI/Transition/Shards", CracksPath = "Art/UI/Transition/Cracks";
        const string SmokePath = "Art/UI/Transition/Smoke", DustPath = "Art/UI/Transition/Dust";
        // Timeline (seconds): the cracks run out from the middle, a short hold, then the pieces collapse one after another
        // from the middle out. 2026-11-19: "มันดูร่วงแบบพร้อมกันเกินไป" - the collapse is spread over SpreadSeconds (was .7)
        // with up to Stagger of extra random wait per piece, so they come down a few at a time.
        const float CrackSeconds = .6f, HoldSeconds = .25f, SpreadSeconds = 1.7f, Stagger = .6f, FallSeconds = 1.3f;
        // From the first crack until the last piece is gone (the floor's boss cards come in after it).
        public const float Duration = CrackSeconds + HoldSeconds + SpreadSeconds + Stagger + .15f + FallSeconds;
        // Shake from the first crack until the collapse ends, as a fraction of the wall's height.
        const float ShakeAmount = .01f, ShakeSpeed = 28f;
        // How the pieces fall: gravity, a small push out of the middle (wall heights per second) and a slow turn.
        const float Gravity = 2.4f, Push = .12f, MaxSpin = 35f;
        // Where a piece breaks away: billowing grey smoke and gritty dust (BossSmokeGraphic); sizes in wall heights.
        static readonly Color SmokeColor = new Color32(170, 178, 172, 255), DustColor = new Color32(196, 200, 190, 255);

        [Serializable] class ShardList { public Shard[] shards; }
        [Serializable] class Shard { public Point[] points; }
        [Serializable] class Point { public float x, y; }

        sealed class Piece
        {
            public RectTransform Rect;
            public UiShardGraphic Cracks;
            public Vector2 Home, Velocity;
            public float CrackAt, Delay, Spin, Size;
            public bool Puffed;
        }

        RectTransform root;
        RenderTexture photo;
        BossSmokeGraphic smoke, dust;
        readonly List<Piece> pieces = new List<Piece>();
        float age, width, height;

        // Breaks whatever `camera` shows over the full-wall panel `wall`. Does nothing without the shard layout.
        public static void Play(RectTransform wall, Camera camera)
        {
            if (wall == null || camera == null) return;
            var layout = GameArt.Load<TextAsset>(ShardsPath);
            if (layout == null) { Debug.LogWarning("[Boss wall crumble] Missing Resources/" + ShardsPath); return; }
            var go = new GameObject("Wall Crumble Transition", typeof(RectTransform)) { layer = wall.gameObject.layer };
            var rect = (RectTransform)go.transform;
            rect.SetParent(wall, false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            go.AddComponent<BossWallCrumble>().Build(camera, JsonUtility.FromJson<ShardList>(layout.text));
        }

        void Build(Camera camera, ShardList layout)
        {
            root = (RectTransform)transform;
            var r = root.rect;
            width = r.width; height = r.height;
            photo = Photograph(camera);
            var photoUv = ViewportOf(camera);
            var cracks = GameArt.Load<Texture2D>(CracksPath);
            var centre = new Vector2(.5f, .52f);

            foreach (var shard in layout.shards)
            {
                if (shard.points == null || shard.points.Length < 3) continue;
                Vector2 mid = Vector2.zero;
                foreach (var p in shard.points) mid += new Vector2(p.x, p.y);
                mid /= shard.points.Length;
                var homeLocal = new Vector2(r.xMin + mid.x * width, r.yMin + mid.y * height);

                var corners = new Vector2[shard.points.Length];
                var wallUv = new Vector2[shard.points.Length];
                var shotUv = new Vector2[shard.points.Length];
                for (int i = 0; i < corners.Length; i++)
                {
                    var n = new Vector2(shard.points[i].x, shard.points[i].y);
                    corners[i] = new Vector2(r.xMin + n.x * width, r.yMin + n.y * height) - homeLocal;
                    wallUv[i] = n;
                    shotUv[i] = new Vector2(Mathf.Lerp(photoUv.xMin, photoUv.xMax, n.x), Mathf.Lerp(photoUv.yMin, photoUv.yMax, n.y));
                }

                var piece = NewChild<UiShardGraphic>("Piece", root);
                piece.raycastTarget = false;
                piece.Set(photo, corners, shotUv);
                var crackLayer = NewChild<UiShardGraphic>("Cracks", piece.rectTransform);
                crackLayer.raycastTarget = false;
                crackLayer.Set(cracks, corners, wallUv);
                crackLayer.color = new Color(1f, 1f, 1f, 0f);
                crackLayer.enabled = cracks != null;

                float area = 0f;
                for (int i = 0; i < corners.Length; i++) area += corners[i].x * corners[(i + 1) % corners.Length].y - corners[(i + 1) % corners.Length].x * corners[i].y;
                var away = new Vector2((mid.x - centre.x) * width / height, mid.y - centre.y);   // in wall heights
                float reach = Mathf.Clamp01(away.magnitude / 1.9f);   // 0 in the middle, 1 at the far ends
                piece.rectTransform.anchoredPosition = homeLocal;
                pieces.Add(new Piece
                {
                    Rect = piece.rectTransform, Cracks = crackLayer, Home = homeLocal,
                    CrackAt = reach * CrackSeconds * .85f,
                    // middle first, and lower pieces a little before the ones above them (it collapses, not explodes)
                    Delay = CrackSeconds + HoldSeconds + reach * SpreadSeconds + mid.y * .25f + UnityEngine.Random.Range(0f, Stagger),
                    Size = Mathf.Sqrt(Mathf.Abs(area) * .5f) / height,   // about the piece's width, in wall heights
                    Velocity = away.normalized * Push * UnityEngine.Random.Range(.4f, 1f) + new Vector2(0f, UnityEngine.Random.Range(-.1f, .08f)),
                    Spin = UnityEngine.Random.Range(-MaxSpin, MaxSpin),
                });
            }
            smoke = Effect("Smoke", SmokePath, 1, 1, SmokeColor);
            dust = Effect("Dust", DustPath, 4, 4, DustColor);
            transform.SetAsLastSibling();
        }

        // A full-size smoke layer over the pieces.
        BossSmokeGraphic Effect(string name, string texturePath, int columns, int rows, Color tint)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = gameObject.layer };
            var rect = (RectTransform)go.transform;
            rect.SetParent(root, false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            var effect = go.AddComponent<BossSmokeGraphic>();
            effect.raycastTarget = false;
            effect.color = tint;
            effect.Use(GameArt.Load<Texture2D>(texturePath), columns, rows);
            return effect;
        }

        // The wall as the camera sees it right now, before the Start screen is hidden.
        static RenderTexture Photograph(Camera camera)
        {
            Canvas.ForceUpdateCanvases();
            int w = Mathf.Max(16, camera.pixelWidth), h = Mathf.Max(16, camera.pixelHeight);
            var shot = new RenderTexture(w, h, 24, RenderTextureFormat.Default) { name = "Boss Wall Crumble Photo" };
            var request = new RenderPipeline.StandardRequest { destination = shot };
            if (RenderPipeline.SupportsRenderRequest(camera, request)) RenderPipeline.SubmitRenderRequest(camera, request);
            else
            {
                var target = camera.targetTexture;
                camera.targetTexture = shot;
                camera.Render();
                camera.targetTexture = target;
            }
            return shot;
        }

        // Where this panel sits in the camera's picture (0..1), so the pieces take their own part of the photo.
        Rect ViewportOf(Camera camera)
        {
            var corners = new Vector3[4];
            root.GetWorldCorners(corners);
            Vector2 a = camera.WorldToViewportPoint(corners[0]), b = camera.WorldToViewportPoint(corners[2]);
            return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
        }

        static T NewChild<T>(string name, RectTransform parent) where T : Component
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = parent.gameObject.layer };
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = Vector2.zero;
            return typeof(T) == typeof(RectTransform) ? rect as T : go.AddComponent<T>();
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            age += dt;
            float shake = Mathf.Clamp01(age / .2f) * (1f - Mathf.Clamp01((age - (Duration - FallSeconds * .5f)) / .6f));
            var jolt = new Vector2((Mathf.PerlinNoise(age * ShakeSpeed, 0f) - .5f) * 2f, (Mathf.PerlinNoise(0f, age * ShakeSpeed) - .5f) * 2f)
                * ShakeAmount * height * shake;

            bool any = false;
            foreach (var p in pieces)
            {
                float fall = age - p.Delay;
                // the crack shows on a piece as the break reaches it, and stays on it as it falls
                p.Cracks.color = new Color(1f, 1f, 1f, Mathf.Clamp01((age - p.CrackAt) / .12f));
                if (fall <= 0f) { p.Rect.anchoredPosition = p.Home + jolt; any = true; continue; }
                if (!p.Puffed) { p.Puffed = true; Billow(p); }
                p.Velocity.y -= Gravity * dt;
                p.Rect.anchoredPosition += p.Velocity * height * dt;
                p.Rect.localEulerAngles += new Vector3(0f, 0f, p.Spin * dt);
                if (p.Rect.anchoredPosition.y > root.rect.yMin - height * .6f) any = true;
            }
            if (smoke.Busy || dust.Busy) any = true;
            if (!any || age > Duration + 2f) Destroy(gameObject);
        }

        // Smoke and dust burst out where a piece breaks away, more for a bigger piece.
        void Billow(Piece p)
        {
            var at = p.Rect.anchoredPosition;
            float size = Mathf.Clamp(p.Size, .08f, .6f);
            smoke.Burst(at, 2 + Mathf.RoundToInt(size * 8f), size * .45f * height, size * .9f * height, .18f * height, 1.6f, .55f);
            dust.Burst(at, 3 + Mathf.RoundToInt(size * 10f), size * .5f * height, size * .55f * height, .35f * height, .9f, .8f);
            smoke.transform.SetAsLastSibling(); dust.transform.SetAsLastSibling();
        }

        // The boss select puts itself on top when it is shown or refreshed: the falling pieces stay over it.
        void LateUpdate()
        {
            if (transform.GetSiblingIndex() != transform.parent.childCount - 1) transform.SetAsLastSibling();
        }

        void OnDestroy()
        {
            if (photo != null) { photo.Release(); Destroy(photo); }
        }
    }
}
