// Copper Camp — random events, fights and gunfights, rumors and conversations
// (JS EVENTS, myStr, oppStats, fight, brawlRound, gunPull, gunfight, gunRound, RUMORS, rumorTalk, TALKS).
using System;
using System.Collections.Generic;
using System.Linq;

namespace CopperCamp
{
    /// <summary>One entry of the JS EVENTS table. Its index in EVENTS is the JS index (S.ev keys depend on it).</summary>
    public class GameEvent
    {
        public string at;           // STREET id: the event fires on entering that place (null = evening event)
        public int cool = 4;        // seasons before it can fire again
        public Func<bool> when;
        public Action run;
        public string title;        // for debugging only
    }

    /// <summary>The JS fight() options object: {saloon}, and {from, first} when a brawl turns into a gunfight.</summary>
    public class FightOpts
    {
        public bool? saloon;        // null = decide from scene.focus (mm or mercury)
        public FightState from;     // the brawl this gunfight grew out of (JS o.from; carried, not read)
        public bool first;          // the player drew first (gunPull "Draw first")
    }

    /// <summary>The JS brawl object F.</summary>
    public class FightState
    {
        public string opp;
        public int hp;
        public float str;
        public bool armed, group;
        public int round, taken;
        public bool setup;
        public Action next;
        public Fx prize;
        public string loc;
        public bool saloon;
    }

    /// <summary>The JS gunfight object G.</summary>
    public class GunState
    {
        public string opp;
        public Action next;
        public string loc;
        public float acc;
        public int round;
        public bool cover, company;
    }

    /// <summary>The JS oppStats() result.</summary>
    public class OppStat
    {
        public bool armed;
        public int hp;
        public float str;
        public bool group;
    }

