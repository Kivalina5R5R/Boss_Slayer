using UnityEngine;

namespace BossSlayer
{
    // บาส 2026-10-27: the pads were summoning sigils (the shared PlayerPadArt). บาส 2026-11-18: replaced by the art team's
    // card look from the "Select Player" mockup (BossPlayerCards); the other games keep their PlayerPadArt pads.
    public sealed partial class BossSlayerGameManager
    {
        void StylePadArt(PlayerPadSelect.Style style)
        {
            style.Ground = BossFantasyTheme.Night;
            BossPlayerCards.Apply(style, () => playArea);
        }
    }
}
