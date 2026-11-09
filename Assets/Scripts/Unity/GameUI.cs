// Copper Camp — the Unity front end (uGUI, built entirely in code at runtime).
// Port of the browser game's render / statsHTML / phaseHTML / deltaHTML / timed / mountUI and its audio routing.
// Add this component to any GameObject in an empty scene; it builds its own Canvas and EventSystem.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

namespace CopperCamp
{
    public class GameUI : MonoBehaviour, IFrontEnd
    {
        public static GameUI Instance { get; private set; }
        /// <summary>True while the player is typing their name, so keys don't walk or pick choices.</summary>
        public static bool TypingInField => Instance != null && Instance.nameField != null && Instance.nameField.isFocused;

        /// <summary>Fraction of the screen width covered by the right-hand panel (for the 3D camera).</summary>
        public float PanelWidthFraction => 0.4f;

        /// <summary>Whether the right-hand panel is showing.</summary>
        public bool PanelVisible
        {
            get => panelVisible;
            set { panelVisible = value; if (panelRoot != null) panelRoot.SetActive(value); }
        }

        /// <summary>Set by the town code while the player is walking (plays the boots loop in walk mode).</summary>
        public bool Walking;

        // ---------------- palette ----------------
        static readonly Color C_PANEL = Hex("#16100c", .92f);
        static readonly Color C_TEXT = Hex("#e8dcc4");
        static readonly Color C_SOFT = Hex("#b49a7c");
        static readonly Color C_COPPER = Hex("#c87533");
        static readonly Color C_BRASS = Hex("#d9a441");
        static readonly Color C_VERDI = Hex("#6f9a8d");
        static readonly Color C_BLOOD = Hex("#aa3333");
        static readonly Color C_BTN = Hex("#2a1f18");
        static readonly Color C_BTN_HI = Hex("#3d2d22");
        static readonly Color C_BTN_DN = Hex("#4d3727");
        static readonly Color C_RULE = Hex("#5a4636");
        static readonly Color C_TRACK = Hex("#2e241d");
        static readonly Color C_HUD = Hex("#120d0a", .86f);
        static readonly Color C_WHO = Hex("#d9c9a8");
        static readonly Color C_MAIN_DN = Hex("#8b4418");
        static readonly Color C_MAIN_EDGE = Hex("#6b3a18");
        const string GOOD = "#7fc0ac", BAD = "#e8806c", NOTE = "#b49a7c", KEYNUM = "#8f7b64";

        const int BODY = 17, TITLE = 26, BTN_T = 18, BTN_D = 14;
        const string SAVE_KEY = "copperCamp", SND_KEY = "copperCampSound";
        const float STEP = 1f / 60f;

        // ---------------- widgets ----------------
        Font font;
        bool panelVisible = true;
        GameObject panelRoot, hudRoot, captionRoot, normalRoot, shiftRoot, phaseRow, cardRow, nameRow, timerRoot, ledgerRoot;
        ScrollRect scroll;
        RectTransform content, choicesRoot, sBtnRoot;
        Text moneyText, skillText, whoText, captionText, titleText, deltaText, bodyText, cardName, cardBody, initialsText;
        Text turnText, timerNote, ledgerHead, ledgerBody, sndText;
        Text sTitle, sNote, sHelp, sMsg, sClock;
        readonly Text[] phaseText = new Text[3];
        readonly Image[] phaseImg = new Image[3];
        Chip[] chips;
        RawImage portraitImg;
        GameObject portraitBox;
        InputField nameField;
        RectTransform timerFill, gaugeFill, extraFill, meterZone, meterMark;
        GameObject meterGo, extraGo;

        // ---------------- state ----------------
        List<Choice> currentChoices = new List<Choice>();
        int serial;
        bool paperShown; string saloonTune;
        // read by the self-test (AutoPlay) and nothing else
        public List<Choice> CurrentChoices => currentChoices;
        public int Serial => serial;
        public bool HasTimer => timerFn != null;
        bool shiftMode, ledgerOpen;
        Action timerFn;
        float timerLeft, timerTotal;
        float tickAcc, pendingLeft, hudClock;
        bool pendingArmed;
        GameState prevState;
        int[] prevSnap;
        string lastCaption, lastMsg;
        int lastSecs = -1, lastPct = -1, lastDust = -1;
        readonly List<ShiftBtn> sBtns = new List<ShiftBtn>();
        readonly Dictionary<string, Texture2D> photos = new Dictionary<string, Texture2D>();

        // ---------------- audio ----------------
        bool soundOn = true;
        AudioSource sfxSrc;
        XFade ambX, musicX;
        readonly Dictionary<string, Voice> loops = new Dictionary<string, Voice>();
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();

        sealed class Chip { public Text label, num; public RectTransform fill; public Image fillImg; }
        sealed class ShiftBtn { public string id, last; public bool hold, main, down; public Image img; public Text txt; }

        /// <summary>One looping AudioSource that eases its volume toward a target.</summary>
        sealed class Voice
        {
            public AudioSource src; public float target, rate; public string key;
            public void Tick(float dt)
            {
                if (src == null) return;
                src.volume = Mathf.MoveTowards(src.volume, target, rate * dt);
                if (target <= 0f && src.volume <= 0.001f && src.isPlaying) src.Stop();
            }
        }

        /// <summary>Two voices that crossfade over 1.5 s when the key (clip name) changes.</summary>
        sealed class XFade
        {
            const float T = 1.5f;
            public Voice a, b;
            public void Set(string key, float vol, Func<string, AudioClip> load)
            {
                if (key == a.key) { a.target = key == null ? 0f : vol; return; }
                var t = a; a = b; b = t;
                b.target = 0f; b.rate = Mathf.Max(.01f, b.src.volume) / T;
                a.key = key; a.src.Stop(); a.src.volume = 0f; a.target = 0f;
                var clip = key == null ? null : load(key);
                if (clip != null) { a.src.clip = clip; a.src.Play(); a.target = vol; a.rate = vol / T; }
            }
            public void Tick(float dt) { a.Tick(dt); b.Tick(dt); }
        }

        // =====================================================================
        // lifecycle
        // =====================================================================
        void Awake()
        {
            Instance = this;
            CC.ShiftView = ShowShift;
            font = Resources.Load<Font>("CC/font/DejaVuSerif");   // open-licensed (DejaVu): has the dashes and curly quotes the story text uses
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            soundOn = PlayerPrefs.GetInt(SND_KEY, 1) != 0;
            EnsureEventSystem();
            BuildUI();
            BuildAudio();
            // a self-test that plays the whole game by itself, only when the page address carries ?autotest
            if (Application.absoluteURL != null && Application.absoluteURL.Contains("autotest")) gameObject.AddComponent<AutoPlay>();
        }

