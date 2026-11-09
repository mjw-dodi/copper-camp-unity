// Copper Camp — short scenes for the evening actions that used to pass in silence (eating at the boarding house,
// drinking with the crew, the free lunch, Mass, the sauna, the doctor and the rest). New to the Unity version:
// the browser game applies these actions without a word. Each scene is a few lines that change with the year,
// the season, where you came from and how you stand, and ends with one button. The stat effects are unchanged.
// scene.speech / scene.speaker carry the spoken line so the 3D room can show it over the speaker's head.
using System;
using System.Collections.Generic;
using System.Linq;

namespace CopperCamp
{
    public static partial class CC
    {
        /// <summary>One way a scene can go: who speaks (portrait id, or null for plain narration), the text,
        /// the line shown in the room, and the button.</summary>
        class Beat
        {
            public string who, name, text, say, button, role;
            public Func<bool> when;
        }

        static Beat B(string who, string role, string text, string say, string button, Func<bool> when = null, string name = null)
            => new Beat { who = who, role = role, text = text, say = say, button = button, when = when, name = name };

        static bool Yr(int a, int b) => S.year >= a && S.year <= b;
        static bool Org(params string[] ids) => ids.Contains(S.o.id);
        static string Season => new[] { "spring", "summer", "autumn", "winter" }[S.season];