    public static partial class CC
    {
        // ---- small local helpers ----
        /// <summary>JS s[0].toUpperCase()+s.slice(1).</summary>
        static string EvUcFirst(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s.Substring(1);
        /// <summary>JS /a|b|c/.test(s) for plain alternatives (case-sensitive substring test).</summary>
        static bool EvAny(string s, params string[] subs) => s != null && subs.Any(x => s.Contains(x, StringComparison.Ordinal));

        // ====================== EVENTS ======================
        public static readonly List<GameEvent> EVENTS = new List<GameEvent>
        {
            // 0
            new GameEvent { title = "The landlady", at = "home", cool = 12, when = () => S.o.id != "chinese", run = () =>
            {
                string ll = PersonName("landlady"), name = EvUcFirst(ll);
                Render("The landlady", "<p>" + name + " stops you on the stair at " + HomeName() + ". Her husband died in the Original two winters back and she has three children and a half-full house. The furnace needs a man, the roof needs a man, and she is not shy about asking.</p>", L(
                    Ch("Spend your evenings fixing the place", "Cheaper board next season.", () => { Apply(new Fx { morale = 6, health = -3 }); F.Set("cheapboard", true); Log("Helped the landlady; board reduced."); LeisurePhase(); }),
                    Ch("Pay your board and keep to yourself", null, () => { Apply(new Fx { morale = -2 }); LeisurePhase(); })), new Opts { loc = S.o.home });
            } },
            // 1
            new GameEvent { title = "A quiet word", at = "mm", cool = 8, when = () => S.year >= 1912 && !S.blacklisted && S.o.id != "chinese", run = () =>
            {
                Render("A quiet word", "<p>A man in a clean collar buys you a beer at the M&M and talks about the weather for twenty minutes before he gets to it: the Company pays twenty dollars a month to men who keep their ears open at the Union Hall and the Finn hall. Nobody has to know.</p>", L(
                    Ch("Take the money", "Standing with the men falls if it ever gets out.", () => { S.money += 20; Apply(new Fx { heat = -20, rep = -15, morale = -8 }); F.Set("stool", true); Log("Took Company money."); LeisurePhase(); }, cost: -20),
                    Ch("Finish the beer and walk out", null, () => { Apply(new Fx { heat = 8, rep = 5 }); LeisurePhase(); })), new Opts { loc = "mm", dark = true });
            } },
            // 2
            new GameEvent { title = "Typhoid on the creek", cool = 11, when = () => S.season == 1, run = () =>
            {
                Render("Typhoid on the creek", "<p>Silver Bow Creek runs yellow and the boarding houses draw from it anyway. Half the East Side is down with typhoid. You feel the fever coming on at the end of shift.</p>", L(
                    Ch("See the Sisters at St. James", null, () => { S.money -= 40; Apply(new Fx { health = -8 }); Log("Typhoid; treated at St. James."); LeisurePhase(); }, cost: 40),
                    Ch("Sweat it out in your bunk", null, () => { Apply(new Fx { health = -22, morale = -6 }); Log("Typhoid; sweated it out."); LeisurePhase(); })), new Opts { loc = "stjames" });
            } },
            // 3
            new GameEvent { title = "Forty below", cool = 6, when = () => S.season == 3, run = () =>
            {
                Render("Forty below", "<p>The cold comes down off the Divide and holds for three weeks. A man from the night shift is found frozen on the Anaconda Road, two blocks from his door. Coal is three dollars a ton and the dealers know it.</p>", L(
                    Ch("Buy coal for the stove", null, () => { S.money -= 18; Apply(new Fx { morale = 3 }); LeisurePhase(); }, cost: 18),
                    Ch("Burn mine timbers and wear your coat to bed", null, () => { Apply(new Fx { health = -10, heat = 3 }); LeisurePhase(); })), new Opts { loc = S.o.home });
            } },
            // 4
            new GameEvent { title = "Faro at the M&M", at = "mm", cool = 9, when = () => S.money > 60, run = () =>
            {
                Render("Faro at the M&M", "<p>The faro bank at the back of the M&M is open, as it is every hour of every day. A Cornishman you know from the hill has just lost a month in ten minutes and is laughing about it.</p>", L(
                    Ch("Put down $40 on the queen", null, () =>
                    {
                        Sfx("coin");
                        if (Rand() < .42f) { S.money += 60; Apply(new Fx { morale = 12 }); Log("Won at faro."); Render("The queen comes up", "<p>Sixty dollars across the table and the dealer doesn't blink. You buy the Cornishman a drink.</p>", L(Ch("Pocket it", null, LeisurePhase)), new Opts { loc = "mm" }); }
                        else { S.money -= 40; Apply(new Fx { morale = -8 }); Log("Lost at faro."); Render("The queen loses", "<p>Forty dollars gone in the time it takes to roll a cigarette. The dealer has already forgotten your face.</p>", L(Ch("Walk out", null, LeisurePhase)), new Opts { loc = "mm" }); }
                    }, cost: 40),
                    Ch("Keep walking", null, LeisurePhase)), new Opts { loc = "mm" });
            } },
            // 5
            new GameEvent { title = "A letter from home", at = "hennessy", cool = 8, when = () => S.year >= 1908 && !F.B("brother") && !F.B("noBrother"), run = () =>
            {
                Render("A letter from home", "<p>Thin paper, three months old. Your mother is poorly. Your brother wants to come over and work the hill with you; he will need forty dollars for the passage and a word with the hiring boss.</p>", L(
                    Ch("Send the forty dollars", null, () => { S.money -= 40; S.sent++; Apply(new Fx { morale = 14 }); F.Set("brother", true); Log("Sent passage money for your brother."); LeisurePhase(); }, cost: 40),
                    Ch("Write that the hill is no place for him", null, () => { Apply(new Fx { morale = -8 }); F.Set("noBrother", true); LeisurePhase(); })), new Opts { loc = "hennessy" });
            } },
            // 6
            new GameEvent { title = "The Original", cool = 99, when = () => F.B("brother") && !F.B("brotherDead") && S.job != null && S.job.risk > 0, run = () =>
            {
                F.Set("brotherDead", true);
                Render("The Original", "<p>Your brother has been on the hill four months. On a Tuesday in the Original a cage drops and seven men go with it. The Company sends a wagon and a check for the funeral. The priest at St. Patrick's says the words. The M&M stands a wake.</p>", L(
                    Ch("Bury him and go back down", null, () => { Apply(new Fx { morale = -25, rep = 10 }); Log("Buried your brother."); LeisurePhase(); }),
                    Ch("Bury him and swear off the rock", "You take the Hennessy clerk job next season whether you like it or not.", () => { Apply(new Fx { morale = -15 }); F.Set("swornNext", true); Log("Buried your brother."); LeisurePhase(); })), new Opts { loc = "original", dark = true });
            } },
            // 7
            new GameEvent { title = "Midsummer / St. Patrick's Day", cool = 13, when = () => S.o.id == "irish" || S.o.id == "cornish", run = () =>
            {
                bool irish = S.o.id == "irish";
                Render(irish ? "Midsummer in Centerville" : "St. Patrick's Day", "<p>" + (irish ? "The Cousin Jacks are lighting their Midsummer bonfire above Centerville and singing hymns at it. Some Gulch boys have gone up to put it out." : "The Hibernians are marching down Main Street with pipes and a banner, and every Cornishman on the hill has been told to stay indoors. You didn't.") + " It is a question of whether you go.</p>", L(
                    Ch("Go", null, () => Fight(irish ? "a Cornish timberman with a shovel" : "three Irish muckers from the Gulch", LeisurePhase, new Fx { rep = 6 })),
                    Ch("Stay home", null, () => { Apply(new Fx { morale = -3 }); LeisurePhase(); })), new Opts { loc = "centerville", dark = true });
            } },
            // 8
            new GameEvent { title = "The Wobblies", cool = 5, when = () => S.o.id == "finnish" || S.rep > 50, run = () =>
            {
                Render("The Wobblies", "<p>A speaker from the Industrial Workers of the World is up on a box outside the Finn hall talking about one big union and the day the hill belongs to the men who dig it. A Company detective is writing names in a notebook across the street.</p>", L(
                    Ch("Stand and listen", null, () => { Apply(new Fx { rep = 10, morale = 6, heat = 12 }); Log("Seen at the IWW speech."); LeisurePhase(); }),
                    Ch("Cross the street", null, () => { Apply(new Fx { rep = -4, heat = -2 }); LeisurePhase(); })), new Opts { loc = "finntown" });
            } },
            // 9
            new GameEvent { title = "The Meaderville harvest", cool = 9, when = () => S.o.id == "italian" || S.season == 2, run = () =>
            {
                Render("The Meaderville harvest", "<p>The gardens in Meaderville come in all at once: tomatoes, peppers, grapes trucked up from California for the cellars. There is a dinner at the Rocky Mountain that goes on until the night shift leaves for the Leonard.</p>", L(
                    Ch("Eat, drink and sing", null, () => { S.money -= 8; Apply(new Fx { morale = S.o.id == "italian" ? 16 : 10, health = 5 }); LeisurePhase(); }, cost: 8),
                    Ch("You have to work in the morning", null, LeisurePhase)), new Opts { loc = "meaderville" });
            } },
            // 10
            new GameEvent { title = "Dr. Huie Pock", cool = 99, when = () => S.o.id == "chinese", run = () =>
            {
                Render("Dr. Huie Pock", "<p>The herbalist on Mercury Street has white patients now, miners with lungs the hospital has given up on. The Butte doctors have gone to the county attorney to shut him down for practicing without a license. He asks whether you will say in court that his medicine works.</p>", L(
                    Ch("Testify for him", null, () => { Apply(new Fx { rep = 12, heat = 10, morale = 8 }); Log("Testified for Dr. Huie Pock."); LeisurePhase(); }),
                    Ch("Keep your head down", null, () => { Apply(new Fx { morale = -6 }); LeisurePhase(); })), new Opts { loc = "chinatown" });
            } },
            // 11
            new GameEvent { title = "Union men at the door", cool = 9, when = () => S.o.id == "chinese", run = () =>
            {
                Render("Union men at the door", "<p>Three men with union buttons come into the shop and tell your employer that no white man will eat here or send his washing here until the Chinese are off the hill. One of them knocks the lamp off the counter on the way out.</p>", L(
                    Ch("Follow them out and say your piece", null, () => Fight("a union picket with a club", LeisurePhase, new Fx { rep = 4 })),
                    Ch("Sweep up the glass", null, () => { Apply(new Fx { morale = -8 }); F.Set("boycott", true); LeisurePhase(); })), new Opts { loc = "chinatown", dark = true });
            } },
            // 12
            new GameEvent { title = "Badnjak", at = "home", cool = 6, when = () => S.o.id == "serb", run = () =>
            {
                Render("Badnjak", "<p>Orthodox Christmas on the East Side. The boarding house burns the oak log in the yard and every Serb and Croat on the hill comes by with slivovitz. A Montenegrin from the Leonard says he can get you on his crew, where they speak your language and the pay is honest.</p>", L(
                    Ch("Join his crew", "Your English counts for less, your arms count for more.", () => { Apply(new Fx { morale = 12, rep = 6 }); F.Set("crew", true); LeisurePhase(); }),
                    Ch("Drink and go home", null, () => { Apply(new Fx { morale = 8 }); LeisurePhase(); })), new Opts { loc = "eastside" });
            } },
            // 13
            new GameEvent { title = "Mary MacLane", at = "library", cool = 99, when = () => S.year >= 1909 && S.year <= 1911, run = () =>
            {
                Render("Mary MacLane", "<p>At the Free Public Library a young woman in a man's overcoat is reading at the long table and writing in a notebook, fast and angry. The librarian says she is the Butte girl who wrote that book about herself and scandalized the whole country, and that she is back from New York and bored of us already.</p>", L(
                    Ch("Ask what she's writing", null, () => { Apply(new Fx { morale = 9 }); Log("Met Mary MacLane at the library."); LeisurePhase(); }),
                    Ch("Leave her be", null, LeisurePhase)), new Opts { loc = "library" });
            } },

            /* ---- the Copper Kings' men ---- */
            // 14
            new GameEvent { title = "Big Jim Mulcahy", cool = 99, when = () => S.o.id != "chinese" && !F.B("mulcahyDone") && S.job != null && S.job.co, run = () =>
            {
                Render("Big Jim Mulcahy", "<p>The shift boss at " + MINE[S.job.loc] + " is a wide man named Mulcahy who hires for the Consolidated and sells the jobs he hires for. He finds you in the dry room: the good contracts go to the men who remember him on payday. Twenty-five dollars a season. Everybody pays.</p>", L(
                    Ch("Pay him", null, () => { S.money -= 25; Apply(new Fx { morale = -5 }); F.Set("mulcahyDone", true); LeisurePhase(); }, cost: 25),
                    Ch("Tell him to go to hell", "He controls the contracts. Company pay drops until this is settled.", () => { Apply(new Fx { rep = 8, morale = 4 }); F.Set("mulcahy", true); F.Set("mulcahyDone", true); Log("Refused Mulcahy's cut."); LeisurePhase(); }),
                    Ch("Settle it in the dry room", null, () => Fight("Big Jim Mulcahy and his brother-in-law", () => { F.Set("mulcahyDone", true); if (S.health > 0 && !F.B("mulcahy")) Log("Mulcahy backed off."); LeisurePhase(); }, new Fx { rep = 6 }))), new Opts { loc = S.job.loc, dark = true });
            } },
            // 15
            new GameEvent { title = "Mulcahy's price", cool = 99, when = () => F.B("mulcahy") && S.rep > 40, run = () =>
            {
                F.Set("mulcahy", false);
                Render("Mulcahy's price", "<p>Word gets round the hill that Mulcahy has been shorting the Gulch men and the Finns alike. A delegation from the Union Hall goes up to the dry room. In the morning the contracts are posted on the board where everyone can read them, and Mulcahy has a job at the Leonard, underground.</p>", L(
                    Ch("Go on", null, () => { Apply(new Fx { rep = 6, morale = 8 }); Log("Mulcahy broken."); LeisurePhase(); })), new Opts { loc = "union" });
            } },
            // 16
            new GameEvent { title = "Enoch Pruett's desk", at = "hennessy", cool = 10, when = () => S.year >= 1912 && S.blacklisted && !F.B("pruett"), run = () =>
            {
                Render("Enoch Pruett's desk", "<p>The clerk who keeps the rustling-card file on the sixth floor drinks at the Silver Bow Club and loses at cards there. A man you know says that for forty dollars Pruett will misfile a name. Kessler's detectives read the file; they don't read what isn't in it.</p>", L(
                    Ch("Pay Pruett", null, () =>
                    {
                        S.money -= 40; F.Set("pruett", true);
                        if (Rand() < .65f) { S.blacklisted = false; S.card = true; Apply(new Fx { heat = -10 }); Log("Bought a rustling card off Pruett."); Render("Misfiled", "<p>A week later a card comes in a plain envelope with your name spelled wrong on it. You don't correct it.</p>", L(Ch("Go on", null, LeisurePhase)), new Opts { loc = "hennessy" }); }
                        else { Apply(new Fx { morale = -8, heat = 5 }); Render("Pruett keeps the money", "<p>Pruett keeps the forty dollars and the file, and sends his regards. There is no one to complain to.</p>", L(Ch("Go on", null, LeisurePhase)), new Opts { loc = "hennessy", dark = true }); }
                    }, cost: 40),
                    Ch("Keep your name clean and your money", null, LeisurePhase)), new Opts { loc = "hennessy" });
            } },
            // 17
            new GameEvent { title = "The Deacon", at = "mercury", cool = 6, when = () => S.year <= 1912 && S.season == 2 && S.o.id != "chinese", run = () =>
            {
                Render("The Deacon", "<p>Election week. Senator Vance's man in the district, a soft-spoken fixer called the Deacon, is paying five dollars a vote at the back of the Casino on Mercury Street and ten for a man who'll vote in Centerville and Meaderville both. The Senator needs the legislature again and the legislature, this year, is for sale by the head.</p>", L(
                    Ch("Vote once for five dollars", null, () => { S.money += 5; Apply(new Fx { morale = -2 }); F.SetI("vance", F.I("vance") + 1); LeisurePhase(); }, cost: -5),
                    Ch("Vote in three precincts for ten", null, () =>
                    {
                        if (Rand() < .25f) { Apply(new Fx { health = -8, morale = -10 }); Log("Jailed for repeat voting."); Render("Caught", "<p>A deputy who works for Hobart, not Vance, lifts you at the Meaderville poll and you spend election night in the Silver Bow County jail. The Deacon doesn't know you.</p>", L(Ch("Go on", null, LeisurePhase)), new Opts { loc = "courthouse", dark = true }); }
                        else { S.money += 10; Apply(new Fx { morale = -4 }); F.SetI("vance", F.I("vance") + 2); LeisurePhase(); }
                    }, cost: -10),
                    Ch("Keep your vote", null, () => { Apply(new Fx { rep = 3 }); LeisurePhase(); })), new Opts { loc = "mercury", dark = true });
            } },
            // 18
            new GameEvent { title = "Dutch Kessler", cool = 99, when = () => S.year >= 1909 && S.rep > 35 && !F.B("kessler") && S.o.id != "chinese", run = () =>
            {
                F.Set("kessler", true);
                Render("Dutch Kessler", "<p>Two men in grey derbies are waiting in the landlady's parlor. The older one is Kessler, Hobart's chief of detectives, and he has a typed statement naming four men in your boarding house as members of the Industrial Workers of the World. He would like you to sign it. He mentions your lungs, your board bill and your standing with the Company, in that order." + (F.B("mulcahy") || F.B("mulcahyDone") && S.rep > 40 ? " He also mentions Mulcahy, and smiles, so you know who filed that." : "") + (F.I("vance") > 2 ? " \"The Senator's people speak well of your voting.\" " : "") + "</p>", L(
                    Ch("Sign it", "Four men lose their cards. You keep yours.", () => { Apply(new Fx { heat = -30, rep = -25, morale = -15 }); S.card = true; S.blacklisted = false; F.Set("stool", true); Log("Signed Kessler's statement."); LeisurePhase(); }),
                    Ch("Give him four names of men who left town", null, () =>
                    {
                        if (Rand() < .4f) { Apply(new Fx { heat = 25, morale = -5 }); Log("Kessler caught the lie."); Render("Kessler reads", "<p>Kessler reads the four names, folds the paper, and says that two of those men are dead and one is in Arizona, and that he will remember the courtesy.</p>", L(Ch("Go on", null, LeisurePhase)), new Opts { loc = S.o.home, dark = true }); }
                        else { Apply(new Fx { heat = -8, rep = 4 }); Log("Fed Kessler dead names."); LeisurePhase(); }
                    }),
                    Ch("Show him the door", null, () => { Apply(new Fx { heat = 18, rep = 12, morale = 6 }); Log("Refused Kessler."); LeisurePhase(); })), new Opts { loc = S.o.home, dark = true });
            } },
            // 19
            new GameEvent { title = "Brandt's night shift", cool = 99, when = () => S.year <= 1907 && S.o.id != "chinese" && !F.B("brandt"), run = () =>
            {
                F.Set("brandt", true);
                Render("Brandt's night shift", "<p>Brandt has sold the Litigant's mines to Hobart, but not quite yet: the papers sign in New York next month, and until then his foreman is paying sixty dollars cash for a night crew to drill through the boundary under the Original and strip the Consolidated's vein before the ink dries. The lawyers call it the apex law. The Company calls it theft and has men with rifles at the drift.</p>", L(
                    Ch("Take the sixty", null, () =>
                    {
                        S.money += 60; Apply(new Fx { lungs = -2, heat = 15 });
                        if (Rand() < .35f)
                        {
                            Render("Rifles in the drift", "<p>The Consolidated guards are waiting on the other side of the boundary with lamps out. There is shooting in a four-foot drift.</p>", L(
                                F.B("gun") ? Ch("Shoot back", null, () => Gunfight("a Consolidated guard with a rifle", LeisurePhase)) : null,
                                Ch("Get out", null, () => { Apply(new Fx { health = -25, morale = -10 }); Log("Shot at in Brandt's raid."); LeisurePhase(); })), new Opts { loc = "original", dark = true });
                        }
                        else { Log("Worked Brandt's raid for sixty dollars."); LeisurePhase(); }
                    }, cost: -60),
                    Ch("Stay out of the Kings' war", null, () => { Apply(new Fx { rep = 2 }); LeisurePhase(); })), new Opts { loc = "original", dark = true });
            } },
            // 20
            new GameEvent { title = "The Sixth Floor", cool = 99, when = () => S.year >= 1911 && S.rep > 60 && !F.B("companyman") && !F.B("hobart") && S.o.id != "chinese", run = () =>
            {
                F.Set("hobart", true);
                Render("The Sixth Floor", "<p>A boy brings a card to the dry room: Mr. Hobart will see you. The elevator in the Hennessy Building goes to the sixth floor and a room with a view of every headframe on the hill. Hobart is a small, courteous man with an accountant's hands. The men listen to you, he says. The Company would like them to go on listening to you, as a shift boss at a shift boss's pay, and would like to know what they say.</p>", L(
                    Ch("Take the shift boss's job", "Company pay up a quarter. The men will know what you are.", () => { F.Set("companyman", true); Apply(new Fx { rep = -35, heat = -40, morale = -10 }); S.card = true; S.blacklisted = false; Log("Went to work for Hobart."); LeisurePhase(); }),
                    Ch("Decline, politely", null, () => { Apply(new Fx { heat = 20, rep = 10, morale = 8 }); Log("Turned Hobart down."); LeisurePhase(); })), new Opts { loc = "hennessy" });
            } },
            // 21
            new GameEvent { title = "What a company man is for", cool = 3, when = () => F.B("companyman") && S.season != 3, run = () =>
            {
                Render("What a company man is for", "<p>Kessler wants names from the Union Hall by Friday. The men on your shift have stopped talking when you come into the dry room. Your landlady has started leaving your plate outside the door.</p>", L(
                    Ch("Give Kessler names", null, () => { Apply(new Fx { morale = -12, rep = -8 }); S.money += 20; LeisurePhase(); }),
                    Ch("Give him nothing and keep the pay", null, () =>
                    {
                        if (Rand() < .5f) { F.Set("companyman", false); Apply(new Fx { heat = 30, morale = 5 }); Log("Hobart dropped you."); Render("Dropped", "<p>The card from the Sixth Floor this time says only that your services are no longer required. Pruett has your file open on his desk when you leave.</p>", L(Ch("Go on", null, LeisurePhase)), new Opts { loc = "hennessy", dark = true }); }
                        else LeisurePhase();
                    })), new Opts { loc = "hennessy", dark = true });
            } },
            // 22
            new GameEvent { title = "3-7-77", cool = 99, when = () => S.year >= 1914 && S.rep > 70 && !F.B("sayles") && S.o.id != "chinese", run = () =>
            {
                F.Set("sayles", true);
                Render("3-7-77", "<p>A card is nailed to your door in the night: three numbers, seven, seven, the old Montana vigilantes' sign. The men who use it now drive Lute Sayles' car and draw Hobart's pay. Your landlady saw the car.</p>", L(
                    F.B("gun") ? Ch("Wait up for the car with the Colt", null, () => Gunfight("three of Sayles' men", LeisurePhase)) : null,
                    Ch("Stay, and sleep with the lamp lit", null, () =>
                    {
                        if (Rand() < .45f) { Apply(new Fx { health = -30, morale = -15 }); Log("Beaten by Sayles' men."); Render("The car", "<p>They take you off the Anaconda Road at two in the morning, three of them, and leave you by the Milwaukee tracks for the day shift to find. You know all three faces. You will never say so in a courtroom and neither will anyone else.</p>", L(Ch("Go on", null, LeisurePhase)), new Opts { loc = "trestle", dark = true }); }
                        else { Apply(new Fx { rep = 10, morale = 5 }); Log("Stared down Sayles' card."); LeisurePhase(); }
                    }),
                    Ch("Take the morning train", "You leave with what you have. The end.", () => Ending("leave"))), new Opts { loc = S.o.home, dark = true });
            } },
            // 23
            new GameEvent { title = "The Deacon collects", cool = 9, when = () => S.o.id == "chinese" && S.year >= 1908, run = () =>
            {
                Render("The Deacon collects", "<p>The Deacon, who buys votes for Senator Vance in the district, also collects for him. The protection on China Alley is ten dollars a month, paid to a woman called Birdie Lavelle at the Casino, and the police who walk Mercury Street are Vance's police. The laundry across the alley stopped paying in March. It burned in April.</p>", L(
                    Ch("Pay Birdie", null, () => { S.money -= 30; Apply(new Fx { morale = -4 }); LeisurePhase(); }, cost: 30),
                    Ch("Refuse", null, () =>
                    {
                        if (Rand() < .4f) { S.money = Math.Max(0, S.money - 80); Apply(new Fx { morale = -15 }); F.Set("boycott", true); Log("Shop wrecked by the Deacon's men."); Render("April", "<p>Nobody sees who does it. The windows, the stock, the lamp through the door. The police come in the morning and write nothing down.</p>", L(Ch("Go on", null, LeisurePhase)), new Opts { loc = "chinatown", dark = true }); }
                        else { Apply(new Fx { rep = 8 }); LeisurePhase(); }
                    })), new Opts { loc = "chinatown", dark = true });
            } },

            // 24
            new GameEvent { title = "Gunplay at the M&M", at = "mm", cool = 11, when = () => S.year >= 1909, run = () =>
            {
                Render("Gunplay at the M&M", "<p>One of Kessler's detectives, drunk since the day shift, puts a bullet through the ceiling of the M&M and then one through the mirror, and announces that he will shoot the next Wobbly who speaks. Forty men are on the floor. He is looking at you.</p>", L(
                    Ch("Hit the floor", null, () => { Apply(new Fx { morale = -5 }); LeisurePhase(); }),
                    F.B("gun") ? Ch("Draw", null, () => Gunfight("a drunk Company detective", LeisurePhase)) : null,
                    Ch("Tackle him", null, () => Fight("a drunk Company detective", LeisurePhase, new Fx { rep = 10 }, new FightOpts { saloon = true })),
                    Ch("Out the back, now", null, () =>
                    {
                        if (Rand() < .2f) { Apply(new Fx { health = -18 }); Log("Winged at the M&M."); Render("Winged", "<p>He fires at the kitchen door as it swings and the bullet takes you across the arm. The cook pulls you through.</p>", L(Ch("Go on", null, LeisurePhase)), new Opts { loc = "mm", dark = true }); }
                        else LeisurePhase();
                    })), new Opts { loc = "mm", dark = true });
            } },
            // 25
            new GameEvent { title = "Miners' Day", at = "mm", cool = 6, when = () => S.season == 1 && S.year <= 1913 && S.o.id != "chinese", run = () =>
            {
                Render("Miners' Day", "<p>The 13th of June, the union's holiday, and by nine at night the M&M is wall to wall. A Cornishman sings a hymn; an Irishman answers with a song about Cromwell; a Finn throws a chair at both of them. The whole room goes up at once.</p>", L(
                    Ch("Wade in with the " + (S.o.id == "cornish" ? "Cousin Jacks" : S.o.id == "irish" ? "Gulch" : "boys from " + S.o.where), null, () => Fight("the crowd at the M&M", LeisurePhase, new Fx { rep = 8 }, new FightOpts { saloon = true })),
                    Ch("Hold the bar and keep your beer", null, () =>
                    {
                        if (Rand() < .5f) { Apply(new Fx { health = -8, morale = 4 }); Log("Miners' Day brawl, from the bar."); Render("The bar", "<p>You keep the beer. A flying bottle costs you a tooth, but the beer is intact and the room is impressed.</p>", L(Ch("Go on", null, LeisurePhase)), new Opts { loc = "mm", dark = true }); }
                        else { Apply(new Fx { morale = 6 }); LeisurePhase(); }
                    }),
                    Ch("Out the window", null, () => { Apply(new Fx { rep = -5 }); LeisurePhase(); })), new Opts { loc = "mm", dark = true });
            } },
            // 26
            new GameEvent { title = "The hill road", cool = 6, when = () => S.year >= 1914 && S.o.id != "chinese", run = () =>
            {
                Render("The hill road", "<p>Coming off shift on the Anaconda Road you walk into it: a dozen strikers with pick handles on one side of the road and four of Sayles' men on the other, and the first shot already fired. The strikers know your face. So do Sayles' men.</p>", L(
                    Ch("Into the ditch and stay there", null, () =>
                    {
                        if (Rand() < .2f) { Apply(new Fx { health = -20, morale = -8 }); Log("Caught in crossfire on the hill road."); Render("Crossfire", "<p>A ricochet off the rail finds you anyway.</p>", L(Ch("Go on", null, LeisurePhase)), new Opts { loc = "anaconda", dark = true }); }
                        else { Apply(new Fx { morale = -6 }); LeisurePhase(); }
                    }),
                    F.B("gun") ? Ch("Stand with the strikers", "Guns against guns. Standing goes up, and so does the Company's interest.", () => { Apply(new Fx { rep = 12, heat = 15 }); Gunfight("four of Sayles' gunmen", LeisurePhase); }) : null,
                    Ch("Walk through it like you own the road", null, () =>
                    {
                        if (Rand() < .5f + S.rep / 400f) { Apply(new Fx { rep = 8, morale = 8 }); Log("Walked through the hill road fight."); Render("The road", "<p>Both sides stop to watch you walk past, and by the time anyone decides what you are, you're at the Gulch.</p>", L(Ch("Go on", null, LeisurePhase)), new Opts { loc = "anaconda", dark = true }); }
                        else
                        {
                            int d = 15 + Round(Rand() * 20); Apply(new Fx { health = -d, morale = -5 });
                            if (S.health <= 0) { Ending("shot"); return; }
                            Log("Shot on the hill road.");
                            Render("The road", "<p>Somebody decides. (" + d + " health)</p>", L(Ch("Go on", null, LeisurePhase)), new Opts { loc = "anaconda", dark = true });
                        }
                    })), new Opts { loc = "anaconda", dark = true });
            } },
            // 27
            new GameEvent { title = "Rolled", at = "mercury", cool = 12, when = () => S.money > 120, run = () =>
            {
                Render("Rolled", "<p>You wake in the alley behind the Casino on Mercury Street with a lump on your head and your boots on. Your coat is gone and your pay with it.</p>", L(
                    Ch("Report it to the police", "They laugh.", () => { S.money -= Math.Min(60, S.money); Apply(new Fx { morale = -10, health = -6 }); Log("Rolled in the district."); LeisurePhase(); }),
                    Ch("Go looking for the man", null, () => { S.money -= Math.Min(60, S.money); Apply(new Fx { health = -6 }); Fight("the man with your coat", LeisurePhase, new Fx { money = 60 }); })), new Opts { loc = "mercury", dark = true });
            } },
        };

        // ====================== FIGHTS ======================
        public static float MyStr() => S.o.str * (S.health / 100f + .4f) + S.skill * .03f;

        public static OppStat OppStats(string opp)
        {
            bool armed = EvAny(opp, "gunm", "Sayles", "detective", "deputy", "guard", "Kessler", "striker");
            bool group = EvAny(opp, "three", "and his", "two of", "crowd", "crew");
            return new OppStat { armed = armed, hp = group ? 3 : 2, str = armed ? 1.15f : EvAny(opp, "Cornish", "timberman", "Mulcahy", "Serb") ? 1.1f : 1f, group = group };
        }

        public static void Fight(string opp, Action next, Fx prize = null, FightOpts o = null)
        {
            prize = prize ?? new Fx(); o = o ?? new FightOpts(); var st = OppStats(opp);
            var F = new FightState
            {
                opp = opp, hp = st.hp, str = st.str, armed = st.armed, group = st.group, round = 0, taken = 0, setup = false, next = next, prize = prize, loc = scene.focus,
                saloon = o.saloon != null ? o.saloon.Value : (scene.focus ?? "") == "mm" || (scene.focus ?? "") == "mercury",
            };
            BrawlRound(F, "It comes to blows with " + opp + ". " + (F.saloon ? "The floor of the M&M is sawdust for a reason." : "There is nobody here to break it up."));
        }

        // Note: the parameter F (the fight) hides CC.F (the flags) here, as in the JS; flags are read through S.flags.
        public static void BrawlRound(FightState F, string text)
        {
            F.round++; string loc = F.loc;
            Func<int> oppHits = () => { int d = Round((5 + Rand() * 9) * F.str * (F.setup ? .5f : 1f)); Apply(new Fx { health = -d, morale = -1 }); F.taken += d; return d; };
            Action<string> finish = (html) =>
            {
                if (S.health <= 0) { Ending("dead"); return; }
                if (F.hp <= 0)
                {
                    S.fights++; Apply(new Fx { morale = 10, rep = 6 });
                    if (F.prize.money != null && F.prize.money.Value != 0) S.money += Round(F.prize.money.Value);
                    if (F.prize.rep != null && F.prize.rep.Value != 0) Apply(new Fx { rep = F.prize.rep });
                    Log("Won a fight with " + F.opp + ".");
                    Render("You win", "<p>" + html + " " + Pick("He goes down and stays down. The bartender puts your beer back on the bar.", "It's over. Somebody buys you a drink; somebody else buys him one.", "The room goes quiet and then goes back to its business. That's respect, on the hill.") + "</p>", L(Ch("Go on", null, F.next)), new Opts { loc = loc, dark = true });
                    return;
                }
                if (F.taken >= 30 || S.health < 25)
                {
                    S.fights++; S.money = Math.Max(0, S.money - 10); Apply(new Fx { morale = -8 }); Log("Lost a fight with " + F.opp + ".");
                    Render("You lose", "<p>" + html + " " + Pick("You wake on the sidewalk on Main Street with the streetcar bell ringing in your skull.", "He has a longer reach and a shorter temper. The Sisters at St. James sew your eyebrow.", "They carry you out the back and leave you by the ash cans.") + " Ten dollars lighter.</p>", L(Ch("Limp home", null, F.next)), new Opts { loc = loc, dark = true });
                    return;
                }
                if (F.armed && F.hp <= 1 && Rand() < .45f) { GunPull(F); return; }
                if (F.round >= 5) { Apply(new Fx { morale = -3 }); Render("The police", "<p>" + html + " Two of Vance's policemen come through the door with clubs and the room empties out the back. Nobody wins.</p>", L(Ch("Go on", null, F.next)), new Opts { loc = loc, dark = true }); return; }
                BrawlRound(F, html);
            };
            var opts = new List<Choice>
            {
                Ch("Swing", "Fists. The honest way.", () =>
                {
                    Sfx("punch");
                    if (Rand() < Clamp(MyStr() * .5f + (F.setup ? .25f : 0), .2f, .9f)) { F.hp--; F.setup = false; finish(Pick("You catch him on the ear and he staggers into a table.", "A straight right and his nose goes sideways.", "You hit him where the shift boss hit you, and he feels it.")); }
                    else { int d = oppHits(); F.setup = false; finish(Pick("You swing and miss and he doesn't.", "He steps inside it and puts you into the bar.", "His fist comes out of nowhere.") + " (" + d + " health)"); }
                }),
            };
            if (F.saloon) opts.Add(Ch("Bottle", "Whatever's on the bar. Hits hard, cuts both ways, and the room remembers.", () =>
            {
                // JS: burst(.3,.5,3000,'highpass') — a glass-smash noise; no matching Sfx, left out.
                if (Rand() < .65f) { F.hp -= 2; Apply(new Fx { rep = -3 }); if (Rand() < .3f) Apply(new Fx { health = -5 }); finish(Pick("The bottle breaks over his head and the room goes \"ohh.\"", "A chair leg, two-handed. He goes down like a timber.")); }
                else { int d = oppHits(); finish("You swing wide and he gets in under it. (" + d + " health)"); }
            }));
            opts.Add(Ch(F.saloon ? "Duck behind the bar" : "Back off and circle", "Take less, set up the next one.", () =>
            {
                F.setup = true;
                if (Rand() < .4f) { int d = oppHits(); finish("He comes over the bar after you. (" + d + " health)"); }
                else finish(Pick("You get the bar between you and catch your breath.", "He swings at air and the room laughs at him."));
            }));
            if (S.flags.B("gun")) opts.Add(Ch("Draw the Colt", "This stops being a fist fight.", () => Gunfight(F.opp, F.next, new FightOpts { from = F })));
            if (F.saloon && F.round >= 2) opts.Add(Ch("Let the bartender end it", "He has a bung starter and the final word.", () =>
            {
                Apply(new Fx { morale = -3 });
                Render("Called off", "<p>The bartender comes round with the bung starter and it's over. You both pay for the glass.</p>", L(Ch("Go on", null, () => { S.money = Math.Max(0, S.money - 4); F.next(); })), new Opts { loc = loc, dark = true });
            }));
            if (F.round == 1) opts.Add(Ch("Walk out", null, () => { Apply(new Fx { morale = -6, rep = -4 }); F.next(); }));
            Render(F.round == 1 ? "A fight" : "Round " + F.round, "<p>" + text + "</p><p class=\"note\">" + F.opp + ": " + (F.hp >= 3 ? "still coming" : F.hp == 2 ? "bloodied" : "nearly done") + ". You've taken " + F.taken + ".</p>", opts, new Opts { loc = loc, dark = true });
        }

        /// <summary>JS opp.replace(/^(a|two of|three of) /,'').</summary>
        static string EvStripArticle(string s)
        {
            foreach (var p in new[] { "a ", "two of ", "three of " }) if (s.StartsWith(p, StringComparison.Ordinal)) return s.Substring(p.Length);
            return s;
        }

        // The JS render() clears any running timer; Core's Render does not, so each handler on a timed screen
        // calls ClearTimed() first (the JS "Run for the door" already did so explicitly).
        public static void GunPull(FightState F)
        {
            Render("He goes for his coat", "<p>" + EvStripArticle(F.opp) + " is losing and knows it. His hand goes inside his coat.</p>", L(
                S.flags.B("gun") ? Ch("Draw first", null, () => { ClearTimed(); Gunfight(F.opp, F.next, new FightOpts { from = F, first = true }); }) : null,
                Ch("Rush him", "Close the distance before the gun comes out.", () =>
                {
                    ClearTimed();
                    if (Rand() < .5f) { F.hp = 0; BrawlRound(F, "You get a hand on his wrist and the pistol goes into the sawdust."); }
                    else
                    {
                        int d = 20 + Round(Rand() * 25); Apply(new Fx { health = -d, morale = -10 });
                        if (S.health <= 0) { Ending("shot"); return; }
                        Log("Shot by " + F.opp + ".");
                        Render("Shot", "<p>The pistol goes off against your coat. You go down in the sawdust and the room goes out the door. (" + d + " health)</p>", L(Ch("Go on", null, F.next)), new Opts { loc = F.loc, dark = true });
                    }
                }),
                Ch("Run for the door", null, () =>
                {
                    ClearTimed();
                    if (Rand() < .6f) { Apply(new Fx { morale = -6 }); S.flags.Set("mirror", true); Render("Out the door", "<p>You're on Main Street before the first shot takes the mirror behind the bar.</p>", L(Ch("Go on", null, F.next)), new Opts { loc = F.loc, dark = true }); }
                    else
                    {
                        int d = 15 + Round(Rand() * 25); Apply(new Fx { health = -d, morale = -8 });
                        if (S.health <= 0) { Ending("shot"); return; }
                        Log("Shot running from " + F.opp + ".");
                        Render("Shot", "<p>The second shot takes you in the shoulder on the doorstep. (" + d + " health)</p>", L(Ch("Go on", null, F.next)), new Opts { loc = F.loc, dark = true });
                    }
                })), new Opts { loc = F.loc, dark = true });
            Timed(3, () =>
            {
                Sfx("shot");
                int d = 18 + Round(Rand() * 25); Apply(new Fx { health = -d, morale = -10 });
                if (S.health <= 0) { Ending("shot"); return; }
                Log("Shot by " + F.opp + ".");
                Render("Too slow", "<p>You stand there deciding and he decides first. The pistol comes out of the coat and goes off twice. (" + d + " health)</p>", L(Ch("Go on", null, F.next)), new Opts { loc = F.loc, dark = true });
            });
        }

        public static void Gunfight(string opp, Action next, FightOpts o = null)
        {
            o = o ?? new FightOpts(); var st = OppStats(opp);
            var G = new GunState { opp = opp, next = next, loc = scene.focus, acc = st.armed ? (st.group ? .55f : .45f) : .3f, round = 0, cover = false, company = EvAny(opp, "gunm", "Sayles", "detective", "deputy", "guard", "Kessler") };
            if (o.first) { Apply(new Fx { morale = -3 }); GunRound(G, "You have the Colt out first. He freezes with his hand in his coat."); return; }
            GunRound(G, "The room is on the floor and the lamps are out. It's you and " + opp + " and the Colt.");
        }

        public static void GunRound(GunState G, string text)
        {
            G.round++; string loc = G.loc;
            float myAcc = Clamp(.3f + S.skill * .02f + (S.morale > 60 ? .08f : 0) + (G.cover ? .15f : 0) + (S.o.id == "serb" ? .05f : 0), .2f, .8f);
            Func<float, int> oppShoots = (mult) => { if (Rand() < G.acc * (mult != 0 ? mult : 1)) { int d = 20 + Round(Rand() * 25); Apply(new Fx { health = -d, morale = -6 }); return d; } return 0; };
            Action win = () =>
            {
                S.fights++; S.kills++; Apply(new Fx { rep = 12, morale = -8 }); Log("Shot it out with " + G.opp + ".");
                if (S.kills >= 2 && Rand() < .45f)
                {
                    S.money = Math.Max(0, S.money - 100); S.flags.Set("jailed", true); Apply(new Fx { morale = -10 }); Log("Jailed for a shooting.");
                    Render("The county jail", "<p>He goes down. This time the police do not look the other way: the city has had enough of you, and Vance's judge, who has had enough of everyone, gives you ninety days in the Silver Bow County jail and a hundred-dollar fine. You will miss a season's work.</p>", L(Ch("Go on", null, G.next)), new Opts { loc = "courthouse", dark = true });
                    return;
                }
                string law = G.company ? "Kessler's men carry him out the back and nobody files a report, but the Sixth Floor now has a reason." : "Vance's police take you to the county jail for the night and the judge, who is Vance's judge, fines you forty dollars for discharging a firearm.";
                if (G.company) Apply(new Fx { heat = 30 }); else S.money = Math.Max(0, S.money - 40);
                Render("He goes down", "<p>" + (G.round > 1 ? "Your second shot." : "One shot.") + " He sits down against the bar with a surprised look and does not get up. " + law + "</p>", L(Ch("Go on", null, G.next)), new Opts { loc = loc, dark = true });
            };
            Action<int, string> hit = (d, html) => { if (S.health <= 0) { Ending("shot"); return; } GunRound(G, html + " (" + d + " health)"); };
            var opts = L(
                Ch("Fire", "Hit chance about " + Round(myAcc * 100) + "%.", () =>
                {
                    ClearTimed();
                    Sfx("shot"); G.cover = false;
                    if (Rand() < myAcc) { win(); return; }
                    int d = oppShoots(0);
                    if (d != 0) hit(d, Pick("You miss. He doesn't.", "Your shot takes the mirror. His takes you."));
                    else GunRound(G, Pick("Both shots go wide. The powder smoke is thick as a shift change.", "You miss and so does he. Someone is screaming in the kitchen."));
                }),
                Ch("Take cover", "Behind the bar, a table, a timber. His next shot is halved; yours is steadier.", () =>
                {
                    ClearTimed();
                    G.cover = true; int d = oppShoots(.5f);
                    if (d != 0) hit(d, "He fires through the table."); else GunRound(G, "Splinters, and a moment to breathe.");
                }),
                Ch("Run", "About even odds he lets you go.", () =>
                {
                    ClearTimed();
                    if (Rand() < .55f) { Apply(new Fx { morale = -5 }); Render("Out the back", "<p>Through the kitchen and over the ash cans into the alley. He doesn't follow. You will both remember.</p>", L(Ch("Go on", null, G.next)), new Opts { loc = loc, dark = true }); }
                    else { int d = oppShoots(1.3f); if (d == 0) d = 15; hit(d, "He shoots you in the back on the way to the door."); }
                }));
            if (G.round > 4)
            {
                Render("The street", "<p>" + text + " Then the police, and the Guard behind them with rifles. Everyone with a gun is in the county jail by morning and everyone without one has gone home. Thirty dollars to the judge.</p>", L(Ch("Go on", null, () => { S.money = Math.Max(0, S.money - 30); G.next(); })), new Opts { loc = loc, dark = true });
                return;
            }
            Render(G.round == 1 ? "Gunfight" : "Gunfight, round " + G.round, "<p>" + text + "</p>", opts, new Opts { loc = loc, dark = true });
            Timed(G.round == 1 ? 5 : 4, () =>
            {
                Sfx("shot");
                int d = oppShoots(1.25f);
                if (d != 0) hit(d, "You hesitate. He doesn't."); else GunRound(G, "You hesitate and his shot goes into the bar an inch from your hand.");
            });
        }

        // ====================== RUMORS and TALKS ======================
        public class RumorTake { public string label, d; public Action fn; }
        public class Rumor { public string text; public RumorTake take; }

        public static readonly List<Func<Rumor>> RUMORS = new List<Func<Rumor>>
        {
            () => { string m = Pick("speculator", "mountaincon", "leonard", "neversweat", "orphangirl"); return new Rumor { text = "“The ground at " + MineOf(m) + " is working. Two men heard the back talk all last week and the Company shored nothing.”", take = new RumorTake { label = "Take note of it", d = "If you work there next season you’ll be ready.", fn = () => { S.tip = m; S.tipTurn = TurnIdx(); } } }; },
            () => { string m = Pick("neversweat", "mountaincon", "leonard", "orphangirl"); return new Rumor { text = "“" + EvUcFirst(MineOf(m)) + " is short of men. They’re paying a dollar more a shift to anyone who’ll go down tomorrow.”", take = new RumorTake { label = "Remember it", d = "About 10% more pay there next season.", fn = () => { S.payTip = m; S.payTipTurn = TurnIdx(); } } }; },
            () => new Rumor { text = "“Kessler’s men were up at the Finn hall again. They don’t go in. They stand across the street and write.”", take = new RumorTake { label = "Keep your head down", d = "Less Company attention.", fn = () => Apply(new Fx { heat = -3 }) } },
            () => new Rumor { text = "“Vance’s boys pay five dollars a head at the Casino on election week. Ten if you’ll vote twice.”" },
            () => new Rumor { text = "“There’s a Cousin Jack and an Irishman in the back who’ve been arguing about a shovel since Tuesday. Teddy’s put money on both.”", take = new RumorTake { label = "Laugh", fn = () => Apply(new Fx { morale = 3 }) } },
        };

        public static void RumorTalk(Leisure l, Action done, string who, string nm = null)
        {
            var r = Pick((IList<Func<Rumor>>)RUMORS)(); var ch = new List<Choice>();
            if (r.take != null) { ch.Add(Ch(r.take.label, r.take.d, () => { r.take.fn(); done(); })); ch.Add(Ch("Pay it no mind", null, done)); }
            else ch.Add(Ch("Listen on", null, () => { Apply(new Fx { morale = 2 }); done(); }));
            var sp = FindPlace(l.place);
            Talk(who, r.text, ch, new Opts { loc = (sp != null ? LocFor(sp) : null) ?? "mm", dark = true }, nm);
        }

        /// <summary>JS TALKS[k](l, done): l is the Leisure being done, done continues the evening.</summary>
        public static readonly Dictionary<string, Action<Leisure, Action>> TALKS = new Dictionary<string, Action<Leisure, Action>>
        {
            ["rumor"] = (l, done) => RumorTalk(l, done, "partner"),
            ["gossip"] = (l, done) => RumorTalk(l, done, "customer", "The women at the counter"),
            ["priest"] = (l, done) =>
            {
                int r = Rel("priest", 1);
                Talk("priest", r == 1 ? "He takes your hand in both of his. “New on the hill? I know every new face by Easter. The Company knows them by Monday.”" : Pick("“Your partner’s mother wrote me. She says he doesn’t write her. Tell him that.”", "“The Finns won’t come in, the Cousin Jacks won’t come in, and the Irish come in to argue. I take what I get.”", "“Do you sleep?” he asks, and when you say you do: “Good. Few do.”"), L(
                    Ch("Ask him to write a letter home", "He writes a fair hand, and keeps it between you.", () => { S.sent++; Apply(new Fx { morale = 6 }); done(); }),
                    Ch("Ask for a blessing on the hill", "It won’t stop the rock, but it steadies the hands.", () => { Apply(new Fx { morale = 4, heat = -2 }); done(); }),
                    Ch("Ask what he’s heard", null, () => RumorTalk(l, done, "priest"))), new Opts { loc = "courthouse", dark = true });
            },
            ["papers"] = (l, done) =>
            {
                Talk("librarian", Pick("The Miner prints two columns about the tariff and half a column about the dead. The Anaconda Standard prints the reverse.", "A Seattle paper a week old: the IWW has taken a hall on Yesler Way and the Pinkertons have taken the one across the street.", "The Mine Inspector’s bulletin on hoisting accidents: the Company’s count is on one page and the men’s count on the next."), L(
                    Ch("Mark the page on ground control", "A little skill.", () => { Apply(new Fx { skill = .15f }); done(); }),
                    Ch("Read the want ads", "Somebody’s always hiring.", () => RumorTalk(l, done, "librarian")),
                    Ch("Put it down", null, done)), new Opts { loc = "library", dark = true });
            },
            ["librarian"] = (l, done) =>
            {
                bool nonEng = !(S.o.id == "irish" || S.o.id == "cornish") && !F.B("english");
                Talk("librarian", Rel("librarian", 1) == 1 ? "“Miss Haskell. Three rules: no spitting, no whistling, and if a man from the Company asks what you read, you read the Bible.” She looks at your hands. “Mine, or the ones on the hill?”" : Pick("“You’re back. That makes you the fifth miner this month to ask for something with pictures. I gave him Robinson Crusoe.”", "“Mary MacLane used to sit at that table and write dreadful things. I miss her.”"), L(
                    nonEng ? Ch("Ask her to teach you English", "Evenings with a primer. Your wages stop getting docked for it.", () => { F.Set("english", true); Apply(new Fx { morale = 3, rep = 2 }); done(); }) : null,
                    Ch("Ask for something to read", null, () => { Apply(new Fx { morale = 5 }); done(); }),
                    Ch("Ask about the men who borrow Marx", "She knows who borrows what.", () => { Apply(new Fx { heat = 3, rep = 3 }); done(); })), new Opts { loc = "library", dark = true });
            },
            ["secretary"] = (l, done) =>
            {
                Talk("secretary", Rel("secretary", 1) == 1 ? "“Ed Doyle, recording secretary. Thirteen thousand members, and not one of them has ever read the by-laws.”" : Pick("“Dues are a dollar a month and a man’s good name. The Company wants both.”", "“The Hall is the biggest north of Denver. They tell me that’s why somebody wants to blow it up.”"), L(
                    Ch("Pay your dues", "Standing with the men.", () => { S.money -= 5; Apply(new Fx { rep = 5, heat = S.year >= 1912 ? 3 : 0 }); done(); }, cost: 5, disabled: S.money < 5),
                    Ch("Ask who’s hiring", "Word travels fast in the Hall.", () =>
                    {
                        string m = Pick("neversweat", "mountaincon", "leonard", "orphangirl"); S.payTip = m; S.payTipTurn = TurnIdx();
                        Talk("secretary", "“" + EvUcFirst(MineOf(m)) + " is short a crew. Tell them the Hall sent you.” About 10% more pay there next season.", L(Ch("Thank him", null, done)), new Opts { loc = "union", dark = true });
                    }),
                    Ch("Ask about the Company’s list", null, () => Talk("secretary", S.heat > 60 ? "He looks at the door before he answers. “Your name is being said on the sixth floor.”" : S.heat > 30 ? "“They’re asking about you, but they ask about everyone.”" : "“You’re a name on a form and nothing more. Stay that way.”", L(Ch("Thank him", null, done)), new Opts { loc = "union", dark = true }))), new Opts { loc = "union", dark = true });
            },
            ["sister"] = (l, done) =>
            {
                Talk("sister", Rel("sister", 1) == 1 ? "“Sister Mary Agnes. I’ve nursed three hundred miners and buried most of them. Let me hear you breathe.” She listens at your back with a cold tube." : Pick("“Miner’s con takes fifteen years if you’re careful and five if you’re not.”", "“I light a candle every night for every man in this ward. The taper bill is dreadful.”"), L(
                    Ch("Ask what to do for your lungs", "Advice, and a little relief.", () =>
                    {
                        Apply(new Fx { lungs = (S.lungs < 80 && !F.B("sisterAdvice")) ? 2 : 0, morale = 2 }); F.Set("sisterAdvice", true);
                        Talk("sister", "“Breathe through your nose. A wet cloth over the mouth when the dust is bad. And the Finns are right about the sauna.”", L(Ch("Thank her", null, done)), new Opts { loc = "stjames", dark = true });
                    }),
                    Ch("Sit with the men in the ward", "Some of them have no one.", () => { Apply(new Fx { morale = 3, rep = 3 }); done(); }),
                    Ch("Leave ten dollars for the ward", null, () => { S.money -= 10; Apply(new Fx { morale = 5, rep = 4 }); done(); }, cost: 10, disabled: S.money < 10)), new Opts { loc = "stjames", dark = true });
            },
            ["wobbly"] = (l, done) =>
            {
                Talk("wobbly", "“Red” Tom Quill sweats beside you with a pamphlet he has been trying to keep dry. “A man dies on this hill every week, and the Company writes him off like a lost shovel. One big union, brother.”", L(
                    Ch("Hear him out", "Standing goes up. So does the Company’s interest.", () => { Apply(new Fx { rep = 4, heat = 6 }); done(); }),
                    Ch("Take a pamphlet", "More of both.", () => { Apply(new Fx { rep = 7, heat = 12 }); done(); }),
                    Ch("Talk about something else", null, () => { Apply(new Fx { morale = 2 }); done(); })), new Opts { loc = "finntown", dark = true });
            },
        };
    }
}
