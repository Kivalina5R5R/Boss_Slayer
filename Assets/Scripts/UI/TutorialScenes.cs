using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static BossSlayer.TutorialArt;

namespace BossSlayer
{
    // 2026-11-27 "ไม่เห็นมีรูปอะไร ... ทำใหม่ไปเลย": the moving picture inside each "how to play" slide, built from the
    // game's own art (boss cards, gems, the fire floor, the Abyssal King, VICTORY) instead of the Showcase pictures that
    // were never drawn. TutorialSlides builds one per page into its framed stage and ticks it with the page's age.
    // Presentation only: nothing here touches gameplay.
    public abstract class TutorialScene
    {
        public enum Kind { Raid, Gems, Dodge, Victory }

        // The stage is about 2.17 : 1 (the left half of the wall); pictures that cover it are cropped to that.
        protected const float StageAspect = 2.17f;

        protected RectTransform Stage { get; private set; }

        public static TutorialScene Create(Kind kind) => kind switch
        {
            Kind.Gems => new GemsScene(),
            Kind.Dodge => new DodgeScene(),
            Kind.Victory => new VictoryScene(),
            _ => new RaidScene(),
        };

        public void Build(RectTransform stage)
        {
            Stage = stage;
            OnBuild();
            Tick(0f);
        }

        protected abstract void OnBuild();

        // time: seconds since the page came up (unscaled).
        public abstract void Tick(float time);

        protected static void Scale(Graphic graphic, float scale) => graphic.rectTransform.localScale = new Vector3(scale, scale, 1f);

        protected static void SetAlpha(Graphic graphic, float alpha)
        {
            var c = graphic.color;
            c.a = alpha;
            graphic.color = c;
        }
    }

    // Page 1: 3 bosses -> a power -> 3 more -> the Abyssal King, lit one step after another.
    sealed class RaidScene : TutorialScene
    {
        const float StepSeconds = 1.1f;
        static readonly string[] FirstCards = { "CardRed", "CardBlue", "CardBrown" };
        static readonly string[] SecondCards = { "CardBlue", "CardBrown", "CardRed" };

        readonly RawImage[][] steps = new RawImage[4][];
        readonly TMP_Text[] arrows = new TMP_Text[3];
        RawImage kingGlow;

        protected override void OnBuild()
        {
            Cover(Stage, "Hall", Ui + "SelectBoss/SelectBossBG_v7", StageAspect, new Color(.55f, .5f, .6f, 1f));
            Fill(Stage, "Shade", UiThemeKit.Alpha(Night, .35f));

            steps[0] = Cards(Box(Stage, "First Bosses", .025f, .2f, .245f, .88f), FirstCards);
            Caption("First Label", "3 BOSSES", "บอส 3 ตัว", .015f, .255f, Gold);
            arrows[0] = Arrow("Arrow 1", .245f, .305f);

            var power = Box(Stage, "Power", .305f, .3f, .455f, .78f);
            // 2026-11-27 "กรอบมันแหว่ง": Perk/SkillFrame is open at the bottom, so the icon gets a closed gold ring of its
            // own, a child of the icon so it grows with it when the step lights up.
            var icon = Picture(power, "Icon", Ui + "Perk/Icons/Heavy Smash", .08f, .08f, .92f, .92f);
            UiThemeKit.Ring(icon.rectTransform, "Frame", UiThemeKit.Shape.Rounded, Gold, 6, -4).raycastTarget = false;
            steps[1] = new[] { icon };
            Caption("Power Label", "POWER", "เลือกพลัง", .29f, .47f, Gold);
            arrows[1] = Arrow("Arrow 2", .455f, .505f);

            steps[2] = Cards(Box(Stage, "More Bosses", .5f, .2f, .68f, .88f), SecondCards);
            Caption("More Label", "3 MORE", "อีก 3 ตัว", .49f, .69f, Gold);
            arrows[2] = Arrow("Arrow 3", .68f, .73f);

            kingGlow = Glow(Stage, "King Glow", UiThemeKit.Alpha(Ember, .5f), .66f, .08f, 1.04f, 1.05f);
            // 2026-11-27 "text กับบอสซ้อนกัน": the king stands above the ABYSSAL KING caption (.04-.18), not behind it.
            steps[3] = new[] { Picture(Stage, "Abyssal King", Ui + "FinalBoss/AbyssalKing", .75f, .22f, .96f, .94f) };
            var final = Caption("King Label", "ABYSSAL KING", "ราชาแห่งห้วงลึก", .7f, 1f, Ember);
            Outline(final, .25f);
        }