        /// <summary>The beats for a leisure action, or null if it has none (it has its own scene already).</summary>
        static List<Beat> BeatsFor(string id)
        {
            string partner = PersonName("partner"), ll = PersonName("landlady");
            switch (id)
            {
                case "board":
                    return new List<Beat>
                    {
                        B("landlady", "landlady", "<p>" + ll + " puts down a plate of stew with a slice of bread standing in it like a headstone. “Eat it hot. The Finn in the back room eats his cold and look at him.”</p><p>You eat it hot. There is a second plate you didn’t ask for.</p>", "Eat it hot.", "Eat"),
                        B("landlady", "landlady", "<p>Pasties on Tuesday, stew on Wednesday, pasties again on Thursday because the Cornishmen complained. Tonight it is a roast, and " + ll + " carves it herself so no one gets the end piece twice.</p><p>“You had the end last week,” she says to you, and gives it to you anyway.</p>", "You had the end last week.", "Eat"),
                        B("landlady", "landlady", "<p>The long table is full of men from three different shifts, eating in shifts. Nobody talks until the plates are clean. Then everybody talks at once about the Company, the ground, and whose turn it is to fetch the coal.</p><p>" + ll + " bangs the ladle on the pot. “Coal. You.”</p>", "Coal. You.", "Fetch the coal", () => S.season == 3),
                        B("landlady", "landlady", "<p>" + ll + " watches you eat with her arms folded. “You’re thinner than when you came.” She puts the bread basket on your side of the table. “The hill does that. So does not eating.”</p>", "You’re thinner than when you came.", "Eat", () => S.health < 60),
                        B("landlady", "landlady", "<p>There is fresh rhubarb from somebody’s brother-in-law in Meaderville and " + ll + " has made it into a pie before anyone could argue. It is sour enough to make a shift boss smile.</p>", "Mind the stones in it.", "Eat", () => S.season == 0 || S.season == 1),
                        B("landlady", "landlady", "<p>Supper is quiet. Half the table works the Speculator and the North Butte and the chairs at that end are empty. " + ll + " sets their places anyway and nobody says anything about it.</p>", "Set them anyway.", "Eat", () => S.year == 1917 && S.season >= 1),
                        B("landlady", "landlady", "<p>The meat is thin this month and the potatoes are thick. “The strike,” " + ll + " says, as if that explains the price of beef in Chicago. It does.</p>", "The strike.", "Eat", () => Yr(1914, 1914) || S.strike),
                    };
                case "sleep":
                    return new List<Beat>
                    {
                        B(null, null, "<p>Your bed is still warm from the man on the day shift who sleeps in it while you work. The blanket smells like his tobacco. You are asleep before you can mind.</p>", null, "Sleep"),
                        B(null, null, "<p>Through the wall the Finn coughs, and stops, and coughs. Out on the hill the hoists run all night. You count the bells until you lose count.</p>", null, "Sleep"),
                        B(null, null, "<p>The frost is on the inside of the window. You sleep in your socks and your coat on top of the blanket, the way everyone does in the Gulch in January.</p>", null, "Sleep", () => S.season == 3),
                        B(null, null, "<p>Somebody is singing on the street below, badly, in a language you don’t have. Then a window goes up, and he stops, and you sleep.</p>", null, "Sleep"),
                    };
                case "drink":
                    return new List<Beat>
                    {
                        B("partner", "partner", "<p>" + partner + " has the corner of the bar and a glass waiting for you. “To the ground,” he says, “for holding.” The crew drinks to that. Somebody adds, “so far,” and they drink to that too.</p>", "To the ground, for holding.", "Drink"),
                        B("partner", "partner", "<p>The talk is the shift boss, then the price of copper, then the shift boss again. " + partner + " does an impression of him that gets the whole end of the bar laughing, including, it turns out, the shift boss’s brother.</p><p>He buys the next round to make up for it.</p>", "He talks like that, I swear.", "Laugh"),
                        B("partner", "partner", "<p>“Seven stacks on the Anaconda Road,” " + partner + " says, counting them on the bar with his finger in the beer. “Seven. And not one of them is ours.” He drinks. “Still. Warm in here.”</p>", "Not one of them is ours.", "Drink"),
                        B("partner", "partner", "<p>A Welshman at the next table starts “Calon Lân” and the Cornishmen take it up, and for once the Irish let them. The M&M goes quiet to listen. Then it goes loud again.</p>", "Let them sing.", "Listen", () => !Org("chinese")),
                        B("partner", "partner", "<p>Someone has a letter from home and reads it out, all of it, including the part about the cow. By the end every man at the bar knows the cow’s name and has an opinion on the price she should fetch.</p>", "Eighteen dollars, not a cent less.", "Drink"),
                        B("partner", "partner", "<p>Payday, and the M&M is three deep at the bar. " + partner + " gets the drinks over the heads of two timbermen without spilling one. “Practice,” he says. “Same as tramming.”</p>", "Same as tramming.", "Drink", () => S.money > 150),
                        B("partner", "partner", "<p>The talk tonight is all the union and who was seen going in to the Company office. Men keep looking at the door. " + partner + " keeps his voice down and his glass full. “Drink up. Talk later. Talk never.”</p>", "Talk never.", "Drink", () => Yr(1912, 1914)),
                        B("partner", "partner", "<p>The crew drinks to the men from the Speculator. Nobody makes a speech. Somebody puts a full glass on the end of the bar and nobody touches it all night.</p>", "Leave it there.", "Drink", () => S.year == 1917 && S.season >= 1),
                    };
                case "lunch":
                    return new List<Beat>
                    {
                        B("teddy", "barkeep", "<p>The free lunch is hard-boiled eggs, pickled herring, rye bread and a ham that has seen better days. Teddy watches you take two eggs. “The sign says free,” he says. “It doesn’t say twice.”</p>", "It doesn’t say twice.", "Eat the eggs"),
                        B("teddy", "barkeep", "<p>You make a sandwich out of the free lunch that would shame a Cousin Jack’s pasty. Teddy slides a nickel beer down the bar to pay for it, which is the custom, and which you pay.</p>", "Nickel for the beer.", "Pay the nickel"),
                        B("teddy", "barkeep", "<p>Somebody has eaten all the herring. Teddy brings out a new crock of it from under the bar with the air of a man producing a rabbit from a hat.</p>", "Herring. Fresh as Tuesday.", "Eat"),
                    };
                case "district":
                    return new List<Beat>
                    {
                        B("nell", "dancer", "<p>A dollar buys a dance and a dance lasts as long as the piano player says it does, which isn’t long. The girl from the Casino counts your steps out loud in a voice that says she has counted a thousand men’s.</p>", "One-two-three, one-two-three.", "Dance", name: "A girl at the Casino"),
                        B("nell", "dancer", "<p>The band is a piano, a fiddle and a cornet that has been in a fight. Venus Alley is louder than the hoists. You dance until your boots hurt, which in mining boots is quickly.</p>", "Another dollar, another dance.", "Dance", name: "A girl at the Casino"),
                        B("nell", "dancer", "<p>The girl you dance with is from Duluth and has never been underground, she says, and never will, she says, and asks you what it’s like. You tell her. She laughs at the wrong parts and that turns out to be the right thing to do.</p>", "What’s it like down there?", "Dance", name: "A girl at the Casino"),
                    };
                case "church":
                    return new List<Beat>
                    {
                        B("priest", "priest", "<p>Father Brosnan says the Mass for the men going down at seven. He reads the names of the week’s dead before the Gospel. It takes a while. The church is full of miners’ wives who already know the names.</p>", "Eternal rest grant unto them.", "Kneel"),
                        B("priest", "priest", "<p>The sermon is on the rich man and the eye of the needle. Father Brosnan does not look at the Hennessy Building while he gives it. He does not need to.</p>", "Easier for a camel.", "Listen"),
                        B("priest", "priest", "<p>After Mass Father Brosnan stands on the steps and shakes every hand. He holds yours a second longer. “Your mother writes to me, you know,” he says. “I tell her you’re fine. Don’t make me a liar.”</p>", "Don’t make me a liar.", "Promise him", () => Org("irish", "italian")),
                        B("priest", "priest", "<p>The candles in front of St. Barbara, patron of miners, are lit three deep. You light one more. The money box says five cents. You put in ten for the man next to you who doesn’t have it.</p>", "Saint Barbara keep you.", "Light a candle"),
                        B("priest", "priest", "<p>Christmas Mass, and the church is so full the men stand in the aisle with their hats in their hands and snow melting off their shoulders. The choir sings in Latin, and then, because the Father allows it, in Irish and Italian and Croatian.</p>", "Peace to men of good will.", "Sing", () => S.season == 3),
                    };
                case "study":
                    return new List<Beat>
                    {
                        B("librarian", "librarian", "<p>Miss Haskell brings you the School of Mines bulletin on square-set timbering and a pencil. “You may not write in it,” she says. “You may write about it.” You fill two pages with drawings of sets.</p>", "You may write about it.", "Study", name: "Miss Haskell"),
                        B("librarian", "librarian", "<p>There is a book on ground pressure by a German professor, translated badly. You read the same page four times. Then it makes sense all at once, the way a hanging wall does just before it comes down.</p>", "Shh.", "Study", name: "Miss Haskell"),
                        B("librarian", "librarian", "<p>Miss Haskell has found you a report on a cave-in at the Mountain Con, with the timbering diagrams. “Read the part about the wedges,” she says. “Twice.”</p>", "Read the part about the wedges.", "Study", name: "Miss Haskell"),
                    };
                case "gamers":
                    return new List<Beat>
                    {
                        B("counter", "counter", "<p>The pasty comes in a paper sleeve, hot enough to warm your hands on the walk home if you could wait that long. You can’t. The woman behind the counter has seen men burn their mouths on these for years. “Blow on it,” she says, without hope.</p>", "Blow on it.", "Eat", name: "The woman at Gamer’s"),
                        B("counter", "counter", "<p>Gamer’s at midnight: the night shift going down, the swing shift coming up, and a pair of lawyers from the courthouse pretending not to be drunk. Coffee is five cents and refills are a matter of how the woman behind the counter feels about you.</p><p>She feels all right about you.</p>", "Another cup?", "Drink the coffee", name: "The woman at Gamer’s"),
                        B("counter", "counter", "<p>The counter woman has the newspaper open to the copper price. “Down two cents,” she says. “That means my pasties go up one.” She says it like a joke. It isn’t.</p>", "Down two cents.", "Eat", () => S.year == 1907 || S.year == 1914, name: "The woman at Gamer’s"),
                        B("counter", "counter", "<p>She gives you the end of the pie at the price of a slice. “You look like a man who just came up,” she says. “Everybody does. You look like it more.”</p>", "You look like you just came up.", "Eat", () => S.lungs < 70, name: "The woman at Gamer’s"),
                    };
                case "sendhome":
                    return new List<Beat>
                    {
                        B("clerk", "clerk", "<p>The clerk at Hennessy’s post office counter writes out the money order in a hand like copperplate. “Fifty dollars to " + HomePlace() + ".” He stamps it. “That’ll be a new roof, or a fiddle and a funeral. They always write back which.”</p>", "Fifty dollars, stamped.", "Send it", name: "The clerk at Hennessy’s"),
                        B("clerk", "clerk", "<p>You write a letter to go with the money. It says the work is good and the pay is good and the town is grand. It doesn’t say anything about the hill. The clerk seals it without reading it. He has sealed a thousand like it.</p>", "Next.", "Seal it", name: "The clerk at Hennessy’s"),
                        B("clerk", "clerk", "<p>“Again?” says the clerk, who knows your name now. “Your people must think the streets here are paved with copper.” He looks out the window at Main Street, which is paved with mud. “Well. Underneath they are.”</p>", "Underneath they are.", "Send it", () => S.sent > 0, name: "The clerk at Hennessy’s"),
                    };
                case "gun":
                    return new List<Beat>
                    {
                        B("clerk", "clerk", "<p>The clerk in the hardware department lays the Colt on a square of green felt like a jeweler. “Thirty-eight Special. They’ll sell you a cheaper one on Galena Street, and it will go off in your pocket.” He wraps it in brown paper. “Don’t tell me what it’s for.”</p>", "Don’t tell me what it’s for.", "Take it", name: "The clerk at Hennessy’s"),
                    };
                case "union":
                    return new List<Beat>
                    {
                        B("speaker", "speaker", "<p>The hall is full and hot and blue with pipe smoke. The secretary reads the minutes. Then the treasurer reads the accounts. Then a man from the floor stands up and says what everyone came to hear, which is that the Company can’t run a mine without miners.</p><p>The applause shakes the windows on North Main.</p>", "They can’t run a mine without miners!", "Applaud"),
                        B("speaker", "speaker", "<p>The meeting is about the rustling card: every man needs one from the Company to get hired, and the Company can take it away. The speaker holds his up. “A dog license,” he says. “For men.”</p>", "A dog license. For men.", "Stand up", () => S.year >= 1912),
                        B("speaker", "speaker", "<p>Miners’ Union Day is coming, June 13th, and the meeting argues about the parade for an hour: who carries the banner, who marches first, whether the band can play anything but the same three tunes. It is the happiest argument you have heard in Butte.</p>", "The band plays what the band plays.", "Vote", () => S.season == 0),
                        B("speaker", "speaker", "<p>A Finn stands up and speaks for ten minutes in Finnish. Nobody else understands a word. When he sits down the whole hall applauds anyway, because everybody understood the last word, which was “Company.”</p>", "Yhtiö! The Company!", "Applaud", () => !Org("chinese")),
                    };
                case "hospital":
                    return new List<Beat>
                    {
                        B("sister", "sister", "<p>The doctor listens to your chest for a long time and doesn’t say anything, which is worse than if he had. Sister Mary Agnes writes on a card. “Rest,” the doctor says at last. “Eat. Stay out of the hot levels.” He knows you won’t. He says it anyway.</p>", "Breathe in. And out.", "Breathe", () => S.lungs < 60, name: "Sister Mary Agnes"),
                        B("sister", "sister", "<p>They clean the cut on your hand that you didn’t know had gone bad and put a dressing on it that smells of carbolic. Sister Mary Agnes ties the bandage herself. “Change it Tuesday,” she says. “Come back if it goes red. Come back if it doesn’t, too. I like to see the ones that live.”</p>", "Change it Tuesday.", "Thank her", name: "Sister Mary Agnes"),
                        B("sister", "sister", "<p>The ward is full of men from the hill: a crushed foot, a burned face, a man who fell down the Badger and lived to complain about the food. You get a bed for an afternoon and the best sleep you’ve had since you came.</p>", "Sleep. That’s an order.", "Sleep", name: "Sister Mary Agnes"),
                    };
                case "sauna":
                    return new List<Beat>
                    {
                        B("finn", "finn", "<p>The heat comes up off the stones like a hand. An old Finn ladles water on them and the room disappears in steam. “Löyly,” he says, which is the word for this, and the only word anybody says for twenty minutes.</p>", "Löyly.", "Sweat"),
                        B("finn", "finn", "<p>The Finns beat themselves with birch switches and then, as a courtesy, beat you. Afterwards you go out into the cold and stand steaming in the alley like a horse. For the first time in weeks you can breathe all the way to the bottom.</p>", "Out! In the snow!", "Go out in the cold", () => S.season == 3),
                        B("finn", "finn", "<p>On the bench the men talk about the union, the Company, and the One Big Union the IWW men keep talking about, in Finnish, which the Company spotters can’t follow. One of them winks at you. “Here,” he says in English, “even the walls are Finnish.”</p>", "Here even the walls are Finnish.", "Sweat", () => S.year >= 1912),
                        B("finn", "finn", "<p>The keeper takes your money and gives you a towel the size of a handkerchief. “Finns don’t need towels,” he says. “You’re not a Finn. You can have the big one.” This is the big one.</p>", "This is the big one.", "Sweat", () => !Org("finnish")),
                    };
                case "gardens":
                    return new List<Beat>
                    {
                        B(null, null, "<p>The streetcar out to Columbia Gardens is full of miners’ kids in their Sunday clothes. Out there the air is clean and the grass is green, which is the strangest thing you have seen in Butte. The pavilion band plays Sousa. Somebody’s child is sick on the roller coaster.</p>", null, "Ride back at dusk"),
                        B(null, null, "<p>The Gardens on a Thursday: Children’s Day, free streetcar fare for the kids, and the Senator’s flower beds laid out in the shape of butterflies and pansies. You lie on the grass and look at a sky that has no smoke in it.</p>", null, "Lie in the grass", () => S.season == 1),
                        B(null, null, "<p>You ride the carousel because nobody you know is there to see you do it. The painted horse is called Copper King, according to the brass plate. You take that as a sign of something.</p>", null, "Ride again"),
                    };
            }
            return null;
        }

