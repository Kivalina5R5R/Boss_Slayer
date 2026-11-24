using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Deepspace
{
    // บาส 2026-10-22: shared "CHOOSE YOUR PLAYER" screen (first built for Boss Slayer, now Deepspace / Memory Farm /
    // VolleyBall too, like the Bounty Hunter 15-min plan's character cards). After a game knows how many play, the floor
    // shows one pad per player; standing on a free pad for HoldSeconds takes it (one player per pad; บาส 2026-10-26: a pick
    // is locked, no switching). When everyone on the floor has a pad the screen holds AllInSeconds and calls back; at TimeoutSeconds
    // the rest get the free pads in order. The pad number becomes the player number (BossFloorPlayers.SetRoster), so
    // that player's colour, lane, team and score are theirs for the session.
    // Positions come from BossFloorPlayers (normalised floor, y = 0 at the bottom), so every game reads the same;
    // the screen draws its own cursor ring per tag (white until it owns a pad, then that pad's colour). Editor mouse mode:
    // the mouse is P1, players with no position are filled in. Without UWB (and not the Editor mouse) it calls back at
    // once with the order unchanged. The look (colours, fonts, pad places, wall art) comes from each game's Style.
    // บาส 2026-10-22: the games install it floor-only (wall = null); the wall cards stay available for a game that wants them.
    public sealed class PlayerPadSelect : MonoBehaviour, IPlayerPadView
    {
        public sealed class Style
        {
            public string Title = "CHOOSE YOUR PLAYER", Hint = "STAND ON A PAD FOR 2 SECONDS";
            public TMP_FontAsset TitleFont, BodyFont;
            public Color WallGround = new Color(0f, 0f, 0f, .55f);   // behind the wall cards when there is no WallBackground
            public Color Ground = new Color(.04f, .05f, .08f, .96f), Plate = new Color(.1f, .1f, .14f), Ink = Color.white, Accent = new Color(1f, .82f, .3f);
            public Func<int, Color> PadColor = k => Color.white;
            public Func<int, string> PadLabel = k => "P" + (k + 1);
            public Func<int, string> PadCaption;                 // a second line under the label (e.g. the team)
            public Func<int, int, Rect> PadRect;                 // pad k of n, normalised floor rect; null = a centred row
            // The RectTransform player positions are local to (its positions are local to it), when that isn't the floor
            // the pads sit on (e.g. a characterPanel): positions are carried across through world space. null = the same.
            public Func<RectTransform> InputSpace;
            public Texture WallBackground, WallLogo;
            public UiThemeKit.Shape Shape = UiThemeKit.Shape.Rounded;
            public Action<TMP_Text, bool, Color> StyleText;      // (text, heading?, face colour); null = font + colour + outline
            // บาส 2026-10-27 (Boss Slayer): art instead of the plates. The Decorate hooks run inside Build, after the plain
            // parts exist, so their objects are baked with the screen; they may re-place "Number" / "State" / "Status".
            public bool PlainPads;                               // hide each pad's plate (frame / fill / hold bar)
            public Action<RectTransform> DecorateFloor;          // the floor panel, after the pads and cursors are built
            public Action<RectTransform, int> DecoratePad;       // pad k's root
            public Action<RectTransform, int, bool, float> AnimatePad;   // every frame: pad k's root, owned, hold 0..1
            public Sprite CursorRing;                            // replaces the code-drawn ring (and hides the dot)
            // 2026-11-18 (Boss Slayer, Food Isekai's player look): a player with no pad yet shows Food Isekai's filled
            // ready circle in this colour (null = the ring for everyone), and on picking it crossfades into the ring
            // like Food Isekai's PlayerMarkerTransition. Cursor sizes in real floor metres (0 = the default 130 px).
            public Color? UnpickedCursorDisc;
            public float UnpickedCursorMeters, PickedCursorMeters;
            // บาส 2026-11-18 (Boss Slayer): once a tag has picked pad k its cursor becomes this picture (untinted, the
            // unpicked disc's size) instead of the tinted ring. null = the ring, as before.
            public Func<int, Sprite> PickedCursorSprite;
        }

        public float HoldSeconds = 2f, TimeoutSeconds = 20f, AllInSeconds = 3f;
        public bool Active { get; private set; }
        public int Count { get; private set; }

        // 2026-11-20 (BossSlayer project): Boss Slayer drives the pads from outside (ShowExternal / DrawExternal, fed by
        // BossPlayerReadySelection). Begin's own loop only runs for the Editor mouse; with UWB it finishes at once.
        static bool Live => false;

        Style style;
        RectTransform wallParent, floorParent, floor, wall, cursorLayer;
        Action done;
        bool editorMouse;
        float time, allIn;
        readonly int[] padOwner = new int[6], playerPad = new int[6], holdPad = new int[6];
        readonly float[] hold = new float[6], punch = new float[6];
        readonly bool[] seen = new bool[6];
        readonly RectTransform[] pads = new RectTransform[6], cards = new RectTransform[6], cursors = new RectTransform[6];
        readonly Image[] padFills = new Image[6], padBars = new Image[6], cardFills = new Image[6], cursorRings = new Image[6], cursorDots = new Image[6];
        readonly ReadyTagCircle[] cursorDiscs = new ReadyTagCircle[6];
        readonly float[] pickAge = new float[6];
        float defaultCursorSize = 130;
        // Food Isekai's PlayerMarkerTransition: 0.68 s, the ring grows from 0.94 as the circle grows to 1.12 and fades.
        const float PickBlendSeconds = .68f;
        readonly TMP_Text[] padStates = new TMP_Text[6], cardStates = new TMP_Text[6];
        TMP_Text floorStatus, wallStatus;

        public static PlayerPadSelect Install(MonoBehaviour game, RectTransform wall, RectTransform floor, Style style)
        {
            if (!game.TryGetComponent(out PlayerPadSelect pick)) pick = game.gameObject.AddComponent<PlayerPadSelect>();
            pick.wallParent = wall; pick.floorParent = floor; pick.style = style ?? new Style();
            return pick;
        }

        // count players pick; `finished` runs once (at once without UWB unless the Editor mouse plays P1).
        public void Begin(int count, Action finished, bool editorMouseIsP1 = false)
        {
            Count = Mathf.Clamp(count, 1, 6);
            editorMouse = editorMouseIsP1 && Application.isEditor;
            if (!Live && !editorMouse) { finished?.Invoke(); return; }
            done = finished; time = 0; allIn = 0; Active = true;
            for (int i = 0; i < 6; i++) { padOwner[i] = playerPad[i] = holdPad[i] = -1; hold[i] = 0; punch[i] = 9; seen[i] = false; }
            Build();
            Layout(Count);
            floor.gameObject.SetActive(true); floor.SetAsLastSibling();
            if (wall != null) { wall.gameObject.SetActive(true); wall.SetAsLastSibling(); }
            Refresh();
        }

        public void Cancel() { Active = false; external = false; done = null; Hide(); }

        void Hide()
        {
            if (floor != null) floor.gameObject.SetActive(false);
            if (wall != null) wall.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ external rules (2026-11-18)
        // Boss Slayer uses Food Isekai's player system (BossPlayerReadySelection): that class owns the rules (who joins,
        // holds, claims, when the game starts) and this screen only draws what it is told. No timers, no callback.
        bool external;

        // Shows `count` pads with nobody on them yet.
        public void ShowExternal(int count)
        {
            Count = Mathf.Clamp(count, 1, 6);
            external = true; Active = true; editorMouse = false; done = null;
            for (int i = 0; i < 6; i++) { padOwner[i] = playerPad[i] = holdPad[i] = -1; hold[i] = 0; punch[i] = 9; seen[i] = false; }
            Build();
            Layout(Count);
            floor.gameObject.SetActive(true); floor.SetAsLastSibling();
            if (wall != null) { wall.gameObject.SetActive(true); wall.SetAsLastSibling(); }
        }

        // 2026-11-19: the screen's floor (backdrop, title) with no pads and no cursors, e.g. while Boss Slayer waits for
        // the launcher's START. ShowExternal brings the pads in later.
        public void ShowBackdrop()
        {
            Build();
            Layout(0);
            foreach (var cursor in cursors) if (cursor != null) cursor.gameObject.SetActive(false);
            floor.gameObject.SetActive(true); floor.SetAsLastSibling();
            if (wall != null) { wall.gameObject.SetActive(true); wall.SetAsLastSibling(); }
        }

        public bool Showing => floor != null && floor.gameObject.activeSelf;

        // Re-lays the row for a new pad count (a tag joined or left before picking).
        public void ResizeExternal(int count)
        {
            if (!external) return;
            Count = Mathf.Clamp(count, 1, 6);
            Layout(Count);
        }

        // One frame of the screen. cursorAt[z] is cursor z's 0..1 floor position (null = hidden), cursorPad[z] the pad it
        // owns (-1 none), holdPadOf[z] / holdProgress[z] the pad it is holding and how far (0..1), padOwner[k] the cursor
        // that owns pad k (-1 free). Pads that change owner get the claim punch.
        public void DrawExternal(Vector2?[] cursorAt, int[] cursorPad, int[] holdPadOf, float[] holdProgress, int[] padOwners, string status)
        {
            if (!external || !Active) return;
            float dt = Time.unscaledDeltaTime;
            for (int k = 0; k < 6; k++)
            {
                punch[k] += dt;
                int owner = k < padOwners.Length ? padOwners[k] : -1;
                if (owner >= 0 && padOwner[k] < 0) punch[k] = 0;
                padOwner[k] = owner;
            }
            for (int z = 0; z < 6; z++)
            {
                bool listed = z < cursorAt.Length;
                int pad = listed ? cursorPad[z] : -1;
                if (pad >= 0 && playerPad[z] < 0) pickAge[z] = 0;
                pickAge[z] += dt;
                playerPad[z] = pad;
                holdPad[z] = listed ? holdPadOf[z] : -1;
                hold[z] = listed ? Mathf.Clamp01(holdProgress[z]) * HoldSeconds : 0;
                ShowCursor(z, listed ? cursorAt[z] : null);
            }
            Refresh(status);
        }

        // The pad (0-based) under a 0..1 floor position, or -1.
        public int PadAtNormalized(Vector2 normalized) => PadAt(normalized);

        // A point local to the input RectTransform (Style.InputSpace) as a 0..1 position on the pad floor.
        public Vector2? InputLocalToFloorNormalized(Vector2 inputLocal)
        {
            if (floorParent == null) return null;
            var space = style.InputSpace?.Invoke();
            var onFloor = space == null || space == floorParent ? inputLocal : (Vector2)floorParent.InverseTransformPoint(space.TransformPoint(inputLocal));
            var r = floorParent.rect;
            return new Vector2(Mathf.InverseLerp(r.xMin, r.xMax, onFloor.x), Mathf.InverseLerp(r.yMin, r.yMax, onFloor.y));
        }

        void Update()
        {
            if (!Active || external) return;
            float dt = Time.unscaledDeltaTime;
            time += dt;
            for (int k = 0; k < Count; k++) punch[k] += dt;
            for (int z = 0; z < Count; z++)
            {
                var at = Position(z);
                ShowCursor(z, at);
                if (!at.HasValue) { hold[z] = 0; holdPad[z] = -1; continue; }
                seen[z] = true;
                // บาส 2026-10-26: a pick is final; a player who owns a pad can't take another one
                if (playerPad[z] >= 0) { hold[z] = 0; holdPad[z] = -1; continue; }
                int pad = PadAt(at.Value);
                if (pad < 0 || padOwner[pad] >= 0) { hold[z] = 0; holdPad[z] = -1; continue; }
                if (holdPad[z] != pad) { holdPad[z] = pad; hold[z] = 0; }
                hold[z] += dt;
                if (hold[z] < HoldSeconds) continue;
                padOwner[pad] = z; playerPad[z] = pad; hold[z] = 0; punch[pad] = 0; pickAge[z] = 0;
            }
            // Everyone who is on the floor has a pad (a tag never seen, or P2-P6 under the Editor mouse, is filled in).
            bool everyone = false;
            for (int z = 0; z < Count; z++) if (seen[z]) { everyone = true; break; }
            for (int z = 0; z < Count; z++) if (seen[z] && playerPad[z] < 0) everyone = false;
            allIn = everyone ? allIn + dt : 0;
            if ((everyone && allIn >= AllInSeconds) || time >= TimeoutSeconds) { Finish(); return; }
            Refresh();
        }

        // Normalised floor position of the player now in slot z (UWB), or the Editor mouse for P1.
        Vector2? Position(int z)
        {
            if (editorMouse)
            {
                if (z != 0 || floorParent == null) return null;
                var canvas = floorParent.GetComponentInParent<Canvas>();
                var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(floorParent, (UnityEngine.InputSystem.Mouse.current != null ? UnityEngine.InputSystem.Mouse.current.position.ReadValue() : Vector2.zero), cam, out var local)) return null;
                var r = floorParent.rect;
                var n = new Vector2(Mathf.InverseLerp(r.xMin, r.xMax, local.x), Mathf.InverseLerp(r.yMin, r.yMax, local.y));
                return n.x > 0 && n.x < 1 && n.y > 0 && n.y < 1 ? n : (Vector2?)null;
            }
            return null;
        }

        int PadAt(Vector2 n)
        {
            for (int k = 0; k < Count; k++)
            {
                if (pads[k] == null) continue;
                var a = pads[k].anchorMin; var b = pads[k].anchorMax;
                if (n.x >= a.x && n.x <= b.x && n.y >= a.y && n.y <= b.y) return k;
            }
            return -1;
        }

        void Finish()
        {
            for (int z = 0; z < Count; z++)   // late / unseen players get the free pads in order
            {
                if (playerPad[z] >= 0) continue;
                for (int k = 0; k < Count; k++) if (padOwner[k] < 0) { padOwner[k] = z; playerPad[z] = k; break; }
            }
            Active = false; Hide();
            var callback = done; done = null;
            callback?.Invoke();
        }

        void ShowCursor(int z, Vector2? at)
        {
            if (cursors[z] == null) return;
            cursors[z].gameObject.SetActive(at.HasValue);
            if (!at.HasValue) return;
            cursors[z].anchorMin = cursors[z].anchorMax = at.Value; cursors[z].anchoredPosition = Vector2.zero;
            bool picked = playerPad[z] >= 0;
            var col = picked ? style.PadColor(playerPad[z]) : Color.white;
            float pickedSize = CursorPixels(style.PickedCursorMeters);
            cursors[z].sizeDelta = Vector2.one * pickedSize;
            if (!style.UnpickedCursorDisc.HasValue || cursorDiscs[z] == null)
            {
                if (cursorRings[z] != null) cursorRings[z].color = col;
                return;
            }
            // Food Isekai look: the filled ready circle until a pad is picked, then a crossfade into the ring
            float blend = picked ? Mathf.SmoothStep(0, 1, Mathf.Clamp01(pickAge[z] / PickBlendSeconds)) : 0;
            float incoming = picked ? Mathf.SmoothStep(0, 1, Mathf.Clamp01(pickAge[z] / PickBlendSeconds / .9f)) : 0;
            float outgoing = picked ? Mathf.SmoothStep(0, 1, Mathf.Clamp01((pickAge[z] / PickBlendSeconds - .1f) / .9f)) : 0;
            var disc = cursorDiscs[z];
            var discColor = style.UnpickedCursorDisc.Value;
            discColor.a *= 1 - outgoing;
            disc.enabled = discColor.a > .001f;
            disc.color = discColor;
            disc.rectTransform.sizeDelta = Vector2.one * CursorPixels(style.UnpickedCursorMeters) * Mathf.Lerp(1f, 1.12f, blend);
            var marker = picked && style.PickedCursorSprite != null ? style.PickedCursorSprite(playerPad[z]) : null;
            if (cursorRings[z] != null)
            {
                var ring = cursorRings[z];
                var sprite = marker != null ? marker : RingSprite();
                if (ring.sprite != sprite) { ring.sprite = sprite; ring.preserveAspect = marker != null; }
                if (marker != null) col = Color.white;   // the picture carries the player's colour
                col.a *= incoming;
                ring.color = col;
                ring.enabled = incoming > .001f;
                // the ring fills the cursor (picked size); a marker is drawn at the unpicked disc's size
                float size = marker != null ? CursorPixels(style.UnpickedCursorMeters) / Mathf.Max(1f, pickedSize) : 1f;
                ring.rectTransform.localScale = Vector3.one * size * Mathf.Lerp(.94f, 1f, blend);
            }
            if (cursorDots[z] != null) cursorDots[z].enabled = picked && style.CursorRing == null && marker == null;
        }

        Sprite RingSprite() => style.CursorRing != null ? style.CursorRing : Circle(true);

        // A real floor size in metres as floor-panel units (0 = the default ring size).
        float CursorPixels(float meters)
        {
            if (meters <= 0 || floorParent == null) return defaultCursorSize;
            float floorMeters = BossFloorPlayers.ActiveFloorWidthMeters;
            return meters * floorParent.rect.width / floorMeters;
        }

        void Refresh(string statusOverride = null)
        {
            int taken = 0;
            for (int k = 0; k < Count; k++)
            {
                bool owned = padOwner[k] >= 0; if (owned) taken++;
                float progress = 0; for (int z = 0; z < Count; z++) if (holdPad[z] == k) progress = Mathf.Max(progress, hold[z] / HoldSeconds);
                var col = style.PadColor(k);
                if (padFills[k] != null) padFills[k].color = owned ? Color.Lerp(style.Plate, col, .45f) : style.Plate;
                if (padBars[k] != null) { padBars[k].fillAmount = owned ? 1 : progress; padBars[k].color = col; }
                if (padStates[k] != null) padStates[k].text = owned ? "READY" : progress > 0 ? "HOLD..." : "STAND HERE";
                if (pads[k] != null) pads[k].localScale = Vector3.one * UiThemeKit.Punch(punch[k], .12f);
                if (pads[k] != null) style.AnimatePad?.Invoke(pads[k], k, owned, progress);
                if (cardFills[k] != null) cardFills[k].color = owned ? Color.Lerp(style.Plate, col, .45f) : style.Plate;
                if (cardStates[k] != null) cardStates[k].text = owned ? "READY" : "...";
                if (cards[k] != null) cards[k].localScale = Vector3.one * UiThemeKit.Punch(punch[k], .12f);
            }
            string line = taken == Count ? $"ALL READY  •  STARTING IN {Mathf.Max(1, Mathf.CeilToInt(AllInSeconds - allIn))}"
                : $"{taken} / {Count} READY  •  AUTO IN {Mathf.Max(1, Mathf.CeilToInt(TimeoutSeconds - time))}";
            if (statusOverride != null) line = statusOverride;
            if (floorStatus != null) floorStatus.text = line;
            if (wallStatus != null) wallStatus.text = line;
        }

        // ------------------------------------------------------------------ UI (find-or-create by name: Play and bakes)
        const string FloorName = "Panel == Player Select", WallName = "Panel == Player Select Wall";

        void Text(TMP_Text t, bool heading, Color face, TMP_FontAsset font)
        {
            if (t == null) return;
            if (style.StyleText != null) { style.StyleText(t, heading, face); return; }
            if (font != null) t.font = font;
            t.color = face; t.fontStyle = FontStyles.Bold; t.raycastTarget = false;
            UiThemeKit.ApplyOutline(t, .2f, new Color(0, 0, 0, .9f));
        }

        // Builds (or finds) both panels; hidden until Begin. Safe in Edit mode for a bake.
        public void Build()
        {
            if (floorParent == null) return;
            if (floor == null)
            {
                floor = UiThemeKit.Child(floorParent, FloorName);
                UiThemeKit.Stretch(floor, 0);
                UiThemeKit.Paint(floor, style.Ground);
                float fu = Mathf.Max(1, floorParent.rect.height) / (float)FloorDisplaySettings.Height;
                var heading = UiThemeKit.Text(floor, "Title", style.TitleFont);
                UiThemeKit.PlaceText(heading, floor, .15f, .82f, .85f, .95f, 110 * fu);
                Text(heading, true, style.Accent, style.TitleFont); heading.text = style.Title;
                var hint = UiThemeKit.Text(floor, "Hint", style.BodyFont);
                UiThemeKit.PlaceText(hint, floor, .15f, .74f, .85f, .82f, 52 * fu);
                Text(hint, false, style.Ink, style.BodyFont); hint.text = style.Hint;
                floorStatus = UiThemeKit.Text(floor, "Status", style.TitleFont);
                UiThemeKit.PlaceText(floorStatus, floor, .15f, .07f, .85f, .17f, 70 * fu);
                Text(floorStatus, true, style.Accent, style.TitleFont);
                for (int k = 0; k < 6; k++)
                {
                    var col = style.PadColor(k);
                    var plate = UiThemeKit.MakePlate(floor, "Player Pad " + (k + 1), col, style.Plate, 26 * fu, 8 * fu, true, style.Accent, true, style.Shape);
                    pads[k] = plate.rect; padFills[k] = plate.fill;
                    var label = UiThemeKit.Text(plate.rect, "Number", style.TitleFont);
                    UiThemeKit.PlaceText(label, plate.rect, .05f, .42f, .95f, .90f, 190 * fu);
                    Text(label, true, col, style.TitleFont); label.text = style.PadLabel(k);
                    if (style.PadCaption != null)
                    {
                        var caption = UiThemeKit.Text(plate.rect, "Caption", style.BodyFont);
                        UiThemeKit.PlaceText(caption, plate.rect, .05f, .33f, .95f, .44f, 40 * fu);
                        Text(caption, false, col, style.BodyFont); caption.text = style.PadCaption(k);
                    }
                    padStates[k] = UiThemeKit.Text(plate.rect, "State", style.BodyFont);
                    UiThemeKit.PlaceText(padStates[k], plate.rect, .05f, .16f, .95f, .32f, 46 * fu);
                    Text(padStates[k], false, style.Ink, style.BodyFont);
                    var bar = UiThemeKit.Child(plate.rect, "Hold Bar");
                    UiThemeKit.Place(bar, .1f, .06f, .9f, .12f);
                    if (!bar.TryGetComponent(out padBars[k])) padBars[k] = bar.gameObject.AddComponent<Image>();
                    padBars[k].sprite = UiThemeKit.SpriteFor(UiThemeKit.Shape.Rounded, UiThemeKit.Solid); padBars[k].type = Image.Type.Filled;
                    padBars[k].fillMethod = Image.FillMethod.Horizontal; padBars[k].fillAmount = 0; padBars[k].raycastTarget = false;
                    if (style.PlainPads)
                        foreach (var part in new[] { plate.frame, plate.fill, plate.trim, plate.glow, padBars[k] }) if (part != null) part.enabled = false;
                    style.DecoratePad?.Invoke(plate.rect, k);
                }
                cursorLayer = UiThemeKit.Child(floor, "Cursors");
                UiThemeKit.Stretch(cursorLayer, 0);
                float size = defaultCursorSize = 130 * fu;
                for (int z = 0; z < 6; z++)
                {
                    var c = UiThemeKit.Child(cursorLayer, "Cursor " + (z + 1));
                    c.sizeDelta = new Vector2(size, size); c.pivot = new Vector2(.5f, .5f);
                    var ring = UiThemeKit.Child(c, "Ring"); UiThemeKit.Stretch(ring, 0);
                    if (!ring.TryGetComponent(out cursorRings[z])) cursorRings[z] = ring.gameObject.AddComponent<Image>();
                    cursorRings[z].sprite = style.CursorRing != null ? style.CursorRing : Circle(true); cursorRings[z].raycastTarget = false;
                    var dot = UiThemeKit.Child(c, "Dot"); UiThemeKit.Place(dot, .36f, .36f, .64f, .64f);
                    if (!dot.TryGetComponent(out Image dotImage)) dotImage = dot.gameObject.AddComponent<Image>();
                    dotImage.sprite = Circle(false); dotImage.raycastTarget = false; dotImage.enabled = style.CursorRing == null;
                    cursorDots[z] = dotImage;
                    var disc = UiThemeKit.Child(c, "Tag Circle"); disc.SetAsFirstSibling();
                    disc.anchorMin = disc.anchorMax = disc.pivot = new Vector2(.5f, .5f); disc.anchoredPosition = Vector2.zero;
                    if (!disc.TryGetComponent(out cursorDiscs[z])) cursorDiscs[z] = disc.gameObject.AddComponent<ReadyTagCircle>();
                    cursorDiscs[z].raycastTarget = false; cursorDiscs[z].enabled = false;
                    cursors[z] = c; c.gameObject.SetActive(false);
                }
                style.DecorateFloor?.Invoke(floor);
            }
            if (wall == null && wallParent != null)
            {
                wall = UiThemeKit.Child(wallParent, WallName);
                UiThemeKit.Stretch(wall, 0);
                float u = Mathf.Max(1, wallParent.rect.height) / 2160f;
                var bg = UiThemeKit.Child(wall, "Background"); UiThemeKit.Stretch(bg, 0);
                if (!bg.TryGetComponent(out RawImage bgImage)) bgImage = bg.gameObject.AddComponent<RawImage>();
                bgImage.texture = style.WallBackground; bgImage.color = style.WallBackground != null ? Color.white : style.WallGround; bgImage.raycastTarget = false;
                float top = .56f;
                if (style.WallLogo != null)
                {
                    var logo = UiThemeKit.Child(wall, "Logo");
                    float aspect = wallParent.rect.width / Mathf.Max(1f, wallParent.rect.height), h = .40f, w = h * style.WallLogo.width / style.WallLogo.height / Mathf.Max(.01f, aspect);
                    UiThemeKit.Place(logo, .5f - w / 2, .57f, .5f + w / 2, .97f);
                    if (!logo.TryGetComponent(out RawImage logoImage)) logoImage = logo.gameObject.AddComponent<RawImage>();
                    logoImage.texture = style.WallLogo; logoImage.raycastTarget = false;
                }
                else top = .80f;
                var heading = UiThemeKit.Text(wall, "Title", style.TitleFont);
                UiThemeKit.PlaceText(heading, wall, .25f, top - .09f, .75f, top, 120 * u);
                Text(heading, true, style.Accent, style.TitleFont); heading.text = style.Title;
                for (int k = 0; k < 6; k++)
                {
                    var col = style.PadColor(k);
                    var card = UiThemeKit.MakePlate(wall, "Player Card " + (k + 1), col, style.Plate, 30 * u, 8 * u, true, style.Accent, false, style.Shape);
                    cards[k] = card.rect; cardFills[k] = card.fill;
                    var label = UiThemeKit.Text(card.rect, "Number", style.TitleFont);
                    UiThemeKit.PlaceText(label, card.rect, .05f, .38f, .95f, .92f, 150 * u);
                    Text(label, true, col, style.TitleFont); label.text = style.PadLabel(k);
                    cardStates[k] = UiThemeKit.Text(card.rect, "State", style.BodyFont);
                    UiThemeKit.PlaceText(cardStates[k], card.rect, .05f, .1f, .95f, .36f, 70 * u);
                    Text(cardStates[k], false, style.Ink, style.BodyFont);
                }
                wallStatus = UiThemeKit.Text(wall, "Status", style.TitleFont);
                UiThemeKit.PlaceText(wallStatus, wall, .2f, .04f, .8f, .13f, 110 * u);
                Text(wallStatus, true, style.Accent, style.TitleFont);
            }
            if (!Active) Hide();
        }

        static Sprite ring, disc;
        // Cursor art made in code (a soft-edged ring / disc), so the screen needs no scene sprites.
        static Sprite Circle(bool hollow)
        {
            var cached = hollow ? ring : disc;
            if (cached != null) return cached;
            const int n = 128; var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = hollow ? "Pad Cursor Ring" : "Pad Cursor Dot" };
            var px = new Color32[n * n]; float h = n / 2f;
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float d = new Vector2(x + .5f - h, y + .5f - h).magnitude / h;
                float a = Mathf.Clamp01((1 - d) * h); if (hollow) a *= Mathf.Clamp01((d - .78f) * h);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px); tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(.5f, .5f), 100);
            if (hollow) ring = sprite; else disc = sprite;
            return sprite;
        }

        public void Layout(int n)
        {
            float gap = .025f, width = Mathf.Min(.19f, (.80f - gap * (n - 1)) / n), total = n * width + (n - 1) * gap, x0 = .5f - total / 2;
            float wallW = Mathf.Min(.12f, (.70f - .012f * (n - 1)) / n), wallTotal = n * wallW + (n - 1) * .012f, wx0 = .5f - wallTotal / 2;
            float wallTop = style.WallLogo != null ? .44f : .66f;
            for (int k = 0; k < 6; k++)
            {
                bool on = k < n;
                if (pads[k] != null)
                {
                    pads[k].gameObject.SetActive(on);
                    if (on)
                    {
                        var r = style.PadRect != null ? style.PadRect(k, n) : new Rect(x0 + k * (width + gap), .25f, width, .43f);
                        UiThemeKit.Place(pads[k], r.xMin, r.yMin, r.xMax, r.yMax);
                    }
                }
                if (cards[k] != null) { cards[k].gameObject.SetActive(on); if (on) UiThemeKit.Place(cards[k], wx0 + k * (wallW + .012f), wallTop - .28f, wx0 + k * (wallW + .012f) + wallW, wallTop); }
            }
        }
    }
}