        RawImage[] Cards(RectTransform column, string[] names)
        {
            var cards = new RawImage[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                float top = 1f - i / (float)names.Length;
                cards[i] = Picture(column, "Card " + (i + 1), Ui + "SelectBoss/" + names[i], 0, top - 1f / names.Length + .02f, 1, top - .02f);
            }
            return cards;
        }

        TMP_Text Caption(string name, string english, string thai, float left, float right, Color color)
        {
            var label = Label(Stage, name, english, thai, left, .04f, right, .18f, 64, color);
            Outline(label, .2f);
            return label;
        }

        TMP_Text Arrow(string name, float left, float right)
        {
            var arrow = Label(Stage, name, ">>", null, left, .42f, right, .64f, 90, Gold);
            Outline(arrow, .2f);
            return arrow;
        }

        public override void Tick(float time)
        {
            int active = (int)(time / StepSeconds) % steps.Length;
            float age = time % StepSeconds;
            for (int i = 0; i < steps.Length; i++)
            {
                bool lit = i == active;
                float scale = lit ? 1.06f + .06f * Mathf.Max(0, 1 - age / .3f) : 1f;
                foreach (var image in steps[i])
                {
                    Scale(image, scale);
                    image.color = lit ? Color.white : new Color(.55f, .55f, .6f, 1f);
                }
            }
            for (int i = 0; i < arrows.Length; i++)
                SetAlpha(arrows[i], i == active ? 1f : .35f + .15f * Mathf.Sin(time * 4f + i));
            SetAlpha(kingGlow, (active == 3 ? .75f : .35f) + .15f * Mathf.Sin(time * 3f));
        }
    }

    // Page 2: a player steps onto each gem in turn, the load bar fills, and the gem fires.
    sealed class GemsScene : TutorialScene
    {
        const float GemSeconds = 1.8f, MoveSeconds = .35f, LoadSeconds = 1.05f;
        static readonly string[] GemArt = { "GemAttack", "GemHeal", "GemStun", "GemUltimate" };
        static readonly string[] English = { "ATTACK", "HEAL", "STUN", "ULTIMATE" };
        static readonly string[] Thai = { "โจมตี", "ฟื้นเลือด", "ทำให้มึน", "ชาร์จท่าไม้ตาย" };
        static readonly Color[] Colors = { UiThemeKit.Rgb(0xFF5B4A), UiThemeKit.Rgb(0x5BE36A), UiThemeKit.Rgb(0xFFD23F), UiThemeKit.Rgb(0x4FB8FF) };

        readonly RawImage[] gems = new RawImage[4];
        readonly RawImage[] glows = new RawImage[4];
        RectTransform player, bar;
        Image load;

        static float GemX(int i) => .155f + i * .23f;

        protected override void OnBuild()
        {
            Cover(Stage, "Floor", Ui + "PlayerSelectCards/Floor", StageAspect, new Color(.8f, .8f, .85f, 1f));
            Fill(Stage, "Shade", UiThemeKit.Alpha(Night, .25f));

            for (int i = 0; i < gems.Length; i++)
            {
                float x = GemX(i);
                glows[i] = Glow(Stage, "Glow " + (i + 1), UiThemeKit.Alpha(Colors[i], .45f), x - .15f, .14f, x + .15f, .9f);
                gems[i] = Picture(Stage, "Gem " + (i + 1), Ui + "Gems/" + GemArt[i], x - .09f, .34f, x + .09f, .72f);
                var label = Label(Stage, "Label " + (i + 1), English[i], Thai[i], x - .115f, .1f, x + .115f, .26f, 66, Colors[i]);
                Outline(label, .25f);
            }

            player = (RectTransform)Picture(Stage, "Player", "Art/Players/P1", 0, .26f, .15f, .8f).transform.parent;
            bar = (RectTransform)Bar(Stage, "Load Bar", Ui + "Gems/GemLoadBarBG", Ui + "Gems/GemLoadBar", 0, .82f, .15f, .9f).transform.parent;
            load = bar.Find("Fill").GetComponent<Image>();
        }

