// Xpz.Dark - icones de traco escuro da moldura, clareados.

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

using Artech.Architecture.Common.Packages;
using Artech.Architecture.Common.Services;
using Artech.Architecture.UI.Framework.Packages;
using Artech.Common.Framework.Commands;
using Artech.Common.Localization;

namespace Xpz.Dark
{
    // =====================================================================
    //  Icones da moldura
    // =====================================================================
    internal static class Icones
    {
        // Os que o Resources.Images entrega aos filtros de desenho.
        private static readonly string[] Nomes =
        {
            "TabAndPaneCloseButton", "PaneUnpinned", "PanePinned", "PinnedHover",
            "TabListButton", "TabListButtonOverflow",
            "TabScrollButtonRight", "TabScrollButtonRightEmpty",
            "TabScrollButtonLeft", "TabScrollButtonLeftEmpty",
            "NewHorizontalTabGroup", "NewVerticalTabGroup",
        };

        public static void Aplicar()
        {
            try
            {
                Assembly fw = Assembly.Load("Artech.FrameworkDE");
                Type images = fw.GetType("Artech.FrameworkDE.Resources.Images", true);
                FieldInfo campo = CampoDoGerenciador(images);
                if (campo == null) { Log.Linha("icones: campo do gerenciador de recursos nao existe - nada feito"); return; }

                var nosso = new GerenciadorClaro("Artech.FrameworkDE.Resources.Images", fw, Nomes);
                campo.SetValue(null, nosso);
                Log.Linha("icones: Resources.Images agora usa o gerenciador que clareia");

                // Campos estaticos: ler primeiro dispara o construtor estatico; so
                // depois a troca fica de vez.
                // (U10: TabAndPaneCloseButton, PanePinned, PaneUnpinned; na U14, ofuscada, os
                // nomes mudam - por isso todos os Icon estaticos dos dois tipos.)
                Trocar(fw, nosso, "Artech.FrameworkDE.DockManager");
                Trocar(fw, nosso, "Artech.FrameworkDE.TabbedMdiManager");
            }
            catch (Exception ex) { Log.Linha("icones FALHOU: " + Log.Curto(ex)); }
        }

        /// <summary>O campo estatico do ResourceManager da classe de recursos. Pelo TIPO: na U10
        /// chama "resourceMan"; na U14, ofuscada, tem outro nome.</summary>
        internal static FieldInfo CampoDoGerenciador(Type images)
        {
            FieldInfo achado = null;
            foreach (FieldInfo f in images.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (!typeof(System.Resources.ResourceManager).IsAssignableFrom(f.FieldType)) continue;
                if (achado != null) return null;   // dois candidatos: nao adivinha
                achado = f;
            }
            return achado;
        }

        private static void Trocar(Assembly fw, GerenciadorClaro g, string tipo)
        {
            foreach (FieldInfo f in fw.GetType(tipo, true).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                // Sao "static readonly" (U10 e U14): a reflexao do .NET Framework grava assim mesmo,
                // como a 0.13.0 fazia. Filtrar IsInitOnly pulava os tres (regressao da 0.13.1).
                if (f.FieldType != typeof(Icon)) continue;
                try
                {
                    var original = f.GetValue(null) as Icon;
                    if (original == null) { Log.Linha("icones: " + tipo + "." + f.Name + " vazio"); continue; }
                    Icon claro = g.Clarear(tipo + "." + f.Name, original);
                    if (claro != null) f.SetValue(null, claro);
                }
                catch (Exception ex) { Log.Linha("icones: " + tipo + "." + f.Name + " FALHOU: " + Log.Curto(ex)); }
            }
        }
    }

    /// <summary>
    /// Entra no lugar do CachingResourceManager de Resources.Images. Para os
    /// icones da lista devolve a versao clara; para todo o resto, e em qualquer
    /// falha propria, devolve exatamente o que o original devolveria.
    /// </summary>
    internal sealed class GerenciadorClaro : CachingResourceManager
    {
        private readonly HashSet<string> _alvos;
        private readonly Dictionary<string, object> _feitos = new Dictionary<string, object>();
        private readonly object _trava = new object();

        public GerenciadorClaro(string baseName, Assembly assembly, IEnumerable<string> alvos)
            : base(baseName, assembly)
        {
            _alvos = new HashSet<string>(alvos, StringComparer.Ordinal);
        }

        public override object GetObject(string name)
        {
            return GetObject(name, null);
        }

