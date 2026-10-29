// P1.8 - estados de interacao iguais em todas as bibliotecas:
//   - no .isl de cada tema, todo hover/pressionado declarado usa as cores do TEMA;
//   - a paleta das janelas (menus WinForms, botoes, grades) usa as mesmas;
//   - a caixa de texto com o cursor tem contorno na cor de destaque; sem cursor, a borda.
using System; using System.Collections; using System.Collections.Generic; using System.Drawing; using System.IO; using System.Linq;
using System.Reflection; using System.Text.RegularExpressions; using System.Windows.Forms;

static class T
{
    const string GX = @"C:\Program Files (x86)\GeneXus\GeneXus18-U10";
    static int falhas;
    static void Check(string n, bool ok, string d) { Console.WriteLine("  {0} {1,-56} {2}", ok ? "[ok] " : "FALHA", n, d); if (!ok) falhas++; }
    static string S(Color c) { return c.R + "," + c.G + "," + c.B; }
    static bool Perto(Color a, Color b, int tol) { return Math.Abs(a.R - b.R) <= tol && Math.Abs(a.G - b.G) <= tol && Math.Abs(a.B - b.B) <= tol; }

    [STAThread] static int Main(string[] a)
    {
        AppDomain.CurrentDomain.AssemblyResolve += (s, e) => { string p = Path.Combine(GX, new AssemblyName(e.Name).Name + ".dll"); return File.Exists(p) ? Assembly.LoadFrom(p) : null; };
        Assembly dark = Assembly.LoadFrom(a[0]);
        BindingFlags E = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        Type temas = dark.GetType("Xpz.Dark.Temas", true), ger = dark.GetType("Xpz.Dark.GeradorIsl", true), pal = dark.GetType("Xpz.Dark.Paleta", true);
        string original = File.ReadAllText(Path.Combine(GX, "GenexusOffice2010Silver.isl"));
        Func<string, Color> P = n => (Color)pal.GetField(n, E).GetValue(null);

        foreach (object t in (IList)temas.GetField("Todos", E).GetValue(null))
        {
            string id = (string)t.GetType().GetField("Id").GetValue(t);
            Func<string, Color> Tk = n => (Color)t.GetType().GetProperty(n).GetValue(t, null);
            Color hover = Tk("Hover"), press = Tk("Pressionado"), sel = Tk("Selecionado");
            string isl = (string)ger.GetMethod("Gerar", E).Invoke(null, new[] { original, t });
            int total = 0, fora = 0;
            foreach (Match m in Regex.Matches(isl, "<state name=\"(HotTracked|Pressed|HotTrackSelected)\"[^>]*?backColor=\"(\\d+), (\\d+), (\\d+)\""))
            {
                total++;
                Color c = Color.FromArgb(int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value), int.Parse(m.Groups[4].Value));
                Color esperado = m.Groups[1].Value == "HotTracked" ? hover : press;
                if (c.ToArgb() != esperado.ToArgb()) fora++;
            }
            Check(id + ": hover/pressionado do .isl = cores do tema", fora == 0 && total > 200, total + " estados, " + fora + " fora");
            pal.GetMethod("DeTema", E).Invoke(null, new[] { t });
            bool iguais = P("Realce").ToArgb() == hover.ToArgb() && P("Pressionado").ToArgb() == press.ToArgb() && P("Selecao").ToArgb() == sel.ToArgb();
            Check(id + ": janelas usam os mesmos estados", iguais, "hover " + S(hover) + " / pressionado " + S(press) + " / selecao " + S(sel));
        }

        // contorno de foco
        var cfg = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { { "Tema", "ametista" } };
        dark.GetType("Xpz.Dark.Fase2").GetMethod("Iniciar", E).Invoke(null, new object[] { cfg });
        var f = new Form { Size = new Size(320, 160), StartPosition = FormStartPosition.Manual, Location = new Point(90, 90), TopMost = true };
        var t1 = new TextBox { Left = 20, Top = 20, Width = 200 }; var t2 = new TextBox { Left = 20, Top = 60, Width = 200 };
        f.Controls.Add(t1); f.Controls.Add(t2);
        f.Show(); f.Activate(); Application.DoEvents();
        dark.GetType("Xpz.Dark.Fase2").GetMethod("Processar", E).Invoke(null, new object[] { f });
        t1.Focus();
        for (int i = 0; i < 6; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(40); }
        Color destaque = P("Link"), borda = P("Borda");
        Func<TextBox, Color> bordaDe = tb =>
        {
            Point p = tb.PointToScreen(new Point(tb.Width / 2, 0)); p.Y -= 1;   // linha de cima da borda (area nao-cliente)
            Rectangle r = tb.RectangleToScreen(tb.ClientRectangle);
            // com o visual do Windows a borda fica DENTRO da caixa: primeira linha da area cliente
            using (var b = new Bitmap(1, 1)) { using (var g = Graphics.FromImage(b)) g.CopyFromScreen(new Point(r.X + r.Width / 2, r.Y), Point.Empty, new Size(1, 1)); return b.GetPixel(0, 0); }
        };
        Color c1 = bordaDe(t1), c2 = bordaDe(t2);
        Check("caixa com o cursor: contorno na cor de destaque", Perto(c1, destaque, 12), S(c1) + " (destaque " + S(destaque) + ")");
        Check("caixa sem o cursor: borda do tema", Perto(c2, borda, 12), S(c2) + " (borda " + S(borda) + ")");
        t2.Focus();
        for (int i = 0; i < 6; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(40); }
        Check("foco muda: a outra caixa ganha o contorno", Perto(bordaDe(t2), destaque, 12) && Perto(bordaDe(t1), borda, 12), S(bordaDe(t2)) + " / " + S(bordaDe(t1)));
        f.Close();
        Console.WriteLine(falhas == 0 ? "TODOS OS TESTES PASSARAM" : falhas + " FALHA(S)");
        return falhas;
    }
}
