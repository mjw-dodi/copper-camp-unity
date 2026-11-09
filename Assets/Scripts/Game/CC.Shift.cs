// Copper Camp — the mine shift: shared runtime and the five job games (JS startShift, runShift, warnStep,
// finishShift and GAMES). Ticked at a fixed 60 Hz by ShiftRunner in Unity.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace CopperCamp
{
    public class ShiftButton { public string id, label; public bool hold, main; }

    /// <summary>The JS shift object U.</summary>
    public class ShiftState
    {
        public string kind, stance;
        public Job job;
        public int t, dur = 1200, breakAt = 600;
        public float prog, dust, fatigue, heat;
        public int ice = 3;
        public bool over, paused, breakDone;
        public string msg = "", note = "";
        public float pace = 1, boost = 1, sloppy = 0, payBonus = 1, risk;
        public float[] warns;
        public int wi = 0, warnT = -1, pushed = 0, lastNoise = -999;

        // input: held buttons and queued taps
        public readonly Dictionary<string, bool> held = new Dictionary<string, bool>();
        public readonly Dictionary<string, int> taps = new Dictionary<string, int>();
        public bool In(string id) => held.TryGetValue(id, out var v) && v;
        public int Taps(string id) => taps.TryGetValue(id, out var v) ? v : 0;
        public void ClearInput() { held.Clear(); taps.Clear(); }

        // per-game state (the JS U.g)
        public string ph = "ready";
        public int pt, swingT, loads, target, sets, crooked, hoist, hammer, stuck, derails;
        public float m, dir = 1, x, tt, jam, pos, v;
        public bool rest;
        public string res = "";
        public int[] hit = new int[3];
        public readonly float[] zones = { .3f, .6f, .88f };
        public readonly float[] lim = { .6f, .55f, .5f };

        // what the panel shows (the JS ui() functions write these)
        public List<ShiftButton> buttons;
        public readonly Dictionary<string, string> labels = new Dictionary<string, string>();
        public float meterMark = -1, meterW = 0;   // green-zone meter; mark -1 hides the marker
        public float extra;                        // red gauge: drill jam or hot-box heat
        public bool warning => warnT >= 0;
        public bool tramRolling => kind == "tram" && v > 0.02f;
    }

    public static partial class CC
    {
        public static readonly Dictionary<string, string> KINDS = new Dictionary<string, string>
        { { "neversweat", "muck" }, { "mountaincon", "timber" }, { "speculator", "drill" }, { "leonard", "hotmuck" }, { "orphangirl", "tram" } };

        public static readonly Dictionary<string, string> KIND_HINT = new Dictionary<string, string>
        {
            { "muck", "shovel and throw into the car" }, { "timber", "set the swinging timber" }, { "drill", "drill without jamming the steel" },
            { "hotmuck", "shovel while you manage the heat" }, { "tram", "push and brake the ore car" }
        };

        public static readonly Dictionary<string, string> GAME_TITLE = new Dictionary<string, string>
        { { "drill", "Machine man" }, { "muck", "Mucker" }, { "timber", "Timberman" }, { "tram", "Trammer" }, { "hotmuck", "Hot box" } };
        public static readonly Dictionary<string, string> GAME_UNIT = new Dictionary<string, string>
        { { "drill", "round" }, { "muck", "car" }, { "timber", "sets" }, { "tram", "loads" }, { "hotmuck", "car" } };
        public static readonly Dictionary<string, string> GAME_HELP = new Dictionary<string, string>
        {
            { "drill", "<b>Hold the button</b> (or Space) to drill. The steel heats and binds if you hold too long: <b>ease off</b> to cool it (watch the bar). When the timbers creak, <b>let go</b> until the ground settles." },
            { "muck", "<b>Tap DIG</b> to fill the shovel, then <b>tap THROW</b> when the marker is in the green to land it in the car. A tired back shrinks the green. Hold <b>STRAIGHTEN UP</b> (B) to catch your breath." },
            { "timber", "A timber swings from the hoist. <b>Tap</b> (or Space) as it crosses the centre to wedge it plumb. Crooked sets weaken the back, and every blow of the maul wakes the ground." },
            { "tram", "<b>Hold PUSH</b> to get the car rolling, let go to coast, hold <b>SPRAG</b> (B) to brake. Take the marked curves slow or she jumps the rails. Deliver the loads to the station." },
            { "hotmuck", "It is a hundred and ten degrees. <b>Tap DIG</b>, then <b>THROW</b> in the green, as in any muck. The <b>heat bar</b> climbs: tap <b>ICE</b> (B) to knock it down. You have three. At full you faint." }
        };
        public static readonly Dictionary<string, string> UNDER_CAPTION = new Dictionary<string, string>
        {
            { "muck", "shovelling muck at the face" }, { "timber", "setting timber on the 3,000 level" }, { "drill", "the 2,400 level" },
            { "hotmuck", "a hundred and ten degrees on the 2,200" }, { "tram", "the haulage drift" }
        };

        public static bool GameHasMeter(string k) => k == "muck" || k == "hotmuck" || k == "timber";
        public static bool GameHasExtra(string k) => k == "drill" || k == "hotmuck";

        static float ZoneW(ShiftState U) => Math.Max(.12f, .30f - U.fatigue * .14f - U.sloppy * .07f - U.heat * .12f);

        /// <summary>The running shift, or null.</summary>
        public static ShiftState Shift;

        public static void StartShift(Job j, string stance)
        {
            string kind = KINDS[j.id];
            var U = new ShiftState
            {
                kind = kind, job = j, stance = stance,
                pace = stance == "push" ? 1.12f : stance == "careful" ? .9f : 1f,
                risk = j.risk * (stance == "push" ? 1.3f : stance == "careful" ? .7f : 1f) * (S.tip == j.id ? .65f : 1f),
                warns = new[] { 140 + Rand() * 140, 420 + Rand() * 170, 760 + Rand() * 220 },
            };
            if (S.tip == j.id) { S.tip = null; U.note = "You went in ready for it."; }
            GameInit(U);
            S.job = j; S.phase = "work";
            string pn = PersonName("partner");
            U.msg = Rel("partner", 1) == 1
                ? pn + " is your partner. He looks at your hands and says nothing, which is a compliment."
                : Pick(pn + " lights your candle off his. “Listen to the ground,” he says. He says it every shift.",
                       pn + " taps the back with his steel before you start. “She’s talking today.”",
                       "Your partner has saved you the dry side of the drift.");
            Sfx("cage");
            MountShift(U);
            Save();
        }

        static void MountShift(ShiftState U)
        {
            Shift = U;
            scene.mode = "under"; scene.under = U;
            string mn = Cap(MINE[U.job.loc]);
            scene.caption = mn + " — " + UNDER_CAPTION[U.kind] + ". Listen to the ground.";
            GameUi(U);
            ShiftView?.Invoke(U);
        }

        /// <summary>Set by the front end: shows the shift panel for U (or hides it when U is null).</summary>
        public static Action<ShiftState> ShiftView;

        public static string Cap(string s) => s.StartsWith("the ") ? "The " + s.Substring(4) : s;

        /// <summary>Button press from the panel or keyboard: id "main" or "aux".</summary>
        public static void ShiftPress(string id, bool on)
        {
            var U = Shift;
            if (U == null || U.over || U.paused) return;
            var b = U.buttons?.Find(x => x.id == id);
            if (b == null) return;
            if (b.hold) U.held[id] = on;
            else if (on) U.taps[id] = U.Taps(id) + 1;
        }

        public static void ShiftSkip()
        {
            var U = Shift;
            if (U == null || U.over || U.paused) return;
            U.prog = Math.Max(U.prog, .6f);
            FinishShift(U, null);
        }

        /// <summary>One 60 Hz frame of the running shift (JS runShift's frame()).</summary>
        public static void ShiftTick()
        {
            var U = Shift;
            if (U == null || U.over || U.paused) return;
            U.t++;
            GameStep(U);
            if (U.over) return;
            if (U.kind != "tram") { WarnStep(U); if (U.over) return; }
            GameUi(U);
            if (!U.breakDone && U.t >= U.breakAt)
            {
                U.paused = true; U.breakDone = true; U.ClearInput();
                CrewBreak(U, () => { U.paused = false; U.ClearInput(); MountShift(U); });
                return;
            }
            if (U.t >= U.dur || U.prog >= 1.25f)
            {
                U.msg = U.prog >= 1 ? "The round is in. The whistle goes." : "The whistle goes with the work half done.";
                Sfx("whistle");
                FinishShift(U, "safe");
            }
        }

        static void WarnStep(ShiftState U)
        {
            if (U.warnT < 0 && U.wi < U.warns.Length && U.t >= U.warns[U.wi])
            {
                U.warnT = U.t; U.wi++; U.pushed = 0; Sfx("creak");
                U.msg = Pick("The timbers creak over your head.", "A trickle of dust from the back. The cap on the set groans.", "The ground talks. Old timers say you listen or you die.");
            }
            if (U.warnT >= 0)
            {
                int dt = U.t - U.warnT;
                if (U.t - U.lastNoise < 18)
                {
                    U.pushed++;
                    if (U.pushed > 45 && Rand() < U.risk * .016f) { Sfx("rumble"); U.msg = "The back comes down."; FinishShift(U, "accident"); return; }
                }
                if (dt > 150) { U.warnT = -1; U.msg = U.pushed > 45 ? "It holds. This time." : "The ground settles."; U.pushed = 0; }
            }
        }

        /// <summary>Ends the shift. A pending call to DoWork is run by the front end after a short delay.</summary>
        public static void FinishShift(ShiftState U, string forced)
        {
            if (U.over) return;
            U.over = true;
            float sm = U.stance == "push" ? 1.12f : U.stance == "careful" ? .92f : 1f;
            float payMult = Clamp((.7f + .5f * U.prog) * sm * U.payBonus, .55f, 1.45f);
            float lungMult = Clamp((.5f + U.dust / 900f) * (U.stance == "careful" ? .9f : 1f), .4f, 1.4f);
            var res = new WorkResult { payMult = payMult, lungMult = lungMult, forced = forced, riskMult = U.risk / U.job.risk, note = U.note, kind = U.kind, prog = U.prog, stance = U.stance };
            var job = U.job;
            PendingAfterShift = () =>
            {
                Shift = null; scene.under = null; scene.mode = "hill";
                ShiftView?.Invoke(null);
                DoWork(job, res);
            };
            PendingDelay = forced == "accident" ? 1.4f : .7f;
        }

        /// <summary>Set by FinishShift; the front end runs it after PendingDelay seconds.</summary>
        public static Action PendingAfterShift;
        public static float PendingDelay;

        // ---------------- the five games ----------------
        static void GameInit(ShiftState U)
        {
            switch (U.kind)
            {
                case "drill":
                    U.buttons = new List<ShiftButton> { new ShiftButton { id = "main", label = "HOLD TO DRILL", hold = true, main = true } };
                    U.jam = 0; U.stuck = 0; break;
                case "muck":
                    U.buttons = new List<ShiftButton> { new ShiftButton { id = "main", label = "DIG", main = true }, new ShiftButton { id = "aux", label = "STRAIGHTEN UP (hold B)", hold = true } };
                    U.ph = "ready"; U.target = Math.Max(13, 19 - Round(S.skill * .8f)); break;
                case "hotmuck":
                    U.buttons = new List<ShiftButton> { new ShiftButton { id = "main", label = "DIG", main = true }, new ShiftButton { id = "aux", label = "ICE — 3 left (B)" } };
                    U.ph = "ready"; U.target = Math.Max(13, 19 - Round(S.skill * .8f)); break;
                case "timber":
                    U.buttons = new List<ShiftButton> { new ShiftButton { id = "main", label = "SET THE TIMBER", main = true } };
                    U.ph = "swing"; U.target = Math.Max(7, 10 - (int)Math.Floor(S.skill / 2)); break;
                case "tram":
                    U.buttons = new List<ShiftButton> { new ShiftButton { id = "main", label = "HOLD TO PUSH", hold = true, main = true }, new ShiftButton { id = "aux", label = "SPRAG — BRAKE (hold B)", hold = true } };
                    U.target = 4; break;
            }
            foreach (var b in U.buttons) U.labels[b.id] = b.label;
        }

        static void GameStep(ShiftState U)
        {
            switch (U.kind)
            {
                case "drill": DrillStep(U); break;
                case "muck": case "hotmuck": MuckStep(U); break;
                case "timber": TimberStep(U); break;
                case "tram": TramStep(U); break;
            }
        }

        static void GameUi(ShiftState U)
        {
            switch (U.kind)
            {
                case "drill":
                    U.extra = U.jam; U.labels["main"] = U.stuck > 0 ? "BOUND — WAIT" : "HOLD TO DRILL"; break;
                case "muck": case "hotmuck":
                    U.labels["main"] = U.ph == "ready" ? "DIG" : U.ph == "swing" ? "THROW!" : "…";
                    U.meterMark = U.ph == "swing" ? U.m : -1; U.meterW = ZoneW(U);
                    if (U.kind == "hotmuck") { U.labels["aux"] = "ICE — " + U.ice + " left (B)"; U.extra = U.heat; }
                    break;
                case "timber":
                    U.labels["main"] = U.ph == "swing" ? "SET THE TIMBER" : "…";
                    U.meterMark = (U.x + 1) / 2; U.meterW = .14f; break;
            }
        }

        static void DrillStep(ShiftState U)
        {
            if (U.stuck > 0) { U.stuck--; U.jam = Math.Max(0, U.jam - .02f); if (U.stuck == 0) U.msg = "She frees up."; return; }
            if (U.In("main"))
            {
                U.prog += (.00145f + S.skill * .0001f) * U.pace * U.boost; U.dust++; U.lastNoise = U.t; U.jam += .0048f * U.pace * U.boost;
                if (U.jam >= 1) { U.jam = 1; U.stuck = 50; U.msg = "The steel binds in the hole. Rock it loose."; Sfx("thud"); }
            }
            else U.jam = Math.Max(0, U.jam - .022f);
        }

        static void MuckStep(ShiftState U)
        {
            float f = U.fatigue;
            if (U.ph == "ready")
            {
                if (U.kind == "muck" && U.In("aux")) { U.fatigue = Math.Max(0, f - .012f); U.taps["main"] = 0; U.rest = true; goto heat; }
                U.rest = false; U.fatigue = Math.Max(0, f - .0007f);
                if (U.Taps("main") > 0) { U.taps["main"] = 0; U.ph = "dig"; U.pt = 0; Sfx("shovel"); }
            }
            else if (U.ph == "dig")
            {
                U.pt++; U.lastNoise = U.t; U.dust += .8f;
                if (U.pt >= Round(14 * (1 + f * .8f) / (U.pace * U.boost))) { U.ph = "swing"; U.m = 0; U.dir = 1; U.swingT = 0; U.taps["main"] = 0; }
            }
            else if (U.ph == "swing")
            {
                U.swingT++; U.m += U.dir * .024f * (1 + f * .5f) * U.pace * U.boost;
                if (U.m >= 1) { U.m = 1; U.dir = -1; }
                if (U.m <= 0) { U.m = 0; U.dir = 1; }
                if (U.Taps("main") > 0 || U.swingT > 170)
                {
                    bool hitTap = U.Taps("main") > 0; float w = ZoneW(U), d = Math.Abs(U.m - .5f); U.taps["main"] = 0;
                    if (hitTap && d < w / 2)
                    {
                        U.loads++; U.prog += 1f / U.target; U.res = "good";
                        U.msg = Pick("It lands in the car with a sound like a good decision.", "Clean throw. The car sits a little lower on its springs.", "Another shovelful, another nickel.");
                    }
                    else if (hitTap && d < w) { U.prog += .5f / U.target; U.res = "half"; U.msg = "Half of it goes over the side of the car."; }
                    else { U.prog += .12f / U.target; U.res = "miss"; U.msg = hitTap ? "You throw it against the rib. The muck stays where it was." : "You hang there too long and it slides off the blade."; }
                    U.fatigue = Math.Min(1, f + .035f + (U.res == "miss" ? .015f : 0)); U.ph = "throw"; U.pt = 0; Sfx("throwrock");
                }
            }
            else if (U.ph == "throw")
            {
                U.pt++; U.lastNoise = U.t; U.dust += .5f;
                if (U.pt >= 8) { U.ph = "ready"; U.taps["main"] = 0; }
            }
            heat:
            if (U.kind == "hotmuck")
            {
                U.heat += (U.ph == "ready" ? .00044f : .00075f) * (1 + U.fatigue * .5f) * U.pace;
                if (U.Taps("aux") > 0)
                {
                    U.taps["aux"] = 0;
                    if (U.ice > 0 && U.heat > .1f) { U.ice--; Sfx("ice"); U.heat = Math.Max(0, U.heat - .5f); U.msg = "Ice down your back. For a minute you can think again."; }
                }
                if (U.heat >= 1) { U.msg = "The heat takes you. You wake at the station with ice on your chest."; U.heat = 1; FinishShift(U, "accident"); }
            }
        }

        static void TimberStep(ShiftState U)
        {
            if (U.ph == "swing")
            {
                U.tt += U.pace * U.boost * (1 + U.fatigue * .2f); U.x = (float)Math.Sin(U.tt * .04f);
                if (U.Taps("main") > 0)
                {
                    U.taps["main"] = 0; float e = Math.Abs(U.x), q = e < .14f ? 1f : e < .32f ? .65f : .25f; U.sets++; U.prog += q / U.target;
                    if (e >= .32f) { U.crooked++; U.risk *= 1.18f; U.msg = "Crooked. She’ll hold, but the back doesn’t like it."; }
                    else U.msg = e < .14f ? "Plumb. The cap beam sits like it grew there." : "Close enough for the Company.";
                    U.res = q >= 1 ? "perfect" : q > .5f ? "good" : "crooked"; U.ph = "hammer"; U.hammer = 0; Sfx("maul");
                }
            }
            else if (U.ph == "hammer")
            {
                U.hammer++; U.lastNoise = U.t; U.dust += 1.3f;
                if (U.hammer % 8 == 1 && U.hammer > 1) Sfx("maul");
                if (U.hammer >= 24) { U.ph = "hoist"; U.hoist = 0; }
            }
            else if (U.ph == "hoist")
            {
                U.hoist++;
                if (U.hoist >= 28) { U.ph = "swing"; U.taps["main"] = 0; }
            }
            if (U.ph != "swing") U.taps["main"] = 0;
        }

        static void TramStep(ShiftState U)
        {
            if (U.stuck > 0) { U.stuck--; U.v = 0; return; }
            if (U.In("main")) U.v = Math.Min(1, U.v + .012f * U.pace * U.boost); else U.v = Math.Max(0, U.v - .004f);
            if (U.In("aux")) U.v = Math.Max(0, U.v - .03f);
            U.pos += U.v * .009f; U.dust += U.v * .35f;
            for (int i = 0; i < 3; i++)
            {
                if (U.hit[i] == 0 && U.pos > U.zones[i])
                {
                    U.hit[i] = 1;
                    if (U.v > U.lim[i] + .15f)
                    {
                        U.derails++; U.stuck = 100; U.prog = Math.Max(0, U.prog - .04f);
                        U.msg = "She jumps the rails on the curve. Two men and a jack to put her back."; Sfx("derail");
                        if (Rand() < .15f * (U.risk / U.job.risk)) { U.msg = "The car jumps the rails and takes your hand against the rib."; FinishShift(U, "accident"); return; }
                    }
                }
            }
            if (U.pos >= 1)
            {
                if (U.v <= .42f) { U.loads++; U.prog += 1f / U.target; U.msg = "Delivered. The skip man waves you back for another."; }
                else { U.prog += .5f / U.target; U.msg = "You hit the stop too hard and half the load goes over the side."; }
                U.pos = 0; U.hit = new int[3];
            }
        }
    }

    /// <summary>The JS res object passed to doWork.</summary>
    public class WorkResult
    {
        public float payMult = 1, lungMult = 1, riskMult = 1, prog;
        public string forced;   // null | "safe" | "accident"
        public string note, kind, stance;
    }
}
