// Boss Slayer runs inside the project's map scene (BossSlayer.unity), where the project's DisplayManager routes the
// outputs: Display 1 is the LED floor, Display 2 is the wall. Deepspace's own display setup (BossFightDisplays) was
// removed on 2026-11-20 when the game moved into that scene; only the floor display index the boss code reads is left.
public static class MultiDisplaySetup
{
    public const int FloorDisplay = 0;
}
