// Copper Camp — core data tables (JS ORIGINS, JOBS, JOBS_CN, LEISURE, MINE, ACCIDENTS, LOC, PEOPLE, GREEN, STREET, PLACE_LINES).
using System.Collections.Generic;
using System.Linq;

namespace CopperCamp
{
    public class StreetPlace
    {
        public string id, shortName, style, sub;
        public float x, w, h;
        public bool spire, cross, lights;
        public string[] acts;
        public System.Func<string> label;
        public System.Func<bool> ruin;
        public string Label => label();
        public bool IsRuin => ruin != null && ruin();
    }

    public static partial class CC
    {
        public static readonly Origin[] ORIGINS =
        {
            new Origin { id = "irish", name = "Irish", home = "gulch", where = "Dublin Gulch", desc = "Off the boat from Cork and up the hill on the Hibernians' say-so. Half the hill is Irish; the other half holds it against you.", health = 75, lungs = 100, morale = 65, money = 12, rep = 12, skill = 2, str = 1.0f, perk = "Cheap board in the Gulch; union standing grows fast." },
            new Origin { id = "cornish", name = "Cornish", home = "centerville", where = "Centerville", desc = "A Cousin Jack from Camborne, third generation underground. You read ground like scripture and the Irish never let you forget you're Methodist.", health = 70, lungs = 95, morale = 60, money = 18, rep = 5, skill = 5, str = .9f, perk = "Fewer accidents, better pay; Irish crews pick fights." },
            new Origin { id = "finnish", name = "Finnish", home = "finntown", where = "Finntown", desc = "From Vaasa to East Broadway. The sauna keeps you alive. The Socialist hall keeps you hopeful. The Company keeps a file.", health = 85, lungs = 100, morale = 55, money = 8, rep = 15, skill = 3, str = 1.1f, perk = "Sauna heals; the Company watches Finntown closely." },
            new Origin { id = "italian", name = "Italian", home = "meaderville", where = "Meaderville", desc = "Up from Calabria to a house with a garden in Meaderville, where the wine is in the cellar and the Leonard runs hot as a bread oven.", health = 75, lungs = 100, morale = 70, money = 10, rep = 5, skill = 3, str = .95f, perk = "Food and wine lift morale cheaply." },
            new Origin { id = "serb", name = "Serbian", home = "eastside", where = "the East Side", desc = "From the Lika hills to a bunk on East Park Street. Little English yet, long arms, and nobody in the M&M starts anything twice.", health = 90, lungs = 100, morale = 50, money = 5, rep = 5, skill = 2, str = 1.3f, perk = "You win fights; the shift boss shorts your pay until your English comes in." },
            new Origin { id = "chinese", name = "Chinese", home = "chinatown", where = "China Alley", desc = "From Taishan to China Alley off Mercury Street. The union bars you from the mines, so you live above ground: noodles, laundry, herbs, and whatever the boycott leaves.", health = 70, lungs = 100, morale = 55, money = 35, rep = 0, skill = 1, str = .85f, perk = "No rock falls on you; pay is thin and boycotts come without warning." },
        };

