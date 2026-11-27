using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace BossSlayer
{
    // บาส 2026-10-29: what the operator screen (Display 1) and the wall "how to play" slides show for each game: palette,
    // fonts, art and the four tutorial pages. Art lives in each game's own Resources folder (<folder>/Showcase/), so the
    // per-game build stripping (Editor/PerGameBuildResources) keeps it out of the other games' builds.
    // Presentation only: nothing here reads or writes gameplay state except the TutorialAllowed check.
    public sealed class GameShowcase
    {
        public sealed class Page
        {
            public string heading, headingThai, image;
            public string[] lines, linesEn;   // Thai / English (Lang)
        }

        public string scene, title, tagline, taglineEn, folder;
        public Color ground, panel, frame, accent, ink, muted, quit;
        public UiThemeKit.Shape shape = UiThemeKit.Shape.Rounded;
        public string[] headlineFonts;
        public string logo, backdrop;
        public Page[] pages;
        public Func<bool> tutorialAllowed;

        public Texture2D Load(string name) => string.IsNullOrEmpty(name) ? null : GameArt.Load<Texture2D>(folder + "/Showcase/" + name);

        // 2026-11-20 (BossSlayer project): only Boss Slayer is left, whatever the scene is called.
        public static GameShowcase For(string scene)
        {
            foreach (var showcase in All) if (showcase.scene == scene) return showcase;
            return All[0];
        }

        static Color Hex(int rgb) => UiThemeKit.Rgb(rgb);

        static readonly GameShowcase[] All =
        {
            new GameShowcase
            {
                scene = "BossSlayer", folder = "Art/UI", title = "BOSS SLAYER", tagline = "ล่าบอสเป็นทีม · ปะทะราชาแห่งห้วงลึก", taglineEn = "Hunt bosses as a team · face the King of the Abyss",
                ground = Hex(0x0B0812), panel = Hex(0x1A1424), frame = Hex(0xD9B25F), accent = Hex(0xD9B25F),
                ink = Hex(0xEFE3C4), muted = Hex(0xB9AC8E), quit = Hex(0x8E2420),
                headlineFonts = new[] { "Cinzel Black SDF", "Cinzel Bold SDF" },
                logo = "Logo", backdrop = "Backdrop",
                pages = new[]
                {
                    new Page { heading = "THE RAID", headingThai = "เป้าหมาย", image = "Slide1", lines = new[]
                        { "ร่วมทีมล่าบอส 3 ตัว → เลือกพลังพิเศษ → อีก 3 ตัว → เลือกพลัง", "ด่านสุดท้ายปะทะ ABYSSAL KING ราชาแห่งห้วงลึก", "ยืนบนการ์ดบนพื้นเพื่อเลือกบอสที่จะสู้" }, linesEn = new[] { "Hunt 3 bosses as a team → pick a power → 3 more → pick a power", "The final stage: face the ABYSSAL KING, lord of the abyss", "Stand on a card on the floor to choose your next boss" } },
                    new Page { heading = "STRIKE WITH GEMS", headingThai = "โจมตีด้วยอัญมณี", image = "Slide2", lines = new[]
                        { "ยืนบนอัญมณีบนพื้นค้างไว้ครู่หนึ่ง เพื่อยิงใส่บอส", "แดง = โจมตีแรง · เขียว = ฟื้นเลือดทีม", "เหลือง = ทำให้บอสมึน · ฟ้า = ชาร์จท่าไม้ตาย" }, linesEn = new[] { "Hold your place on a gem on the floor to fire at the boss", "Red = heavy attack · Green = heals the team", "Yellow = stuns the boss · Blue = charges the ultimate" } },
                    new Page { heading = "DODGE THE RED", headingThai = "หลบพื้นแดง", image = "Slide3", lines = new[]
                        { "พื้นสีแดงเตือน 2 วินาทีก่อนบอสโจมตี — รีบหลบออก!", "เลือดหมดจะล้ม 5 วินาที แล้วกลับมาด้วยเลือดครึ่งหนึ่ง", "สตันครบ บอสมึน 6 วิ · ชาร์จครบ 8 ปล่อยท่าไม้ตายทั้งทีม" }, linesEn = new[] { "Red floor warns 2 seconds before the boss strikes: get out!", "Out of health? Down for 5 seconds, back with half health", "Full stun = boss dazed 6 s · 8 charges = team ultimate" } },
                    new Page { heading = "VICTORY", headingThai = "วิธีชนะ", image = "Slide4", lines = new[]
                        { "ระหว่างทาง ยืนบนการ์ดเพื่อโหวตพลังพิเศษของทีม", "โค่น ABYSSAL KING ได้ = RAID COMPLETE!", "แต้มคือดาเมจที่ทำได้ คนทำมากสุดได้ MVP" }, linesEn = new[] { "Between bosses, stand on a card to vote for a team power", "Bring down the ABYSSAL KING = RAID COMPLETE!", "Score is damage dealt: the top player is MVP" } },
                },
                tutorialAllowed = () => UnityEngine.Object.FindFirstObjectByType<BossSlayerGameManager>()?.TutorialAllowed ?? true,
            },
        };

        // ---------------------------------------------------------------- fonts
        // Loaded scene fonts, by name: the headline face per game, and a face that can draw Thai for the body text.
        static readonly Dictionary<string, TMP_FontAsset> fonts = new();

        public TMP_FontAsset Headline()
        {
            foreach (var name in headlineFonts) { var f = FindFont(name); if (f != null) return f; }
            return TMP_Settings.defaultFontAsset;
        }

        public static TMP_FontAsset Thai()
        {
            if (fonts.TryGetValue("<thai>", out var cached) && cached != null) return cached;
            TMP_FontAsset best = null;
            foreach (var f in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
            {
                if (f == null || !f.HasCharacter('ก', true, true)) continue;
                // prefer a face named Thai (Noto Sans Thai / Volley Thai) over a Latin face that only falls back to one
                if (best == null || (f.name.Contains("Thai") && !best.name.Contains("Thai"))) best = f;
            }
            return fonts["<thai>"] = best != null ? best : TMP_Settings.defaultFontAsset;
        }

        static TMP_FontAsset FindFont(string name)
        {
            if (fonts.TryGetValue(name, out var cached) && cached != null) return cached;
            foreach (var f in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
                if (f != null && f.name == name) return fonts[name] = f;
            return null;
        }
    }
}
