// Copper Camp — player settings and accessibility options.
// Persisted via PlayerPrefs through the front end so they survive across sessions.
using UnityEngine;

namespace CopperCamp
{
    /// <summary>Accessibility and presentation settings. Stored as a small JSON-ish string in PlayerPrefs.</summary>
    public static class Settings
    {
        const string KEY = "copperCampSettings";

        public static float MusicVolume { get; set; } = 1f;
        public static float SfxVolume { get; set; } = 1f;
        public static int TextSize { get; set; } = 1;       // 0 = small, 1 = normal, 2 = large
        public static bool ReducedMotion { get; set; }
        public static bool Colorblind { get; set; }
        public static bool HighContrast { get; set; }

        public static float TextScale => TextSize == 0 ? .85f : TextSize == 2 ? 1.25f : 1f;

        public static void Load()
        {
            string raw = PlayerPrefs.GetString(KEY, "");
            if (string.IsNullOrEmpty(raw)) return;
            foreach (var pair in raw.Split(';'))
            {
                var kv = pair.Split('=');
                if (kv.Length != 2) continue;
                switch (kv[0])
                {
                    case "music": if (float.TryParse(kv[1], out var mv)) MusicVolume = Mathf.Clamp01(mv); break;
                    case "sfx": if (float.TryParse(kv[1], out var sv)) SfxVolume = Mathf.Clamp01(sv); break;
                    case "text": if (int.TryParse(kv[1], out var ts)) TextSize = Mathf.Clamp(ts, 0, 2); break;
                    case "motion": ReducedMotion = kv[1] == "1"; break;
                    case "colorblind": Colorblind = kv[1] == "1"; break;
                    case "contrast": HighContrast = kv[1] == "1"; break;
                }
            }
        }

        public static void Save()
        {
            PlayerPrefs.SetString(KEY, $"music={MusicVolume.ToString("0.00")};sfx={SfxVolume.ToString("0.00")};text={TextSize};motion={(ReducedMotion ? 1 : 0)};colorblind={(Colorblind ? 1 : 0)};contrast={(HighContrast ? 1 : 0)}");
            PlayerPrefs.Save();
        }
    }
}