        public static readonly Job[] JOBS =
        {
            new Job { id = "neversweat", name = "Mucker at the Anaconda \"Neversweat\"", loc = "anaconda", pay = 260, risk = .14f, lungs = 3, co = true, desc = "Shovel ore under the seven stacks on the Anaconda Road. Steady ground, steady pay." },
            new Job { id = "mountaincon", name = "Timberman at the Mountain Con", loc = "mountaincon", pay = 290, risk = .18f, lungs = 4, co = true, desc = "Set square-set timbers three thousand feet down in Centerville, the deepest shaft on the hill." },
            new Job { id = "speculator", name = "Machine man at the Speculator", loc = "speculator", pay = 320, risk = .24f, lungs = 6, co = false, desc = "Run a widow-maker drill for the North Butte company on Granite Mountain. Best money on the hill, worst air." },
            new Job { id = "leonard", name = "Hot box at the Leonard", loc = "leonard", pay = 300, risk = .2f, lungs = 5, co = true, desc = "The Leonard in Meaderville runs so hot the men work in their drawers and the Company sends down ice." },
            new Job { id = "orphangirl", name = "Trammer at the Orphan Girl", loc = "orphangirl", pay = 215, risk = .09f, lungs = 2, co = false, desc = "Push ore cars at the Orphan Girl on the west edge of town. Zinc and silver, quieter ground." },
            new Job { id = "hennessy", name = "Clerk at Hennessy's Mercantile", loc = "hennessy", pay = 150, risk = 0, lungs = 0, co = false, desc = "Stack dry goods on Granite Street under Silas Hobart's sixth-floor offices. No rock, no risk, no respect." },
            new Job { id = "idle", name = "Sit out the season", loc = null, pay = 0, risk = 0, lungs = 0, co = false, desc = "Rest the lungs and the back. Board still comes due." },
        };

        public static readonly Job[] JOBS_CN =
        {
            new Job { id = "pekin", name = "Cook at the noodle parlor", loc = "chinatown", pay = 180, risk = 0, lungs = 0, desc = "Chow mein and coffee for miners coming off shift at a Main Street noodle house. The Pekin opens upstairs in 1911." },
            new Job { id = "laundry", name = "Laundryman on China Alley", loc = "chinatown", pay = 140, risk = 0, lungs = 1, desc = "Boil, starch and press a hundred shirts a day in the steam. Cheap, hard, and the first trade the boycott hits." },
            new Job { id = "herbs", name = "Clerk at Wah Chong Tai & Co.", loc = "chinatown", pay = 205, risk = 0, lungs = 0, desc = "Herbs, tea, silk and credit for the whole alley. Good pay when the mercantile has it." },
            new Job { id = "idle", name = "Sit out the season", loc = "chinatown", pay = 0, risk = 0, lungs = 0, desc = "Rest. Board still comes due." },
        };