        void Start()
        {
            CC.UI = this;
            try { CC.Title(); } catch (Exception e) { Debug.LogException(e); }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (ReferenceEquals(CC.UI, this)) CC.UI = null;
            if (CC.ShiftView == (Action<ShiftState>)ShowShift) CC.ShiftView = null;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            HandleKeys();

            // the running shift, ticked at a fixed 60 Hz (at most 4 steps a frame)
            if (CC.Shift != null)
            {
                tickAcc += dt;
                int n = 0;
                while (tickAcc >= STEP && n < 4)
                {
                    tickAcc -= STEP; n++;
                    try { CC.ShiftTick(); } catch (Exception e) { Debug.LogException(e); }
                    if (CC.Shift == null) break;
                }
                if (tickAcc > STEP) tickAcc = 0f;
            }
            else tickAcc = 0f;

            // the call FinishShift leaves behind, after its delay
            if (CC.PendingAfterShift != null)
            {
                if (!pendingArmed) { pendingArmed = true; pendingLeft = CC.PendingDelay; }
                pendingLeft -= dt;
                if (pendingLeft <= 0f)
                {
                    var a = CC.PendingAfterShift;
                    CC.PendingAfterShift = null; pendingArmed = false;
                    try { a(); } catch (Exception e) { Debug.LogException(e); }
                }
            }
            else pendingArmed = false;

            // timed choice
            if (timerFn != null)
            {
                timerLeft -= dt;
                SetFill(timerFill, timerTotal > 0f ? timerLeft / timerTotal : 0f);
                if (timerLeft <= 0f)
                {
                    var f = timerFn;
                    StopTimer();
                    try { f(); } catch (Exception e) { Debug.LogException(e); }
                }
            }

            if (shiftMode && CC.Shift != null) UpdateShift(CC.Shift);

            hudClock -= dt;
            if (hudClock <= 0f) { hudClock = .25f; RefreshHud(); }
            RefreshCaption();
            UpdateAudio(dt);
        }

        // =====================================================================
        // IFrontEnd
        // =====================================================================
        public void Show(string title, string body, List<Choice> choices, string portraitId, string speakerName)
        {
            StopTimer();
            serial++;
            paperShown = body != null && body.Contains("class=\"paper\"");
            shiftMode = false;
            shiftRoot.SetActive(false);
            normalRoot.SetActive(true);
            ReleaseShiftButtons();

            RefreshHud();
            RefreshPhase();

            titleText.gameObject.SetActive(!string.IsNullOrEmpty(title));
            titleText.text = Html(title, TITLE);

            string d = DeltaLine();
            deltaText.gameObject.SetActive(d.Length > 0);
            deltaText.text = d;

            string rich = Html(body, BODY);
            if (!string.IsNullOrEmpty(portraitId))
            {
                // like the JS talk(): portrait on the left, name and words on the right
                cardRow.SetActive(true);
                bodyText.gameObject.SetActive(false);
                cardName.text = "<b>" + Html(speakerName ?? "", 18) + "</b>";
                cardBody.text = rich;
                SetPortrait(portraitId, speakerName);
            }
            else
            {
                cardRow.SetActive(false);
                bodyText.gameObject.SetActive(rich.Length > 0);
                bodyText.text = rich;
            }

            nameRow.SetActive(CC.TitleMode);
            BuildChoices(choices);
            RefreshLedger();
            ScrollTop();
            Deselect();
        }

        public void Timed(float seconds, Action onExpire)
        {
            StopTimer();
            if (seconds <= 0f || onExpire == null) return;
            timerFn = onExpire;
            timerTotal = timerLeft = seconds;
            SetFill(timerFill, 1f);
            timerNote.text = seconds.ToString("0.##", CultureInfo.InvariantCulture) + " seconds. Then he decides for you.";
            timerRoot.SetActive(true);
        }

        public void Sfx(string name)
        {
            if (!soundOn || string.IsNullOrEmpty(name) || sfxSrc == null) return;
            if (name == "thud") name = "throwrock";
            var c = Clip(name);
            if (c != null) sfxSrc.PlayOneShot(c, .9f);
        }

        public void SaveText(string text)
        {
            if (text == null) PlayerPrefs.DeleteKey(SAVE_KEY);
            else PlayerPrefs.SetString(SAVE_KEY, text);
            PlayerPrefs.Save();
        }

        public string LoadText() => PlayerPrefs.HasKey(SAVE_KEY) ? PlayerPrefs.GetString(SAVE_KEY) : null;

        public string AskName() => nameField != null && nameField.text != null ? nameField.text.Trim() : "";

        // =====================================================================
        // the shift panel (JS mountUI + the per-frame part of runShift)
        // =====================================================================
        public void ShowShift(ShiftState U)
        {
            if (U == null)
            {
                shiftMode = false;
                ReleaseShiftButtons();
                shiftRoot.SetActive(false);
                normalRoot.SetActive(true);
                return;
            }
            shiftMode = true;
            normalRoot.SetActive(false);
            shiftRoot.SetActive(true);
            RefreshHud();
            RefreshPhase();
            RefreshLedger();

            sTitle.text = Html(Get(CC.GAME_TITLE, U.kind), TITLE);
            string mn = U.job != null && U.job.loc != null && CC.MINE.TryGetValue(U.job.loc, out var m) ? CC.Cap(m) : "";
            sNote.text = mn + (U.stance == "push" ? " · pushing for the bonus" : U.stance == "careful" ? " · working it careful" : "");
            sHelp.text = Html(Get(CC.GAME_HELP, U.kind), 15);
            meterGo.SetActive(CC.GameHasMeter(U.kind));
            extraGo.SetActive(CC.GameHasExtra(U.kind));
            lastMsg = null; lastSecs = lastPct = lastDust = -1;
            BuildShiftButtons(U);
            UpdateShift(U);
            ScrollTop();
            Deselect();
        }

        void BuildShiftButtons(ShiftState U)
        {
            ClearChildren(sBtnRoot);
            sBtns.Clear();
            if (U.buttons != null)
            {
                foreach (var b in U.buttons)
                {
                    var e = new ShiftBtn { id = b.id, hold = b.hold, main = b.main };
                    var go = Mk("Btn_" + b.id, sBtnRoot);
                    e.img = go.AddComponent<Image>();
                    e.img.color = b.main ? C_COPPER : C_BTN;
                    AddOutline(go, b.main ? C_MAIN_EDGE : C_COPPER, b.main ? 2f : 1f);
                    LE(go, -1, b.main ? 64 : 46);
                    e.txt = MkText(go.transform, "Label", b.main ? 21 : 16, b.main ? Color.white : C_TEXT, FontStyle.Bold, TextAnchor.MiddleCenter);
                    Stretch(e.txt.rectTransform, 6);
                    e.txt.text = U.labels.TryGetValue(b.id, out var l) ? l : b.label;
                    e.last = e.txt.text;
                    var et = go.AddComponent<EventTrigger>();
                    var ee = e;
                    AddTrigger(et, EventTriggerType.PointerDown, () => { ee.down = true; CC.ShiftPress(ee.id, true); });
                    Action up = () => { if (!ee.down) return; ee.down = false; if (ee.hold) CC.ShiftPress(ee.id, false); };
                    AddTrigger(et, EventTriggerType.PointerUp, up);
                    AddTrigger(et, EventTriggerType.PointerExit, up);
                    sBtns.Add(e);
                }
            }
            // the skip button is always last
            var sk = MkButton(sBtnRoot, "Skip", out var skText, true);
            skText.text = "Skip to the whistle (take the Company’s odds)";
            sk.onClick.AddListener(() => { Deselect(); try { CC.ShiftSkip(); } catch (Exception ex) { Debug.LogException(ex); } });
        }

