// Copper Camp — save format: one "key<TAB>value" pair per line. Simple, readable and stable across versions.
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CopperCamp
{
    public static class SaveCodec
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        static string Esc(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\t", "\\t").Replace("\n", "\\n").Replace("\r", "");
        static string Unesc(string s)
        {
            var b = new StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '\\' && i + 1 < s.Length)
                {
                    char c = s[++i];
                    b.Append(c == 't' ? '\t' : c == 'n' ? '\n' : c);
                }
                else b.Append(s[i]);
            }
            return b.ToString();
        }

        public static string Encode(GameState S)
        {
            var b = new StringBuilder();
            void W(string k, object v) { b.Append(k).Append('\t').Append(Esc(System.Convert.ToString(v, Inv))).Append('\n'); }
            W("v", 1);
            W("name", S.name); W("o", S.originId); W("year", S.year); W("season", S.season);
            W("health", S.health); W("lungs", S.lungs); W("morale", S.morale); W("money", S.money);
            W("rep", S.rep); W("heat", S.heat); W("skill", S.skill);
            W("card", S.card ? 1 : 0); W("blacklisted", S.blacklisted ? 1 : 0); W("strike", S.strike ? 1 : 0); W("halfpay", S.halfpay ? 1 : 0);
            W("sent", S.sent); W("fights", S.fights); W("kills", S.kills);
            W("job", S.jobId ?? ""); W("phase", S.phase); W("shifts", S.shifts); W("slots", S.slots); W("seasonPay", S.seasonPay);
            W("tip", S.tip ?? ""); W("tipTurn", S.tipTurn); W("payTip", S.payTip ?? ""); W("payTipTurn", S.payTipTurn);
            W("bossName", S.bossName ?? ""); W("pendingPlace", S.pendingPlace ?? "");
            foreach (var l in S.log) W("log", l);
            foreach (var kv in S.flags.d) W("f:" + kv.Key, kv.Value);
            foreach (var kv in S.rel) W("r:" + kv.Key, kv.Value);
            foreach (var u in S.used) W("u", u);
            foreach (var kv in S.ev) W("e:" + kv.Key, kv.Value);
            foreach (var k in S.brkRecent) W("b", k);
            return b.ToString();
        }

        public static GameState Decode(string text)
        {
            var S = new GameState();
            float Fl(string v) => float.Parse(v, Inv);
            int In(string v) => int.Parse(v, Inv);
            string Ns(string v) => v == "" ? null : v;
            foreach (var raw in text.Split('\n'))
            {
                if (raw.Length == 0) continue;
                int tab = raw.IndexOf('\t');
                if (tab < 0) continue;
                string k = raw.Substring(0, tab), v = Unesc(raw.Substring(tab + 1));
                if (k.StartsWith("f:")) { S.flags.d[k.Substring(2)] = v; continue; }
                if (k.StartsWith("r:")) { S.rel[k.Substring(2)] = In(v); continue; }
                if (k.StartsWith("e:")) { S.ev[In(k.Substring(2))] = In(v); continue; }
                switch (k)
                {
                    case "name": S.name = v; break;
                    case "o": S.originId = v; break;
                    case "year": S.year = In(v); break;
                    case "season": S.season = In(v); break;
                    case "health": S.health = Fl(v); break;
                    case "lungs": S.lungs = Fl(v); break;
                    case "morale": S.morale = Fl(v); break;
                    case "money": S.money = In(v); break;
                    case "rep": S.rep = Fl(v); break;
                    case "heat": S.heat = Fl(v); break;
                    case "skill": S.skill = Fl(v); break;
                    case "card": S.card = v == "1"; break;
                    case "blacklisted": S.blacklisted = v == "1"; break;
                    case "strike": S.strike = v == "1"; break;
                    case "halfpay": S.halfpay = v == "1"; break;
                    case "sent": S.sent = In(v); break;
                    case "fights": S.fights = In(v); break;
                    case "kills": S.kills = In(v); break;
                    case "job": S.jobId = Ns(v); break;
                    case "phase": S.phase = v; break;
                    case "shifts": S.shifts = In(v); break;
                    case "slots": S.slots = In(v); break;
                    case "seasonPay": S.seasonPay = In(v); break;
                    case "tip": S.tip = Ns(v); break;
                    case "tipTurn": S.tipTurn = In(v); break;
                    case "payTip": S.payTip = Ns(v); break;
                    case "payTipTurn": S.payTipTurn = In(v); break;
                    case "bossName": S.bossName = v; break;
                    case "pendingPlace": S.pendingPlace = Ns(v); break;
                    case "log": S.log.Add(v); break;
                    case "u": S.used.Add(v); break;
                    case "b": S.brkRecent.Add(v); break;
                }
            }
            return S;
        }
    }
}
