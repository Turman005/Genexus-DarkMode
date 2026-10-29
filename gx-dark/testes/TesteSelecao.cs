// Selecao de varias revisoes no History (Ctrl+clique), com e sem a Xpz.Dark.
// Em 29/10 o usuario so conseguia comparar com a revisao atual; no modo seguro (Shift)
// a selecao dupla funcionava. Usa a janela History do proprio GeneXus (HistoryToolwindow,
// com a grade e as aparencias do InitializeComponent dela) e clica de verdade.
//   TesteSelecao <Xpz.Dark.dll> <saida> [sem]
using System; using System.Collections.Generic; using System.Data; using System.Drawing; using System.IO; using System.Reflection; using System.Runtime.InteropServices; using System.Windows.Forms;
using Infragistics.Win.UltraWinGrid;
static class T
{
    const string GX = @"C:\Program Files (x86)\GeneXus\GeneXus18-U10";
    [DllImport("user32.dll")] static extern void mouse_event(int f, int x, int y, int d, IntPtr e);
    [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte sc, int f, IntPtr e);
    [STAThread] static int Main(string[] a)
    {
        AppDomain.CurrentDomain.AssemblyResolve += (s, e) =>
        {
            string n = new AssemblyName(e.Name).Name + ".dll";
            foreach (string d in new[] { GX, Path.Combine(GX, "Packages") }) { string p = Path.Combine(d, n); if (File.Exists(p)) return Assembly.LoadFrom(p); }
            return null;
        };
        // Sem KB, os eventos da janela History (AfterSelectChange -> GetSelection) acham o
        // controlador vazio: o erro vai para o console, nao para a caixa do .NET que trava o teste.
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (s, e) => { errosGx++; if (errosGx == 1) Console.WriteLine("  (evento do GeneXus sem KB: " + e.Exception.GetType().Name + " em " + e.Exception.TargetSite + ")"); };
        return Rodar(a[0], a[1], a.Length > 2 && a[2] == "sem");
    }
    static int falhas, errosGx;
    static void Espera(int ms) { var r = System.Diagnostics.Stopwatch.StartNew(); while (r.ElapsedMilliseconds < ms) { Application.DoEvents(); System.Threading.Thread.Sleep(20); } }
    static void Clique(Point p, bool ctrl)
    {
        Cursor.Position = p; Espera(80);
        if (ctrl) { keybd_event(0x11, 0, 0, IntPtr.Zero); Espera(40); }
        mouse_event(0x02, 0, 0, 0, IntPtr.Zero); Espera(40); mouse_event(0x04, 0, 0, 0, IntPtr.Zero); Espera(80);
        if (ctrl) { keybd_event(0x11, 0, 2, IntPtr.Zero); }
        Espera(200);
    }
    static Color Pixel(Point p) { using (var b = new Bitmap(1, 1)) { using (var g = Graphics.FromImage(b)) g.CopyFromScreen(p, Point.Empty, b.Size); return b.GetPixel(0, 0); } }
    static string Rgb(Color c) { return c.R + "," + c.G + "," + c.B; }