        static string HomePlace()
        {
            switch (S.o.id)
            {
                case "irish": return "County Cork";
                case "cornish": return "Redruth";
                case "finnish": return "Oulu";
                case "italian": return "Belluno";
                case "serb": return "Herzegovina";
                case "chinese": return "Taishan";
            }
            return "home";
        }

        /// <summary>Shows a short scene for a leisure action that has no scene of its own, then continues. False if none.</summary>
        public static bool LeisureScene(Leisure l, Action after)
        {
            var all = BeatsFor(l.id);
            if (all == null) return false;
            var ok = all.Where(b => b.when != null && b.when()).ToList();
            // conditional beats win half the time when they apply; otherwise any unconditioned beat
            var pool = ok.Count > 0 && Rand() < .5f ? ok : all.Where(b => b.when == null).ToList();
            if (pool.Count == 0) pool = ok;
            if (pool.Count == 0) return false;
            var beat = pool[(int)Math.Floor(Rand() * pool.Count) % pool.Count];
            var sp = FindPlace(l.place);
            // inside a room the caption already names the room; only re-aim the view when the scene happens outdoors
            bool indoors = scene.walk != null && scene.walk.inside;
            var opts = new Opts { loc = indoors ? null : (sp != null ? LocFor(sp) : l.place), dark = true };
            var ch = L(Ch(beat.button ?? "Go on", null, after));
            if (beat.who != null) Talk(beat.who, beat.text, ch, opts, beat.name ?? PersonName(beat.who));
            else Render(l.name, beat.text, ch, opts);
            Speak(beat.role, beat.say);
            return true;
        }

