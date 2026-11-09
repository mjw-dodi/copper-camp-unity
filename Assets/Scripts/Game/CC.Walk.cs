// Copper Camp — walking Uptown (JS walkInit, walkPhase, walkStep, walkUI, walkTo). The 3D street reads scene.walk.
using System;
using System.Collections.Generic;
using System.Linq;

namespace CopperCamp
{
    public class Npc { public float x, v, speed, ph; public int lane; public int coat; public bool skirt; }

    public partial class WalkState
    {
        public int dir = 1;
        public bool moving;
        public string near = "\u0001";   // "undefined" in the JS: forces the next WalkStep to refresh the panel
        public int btn;                  // -1 / 0 / 1 from the on-screen West/East buttons or the keyboard
        public List<Npc> npcs = new List<Npc>();
    }

    public static partial class CC
    {
        /// <summary>JS rnd(seed): the seeded LCG used for street NPCs and filler shops.</summary>
        public static Func<float> Rnd(int seed)
        {
            uint s = (uint)seed; if (s == 0) s = 1;
            return () => { s = unchecked(s * 1664525u + 1013904223u); return s / 4294967296f; };
        }

        static void WalkInit()
        {
            var r = Rnd(S.year * 7 + S.season);
            var w = new WalkState { px = STREET[0].x, target = -1, seasonKey = Key() };
            for (int i = 0; i < 9; i++)
                w.npcs.Add(new Npc { x = 200 + r() * (WORLD - 400), v = (r() < .5f ? -1 : 1) * (.6f + r() * .9f), speed = .7f + r() * .6f, ph = r() * 6, lane = (int)Math.Floor(r() * 3) * 6, coat = i % 5, skirt = r() < .3f });
            scene.walk = w;
        }

        public static void WalkPhase()
        {
            S.phase = "leisure";
            if (scene.walk == null || scene.walk.seasonKey != Key() || scene.walk.npcs.Count == 0) WalkInit();
            scene.dark = true; scene.season = S.season; scene.year = S.year;
            Save();
            if (scene.walk.inside && scene.inside != null)
            {
                var sp = FindPlace(scene.inside);
                if (sp != null) { InsideMenu(sp); return; }
            }
            scene.walk.inside = false;
            scene.inside = null;
            scene.mode = "walk";
            scene.walk.near = "\u0001";
            WalkStep();
        }

        /// <summary>The place the player is standing at, or null.</summary>
        public static StreetPlace PlaceAt(float px) => STREET.FirstOrDefault(s => Math.Abs(s.x - px) < s.w / 2 + 26);

        /// <summary>One 60 Hz frame of walking (JS walkStep). keyDx is -1/0/1 from the keyboard.</summary>
        public static void WalkStep(int keyDx = 0)
        {
            var Wk = scene.walk;
            if (Wk == null || scene.mode != "walk") return;
            int dx = keyDx != 0 ? keyDx : Wk.btn; float sp = 4;
            if (dx != 0) Wk.target = -1;
            else if (Wk.target >= 0)
            {
                float d = Wk.target - Wk.px;
                if (Math.Abs(d) <= 8) { Wk.px = Wk.target; Wk.target = -1; Wk.near = "\u0001"; }
                else { dx = d > 0 ? 1 : -1; sp = 8; }
            }
            Wk.moving = dx != 0;
            if (dx != 0) { Wk.dir = dx; Wk.px = Clamp(Wk.px + dx * sp, 30, WORLD - 30); }
            foreach (var n in Wk.npcs)
            {
                n.x += n.v * n.speed;
                if (n.x < 60 || n.x > WORLD - 60) n.v = -n.v;
            }
            var pl = PlaceAt(Wk.px);
            string id = pl?.id;
            Wk.at = id;
            if (id != Wk.near) { Wk.near = id; if (Wk.target < 0) WalkUI(pl); }
        }

        public static void WalkTo(string id)
        {
            var sp = FindPlace(id);
            if (sp != null && scene.walk != null) scene.walk.target = sp.x;
        }

        /// <summary>Places that have something on offer this season (the JS "Walk to:" chips).</summary>
        public static List<StreetPlace> WalkDestinations() => STREET.Where(s => s.acts.Any(a => LeisureFor(a) != null)).ToList();

        static void WalkUI(StreetPlace pl)
        {
            string name = pl?.Label;
            string body = (pl != null ? "" : "<p>" + WalkIntro() + "</p>")
                + (pl != null ? "<p><b>" + name + "</b><br><span class=\"note\">" + pl.sub + "</span></p>" : "<p class=\"note\">Walk up to a door, or pick a place below.</p>")
                + "<p class=\"note\">" + SlotsLine() + "</p>";
            scene.caption = pl != null ? name + " — " + pl.sub : "Uptown Butte — Park Street and Main";
            var ch = new List<Choice>();
            if (pl != null)
            {
                if (pl.IsRuin) body += "<p class=\"note\">Bricks and a sign nobody has taken down. The union is finished.</p>";
                else if (pl.acts.Any(a => LeisureFor(a) != null))
                {
                    var names = pl.acts.Select(LeisureFor).Where(f => f != null).Take(4)
                        .Select(f => System.Text.RegularExpressions.Regex.Replace(f.l.name, "^(The |Talk to |Talk with )", ""));
                    var here = pl;
                    ch.Add(Ch(INTERIOR.ContainsKey(pl.id) ? "Go inside" : "See what’s on", string.Join(" · ", names), () => { Sfx("door"); EnterPlace(here); }));
                }
            }
            ch.Add(Ch("Turn in for the night (ends the evening)", null, () => { var f = LeisureFor("sleep"); DoLeisure(f.l, 0); }));
            Render("Off shift", body, ch, new Opts { keepScene = true });
        }

        /// <summary>Kessler's man follows you on the street (JS: year >= 1909 && heat > 40).</summary>
        public static bool KesslerTail => S != null && S.year >= 1909 && S.heat > 40;
    }
}