    static int Rodar(string dll, string saida, bool sem)
    {
        Assembly dark = null;
        BindingFlags E = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        var cfg = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { { "Tema", "ametista" } };
        if (!sem)
        {
            dark = Assembly.LoadFrom(dll);
            dark.GetType("Xpz.Dark.Paleta").GetMethod("Carregar", E).Invoke(null, new object[] { cfg });
            Infragistics.Win.Office2007ColorTable.ColorScheme = Infragistics.Win.Office2007ColorScheme.Black;
            Infragistics.Win.Office2010ColorTable.ColorScheme = Infragistics.Win.Office2010ColorScheme.Black;
            dark.GetType("Xpz.Dark.Cores").GetMethod("CarregarIsl", E).Invoke(null, new object[] { "teste" });
        }
        else Infragistics.Win.AppStyling.StyleManager.Load(Path.Combine(GX, "GenexusOffice2010Silver.isl"));

        // A janela History do GeneXus (sem KB: so o InitializeComponent)
        Type th = Assembly.LoadFrom(Path.Combine(GX, "Artech.Core.UI.dll")).GetType("Artech.Core.UI.ToolWindows.HistoryToolwindow");
        // Os eventos da janela (Load, InitializeLayout, AfterSelectChange) precisam da KB: a
        // grade de teste e uma GxUltraGrid nova com o layout copiado da grade da janela.
        UltraGrid grade;
        try
        {
            Control hist = (Control)Activator.CreateInstance(th, true);
            FieldInfo campo = th.GetField("ultraGrid1", BindingFlags.Instance | BindingFlags.NonPublic);
            var orig = (UltraGrid)campo.GetValue(hist);
            grade = (UltraGrid)Activator.CreateInstance(campo.FieldType);
            grade.DisplayLayout.CopyFrom(orig.DisplayLayout, PropertyCategories.All);
            Console.WriteLine("  janela History do GeneXus criada; CellClickAction = " + grade.DisplayLayout.Override.CellClickAction +
                              ", SelectTypeRow = " + grade.DisplayLayout.Override.SelectTypeRow);
        }
        catch (Exception ex) { Console.WriteLine("  FALHA HistoryToolwindow nao criou: " + (ex.InnerException ?? ex).Message); return 1; }

        var tab = new DataTable("Revisions");
        foreach (string c in new[] { "Revision", "Name", "Modified", "User", "CreatedIn", "CurrentIn", "RevisionDate" }) tab.Columns.Add(c);
        for (int i = 7; i >= 1; i--) tab.Rows.Add(i.ToString(), "ObjetoTeste", "26/10", "usuario", "kbteste", i == 7 ? "kbteste" : "", "26/10");

        var f = new Form { Text = "selecao", Size = new Size(900, 360), StartPosition = FormStartPosition.Manual, Location = new Point(80, 80), TopMost = true };
        grade.Dock = DockStyle.Fill; f.Controls.Add(grade);
        if (!sem) dark.GetType("Xpz.Dark.Fase2").GetMethod("Iniciar", E).Invoke(null, new object[] { cfg });
        f.Show(); f.Activate(); Espera(200);
        // Como no GeneXus: a grade ja foi escurecida quando os dados chegam. A GxUltraGrid
        // repoe a aparencia branca no InitializeLayout - sem o timer, nao pode ficar branca.
        if (!sem) { dark.GetType("Xpz.Dark.Fase2").GetMethod("Processar", E).Invoke(null, new object[] { f }); Espera(300); }
        grade.DataSource = tab; Espera(300);
        Console.WriteLine("  linhas na grade: " + grade.Rows.Count);
        if (!sem)
        {
            Color vazio = Pixel(grade.PointToScreen(new Point(grade.Width / 2, grade.Height - 20)));
            bool escura = vazio.R + vazio.G + vazio.B < 3 * 100; if (!escura) falhas++;
            Console.WriteLine("  {0} {1,-56} {2}", escura ? "[ok] " : "FALHA", "grade escura logo ao receber os dados (sem esperar o timer)", "area vazia " + Rgb(vazio));
        }

        Func<int, Point> centro = i => { var el = grade.Rows[i].Cells[1].GetUIElement(); return grade.PointToScreen(new Point(el.Rect.X + el.Rect.Width / 2, el.Rect.Y + el.Rect.Height / 2)); };
        Point normal = centro(5);
        Clique(centro(1), false);
        Clique(centro(3), true);
        Cursor.Position = new Point(Screen.PrimaryScreen.Bounds.Right - 2, 2); Espera(300);

        int n = grade.Selected.Rows.Count;
        bool ok = n == 2; if (!ok) falhas++;
        Console.WriteLine("  {0} {1,-56} {2}", ok ? "[ok] " : "FALHA", "Ctrl+clique seleciona duas revisoes", n + " selecionada(s)");
        Color c1 = Pixel(new Point(centro(1).X + 40, centro(1).Y)), c3 = Pixel(new Point(centro(3).X + 40, centro(3).Y)), cn = Pixel(new Point(normal.X + 40, normal.Y));
        bool visivel = !c1.Equals(cn) && !c3.Equals(cn); if (!visivel) falhas++;
        Console.WriteLine("  {0} {1,-56} {2}", visivel ? "[ok] " : "FALHA", "linhas selecionadas se destacam das outras", "sel " + Rgb(c1) + " / " + Rgb(c3) + ", normal " + Rgb(cn));
        using (var b = new Bitmap(f.Width, f.Height)) { using (var g = Graphics.FromImage(b)) g.CopyFromScreen(f.Location, Point.Empty, f.Size); b.Save(Path.Combine(saida, sem ? "selecao-sem.png" : "selecao.png")); }
        f.Close();
        return falhas;
    }
}