        public override void Tick(float time)
        {
            int gem = (int)(time / GemSeconds) % gems.Length;
            int previous = (gem + gems.Length - 1) % gems.Length;
            float age = time % GemSeconds;
            bool firstVisit = time < GemSeconds;

            float x = firstVisit ? GemX(gem) : Mathf.Lerp(GemX(previous), GemX(gem), Ease(0, MoveSeconds, age));
            float hop = Mathf.Sin(Mathf.Clamp01(age / MoveSeconds) * Mathf.PI) * (firstVisit ? 0 : .06f);
            CentreAt(player, x, .53f + hop);
            CentreAt(bar, x, .86f + hop);

            float loaded = Mathf.Clamp01((age - MoveSeconds) / LoadSeconds);
            load.fillAmount = loaded;
            bool fired = loaded >= 1f;
            float flash = fired ? 1f - Mathf.Clamp01((age - MoveSeconds - LoadSeconds) / .4f) : 0f;

            for (int i = 0; i < gems.Length; i++)
            {
                bool current = i == gem;
                gems[i].rectTransform.anchoredPosition = new Vector2(0, Mathf.Sin(time * 2.2f + i * 1.3f) * 6f);
                Scale(gems[i], current ? 1.05f + .3f * flash : .92f);
                gems[i].color = current ? Color.white : new Color(.7f, .7f, .75f, 1f);
                SetAlpha(glows[i], current ? .45f + .55f * flash + .1f * loaded : .15f);
            }
        }
    }

    // Page 3: the floor turns red, counts 2..1, the player steps out, the boss slams. Stun and ultimate bars fill below.
    sealed class DodgeScene : TutorialScene
    {
        const float CycleSeconds = 3.4f, WarnSeconds = 2f, SlamSeconds = .6f;

        Image zone;
        RawImage zoneGlow;
        TMP_Text count, safe;
        RawImage player;
        Image stun, ultimate;

        protected override void OnBuild()
        {
            Cover(Stage, "Fire Floor", "Art/BG/Fire/BGFloorFire", StageAspect, Color.white);
            Fill(Stage, "Shade", UiThemeKit.Alpha(Night, .2f));

            var zoneBox = Box(Stage, "Warning Zone", .27f, .27f, .63f, .9f);
            zone = zoneBox.gameObject.AddComponent<Image>();
            UiThemeKit.Skin(zone, UiThemeKit.Shape.Chamfer, UiThemeKit.Alpha(Ember, .3f), 24);
            zone.raycastTarget = false;
            UiThemeKit.Ring(zoneBox, "Edge", UiThemeKit.Shape.Chamfer, Ember, 6, 0).raycastTarget = false;
            zoneGlow = Glow(Stage, "Zone Glow", UiThemeKit.Alpha(Ember, 0), .15f, .1f, .75f, 1.05f);

            count = Label(zoneBox, "Count", "2", null, 0, .2f, 1, .8f, 220, Color.white);
            Outline(count, .25f);

            player = Picture(Stage, "Player", "Art/Players/P2", 0, .38f, .13f, .78f);
            safe = Label(Stage, "Safe", "SAFE!", "รอดแล้ว!", .68f, .78f, .98f, .92f, 72, UiThemeKit.Rgb(0x5BE36A));
            Outline(safe, .25f);

            Picture(Stage, "Stun Icon", Ui + "Gems/GemStun", .015f, .03f, .085f, .22f);
            stun = Bar(Stage, "Stun Bar", Ui + "Hud/StunBack", Ui + "Hud/StunFill", .095f, .1f, .47f, .15f);
            stun.color = UiThemeKit.Rgb(0xFFD23F);

            Picture(Stage, "Ultimate Icon", Ui + "Gems/GemUltimate", .53f, .03f, .6f, .22f);
            ultimate = Bar(Stage, "Ultimate Bar", Ui + "Hud/StunBack", Ui + "Hud/UltimateFill", .61f, .1f, .985f, .15f);
        }

