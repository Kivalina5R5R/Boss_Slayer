using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Deepspace
{
    // Boss Fight "arcane codex" look: night-violet stone tablets with cut corners, gilded borders and rules, parchment
    // lettering in Cinzel, and each boss's own element colour (BossThemePalette) as the card / banner accent.
    // Presentation only; nothing here reads or writes game state.
    public static class BossFantasyTheme
    {
        public static readonly Color Night = Rgb(0x0B0812), Stone = Rgb(0x1A1424), StoneLight = Rgb(0x2A2138),
            Gold = Rgb(0xD9B25F), GoldLight = Rgb(0xF6E3A6), GoldDeep = Rgb(0x8C6A2E), Parchment = Rgb(0xEFE3C4),
            ParchmentDim = Rgb(0xB9AC8E), Blood = Rgb(0x8E2420), Ink = Rgb(0x1A1208);

        public static Color Rgb(int hex, float a = 1) => UiThemeKit.Rgb(hex, a);

        // Font swap that also swaps the material: a label that already had an outline carries an instance material
        // bound to its OLD font's atlas, so Cinzel glyphs sampled the Outfit atlas (scrambled "READY" / "DRAGON").
        public static void UseFont(TMP_Text t, TMP_FontAsset font)
        {
            if (t == null || font == null) return;
            bool changed = t.font != font;
            if (changed) t.font = font;
            if (t.fontSharedMaterial == null || t.fontSharedMaterial.mainTexture != font.atlasTexture) { t.fontSharedMaterial = font.material; changed = true; }
            if (changed) Refresh(t);
        }

        // The canvas renderer keeps sampling the OLD font's atlas after a font swap in Play (Cinzel glyph UVs on the
        // Outfit atlas = scrambled letters); rebuild the mesh and hand it the new atlas explicitly.
        public static void Refresh(TMP_Text t)
        {
            if (t == null || !Application.isPlaying || !t.isActiveAndEnabled || t.font == null) return;
            t.SetAllDirty();
            t.ForceMeshUpdate(true, true);
            t.canvasRenderer.SetMaterial(t.materialForRendering, t.font.atlasTexture);
        }

        // Carved-gold lettering: light gold at the top, deep gold at the bottom, thin dark edge.
        public static void GoldText(TMP_Text t, TMP_FontAsset font, float spacing = 8, float outline = .12f)
        {
            if (t == null) return;
            UseFont(t, font);
            t.fontStyle = FontStyles.Normal;
            t.color = Color.white;
            t.enableVertexGradient = true;
            t.colorGradient = new VertexGradient(GoldLight, GoldLight, GoldDeep, GoldDeep);
            t.characterSpacing = spacing;
            t.raycastTarget = false;
            if (Application.isPlaying) { var m = OutlineMaterial(t.font, outline); if (m != null) { t.fontSharedMaterial = m; Refresh(t); } }
            else UiThemeKit.ApplyOutline(t, outline, Ink);
        }

        // Plain lettering with a dark outline (HUD numbers over bright fills).
        public static void Outlined(TMP_Text t, TMP_FontAsset font, Color face, float width, float spacing = 2)
        {
            Letter(t, font, face, spacing);
            if (t == null) return;
            if (Application.isPlaying) { var m = OutlineMaterial(t.font, width); if (m != null) { t.fontSharedMaterial = m; Refresh(t); } }
            else UiThemeKit.ApplyOutline(t, width, Ink);
        }

        // One shared outline material per font / width, built from the font's own material. The outline setters on a
        // label that switched fonts in Play re-instanced its OLD material (Body atlas), which scrambled the letters.
        static readonly System.Collections.Generic.Dictionary<(int, int), Material> outlines = new();
        static Material OutlineMaterial(TMP_FontAsset font, float width)
        {
            if (font == null || font.material == null) return null;
            var key = (font.GetInstanceID(), Mathf.RoundToInt(width * 1000));
            if (outlines.TryGetValue(key, out var m) && m != null) return m;
            ShaderUtilities.GetShaderPropertyIDs();
            m = new Material(font.material) { name = font.name + " Fantasy Outline" };
            m.EnableKeyword(ShaderUtilities.Keyword_Outline);
            m.SetColor(ShaderUtilities.ID_OutlineColor, Ink);
            m.SetFloat(ShaderUtilities.ID_OutlineWidth, width);
            ShaderUtilities.UpdateShaderRatios(m);
            return outlines[key] = m;
        }

        public static void Letter(TMP_Text t, TMP_FontAsset font, Color face, float spacing = 4)
        {
            if (t == null) return;
            UseFont(t, font);
            t.fontStyle = FontStyles.Normal;
            t.enableVertexGradient = false;
            t.color = face;
            t.characterSpacing = spacing;
            t.raycastTarget = false;
        }

        // Ornamental rule: a thin gold line ending in a diamond, e.g. either side of a title. Anchors in parent space.
        public static void Rule(RectTransform parent, string name, float left, float right, float y, float thickness, bool diamondAtRight)
        {
            var root = UiThemeKit.Child(parent, name);
            UiThemeKit.Place(root, left, y, right, y);
            root.sizeDelta = new Vector2(0, thickness * 6);
            var line = UiThemeKit.Child(root, "Line");
            UiThemeKit.Stretch(line, 0);
            line.offsetMin = new Vector2(diamondAtRight ? 0 : thickness * 5, thickness * 2.5f);
            line.offsetMax = new Vector2(diamondAtRight ? -thickness * 5 : 0, -thickness * 2.5f);
            UiThemeKit.Paint(line, UiThemeKit.Alpha(Gold, .85f));
            var gem = UiThemeKit.Child(root, "Diamond");
            gem.anchorMin = gem.anchorMax = new Vector2(diamondAtRight ? 1 : 0, .5f);
            gem.sizeDelta = new Vector2(thickness * 4.2f, thickness * 4.2f);
            gem.anchoredPosition = new Vector2(diamondAtRight ? -thickness * 2.5f : thickness * 2.5f, 0);
            gem.localRotation = Quaternion.Euler(0, 0, 45);
            UiThemeKit.Paint(gem, Gold);
        }
    }
}
