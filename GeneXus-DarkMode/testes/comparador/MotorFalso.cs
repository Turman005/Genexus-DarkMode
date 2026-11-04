// Motor falso para o teste do comparador no box (mono/xvfb): so a paleta, o liga/desliga e o log.
// A paleta e calculada como no motor real (Tema.M sobre a paleta Next).
using System; using System.Drawing; using System.Collections.Generic;
namespace GxDarkTheme
{
    public static partial class DarkEngine
    {
        public static volatile bool On = true;
        public static Color Editor = Color.FromArgb(17, 21, 28), Panel = Color.FromArgb(24, 31, 42), Chrome = Color.FromArgb(36, 45, 60), Menu = Color.FromArgb(24, 31, 42),
            Input = Color.FromArgb(31, 39, 52), Hover = Color.FromArgb(52, 72, 96), Button = Color.FromArgb(44, 60, 80), Border = Color.FromArgb(51, 61, 71),
            Accent = Color.FromArgb(44, 141, 206), Sel = Color.FromArgb(44, 60, 80), Fg = Color.FromArgb(220, 226, 235), FgBright = Color.FromArgb(240, 244, 250),
            FgDim = Color.FromArgb(138, 144, 155), FgSoft = Color.FromArgb(190, 198, 210), FgDisabled = Color.FromArgb(150, 157, 168), Link = Color.FromArgb(91, 167, 255),
            HotBg = Color.FromArgb(52, 72, 96), HotBorder = Color.FromArgb(78, 104, 136), MenuHot = Color.FromArgb(31, 78, 121);
        static readonly Color[] paletaNext = { Editor, Panel, Chrome, Menu, Input, Hover, Button, Border, Accent, Sel, Fg, FgBright, FgDim, FgSoft, FgDisabled, Link, HotBg, HotBorder, MenuHot };
        public static Tema TemaAtual = Temas.Next;
        public static void SetTheme(string id)
        {
            TemaAtual = Temas.Achar(id);
            Color[] p = new Color[paletaNext.Length];
            for (int i = 0; i < p.Length; i++) p[i] = TemaAtual.M(paletaNext[i]);
            Editor = p[0]; Panel = p[1]; Chrome = p[2]; Menu = p[3]; Input = p[4]; Hover = p[5]; Button = p[6]; Border = p[7]; Accent = p[8]; Sel = p[9];
            Fg = p[10]; FgBright = p[11]; FgDim = p[12]; FgSoft = p[13]; FgDisabled = p[14]; Link = p[15]; HotBg = p[16]; HotBorder = p[17]; MenuHot = p[18];
        }
        public static readonly List<string> LogLines = new List<string>();
        public static void Log(string m) { LogLines.Add(m); }
        static readonly HashSet<string> once = new HashSet<string>();
        public static void LogOnce(string k, string m) { if (once.Add(k)) Log(m); }
        public static double Lum(Color c) { return 0.299 * c.R + 0.587 * c.G + 0.114 * c.B; }
        public static bool IsDarkText(Color c) { return !c.IsEmpty && c.A > 0 && Lum(c) < 115; }
    }
}
