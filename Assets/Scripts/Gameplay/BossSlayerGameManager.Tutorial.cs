namespace BossSlayer
{
    public sealed partial class BossSlayerGameManager
    {
        // The operator's TUTORIAL slides may only run before play (TutorialSlides closes itself once this turns false).
        // 2026-10-30: the rush clock starts at the first boss select, so "!IsRushActive" shut the tutorial out from the
        // player select on; now the menu, lobby, player select and the FIRST boss select allow it - not the perk votes and
        // later boss picks of a running rush, and never once a fight is on.
        public bool TutorialAllowed => !gameStarted && !sessionActive && !resultShown
                                       && (!IsRushActive || (rushStage == RushStage.Rush1 && rushKills == 0));
    }
}
