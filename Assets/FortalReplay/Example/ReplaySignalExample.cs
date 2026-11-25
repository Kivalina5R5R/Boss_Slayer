using UnityEngine;
using FortalReplay;

/// <summary>
/// Example for Fortal Replay Signal: where the three calls go. Put it on any object to try it in the Editor
/// (right-click the component's title: "Test: start a round" / "Test: a moment" / "Test: end the round"), then copy the
/// calls into your own game manager and delete this file.
/// </summary>
public class ReplaySignalExample : MonoBehaviour
{
    [Tooltip("The game's name as the venue knows it. The same name every time: clips are filed under it.")]
    [SerializeField] string gameName = "MyGame";
    [SerializeField] int playerCount = 4;

    int[] scores;

    // 1) The players can play now (after the menu and the countdown): the recorder starts recording.
    [ContextMenu("Test: start a round")]
    public void OnCountdownFinished()
    {
        scores = new int[playerCount];
        ReplaySignal.GameStarted(gameName, playerCount);
    }

    // 2) Optional, as often as you like: something worth a highlight just happened.
    [ContextMenu("Test: a moment")]
    public void OnBossDefeated()
    {
        int player = 0;                                   // who did it (0 = player 1), or -1 for nobody in particular
        scores[player] += 100;
        ReplaySignal.Moment("boss_down", player);
    }

    // 3) The result screen appears: the recorder stops about 4 seconds later and the round is cut.
    [ContextMenu("Test: end the round")]
    public void OnResultScreen()
    {
        int best = 0;
        for (int i = 1; i < scores.Length; i++) if (scores[i] > scores[best]) best = i;
        ReplaySignal.GameEnded(scores, new[] { best });
    }

    // Staff pressed Exit in the middle of a round.
    public void OnExitPressed() => ReplaySignal.GameAborted();
}
