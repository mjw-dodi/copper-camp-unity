// Copper Camp — core state, helpers and the screen API.
// The game logic is a close port of the browser game (index.html, v2.2). It is one static partial class, CC,
// split across files by subsystem, so the JavaScript reads almost line for line in C#.
// Logic files use only System and UnityEngine.Random/Mathf/Debug, so they also compile outside Unity.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

namespace CopperCamp
{
    // ---------- data types ----------
    public class Origin
    {
        public string id, name, home, where, desc, perk;
        public float health, lungs, morale, money, rep, skill, str;
    }

    public class Job
    {
        public string id, name, loc, desc;
        public int pay;
        public float risk, lungs;
        public bool co;
    }

    public class Leisure
    {
        public string id, place, name, desc, talk, run, until;
        public int cost;
        public Fx fx;
        public bool once, ends;
        public float fight, rolled;
        public int[] seasons;
    }

    /// <summary>A stat change, like the JS object {money:-10, morale:5}. Unset fields are skipped.</summary>
    public class Fx
    {
        public float? money, health, lungs, morale, rep, heat, skill;
        public bool IsEmpty => money == null && health == null && lungs == null && morale == null && rep == null && heat == null && skill == null;
    }

    /// <summary>One button. cost: null = no cost label; &gt;0 "$N"; &lt;0 "+$N"; 0 "free".</summary>
    public class Choice
    {
        public string t, d;
        public int? cost;
        public bool disabled;
        public Action go;
    }

    /// <summary>Options for Render, like the JS opts object.</summary>
    public class Opts
    {
        public string loc;      // focus the hill view on a LOC id
        public string caption;  // caption override
        public bool dark, fire;
        public bool keepScene;  // do not switch the 3D view (used by in-shift scenes such as the dinner pail)
    }

    /// <summary>The JS S.flags object. Values are stored as strings so the save stays simple.</summary>
    [Serializable]
    public class Flags
    {
        public Dictionary<string, string> d = new Dictionary<string, string>();
        public bool Has(string k) => d.ContainsKey(k);
        /// <summary>Truthy, as in JS: set and not "", "0" or "false".</summary>
        public bool B(string k) => d.TryGetValue(k, out var v) && v != "" && v != "0" && v != "false";
        public int I(string k) => d.TryGetValue(k, out var v) && int.TryParse(v, out var n) ? n : 0;
        public string Str(string k) => d.TryGetValue(k, out var v) ? v : null;
        public void Set(string k, bool v) { d[k] = v ? "1" : "0"; }
        public void SetI(string k, int v) { d[k] = v.ToString(); }
        public void SetS(string k, string v) { d[k] = v ?? ""; }
        public void Del(string k) { d.Remove(k); }
    }

    /// <summary>The JS S object.</summary>
    public class GameState
    {
        public string name = "Jack";
        public string originId;
        public int year = 1906, season = 0;
        public float health, lungs, morale, rep, heat, skill;
        public int money;
        public bool card, blacklisted, strike, halfpay;
        public int sent, fights, kills;
        public List<string> log = new List<string>();
        public Flags flags = new Flags();
        public string jobId;            // JS S.job (null when none)
        public string phase = "work";   // work | worked | event | leisure | end
        public int shifts = 0;
        public int slots = 3;
        public HashSet<string> used = new HashSet<string>();
        public int seasonPay = 0;
        public Dictionary<string, int> rel = new Dictionary<string, int>();
        public string tip; public int tipTurn = -99;
        public string payTip; public int payTipTurn = -99;
        public string bossName = "Foreman Dan Kearns";
        public string pendingPlace;
        public bool ended;
        public Dictionary<int, int> ev = new Dictionary<int, int>();   // JS S.flags.ev: event index -> turn fired
        public List<string> brkRecent = new List<string>();            // JS S.flags.brkRecent