        public static readonly Leisure[] LEISURE =
        {
            new Leisure { id = "board", place = "home", name = "Eat well at the boarding house", cost = 30, fx = new Fx { health = 8, morale = 3 }, once = true, desc = "The landlady’s full table: beef, potatoes, pie." },
            new Leisure { id = "landlady", place = "home", name = "Sit up with the landlady", cost = 0, fx = new Fx { morale = 3 }, desc = "Coffee at the kitchen table. She knows everyone’s business." },
            new Leisure { id = "sleep", place = "home", name = "Turn in for the night", cost = 0, fx = new Fx { health = 8, lungs = 1 }, ends = true, desc = "Nothing happens; nothing is also what hurts least. Ends the evening." },
            new Leisure { id = "church", place = "church", name = "Mass at St. Patrick’s", cost = 3, fx = new Fx { morale = 6, heat = -4 }, once = true, desc = "Even the Company looks kindly on a man in a pew." },
            new Leisure { id = "priest", place = "church", name = "Talk to Father Brosnan", cost = 0, talk = "priest", once = true, desc = "He hears more than the Company does, and tells less." },
            new Leisure { id = "library", place = "library", name = "Read the papers", cost = 0, fx = new Fx { morale = 3 }, talk = "papers", once = true, desc = "The Miner, the Anaconda Standard, and a week-old Seattle paper." },
            new Leisure { id = "study", place = "library", name = "Study ground control and timbering", cost = 0, fx = new Fx { skill = .25f }, once = true, desc = "Miss Haskell finds you the Mine Inspector’s bulletin and a pencil." },
            new Leisure { id = "librarian", place = "library", name = "Talk to Miss Haskell", cost = 0, talk = "librarian", once = true, desc = "She knows who borrows what." },
            new Leisure { id = "gamers", place = "gamers", name = "Pasties at Gamer’s", cost = 12, fx = new Fx { health = 4, morale = 4 }, once = true, desc = "A pasty and coffee, cheap and honest." },
            new Leisure { id = "gossip", place = "gamers", name = "Gossip at the counter", cost = 0, fx = new Fx { morale = 2 }, talk = "gossip", once = true, desc = "The wives talk; the night shift listens." },
            new Leisure { id = "sendhome", place = "hennessy", name = "Send money home", cost = 50, fx = new Fx { morale = 10 }, desc = "A money order at the post office counter. Letters come back slow." },
            new Leisure { id = "gun", place = "hennessy", name = "Buy a revolver", cost = 18, fx = new Fx { morale = 3 }, desc = "A .38 Colt from the hardware counter. Every fight after this can go to guns, and the ones with Company men will." },
            new Leisure { id = "drink", place = "mm", name = "Drink with the crew at the M&M", cost = 15, fx = new Fx { morale = 12 }, fight = .25f, desc = "A round for the crew, a song nobody can sing. Trouble likely." },
            new Leisure { id = "lunch", place = "mm", name = "The free lunch", cost = 3, fx = new Fx { health = 4, morale = 2 }, once = true, desc = "A nickel beer and all you can eat from the counter." },
            new Leisure { id = "faro", place = "mm", name = "Play faro", cost = 40, run = "faro", desc = "The bank at the back never closes. Near enough to even odds." },
            new Leisure { id = "listen", place = "mm", name = "Listen to the room", cost = 0, talk = "rumor", once = true, desc = "Forty men and a piano. Somebody always knows something." },
            new Leisure { id = "teddy", place = "mm", name = "Talk to Teddy behind the bar", cost = 0, fx = new Fx { morale = 2 }, desc = "He knows what the hill says about you." },
            new Leisure { id = "union", place = "union", name = "Sit in on the union meeting", cost = 0, fx = new Fx { rep = 6 }, once = true, until = "1914-1", desc = "Speeches, motions, and the men who know what’s coming. The Company keeps a list." },
            new Leisure { id = "secretary", place = "union", name = "Talk to Secretary Doyle", cost = 0, talk = "secretary", once = true, until = "1914-1", desc = "Dues, jobs, and what the Hall has heard about you." },
            new Leisure { id = "hospital", place = "stjames", name = "See the doctor at St. James", cost = 60, fx = new Fx { health = 25 }, once = true, desc = "The Sisters of Charity will patch you up and ask about your soul." },
            new Leisure { id = "sister", place = "stjames", name = "Talk to Sister Mary Agnes", cost = 0, talk = "sister", once = true, desc = "She has nursed three hundred miners." },
            new Leisure { id = "sauna", place = "sauna", name = "The Finntown sauna", cost = 4, fx = new Fx { health = 8, lungs = 2 }, once = true, desc = "Steam the rock dust out. Finns go free; everyone else pays the old man at the door." },
            new Leisure { id = "finns", place = "sauna", name = "Talk with the men on the bench", cost = 0, talk = "wobbly", once = true, desc = "Birch, steam, and a pamphlet nobody admits to." },
            new Leisure { id = "district", place = "mercury", name = "The dance halls on Mercury Street", cost = 45, fx = new Fx { morale = 18 }, fight = .3f, rolled = .12f, desc = "Music, girls and bad whiskey. Fights start easy and wallets go missing." },
            new Leisure { id = "nell", place = "mercury", name = "Look for Nell Doherty", cost = 0, fx = new Fx { morale = 4 }, desc = "She dances for a dollar a turn and reads at midnight." },
            new Leisure { id = "gardens", place = "gardens", name = "Sundays at Columbia Gardens", cost = 6, fx = new Fx { morale = 14, health = 3 }, seasons = new[] { 0, 1, 2 }, once = true, desc = "The pavilion band, the flower beds and the roller coaster. Nearly free." },
        };

