// Copper Camp — the game flow: title, season loop, the dry room, counter jobs, results, the evening in town,
// season end, the newspaper, scripted history, the dinner-pail break and the endings.
// A close port of the JS (title, newGame, resume, workPhase, doWork, crewBreak, leisurePhase, endSeason, ending …).
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace CopperCamp
{
    public static partial class CC
    {
        // ---------- small JS helpers used only here ----------
        /// <summary>JS Math.round on a double.</summary>
        static int JsRoundD(double v) => (int)Math.Floor(v + 0.5);
        /// <summary>JS String.replace with a string pattern: first occurrence only.</summary>
        static string ReplaceFirstJs(string s, string a, string b)
        {
            int i = s.IndexOf(a, StringComparison.Ordinal);
            return i < 0 ? s : s.Substring(0, i) + b + s.Substring(i + a.Length);
        }
        static Fx CopyFx(Fx f) => f == null ? new Fx() : new Fx { money = f.money, health = f.health, lungs = f.lungs, morale = f.morale, rep = f.rep, heat = f.heat, skill = f.skill };

        /// <summary>JS INTERIOR: places with a drawn interior, and its kind.</summary>
        public static readonly Dictionary<string, string> INTERIOR = new Dictionary<string, string>
        {
            { "dry", "dry" }, { "mm", "bar" }, { "mercury", "bar" }, { "union", "hall" }, { "church", "church" }, { "library", "shelves" },
            { "gamers", "counter" }, { "hennessy", "store" }, { "sauna", "steam" }, { "stjames", "ward" }, { "home", "room" },
        };

        // ====================== TITLE ======================
        /// <summary>True while the title screen shows; the UI then shows the name field.</summary>
        public static bool TitleMode;
        static string sel;

        public static void Title()
        {
            TitleMode = true;
            scene.focus = null; scene.caption = "Looking north from the Flat toward the Butte Hill. Big Butte on the left, Granite Mountain on the right.";
            string body = "<p>Butte, 1906. The richest hill on earth buries a man a week. Survive twelve years of it — the mines, the Company, the M&M — and come out with a house on the Flat or a ticket home.</p><p class=\"note\">Pick where you came from. Each origin plays differently.</p>";
            var saved = Load();
            var ch = new List<Choice>();
            // the origin buttons (in the JS they sit in the body; the perk shows only for the selected one)
            foreach (var o in ORIGINS)
                ch.Add(Ch(o.name + " — " + o.where, o.desc + (sel == o.id ? "<br><em>" + o.perk + "</em>" : ""), () => { sel = o.id; FocusOn(o.home); Title(); }));
            if (saved != null) ch.Add(Ch("Continue as " + saved.name + ", " + SEASONS[saved.season] + " " + saved.year, saved.o.name + ", from " + saved.o.where + ". $" + saved.money + " saved.", () => Resume(saved)));
            ch.Add(Ch("Step off the train", sel != null ? "" : "Pick an origin first.", () => NewGame(ORIGINS.First(o => o.id == sel), (UI?.AskName() ?? "").Trim()), disabled: sel == null));
            Render("Copper Camp", body, ch);
            scene.mode = "photo"; scene.caption = "The Butte Hill, 1906 — an AI-generated view in the style of an old photograph.";
        }

        public static void NewGame(Origin o, string name)
        {
            name = Regex.Replace(name ?? "", "[<>&\"\\\\]", "");
            name = Regex.Replace(name, @"\s+", " ").Trim();
            if (name.Length > 24) name = name.Substring(0, 24);
            TitleMode = false;
            S = new GameState
            {
                name = name != "" ? name : "Jack", originId = o.id, year = 1906, season = 0,
                health = o.health, lungs = o.lungs, morale = o.morale, money = (int)o.money, rep = o.rep, heat = 0, skill = o.skill,
                card = false, blacklisted = false, strike = false, halfpay = false, sent = 0, fights = 0, job = null, phase = "work",
            };
            scene.walk = null; scene.mode = "hill";
            Log("You step off the Northern Pacific into the smoke.");
            WorkPhase();
        }

        public static void Resume(GameState d)
        {
            S = d; TitleMode = false;
            if (S.phase == "work") WorkPhase();
            else if (S.phase == "worked") EventPhase();
            else if (S.phase == "end") NextSeason();
            else LeisurePhase();
        }

        // ====================== SEASON FLOW ======================
        public static void WorkPhase()
        {
            S.phase = "work";
            if (!F.Has("seasonInit") || F.I("seasonInit") != TurnIdx()) { F.SetI("seasonInit", TurnIdx()); S.slots = 3; S.used.Clear(); S.shifts = 0; F.Set("mined", false); S.seasonPay = 0; }
            if (scene.walk != null) scene.walk.inside = false;
            if (ScriptedOnce(Key(), PRE)) return;
            bool cn = S.o.id == "chinese"; var jobs = cn ? JOBS_CN : JOBS;
            int sh = S.shifts;
            string intro = "<p>" + (sh == 0 ? Flavor() : "You are back at the dry room with the day’s dust still in your collar. So far this season: <b>$" + S.seasonPay + "</b> earned.") + "</p><p><b>Where will you work" + (sh != 0 ? " your next shift" : " this season") + "?</b> <span class=\"note\">A season is three shifts. Try a different job each time and see how they differ" + (S.year == 1906 && S.season == 0 ? ". Higher pay means worse air; skill makes you safer" : "") + ".</span></p>";
            if (F.B("jailed"))
            {
                F.Set("jailed", false);
                Render("The county jail", "<p>Ninety days of county beans and a view of the courthouse wall. The hill goes on without you. Board at the jail is free, which is the only kind thing anyone says about it.</p>",
                    L(Ch("Serve it out", null, () => { S.job = JOBS[JOBS.Length - 1]; Apply(new Fx { health = -4, morale = -6 }); S.money += 130; Log("Served ninety days."); EventPhase(); })),
                    new Opts { loc = "courthouse", dark = true });
                return;
            }
            var ch = new List<Choice>();
            foreach (var j in jobs)
            {
                bool dis = false; string why = "";
                if (!cn)
                {
                    if (S.year >= 1913 && j.co && !S.card) { dis = true; why = "Consolidated mines require a rustling card."; }
                    if (S.blacklisted && j.co) { dis = true; why = "Your name is on the Company list."; }
                    if (F.B("speculatorBurned") && j.id == "speculator") { dis = true; why = "The Speculator is sealed after the fire."; }
                    if (S.strike && j.id != "idle" && j.id != "hennessy") { dis = true; why = "The hill is struck."; }
                    if (F.B("sworn") && j.risk != 0) { dis = true; why = "You swore it over his grave."; }
                }
                int pay = JsRoundD(JobPay(j) / (double)SHIFTS);
                ch.Add(Ch(j.name,
                    (why != "" ? why : j.desc) + (KINDS.ContainsKey(j.id) ? " <em>Game: " + KIND_HINT[KINDS[j.id]] + ".</em>" : "") + (j.risk != 0 ? " <em>Risk: " + RiskWord(j) + ". Dust: " + (j.lungs > 5 ? "heavy" : j.lungs > 3 ? "moderate" : "light") + ".</em>" : ""),
                    () => DoWork(j, null), cost: pay != 0 ? -pay : 0, disabled: dis));
            }
            if (sh > 0) ch.Add(Ch("Head to town for the evening", "Stop working this season. Fewer shifts means less pay.", EventPhase));
            Render(DateStr() + " — shift " + (sh + 1) + " of " + SHIFTS, intro, ch, new Opts { loc = S.o.home });
        }

        static readonly Dictionary<int, string[]> FLAVOR = new Dictionary<int, string[]>
        {
            { 0, new[] { "The snow goes off the hill and the streets of Uptown run brown to the creek.", "Spring. The Milwaukee Road brings another trainload of men with cardboard suitcases." } },
            { 1, new[] { "Summer dust on Granite Street. The Columbia Gardens streetcar is full every Sunday.", "The smelter smoke lies flat over the Flat in the heat." } },
            { 2, new[] { "Autumn. The boarding houses fill with men back from the harvest.", "The first frost and the stoves are lit in every shack on the hill." } },
            { 3, new[] { "Forty below on the Hill. Men freeze walking home from the night shift.", "Christmas in the camp. The M&M puts a turkey on the free lunch." } },
        };
        public static string Flavor() => Pick(FLAVOR[S.season]);

        public static string RiskWord(Job j)
        {
            float r = j.risk * (1 - S.skill * .04f);
            return r > .2f ? "high" : r > .12f ? "moderate" : "low";
        }

        public static int JobPay(Job j)
        {
            double p = j.pay;
            if (F.B("companyman") && j.co) p *= 1.25;
            if (F.B("mulcahy") && j.co) p *= .85;
            if (S.o.id == "cornish" && j.risk != 0) p *= 1.08;
            if (S.o.id == "serb" && S.year < 1908 && !F.B("english")) p *= .9;
            if (S.payTip == j.id && TurnIdx() <= S.payTipTurn + 1) p *= 1.1;
            if (S.halfpay) p *= .5;
            if (S.o.id == "chinese" && F.B("boycott") && j.id != "idle") p *= .6;
            return JsRoundD(p);
        }

        // ====================== THE DRY ROOM ======================
        public static void ShiftIntro(Job j)
        {
            S.job = j; S.phase = "work"; BossFor(j);
            if (j.risk == 0) { CounterScene(j); return; }
            string kind = KINDS[j.id], pn = PersonName("partner");
            if (S.tip != null && TurnIdx() > S.tipTurn + 1) S.tip = null;
            var briefs = new Dictionary<string, string>
            {
                { "muck", "The round was shot last shift. There’s a hill of muck at the face and one car on the track. Fill it before the whistle." },
                { "timber", "The back is loose from the last blast. I want square sets in before the dinner pail, and I want them plumb." },
                { "drill", "Six holes in the face, and mind the fuse. The machine man gets the bonus and the dust." },
                { "hotmuck", "It’s a hundred and ten on this level. There’s ice at the station, and it’s not for the boss." },
                { "tram", "Ore from the chute to the station. Loaded cars down, empties back. Keep her on the rails and take the curves slow." },
            };
            string t = "“" + briefs[kind] + "”";
            if (F.B("mulcahy") && j.co) t += " He doesn’t look at you when he says it. The good contracts went to somebody else last week.";
            if (S.year >= 1912 && j.co) t += " Your rustling card is in his shirt pocket until the whistle.";
            if (S.tip == j.id) t += " <em>" + pn + " catches your eye: he heard the same thing you did about the ground here. You’ll be listening.</em>";
            Action go(string s) => () => StartShift(j, s);
            Talk("boss", t, L(
                Ch("Steady as she goes", "Fill the car, go home whole. Normal pay, normal risk.", go("steady")),
                Ch("Push for the contract bonus", "Faster pace and up to 12% more pay, but about 30% more chance of a bad one.", go("push")),
                Ch("Take it slow and watch the ground", "Slower pace and about 8% less pay, but 30% fewer accidents.", go("careful")),
                Ch("Skip to the whistle", "No game. Take the Company’s odds.", () => DoWork(j, new WorkResult { payMult = 1, lungMult = 1, forced = null, riskMult = 1 }))),
                new Opts { loc = j.loc, dark = true, caption = Cap(MINE[j.loc]) + " — the dry room, before the cage" });
            scene.mode = "dry";
        }

        // ====================== COUNTER JOBS ======================
        public class CounterChoice { public string t, d, note; public int money; public float pay; public Fx fx; public Action go; }
        public class CounterVisit { public string who, name, text; public CounterChoice[] ch; }

        public static readonly Dictionary<string, CounterVisit[]> COUNTER = new Dictionary<string, CounterVisit[]>
        {
            { "hennessy", new[] {
                new CounterVisit { who = "customer", name = "Mrs. Kovacevich", text = "She stands at the dry-goods counter with a child on each side. “My husband is in St. James. The Company says the doctor is not the Company’s doing. I need flour until Saturday.” Mr. Pruett’s ledger is open on the shelf behind you.", ch = new[] {
                    new CounterChoice { t = "Put it on credit", d = "It comes out of your pay.", money = -15, fx = new Fx { rep = 6, morale = 4 }, note = "You put Mrs. Kovacevich’s flour on your own account." },
                    new CounterChoice { t = "Tell her the ledger is closed", fx = new Fx { rep = -2, morale = -3 } } } },
                new CounterVisit { who = "gentleman", name = "Pruett, from the sixth floor", text = "Pruett drifts in from the stairs and runs a finger along the shelf. “The union men are paid on Friday. A nickel on the dollar to anyone in a union button; the Company prefers it to go uncounted.”", ch = new[] {
                    new CounterChoice { t = "Do as he says", d = "Twenty dollars for you at the end of the week.", money = 20, fx = new Fx { rep = -8, morale = -4 }, note = "You added a nickel on the dollar for the union men." },
                    new CounterChoice { t = "Ring it up honest", fx = new Fx { heat = 4, rep = 3 } } } },
                new CounterVisit { who = "customer", name = "A boy at the cartridge case", text = "A boy of about twelve has two boxes of shells in his coat and his eyes on the door. The floorwalker is looking the other way.", ch = new[] {
                    new CounterChoice { t = "Look away", fx = new Fx { morale = 3 }, note = "You let a boy go with two boxes of shells." },
                    new CounterChoice { t = "Collar him", d = "Five dollars from the manager.", money = 5, fx = new Fx { rep = -4, morale = -2 } } } },
                new CounterVisit { who = "gentleman", name = "A man from the Deacon’s crowd", text = "He wants two hundred rounds “for hunting.” There is nothing in season but the union.", ch = new[] {
                    new CounterChoice { t = "Sell it", money = 10, fx = new Fx { heat = 3 } },
                    new CounterChoice { t = "“Out of stock”", fx = new Fx { heat = -2 } } } } } },
            { "pekin", new[] {
                new CounterVisit { who = "customer", name = "A graveyard-shift Cornishman", text = "Two in the morning. The graveyard shift is stacked four deep at the counter, and a drunk Cornishman holds out a dollar for a fifty-cent bowl and says keep it.", ch = new[] {
                    new CounterChoice { t = "Give him his change", fx = new Fx { rep = 2, morale = 1 } },
                    new CounterChoice { t = "Keep it", money = 8, fx = new Fx { morale = -3 } } } },
                new CounterVisit { who = "wobbly", name = "A picket from the union hall", text = "A union man with a button in his lapel sits at your counter and asks the price of a bowl, loudly, so the room can hear. “White men,” he says, “eat at white places.”", ch = new[] {
                    new CounterChoice { t = "Serve him anyway, politely", fx = new Fx { rep = 3, heat = 2 }, note = "You served the picket politely." },
                    new CounterChoice { t = "Close the counter for the afternoon", pay = .8f, fx = new Fx { morale = -4 } } } },
                new CounterVisit { who = "customer", name = "An old miner with a cough", text = "He eats slowly, and when he reaches for his pocket you can see there is nothing in it. The cough comes from the bottom of him.", ch = new[] {
                    new CounterChoice { t = "Tell him it’s on the house", money = -5, fx = new Fx { morale = 4, rep = 3 } },
                    new CounterChoice { t = "Let the owner decide", fx = new Fx { morale = -3 } } } } } },
            { "laundry", new[] {
                new CounterVisit { who = "customer", name = "A boarding-house cook", text = "A bundle comes in with a shirt stiff with dried blood and a torn collar. “There was a disagreement,” she says, and looks at the door.", ch = new[] {
                    new CounterChoice { t = "Wash it and say nothing", fx = new Fx { rep = 2 } },
                    new CounterChoice { t = "Tell her you saw nothing and charge double", money = 6, fx = new Fx { morale = -2 } } } },
                new CounterVisit { who = "gentleman", name = "A man in a clean collar", text = "A Company man leaves a coat and tells you to have it ready Thursday. There is a folded paper in the pocket.", ch = new[] {
                    new CounterChoice { t = "Return it unread", fx = new Fx { heat = -2 } },
                    new CounterChoice { t = "Read it", d = "You might learn something. You might wish you hadn’t.", go = () => { Apply(new Fx { heat = 5, rep = 3 }); F.Set("stoolTip", true); } } } },
                new CounterVisit { who = "customer", name = "Steam", text = "The copper is boiling and the room is a fog. Your hands are scalded to the wrist and there are forty shirts still to press.", ch = new[] {
                    new CounterChoice { t = "Keep pressing", pay = 1.1f, fx = new Fx { health = -5 } },
                    new CounterChoice { t = "Soak your hands and rest", fx = new Fx { health = 3 }, pay = .85f } } } } },
            { "herbs", new[] {
                new CounterVisit { who = "customer", name = "A miner who can’t pay", text = "A Cornishman with a cough that goes all the way down wants a tonic and has a dollar less than the price. “Put it on the book.”", ch = new[] {
                    new CounterChoice { t = "Put it on the book", money = -4, fx = new Fx { rep = 3, morale = 3 } },
                    new CounterChoice { t = "No book for strangers", fx = new Fx { rep = -2 } } } },
                new CounterVisit { who = "gentleman", name = "The county attorney’s clerk", text = "A clerk comes in asking, politely, who is practicing medicine in the back room without a license.", ch = new[] {
                    new CounterChoice { t = "“Only tea and silk here”", fx = new Fx { heat = 2 } },
                    new CounterChoice { t = "Tell him to come back with a warrant", fx = new Fx { rep = 3, heat = 6 } } } },
                new CounterVisit { who = "customer", name = "A woman sending money to Taishan", text = "She wants a draft to her husband’s village and can’t write the characters. The ledger takes a quarter of an hour.", ch = new[] {
                    new CounterChoice { t = "Write it for her, free", fx = new Fx { rep = 3, morale = 2 } },
                    new CounterChoice { t = "Charge the usual fee", money = 3 } } } } },
        };

        public static void CounterScene(Job j)
        {
            var pool = COUNTER.TryGetValue(j.id, out var p) ? p : new CounterVisit[0];
            if (pool.Length == 0) { DoWork(j, new WorkResult { payMult = 1, lungMult = 1, forced = null }); return; }
            var v = Pick(pool);
            Talk(v.who, v.text, v.ch.Select(c => Ch(c.t, c.d, () =>
            {
                if (c.fx != null) Apply(c.fx);
                if (c.money != 0) S.money += c.money;
                c.go?.Invoke();
                DoWork(j, new WorkResult { payMult = c.pay != 0 ? c.pay : 1, lungMult = 1, forced = null, note = c.note, riskMult = 1, kind = "counter" });
            })).ToList(), new Opts { loc = j.loc ?? S.o.home, caption = (j.name ?? "") + " — the counter" }, v.name);
        }

        // ====================== RESULTS ======================
        public static List<Choice> AfterShiftChoices()
        {
            int left = SHIFTS - S.shifts;
            if (S.health <= 0 || left <= 0) return L(Ch("Walk home", null, EventPhase));
            return L(Ch("Work another shift", left + " left this season. Try a different job and see how it plays.", WorkPhase),
                     Ch("Head to town for the evening", "Stop working this season. Fewer shifts means less pay.", EventPhase));
        }

        static int RelGet(string id) => S.rel.TryGetValue(id, out var v) ? v : 0;

        public static void DoWork(Job j, WorkResult res)
        {
            if (res == null && j.id != "idle") { ShiftIntro(j); return; }
            res = res ?? new WorkResult { payMult = 1, lungMult = 1, forced = null };
            S.job = j; S.phase = "worked";
            int pay = JsRoundD(JobPay(j) * (double)res.payMult / SHIFTS);
            string html = "", cls = ""; bool dark = false;
            if (j.id == "idle")
            {
                html = "<p>You sit the season out in your room at " + HomeName() + ". The board bill comes anyway.</p>";
                Apply(new Fx { health = 6, morale = -6 }); S.shifts = SHIFTS;
            }
            else
            {
                S.money += pay; S.seasonPay += pay; S.shifts++; if (j.risk != 0) F.Set("mined", true);
                Apply(new Fx { lungs = -j.lungs * res.lungMult / SHIFTS });
                if (S.o.id == "chinese")
                {
                    html = "<p>" + Pick("Steam, starch and sixteen-hour days.", "Miners off the graveyard shift eat at two in the morning and tip in copper.", "The mercantile ledger runs long on credit and short on cash.") + " You clear <b>$" + pay + "</b>.</p>";
                    if (Rand() < .25f) { F.Set("boycott", true); html += "<div class=\"result bad\">Boycott signs go up again on Main Street: <em>Patronize white labor.</em> Trade falls off by half until it blows over.</div>"; Log("Boycott against Chinese businesses."); }
                    else F.Set("boycott", false);
                    Apply(new Fx { morale = -2 });
                }
                else
                {
                    S.skill = Clamp(S.skill + (j.risk != 0 ? .3f / SHIFTS : 0), 0, 8);
                    float r = j.risk * (1 - S.skill * .04f) * (res.riskMult != 0 ? res.riskMult : 1) / SHIFTS;
                    html = "<p>" + Pick("Three shifts a day, seven days a week, and the rock never stops.", "The dry room steams. Your candle gutters in the bad air.", "Payday at the Company window; the line runs down the hill.") + " You draw <b>$" + pay + "</b> for the shift" + (res.payMult > 1.1f ? ", with the contract bonus for clean rounds" : res.payMult < .85f ? ", docked for short rounds" : "") + ".</p>";
                    if (res.forced == "accident" || (res.forced != "safe" && Rand() < r) || (res.forced == "safe" && Rand() < r * .25f))
                    {
                        var acc = Pick(ACCIDENTS[j.loc]);
                        int dmg = Round(acc.lo + Rand() * (acc.hi - acc.lo));
                        if (dmg > 30 && Rand() < .12f) { S.health = 0; F.SetS("killed", acc.text); Ending("dead"); return; }
                        Apply(new Fx { health = -dmg, morale = -10 });
                        int pr = RelGet("partner"); string saved = "";
                        if (RelGet("greenhorn") >= 3 && Rand() < .35f) { Apply(new Fx { health = 6 }); saved = " The kid you showed the shovel to runs for the shift boss and comes back with the whole crew."; }
                        else if (pr >= 4 && Rand() < .6f) { Apply(new Fx { health = 8 }); saved = " " + PersonName("partner") + " digs you out with his hands before the crew gets there."; }
                        else if (pr >= 2 && Rand() < .25f) { F.Set("partnerDead", true); S.rel["partner"] = 0; saved = " " + PersonName("partner") + " was on the other side of the set. They bring him up under a blanket."; Apply(new Fx { morale = -15 }); Log("Buried " + PersonName("partner") + "."); }
                        html += "<div class=\"result bad\">" + acc.text + " You lose " + dmg + " health and two weeks' wages." + saved + "</div>";
                        S.money -= Round(pay * .15f); cls = "bad"; dark = true; Log("Hurt at " + MINE[j.loc] + ".");
                    }
                    else if (Rand() < .25f)
                    {
                        html += "<div class=\"result good\">" + Pick("Your crew breaks into a vein of pure chalcocite. The shift boss stands a round.", "You spot the bad ground before it comes down and the whole crew knows it.", "The hoist man lets you ride the top of the cage. Nothing happens. You feel like a king.") + "</div>";
                        Apply(new Fx { rep = 4, morale = 5 });
                    }
                    if (S.lungs < 35) html += "<p class=\"note\">You cough into your sleeve on the walk home. The old timers call it miner's con and they don't look at you when they say it.</p>";
                }
            }
            if (S.lungs < 35) Apply(new Fx { health = -2 });
            if (S.lungs < 15) Apply(new Fx { health = -3 });
            Render(j.name, html + (!string.IsNullOrEmpty(res.note) ? "<p class=\"note\">" + res.note + "</p>" : ""), AfterShiftChoices(), new Opts { loc = j.loc ?? S.o.home, dark = dark });
        }

        public static string HomeName()
        {
            switch (S.o.home)
            {
                case "gulch": return "the Mullin House in Dublin Gulch";
                case "centerville": return "a Cornish boarding house in Centerville";
                case "finntown": return "the Big Ship on East Broadway";
                case "meaderville": return "the family house in Meaderville";
                case "eastside": return "a Serb boarding house on East Park";
                case "chinatown": return "the rooms above Wah Chong Tai";
            }
            return null;
        }

        // ====================== EVENTS ======================
        public static void EventPhase()
        {
            S.phase = "event";
            if (S.health <= 0) { Ending("dead"); return; }
            if (ScriptedOnce(Key(), MID)) return;
            var pool = new List<int>();
            for (int i = 0; i < EVENTS.Count; i++)
            {
                var e = EVENTS[i];
                if ((e.when == null || e.when()) && string.IsNullOrEmpty(e.at) && !(S.ev.ContainsKey(i) && TurnIdx() - S.ev[i] < (e.cool != 0 ? e.cool : 4))) pool.Add(i);
            }
            var unseen = pool.Where(i => !S.ev.ContainsKey(i)).ToList();
            if (Rand() < .78f && pool.Count > 0)
            {
                int i = Pick<int>(unseen.Count > 0 && Rand() < .7f ? unseen : pool);
                S.ev[i] = TurnIdx(); EVENTS[i].run();
            }
            else LeisurePhase();
        }

        // ====================== SCRIPTED HISTORY ======================
        public static readonly string[] PRE = { "1906-0", "1908-0", "1910-0", "1912-3", "1914-2", "1917-2" };
        public static readonly string[] MID = { "1907-3", "1914-1", "1917-1" };

        public static bool ScriptedOnce(string k, string[] set)
        {
            if (!set.Contains(k) || F.B("h" + k)) return false;
            F.Set("h" + k, true); HISTORY[k](); return true;
        }

        public static readonly Dictionary<string, Action> HISTORY = new Dictionary<string, Action>
        {
            { "1906-0", () => Render("Spring 1906", "<p><b>Each season you work up to three shifts</b> (try different jobs: each one plays differently) <b>and spend three hours in town.</b> Watch your lungs — rock dust doesn't heal — and watch the Company, which keeps a list. Reach Winter 1917 alive.</p><p class=\"note\">Number keys pick choices. After each choice, what it cost you shows under the heading.</p><details><summary class=\"note\" style=\"cursor:pointer\">The hill you've come to</summary><p>Three men fought for this hill. Cornelius Vance, the Senator, who bought the legislature by the head and owns the Orphan Girl, the streetcar and Columbia Gardens. Augustus Brandt, the Litigant, who mined under his neighbors' claims with a lawyer at each shaft collar. And Silas Hobart, the Sixth Floor, who runs the Consolidated Copper Company for the eastern trust from the top of the Hennessy Building and has just bought Brandt out for a private railcar and twelve million dollars. The war is over and Hobart won. The hill is the Consolidated's now, from the Neversweat to the Leonard, and the Company knows your name before you do.</p><p>You find a bunk at " + HomeName() + ". Board is a dollar a day. The shifts are eight hours, the wage is three-fifty, and the Butte Miners' Union has thirteen thousand members and the biggest hall west of Chicago.</p></details>",
                L(Ch("Go to work", "Your first choice: a mine, or a safer job above ground.", WorkPhase)), new Opts { loc = S.o.home }) },
            { "1907-3", () => Render("The Panic of 1907", "<p>The banks in New York go under in October and copper falls from twenty-five cents to twelve. The Company closes every shaft on the hill for the winter. Ten thousand men with no work and nowhere to go. The M&M keeps the free lunch going; the boarding houses carry men on credit; some don't.</p>", L(
                Ch("Ride it out on credit", "Half pay next season while the hill reopens.", () => { F.Set("halfNext", true); S.money -= 40; Apply(new Fx { morale = -10 }); Log("Panic of 1907; mines closed."); LeisurePhase(); }),
                Ch("Go work the harvest in the Gallatin", "Hard, cheap, and out of the smoke.", () => { S.money += 60; Apply(new Fx { health = 8, morale = -4 }); Log("Worked the harvest through the Panic."); LeisurePhase(); })), new Opts { loc = "hennessy" }) },
            { "1908-0", () => Render("The hill reopens", "<p>The hoists start again in March. The Company takes men back at its own pace and its own price. " + (S.halfpay ? "You work half-time until summer." : "You were lucky.") + "</p>", L(Ch("Back to work", null, WorkPhase)), new Opts { loc = "anaconda" }) },
            { "1910-0", () => Render("One Company", "<p>Hobart's Consolidated absorbs what was left of Vance's and Brandt's mines; Vance keeps the Orphan Girl, the streetcar line and his seat in Washington, and Brandt is in New York suing somebody else. One payroll, one hiring hall, one list, kept by Hobart's chief of detectives, a Pinkerton out of Chicago the men call Dutch Kessler. The union negotiates and keeps the three-fifty, but the men who sit across the table from the Company now all seem to have new suits.</p>", L(Ch("Go to work", null, WorkPhase)), new Opts { loc = "hennessy" }) },
            { "1912-3", () =>
                {
                    if (S.o.id == "chinese") { Render("The rustling card", "<p>The Company announces that from the new year no man works in a Consolidated mine without a card from Enoch Pruett's desk on the sixth floor. It means nothing to China Alley, where the union has barred you since before you arrived.</p>", L(Ch("Go on", null, WorkPhase)), new Opts { loc = "hennessy" }); return; }
                    Render("The rustling card", "<p>The Company announces that from the new year no man works in a Consolidated mine without a card from the hiring office on the sixth floor of the Hennessy Building. To get one you give your name, your history, and your lodges to a clerk named Enoch Pruett, who writes slowly and remembers everything. Kessler's detectives check. The union calls it a blacklist. The union, you notice, does not strike.</p>", L(
                        Ch("Apply for a card", "The Company reads your file.", () =>
                        {
                            if (S.heat > 55 || (!F.Has("stool") && S.rep > 65 && Rand() < .7f))
                            {
                                S.blacklisted = true; Apply(new Fx { morale = -15, rep = 10 }); Log("Denied a rustling card. Blacklisted.");
                                Render("Denied", "<p>The clerk reads a card, looks at you, and says the Company is not hiring. He does not say why. He does not have to. The North Butte company at the Speculator still takes men without asking, and so does the Orphan Girl.</p>", L(Ch("Go on", null, WorkPhase)), new Opts { loc = "hennessy", dark = true });
                            }
                            else
                            {
                                S.card = true; Apply(new Fx { heat = -5, rep = -4 }); Log("Issued a rustling card.");
                                Render("Issued", "<p>A pasteboard card with your name and a number. Men on the hill call it a dog tag. You keep it in your boot.</p>", L(Ch("Go on", null, WorkPhase)), new Opts { loc = "hennessy" });
                            }
                        }),
                        Ch("Refuse on principle", "Only the Speculator, the Orphan Girl and Hennessy's will have you.", () => { Apply(new Fx { rep = 15, heat = 10, morale = 5 }); Log("Refused the rustling card."); WorkPhase(); })), new Opts { loc = "hennessy" });
                } },
            { "1914-1", () =>
                {
                    if (S.o.id == "chinese") { Render("Miners' Day, 1914", "<p>On the 13th of June the Miners' Union parade turns into a riot on North Main. On the 23rd somebody dynamites the Union Hall; twenty-six charges, one after another, while a crowd watches from across the street. The men who ran the hill for thirty years are finished in an evening. China Alley locks its doors and waits.</p>", L(Ch("Go on", null, LeisurePhase)), new Opts { loc = "union", dark = true, fire = false }); return; }
                    Render("The Union Hall", "<p>On the 13th of June the Miners' Day parade turns into a riot on North Main: the old union against the new, and both against the Company men nobody can see. On the night of the 23rd the word goes round that they are going to blow the hall. A crowd gathers across the street.</p>", L(
                        Ch("Go and watch", null, () =>
                        {
                            bool hurt = Rand() < .3f; Apply(new Fx { morale = -10, rep = 8, heat = 10, health = hurt ? -18 : 0 }); Log("Watched the Union Hall dynamited.");
                            Render("Twenty-six charges", "<p>They blow it from the inside, charge after charge, the whole of North Main lit orange, bricks landing on Granite Street. A man next to you is laughing and crying at once. " + (hurt ? "A piece of cornice takes you across the shoulder." : "") + " In the morning the Butte Miners' Union does not exist.</p>", L(Ch("Go on", null, LeisurePhase)), new Opts { loc = "union", dark = true });
                        }),
                        Ch("Stay in your bunk", null, () => { Apply(new Fx { morale = -8 }); Log("Union Hall dynamited."); LeisurePhase(); })), new Opts { loc = "union", dark = true });
                } },
            { "1914-2", () =>
                {
                    F.Set("martial", true);
                    Render("Martial law", "<p>In September the Governor, who owes his nomination to Senator Vance and his campaign fund to Hobart, sends the National Guard. Soldiers on Main Street with fixed bayonets, a curfew at nine, and Kessler's detectives walking the Guard through Finntown and the East Side pointing out men with the wrong newspapers. The union is gone; there is no one to negotiate the wage, and the Company sets it. For the next three years standing with the men buys you friends, not protection.</p>",
                        L(Ch("Keep your head down", null, () => { Apply(new Fx { heat = S.o.id == "finnish" ? 15 : 5, morale = -6 }); WorkPhase(); })), new Opts { loc = "courthouse", dark = true });
                } },
            { "1917-1", () =>
                {
                    if (S.job != null && S.job.id == "speculator") { SpeculatorFire(); return; }
                    Render("The Speculator fire", "<p>Just before midnight on the 8th of June, a carbide lamp touches the frayed cable on the 2,400 level of the Granite Mountain shaft and the Speculator goes up like a chimney. Smoke pours into every connected mine on the hill. By morning the count is past a hundred and sixty dead; the Company says the shafts were not connected. Everyone on the hill knows they were.</p>", L(
                        Ch("Join the rescue crews", "Helmet men are going down with the Draeger apparatus.", () =>
                        {
                            bool hurt = Rand() < .4f; Apply(new Fx { rep = 20, morale = -12, health = hurt ? -20 : -4, lungs = -6 }); Log("Worked the Speculator rescue.");
                            Render("The 2,400", "<p>You carry men out of the drifts off the Speculator shaft for three days. On the third day they find Manus Duggan's bulkhead and the twenty-five he sealed in behind it, alive; and then they find Manus, who went out looking for a way through." + (hurt ? " A fall in the crosscut takes you off the rescue." : "") + "</p>", L(Ch("Go on", null, LeisurePhase)), new Opts { loc = "speculator", dark = true, fire = true });
                        }),
                        Ch("Stand at the shaft collar with the wives", null, () => { Apply(new Fx { morale = -15 }); Log("The Speculator fire."); LeisurePhase(); })), new Opts { loc = "speculator", dark = true, fire = true });
                } },
            { "1917-2", () =>
                {
                    Render("The strike", "<p>After the fire, fifteen thousand men walk off the hill under the Metal Mine Workers' Union. Hobart tells the papers the strike is a German plot. On the first of August six masked men take Frank Little of the IWW out of a boarding house on North Wyoming, drag him behind a car, and hang him from the Milwaukee Road trestle south of town with a card pinned to his leg that reads 3-7-77. Everyone on the hill knows the car. It belongs to Lute Sayles, who runs Kessler's gunmen. Nobody is arrested. The funeral is the largest Butte has ever seen.</p>", L(
                        Ch("Stay out with the strike", "No pay this season.", () => { S.strike = true; Apply(new Fx { rep = 18, heat = 15, morale = 4 }); Log("Stood with the 1917 strike."); WorkPhase(); }),
                        Ch("Go back under Sayles' gunmen", "Full pay. The men will remember.", () => { Apply(new Fx { rep = -25, morale = -10 }); Log("Worked through the 1917 strike."); Fight("a striker at the Mountain Con gate", WorkPhase); })), new Opts { loc = "trestle", dark = true });
                } },
        };

        public static void SpeculatorFire()
        {
            Render("June 8th, 1917", "<p>You are on the graveyard shift on the 2,400 level of the Granite Mountain when the lights go and the smoke comes down the drift like water. Men are running both ways. A shift boss named Manus Duggan is shouting for anyone who can hear him to follow him into a dead-end crosscut and help build a bulkhead.</p>", L(
                Ch("Follow Duggan", null, () =>
                {
                    bool live = Rand() < .55f + S.skill * .03f;
                    if (live)
                    {
                        Apply(new Fx { health = -25, lungs = -15, morale = -20, rep = 15 }); F.Set("speculatorBurned", true); Log("Survived the Speculator fire behind Duggan's bulkhead.");
                        Render("Thirty-six hours", "<p>Twenty-six of you behind a wall of timber and your own shirts, sealing it with mud and prayer, in the dark, for thirty-six hours. Duggan goes out to find a way through and does not come back. When the helmet men break in you walk out on your own legs and the whole hill is at the shaft collar.</p>", L(Ch("Go on", null, () => { S.job = null; EventPhase(); })), new Opts { loc = "speculator", dark = true, fire = true });
                    }
                    else Ending("fire");
                }),
                Ch("Run for the shaft", null, () =>
                {
                    if (Rand() < .3f)
                    {
                        Apply(new Fx { health = -35, lungs = -20, morale = -20 }); F.Set("speculatorBurned", true); Log("Survived the Speculator fire.");
                        Render("The station", "<p>You reach the station on the 2,400 and the cage is there and then it is gone, and you are hanging on the bell cord with ten other men until it comes back. Behind you nobody is running anymore.</p>", L(Ch("Go on", null, () => { S.job = null; EventPhase(); })), new Opts { loc = "speculator", dark = true, fire = true });
                    }
                    else Ending("fire");
                })), new Opts { loc = "speculator", dark = true, fire = true });
        }

        // ====================== THE DINNER-PAIL BREAK ======================
        public static void CrewBreak(ShiftState U, Action next)
        {
            var j = U.job; string pn = PersonName("partner"); bool cn = S.o.id == "chinese";
            var ctx = new Opts { loc = j.loc, dark = true, caption = Cap(MINE[j.loc]) + " — dinner pail, half a shift gone", keepScene = true };
            Func<Fx, string, Action> done = (fx, note) => () =>
            {
                if (fx != null) Apply(fx);
                if (!string.IsNullOrEmpty(note)) U.note = (!string.IsNullOrEmpty(U.note) ? U.note + " " : "") + note;
                next();
            };
            Action<string, string, List<Choice>> brk = (id, text, ch) => { Talk(id, text, ch, ctx); scene.caption = ctx.caption; };
            var V = new List<(string k, int w, Action run)>();
            void Add(string k, int w, Action run) => V.Add((k, w, run));
            Add("v1", 3, () => brk("partner", pn + " passes you half a pasty from his dinner pail. “Eat. You’ll want it for the second half.”", L(
                Ch("Share the pail", "Trust grows. A little health, and your back eases.", () => { Rel("partner", 1); U.fatigue = Math.Max(0, U.fatigue - .25f); done(new Fx { health = 3, morale = 3 }, "You and " + pn + " split the pail.")(); }),
                Ch("Eat alone and rest your back", "A longer breather.", () => { U.fatigue = Math.Max(0, U.fatigue - .4f); done(new Fx { morale = -1 }, null)(); }))));
            if (!cn) Add("v2", 3, () =>
            {
                int r = Rel("greenhorn", 0);
                brk("greenhorn", r >= 2 ? "“I held it right today, didn’t I?” He is trying not to look pleased. The boss is two crosscuts away." : "He is sitting on the muck with his hands open. “I can’t— my hands—” They are blistered through. The boss is two crosscuts away and he knows what happens to men who can’t keep up.", L(
                    Ch("Show him how to hold it", "Costs you time. Earns his trust and the crew’s.", () => { U.t = Math.Min(U.t + 70, U.dur - 60); Rel("greenhorn", 1); done(new Fx { rep = 4, morale = 3 }, "You showed " + ReplaceFirstJs(PersonName("greenhorn"), "young ", "the kid ") + " how to hold it.")(); }),
                    Ch("Tell him to keep up or go home", null, done(new Fx { rep = -3, morale = -2 }, null))));
            });
            if (!cn && S.year >= 1908) Add("v3", 2, () => brk("wobbly", "“Red” Tom Quill is passing a card down the drift between mouthfuls. “One big union, boys. They bury us by the hundred and call it production.” He holds a card out to you.", L(
                Ch("Hear him out", "The men notice. So do the Company’s ears.", done(new Fx { rep = 4, heat = 6 }, null)),
                Ch("Take a card", "Standing goes up. So does the Company’s interest.", done(new Fx { rep = 8, heat = 15 }, "You took a Wobbly card.")),
                Ch("Tell him the boss is coming", "He moves on. The men wonder whose side you’re on.", done(new Fx { heat = -3, rep = -2 }, null)))));
            Add("v4", 2, () => brk("boss", "“I see the car and I see the clock.” " + PersonName("boss") + " leans on a timber. “Your partner will clear a car and a half before the whistle. What’s the matter with you?”", L(
                Ch("“Yes, boss”", "Faster for the second half. You’ll tire sooner.", () => { U.boost = 1.1f; U.fatigue = Math.Min(1, U.fatigue + .15f); done(new Fx { rep = -1 }, null)(); }),
                Ch("“The ground’s bad here”", "He doesn’t like it. The men do.", () => { U.payBonus *= .95f; done(new Fx { rep = 4 }, "You told the boss the ground was bad.")(); }),
                Ch("Slip him ten dollars", "He finds somebody else to lean on.", () => { S.money -= 10; U.payBonus *= 1.05f; done(null, "You slipped the boss ten dollars.")(); }, cost: 10, disabled: S.money < 10))));
            Add("v5", 2, () =>
            {
                string other = Pick<string>(KINDS.Keys.Where(k => k != j.id).ToList());
                brk("partner", pn + " keeps his voice down. “A man from " + MineOf(other) + " says the ground there is working and the Company won’t shore it. If you go over, listen twice.”", L(
                    Ch("Mark it", "If you work there next season you’ll be ready for it.", () => { S.tip = other; S.tipTurn = TurnIdx(); done(new Fx { morale = 1 }, "Heard that the ground at " + MineOf(other) + " is working.")(); }),
                    Ch("Laugh it off", null, done(new Fx { morale = 2 }, null))));
            });
            if (!cn) Add("v6", 1, () => brk("partner", pn + " holds out a flask. “For the back. Don’t tell the boss.”", L(
                Ch("Take a pull", "Spirits up. Your aim for the second half is worse.", () => { U.sloppy = 1; done(new Fx { morale = 6 }, null)(); }),
                Ch("Pass", null, done(new Fx { rep = 1 }, null)))));
            Add("v7", 1, () =>
            {
                var songs = new Dictionary<string, string>
                {
                    { "irish", "Somebody starts “The Rising of the Moon” and half the drift knows the words." },
                    { "cornish", "Somebody starts a Methodist hymn. The Irish go quiet, and then, grudgingly, hum the bass." },
                    { "finnish", "Somebody starts a slow song from Ostrobothnia and the men beat time with their steels." },
                    { "italian", "Somebody begins an aria in a hoarse tenor and is booed, and then joined." },
                    { "serb", "Somebody starts the gusle-song about Kosovo with his spoon on a dinner pail." },
                    { "chinese", "Somebody hums a tune from Taishan." },
                };
                brk("partner", songs[S.o.id] + " " + pn + " nudges you.", L(
                    Ch("Join in", "The second half goes easier.", () => { U.fatigue = Math.Max(0, U.fatigue - .15f); done(new Fx { morale = 5, rep = 2 }, null)(); }),
                    Ch("Rest your back", null, () => { U.fatigue = Math.Max(0, U.fatigue - .3f); done(null, null)(); })));
            });
            if (!cn && S.year >= 1909 && S.heat > 25) Add("v8", 3, () => brk("partner", pn + " says nothing and tips his head toward a man from the next crew sitting apart, writing on a card against his knee.", L(
                Ch("Say nothing", null, done(new Fx { heat = 3 }, null)),
                Ch("Feed him a lie loud enough to carry", "Might send the Company the wrong way. Might not.", () => { if (Rand() < .6f) done(new Fx { heat = -5 }, "You fed the Company’s ears a lie.")(); else done(new Fx { heat = 8 }, null)(); }),
                Ch("Face him", "Fists in the drift.", () => Fight("a Company stool from the next crew", next, new Fx { rep = 5 }, new FightOpts { saloon = false })))));
            var recent = S.brkRecent;
            var pool = new List<(string k, int w, Action run)>();
            foreach (var v in V) { if (recent.Contains(v.k)) continue; for (int i = 0; i < v.w; i++) pool.Add(v); }
            var chosen = pool.Count > 0 ? Pick<(string k, int w, Action run)>(pool) : Pick<(string k, int w, Action run)>(V);
            var nr = new List<string>(recent) { chosen.k };
            S.brkRecent = nr.Skip(Math.Max(0, nr.Count - 4)).ToList();
            chosen.run();
        }

        // ====================== LEISURE ======================
        public static void FaroRun(Action after)
        {
            bool win = Rand() < .44f; Sfx("coin");
            if (win) { S.money += 90; Apply(new Fx { morale = 10 }); Log("Won at faro."); }
            else { Apply(new Fx { morale = -6 }); Log("Lost at faro."); }
            Render(win ? "The queen comes up" : "The queen loses", win ? "<p>Ninety dollars across the baize and the dealer doesn’t blink. You buy the table a round.</p>" : "<p>Forty dollars gone in the time it takes to roll a cigarette. The dealer has already forgotten your face.</p>",
                L(Ch("Go on", null, after)), new Opts { loc = "mm", dark = true });
        }

        public static void LeisurePhase()
        {
            S.phase = "leisure";
            if (S.health <= 0) { Ending("dead"); return; }
            if (S.pendingPlace != null)
            {
                string id = S.pendingPlace; S.pendingPlace = null; var sp = FindPlace(id);
                if (sp != null)
                {
                    // JS: walkInit() when the street is missing or from another season
                    if (scene.walk == null || scene.walk.seasonKey != Key()) scene.walk = new WalkState { seasonKey = Key() };
                    InsideMenu(sp); return;
                }
            }
            WalkPhase();
        }

        public static void EveningStep(Leisure l)
        {
            S.slots = S.slots - 1;
            if (l != null && l.ends) S.slots = 0;
            if (S.slots <= 0) { if (scene.walk != null) scene.walk.inside = false; EndSeason(l); return; }
            LeisurePhase();
        }

        public static void DoLeisure(Leisure l, int cost)
        {
            if (!F.B("talked")) { if (Meet(l, cost)) return; }
            bool hadTalk = F.B("talked");   // someone already spoke to you about this; no second scene
            F.Set("talked", false);
            S.money -= cost; var fx = CopyFx(l.fx);
            if (l.id == "sendhome") S.sent++;
            if (l.id == "gun") { F.Set("gun", true); Log("Bought a Colt."); }
            if (l.id == "union" && S.year >= 1912) fx.heat = 8;
            if (l.id == "union" && S.o.id == "irish") fx.rep = (fx.rep ?? 0) + 4;
            if (l.id == "district" && F.B("stool")) fx.morale -= 5;
            if (l.id == "study" && S.skill >= 7) fx.skill = null;
            Apply(fx); if (l.once) S.used.Add(l.id);
            Action after = () => EveningStep(l);
            if (l.run == "faro") { FaroRun(after); return; }
            if (l.talk != null && TALKS.ContainsKey(l.talk)) { TALKS[l.talk](l, after); return; }
            if (l.id == "district" && Rand() < .25f)
                Render("Birdie Lavelle", "<p>The woman who runs the Casino for the Deacon stops at your table with two of Sayles’ men behind her. The district has a new rule for strangers: fifteen dollars to drink here, or drink somewhere else.</p>", L(
                    Ch("Pay the fifteen", null, () => { S.money -= 15; after(); }, cost: 15),
                    Ch("Drink somewhere else", null, () => { Apply(new Fx { morale = -6 }); after(); }),
                    Ch("Ask the gunmen what they’re for", null, () => Fight("two of Sayles’ men", after))), new Opts { loc = "mercury", dark = true });
            else if (l.rolled > 0 && Rand() < l.rolled)
            {
                S.money = Math.Max(0, S.money - 40); Apply(new Fx { morale = -6 });
                Render("Rolled", "<p>Somewhere between the Casino and the Copper Block your money goes out of your pocket and into someone else’s. Forty dollars. The girl who was laughing at your jokes is gone too.</p>", L(Ch("Go home", null, after)), new Opts { loc = "mercury", dark = true });
            }
            else if (l.fight > 0 && Rand() < l.fight * (S.o.id == "cornish" ? 1.3f : 1))
                Fight(Pick("a Cornish timberman who says the Irish can’t drill", "an Irish mucker who says the Cornish can’t fight", "a company gunman off duty", "a Finn who has decided you are a Company stool", "a drunk Welshman from the Mountain Con"), after);
            else if (hadTalk || !LeisureScene(l, after)) after();   // Unity addition: a short scene instead of silence
        }

        /// <summary>JS meet(l, cost): Teddy, Nell or the landlady may intercept a leisure action. True if a scene was shown.</summary>
        public static bool Meet(Leisure l, int cost)
        {
            Action go = () => { F.Set("talked", true); DoLeisure(l, cost); };
            if (l.id == "teddy" || (l.id == "drink" && Rand() < .65f))
            {
                int r = Rel("teddy", 1); string t; var ch = L(Ch("Take the beer", null, go));
                if (F.B("mirror")) { t = "\"You know that mirror cost the house two hundred dollars. I'm not saying it was you. I'm saying the house knows.\""; F.Set("mirror", false); }
                else if (S.kills != 0) t = "\"They say you're handy with that Colt. Keep it in your coat in here. I pour for everybody and I bury nobody.\"";
                else if (r == 1) t = "\"New on the hill? " + Pick("Gulch", "Centerville", "Finntown", "Meaderville", "the East Side", "China Alley") + " by the look of the boots. First one's on the house. Second one isn't.\"";
                else if (r >= 6 && S.heat > 50 && !F.B("teddyTip")) { F.Set("teddyTip", true); t = "He leans in while he wipes the bar. \"The fellow at the end in the grey derby is Kessler's. He's been asking which boarding house you're at. Go out the kitchen tonight.\" You do."; ch = L(Ch("Out the kitchen", null, () => { Apply(new Fx { heat = -12, morale = 4 }); go(); })); }
                else if (r >= 5 && S.money < 30) t = "\"You're light. I can see it from here.\" He puts twenty on the bar and doesn't write it down. \"Pay me when the Company pays you.\"";
                else t = Pick("\"Same as last time?\" He already has it poured.", "\"Your partner was in asking after you. Said you'd know what for.\"", "\"Quiet tonight. The Finns are at the hall and the Irish are at the wake.\"", "He slides the free lunch down the bar without being asked. \"Eat. You look like the hill's winning.\"");
                if (r >= 5 && S.money < 30) ch = L(Ch("Take the twenty", null, () => { S.money += 20; go(); }));
                return Talk("teddy", t, ch, new Opts { loc = "mm", dark = true });
            }
            if ((l.id == "district" || l.id == "nell") && !F.B("nellGone"))
            {
                int r = Rel("nell", 0);
                if (r == 0)
                {
                    Rel("nell", 1);
                    return Talk("nell", "She is dancing for a dollar a turn at the Casino and laughing at a Cornishman who can't keep time. When he goes she comes and sits down without asking. \"You're not from the Deacon and you're not drunk yet. That makes you the most interesting man in the room. Nell Doherty, from Cork by way of Chicago.\" She looks at your hands. \"Machine man?\"", L(
                        Ch("Tell her your name", null, () => { Apply(new Fx { morale = 6 }); go(); }),
                        Ch("Buy her a drink and ask nothing", null, () => { Rel("nell", 1); Apply(new Fx { morale = 4 }); go(); })), new Opts { loc = "mercury", dark = true });
                }
                if (r < 3)
                {
                    Rel("nell", 1);
                    return Talk("nell", Pick("\"I thought you'd gone back to the old country. Or down the Speculator, which is the same thing.\" She takes your arm. \"Walk me to the end of the alley. The Deacon's man counts the girls at midnight and I like to be miscounted.\"", "\"You smell like rock. Everyone in here smells like rock or whiskey and I've decided I prefer rock.\" She has a book in her bag from the Free Library. She won't say which one.", "\"Birdie says I'm to be nice to the Company men and cold to the union men. I told her I can't tell them apart with their hats off.\" She looks at you. \"Which are you?\""), L(
                        Ch("Spend the evening with her", null, () => { Apply(new Fx { morale = 8 }); go(); }),
                        Ch("Just the dancing tonight", null, go)), new Opts { loc = "mercury", dark = true });
                }
                if (r >= 3 && !F.B("nellAsked"))
                {
                    F.Set("nellAsked", true);
                    return Talk("nell", "She doesn't sit down this time. \"The Deacon holds a paper on me. A hundred and fifty dollars, for the passage and the dresses and a doctor once. He adds to it. I've asked three men to buy it and all three said they would.\" She waits. \"I'm not asking you. I'm telling you what it costs.\"", L(
                        Ch("Buy the paper from the Deacon", null, () =>
                        {
                            S.money -= 150; F.Set("nellFree", true); Rel("nell", 3); Apply(new Fx { morale = 15, heat = 5 }); Log("Bought Nell's paper from the Deacon.");
                            Talk("nell", "The Deacon takes the money with a face like a bishop. Nell takes a room on the Flat the same week and a job at the Free Library shelving books, which the librarian says is a scandal and does anyway. She does not say thank you. She says, \"I'll see you Sunday,\" and she does.", L(Ch("Go on", null, go)), new Opts { loc = "mercury", dark = true });
                        }, cost: 150, disabled: S.money < 150),
                        Ch("You haven't got it", null, () => { Apply(new Fx { morale = -6 }); go(); })), new Opts { loc = "mercury", dark = true });
                }
                if (F.B("nellFree"))
                {
                    Rel("nell", 1);
                    return Talk("nell", S.kills != 0 ? "\"Teddy told me. I don't want to know his name.\" She holds your coat lapel. \"I came off Mercury Street to get away from men who settle things that way. Don't make me a liar.\""
                        : F.B("stool") ? "She has heard, somehow, what the Company pays you for. She doesn't say it. She says, \"Walk me home,\" and walks a step ahead the whole way."
                        : Pick("Sunday. She has learned the names of the flowers at Columbia Gardens and tells you all of them, wrong, and dares you to correct her.", "\"The librarian asked if we were engaged. I said we were negotiating.\" She looks at the hill. \"I want a house I can't see that from.\"", "\"Dinny came by the library to ask if I thought you'd live. I said you were too stubborn to die and too stupid to leave. He said that was the Gulch's opinion too.\""),
                        L(Ch("Walk her home", null, () => { Apply(new Fx { morale = 10 }); go(); })), new Opts { loc = "mercury", dark = true });
                }
            }
            if (((l.id == "board" || l.id == "sleep") && Rand() < .5f) || l.id == "landlady")
            {
                int r = Rel("landlady", 1); string t; var ch = L(Ch("Good night", null, go));
                if (F.B("brotherDead") && !F.B("llBrother")) { F.Set("llBrother", true); t = "She has put his boots by your door, cleaned. \"I lost mine in the Original too. You'll not want to hear that it gets easier. It does and it doesn't.\" She leaves the lamp lit in the hall."; }
                else if (S.health < 40 && r >= 4) { t = "She looks at you on the stair and says nothing, and in the morning there is broth and a mustard plaster and a note that says the board bill can wait. You sleep two days."; ch = L(Ch("Sleep", null, () => { Apply(new Fx { health = 10 }); go(); })); }
                else if (r == 1) t = "\"Rent is Saturdays. Boots off in the hall. No women past the parlor and no Cousin Jacks past the gate, if you're Irish, and the other way round if you're not. The furnace eats coal like a shift boss eats pie.\" She looks you over. \"You'll do.\"";
                else if (r >= 6) t = "\"You've been here longer than my husband was.\" She says it like an accusation and then pours you coffee from her own pot, which no boarder has ever had.";
                else t = Pick("\"There's a letter. It came Tuesday. I didn't read it, which is more than Mrs. Pentreath would do.\"", "\"The Finn in the back room coughs all night. Don't you start.\"", "\"If a man in a grey hat asks for you, I've never heard of you. I've said that to three of them this year.\"", "\"Supper's at six. The pie is for the men who pay Saturdays.\"");
                return Talk("landlady", t, ch, new Opts { loc = S.o.home, dark = true });
            }
            return false;
        }

        // ====================== THE STREET AND THE PLACES ======================
        public class LeisureOpt { public Leisure l; public int cost; }

        /// <summary>JS leisureFor(id): the action and its cost, or null when it is not on offer.</summary>
        public static LeisureOpt LeisureFor(string id)
        {
            var l = FindLeisure(id); if (l == null) return null;
            if (l.seasons != null && !l.seasons.Contains(S.season)) return null;
            if (l.until != null && (S.year > 1914 || (S.year == 1914 && S.season >= 1))) return null;
            if (S.o.id == "chinese" && (l.id == "union" || l.id == "secretary")) return null;
            if (l.id == "gun" && F.B("gun")) return null;
            if (l.id == "nell" && (F.B("nellGone") || RelGet("nell") == 0)) return null;
            int cost = l.cost; if (l.id == "sauna" && S.o.id == "finnish") cost = 0; if (l.id == "board" && S.o.id == "italian") cost = 18;
            return new LeisureOpt { l = l, cost = cost };
        }

        public static string SlotsLine()
        {
            int n = Math.Max(0, S.slots);
            return "Evening: " + new string('●', n) + new string('○', Math.Max(0, 3 - n)) + " — " + n + " of 3 hours left. Each thing you do takes one.";
        }

        public static string WalkIntro()
            => (S.year == 1906 && S.season == 0 ? "<em>Pick a place below to walk there, or use the arrow keys. Enter goes in.</em> " : "")
               + Pick("The whistle has gone and the night shift is walking up the hill past you. The lamps are lit on Main Street.", "Payday. Half the hill is on Park Street with money in its pocket and the other half is waiting for it.", "The wind comes down off the Divide and the streetcar bell rings at the bottom of the hill.", "Smoke, lamplight, a piano somewhere, and a thousand men with nowhere they have to be until morning.");

        public static void EnterPlace(StreetPlace sp)
        {
            var at = new List<int>();
            for (int i = 0; i < EVENTS.Count; i++)
            {
                var e = EVENTS[i];
                if (e.at == sp.id && (e.when == null || e.when()) && !(S.ev.ContainsKey(i) && TurnIdx() - S.ev[i] < (e.cool != 0 ? e.cool : 4))) at.Add(i);
            }
            if (at.Count > 0 && Rand() < .4f) { int i = Pick<int>(at); S.ev[i] = TurnIdx(); S.pendingPlace = sp.id; EVENTS[i].run(); return; }
            InsideMenu(sp);
        }

        public static void InsideMenu(StreetPlace sp)
        {
            S.phase = "leisure"; string nm = sp.Label;
            var ch = sp.acts.Select(LeisureFor).Where(f => f != null).Select(f =>
            {
                bool used = S.used.Contains(f.l.id);
                return Ch(f.l.name, used ? "Already done this season." : f.l.desc, () => DoLeisure(f.l, f.cost), cost: f.cost, disabled: used || (f.cost > 0 && S.money < f.cost));
            }).ToList();
            string body = "<p>" + PlaceLine(sp) + "</p><p class=\"note\">" + SlotsLine() + "</p>";
            bool K = INTERIOR.ContainsKey(sp.id); bool wasIn = scene.mode == "inside";
            if (K && !wasIn) Sfx("door");
            // JS: scene.walk.inside = K ? sp.id : null; a place with no interior (the Gardens stop) keeps the street in view
            if (scene.walk != null) scene.walk.inside = K;
            scene.inside = K ? sp.id : null;
            ch.Add(Ch(K ? "Step back outside" : "Walk on", null, () => { Sfx("door"); if (scene.walk != null) scene.walk.inside = false; WalkPhase(); }));
            Render(nm, body, ch, K ? new Opts { loc = LocFor(sp), dark = true } : new Opts { keepScene = true });
            if (!K) scene.mode = "walk";
            scene.caption = nm + " — " + sp.sub;
        }

        /// <summary>JS placeMenu(): the street as a plain menu (the reduced-motion fallback).</summary>
        public static void PlaceMenu()
        {
            S.phase = "leisure";
            var ch = STREET.Where(sp => sp.acts.Any(id => LeisureFor(id) != null)).Select(sp => Ch(sp.Label, sp.sub, () => InsideMenu(sp))).ToList();
            ch.Add(Ch("Turn in for the night", "Walk home and sleep. Ends the evening.", () => { var f = LeisureFor("sleep"); DoLeisure(f.l, 0); }));
            Render("Off shift", "<p>The season’s wages are in your pocket and the night is yours. Where to?</p><p class=\"note\">" + SlotsLine() + "</p>", ch, new Opts { loc = "mm", dark = true });
        }

        // ====================== THE NEWSPAPER ======================
        public static readonly Dictionary<int, int> COPPER = new Dictionary<int, int>
        { { 1906, 19 }, { 1907, 20 }, { 1908, 13 }, { 1909, 13 }, { 1910, 13 }, { 1911, 12 }, { 1912, 16 }, { 1913, 15 }, { 1914, 13 }, { 1915, 17 }, { 1916, 27 }, { 1917, 29 } };

        public static readonly Dictionary<string, string[]> NEWS = new Dictionary<string, string[]>
        {
            { "1906-0", new[] { "BRANDT SELLS TO THE CONSOLIDATED", "The Litigant leaves for New York in a private car; Hobart says the hill will now be \"worked as one mine, for one owner, by one rule.\"" } },
            { "1906-1", new[] { "HOBART PROMISES SIX-DAY WEEK", "Union committee told the eight-hour day \"is not under discussion\"; dues to rise." } },
            { "1907-2", new[] { "COPPER TOUCHES TWENTY-FIVE CENTS", "Record payroll on the hill; the M&M reports a shortage of ten-cent pieces." } },
            { "1907-3", new[] { "PANIC CLOSES THE HILL", "Every Consolidated shaft idle for the winter; soup line at the Hibernian hall runs down Main Street." } },
            { "1908-0", new[] { "HOISTS RUNNING AGAIN", "Half-time until summer; Hobart thanks the men for their patience, which was not asked." } },
            { "1909-1", new[] { "COLUMBIA GARDENS OPENS SEASON", "Senator Vance's gift to the city: new roller coaster, free streetcar for children on Thursdays." } },
            { "1910-0", new[] { "ONE COMPANY ON THE HILL", "The Consolidated absorbs the last Vance and Brandt properties; Kessler named chief of the Company's detective bureau." } },
            { "1910-1", new[] { "THE \"M\" ON BIG BUTTE", "School of Mines students whitewash a letter ninety feet high; visible from Meaderville." } },
            { "1911-1", new[] { "PEKIN NOODLE PARLOR OPENS", "Upstairs on South Main; chow mein and coffee for the graveyard shift." } },
            { "1912-3", new[] { "CARD SYSTEM FOR CONSOLIDATED MINES", "No man employed without a card from the Hennessy Building after January 1. Union votes not to strike." } },
            { "1913-1", new[] { "MINERS' DAY PARADE THE LARGEST YET", "Four bands and the Hibernians; a Cornish float withdrawn after \"a misunderstanding.\"" } },
            { "1914-1", new[] { "UNION HALL DYNAMITED", "Twenty-six charges on the night of the 23rd; crowd watched from Granite Street. No arrests." } },
            { "1914-2", new[] { "GOVERNOR SENDS THE GUARD", "Martial law in Silver Bow County; curfew at nine; raids on East Broadway and East Park." } },
            { "1915-0", new[] { "COPPER RECOVERS", "War orders from Europe lift the hill; Hobart hires a thousand men." } },
            { "1916-1", new[] { "TWENTY-SEVEN CENTS", "The richest year the hill has had; the Company declares an extra dividend. The wage stays at three-fifty." } },
            { "1917-1", new[] { "GRANITE MOUNTAIN DISASTER", "Fire in the Speculator shaft; death toll past one hundred and sixty. Manus Duggan missing after leading twenty-five men to a bulkhead." } },
            { "1917-2", new[] { "FRANK LITTLE HANGED FROM TRESTLE", "IWW organizer taken from his bed by masked men; card pinned to the body. Fifteen thousand march in the funeral. The Company blames \"German agents.\"" } },
            { "1917-3", new[] { "STRIKE COLLAPSES; WAR TAKES THE YOUNG MEN", "Draft boards sit at the courthouse; the hill returns to work under the Guard." } },
        };

        public static readonly string[] ADS =
        {
            "DR. HUIE POCK — Chinese herbs and medicines, 55 East Mercury. The miner's cough treated.",
            "GAMER'S CONFECTIONERY — Pasties 15¢, Coffee 5¢. Open for the night shift.",
            "HENNESSY'S — Blankets, boots, and powder. The Company store that is not the Company's.",
            "COLUMBIA GARDENS — Dancing every night in the Pavilion. Take the Vance streetcar.",
            "CENTENNIAL BREWERY — Butte Beer. Ask for it by name at the M&M.",
            "WAH CHONG TAI & CO. — Tea, silk, rice, credit. China Alley off Mercury.",
            "ST. JAMES HOSPITAL — The Sisters of Charity. Miners' tickets honored.",
            "FINNISH WORKERS' HALL — Sauna Saturdays. Socialist meeting Sundays. All welcome but Kessler.",
        };

        /// <summary>The newspaper HTML that EndSeason appends to its body.</summary>
        public static string FrontPage()
        {
            string k = Key();
            string[] h = NEWS.TryGetValue(k, out var nh) ? nh : new[]
            {
                new[] { "COPPER FIRM AT " + COPPER[S.year] + " CENTS", "The hill works three shifts; the Consolidated reports \"satisfactory\" tonnage from the Neversweat and the Leonard." },
                new[] { "FORTY BELOW ON THE HILL", "Two men found frozen on the Anaconda Road; coal dealers deny a combine." },
                new[] { "SENATOR VANCE RETURNS FROM WASHINGTON", "Received at the depot by a band and the Deacon; says the tariff \"will hold.\"" },
                new[] { "CAGE DROPS AT THE MOUNTAIN CON", "Seven men killed on the 2,800; Company says the brake was \"inspected Tuesday.\"" },
                new[] { "HOT WATER FLOODS LEONARD DRIFT", "Three scalded; Meaderville petitions for ice at the stations." },
                new[] { "MARY MACLANE SEEN AT THE LIBRARY", "The Butte authoress, back from New York, declines to say whether she is writing about us." },
            }[(S.year * 3 + S.season) % 6];
            string ds = DateStr();
            var mine = S.log.Where(x => x.StartsWith(ds, StringComparison.Ordinal)).Select(x => x.Substring(ds.Length + 3)).ToList();
            string name = S.name.ToUpperInvariant();
            var pers = new List<(Regex re, Func<Match, string[]> f)>
            {
                (new Regex(@"Hurt at (the .+?)\."), m => new[] { name + " INJURED AT " + ReplaceFirstJs(m.Groups[1].Value.ToUpperInvariant(), "THE ", "THE "), "A machine man of " + S.o.where + " was brought up from " + m.Groups[1].Value + " with injuries; the Company physician calls his recovery \"likely.\"" }),
                (new Regex("Won a fight with"), m => new[] { "DISTURBANCE AT THE M&M", "One man carried out; the other, a " + S.o.name + " of " + S.o.where + ", \"did not start it,\" according to the bartender, who always says so." }),
                (new Regex("Lost a fight with"), m => new[] { "ROW ON NORTH MAIN", "A " + S.o.name + " miner found on the sidewalk by the night watch; declined to give a name." }),
                (new Regex("Shot it out"), m => new[] { "SHOOTING ON MAIN STREET; ONE DEAD", "The coroner's jury finds death by a person or persons unknown. Vance's police \"are satisfied.\"" }),
                (new Regex("Bought a house"), m => new[] { "LOT SOLD ON THE FLAT", "A Consolidated miner buys a four-room house south of the creek; neighbors report he \"looks at the hill a good deal.\"" }),
                (new Regex("Survived the Speculator"), m => new[] { "ONE OF DUGGAN'S MEN", "Walked out on his own legs after thirty-six hours behind the bulkhead; asked for coffee and his partner, in that order." }),
                (new Regex("Bought Nell"), m => new[] { "CASINO DANCER LEAVES THE DISTRICT", "Miss Doherty takes a position at the Free Library; the Deacon \"wishes her well.\"" }),
                (new Regex("Jailed"), m => new[] { "NINETY DAYS FOR " + name, "Judge says the city \"has had enough of the Colt\" on Main Street." }),
                (new Regex("Refused Kessler|Refused the rustling|Stood with the 1917"), m => new[] { "AGITATOR NAMED BY COMPANY", "The Consolidated's detective bureau names a " + S.o.name + " of " + S.o.where + " as \"a disturbing influence.\" He is said to be unconcerned." }),
                (new Regex("Signed Kessler|Took Company money|Went to work for Hobart"), m => new[] { "CONSOLIDATED PROMOTES FROM THE RANKS", "The Company commends \"men who understand the hill's interests are the city's.\"" }),
                (new Regex(@"Buried (.+?)\."), m => new[] { "FUNERAL AT ST. PATRICK'S", "For " + m.Groups[1].Value + ", of " + S.o.where + ". The M&M stood the wake." }),
                (new Regex("Lost a season"), m => new[] { "UNDERTAKER WARNS OF THE DRINK", "Mr. Duggan of Duggan's Mortuary says he buried eleven men this quarter \"who the rock never touched.\"" }),
            };
            string[] p = null;
            foreach (var line in mine)
            {
                foreach (var (re, f) in pers) { var m = re.Match(line); if (m.Success) { p = f(m); break; } }
                if (p != null) break;
            }
            if (p == null) p = new[]
            {
                new[] { "NO NEWS FROM " + ReplaceFirstJs(S.o.where.ToUpperInvariant(), "THE ", ""), "Which, the priest at St. Patrick's observes, is the only kind worth having." },
                new[] { "PAYDAY QUIET", "The night watch reports \"nothing worth the ink.\" The M&M disputes this." },
            }[S.season % 2];
            return "<div class=\"paper\"><div class=\"mast\">THE BUTTE MINER</div><div class=\"dl\">Butte, Montana — " + DateStr() + " — Copper " + COPPER[S.year] + "¢ — Price 5 cents</div><div class=\"cols\"><h3>" + h[0] + "</h3><p>" + h[1] + "</p><h4>" + p[0] + "</h4><p>" + p[1] + "</p><div class=\"ad\">" + ADS[(S.year + S.season * 3) % ADS.Length] + "</div></div></div>";
        }

        // ====================== SEASON END ======================
        public static void EndSeason(Leisure l)
        {
            int board = F.B("house") ? 45 : F.B("cheapboard") ? 95 : (S.o.id == "irish" ? 110 : S.o.id == "chinese" ? 100 : S.o.id == "serb" ? 120 : 130);
            int gear = F.B("mined") ? 30 : 0; F.Set("mined", false); S.money -= board + gear; F.Set("cheapboard", false); S.strike = false;
            // choices made this season that bind the next one
            S.halfpay = F.B("halfNext"); F.Set("halfNext", false); F.Set("sworn", F.B("swornNext")); F.Set("swornNext", false);
            var lines = new List<string> { (F.B("house") ? "Coal, taxes and keep at the house on the Flat: $" + board + "." : "Board and keep at " + HomeName() + ": $" + board + ".") + (gear != 0 ? " Candles, powder and boots at the Company store: $" + gear + "." : "") };
            if (S.heat > 80 && S.year < 1912 && S.job != null && S.job.co && Rand() < .35f) { S.money -= 60; lines.Add("<em>Kessler's detectives have a word with the shift boss. You are let go three weeks before payday, and the Company keeps the three weeks.</em>"); Log("Let go on Kessler's word."); }
            if (S.heat > 85 && !S.blacklisted && S.year >= 1912 && S.o.id != "chinese") { S.blacklisted = true; S.card = false; lines.Add("<em>Your rustling card is pulled. The sixth floor has decided about you.</em>"); Log("Blacklisted by the Company."); }
            if (l != null) lines.Add(Pick("The season turns.", "Another ninety days on the hill.", "The whistle at the Anaconda marks the shifts and the shifts mark the years."));
            if (S.heat > 75) lines.Add("<em>A man from the sixth floor asks the shift boss about you by name.</em>");
            if (S.lungs < 30) lines.Add("<em>You can't climb the Anaconda Road without stopping twice.</em>");
            string html = "<p>" + string.Join(" ", lines) + "</p>" + FrontPage();
            if (S.money < -100) { Ending("broke"); return; }
            if (S.health <= 0) { Ending("dead"); return; }
            if (S.lungs <= 0) { Ending("con"); return; }
            if (S.morale <= 0)
            {
                F.SetI("binges", F.I("binges") + 1);
                if (F.I("binges") >= 3) { Ending("drink"); return; }
                S.morale = 18; S.money = Math.Max(-99, S.money - 80); Apply(new Fx { health = -12 }); Log("Lost a season to the drink.");
                html += "<div class=\"result bad\">You lose the season to the M&M. The bartender keeps a tab, the landlady keeps your room, and neither says anything when you come back. " + (F.I("binges") == 2 ? "The second time, the bartender says it: one more and he'll be pouring at your wake." : "Eighty dollars and a piece of your health.") + "</div>";
            }
            var ch = L(Ch("Next season", null, NextSeason));
            if (S.money >= 2000 && S.year >= 1910 && !F.B("house"))
                ch.Add(Ch("Buy a house on the Flat", "A frame house with a yard on the Flat, south of the creek, out from under the smoke. No more board; you keep working, because everyone does.", () =>
                {
                    S.money -= 2000; F.Set("house", true); Apply(new Fx { morale = 25, health = 5 }); Log("Bought a house on the Flat.");
                    Render("A house on the Flat", "<p>Four rooms, a porch, and a yard that faces away from the hill, south of Silver Bow Creek where the smoke thins. You sleep in your own bed for the first time since the old country. The hill is still there in the morning.</p>", L(Ch("Next season", null, NextSeason)), new Opts { loc = "trestle" });
                }, cost: 2000));
            if (S.money >= 200) ch.Add(Ch("Take the train out", "Back east, back home, or on to Seattle. You leave with what you have. The end.", () => Ending("leave"), cost: 40));
            S.phase = "end";
            Render("End of " + DateStr(), html, ch, new Opts { loc = S.o.home });
        }

        public static void NextSeason()
        {
            S.season++; if (S.season > 3) { S.season = 0; S.year++; }
            S.job = null;
            if (S.year > 1917) { S.year = 1917; S.season = 3; Ending("survived"); return; }
            WorkPhase();
        }

        // ====================== ENDINGS ======================
        public static void Ending(string kind)
        {
            S.ended = true; DeleteSave(); Shift = null;
            int n = TurnIdx() + 1, ny = (int)Math.Floor(n / 4.0); string t = null, b = null, loc = "stjames"; bool dark = true;
            string span = n < 4 ? new[] { "one season", "two seasons", "three seasons" }[n - 1] : ny == 1 ? "a year" : ny + " years";
            string Span() => char.ToUpperInvariant(span[0]) + span.Substring(1);
            string grade()
            {
                float sc = S.money + S.health * 3 + S.rep * 2 + S.sent * 40 + (F.B("house") ? 1500 : 0);
                return sc > 2400 ? "You did better than almost anyone who came up that hill." : sc > 1200 ? "You did about as well as a man could." : sc > 600 ? "You lived, which was the hard part." : "You lived. Barely.";
            }
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            switch (kind)
            {
                case "dead":
                    {
                        bool mine = F.B("killed"); t = mine ? "Killed on the hill" : "Buried in Butte";
                        b = "<p>" + (mine ? F.Str("killed") + "</p><p>" : "") + (F.B("nellFree") ? (mine ? "Nell Doherty stands at the shaft collar with the wives and does not cry until the cage comes up. " : "Nell Doherty sits at the back of St. Patrick's and leaves before the last hymn. ") : "") + (mine ? "They bring you up in the cage under a blanket. The Company pays the funeral" : "The Sisters at St. James do what they can, which is not enough. The county pays the funeral") + " and the landlady packs your things into the cardboard suitcase you came with. At St. Patrick's the priest has done this " + n * 13 + " times since you arrived.</p><p>You lasted " + span + " on the richest hill on earth.</p>";
                        loc = mine && S.job != null ? S.job.loc ?? "anaconda" : "stjames"; break;
                    }
                case "fire": t = "The Speculator, June 8th, 1917"; b = "<p>The smoke finds you in the drift off the 2,400 level with a hundred and sixty-seven other men. They bring you up on the fourth day. Your name goes on the monument on the hill, and in a letter the Company sends home with a check for the passage you never took.</p>"; loc = "speculator"; scene.fire = true; break;
                case "shot": t = "Shot on Main Street"; b = "<p>They carry you up to St. James and the Sisters do what they can. The coroner's jury, which is Vance's jury, finds that you died by a person or persons unknown. The M&M has the hole in the mirror for years and the bartender tells the story to anyone who asks.</p>"; loc = "mm"; break;
                case "con": t = "Miner's con"; b = "<p>The dust wins. You can't cross a room without stopping, and then you can't cross the bed. The Sisters at St. James keep you comfortable. " + Span() + " of rock in your chest and $" + Math.Max(0, S.money) + " in a tobacco tin under the mattress.</p>"; break;
                case "broke": t = "Out on the Northern Pacific"; b = "<p>The landlady sells your trunk for the board you owe. You ride a boxcar out of the yards on the Flat with no money and a Company file that follows you to every mine in the West.</p>"; loc = "trestle"; break;
                case "drink": t = "The M&M never closes"; b = "<p>Nobody notices when you stop going to the hill. The free lunch is enough; the bartender knows your name; the cold doesn't reach the back of the room. One morning in " + S.year + " the swamper finds you under the faro table and the city buries you on the county.</p>"; loc = "mm"; break;
                case "leave": t = "The train out"; b = "<p>You take the Northern Pacific east with $" + Math.Max(0, S.money - 40) + " sewn into your coat and " + (S.sent != 0 ? "a family who have been waiting for the letter." : "no one waiting at the other end.") + " You never go underground again, and you never quite stop listening for the whistle. " + grade() + "</p>"; loc = "trestle"; dark = false; break;
                case "survived":
                    t = "Winter 1917";
                    b = "<p>The strike collapses in December. The war takes the young men and the influenza takes the rest the year after. You are still here" + (F.B("house") ? ", in your own house on the Flat" : "") + (F.B("nellFree") ? (F.B("house") ? " with Nell Doherty, who has a key and uses it" : ", and Nell Doherty walks you home from the library on Sundays and says nothing about the hill") : "") + (F.B("companyman") ? ", a Company man the hill crosses the street to avoid" : "") + ": " + S.health.ToString(inv) + " health, " + Round(S.lungs) + " percent of your lungs, $" + S.money + " in the bank at Clark's, " + S.fights + " fights, " + S.sent + " money orders home, and a standing of " + Round(S.rep) + " with the men who are left. " + grade() + "</p>";
                    loc = S.o.home; dark = false; break;
            }
            Log(t + ".");
            Render(t, b + "<p class=\"note\">" + Span() + " in the camp. Started as " + S.o.name + ", from " + S.o.where + ".</p>",
                L(Ch("Start again", null, () => { S = null; scene.fire = false; Title(); })), new Opts { loc = loc, dark = dark });
        }
    }
}
