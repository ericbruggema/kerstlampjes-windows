using System;
using System.Globalization;
using System.IO;

namespace Kerstlampjes
{
    /// <summary>Alle instellingen van de lampjes (zelfde als in de Android-app), opgeslagen in %APPDATA%.</summary>
    public class Settings
    {
        public int Size = 14;        // grootte lampje (px bij 100% schaal)
        public int Spacing = 44;     // afstand tussen lampjes
        public int Speed = 5;        // 1..10
        public int Mode = 1;         // zie Modes
        public int Palette = 0;      // zie Palettes
        public int Corner = 28;      // afronding van de hoeken
        public int Inset = 4;        // afstand van de rand
        public int Opacity = 100;    // 30..100 %
        public int Sag = 6;          // doorhang van de draad
        public bool On = true;
        public bool AllScreens = true;
        public bool HideFullscreen = true;

        public Settings Clone() { return (Settings)MemberwiseClone(); }

        static string FilePath
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Kerstlampjes", "settings.ini");
            }
        }

        static int Clamp(string v, int min, int max, int def)
        {
            int n;
            if (!int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out n)) return def;
            return Math.Max(min, Math.Min(max, n));
        }

        public static Settings Load()
        {
            var s = new Settings();
            try
            {
                foreach (var line in File.ReadAllLines(FilePath))
                {
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string k = line.Substring(0, eq).Trim(), v = line.Substring(eq + 1).Trim();
                    switch (k)
                    {
                        case "size": s.Size = Clamp(v, 6, 40, s.Size); break;
                        case "spacing": s.Spacing = Clamp(v, 20, 120, s.Spacing); break;
                        case "speed": s.Speed = Clamp(v, 1, 10, s.Speed); break;
                        case "mode": s.Mode = Clamp(v, 0, Modes.Count - 1, s.Mode); break;
                        case "palette": s.Palette = Clamp(v, 0, Palettes.Colors.Length - 1, s.Palette); break;
                        case "corner": s.Corner = Clamp(v, 0, 80, s.Corner); break;
                        case "inset": s.Inset = Clamp(v, 0, 40, s.Inset); break;
                        case "opacity": s.Opacity = Clamp(v, 30, 100, s.Opacity); break;
                        case "sag": s.Sag = Clamp(v, 0, 20, s.Sag); break;
                        case "on": s.On = v != "0"; break;
                        case "allscreens": s.AllScreens = v != "0"; break;
                        case "hidefullscreen": s.HideFullscreen = v != "0"; break;
                    }
                }
            }
            catch { /* geen bestand of kapot: standaardwaarden */ }
            return s;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllLines(FilePath, new[]
                {
                    "size=" + Size, "spacing=" + Spacing, "speed=" + Speed, "mode=" + Mode,
                    "palette=" + Palette, "corner=" + Corner, "inset=" + Inset, "opacity=" + Opacity,
                    "sag=" + Sag, "on=" + (On ? 1 : 0), "allscreens=" + (AllScreens ? 1 : 0),
                    "hidefullscreen=" + (HideFullscreen ? 1 : 0)
                });
            }
            catch { /* niet kunnen opslaan is niet erg */ }
        }
    }

    public static class Modes
    {
        public const int Steady = 0, Twinkle = 1, Alternate = 2, Chase = 3, Wave = 4;
        public const int Count = 5;
        public static readonly string[] Names =
            { "Vast aan", "Fonkelen", "Om en om knipperen", "Lopend licht", "Kleurgolf" };
    }

    public static class Palettes
    {
        public static readonly string[] Names =
            { "Klassiek bont", "Warm wit", "Rood / groen / goud", "IJsblauw / wit", "Roze / paars" };

        public static readonly int[][] Colors =
        {
            new[] { unchecked((int)0xFFFF3B30), unchecked((int)0xFFFFD54F), unchecked((int)0xFF4CD964), unchecked((int)0xFF40A9FF), unchecked((int)0xFFFF9500), unchecked((int)0xFFFF4FD8) },
            new[] { unchecked((int)0xFFFFE3A8), unchecked((int)0xFFFFD27F), unchecked((int)0xFFFFEBC4) },
            new[] { unchecked((int)0xFFFF3B30), unchecked((int)0xFF34C759), unchecked((int)0xFFFFC94A) },
            new[] { unchecked((int)0xFF7FD4FF), unchecked((int)0xFFFFFFFF), unchecked((int)0xFF3D7BFF) },
            new[] { unchecked((int)0xFFFF6FB5), unchecked((int)0xFFB36BFF), unchecked((int)0xFFFFC2E2) }
        };
    }
}