        public static readonly Dictionary<string, string> MINE = new Dictionary<string, string>
        {
            { "anaconda", "the Neversweat" }, { "mountaincon", "the Mountain Con" }, { "speculator", "the Speculator" },
            { "leonard", "the Leonard" }, { "orphangirl", "the Orphan Girl" }, { "original", "the Original" },
        };

        public class Accident { public string text; public int lo, hi; }
        public static readonly Dictionary<string, Accident[]> ACCIDENTS = new Dictionary<string, Accident[]>
        {
            { "anaconda", new[] { new Accident { text = "A slab lets go off the back of the stope and pins your leg under a ton of ore.", lo = 15, hi = 35 }, new Accident { text = "The cage drops two levels before the brakeman catches it. Men are screaming.", lo = 10, hi = 30 } } },
            { "mountaincon", new[] { new Accident { text = "Three thousand feet down the timbers groan and a square-set collapses on the crew.", lo = 20, hi = 40 }, new Accident { text = "Hot water bursts from a drill hole and scalds you to the waist.", lo = 15, hi = 35 } } },
            { "speculator", new[] { new Accident { text = "A missed hole goes off in your face when the next shift drills into it.", lo = 25, hi = 50 }, new Accident { text = "The air on the 2,400 is so bad you go down on your knees and a Finn drags you to the station.", lo = 10, hi = 25 } } },
            { "leonard", new[] { new Accident { text = "The heat takes you. You wake at the shaft station with ice on your chest.", lo = 10, hi = 25 }, new Accident { text = "A chute hangs up and the trammer who clears it isn't fast enough. You carry him out.", lo = 10, hi = 30 } } },
            { "orphangirl", new[] { new Accident { text = "A loose rail throws the ore car and your hand with it.", lo = 8, hi = 20 }, new Accident { text = "Gas in a dead end. You walk out coughing blood.", lo = 10, hi = 25 } } },
        };

        public static readonly Dictionary<string, string> LOC = new Dictionary<string, string>
        {
            { "anaconda", "The Anaconda \"Neversweat,\" Anaconda Road" },
            { "mountaincon", "Mountain Con, Centerville — deepest shaft on the hill" },
            { "speculator", "The Speculator and Granite Mountain shafts, North Butte" },
            { "leonard", "The Leonard Mine, Meaderville" },
            { "orphangirl", "The Orphan Girl, west edge of Uptown" },
            { "original", "The Original Mine" },
            { "hennessy", "Hennessy Building, Granite & Main — Hobart's Consolidated on the sixth floor" },
            { "mm", "M&M Cigar Store, North Main — open since 1890, never locked" },
            { "union", "Butte Miners' Union Hall, North Main" },
            { "mercury", "Mercury Street and Venus Alley — the district" },
            { "gamers", "Gamer's Confectionery, West Park Street" },
            { "library", "Butte Free Public Library, West Broadway" },
            { "stjames", "St. James Hospital" },
            { "gardens", "Columbia Gardens, at the end of the streetcar line" },
            { "finntown", "Finntown, East Broadway — saunas and the Socialist hall" },
            { "gulch", "Dublin Gulch, under the Anaconda stacks" },
            { "centerville", "Centerville — Cousin Jacks and Irish, back to back" },
            { "meaderville", "Meaderville — Italian gardens and the hot mines" },
            { "eastside", "East Park Street — the Serbian boarding houses" },
            { "chinatown", "China Alley, off Mercury Street — Wah Chong Tai & Co." },
            { "courthouse", "Silver Bow County Courthouse, West Granite" },
            { "trestle", "The Milwaukee Road trestle, south of town" },
        };

        public static readonly Dictionary<string, string> GREEN = new Dictionary<string, string>
        {
            { "irish", "young Seamus Cullen" }, { "cornish", "young Tom Penhale" }, { "finnish", "young Aarne Heikkilä" },
            { "italian", "young Gino Ferrari" }, { "serb", "young Petar Babić" }, { "chinese", "young Lee" },
        };

