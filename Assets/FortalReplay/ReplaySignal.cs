using System.Text;
using UnityEngine;

namespace FortalReplay
{
    /// <summary>
    /// The three calls a Main Arena game needs so the Fortal replay system records it (Fortal Replay Signal 1.0).
    ///
    ///   ReplaySignal.GameStarted("MyGame", playerCount);   // players can play now (after the countdown)
    ///   ReplaySignal.Moment("boss_down");                  // optional: something worth a highlight just happened
    ///   ReplaySignal.GameEnded(scores);                    // the result screen
    ///
    /// GameStarted makes the recorder on the game PC start recording, GameEnded makes it stop (about 4 s later) and
    /// send the round to be cut into a highlight reel. Nothing else is needed: no network, no settings, no scene object.
    /// It works by writing two small files into a ReplayLogs folder next to the game's exe; when no recorder is
    /// installed the files are simply written and nothing happens. A call can never throw into the game.
    ///
    /// Safe to call wrongly: GameStarted twice = the first round is closed and a new one starts; GameEnded with no
    /// round = nothing; quitting the game mid-round ends the round; a round nobody ends is closed after
    /// <see cref="MaxMinutes"/>. A round shorter than 20 seconds is not cut (the recorder skips it).
    /// </summary>
    public static class ReplaySignal
    {
        /// <summary>A round still open after this many minutes is closed by itself (a game that never said "ended").</summary>
        public static float MaxMinutes = 30f;

        /// <summary>True between GameStarted and GameEnded.</summary>
        public static bool IsRecording => ReplayLog.Active;

        static Watchdog watchdog;

        /// <summary>The round starts: call when the players can really play (after menus and the countdown).</summary>
        /// <param name="game">The game's name as the venue knows it, e.g. "Deepspace". Letters, digits, _ and - are
        /// kept, everything else is dropped. Use the same name every time: clips are filed under it.</param>
        /// <param name="playerCount">How many people play this round (0 if not known).</param>
        public static void GameStarted(string game, int playerCount = 0)
        {
            try
            {
                ReplayLog.BeginSession(CleanName(game), Mathf.Max(0, playerCount));
                if (watchdog == null && Application.isPlaying)      // (not in an Editor script: no objects are made there)
                {
                    var go = new GameObject("FortalReplaySignal") { hideFlags = HideFlags.HideAndDontSave };
                    Object.DontDestroyOnLoad(go);
                    watchdog = go.AddComponent<Watchdog>();
                }
                if (watchdog != null) watchdog.started = Time.realtimeSinceStartup;
            }
            catch (System.Exception e) { Debug.LogWarning("[ReplaySignal] GameStarted: " + e.Message); }
        }

        /// <summary>Optional: something worth showing just happened. The reel is built from these first.</summary>
        /// <param name="name">What happened, e.g. "boss_down", "combo", "last_second_win" (letters, digits, _).</param>
        /// <param name="player">Who did it (0 = player 1), or -1 when it is nobody in particular.</param>
        /// <param name="secondsBefore">How much of the lead-up to keep in the clip.</param>
        /// <param name="secondsAfter">How much of the aftermath to keep.</param>
        public static void Moment(string name, int player = -1, float secondsBefore = 5f, float secondsAfter = 3f)
        {
            if (!ReplayLog.Active) return;
            try
            {
                string what = CleanName((name ?? "").Trim().Replace(' ', '_'));      // "boss down" -> "boss_down"
                if (player >= 0) ReplayLog.Event(what, secondsBefore, secondsAfter, player);
                else ReplayLog.Event(what, secondsBefore, secondsAfter);
            }
            catch (System.Exception e) { Debug.LogWarning("[ReplaySignal] Moment: " + e.Message); }
        }

        /// <summary>The round is over: call when the result screen appears.</summary>
        /// <param name="scores">Final score of each player, index 0 = player 1 (null if the game has no scores).</param>
        /// <param name="winners">Indexes of the winners (null or empty = a draw / no winner).</param>
        public static void GameEnded(int[] scores = null, int[] winners = null)
        {
            try { ReplayLog.EndSession("completed", winners ?? new int[0], scores); }
            catch (System.Exception e) { Debug.LogWarning("[ReplaySignal] GameEnded: " + e.Message); }
        }

        /// <summary>The round stopped before its end (staff pressed Exit, everyone left). The recording still stops
        /// and what was played is cut.</summary>
        public static void GameAborted()
        {
            try { ReplayLog.EndSession("aborted"); }
            catch (System.Exception e) { Debug.LogWarning("[ReplaySignal] GameAborted: " + e.Message); }
        }

        /// <summary>File-name safe: ASCII letters, digits, _ and -; a space becomes nothing ("Deep Space" -> "DeepSpace").</summary>
        static string CleanName(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s ?? "")
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_' || c == '-') sb.Append(c);
            return sb.Length > 0 ? sb.ToString() : "Game";
        }

        /// <summary>Closes a round the game forgot to end, so the recorder never records for hours.</summary>
        sealed class Watchdog : MonoBehaviour
        {
            public float started;

            void Update()
            {
                if (ReplayLog.Active && MaxMinutes > 0f && Time.realtimeSinceStartup - started > MaxMinutes * 60f)
                {
                    Debug.LogWarning("[ReplaySignal] the round was never ended: closed after " + MaxMinutes + " minutes");
                    ReplayLog.EndSession("timeout");
                }
            }
        }
    }
}