        void ReleaseShiftButtons()
        {
            foreach (var e in sBtns)
            {
                if (e.down && e.hold) CC.ShiftPress(e.id, false);
                e.down = false;
            }
        }

        void UpdateShift(ShiftState U)
        {
            if (meterGo.activeSelf)
            {
                float w = Mathf.Clamp01(U.meterW);
                meterZone.anchorMin = new Vector2(.5f - w / 2f, 0f);
                meterZone.anchorMax = new Vector2(.5f + w / 2f, 1f);
                bool show = U.meterMark >= 0f;
                if (meterMark.gameObject.activeSelf != show) meterMark.gameObject.SetActive(show);
                if (show)
                {
                    float x = Mathf.Clamp01(U.meterMark);
                    meterMark.anchorMin = new Vector2(x, 0f);
                    meterMark.anchorMax = new Vector2(x, 1f);
                }
            }
            SetFill(gaugeFill, Mathf.Min(1f, U.prog));
            if (extraGo.activeSelf) SetFill(extraFill, U.extra);

            if (U.msg != lastMsg) { lastMsg = U.msg; sMsg.text = Html(U.msg, 15); }
            int secs = Math.Max(0, Mathf.CeilToInt((U.dur - U.t) / 60f));
            int pct = Math.Min(100, CC.Round(U.prog * 100f));
            int dust = CC.Round(U.dust / 9f);
            if (secs != lastSecs || pct != lastPct || dust != lastDust)
            {
                lastSecs = secs; lastPct = pct; lastDust = dust;
                sClock.text = "Shift: " + secs + " s · " + Get(CC.GAME_UNIT, U.kind) + " " + pct + "% · dust " + dust + "%";
            }

            foreach (var e in sBtns)
            {
                if (U.labels.TryGetValue(e.id, out var l) && l != e.last) { e.last = l; e.txt.text = l; }
                bool on = e.down || (e.hold && U.In(e.id));
                e.img.color = e.main ? (on ? C_MAIN_DN : C_COPPER) : (on ? C_BTN_DN : C_BTN);
            }
        }

        // =====================================================================
        // keyboard: number keys pick choices; Space / B / Left Shift drive the shift
        // =====================================================================
#if ENABLE_INPUT_SYSTEM
        static readonly Key[] DIGITS = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9 };
        static readonly Key[] NUMPAD = { Key.Numpad1, Key.Numpad2, Key.Numpad3, Key.Numpad4, Key.Numpad5, Key.Numpad6, Key.Numpad7, Key.Numpad8, Key.Numpad9 };
#else
        static readonly KeyCode[] DIGITS = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5, KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9 };
        static readonly KeyCode[] NUMPAD = { KeyCode.Keypad1, KeyCode.Keypad2, KeyCode.Keypad3, KeyCode.Keypad4, KeyCode.Keypad5, KeyCode.Keypad6, KeyCode.Keypad7, KeyCode.Keypad8, KeyCode.Keypad9 };
#endif

        void HandleKeys()
        {
            bool mainDown, mainUp, auxDown, auxUp, mod;
            int digit = -1;
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return;
            mainDown = kb.spaceKey.wasPressedThisFrame;
            mainUp = kb.spaceKey.wasReleasedThisFrame;
            auxDown = kb.bKey.wasPressedThisFrame || kb.leftShiftKey.wasPressedThisFrame;
            auxUp = kb.bKey.wasReleasedThisFrame || kb.leftShiftKey.wasReleasedThisFrame;
            mod = kb.ctrlKey.isPressed || kb.altKey.isPressed;
            for (int i = 0; i < 9 && digit < 0; i++)
                if (kb[DIGITS[i]].wasPressedThisFrame || kb[NUMPAD[i]].wasPressedThisFrame) digit = i;
#else
            mainDown = Input.GetKeyDown(KeyCode.Space);
            mainUp = Input.GetKeyUp(KeyCode.Space);
            auxDown = Input.GetKeyDown(KeyCode.B) || Input.GetKeyDown(KeyCode.LeftShift);
            auxUp = Input.GetKeyUp(KeyCode.B) || Input.GetKeyUp(KeyCode.LeftShift);
            mod = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) || Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            for (int i = 0; i < 9 && digit < 0; i++)
                if (Input.GetKeyDown(DIGITS[i]) || Input.GetKeyDown(NUMPAD[i])) digit = i;
