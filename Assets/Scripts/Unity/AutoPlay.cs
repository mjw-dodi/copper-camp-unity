// Copper Camp — a self-test that plays the game through the real front end, the way a cautious player would:
// it works its shifts (letting each one run a moment, then taking the whistle), walks the street, goes into places,
// keeps out of fights and card games and sees the doctor when it is hurt. It writes one console line per screen,
// "CCT|...", so a test harness can follow it, and counts any exception. Added only when the page address has ?autotest.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CopperCamp
{
    public class AutoPlay : MonoBehaviour
    {
        const float STEP = .35f;
        float wait = 3f, shiftAge; ShiftState lastShift; int lastSerial = -1, screens, games, exceptions, walkTries;
        int targetGames = 3; bool walkingThere;
        readonly System.Random rnd = new System.Random(1906);

        void OnEnable() { Application.logMessageReceived += OnLog; }
        void OnDisable() { Application.logMessageReceived -= OnLog; }
        void OnLog(string msg, string stack, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Error) return;
            if (msg.StartsWith("CCT|") || msg.Contains("Shader") || msg.Contains("shader")) return;
            exceptions++;
            Debug.Log("CCT|EXC|" + msg.Replace("\n", " ") + " @ " + (stack ?? "").Split('\n').FirstOrDefault());
        }

        // ?autotest&rooms: start a game, then show each interior in turn for a few seconds (for screenshots)
        bool roomTour => Application.absoluteURL != null && Application.absoluteURL.Contains("rooms");
        List<string> rooms; int roomIdx = -1; float roomClock;
        void RoomTour()
        {
            if (CC.S == null || CC.TitleMode) return;
            if (rooms == null) rooms = CC.INTERIOR.Keys.Where(k => k != "dry").ToList();
            roomClock -= Time.unscaledDeltaTime;
            if (roomClock > 0) return;
            roomClock = 6f; roomIdx++;
            if (roomIdx >= rooms.Count) { Debug.Log("CCT|ROOMSDONE"); enabled = false; return; }
            CC.scene.mode = "inside"; CC.scene.inside = rooms[roomIdx]; CC.scene.season = CC.S.season; CC.scene.year = CC.S.year;
            Debug.Log("CCT|ROOM|" + rooms[roomIdx]);
        }

        void Update()
        {
            var ui = GameUI.Instance; if (ui == null) return;
            if (roomTour && CC.S != null && !CC.TitleMode) { RoomTour(); return; }
            if (ui.Serial != lastSerial)
            {
                lastSerial = ui.Serial; screens++;
                var S = CC.S; var sc = CC.scene;
                Debug.Log("CCT|" + screens + "|" + (S != null ? CC.DateStr() : "-") + "|" + sc.mode + "|" + (sc.inside ?? "") + "|" + Title(ui) +
                          (S != null ? "|$" + S.money + " h" + Mathf.RoundToInt(S.health) + " l" + Mathf.RoundToInt(S.lungs) + " m" + Mathf.RoundToInt(S.morale) : ""));
            }
            wait -= Time.unscaledDeltaTime;
            if (wait > 0) return;
            wait = STEP;
            try { Act(ui); } catch (Exception e) { exceptions++; Debug.Log("CCT|EXC|autoplay " + e.Message); }
        }

        static string Title(GameUI ui)
        {
            var c = ui.CurrentChoices; return c != null && c.Count > 0 ? "[" + string.Join(" / ", c.Take(4).Select(x => x.t)) + "]" : "[]";
        }

        void Act(GameUI ui)
        {
            var S = CC.S;
            // a shift: let it run a moment so the mine view draws, then take the whistle
            if (CC.Shift != null && !CC.Shift.paused)
            {
                if (CC.Shift != lastShift) { lastShift = CC.Shift; shiftAge = 0; }
                shiftAge += STEP;
                if (shiftAge > 2.5f && !CC.Shift.over) CC.ShiftSkip();
                return;
            }
            if (CC.PendingAfterShift != null) return;

            var choices = ui.CurrentChoices.Where(c => !c.disabled && c.go != null).ToList();

            // the end of a life: report it, then start the next one
            if (S != null && S.ended)
            {
                var end = choices.FirstOrDefault(c => c.t == "Start again");
                Debug.Log("CCT|END|" + CC.DateStr() + "|turn " + (CC.TurnIdx() + 1) + "/48|screens " + screens + "|exceptions " + exceptions);
                games++;
                if (games >= targetGames || (S.year >= 1917 && S.season >= 3)) { Debug.Log("CCT|DONE|games " + games + "|exceptions " + exceptions); enabled = false; return; }
                end?.go(); wait = 2f; return;
            }
            if (CC.TitleMode)
            {
                var origin = CC.ORIGINS[games % CC.ORIGINS.Length];
                var oc = choices.FirstOrDefault(c => c.t.Contains(origin.name) && !c.t.StartsWith("Step off"));
                var start = choices.FirstOrDefault(c => c.t.StartsWith("Step off"));
                if (start != null && lastPickedOrigin == origin.name) { lastPickedOrigin = null; start.go(); wait = 2f; return; }
                if (oc != null) { lastPickedOrigin = origin.name; oc.go(); return; }
            }
            // the street: walk somewhere, now and then go in
            var sc = CC.scene;
            if (sc.mode == "walk" && sc.walk != null && !sc.walk.inside)
            {
                if (walkingThere) { if (sc.walk.target >= 0) return; walkingThere = false; wait = 1.2f; return; }
                if (walkTries < 2 && rnd.NextDouble() < .7)
                {
                    var go = choices.FirstOrDefault(c => c.t == "Go inside" || c.t.StartsWith("See what"));
                    if (go != null && walkTries > 0 && rnd.NextDouble() < .75) { walkTries = 0; go.go(); wait = 1.5f; return; }
                    var dests = CC.WalkDestinations();
                    if (dests.Count > 0) { CC.WalkTo(dests[rnd.Next(dests.Count)].id); walkingThere = true; walkTries++; return; }
                }
            }
            else walkTries = 0;
            if (choices.Count == 0) return;   // a timed screen decides for itself
            var best = choices.OrderByDescending(Score).First();
            best.go();
            wait = sc.mode == "inside" ? .9f : STEP;
        }
        string lastPickedOrigin;

        float Score(Choice c)
        {
            var t = (c.t ?? "").ToLowerInvariant(); var S = CC.S; float s = (float)rnd.NextDouble();
            string[] bad = { "take the train", "fight", "faro", "gun", "push for the contract", "dance hall", "draw", "swing first", "slots", "start again" };
            foreach (var b in bad) if (t.Contains(b)) s -= 5;
            string[] good = { "take it slow", "mucker", "timberman", "machine man", "hot box", "trammer", "eat well", "turn in", "next season", "go on", "continue", "walk home", "stay home",
                              "laugh it off", "mark it", "share the pail", "walk on", "keep", "work another shift", "go inside" };
            foreach (var g in good) if (t.Contains(g)) s += 2;
            // try each job in turn so every mine view gets drawn
            if (S != null && t.Contains(new[] { "mucker", "timberman", "machine man", "hot box", "trammer" }[(S.shifts + S.year) % 5])) s += 1.5f;
            if (S != null && S.health < 45 && (t.Contains("st. james") || t.Contains("doctor") || t.Contains("rest"))) s += 6;
            if (S != null && S.morale < 30 && (t.Contains("drink") || t.Contains("church") || t.Contains("sauna"))) s += 3;
            if (c.cost != null && S != null && c.cost > S.money * .4f) s -= 4;
            return s;
        }
    }
}
