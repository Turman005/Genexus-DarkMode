// Sistema de cores: em CADA tema, o contraste minimo entre os nomes por funcao
// (WCAG 2.x: 4.5:1 para texto normal, 3:1 para texto desabilitado e elementos de
// interface como polegar de rolagem e borda de foco).
using System; using System.Collections; using System.Collections.Generic; using System.Drawing; using System.IO; using System.Reflection;

static class T
{
    const string GX = @"C:\Program Files (x86)\GeneXus\GeneXus18-U10";
    static int falhas;

    static double Lin(int v) { double s = v / 255.0; return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4); }
    static double LR(Color c) { return 0.2126 * Lin(c.R) + 0.7152 * Lin(c.G) + 0.0722 * Lin(c.B); }
    static double K(Color a, Color b) { double x = LR(a), y = LR(b); return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05); }

    [STAThread] static int Main(string[] a)
    {
        AppDomain.CurrentDomain.AssemblyResolve += (s, e) => { string p = Path.Combine(GX, new AssemblyName(e.Name).Name + ".dll"); return File.Exists(p) ? Assembly.LoadFrom(p) : null; };
        Assembly dark = Assembly.LoadFrom(a[0]);
        BindingFlags E = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        Type pal = dark.GetType("Xpz.Dark.Paleta", true), temas = dark.GetType("Xpz.Dark.Temas", true);
        Func<string, Color> c = n =>
        {
            var p = pal.GetProperty(n, E); if (p != null) return (Color)p.GetValue(null, null);
            return (Color)pal.GetField(n, E).GetValue(null);
        };
        // [frente, fundo, minimo]
        var regras = new[]
        {
            Tuple.Create("TextoPrimario", "Superficie0", 7.0), Tuple.Create("TextoPrimario", "Superficie1", 7.0),
            Tuple.Create("TextoPrimario", "Superficie2", 7.0), Tuple.Create("TextoPrimario", "Superficie3", 4.5),
            Tuple.Create("TextoPrimario", "Selecao", 4.5),     Tuple.Create("TextoSecundario", "Superficie2", 4.5),
            Tuple.Create("TextoSecundario", "Superficie0", 4.5), Tuple.Create("TextoDesabilitado", "Superficie2", 3.0),
            Tuple.Create("Destaque", "Superficie0", 4.5),      Tuple.Create("Destaque", "Superficie2", 4.5),
            Tuple.Create("RolagemPolegar", "RolagemTrilho", 1.6),
        };
        foreach (object t in (IList)temas.GetField("Todos", E).GetValue(null))
        {
            string id = (string)t.GetType().GetField("Id").GetValue(t);
            pal.GetMethod("DeTema", E).Invoke(null, new[] { t });
            var ruins = new List<string>();
            double pior = 99;
            foreach (var r in regras)
            {
                double k = K(c(r.Item1), c(r.Item2));
                if (k < r.Item3) ruins.Add(string.Format("{0} sobre {1} = {2:0.0}:1 (min {3})", r.Item1, r.Item2, k, r.Item3));
                pior = Math.Min(pior, k / r.Item3);
            }
            bool ok = ruins.Count == 0;
            Console.WriteLine("  {0} {1,-10} {2} regras de contraste{3}", ok ? "[ok] " : "FALHA", id, regras.Length, ok ? "" : ": " + string.Join("; ", ruins.ToArray()));
            if (!ok) falhas++;
        }
        Console.WriteLine(falhas == 0 ? "TODOS OS TESTES PASSARAM" : falhas + " FALHA(S)");
        return falhas;
    }
}
