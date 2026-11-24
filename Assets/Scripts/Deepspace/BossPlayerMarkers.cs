using UnityEngine;

namespace Deepspace
{
    // บาส 2026-11-18: Boss Slayer's floor player markers from the art team ("Player": P1..P6), a pixel ring in the
    // player's colour with the P number on top, replacing the code-drawn ring and centre dot. The colours below are
    // the art's own, so the rest of the game (HP cards, juice, pad glow) matches the marker.
    // Art: Resources/BossSlayer/Players/P1..P6.png (284x288). Loaded as textures and made into sprites here, so the
    // import settings do not matter.
    public static class BossPlayerMarkers
    {
        const string Folder = "BossSlayer/Players/P";

        static readonly Color[] colors =
        {
            new Color32(2, 2, 255, 255),      // P1 blue
            new Color32(238, 121, 17, 255),   // P2 orange
            new Color32(238, 17, 170, 255),   // P3 pink
            new Color32(17, 238, 235, 255),   // P4 cyan
            new Color32(161, 17, 238, 255),   // P5 purple
            new Color32(238, 17, 23, 255),    // P6 red
        };

        static readonly Sprite[] sprites = new Sprite[6];

        public static Color ColorOf(int index) => colors[Mathf.Clamp(index, 0, colors.Length - 1)];

        // The marker for player index (0-based), or null when its picture is missing.
        public static Sprite For(int index)
        {
            if (index < 0 || index >= sprites.Length) return null;
            if (sprites[index] != null) return sprites[index];
            var texture = Resources.Load<Texture2D>(Folder + (index + 1));
            if (texture == null) return null;
            sprites[index] = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 100f);
            sprites[index].name = "Boss Player Marker P" + (index + 1);
            return sprites[index];
        }

        // Width / height of the marker picture (it is not quite square).
        public static float Aspect(int index)
        {
            var sprite = For(index);
            return sprite != null ? sprite.rect.width / sprite.rect.height : 1f;
        }
    }
}
