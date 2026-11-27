using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BossSlayer
{
    // บาส 2026-10-30: "เพิ่มระบบภาษาไทยให้เกม 4 เกมนี้ ... ทั้งในเกมหลักและ Tutorial" — Thai / English for Deepspace, Memory
    // Farm, Boss Slayer and Animal Volleyball. He picked a TH/EN button on the operator screen (Display 1), Thai first,
    // in Boss Slayer the web launcher's button now chooses it, English at every start (see Current).
    //
    // How: the games keep writing their text as before (English in code, some Thai, baked scene labels). LangDriver looks
    // at every visible TMP text just before it is drawn (LateUpdate, very late) and swaps any text found in LangTable to
    // the current language — whole strings, pattern strings with numbers / names ("ROUND {0}"), and line by line /
    // between rich-text tags for composed labels. Nothing in the games' logic changes, so a text the table doesn't know
    // simply stays as written. New text: add its pair to LangTable (or write Lang.T(en, th) where the code is new).
    // LangDriver only runs in the BossSlayer scene.
    public static class Lang
    {
        public enum Language { Thai, English }

        static readonly HashSet<string> Scenes = new() { "BossSlayer" };
        static Language current = Language.English;

        // 2026-11-19: "game base on ภาษาอังกฤษ" - Boss Slayer is an English game. 2026-11-27: "จะสลับเป็นภาษาไทยทั้งหมด
        // ก็ต่อเมื่อกดปุ่มจาก launcher เท่านั้น" - every run starts in English (nothing is remembered between runs) and
        // the whole game turns Thai only when the web launcher's language button asks (BossArenaRemoteHooks.SetLanguage).
        public static Language Current => current;
        public static bool Thai => current == Language.Thai;
        public static event Action Changed;

        public static void Set(Language language)
        {
            if (current == language) return;
            current = language;
            Changed?.Invoke();
        }

        public static void Toggle() => Set(Thai ? Language.English : Language.Thai);

        /// <summary>For code written with both languages at hand.</summary>
        public static string T(string en, string th) => Thai ? th : en;

        // ---------------------------------------------------------------- translation
        static Dictionary<string, string> toThai, toEnglish;
        static List<(Regex read, string write)> toThaiPatterns, toEnglishPatterns;
        static readonly Dictionary<string, string> cache = new();
        static readonly Regex Tag = new(@"(<[^>]*>)", RegexOptions.Compiled);
        static string builtFor;

        // The running game's own table first, then the shared one (player select, lobby, dropout...): one game's
        // "Player {0}" pattern never touches another game's text. Patterns: the most specific (most fixed letters) first.
        static void Build()
        {
            if (toThai != null) return;   // Rebuild() on every scene load
            string scene = SceneManager.GetActiveScene().name;
            builtFor = scene;
            cache.Clear();
            toThai = new Dictionary<string, string>(StringComparer.Ordinal);
            toEnglish = new Dictionary<string, string>(StringComparer.Ordinal);
            var found = new List<(string en, string th, int order)>();
            int order = 0;
            if (LangTable.ByScene.TryGetValue(scene, out var own))
                foreach (var pair in own) found.Add((pair.en, pair.th, order++));
            foreach (var pair in LangTable.Shared) found.Add((pair.en, pair.th, order++));
            foreach (var (en, th, _) in found)
            {
                if (en.Contains("{0}")) continue;
                toThai.TryAdd(en, th);
                toEnglish.TryAdd(th, en);
            }
            found.RemoveAll(f => !f.en.Contains("{0}"));
            // each direction ranks by the side it reads: the most fixed letters first, table order on a tie
            // (bug hunt 2026-10-30: one ranking for both sides let a same-text pair hide "×{0} · {1}" in Thai)
            int Rank((string en, string th, int order) a, (string en, string th, int order) b, bool readThai)
            {
                int byLetters = readThai ? Fixed(b.th).CompareTo(Fixed(a.th)) : Fixed(b.en).CompareTo(Fixed(a.en));
                return byLetters != 0 ? byLetters : a.order.CompareTo(b.order);
            }
            var forThai = new List<(string en, string th, int order)>(found);
            forThai.Sort((a, b) => Rank(a, b, false));
            toThaiPatterns = new List<(Regex, string)>();
            foreach (var (en, th, _) in forThai) toThaiPatterns.Add((Pattern(en), th));
            var forEnglish = new List<(string en, string th, int order)>(found);
            forEnglish.Sort((a, b) => Rank(a, b, true));
            toEnglishPatterns = new List<(Regex, string)>();
            foreach (var (en, th, _) in forEnglish) toEnglishPatterns.Add((Pattern(th), en));
        }

        /// <summary>The scene changed: use its table (runs on every scene load; Editor tools call it after opening one).</summary>
        public static void Rebuild() { toThai = null; cache.Clear(); }

        static int Fixed(string template) => Regex.Replace(template, @"\{\d\}", "").Length;

        // "ROUND {0} / {1}" -> ^ROUND (.+?) / (.+?)$
        static Regex Pattern(string template)
        {
            var sb = new StringBuilder("^");
            int i = 0;
            foreach (Match m in Regex.Matches(template, @"\{(\d)\}"))
            {
                sb.Append(Regex.Escape(template.Substring(i, m.Index - i)));
                sb.Append("(?<g" + m.Groups[1].Value + @">[^\n]+?)");   // never across a line: "SET {0}" must not swallow line 2
                i = m.Index + m.Length;
            }
            sb.Append(Regex.Escape(template.Substring(i))).Append('$');
            return new Regex(sb.ToString(), RegexOptions.Compiled);
        }

        /// <summary>The text in the current language: whole string, pattern, then line by line and between tags.</summary>
        public static string Tr(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            Build();   // cheap when the scene did not change
            string key = (Thai ? "t" : "e") + text;
            if (cache.TryGetValue(key, out var hit)) return hit;
            string result = text.IndexOf('\n') >= 0 ? TranslateLines(text) : TranslateSegments(text);
            if (cache.Count > 20000) cache.Clear();   // a score counter makes a new string every frame
            cache[key] = result;
            return result;
        }

        static string TranslateLines(string text)
        {
            var whole = Whole(text);
            if (whole != null) return whole;
            var lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++) lines[i] = TranslateSegments(lines[i]);
            return string.Join("\n", lines);
        }

        // "<color=#fff>ROUND 2</color> GO!" -> each run of text between tags on its own
        static string TranslateSegments(string text)
        {
            var whole = Whole(text);
            if (whole != null) return whole;
            if (text.IndexOf('<') < 0) return text;
            var parts = Tag.Split(text);
            bool changed = false;
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length == 0 || parts[i][0] == '<') continue;
                var t = Whole(parts[i]);
                if (t != null) { parts[i] = t; changed = true; }
            }
            return changed ? string.Concat(parts) : text;
        }

        // one piece of text (no tags / lines to split): exact pair, else a pattern; surrounding spaces are kept
        static string Whole(string text)
        {
            int a = 0, b = text.Length;
            while (a < b && char.IsWhiteSpace(text[a])) a++;
            while (b > a && char.IsWhiteSpace(text[b - 1])) b--;
            if (a == b) return null;
            string core = a == 0 && b == text.Length ? text : text.Substring(a, b - a);
            string got = Exact(core) ?? FromPattern(core);
            return got == null ? null : text.Substring(0, a) + got + text.Substring(b);
        }

        static string Exact(string core)
        {
            if (Thai) return toThai.TryGetValue(core, out var th) ? th : null;
            return toEnglish.TryGetValue(core, out var en) ? en : null;
        }

        static string FromPattern(string core)
        {
            foreach (var (re, write) in Thai ? toThaiPatterns : toEnglishPatterns)
            {
                var m = re.Match(core);
                if (!m.Success) continue;
                return Regex.Replace(write, @"\{(\d)\}", g =>
                {
                    var v = m.Groups["g" + g.Groups[1].Value];
                    return v.Success ? Part(v.Value) : g.Value;
                });
            }
            return null;
        }

        // A {n} part: a name may have its own pair, also inside colour tags ("<color>LEFT TEAM</color>"), and a list of
        // names ("Player 1, Player 2, Player 3") is done name by name (bug hunt 2026-10-30).
        static string Part(string value)
        {
            if (value.IndexOf(", ", StringComparison.Ordinal) < 0) return TranslateSegments(value);
            var items = value.Split(new[] { ", " }, StringSplitOptions.None);
            for (int i = 0; i < items.Length; i++) items[i] = TranslateSegments(items[i]);
            return string.Join(", ", items);
        }

        // ---------------------------------------------------------------- driver
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            SceneManager.sceneLoaded += (s, _) => { Rebuild(); Ensure(s); };
            Ensure(SceneManager.GetActiveScene());
        }

        static void Ensure(Scene scene)
        {
            // the scene's own baked object (Tools/Deepspace/Bake Operator Display) normally runs already
            if (!Scenes.Contains(scene.name) || LangDriver.Instance != null) return;
            SceneManager.MoveGameObjectToScene(new GameObject(DriverName, typeof(LangDriver)), scene);
        }

        public const string DriverName = "Language (TH/EN)";

        /// <summary>The four games that use the TH/EN system.</summary>
        public static bool Uses(string scene) => Scenes.Contains(scene);

        /// <summary>Editor capture: every distinct text drawn this session goes here (null = off).</summary>
        public static HashSet<string> Capture;
    }

    // Translates while TextMeshPro builds the letters (ITextPreprocessor), not by writing the label: the game's own text
    // stays exactly as it wrote it (English in code), so code that reads a label back still sees its own string, a label
    // set to the same text again is not rebuilt, measuring (GetPreferredValues) and per-letter animation (ForceMeshUpdate
    // + vertex edits) run on the Thai letters, and a label appearing mid-frame is Thai from its first frame.
    // (2026-10-30 bug hunt: the first version rewrote labels in LateUpdate - English flashes on pooled pop-ups, every-frame
    // rebuilds, the Volley serve fly-in lost.) Every label gets its preprocessor the first time TMP generates it
    // (TEXT_CHANGED_EVENT, also raised in builds) and on a periodic sweep; TH/EN re-parses them all.
    public sealed class LangDriver : MonoBehaviour
    {
        public static LangDriver Instance { get; private set; }
        readonly List<TMP_Text> mine = new();
        int frame;

        void Awake()
        {
            if (Instance != null && Instance != this) { enabled = false; return; }   // one driver is enough
            Instance = this;
#if UNITY_EDITOR
            // Editor harnesses (scenario tests, previews) collect every text the games draw into Library/LangCapture/
            if (System.IO.File.Exists("Library/LangCapture.on")) Lang.Capture ??= new HashSet<string>();
#endif
        }

        void OnEnable()
        {
            if (Instance != this) return;
            TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextBuilt);
            Lang.Changed += Reparse;
            Sweep();
        }

        void OnDisable()
        {
            TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextBuilt);
            Lang.Changed -= Reparse;
        }

        void OnDestroy() { if (Instance == this) Instance = null; Flush(); }
        void OnApplicationQuit() => Flush();

        // a label TMP just built: if it has no preprocessor yet, give it ours and build it again right away (same frame)
        void OnTextBuilt(UnityEngine.Object obj)
        {
            if (obj is not TMP_Text t) return;
            if (t.textPreprocessor == null) { if (Adopt(t)) t.ForceMeshUpdate(false, true); return; }
            if (t.textPreprocessor is not LangPreprocessor p) return;
            // Fake bold (FontStyles.Bold, e.g. Cinzel labels) spreads Thai letters apart and no rich-text tag can turn
            // the label's own Bold off: drop it while the label shows Thai, give it back when it shows English again
            bool bold = (t.fontStyle & FontStyles.Bold) != 0;
            if (p.LastThai && bold)
            {
                unbolded.Add(t);
                t.fontStyle &= ~FontStyles.Bold;
                t.ForceMeshUpdate(false, true);   // same frame; the next build sees no Bold and stops here
            }
            else if (!p.LastThai && unbolded.Remove(t))
            {
                t.fontStyle |= FontStyles.Bold;
                t.ForceMeshUpdate(false, true);
            }
        }

        readonly HashSet<TMP_Text> unbolded = new();   // labels whose Bold is off only while they show Thai

        bool Adopt(TMP_Text t)
        {
            if (t.textPreprocessor != null) return false;   // ours already, or a label that opted out (Lang.Untranslated)
            t.textPreprocessor = new LangPreprocessor(t);
            mine.Add(t);
            return true;
        }

        void Sweep()
        {
            foreach (var t in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (Adopt(t)) t.ForceMeshUpdate(true, true);
            mine.RemoveAll(t => t == null);
        }

        // TH/EN pressed: every label builds again in the new language (hidden ones too, so they open right)
        void Reparse()
        {
            mine.RemoveAll(t => t == null);
            foreach (var t in mine) t.ForceMeshUpdate(true, true);
        }

        void LateUpdate()
        {
            if (++frame % 120 == 0) { Sweep(); Flush(); }   // backup for labels the build event missed
        }

        void Flush()
        {
#if UNITY_EDITOR
            if (Lang.Capture == null || Lang.Capture.Count == 0) return;
            System.IO.Directory.CreateDirectory("Library/LangCapture");
            var file = "Library/LangCapture/" + SceneManager.GetActiveScene().name + ".txt";
            var all = new HashSet<string>(System.IO.File.Exists(file) ? System.IO.File.ReadAllText(file).Split('\u241E') : new string[0]);
            all.UnionWith(Lang.Capture);
            all.Remove("");
            System.IO.File.WriteAllText(file, string.Join("\u241E", all));   // the separator: texts hold newlines
            Lang.Capture.Clear();
#endif
        }
    }

    // One per label: the text TMP builds = the game's text in the current language. Thai drawn with a font's letter
    // spacing or fake bold (Cinzel) has its vowels and tone marks pulled off the letters, so Thai output is prefixed with
    // tags that cancel both - the label's own settings are never touched, English keeps them.
    sealed class LangPreprocessor : ITextPreprocessor
    {
        readonly TMP_Text owner;
        public LangPreprocessor(TMP_Text owner) { this.owner = owner; }

        /// <summary>The last text built was Thai (LangDriver drops fake Bold for it).</summary>
        public bool LastThai { get; private set; }

        public string PreprocessText(string text)
        {
            if (string.IsNullOrEmpty(text)) { LastThai = false; return text; }
            Lang.Capture?.Add(text);
            var shown = Lang.Tr(text);
            LastThai = HasThai(shown);
            if (owner == null || !owner.richText || !LastThai || owner.characterSpacing <= 0) return shown;
            // cancel the label's letter spacing for Thai (cspace in em: characterSpacing is in 1/100 em)
            return "<cspace=" + (-owner.characterSpacing / 100f).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "em>" + shown;
        }

        static bool HasThai(string s)
        {
            foreach (char c in s) if (c >= '\u0E00' && c <= '\u0E7F') return true;
            return false;
        }
    }

    // For labels that already hold the right language (the tutorial slides, Lang.T text): leave them alone.
    public sealed class LangUntranslated : ITextPreprocessor
    {
        public static readonly LangUntranslated Instance = new();
        public string PreprocessText(string text) => text;
    }
}
