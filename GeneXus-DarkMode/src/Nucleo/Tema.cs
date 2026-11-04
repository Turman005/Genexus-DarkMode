// Temas do GxDarkTheme. "next" e a paleta original do GeneXus Next (identidade: nenhuma cor muda).
// Os outros 7 temas (de Filipe Castro / Xpz.Dark) sao derivados da paleta Next por uma funcao de cor M():
//   - familia de fundo/texto (azulada, pouco saturada) -> matiz/saturacao do tema;
//   - familia de destaque (azul saturado do Next)       -> matiz/saturacao de destaque do tema;
//   - demais cores (vermelho/verde/ambar semanticos)    -> intactas;
//   - a luminancia relativa (WCAG) de cada cor e preservada -> todo contraste medido no Next vale em qualquer tema
//     e nada claro aparece num tema escuro.
// Codigo "puro" (so System.Drawing.Color): compila na DLL (net48) e nos testes do box (.NET 8).
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace GxDarkTheme
{
    public sealed class Tema
    {
        public string Id, Nome, Descricao;
        public double Matiz, Sat, MatizDestaque, SatDestaque;
        public bool IsNext;
        // sintaxe: texto, palavra, controle, tipo, funcao, variavel, string, numero, comentario (null = sintaxe original do GxDarkTheme)
        public Dictionary<string, Color> Sintaxe;
        readonly Dictionary<int, int> cache = new Dictionary<int, int>();
        public override string ToString() { return Nome; }

        // paleta Next de referencia (fundo/texto ~ matiz 216, sat ~0.25; destaque #2C8DCE ~ matiz 204-207, sat ~0.65-0.70)
        const double NextSat = 0.25, NextHueAcc = 207, NextSatAcc = 0.70;

        public Color M(Color c)
        {
            if (IsNext || c.A == 0) return c;
            int k = c.ToArgb() | unchecked((int)0xFF000000);
            int v;
            lock (cache)
            {
                if (!cache.TryGetValue(k, out v)) { v = Calc(Color.FromArgb(k)).ToArgb(); cache[k] = v; }
            }
            Color r = Color.FromArgb(v);
            return Color.FromArgb(c.A, r.R, r.G, r.B);
        }

        public int M(int argb) { return M(Color.FromArgb(argb)).ToArgb(); }

        Color Calc(Color c)
        {
            double h, s, l;
            Cor.ToHsl(c, out h, out s, out l);
            int chroma = Math.Max(c.R, Math.Max(c.G, c.B)) - Math.Min(c.R, Math.Min(c.G, c.B));
            if (chroma < 5) return c;                       // cinza puro / branco / preto: intacto
            double nh, ns;
            if (h >= 190 && h <= 250 && s < 0.40)
            {
                nh = Matiz; ns = Math.Min(0.85, s * Sat / NextSat);
            }
            else if (h >= 190 && h <= 240 && s >= 0.40)
            {
                nh = MatizDestaque + (h - NextHueAcc) * 0.5; ns = Math.Min(1.0, s * SatDestaque / NextSatAcc);
            }
            else return c;                                  // cores semanticas (erro, sucesso, aviso...): intactas
            nh = ((nh % 360) + 360) % 360;
            return Cor.ComLuminancia(nh, ns, Cor.Luminancia(c));
        }

        static readonly Regex Hex = new Regex("#([0-9A-Fa-f]{6})(?![0-9A-Fa-f])", RegexOptions.Compiled);
        // troca todas as cores #RRGGBB de um texto CSS/JS (mantendo maiusculas/minusculas)
        public string MapCss(string css)
        {
            if (IsNext || string.IsNullOrEmpty(css)) return css;
            return Hex.Replace(css, delegate(Match m)
            {
                int x = int.Parse(m.Groups[1].Value, NumberStyles.HexNumber);
                Color o = Color.FromArgb(255, (x >> 16) & 255, (x >> 8) & 255, x & 255);
                Color n = M(o);
                if (n.ToArgb() == o.ToArgb()) return m.Value;
                bool lower = m.Groups[1].Value.ToLowerInvariant() == m.Groups[1].Value && m.Groups[1].Value.ToUpperInvariant() != m.Groups[1].Value;
                string s = n.R.ToString("X2") + n.G.ToString("X2") + n.B.ToString("X2");
                return "#" + (lower ? s.ToLowerInvariant() : s);
            });
        }

        // recolore pixels ARGB (icones) em lugar; alfa preservado
        public void MapPixels(int[] px)
        {
            if (IsNext) return;
            for (int i = 0; i < px.Length; i++)
            {
                int c = px[i];
                int a = (c >> 24) & 255;
                if (a == 0) continue;
                int n = M(c | unchecked((int)0xFF000000));
                px[i] = (a << 24) | (n & 0xFFFFFF);
            }
        }
    }

    public static class Cor
    {
        public static void ToHsl(Color c, out double h, out double s, out double l)
        {
            double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
            double mx = Math.Max(r, Math.Max(g, b)), mn = Math.Min(r, Math.Min(g, b));
            l = (mx + mn) / 2; double d = mx - mn;
            if (d < 1e-9) { h = 0; s = 0; return; }
            s = l > 0.5 ? d / (2 - mx - mn) : d / (mx + mn);
            if (mx == r) h = (g - b) / d + (g < b ? 6 : 0);
            else if (mx == g) h = (b - r) / d + 2;
            else h = (r - g) / d + 4;
            h *= 60;
        }

        public static Color FromHsl(double h, double s, double l)
        {
            double q = l < 0.5 ? l * (1 + s) : l + s - l * s, p = 2 * l - q;
            double hk = h / 360.0;
            return Color.FromArgb(255, B(p, q, hk + 1 / 3.0), B(p, q, hk), B(p, q, hk - 1 / 3.0));
        }
        static int B(double p, double q, double t)
        {
            if (t < 0) t += 1; if (t > 1) t -= 1;
            double v = t < 1 / 6.0 ? p + (q - p) * 6 * t : t < 0.5 ? q : t < 2 / 3.0 ? p + (q - p) * (2 / 3.0 - t) * 6 : p;
            int x = (int)Math.Round(v * 255); return x < 0 ? 0 : x > 255 ? 255 : x;
        }

        static double Lin(int v) { double c = v / 255.0; return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4); }
        public static double Luminancia(Color c) { return 0.2126 * Lin(c.R) + 0.7152 * Lin(c.G) + 0.0722 * Lin(c.B); }
        public static double Contraste(Color a, Color b)
        {
            double x = Luminancia(a), y = Luminancia(b);
            if (x < y) { double t = x; x = y; y = t; }
            return (x + 0.05) / (y + 0.05);
        }

        // cor com matiz/saturacao dadas e a mesma luminancia relativa (busca binaria na luminosidade HSL)
        public static Color ComLuminancia(double h, double s, double alvo)
        {
            double lo = 0, hi = 1; Color best = FromHsl(h, s, 0.5);
            for (int i = 0; i < 40; i++)
            {
                double mid = (lo + hi) / 2;
                Color c = FromHsl(h, s, mid);
                if (Luminancia(c) < alvo) lo = mid; else hi = mid;
            }
            Color a = FromHsl(h, s, lo), b = FromHsl(h, s, hi);
            best = Math.Abs(Luminancia(a) - alvo) <= Math.Abs(Luminancia(b) - alvo) ? a : b;
            // ajuste fino de 1 nivel por canal para chegar o mais perto possivel
            return best;
        }

        public static Color Hex(string hex)
        {
            if (hex.StartsWith("#")) hex = hex.Substring(1);
            int x = int.Parse(hex, NumberStyles.HexNumber);
            return Color.FromArgb(255, (x >> 16) & 255, (x >> 8) & 255, x & 255);
        }
    }

    public static class Temas
    {
        public static readonly List<Tema> Todos = new List<Tema>();
        public static readonly Tema Next;
        public static readonly string[] Tokens = { "texto", "palavra", "controle", "tipo", "funcao", "variavel", "string", "numero", "comentario" };

        static Temas()
        {
            Next = new Tema { Id = "next", Nome = "GeneXus Next", Descricao = "Paleta do GeneXus Next (padrao). Sintaxe estilo VS Code Dark+.", Matiz = 216, Sat = 0.25, MatizDestaque = 207, SatDestaque = 0.70, IsNext = true };
            Todos.Add(Next);
            // temas de Filipe Castro (Xpz.Dark 0.13.6): id, nome, descricao, matiz, sat, matiz de destaque, sat de destaque, sintaxe
            Todos.Add(Novo("ardosia", "Ardósia", "Azul-ardósia, sóbrio. Sintaxe estilo VS Code.", 216, 0.26, 205, 0.70,
                S("D4D4D4", "569CD6", "C586C0", "4EC9B0", "DCDCAA", "9CDCFE", "CE9178", "B5CEA8", "6A9955")));
            Todos.Add(Novo("grafite", "Grafite", "Cinza neutro, sem tom.", 0, 0.0, 207, 0.65,
                S("D4D4D4", "569CD6", "C586C0", "4EC9B0", "DCDCAA", "9CDCFE", "CE9178", "B5CEA8", "6A9955")));
            Todos.Add(Novo("meianoite", "Meia-noite", "Azul-marinho com destaque ciano.", 218, 0.45, 190, 0.75,
                S("D6DEEB", "82AAFF", "C792EA", "7FDBCA", "82AAFF", "ADDB67", "ECC48D", "F78C6C", "7A9C9C")));
            Todos.Add(Novo("ametista", "Ametista", "Quase preto com destaque roxo neon.", 265, 0.22, 270, 0.80,
                S("E2DDF0", "BD93F9", "FF79C6", "8BE9FD", "50FA7B", "F1FA8C", "FFB86C", "BD93F9", "8C84B8")));
            Todos.Add(Novo("brasa", "Brasa", "Escuro quente com destaque vermelho.", 0, 0.10, 4, 0.75,
                S("E6DCDA", "F07178", "FF9E64", "FFCB6B", "82AAFF", "F2C6A0", "C3E88D", "F78C6C", "8A929E")));
            Todos.Add(Novo("ambar", "Âmbar", "Escuro neutro com destaque laranja.", 30, 0.10, 32, 0.80,
                S("D9D7CE", "FFB454", "F29668", "E6B673", "FFD173", "95E6CB", "AAD94C", "D2A6FF", "8A909A")));
            Todos.Add(Novo("oliva", "Oliva", "Verde escuro suave, tons terrosos.", 140, 0.10, 145, 0.55,
                S("D3C6AA", "A7C080", "E69875", "83C092", "DBBC7F", "7FBBB3", "D699B6", "E67E80", "8F9A86")));
        }

        static Tema Novo(string id, string nome, string desc, double matiz, double sat, double acento, double satAc, Dictionary<string, Color> s)
        {
            return new Tema { Id = id, Nome = nome, Descricao = desc, Matiz = matiz, Sat = sat, MatizDestaque = acento, SatDestaque = satAc, Sintaxe = s };
        }

        static Dictionary<string, Color> S(params string[] hex)
        {
            Dictionary<string, Color> d = new Dictionary<string, Color>();
            for (int i = 0; i < Tokens.Length; i++) d[Tokens[i]] = Cor.Hex(hex[i]);
            return d;
        }

        public static Tema Achar(string id)
        {
            if (!string.IsNullOrEmpty(id))
            {
                string k = id.Trim().ToLowerInvariant();
                foreach (Tema t in Todos) if (t.Id == k) return t;
            }
            return Next;
        }

        // estilo do editor (Actipro) -> token de sintaxe dos temas do Filipe; null = mantem a cor original do GxDarkTheme
        public static string TokenDe(string lang, string chave)
        {
            if (lang == "JScript" || lang == "VBScript")
            {
                if (chave == "ReservedWordStyle" || chave == "ReservedWordOperatorStyle") return "palavra";
                if (chave == "NativeTypeStyle") return "tipo";
                if (chave == "PreProcessorDirectiveKeywordStyle") return "controle";
            }
            switch (chave)
            {
                case "DefaultStyle": case "OperatorStyle": case "CodeSnippetDependentFieldStyle":
                case "PropertyDelimiterStyle": case "ValueDelimiterStyle": case "PreProcessorDirectiveDefaultStyle": return "texto";
                case "ReservedWordStyle": return "palavra";
                case "KeyWordStyle": case "TagNameStyle": return "controle";
                case "DefWordStyle": case "ProcessingInstructionDefaultStyle": case "DeclarationDefaultStyle": return "tipo";
                case "FcnReservedWordStyle": case "FcnSomeGenReservedWordStyle": case "ASPDelimiterStyle": return "funcao";
                case "VariableStyle": case "TagAttributeStyle": case "PropertyNameStyle": case "PropertyDefaultStyle": return "variavel";
                case "NumberStyle": case "EntityStyle": case "DeprecatedWordStyle": return "numero";
                case "StringDelimiterStyle": case "StringDefaultStyle": case "TagAttributeValueStyle": case "ValueDefaultStyle": return "string";
                case "CommentDelimiterStyle": case "CommentDefaultStyle": case "CommentURLStyle": return "comentario";
            }
            return null;
        }
    }
}
