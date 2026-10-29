// Fotografa a previa de codigo da tela de configuracao em cada tema + a pagina Temas.
using System; using System.Drawing; using System.IO; using System.Reflection; using System.Windows.Forms;
static class T
{
    const string GX = @"C:\Program Files (x86)\GeneXus\GeneXus18-U10";
    [STAThread] static int Main(string[] a)
    {
        AppDomain.CurrentDomain.AssemblyResolve += (s, e) => { string p = Path.Combine(GX, new AssemblyName(e.Name).Name + ".dll"); return File.Exists(p) ? Assembly.LoadFrom(p) : null; };
        Assembly dark = Assembly.LoadFrom(a[0]);
        BindingFlags I = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        Type tela = dark.GetType("Xpz.Dark.TelaConfiguracao", true);
        var t = (Form)Activator.CreateInstance(tela, true);
        t.StartPosition = FormStartPosition.Manual; t.Location = new Point(-32000, -32000); t.ShowInTaskbar = false;
        t.Show(); Application.DoEvents();
        var combo = (ComboBox)tela.GetField("_tema", I).GetValue(t);
        var previa = (Control)tela.GetField("_previa", I).GetValue(t);
        var mostrar = tela.GetMethod("Mostrar", I);
        int n = combo.Items.Count - 1;   // sem o Personalizado
        int w = previa.Width, h = previa.Height;
        using (var pr = new Bitmap(w * 2 + 30, (h + 28) * ((n + 1) / 2) + 10))
        using (var g = Graphics.FromImage(pr))
        {
            g.Clear(Color.FromArgb(12, 12, 14));
            for (int i = 0; i < n; i++)
            {
                combo.SelectedIndex = i; mostrar.Invoke(t, new object[] { 1 }); Application.DoEvents();
                using (var b = new Bitmap(w, h)) { previa.DrawToBitmap(b, new Rectangle(0, 0, w, h));
                    int x = 10 + (i % 2) * (w + 10), y = 6 + (i / 2) * (h + 28);
                    TextRenderer.DrawText(g, combo.Items[i].ToString(), new Font("Segoe UI Semibold", 10f), new Point(x, y), Color.White);
                    g.DrawImage(b, x, y + 22); }
            }
            pr.Save(Path.Combine(a[1], "previas-sintaxe.png"));
        }
        combo.SelectedIndex = 3; mostrar.Invoke(t, new object[] { 3 }); Application.DoEvents();
        using (var b = new Bitmap(t.Width, t.Height)) { t.DrawToBitmap(b, new Rectangle(0, 0, t.Width, t.Height)); b.Save(Path.Combine(a[1], "tela-temas.png")); }
        mostrar.Invoke(t, new object[] { 4 }); Application.DoEvents();
        using (var b = new Bitmap(t.Width, t.Height)) { t.DrawToBitmap(b, new Rectangle(0, 0, t.Width, t.Height)); b.Save(Path.Combine(a[1], "tela-diagnostico.png")); }
        combo.DroppedDown = false; t.Close();
        return 0;
    }
}
