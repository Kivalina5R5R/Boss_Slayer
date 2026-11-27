using UnityEngine;
using UnityEngine.UI;

namespace BossSlayer
{
    // 2026-11-26 "ของหินช่วยทำ ให้มีหินร่วงมา จากตรงที่อื่นๆด้วย ตัดรูปเอา": over the Earth wall, rocks cut from the wall
    // picture itself (Art/BG/Land/Rock0..5: its glowing-edge cubes and chips) keep breaking off the top edge anywhere
    // along the wall and tumble down, so the fall is not only in the centre video. Lives on a full-size layer of the
    // wall, so it moves with the wall; shown only while the wall shows the Earth picture.
    public sealed class BossWallRockfall : MonoBehaviour
    {
        const string EarthWallName = "BGEarthScene", RockFolder = "Art/BG/Land/Rock";
        const int RockArtCount = 6, RockCount = 16;
        // Wall pixels (of 2160 high): sizes, gravity, the longest wait before a rock breaks off.
        const float SizeMin = 40f, SizeMax = 170f, Gravity = 1500f, WaitMax = 5f, FadeFrom = .78f;

        struct Rock
        {
            public RectTransform Rect;
            public RawImage Image;
            public float Wait, X, Y, Fall, Size, Aspect, Spin, Angle, Drift, Floor;
        }

        Texture2D[] rockArt;
        Rock[] rocks;

        public static BossWallRockfall Attach(RawImage wall)
        {
            if (wall == null) return null;
            var layer = BossHallSpace.Layer(wall.rectTransform, "Wall Rockfall");
            if (!layer.TryGetComponent(out BossWallRockfall fall)) fall = layer.gameObject.AddComponent<BossWallRockfall>();
            fall.Build(layer);
            return fall;
        }

        // Shows the rockfall over the Earth wall only.
        public void Show(Texture wallArt)
        {
            bool on = wallArt != null && wallArt.name == EarthWallName && rockArt.Length > 0;
            if (gameObject.activeSelf == on) return;
            gameObject.SetActive(on);
            if (on) Scatter();
        }

        void Build(RectTransform layer)
        {
            if (rocks != null) return;
            var found = new System.Collections.Generic.List<Texture2D>();
            for (int i = 0; i < RockArtCount; i++)
            {
                var art = GameArt.Load<Texture2D>(RockFolder + i);
                if (art != null) found.Add(art);
            }
            rockArt = found.ToArray();
            rocks = new Rock[RockCount];
            for (int i = 0; i < RockCount; i++)
            {
                var image = BossHallSpace.Sprite(layer, "Rock " + i);
                rocks[i] = new Rock { Rect = image.rectTransform, Image = image };
            }
            Scatter();
        }

        // Every rock waits a different time before its first fall, so they do not all drop together.
        void Scatter()
        {
            if (rocks == null) return;
            for (int i = 0; i < rocks.Length; i++) Respawn(ref rocks[i], Random.Range(0f, WaitMax));
        }

        void Respawn(ref Rock rock, float wait)
        {
            rock.Wait = wait;
            rock.X = Random.value;
            // Small chips are common, big blocks rare.
            rock.Size = Mathf.Lerp(SizeMin, SizeMax, Random.value * Random.value);
            rock.Y = BossHallSpace.DesignH + rock.Size;
            rock.Fall = Random.Range(0f, 180f);
            rock.Spin = Random.Range(-160f, 160f);
            rock.Angle = Random.Range(0f, 360f);
            rock.Drift = Random.Range(-40f, 40f);
            // Some land on the floor ledges, most fall out of view.
            rock.Floor = Random.value < .35f ? Random.Range(250f, 700f) : -SizeMax;
            if (rockArt.Length == 0) return;
            var art = rockArt[Random.Range(0, rockArt.Length)];
            rock.Image.texture = art;
            rock.Aspect = (float)art.width / Mathf.Max(1, art.height);
            rock.Image.color = Color.clear;
        }

        void Update()
        {
            if (rocks == null || rockArt.Length == 0) return;
            float dt = Time.unscaledDeltaTime;
            var area = ((RectTransform)transform).rect;
            float unit = area.height / BossHallSpace.DesignH;
            for (int i = 0; i < rocks.Length; i++)
            {
                ref var rock = ref rocks[i];
                if (rock.Wait > 0f) { rock.Wait -= dt; rock.Image.color = Color.clear; continue; }
                rock.Fall += Gravity * dt;
                rock.Y -= rock.Fall * dt;
                rock.X += rock.Drift * dt / BossHallSpace.DesignW;
                rock.Angle += rock.Spin * dt;
                if (rock.Y <= rock.Floor) { Respawn(ref rock, Random.Range(.3f, WaitMax)); continue; }
                // Fades out over the last part of its drop.
                float travelled = 1f - (rock.Y - rock.Floor) / (BossHallSpace.DesignH + rock.Size - rock.Floor);
                float alpha = travelled < FadeFrom ? 1f : 1f - (travelled - FadeFrom) / (1f - FadeFrom);
                rock.Rect.anchorMin = rock.Rect.anchorMax = new Vector2(rock.X, rock.Y / BossHallSpace.DesignH);
                rock.Rect.sizeDelta = new Vector2(rock.Size * Mathf.Max(rock.Aspect, 1f), rock.Size / Mathf.Min(rock.Aspect, 1f)) * unit * .8f;
                rock.Rect.localEulerAngles = new Vector3(0f, 0f, rock.Angle);
                rock.Image.color = new Color(1f, 1f, 1f, alpha);
            }
        }
    }
}
