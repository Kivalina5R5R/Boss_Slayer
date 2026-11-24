using UnityEngine;

namespace Deepspace
{
    // The floor pad screen as BossPlayerReadySelection sees it: it only draws, the selection owns the rules.
    // PlayerPadSelect implements it (its external mode).
    public interface IPlayerPadView
    {
        // Shows `count` free pads.
        void ShowExternal(int count);

        // Re-lays the row for a new pad count.
        void ResizeExternal(int count);

        // One frame of the screen; see PlayerPadSelect.DrawExternal for what each array holds.
        void DrawExternal(Vector2?[] cursorAt, int[] cursorPad, int[] holdPadOf, float[] holdProgress, int[] padOwners, string status);

        // Hides the screen.
        void Cancel();

        // The pad (0-based) under a 0..1 floor position, or -1.
        int PadAtNormalized(Vector2 normalized);

        // A point local to the gameplay floor input as a 0..1 position on the pad floor.
        Vector2? InputLocalToFloorNormalized(Vector2 inputLocal);
    }
}