        /// <summary>What the 3D room shows over the speaker's head; cleared by the next screen.</summary>
        public static void Speak(string role, string line)
        {
            scene.speaker = role; scene.speech = line; scene.speechSerial++;
        }

        /// <summary>For the conversations the game already had (Teddy, Nell, the landlady, the priest...): the first quoted
        /// words of the text, short enough for a speech bubble.</summary>
        static string FirstQuote(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            string t = System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", "");
            // walk the quotes in order, pairing each opening mark with the next closing one, so the words between two
            // quotations ("...," she says. "...") are never mistaken for speech
            var ms = System.Text.RegularExpressions.Regex.Matches(t, "[\"“]([^\"”]*)[\"”]");
            for (int k = 0; k < ms.Count; k++)
            {
                string s = ms[k].Groups[1].Value.Trim();
                if (s.Length < 3) continue;
                // “The sign says free,” he says. “It doesn’t say twice.” — one speech broken by the narration
                if (s.EndsWith(",") && k + 1 < ms.Count) s = s.TrimEnd(',') + ". " + ms[k + 1].Groups[1].Value.Trim();
                // a long speech: keep its first sentence (at least a few words long)
                if (s.Length > 24)
                {
                    int cut = s.IndexOfAny(new[] { '.', '?', '!' }, 24);
                    if (cut > 0 && cut < s.Length - 1) s = s.Substring(0, cut + 1);
                }
                return s.Length > 90 ? s.Substring(0, 87).TrimEnd() + "…" : s;
            }
            return null;
        }

        static readonly Dictionary<string, string> TALK_ROLE = new Dictionary<string, string>
        {
            { "teddy", "barkeep" }, { "nell", "nell" }, { "landlady", "landlady" }, { "priest", "priest" }, { "partner", "partner" },
            { "wobbly", "finn" }, { "librarian", "librarian" }, { "sister", "sister" }, { "secretary", "speaker" }, { "counter", "counter" }, { "clerk", "clerk" }
        };
    }
}