        public override object GetObject(string name, CultureInfo culture)
        {
            object original = base.GetObject(name, culture);
            try
            {
                if (name == null || !_alvos.Contains(name)) return original;
                lock (_trava)
                {
                    object pronto;
                    if (_feitos.TryGetValue(name, out pronto)) return pronto;
                    var ic = original as Icon;
                    object r = ic == null ? original : (object)(Clarear("Images." + name, ic) ?? ic);
                    _feitos[name] = r;
                    return r;
                }
            }
            catch { return original; }
        }

        /// <summary>Versao clara do icone, ou null se ele ja e claro o bastante.</summary>
        public Icon Clarear(string rotulo, Icon original)
        {
            try
            {
                string decisao;
                Icon r = Pintura.ClarearSeEscuro(original, out decisao);
                Log.Linha("icones: " + rotulo + " " + decisao);
                return r;
            }
            catch (Exception ex) { Log.Linha("icones: " + rotulo + " FALHOU: " + Log.Curto(ex)); return null; }
        }
    }

    internal static class Pintura
    {
        // Abaixo disto o glifo some em fundo escuro. Medido no inventario de 27/10:
        // os icones de moldura com traco escuro ficaram todos bem abaixo de 0.30.
        private const double LIMIAR = 0.45;

        public static Icon ClarearSeEscuro(Icon original, out string decisao)
        {
            using (Bitmap fonte = original.ToBitmap())
            using (var bmp = new Bitmap(fonte.Width, fonte.Height, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Transparent);
                    g.DrawImageUnscaled(fonte, 0, 0);
                }

                double soma = 0; int vis = 0;
                for (int y = 0; y < bmp.Height; y++)
                    for (int x = 0; x < bmp.Width; x++)
                    {
                        Color c = bmp.GetPixel(x, y);
                        if (c.A >= 128) { vis++; soma += Lum(c); }
                    }
                double lum = vis > 0 ? soma / vis : 1.0;
                if (lum >= LIMIAR)
                {
                    decisao = string.Format(CultureInfo.InvariantCulture, "mantido (lum {0:0.00}, {1}x{2})", lum, bmp.Width, bmp.Height);
                    return null;
                }

                // Inverte a LUMINOSIDADE preservando matiz e saturacao: preto vira
                // branco, cinza-escuro vira cinza-claro, e um detalhe colorido
                // continua da mesma cor, so mais claro.
                for (int y = 0; y < bmp.Height; y++)
                    for (int x = 0; x < bmp.Width; x++)
                    {
                        Color c = bmp.GetPixel(x, y);
                        if (c.A == 0) continue;
                        double h, s, l;
                        ParaHsl(c, out h, out s, out l);
                        bmp.SetPixel(x, y, DeHsl(c.A, h, s, 1.0 - l));
                    }

                decisao = string.Format(CultureInfo.InvariantCulture, "CLAREADO (lum {0:0.00}, {1}x{2})", lum, bmp.Width, bmp.Height);
                // O HICON fica vivo pelo resto do processo - sao uma duzia de icones.
                return Icon.FromHandle(bmp.GetHicon());
            }
        }

        private static double Lum(Color c)
        {
            return (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255.0;
        }

        private static void ParaHsl(Color c, out double h, out double s, out double l)
        {
            double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
            l = (max + min) / 2.0;
            if (max == min) { h = 0; s = 0; return; }
            double d = max - min;
            s = l > 0.5 ? d / (2.0 - max - min) : d / (max + min);
            if (max == r) h = (g - b) / d + (g < b ? 6 : 0);
            else if (max == g) h = (b - r) / d + 2;
            else h = (r - g) / d + 4;
            h /= 6.0;
        }

        private static Color DeHsl(int a, double h, double s, double l)
        {
            double r, g, b;
            if (s == 0) { r = g = b = l; }
            else
            {
                double q = l < 0.5 ? l * (1 + s) : l + s - l * s;
                double p = 2 * l - q;
                r = Canal(p, q, h + 1.0 / 3);
                g = Canal(p, q, h);
                b = Canal(p, q, h - 1.0 / 3);
            }
            return Color.FromArgb(a, Byte(r), Byte(g), Byte(b));
        }

        private static double Canal(double p, double q, double t)
        {
            if (t < 0) t += 1; if (t > 1) t -= 1;
            if (t < 1.0 / 6) return p + (q - p) * 6 * t;
            if (t < 1.0 / 2) return q;
            if (t < 2.0 / 3) return p + (q - p) * (2.0 / 3 - t) * 6;
            return p;
        }

        private static int Byte(double v) { return Math.Max(0, Math.Min(255, (int)Math.Round(v * 255))); }
    }
}