        /// <summary>JS PEOPLE[id].name().</summary>
        public static string PersonName(string id)
        {
            string o = S?.originId ?? "irish";
            switch (id)
            {
                case "landlady":
                    switch (S?.o?.home)
                    {
                        case "centerville": return "Mrs. Pentreath";
                        case "finntown": return "the Widow Mäkinen";
                        case "meaderville": return "Nonna Rossi";
                        case "eastside": return "Mrs. Kovacevich";
                        case "chinatown": return "Old Chin, who keeps the rooms";
                        default: return "Mrs. Hanley";
                    }
                case "teddy": return "Teddy, behind the bar at the M&M";
                case "nell": return "Nell Doherty";
                case "partner":
                    {
                        bool dead = S != null && F.B("partnerDead");
                        var alive = new Dictionary<string, string> { { "irish", "Dinny Shea" }, { "cornish", "Jacka Trethewey" }, { "finnish", "Eino Lahti" }, { "italian", "Beppe Carbone" }, { "serb", "Milan Vukovic" }, { "chinese", "Ah Sam" } };
                        var second = new Dictionary<string, string> { { "irish", "Mick Harrington" }, { "cornish", "Will Rowe" }, { "finnish", "Matti Koski" }, { "italian", "Tony Bianchi" }, { "serb", "Stevo Lalich" }, { "chinese", "Ah Fong" } };
                        return (dead ? second : alive)[o];
                    }
                case "kessler": return "Dutch Kessler";
                case "boss": return S?.bossName ?? "Big Jim Mulcahy";
                case "greenhorn": return GREEN[o];
                case "priest": return "Father Brosnan";
                case "librarian": return "Miss Haskell, the librarian";
                case "sister": return "Sister Mary Agnes";
                case "secretary": return "Secretary Ed Doyle";
                case "wobbly": return "“Red” Tom Quill";
                case "customer": return "A customer";
                case "gentleman": return "A gentleman";
            }
            return id;
        }

        public static readonly StreetPlace[] STREET =
        {
            new StreetPlace { id = "home", shortName = "Home", x = 120, w = 130, h = 110, style = "timber", sub = "home", acts = new[] { "board", "landlady", "sleep" },
                label = () => { switch (S.o.home) { case "centerville": return "Pentreath’s Boarding House"; case "finntown": return "The Big Ship"; case "meaderville": return "Casa Rossi"; case "eastside": return "Kovacevich Boarding House"; case "chinatown": return "Wah Chong Tai & Co."; default: return "Mullin House"; } } },
            new StreetPlace { id = "church", shortName = "Church", x = 320, w = 110, h = 150, style = "stone", spire = true, label = () => "St. Patrick’s", sub = "West Park Street", acts = new[] { "church", "priest" } },
            new StreetPlace { id = "library", shortName = "Library", x = 510, w = 140, h = 120, style = "stone", label = () => "Free Public Library", sub = "West Broadway", acts = new[] { "library", "study", "librarian" } },
            new StreetPlace { id = "gamers", shortName = "Gamer’s", x = 680, w = 100, h = 95, style = "brick", label = () => "Gamer’s Confectionery", sub = "West Park Street", acts = new[] { "gamers", "gossip" } },
            new StreetPlace { id = "hennessy", shortName = "Hennessy’s", x = 870, w = 180, h = 230, style = "brick", label = () => "Hennessy Building", sub = "Granite & Main — post office, hardware counter, the Sixth Floor", acts = new[] { "sendhome", "gun" } },
            new StreetPlace { id = "mm", shortName = "M&M", x = 1070, w = 120, h = 105, style = "brick", label = () => "M&M Cigar Store", sub = "North Main — never closed since 1890", acts = new[] { "drink", "lunch", "faro", "listen", "teddy" } },
            new StreetPlace { id = "union", shortName = "Union Hall", x = 1240, w = 150, h = 150, style = "stone", label = () => "Miners’ Union Hall", sub = "North Main", acts = new[] { "union", "secretary" }, ruin = () => S.year > 1914 || (S.year == 1914 && S.season >= 1) },
            new StreetPlace { id = "stjames", shortName = "St. James", x = 1410, w = 150, h = 140, style = "brick", cross = true, label = () => "St. James Hospital", sub = "the Sisters of Charity", acts = new[] { "hospital", "sister" } },
            new StreetPlace { id = "sauna", shortName = "Sauna", x = 1570, w = 100, h = 85, style = "timber", label = () => "Finntown Sauna", sub = "East Broadway", acts = new[] { "sauna", "finns" } },
            new StreetPlace { id = "mercury", shortName = "Mercury St", x = 1740, w = 160, h = 110, style = "timber", lights = true, label = () => "The Casino", sub = "Mercury Street — the district", acts = new[] { "district", "nell" } },
            new StreetPlace { id = "gardens", shortName = "Gardens", x = 1910, w = 120, h = 60, style = "stop", label = () => "Streetcar to Columbia Gardens", sub = "end of the line", acts = new[] { "gardens" } },
        };
        public const float WORLD = 2050;

