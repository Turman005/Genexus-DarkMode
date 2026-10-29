// Xpz.Dark - conta de cor: HSL, contraste, ajuste de cor de sintaxe para fundo escuro.
// Sem dependencia de biblioteca de terceiros: a tela de configuracao e os testes usam isto.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace Xpz.Dark
{
    /// <summary>Ajuste de cor de sintaxe para fundo escuro. Sem nada da Actipro:
    /// a previa da tela de configuracao e o teste fora do GeneXus usam isto.</summary>
    internal static class Ajuste
    {
        /// <summary>Fundo de estilo: branco some (fica o da area de texto); claro colorido
        /// (ex.: realce amarelo de busca) vira a versao escura do mesmo matiz.</summary>
        internal static Color FundoDoEstilo(Color b)
        {
            if (b.IsEmpty || b.A < 255) return b;
            if (Paleta.Lum(b) < 0.40) return b;                                   // ja escuro
            if (Paleta.Croma(b) < 0.10) return Color.Empty;                        // branco/cinza claro
            double h, s, l; Hsl(b, out h, out s, out l);
            return DeHsl(h, Math.Min(s, 0.55), 0.24);
        }

        /// <summary>Texto de estilo: neutro escuro vira o texto claro; colorido mantem o
        /// matiz e clareia ate o contraste minimo.</summary>
        internal static Color TextoDoEstilo(Color f, string chave, Color fundo)
        {
            return Ajustar(f, chave, fundo, Paleta.Texto, Paleta.ContrasteCodigo);
        }

        /// <summary>Tambem usado pela previa da tela de configuracao, com valores ainda nao salvos.</summary>
        internal static Color Ajustar(Color f, string chave, Color fundo, Color textoClaro, double contraste)
        {
            if (f.IsEmpty)
                return chave == "DefaultStyle" ? textoClaro : f;
            if (Paleta.Croma(f) < 0.12)
            {
                if (Paleta.Contraste(f, fundo) >= contraste) return f;
                return Paleta.Lum(f) < 0.35 ? textoClaro : Paleta.Misturar(textoClaro, fundo, 0.25);
            }
            if (Paleta.Contraste(f, fundo) >= contraste) return f;
            double h, s, l; Hsl(f, out h, out s, out l);
            s = Math.Min(s, 0.60);                         // tira o neon: paletas escuras usam tons suaves
            for (double nl = Math.Max(l, 0.58); nl <= 0.95; nl += 0.02)
            {
                Color c = DeHsl(h, s, nl);
                if (Paleta.Contraste(c, fundo) >= contraste) return c;
            }
            return DeHsl(h, s, 0.95);
        }

        // ------------------------------------------------------------ HSL
        internal static void Hsl(Color c, out double h, out double s, out double l)
        {
            double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
            l = (max + min) / 2;
            if (max == min) { h = 0; s = 0; return; }
            double d = max - min;
            s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
            if (max == r) h = (g - b) / d + (g < b ? 6 : 0);
            else if (max == g) h = (b - r) / d + 2;
            else h = (r - g) / d + 4;
            h /= 6;
        }

        internal static Color DeHsl(double h, double s, double l)
        {
            double r, g, b;
            if (s == 0) { r = g = b = l; }
            else
            {
                double q = l < 0.5 ? l * (1 + s) : l + s - l * s, p = 2 * l - q;
                r = Canal(p, q, h + 1.0 / 3); g = Canal(p, q, h); b = Canal(p, q, h - 1.0 / 3);
            }
            return Color.FromArgb((int)Math.Round(r * 255), (int)Math.Round(g * 255), (int)Math.Round(b * 255));
        }

        private static double Canal(double p, double q, double t)
        {
            if (t < 0) t += 1; if (t > 1) t -= 1;
            if (t < 1.0 / 6) return p + (q - p) * 6 * t;
            if (t < 0.5) return q;
            if (t < 2.0 / 3) return p + (q - p) * (2.0 / 3 - t) * 6;
            return p;
        }
    }
}
