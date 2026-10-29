// Xpz.Dark - PALETA: as cores em uso e os nomes por funcao (o sistema de cores).

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
    internal static class Paleta
    {
        // Harmoniza com o tema-escuro.isl (fundos de 28 a 46). As seis cores base
        // vem do dark.ini (tela de configuracao); as demais derivam delas.
        public static Color FundoCodigo  = Color.FromArgb(30, 30, 30);   // editor de codigo
        public static Color FundoEntrada = Color.FromArgb(30, 30, 30);   // caixa de texto, lista, grade
        public static Color FundoPainel  = Color.FromArgb(37, 37, 38);   // painel, dialogo, form
        public static Color FundoElevado = Color.FromArgb(55, 55, 59);   // botao
        public static Color Borda        = Color.FromArgb(70, 70, 76);
        public static Color Linha        = Color.FromArgb(50, 50, 54);   // linhas de grade/PropertyGrid
        public static Color Texto        = Color.FromArgb(222, 222, 222);
        public static Color TextoApagado = Color.FromArgb(150, 150, 150);
        public static Color TextoInativo = Color.FromArgb(175, 175, 175); // aba nao selecionada: legivel, mas recuado
        public static Color Selecao      = Color.FromArgb(38, 79, 120);
        public static Color Link         = Color.FromArgb(86, 156, 214);
        public static Color Realce       = Color.FromArgb(62, 62, 66);   // hover de menu
        public static Color Pressionado   = Color.FromArgb(44, 88, 132);
        public static Color BordaDestaque = Color.FromArgb(60, 110, 160);
        public static double ContrasteCodigo = 4.5;                     // WCAG AA; 7 = alto contraste

        // ------------------------------------------------------------------
        // NOMES POR FUNCAO (o "sistema de cores"). Quem desenha usa ESTES nomes, nao
        // contas soltas ("fundo + 8"): o tema decide os valores num lugar so, e o teste
        // de contraste confere todos os temas (testes\TesteTokens.cs).
        //   superficies, do mais fundo ao mais elevado:
        public static Color Superficie0 { get { return FundoCodigo; } }    // editor, faixa de abas
        public static Color Superficie1 { get { return FundoEntrada; } }   // campos, listas, grades
        public static Color Superficie2 { get { return FundoPainel; } }    // paineis, dialogos
        public static Color Superficie3 { get { return FundoElevado; } }   // botoes, aba ativa
        //   texto:
        public static Color TextoPrimario { get { return Texto; } }
        public static Color TextoSecundario { get { return TextoInativo; } }   // aba inativa, rotulo secundario
        public static Color TextoDesabilitado { get { return TextoApagado; } }
        //   interacao:
        public static Color Destaque { get { return Link; } }              // aba ativa, foco, link
        public static Color Hover { get { return Realce; } }
        public static Color Divisor { get { return Linha; } }
        //   componentes (derivados no DeTema/Carregar):
        public static Color RolagemTrilho, RolagemPolegar, RolagemPolegarAtivo, CabecalhoGrupo, SelecaoSemFoco;

        private static void Derivados()
        {
            RolagemTrilho       = Somar(FundoCodigo, 8);
            RolagemPolegar      = Somar(FundoCodigo, 50);
            RolagemPolegarAtivo = Somar(FundoCodigo, 90);
            CabecalhoGrupo      = Somar(FundoPainel, 10);
            SelecaoSemFoco      = Somar(FundoEntrada, 25);
        }

        /// <summary>Tema em uso (moldura, janelas, sintaxe).</summary>
        public static Tema TemaAtual = Temas.Achar(Temas.Padrao);
        static Paleta() { Derivados(); }
        /// <summary>Cores proprias da tela de configuracao (Tema = Personalizado).</summary>
        public static bool Personalizado;

        public static void Carregar(Dictionary<string, string> cfg)
        {
            if (cfg == null) return;
            Personalizado = string.Equals(Config.Valor(cfg, "Tema"), "Personalizado", StringComparison.OrdinalIgnoreCase);
            DeTema(Temas.DoConfig(cfg));
            if (Personalizado)
            {
                FundoCodigo  = Ler(cfg, "CorFundoCodigo", FundoCodigo);
                FundoEntrada = Ler(cfg, "CorFundoEntrada", FundoEntrada);
                FundoPainel  = Ler(cfg, "CorFundoPainel", FundoPainel);
                Texto        = Ler(cfg, "CorTexto", Texto);
                Selecao      = Ler(cfg, "CorSelecao", Selecao);
                Link         = Ler(cfg, "CorLink", Link);
                FundoElevado = Somar(FundoPainel, 18);
                Borda        = Somar(FundoPainel, 33);
                Linha        = Somar(FundoPainel, 13);
                Realce       = Somar(FundoPainel, 25);
                Pressionado   = Somar(Selecao, 10);
                BordaDestaque = Somar(Selecao, 45);
                TextoApagado = Misturar(Texto, FundoPainel, 0.38);
                TextoInativo = Misturar(Texto, FundoPainel, 0.22);
                Derivados();
            }
            double k;
            string v = Config.Valor(cfg, "EditorContraste");
            if (v != null && double.TryParse(v.Replace(',', '.'), System.Globalization.NumberStyles.Float,
                                             System.Globalization.CultureInfo.InvariantCulture, out k) && k >= 3 && k <= 12)
                ContrasteCodigo = k;
        }

        /// <summary>Todas as cores das janelas a partir de um tema: rampa do tom do tema
        /// para os fundos, destaque para selecao/hover/link, texto levemente tingido.</summary>
        public static void DeTema(Tema t)
        {
            if (t == null) return;
            TemaAtual = t;
            FundoCodigo  = t.Fundo(0.085);
            FundoEntrada = t.Fundo(0.105);
            FundoPainel  = t.Fundo(0.130);
            Linha        = t.Fundo(0.170);
            FundoElevado = t.Fundo(0.200);
            Borda        = t.Fundo(0.250);
            Realce        = t.Hover;
            Selecao       = t.Selecionado;
            Pressionado   = t.Pressionado;
            BordaDestaque = t.BordaDestaque;
            Link         = t.Destaque(Math.Min(0.9, t.SatAcento + 0.1), 0.68);
            Texto        = Ajuste.DeHsl(t.Matiz / 360.0, t.Saturacao * 0.35, 0.89);
            TextoApagado = Misturar(Texto, FundoPainel, 0.38);
            TextoInativo = Misturar(Texto, FundoPainel, 0.22);
            Derivados();
        }

        /// <summary>As seis cores-base do tema, na ordem das chaves Cor* do dark.ini,
        /// sem mexer na paleta em uso (para a tela de configuracao).</summary>
        public static Color[] SeisDoTema(Tema t)
        {
            return new[] { t.Fundo(0.085), t.Fundo(0.105), t.Fundo(0.130), Ajuste.DeHsl(t.Matiz / 360.0, t.Saturacao * 0.35, 0.89),
                           t.Selecionado, t.Destaque(Math.Min(0.9, t.SatAcento + 0.1), 0.68) };
        }

        /// <summary>As cores-chave atuais, na ordem fixa (para trocar tema ao vivo).</summary>
        public static Color[] Fotografia()
        {
            return new[] { FundoPainel, FundoEntrada, FundoCodigo, FundoElevado, Borda, Linha, Realce, Selecao, Link, Texto, TextoApagado, TextoInativo, Pressionado, BordaDestaque };
        }

        /// <summary>"R, G, B" ou "#RRGGBB". Valor invalido = padrao.</summary>
        public static Color Ler(Dictionary<string, string> cfg, string chave, Color padrao)
        {
            Color c;
            return TentarCor(Config.Valor(cfg, chave), out c) ? c : padrao;
        }

        public static bool TentarCor(string v, out Color c)
        {
            c = Color.Empty;
            if (string.IsNullOrEmpty(v)) return false;
            v = v.Trim();
            try
            {
                if (v.StartsWith("#") && v.Length == 7)
                {
                    c = Color.FromArgb(Convert.ToInt32(v.Substring(1, 2), 16), Convert.ToInt32(v.Substring(3, 2), 16), Convert.ToInt32(v.Substring(5, 2), 16));
                    return true;
                }
                string[] p = v.Split(',');
                if (p.Length != 3) return false;
                c = Color.FromArgb(int.Parse(p[0].Trim()), int.Parse(p[1].Trim()), int.Parse(p[2].Trim()));
                return true;
            }
            catch { return false; }
        }

        public static string Texto3(Color c) { return c.R + ", " + c.G + ", " + c.B; }

        private static int B(int v) { return v < 0 ? 0 : v > 255 ? 255 : v; }
        public static Color Somar(Color c, int d) { return Color.FromArgb(B(c.R + d), B(c.G + d), B(c.B + d)); }
        public static Color Misturar(Color a, Color b, double t)
        {
            return Color.FromArgb(B((int)Math.Round(a.R + (b.R - a.R) * t)), B((int)Math.Round(a.G + (b.G - a.G) * t)), B((int)Math.Round(a.B + (b.B - a.B) * t)));
        }

        /// <summary>Contraste WCAG entre duas cores (1 a 21).</summary>
        public static double Contraste(Color a, Color b)
        {
            double la = LumRel(a), lb = LumRel(b);
            return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
        }

        private static double Lin(int v) { double s = v / 255.0; return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4); }
        public static double LumRel(Color c) { return 0.2126 * Lin(c.R) + 0.7152 * Lin(c.G) + 0.0722 * Lin(c.B); }

        public static double Lum(Color c) { return (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255.0; }
        public static double Croma(Color c) { return (Math.Max(c.R, Math.Max(c.G, c.B)) - Math.Min(c.R, Math.Min(c.G, c.B))) / 255.0; }

        /// <summary>Fundo claro e neutro (branco, cinza de controle, amarelo-dica).</summary>
        public static bool FundoClaro(Color c)
        {
            if (c.IsEmpty || c.A < 255) return false;
            if (c.ToArgb() == SystemColors.Info.ToArgb()) return true;
            return Lum(c) > 0.80 && Croma(c) < 0.10;
        }

        public static Color FundoEscuroPara(Color c)
        {
            return Lum(c) > 0.95 ? FundoEntrada : FundoPainel;
        }

        /// <summary>Texto neutro escuro (preto, cinza de texto). Colorido fica.</summary>
        public static bool TextoEscuro(Color c)
        {
            return !c.IsEmpty && c.A == 255 && Lum(c) < 0.62 && Croma(c) < 0.12;
        }

        public static Color TextoClaroPara(Color c)
        {
            return Lum(c) < 0.35 ? Texto : TextoApagado;   // preto -> claro; cinza -> apagado
        }
    }
}