        public static readonly Dictionary<string, string[]> PLACE_LINES = new Dictionary<string, string[]>
        {
            { "home", new[] { "The kitchen is warm and smells of onions and coal.", "Somebody is snoring in the back room. Somebody always is." } },
            { "church", new[] { "Candles, and the smell of wet wool. A few women kneel; one man sleeps.", "The spire throws a long shadow down West Park." } },
            { "library", new[] { "Green lamps, a stove, and the quiet of people who work all day in noise.", "Miss Haskell stamps a book without looking up." } },
            { "gamers", new[] { "Steam off the urn and the smell of pasties. The night shift lines up for their dinner pails.", "The counter is warm and loud and cheaper than anywhere on Main." } },
            { "hennessy", new[] { "The brass scale, the post office grille, and a hardware wall nobody asks about.", "Six floors above you, Hobart’s lamp is lit." } },
            { "mm", new[] { "Forty men, a faro bank, a piano that has never been tuned, and a free lunch. The doors have no locks.", "Teddy polishes a glass and watches the room the way a pilot watches water." } },
            { "union", new[] { "Benches full of men with their hats on their knees. The Hall is the biggest north of Denver.", "The banner behind the podium has been mended twice." } },
            { "stjames", new[] { "White sheets, carbolic, and the quiet of a ward at night. The Sisters move like they’re walking on ice.", "The man in the third bed is waiting for his lungs to decide." } },
            { "sauna", new[] { "Steam, birch, and Finns who talk about everything except the mine.", "The stones hiss. You can feel the rock dust leaving." } },
            { "mercury", new[] { "Red lamps, a piano that is trying, and the district’s unwritten rules.", "Birdie’s man counts the girls at midnight." } },
            { "gardens", new[] { "Lanterns in the trees, the pavilion band, and the roller coaster that screams.", "Out here the air smells like grass." } },
        };

        public static StreetPlace FindPlace(string id) => STREET.FirstOrDefault(s => s.id == id);
        public static Leisure FindLeisure(string id) => LEISURE.FirstOrDefault(l => l.id == id);
        public static string MineOf(string jobId) { var j = JOBS.FirstOrDefault(x => x.id == jobId); return j != null && j.loc != null ? MINE[j.loc] : "the hill"; }
        public static void BossFor(Job j) { S.bossName = j.co ? "Big Jim Mulcahy" : j.id == "speculator" ? "Foreman Tom Ahern" : "Foreman Dan Kearns"; }
        public static string PlaceLine(StreetPlace sp) => PLACE_LINES.TryGetValue(sp.id, out var a) ? Pick(a) : "";
        public static string LocFor(StreetPlace sp)
        {
            switch (sp.id)
            {
                case "home": return S.o.home;
                case "church": return "courthouse";
                case "sauna": return "finntown";
                default: return sp.id;
            }
        }
    }
}
