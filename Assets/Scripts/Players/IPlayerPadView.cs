using System;
using UnityEngine;

namespace BossSlayer
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

        // Everyone is in: the pads pop (grow, then shrink away), the screen hides, then `done` runs. A Cancel first
        // stops it without running `done`.
        void PlayExit(Action done);

        // During PlayExit: moves the players' markers (0..1 floor positions, null = not on the floor) and nothing else.
        void MoveCursors(Vector2?[] cursorAt);

        // The pad (0-based) under a 0..1 floor position, or -1.
        int PadAtNormalized(Vector2 normalized);

        // A point local to the gameplay floor input as a 0..1 position on the pad floor.
        Vector2? InputLocalToFloorNormalized(Vector2 inputLocal);
    }
}
