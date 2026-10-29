// Abas das partes (Crownwood Magic TabControl, como Web Layout/Rules/Events), nos
// estilos IDE e Plain e aparencias MultiForm/MultiDocument, antes e depois da Xpz.Dark.
using System; using System.Collections.Generic; using System.Drawing; using System.IO; using System.Reflection; using System.Windows.Forms;
using Crownwood.Magic.Controls; using Crownwood.Magic.Common;
static class T
{
    const string GX = @"C:\Program Files (x86)\GeneXus\GeneXus18-U10";
    [STAThread] static int Main(string[] a)
    {
        AppDomain.CurrentDomain.AssemblyResolve += (s, e) => { string p = Path.Combine(GX, new AssemblyName(e.Name).Name + ".dll"); return File.Exists(p) ? Assembly.LoadFrom(p) : null; };
        return Rodar(a[0], a[1]);
    }
    static Crownwood.Magic.Controls.TabControl Abas(VisualStyle st, Crownwood.Magic.Controls.TabControl.VisualAppearance ap, int y)
    {
        var t = new Crownwood.Magic.Controls.TabControl { Style = st, Appearance = ap, Left = 10, Top = y, Width = 560, Height = 70 };
        foreach (var n in new[] { "Web Layout", "Rules", "Events", "Conditions", "Variables", "Help", "Documentation" })
            t.TabPages.Add(new Crownwood.Magic.Controls.TabPage(n));
        t.SelectedIndex = 2;
        return t;
    }
    static int Rodar(string dll, string saida)
    {
        var f = new Form { Text = "abas", Size = new Size(600, 440), StartPosition = FormStartPosition.Manual, Location = new Point(60, 60), TopMost = true };
        var l = new List<Crownwood.Magic.Controls.TabControl> {
            Abas(VisualStyle.IDE, Crownwood.Magic.Controls.TabControl.VisualAppearance.MultiForm, 10),
            Abas(VisualStyle.IDE, Crownwood.Magic.Controls.TabControl.VisualAppearance.MultiDocument, 90),
            Abas(VisualStyle.Plain, Crownwood.Magic.Controls.TabControl.VisualAppearance.MultiForm, 170),
            Abas(VisualStyle.Plain, Crownwood.Magic.Controls.TabControl.VisualAppearance.MultiDocument, 250),
            Abas(VisualStyle.Plain, Crownwood.Magic.Controls.TabControl.VisualAppearance.MultiBox, 330) };   // o do GeneXus
        foreach (var t in l) f.Controls.Add(t);
        f.Show(); Application.DoEvents(); System.Threading.Thread.Sleep(300); Application.DoEvents();
        Foto(f, Path.Combine(saida, "abas-antes.png"));
        Assembly dark = Assembly.LoadFrom(dll);
        BindingFlags E = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        var cfg = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { { "Tema", "ardosia" } };
        Type f2 = dark.GetType("Xpz.Dark.Fase2", true);
        f2.GetMethod("Iniciar", E).Invoke(null, new object[] { cfg });
        f2.GetMethod("Processar", E).Invoke(null, new object[] { f });
        Application.DoEvents(); System.Threading.Thread.Sleep(300); Application.DoEvents();
        Foto(f, Path.Combine(saida, "abas-depois.png"));
        l[4].SelectedIndex = 1; Application.DoEvents(); System.Threading.Thread.Sleep(200); Application.DoEvents();   // troca a aba ativa
        Foto(f, Path.Combine(saida, "abas-depois2.png"));
        f.Close();
        return 0;
    }
    // so a area interna: a barra de titulo (ativa/inativa) e o que esta atras variam entre rodadas
    static void Foto(Form f, string arq) { Rectangle r = f.RectangleToScreen(f.ClientRectangle); using (var b = new Bitmap(r.Width, r.Height)) { using (var g = Graphics.FromImage(b)) g.CopyFromScreen(r.Location, Point.Empty, r.Size); b.Save(arq); } }
}