        public Origin o => CC.ORIGINS.FirstOrDefault(x => x.id == originId);
        public Job job
        {
            get => jobId == null ? null : CC.FindJob(jobId);
            set => jobId = value?.id;
        }
    }

    /// <summary>What the 3D view shows; the JS `scene` object.</summary>
    public class SceneState
    {
        public string mode = "photo";   // photo | hill | under | dry | walk | inside
        public string focus;            // LOC id
        public string speech, speaker;  // a line spoken in the room, and who says it (a role the 3D room knows)
        public int speechSerial;
        public string caption = "";
        public bool dark, fire;
        public int season, year = 1906;
        public string inside;           // STREET id of the interior being shown
        public ShiftState under;        // the running shift, when mode == under
        public WalkState walk;          // the street, when mode == walk
    }

    public partial class WalkState
    {
        public float px = 120f, target = -1f;
        public string at;           // STREET id the player is standing at
        public bool inside;         // showing an interior
        public string seasonKey;
    }

    // ---------- the game ----------
    public static partial class CC
    {
        public static GameState S;
        public static readonly SceneState scene = new SceneState();
        public static Flags F => S.flags;
        public static readonly string[] SEASONS = { "Spring", "Summer", "Autumn", "Winter" };
        public const int SHIFTS = 3;

        /// <summary>Set by the Unity front end; null when running headless (tests).</summary>
        public static IFrontEnd UI;

        // ---- JS helpers ----
        public static float Rand() => Random.value;                                // Math.random()
        public static T Pick<T>(IList<T> a) => a[Mathf.Min(a.Count - 1, (int)(Rand() * a.Count))];
        public static T Pick<T>(List<T> a) => a[Mathf.Min(a.Count - 1, (int)(Rand() * a.Count))];
        public static T Pick<T>(params T[] a) => a[Mathf.Min(a.Length - 1, (int)(Rand() * a.Length))];
        public static float Clamp(float v, float a, float b) => Math.Max(a, Math.Min(b, v));
        /// <summary>JS Math.round (halves round up).</summary>
        public static int Round(float v) => (int)Math.Floor(v + 0.5f);
        public static int RandInt(int lo, int hi) => lo + (int)(Rand() * (hi - lo + 1));  // uniform in [lo, hi]
        public static string Key() => S.year + "-" + S.season;
        public static string DateStr() => SEASONS[S.season] + " " + S.year;
        public static int TurnIdx() => (S.year - 1906) * 4 + S.season;
        public static int Rel(string id, int d = 0)
        {
            S.rel.TryGetValue(id, out var v);
            v += d;
            S.rel[id] = v;
            return v;
        }

        public static void Log(string t)
        {
            S.log.Insert(0, DateStr() + " — " + t);
            if (S.log.Count > 14) S.log.RemoveRange(14, S.log.Count - 14);
        }

        public static void Apply(Fx fx)
        {
            if (fx == null) return;
            if (fx.money != null) S.money = Round(S.money + fx.money.Value);
            if (fx.health != null) S.health = Clamp(S.health + fx.health.Value, 0, 100);
            if (fx.lungs != null) S.lungs = Clamp(S.lungs + fx.lungs.Value, 0, 100);
            if (fx.morale != null) S.morale = Clamp(S.morale + fx.morale.Value, 0, 100);
            if (fx.rep != null) S.rep = Clamp(S.rep + fx.rep.Value, 0, 100);
            if (fx.heat != null) S.heat = Clamp(S.heat + fx.heat.Value, 0, 100);
            if (fx.skill != null) S.skill = Clamp(S.skill + fx.skill.Value, 0, 100);
        }

        // ---- choices ----
        public static Choice Ch(string t, string d, Action go, int? cost = null, bool disabled = false)
            => new Choice { t = t, d = d, go = go, cost = cost, disabled = disabled };
        public static List<Choice> L(params Choice[] c) => new List<Choice>(c.Where(x => x != null));