#endif
            var U = CC.Shift;
            if (shiftMode && U != null)
            {
                if (mainDown) CC.ShiftPress("main", true);
                if (mainUp) CC.ShiftPress("main", false);
                if (auxDown) CC.ShiftPress("aux", true);
                if (auxUp) CC.ShiftPress("aux", false);
                return;   // no choice keys while the shift panel is up
            }
            if (digit < 0 || mod) return;
            if (U != null && !U.paused && !U.over) return;
            if (nameField != null && nameField.isFocused) return;
            ClickNth(digit);
        }

        void ClickNth(int n)
        {
            int k = 0;
            foreach (var c in currentChoices)
            {
                if (c == null || c.disabled) continue;
                if (k == n) { Choose(c, serial); return; }
                k++;
            }
        }

        void Choose(Choice c, int ser)
        {
            if (c == null || c.disabled || ser != serial || c.go == null) return;
            Deselect();
            try { c.go(); } catch (Exception e) { Debug.LogException(e); }
        }

        // =====================================================================
        // screen pieces
        // =====================================================================
        void BuildChoices(List<Choice> list)
        {
            ClearChildren(choicesRoot);
            currentChoices = list ?? new List<Choice>();
            int n = 0, ser = serial;
            foreach (var c in currentChoices)
            {
                if (c == null) continue;
                bool en = !c.disabled;
                var btn = MkButton(choicesRoot, "Choice", out var t, false);
                string num = en && n < 9 ? "<color=" + KEYNUM + "><size=13>" + (++n) + "</size></color>  " : "";
                t.text = num + "<b>" + Html(c.t, BTN_T) + "</b>";
                if (c.cost != null)
                {
                    int v = c.cost.Value;
                    string cs = v > 0 ? "$" + v : v < 0 ? "+$" + (-v) : "free";
                    var ct = MkText(t.transform.parent, "Cost", 16, C_COPPER, FontStyle.Italic, TextAnchor.UpperRight);
                    ct.horizontalOverflow = HorizontalWrapMode.Overflow;
                    ct.text = cs;
                    LE(ct.gameObject, -1, -1).minWidth = 9 * cs.Length + 8;
                }
                if (!string.IsNullOrEmpty(c.d))
                {
                    var d = MkText(btn.transform, "Desc", BTN_D, C_SOFT);
                    d.text = Html(c.d, BTN_D);
                }
                if (!en)
                {
                    btn.interactable = false;
                    var cg = btn.gameObject.AddComponent<CanvasGroup>();
                    cg.alpha = .45f;
                }
                var cc = c;
                btn.onClick.AddListener(() => Choose(cc, ser));
            }
        }

        /// <summary>A panel button: dark fill, 1 px copper outline, hover tint, a title row and room for a description.</summary>
        Button MkButton(Transform parent, string name, out Text title, bool small)
        {
            var go = Mk(name, parent);
            var img = Img(go, Color.white, true);
            AddOutline(go, small ? C_RULE : C_COPPER, 1f);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.colors = Tints(small ? new Color(0, 0, 0, 0) : C_BTN, C_BTN_HI, C_BTN_DN);
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            VL(go, 2, small ? new RectOffset(10, 10, 5, 5) : new RectOffset(12, 12, 8, 8));
            var row = Mk("Row", go.transform);
            HL(row, 8).childAlignment = TextAnchor.UpperLeft;
            title = MkText(row.transform, "Title", small ? 13 : BTN_T, small ? C_SOFT : C_TEXT, small ? FontStyle.Italic : FontStyle.Normal);
            LE(title.gameObject, -1, -1).flexibleWidth = 1;
            return btn;
        }

        void SetPortrait(string id, string speaker)
        {
            // the Chinese origin's partner and landlady are different people (the JS draws them)
            var o = CC.S?.o;
            string key = o != null && o.id == "chinese" && (id == "partner" || id == "landlady") ? id + "_drawn" : id;
            if (!photos.TryGetValue(key, out var tex))
            {
                tex = Resources.Load<Texture2D>("CC/photo/" + key);
                photos[key] = tex;
            }
            bool has = tex != null;
            portraitImg.gameObject.SetActive(has);
            portraitBox.SetActive(!has);
            if (has)
            {
                portraitImg.texture = tex;
                float w = tex.width, h = tex.height;   // object-fit: cover
                portraitImg.uvRect = w >= h ? new Rect((1f - h / w) / 2f, 0f, h / w, 1f) : new Rect(0f, (1f - w / h) / 2f, 1f, w / h);
            }
            else initialsText.text = Initials(speaker ?? id);
        }

        static string Initials(string nm)
        {
            var parts = Regex.Replace(nm ?? "", "<[^>]*>", "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "?";
            string a = parts[0].Substring(0, 1), b = parts.Length > 1 ? parts[parts.Length - 1].Substring(0, 1) : "";
            return (a + b).ToUpperInvariant();
        }

        void StopTimer()
        {
            timerFn = null;
            timerLeft = timerTotal = 0f;
            if (timerRoot != null) timerRoot.SetActive(false);
        }

        /// <summary>JS deltaHTML: what the last choice changed, compared with the previous screen.</summary>
        string DeltaLine()
        {
            var S = CC.S;
            if (S == null || S.ended) return "";
            if (!ReferenceEquals(S, prevState)) { prevState = S; prevSnap = null; }
            var now = new[] { S.money, CC.Round(S.health), CC.Round(S.lungs), CC.Round(S.morale), CC.Round(S.rep), CC.Round(S.heat) };
            var prev = prevSnap;
            prevSnap = now;
            if (prev == null) return "";
            string[] labels = { "$", "Health", "Lungs", "Spirits", "Standing", "Suspicion" };
            var sb = new StringBuilder();
            for (int i = 0; i < now.Length; i++)
            {
                int d = now[i] - prev[i];
                if (d == 0) continue;
                bool good = i == 5 ? d < 0 : d > 0;
                string s = i == 0 ? (d > 0 ? "+$" : "−$") + Math.Abs(d) : labels[i] + " " + (d > 0 ? "+" : "−") + Math.Abs(d);
                if (sb.Length > 0) sb.Append("    ");
                sb.Append("<color=").Append(good ? GOOD : BAD).Append('>').Append(s).Append("</color>");
            }
            return sb.ToString();
        }

        /// <summary>JS phaseHTML.</summary>
        void RefreshPhase()
        {
            var S = CC.S;
            bool show = S != null && !CC.TitleMode;
            phaseRow.SetActive(show);
            if (!show) return;
            int ph = S.phase == "work" || S.phase == "worked" || S.phase == "event" ? 0 : S.phase == "leisure" ? 1 : 2;
            string[] names = { "WORK", "TOWN", "NIGHT" };
            for (int i = 0; i < 3; i++)
            {
                string s = (i + 1) + " " + names[i];
                if (i == 0 && ph == 0) s += " " + Math.Min(CC.SHIFTS, S.shifts) + "/" + CC.SHIFTS;
                if (i == 1 && ph == 1) s += " " + new string('●', Math.Max(0, S.slots)) + new string('○', Math.Max(0, 3 - S.slots));
                phaseText[i].text = s;
                bool on = i == ph;
                phaseImg[i].color = on ? C_COPPER : new Color(0, 0, 0, 0);
                phaseText[i].color = on ? Color.white : C_SOFT;
            }
            turnText.text = Math.Min(48, CC.TurnIdx() + 1) + " / 48";
        }

        /// <summary>JS statsHTML.</summary>
        void RefreshHud()
        {
            var S = CC.S;
            bool show = S != null && !CC.TitleMode;
            if (hudRoot.activeSelf != show) hudRoot.SetActive(show);
            if (!show) return;
            moneyText.text = "$" + S.money;
            SetChip(chips[0], S.health, false);
            SetChip(chips[1], S.lungs, false);
            SetChip(chips[2], S.morale, false);
            SetChip(chips[3], S.rep, false);
            SetChip(chips[4], S.heat, true);
            skillText.text = "Skill " + S.skill.ToString("0.0", CultureInfo.InvariantCulture);
            var o = S.o;
            whoText.text = S.name + ", " + (o != null ? o.name : "") + (S.card ? " · card" : "") + (S.blacklisted ? " · blacklisted" : "")
                + (S.flags.B("gun") ? " · Colt" : "") + (S.flags.B("house") ? " · house" : "");
        }

        void SetChip(Chip c, float v, bool inv)
        {
            bool bad = inv ? v > 66 : v < 34, warn = inv ? v > 33 : v < 67;
            c.fillImg.color = bad ? C_BLOOD : warn ? C_COPPER : C_VERDI;
            SetFill(c.fill, v / 100f);
            c.num.text = CC.Round(v).ToString(CultureInfo.InvariantCulture);
        }

        void RefreshLedger()
        {
            var S = CC.S;
            bool show = S != null && !CC.TitleMode;
            ledgerRoot.SetActive(show);
            if (!show) return;
            ledgerHead.text = (ledgerOpen ? "▼ " : "► ") + "Your ledger";
            ledgerBody.gameObject.SetActive(ledgerOpen);
            if (!ledgerOpen) return;
            var sb = new StringBuilder();
            foreach (var l in S.log) { if (sb.Length > 0) sb.Append('\n'); sb.Append(Html(l, 13)); }
            ledgerBody.text = sb.ToString();
        }

        void RefreshCaption()
        {
            string c = CC.scene.caption ?? "";
            if (c == lastCaption) return;
            lastCaption = c;
            captionText.text = Html(c, 15);
            captionRoot.SetActive(c.Length > 0);
        }

        void ScrollTop()
        {
            if (content != null) content.anchoredPosition = Vector2.zero;
            if (scroll != null) scroll.velocity = Vector2.zero;
        }

        static void Deselect()
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        // =====================================================================
        // building the hierarchy
        // =====================================================================
        void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        void BuildUI()
        {
            var root = new GameObject("CopperCampUI", typeof(RectTransform));
            root.transform.SetParent(transform, false);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 10;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = .5f;
            root.AddComponent<GraphicRaycaster>();
            BuildHud(root.transform);
            BuildCaption(root.transform);
            BuildPanel(root.transform);
            BuildSoundButton(root.transform);
        }

        void BuildHud(Transform R)
        {
            hudRoot = Mk("HUD", R);
            Place(hudRoot, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -36), Vector2.zero);
            Img(hudRoot, C_HUD);
            var hl = HL(hudRoot, 14);
            hl.padding = new RectOffset(12, 128, 6, 6);
            moneyText = HudText(hudRoot.transform, "Money", 16, C_BRASS);
            string[] names = { "Health", "Lungs", "Spirits", "Standing", "Suspicion" };
            chips = new Chip[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                var go = Mk(names[i], hudRoot.transform);
                HL(go, 5);
                var c = new Chip { label = HudText(go.transform, "Label", 14, C_TEXT) };
                c.label.text = names[i];
                c.fill = Bar(go.transform, "Bar", 44, 6, new Color(1, 1, 1, .14f), C_VERDI, null);
                c.fillImg = c.fill.GetComponent<Image>();
                c.num = HudText(go.transform, "Value", 14, C_TEXT);
                chips[i] = c;
            }
            skillText = HudText(hudRoot.transform, "Skill", 14, C_TEXT);
            LE(Mk("Spacer", hudRoot.transform), -1, -1).flexibleWidth = 1;
            whoText = HudText(hudRoot.transform, "Who", 14, C_WHO, FontStyle.Italic);
            whoText.alignment = TextAnchor.MiddleRight;
            hudRoot.SetActive(false);
        }

        Text HudText(Transform p, string name, int size, Color col, FontStyle st = FontStyle.Normal)
        {
            var t = MkText(p, name, size, col, st, TextAnchor.MiddleLeft);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return t;
        }

        void BuildCaption(Transform R)
        {
            captionRoot = Mk("Caption", R);
            var rt = Place(captionRoot, Vector2.zero, new Vector2(.52f, 0), Vector2.zero, new Vector2(0, 30));
            rt.pivot = Vector2.zero;
            Img(captionRoot, Hex("#120d0a", .72f));
            VL(captionRoot, 0, new RectOffset(12, 12, 6, 7));
            Fit(captionRoot);
            captionText = MkText(captionRoot.transform, "Text", 15, C_TEXT, FontStyle.Italic);
            captionRoot.SetActive(false);
        }

        void BuildSoundButton(Transform R)
        {
            var go = Mk("Sound", R);
            Place(go, Vector2.one, Vector2.one, new Vector2(-118, -31), new Vector2(-10, -5));
            var img = Img(go, Color.white, true);
            AddOutline(go, C_BRASS, 1f);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.colors = Tints(Hex("#120d0a", .6f), Hex("#3a2c14", .85f), Hex("#4a3818", .9f));
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            sndText = MkText(go.transform, "Text", 14, C_BRASS, FontStyle.Normal, TextAnchor.MiddleCenter);
            Stretch(sndText.rectTransform, 0);
            sndText.text = "Sound: " + (soundOn ? "on" : "off");
            btn.onClick.AddListener(ToggleSound);
        }

        void BuildPanel(Transform R)
        {
            panelRoot = Mk("Panel", R);
            Place(panelRoot, new Vector2(1f - PanelWidthFraction, 0), Vector2.one, new Vector2(0, 10), new Vector2(-10, -42));
            Img(panelRoot, C_PANEL, true);
            Frame(panelRoot.transform, C_COPPER, 2f);

            // scroll view: Scroll > Viewport (mask) > Content (vertical layout, fitted to its children)
            var sv = Mk("Scroll", panelRoot.transform);
            Stretch((RectTransform)sv.transform, 2);
            scroll = sv.AddComponent<ScrollRect>();
            var vp = Mk("Viewport", sv.transform);
            var vrt = Place(vp, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-10, 0));
            Img(vp, new Color(0, 0, 0, 0), true);   // invisible, but catches the scroll wheel
            vp.AddComponent<RectMask2D>();
            var ct = Mk("Content", vp.transform);
            content = Place(ct, new Vector2(0, 1), Vector2.one, Vector2.zero, Vector2.zero);
            content.pivot = new Vector2(.5f, 1);
            VL(ct, 10, new RectOffset(14, 8, 12, 16));
            Fit(ct);
            scroll.content = content; scroll.viewport = vrt;
            scroll.horizontal = false; scroll.vertical = true; scroll.inertia = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            scroll.verticalScrollbar = BuildScrollbar(sv.transform);
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            var C = ct.transform;
            BuildPhaseRow(C);

            // ---- the normal screen ----
            normalRoot = Mk("Normal", C);
            VL(normalRoot, 8, null);
            var N = normalRoot.transform;
            titleText = MkText(N, "Title", TITLE, C_BRASS, FontStyle.Bold);
            deltaText = MkText(N, "Deltas", 14, C_TEXT, FontStyle.Italic);
            bodyText = MkText(N, "Body", BODY, C_TEXT);

            cardRow = Mk("Talk", N);
            HL(cardRow, 12).childAlignment = TextAnchor.UpperLeft;
            var ph = Mk("Portrait", cardRow.transform);
            LE(ph, 72, 72).flexibleWidth = 0;
            portraitImg = Mk("Photo", ph.transform).AddComponent<RawImage>();
            Stretch(portraitImg.rectTransform, 0);
            portraitImg.raycastTarget = false;
            AddOutline(portraitImg.gameObject, Hex("#3a2a1f"), 2f);
            portraitBox = Mk("Initials", ph.transform);
            Stretch((RectTransform)portraitBox.transform, 0);
            Img(portraitBox, C_BTN);
            AddOutline(portraitBox, C_COPPER, 2f);
            initialsText = MkText(portraitBox.transform, "Text", 26, C_COPPER, FontStyle.Bold, TextAnchor.MiddleCenter);
            Stretch(initialsText.rectTransform, 0);
            var col = Mk("Words", cardRow.transform);
            VL(col, 4, null);
            LE(col, -1, -1).flexibleWidth = 1;
            cardName = MkText(col.transform, "Name", 18, C_COPPER);
            cardBody = MkText(col.transform, "Text", BODY, C_TEXT);
            cardRow.SetActive(false);

            BuildNameRow(N);

            timerRoot = Mk("Timer", N);
            VL(timerRoot, 4, null);
            timerFill = Bar(timerRoot.transform, "Bar", -1, 9, C_TRACK, C_BLOOD, C_BLOOD);
            timerNote = MkText(timerRoot.transform, "Note", 14, C_SOFT, FontStyle.Italic);
            timerRoot.SetActive(false);

            var chs = Mk("Choices", N);
            VL(chs, 6, new RectOffset(1, 1, 4, 1));
            choicesRoot = (RectTransform)chs.transform;

            // ---- the shift screen ----
            shiftRoot = Mk("Shift", C);
            VL(shiftRoot, 8, null);
            var H = shiftRoot.transform;
            sTitle = MkText(H, "Title", TITLE, C_BRASS, FontStyle.Bold);
            sNote = MkText(H, "Note", 13, C_SOFT, FontStyle.Italic);
            sHelp = MkText(H, "Help", 15, C_TEXT);
            meterGo = Mk("Meter", H);
            Img(meterGo, C_TRACK);
            AddOutline(meterGo, C_RULE, 2f);
            LE(meterGo, -1, 18);
            var zone = Mk("Zone", meterGo.transform);
            Img(zone, new Color(C_VERDI.r, C_VERDI.g, C_VERDI.b, .55f));
            meterZone = Place(zone, new Vector2(.4f, 0), new Vector2(.6f, 1), Vector2.zero, Vector2.zero);
            var mk = Mk("Marker", meterGo.transform);
            Img(mk, C_COPPER);
            AddOutline(mk, Hex("#3a1a08"), 1f);
            meterMark = Place(mk, new Vector2(.5f, 0), new Vector2(.5f, 1), new Vector2(-2.5f, -3), new Vector2(2.5f, 3));
            gaugeFill = Bar(H, "Gauge", -1, 14, C_TRACK, C_COPPER, C_RULE);
            extraFill = Bar(H, "Extra", -1, 14, C_TRACK, C_BLOOD, C_RULE);
            extraGo = extraFill.parent.gameObject;
            sMsg = MkText(H, "Msg", 15, C_SOFT, FontStyle.Italic);
            LE(sMsg.gameObject, -1, -1).minHeight = 22;
            sClock = MkText(H, "Clock", 13, C_SOFT, FontStyle.Italic);
            var sb = Mk("Buttons", H);
            VL(sb, 8, new RectOffset(2, 2, 6, 2));
            sBtnRoot = (RectTransform)sb.transform;
            shiftRoot.SetActive(false);

            // ---- the ledger (a collapsible S.log) ----
            ledgerRoot = Mk("Ledger", C);
            VL(ledgerRoot, 4, new RectOffset(0, 0, 6, 0));
            var rule = Mk("Rule", ledgerRoot.transform);
            Img(rule, C_RULE);
            LE(rule, -1, 1);
            var head = Mk("Head", ledgerRoot.transform);
            var hb = head.AddComponent<Button>();
            hb.targetGraphic = Img(head, new Color(0, 0, 0, 0), true);
            hb.navigation = new Navigation { mode = Navigation.Mode.None };
            VL(head, 0, null);
            ledgerHead = MkText(head.transform, "Text", 14, C_SOFT, FontStyle.Italic);
            hb.onClick.AddListener(() => { ledgerOpen = !ledgerOpen; RefreshLedger(); Deselect(); });
            ledgerBody = MkText(ledgerRoot.transform, "Log", 13, C_SOFT);
            ledgerBody.lineSpacing = 1.2f;
            ledgerRoot.SetActive(false);

            panelRoot.SetActive(panelVisible);
        }

        void BuildPhaseRow(Transform C)
        {
            phaseRow = Mk("Phase", C);
            HL(phaseRow, 6);
            for (int i = 0; i < 3; i++)
            {
                var seg = Mk("Seg" + i, phaseRow.transform);
                phaseImg[i] = Img(seg, new Color(0, 0, 0, 0));
                AddOutline(seg, C_RULE, 1f);
                HL(seg, 0).padding = new RectOffset(8, 8, 2, 2);
                phaseText[i] = HudText(seg.transform, "Text", 12, C_SOFT);
            }
            LE(Mk("Spacer", phaseRow.transform), -1, -1).flexibleWidth = 1;
            turnText = HudText(phaseRow.transform, "Turn", 12, C_SOFT);
            turnText.alignment = TextAnchor.MiddleRight;
            phaseRow.SetActive(false);
        }

        void BuildNameRow(Transform N)
        {
            nameRow = Mk("Name", N);
            var img = Img(nameRow, Color.white, true);
            AddOutline(nameRow, C_COPPER, 1f);
            LE(nameRow, -1, 42);
            var txt = MkText(nameRow.transform, "Text", 18, C_TEXT, FontStyle.Normal, TextAnchor.MiddleLeft);
            var ph = MkText(nameRow.transform, "Placeholder", 16, C_SOFT, FontStyle.Italic, TextAnchor.MiddleLeft);
            foreach (var t in new[] { txt, ph })
            {
                t.supportRichText = false;   // InputField wants plain text
                Place(t.gameObject, Vector2.zero, Vector2.one, new Vector2(10, 2), new Vector2(-10, -2));
            }
            ph.horizontalOverflow = HorizontalWrapMode.Overflow;
            ph.text = "Your name on the hill (e.g. Paddy, Jan, Yrjö, Lung Wah)";
            nameField = nameRow.AddComponent<InputField>();
            nameField.textComponent = txt;
            nameField.placeholder = ph;
            nameField.targetGraphic = img;
            nameField.characterLimit = 24;
            nameField.lineType = InputField.LineType.SingleLine;
            nameField.customCaretColor = true;
            nameField.caretColor = C_BRASS;
            nameField.selectionColor = new Color(C_COPPER.r, C_COPPER.g, C_COPPER.b, .45f);
            var cb = Tints(C_BTN, C_BTN_HI, C_BTN_HI);
            cb.selectedColor = C_BTN_HI;
            nameField.colors = cb;
            nameField.navigation = new Navigation { mode = Navigation.Mode.None };
            nameRow.SetActive(false);
        }

        Scrollbar BuildScrollbar(Transform sv)
        {
            var go = Mk("Scrollbar", sv);
            Place(go, new Vector2(1, 0), Vector2.one, new Vector2(-8, 4), new Vector2(-2, -4));
            Img(go, new Color(1, 1, 1, .05f), true);
            var area = Mk("Sliding Area", go.transform);
            Stretch((RectTransform)area.transform, 0);
            var handle = Mk("Handle", area.transform);
            var himg = Img(handle, new Color(C_COPPER.r, C_COPPER.g, C_COPPER.b, .7f), true);
            var sb = go.AddComponent<Scrollbar>();
            sb.handleRect = Place(handle, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            sb.targetGraphic = himg;
            sb.direction = Scrollbar.Direction.BottomToTop;
            sb.navigation = new Navigation { mode = Navigation.Mode.None };
            return sb;
        }

        // =====================================================================
        // audio
        // =====================================================================
        void BuildAudio()
        {
            sfxSrc = NewSource(false);
            ambX = new XFade { a = NewVoice(1f), b = NewVoice(1f) };
            musicX = new XFade { a = NewVoice(1f), b = NewVoice(1f) };
        }

        AudioSource NewSource(bool loop)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = loop;
            s.spatialBlend = 0f;
            s.volume = loop ? 0f : 1f;
            s.mute = !soundOn;
            return s;
        }

        Voice NewVoice(float rate) => new Voice { src = NewSource(true), rate = rate };

        AudioClip Clip(string n)
        {
            if (!clips.TryGetValue(n, out var c))
            {
                c = Resources.Load<AudioClip>("CC/audio/" + n);
                clips[n] = c;
            }
            return c;
        }

        void Loop(string name, float vol)
        {
            if (!loops.TryGetValue(name, out var v))
            {
                if (vol <= 0f) return;
                var clip = Clip(name);
                if (clip == null) return;
                v = NewVoice(1f / .12f);
                v.src.clip = clip;
                v.key = name;
                loops[name] = v;
            }
            v.target = Mathf.Max(0f, vol);
            if (vol > 0f && !v.src.isPlaying) { v.src.volume = 0f; v.src.Play(); }
        }

        void UpdateAudio(float dt)
        {
            var sc = CC.scene;
            var U = CC.Shift;
            string amb = sc.mode == "under" || sc.mode == "dry" ? "amb_under"
                : sc.mode == "inside" && (sc.inside == "mm" || sc.inside == "mercury") ? "amb_saloon" : "amb_wind";
            ambX.Set(amb, amb == "amb_under" ? .8f : amb == "amb_saloon" ? .6f : .5f, Clip);
            // music, the way the browser game routes it: the title tune, a rag or a stomp in the M&M and on Mercury Street,
            // and the creek tune over the season's newspaper
            bool saloon = (sc.mode == "inside" && (sc.inside == "mm" || sc.inside == "mercury")) || (sc.mode == "hill" && (sc.focus == "mm" || sc.focus == "mercury"));
            if (!saloon) saloonTune = null; else if (saloonTune == null) saloonTune = UnityEngine.Random.value < .5f ? "music_rag" : "music_stomp";
            string tune = sc.mode == "photo" ? "music_ridge" : saloon ? saloonTune : paperShown ? "music_creek" : null;
            musicX.Set(tune, tune == "music_ridge" ? .5f : tune == "music_creek" ? .4f : .34f, Clip);

            bool run = U != null && !U.over && !U.paused;
            Loop("drill", run && U.kind == "drill" && U.In("main") && U.stuck == 0 ? .55f : 0f);
            Loop("tram", run && U.kind == "tram" && U.v > .05f ? .2f + .6f * U.v : 0f);
            Loop("boots", sc.mode == "walk" && Walking ? .5f : 0f);

            ambX.Tick(dt);
            musicX.Tick(dt);
            foreach (var v in loops.Values) v.Tick(dt);
        }

        void ToggleSound()
        {
            soundOn = !soundOn;
            PlayerPrefs.SetInt(SND_KEY, soundOn ? 1 : 0);
            PlayerPrefs.Save();
            foreach (var s in GetComponents<AudioSource>()) s.mute = !soundOn;
            sndText.text = "Sound: " + (soundOn ? "on" : "off");
            Deselect();
        }

        // =====================================================================
        // HTML → Unity rich text
        // =====================================================================
        static readonly Regex TagRx = new Regex(@"<!--.*?-->|<(/?)([a-zA-Z][a-zA-Z0-9]*)([^>]*)>", RegexOptions.Singleline | RegexOptions.Compiled);
        static readonly Regex ClassRx = new Regex(@"class\s*=\s*[""']([^""']*)[""']", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        static readonly Regex EntRx = new Regex(@"&(#x[0-9a-fA-F]+|#[0-9]+|[a-zA-Z]+);", RegexOptions.Compiled);
        static readonly Dictionary<string, string> ENT = new Dictionary<string, string>
        {
            { "amp", "&" }, { "lt", "<" }, { "gt", ">" }, { "quot", "\"" }, { "apos", "'" }, { "nbsp", " " },
            { "mdash", "—" }, { "ndash", "–" }, { "hellip", "…" }, { "rsquo", "’" }, { "lsquo", "‘" },
            { "rdquo", "”" }, { "ldquo", "“" }, { "middot", "·" }, { "times", "×" }, { "cent", "¢" },
        };

        /// <summary>
        /// Converts the game's HTML-ish text to Unity rich text: p → paragraph break, br → newline, b/strong → b,
        /// i/em → i, h2/h3 → bold at size+2, class "note" → soft italic; every other tag is dropped but its text kept.
        /// Output tags are always balanced, so Unity never shows raw markup.
        /// </summary>
        public static string Html(string s, int size = BODY)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = Regex.Replace(s, @"[ \t\r\n]+", " ");
            var sb = new StringBuilder(s.Length + 32);
            var stack = new List<KeyValuePair<string, string>>();   // open element → the rich-text that closes it
            int pos = 0;
            foreach (Match m in TagRx.Matches(s))
            {
                sb.Append(Decode(s.Substring(pos, m.Index - pos)));
                pos = m.Index + m.Length;
                if (m.Groups[2].Length == 0) continue;                // comment
                string tag = m.Groups[2].Value.ToLowerInvariant();
                string attrs = m.Groups[3].Value;
                if (m.Groups[1].Length > 0)
                {
                    int k = stack.FindLastIndex(e => e.Key == tag);
                    if (k < 0) continue;                                 // stray closing tag
                    for (int j = stack.Count - 1; j >= k; j--) sb.Append(stack[j].Value);
                    stack.RemoveRange(k, stack.Count - k);
                    continue;
                }
                bool selfClose = attrs.TrimEnd().EndsWith("/");
                if (tag == "br") { sb.Append('\n'); continue; }
                if (tag == "hr" || tag == "img" || tag == "input" || tag == "wbr" || tag == "meta" || tag == "link") continue;

                var cm = ClassRx.Match(attrs);
                bool note = cm.Success && Regex.IsMatch(cm.Groups[1].Value, @"\bnote\b");
                string nOpen = note ? "<i><color=" + NOTE + ">" : "", nClose = note ? "</color></i>" : "";
                string open, close;
                switch (tag)
                {
                    case "b": case "strong": open = "<b>"; close = "</b>"; break;
                    case "i": case "em": case "cite": open = "<i>"; close = "</i>"; break;
                    case "h1": case "h2": case "h3":
                        open = "\n<b><size=" + (size + (tag == "h1" ? 4 : 2)) + ">"; close = "</size></b>\n"; break;
                    case "h4": case "h5": case "h6": open = "\n<b>"; close = "</b>\n"; break;
                    case "p": open = "\n\n" + nOpen; close = nClose + "\n\n"; break;
                    case "div": case "summary": case "section": case "blockquote": case "ul": case "ol": case "tr": case "table":
                        open = "\n" + nOpen; close = nClose + "\n"; break;
                    case "li": open = "\n• "; close = ""; break;
                    case "small": open = "<size=" + Math.Max(8, size - 2) + ">"; close = "</size>"; break;
                    default: open = nOpen; close = nClose; break;          // span, details, a, font …
                }
                sb.Append(open);
                if (!selfClose) stack.Add(new KeyValuePair<string, string>(tag, close));
                else sb.Append(close);
            }
            sb.Append(Decode(s.Substring(pos)));
            for (int j = stack.Count - 1; j >= 0; j--) sb.Append(stack[j].Value);

            string r = sb.ToString();
            r = Regex.Replace(r, @"[  ]*\n[  ]*", "\n");
            r = Regex.Replace(r, @"\n{3,}", "\n\n");
            return r.Trim('\n', ' ');
        }

        static string Decode(string t)
        {
            if (t.IndexOf('&') < 0) return t;
            return EntRx.Replace(t, m =>
            {
                string e = m.Groups[1].Value;
                try
                {
                    if (e.StartsWith("#x") || e.StartsWith("#X")) return char.ConvertFromUtf32(Convert.ToInt32(e.Substring(2), 16));
                    if (e.StartsWith("#")) return char.ConvertFromUtf32(int.Parse(e.Substring(1), CultureInfo.InvariantCulture));
                }
                catch (Exception) { return m.Value; }
                return ENT.TryGetValue(e.ToLowerInvariant(), out var v) ? v : m.Value;
            });
        }

        // =====================================================================
        // small uGUI helpers
        // =====================================================================
        static GameObject Mk(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.layer = parent.gameObject.layer;
            return go;
        }

        /// <summary>Sets anchors and offsets in one call; returns the RectTransform.</summary>
        static RectTransform Place(GameObject go, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
        {
            var rt = (RectTransform)go.transform;
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = oMin; rt.offsetMax = oMax;
            return rt;
        }

        static void Stretch(RectTransform rt, float inset) => Place(rt.gameObject, Vector2.zero, Vector2.one, new Vector2(inset, inset), new Vector2(-inset, -inset));

        static Image Img(GameObject go, Color c, bool raycast = false)
        {
            var img = go.AddComponent<Image>();
            img.color = c; img.raycastTarget = raycast;
            return img;
        }

        Text MkText(Transform parent, string name, int size, Color col, FontStyle st = FontStyle.Normal, TextAnchor al = TextAnchor.UpperLeft)
        {
            var t = Mk(name, parent).AddComponent<Text>();
            t.font = font; t.fontSize = size; t.color = col; t.fontStyle = st; t.alignment = al;
            t.supportRichText = true; t.lineSpacing = 1.08f; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        static VerticalLayoutGroup VL(GameObject go, float spacing, RectOffset pad)
        {
            var v = go.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing; v.padding = pad ?? new RectOffset(0, 0, 0, 0); v.childAlignment = TextAnchor.UpperLeft;
            v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            return v;
        }

        static HorizontalLayoutGroup HL(GameObject go, float spacing)
        {
            var h = go.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing; h.padding = new RectOffset(0, 0, 0, 0); h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = false; h.childForceExpandHeight = false;
            return h;
        }

        static void Fit(GameObject go)
        {
            var f = go.AddComponent<ContentSizeFitter>();
            f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            f.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        }

        /// <summary>A LayoutElement; w/h &gt;= 0 set both the min and preferred size.</summary>
        static LayoutElement LE(GameObject go, float w, float h)
        {
            var le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            if (w >= 0) { le.minWidth = w; le.preferredWidth = w; }
            if (h >= 0) { le.minHeight = h; le.preferredHeight = h; }
            return le;
        }

        /// <summary>Button tints. The target Image is white, so these are the exact colours shown.</summary>
        static ColorBlock Tints(Color normal, Color hover, Color down)
        {
            var cb = ColorBlock.defaultColorBlock;
            cb.normalColor = normal; cb.highlightedColor = hover; cb.pressedColor = down;
            cb.selectedColor = normal; cb.disabledColor = C_BTN; cb.colorMultiplier = 1f; cb.fadeDuration = .08f;
            return cb;
        }

        static void AddOutline(GameObject go, Color c, float d)
        {
            var o = go.AddComponent<Outline>();
            o.effectColor = c; o.effectDistance = new Vector2(d, -d); o.useGraphicAlpha = false;
        }

        /// <summary>A border of four thin images, so a translucent background does not show an Outline through it.</summary>
        static void Frame(Transform p, Color c, float th)
        {
            Place(Img(Mk("Edge", p), c).gameObject, new Vector2(0, 1), Vector2.one, new Vector2(0, -th), Vector2.zero);
            Place(Img(Mk("Edge", p), c).gameObject, Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, th));
            Place(Img(Mk("Edge", p), c).gameObject, Vector2.zero, new Vector2(0, 1), Vector2.zero, new Vector2(th, 0));
            Place(Img(Mk("Edge", p), c).gameObject, new Vector2(1, 0), Vector2.one, new Vector2(-th, 0), Vector2.zero);
        }

        /// <summary>A track with a fill child sized by its right anchor. Returns the fill.</summary>
        static RectTransform Bar(Transform parent, string name, float w, float h, Color track, Color fill, Color? border)
        {
            var go = Mk(name, parent);
            Img(go, track);
            if (border != null) AddOutline(go, border.Value, 1f);
            LE(go, w, h);
            var f = Mk("Fill", go.transform);
            Img(f, fill);
            return Place(f, Vector2.zero, new Vector2(0, 1), Vector2.zero, Vector2.zero);
        }

        static void SetFill(RectTransform fill, float f)
        {
            if (fill != null) fill.anchorMax = new Vector2(Mathf.Clamp01(f), 1f);
        }

        static void AddTrigger(EventTrigger et, EventTriggerType type, Action a)
        {
            var e = new EventTrigger.Entry { eventID = type };
            e.callback.AddListener(_ => a());
            et.triggers.Add(e);
        }

        static void ClearChildren(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                var c = t.GetChild(i).gameObject;
                c.SetActive(false);
                Destroy(c);
            }
        }

        static string Get(Dictionary<string, string> d, string k) => k != null && d.TryGetValue(k, out var v) ? v : "";

        static Color Hex(string h, float a = 1f)
        {
            h = h.TrimStart('#');
            if (h.Length == 3) h = new string(new[] { h[0], h[0], h[1], h[1], h[2], h[2] });
            int n = Convert.ToInt32(h, 16);
            return new Color(((n >> 16) & 255) / 255f, ((n >> 8) & 255) / 255f, (n & 255) / 255f, a);
        }
    }
}