        public override void Tick(float time)
        {
            float age = time % CycleSeconds;
            bool slam = age >= WarnSeconds && age < WarnSeconds + SlamSeconds;

            if (age < WarnSeconds)
            {
                // blinks faster as the hit comes
                float blink = .5f + .5f * Mathf.Sin(age * (6f + age * 8f));
                zone.color = UiThemeKit.Alpha(Ember, .22f + .3f * blink);
                SetAlpha(zoneGlow, .15f + .2f * blink);
                count.text = Mathf.CeilToInt(WarnSeconds - age).ToString();
                Scale(count, 1f + .25f * (1f - (age % 1f)));
            }
            else if (slam)
            {
                float hit = 1f - (age - WarnSeconds) / SlamSeconds;
                zone.color = UiThemeKit.Alpha(Color.Lerp(Ember, Color.white, hit * .5f), .55f + .4f * hit);
                SetAlpha(zoneGlow, .9f * hit);
                count.text = Lang.Thai ? "ตูม!" : "SLAM!";
                if (Lang.Thai && count.font != GameShowcase.Thai()) count.font = GameShowcase.Thai();
                Scale(count, 1f + .2f * hit);
            }
            else
            {
                zone.color = UiThemeKit.Alpha(Ember, .1f);
                SetAlpha(zoneGlow, 0f);
                count.text = "";
            }

            float x = Mathf.Lerp(.45f, .84f, Ease(.45f, 1.15f, age));
            CentreAt((RectTransform)player.transform.parent, x, .58f);
            SetAlpha(player, age > CycleSeconds - .35f ? (CycleSeconds - age) / .35f : Mathf.Clamp01(age / .2f));
            SetAlpha(safe, age >= WarnSeconds && age < CycleSeconds - .2f ? 1f : 0f);

            stun.fillAmount = Mathf.Repeat(time / 4f, 1f);
            ultimate.fillAmount = Mathf.Floor(Mathf.Repeat(time / .6f, 9f)) / 8f;
        }
    }

    // Page 4: VICTORY punches in over a turning gold ring, RAID COMPLETE under it, the team with the MVP crowned.
    sealed class VictoryScene : TutorialScene
    {
        const float CycleSeconds = 4f;

        RawImage title, complete, ringInner, glow;
        RectTransform mvp;

        protected override void OnBuild()
        {
            Cover(Stage, "Night", Ui + "Start/BG", StageAspect, new Color(.45f, .4f, .5f, 1f));
            glow = Glow(Stage, "Glow", UiThemeKit.Alpha(Gold, .3f), .2f, .2f, .8f, 1f);
            ringInner = Picture(Stage, "Ring", Ui + "Victory/Ring", .3f, .22f, .7f, .98f);

            title = Picture(Stage, "Title", Ui + "Victory/Title", .14f, .5f, .86f, .88f);
            complete = Picture(Stage, "Raid Complete", Ui + "Result/RaidCompleteText", .2f, .36f, .8f, .48f);

            for (int i = 0; i < 4; i++)
            {
                float x = .29f + i * .14f;
                Picture(Stage, "Player " + (i + 1), "Art/Players/P" + (i + 1), x - .055f, .04f, x + .055f, .3f);
            }
            var crown = Label(Stage, "MVP", "MVP", null, .2f, .27f, .38f, .37f, 70, Gold);
            Outline(crown, .25f);
            mvp = (RectTransform)crown.transform;
        }

        public override void Tick(float time)
        {
            float age = time % CycleSeconds;
            float punch = Mathf.Max(0, 1 - age / .45f);
            Scale(title, 1f + .35f * punch * punch);
            SetAlpha(title, Mathf.Clamp01(age / .15f));
            SetAlpha(complete, Ease(.35f, .7f, age));
            ringInner.rectTransform.localEulerAngles = new Vector3(0, 0, time * 14f);
            SetAlpha(glow, .2f + .1f * Mathf.Sin(time * 2.5f) + .3f * punch);
            mvp.anchoredPosition = new Vector2(0, Mathf.Abs(Mathf.Sin(time * 3f)) * 10f);
        }
    }
}