        // ---- screen API (the JS render / talk / timed) ----
        /// <summary>JS render(title, body, choices, opts). Body is HTML-ish text: p, b, i, em, strong, br are kept.</summary>
        public static void Render(string title, string body, List<Choice> choices, Opts opts = null)
        {
            opts = opts ?? new Opts();
            scene.speech = null; scene.speaker = null;
            if (S != null) { scene.season = S.season; scene.year = S.year; }
            if (!opts.keepScene)
            {
                scene.dark = opts.dark;
                scene.fire = opts.fire;
                bool inside = scene.walk != null && scene.walk.inside && S != null && S.phase == "leisure";
                scene.mode = inside ? "inside" : (S == null ? "photo" : "hill");
                if (opts.loc != null && !inside) FocusOn(opts.loc, opts.caption);   // inside a room, the caption keeps naming the room
            }
            if (S != null && !S.ended) Save();
            if (UI != null) UI.Show(title, body, choices, null, null);
        }

        /// <summary>JS talk(id, text, choices, opts, nm): a portrait card with the person's name.</summary>
        public static bool Talk(string id, string text, List<Choice> choices, Opts opts = null, string nm = null)
        {
            opts = opts ?? new Opts();
            scene.speech = null; scene.speaker = null;
            if (S != null) { scene.season = S.season; scene.year = S.year; }
            if (!opts.keepScene)
            {
                scene.dark = opts.dark;
                scene.fire = opts.fire;
                bool inside = scene.walk != null && scene.walk.inside && S != null && S.phase == "leisure";
                if (scene.mode != "under" && scene.mode != "dry") scene.mode = inside ? "inside" : "hill";
                if (opts.loc != null && !inside) FocusOn(opts.loc, opts.caption);
            }
            if (S != null && !S.ended) Save();
            if (id != null && TALK_ROLE.TryGetValue(id, out var role)) Speak(role, FirstQuote(text));
            if (UI != null) UI.Show(null, text, choices, id, nm ?? PersonName(id));
            return true;
        }

        /// <summary>JS timed(sec, onExpire): a draining bar; when it runs out, onExpire runs.</summary>
        public static void Timed(float sec, Action onExpire) { if (UI != null) UI.Timed(sec, onExpire); }
        public static void ClearTimed() { if (UI != null) UI.Timed(0, null); }

        public static void FocusOn(string k, string text = null)
        {
            scene.focus = k;
            scene.caption = text ?? (LOC.TryGetValue(k, out var l) ? l : "");
        }

        /// <summary>Sound effects: whistle, creak, rumble, shot, punch, coin, bell, cage, door, shovel, throwrock, maul, derail, ice.</summary>
        public static void Sfx(string name) { if (UI != null) UI.Sfx(name); }

        public static Job FindJob(string id) => JOBS.FirstOrDefault(j => j.id == id) ?? JOBS_CN.FirstOrDefault(j => j.id == id);

        // ---- save / load (PlayerPrefs through the front end) ----
        public static void Save() { if (UI != null && S != null && !S.ended) UI.SaveText(SaveCodec.Encode(S)); }
        public static GameState Load()
        {
            string t = UI?.LoadText();
            if (string.IsNullOrEmpty(t)) return null;
            try { var d = SaveCodec.Decode(t); return d.o != null ? d : null; } catch { return null; }
        }
        public static void DeleteSave() { UI?.SaveText(null); }
    }

    /// <summary>What the game needs from Unity. Implemented by GameUI.</summary>
    public interface IFrontEnd
    {
        /// <param name="portraitId">null, or a PEOPLE id whose portrait is shown with speakerName.</param>
        void Show(string title, string body, List<Choice> choices, string portraitId, string speakerName);
        void Timed(float seconds, Action onExpire);   // seconds 0 = cancel
        void Sfx(string name);
        void SaveText(string text);                   // null = delete
        string LoadText();
        string AskName();                             // the name typed on the title screen
    }
}
