// Menus da DevExpress (Variables/Structure) montados no menu escuro do WinForms:
// itens, icone, separador (BeginGroup), submenu, marcado, desabilitado e o CLIQUE chegando
// ao item original da DevExpress. Foto: popup-devexpress.png.
using System; using System.Collections.Generic; using System.Drawing; using System.IO; using System.Reflection; using System.Windows.Forms;
using DevExpress.Utils.Menu;
static class T
{
    const string GX = @"C:\Program Files (x86)\GeneXus\GeneXus18-U10";
    static int falhas;
    static void Check(string n, bool ok, string d) { Console.WriteLine("  {0} {1,-52} {2}", ok ? "[ok] " : "FALHA", n, d); if (!ok) falhas++; }
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool EnumThreadWindows(uint tid, EnumProc cb, IntPtr l);
    [System.Runtime.InteropServices.DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    static List<IntPtr> Janelas() { var l = new List<IntPtr>(); EnumThreadWindows(GetCurrentThreadId(), (h, p) => { l.Add(h); return true; }, IntPtr.Zero); return l; }
    [STAThread] static int Main(string[] a)
    {
        AppDomain.CurrentDomain.AssemblyResolve += (s, e) => { string p = Path.Combine(GX, new AssemblyName(e.Name).Name + ".dll"); return File.Exists(p) ? Assembly.LoadFrom(p) : null; };
        return Rodar(a[0], a[1]);
    }
    static int Rodar(string dll, string saida)
    {
        Assembly dark = Assembly.LoadFrom(dll);
        BindingFlags E = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        var cfg = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { { "Tema", "ametista" } };
        dark.GetType("Xpz.Dark.Fase2").GetMethod("Iniciar", E).Invoke(null, new object[] { cfg });

        int cliques = 0;
        var menu = new DXPopupMenu();
        var img = new Bitmap(16, 16); using (var g = Graphics.FromImage(img)) g.FillEllipse(Brushes.Orange, 2, 2, 12, 12);
        var add = new DXMenuItem("Add Variable", (s, e) => cliques++); add.Image = img; menu.Items.Add(add);
        menu.Items.Add(new DXMenuItem("Remove") { Enabled = false });
        var sub = new DXSubMenuItem("Sort") { BeginGroup = true }; sub.Items.Add(new DXMenuItem("Ascending")); sub.Items.Add(new DXMenuItem("Descending")); menu.Items.Add(sub);
        menu.Items.Add(new DXMenuCheckItem("Show Standard Variables", true));

        // estrutura montada (sem abrir)
        var cms = new ContextMenuStrip();
        Type md = dark.GetType("Xpz.Dark.MenusDevExpress", true);
        md.GetMethod("Montar", E).Invoke(null, new object[] { menu.Items, cms.Items });
        Check("itens montados (com separador do BeginGroup)", cms.Items.Count == 5, cms.Items.Count + " itens");
        Check("icone do item preservado", ((ToolStripMenuItem)cms.Items[0]).Image != null, "");
        Check("item desabilitado continua desabilitado", !cms.Items[1].Enabled, "");
        Check("submenu com 2 itens", ((ToolStripMenuItem)cms.Items[3]).DropDownItems.Count == 2, "");
        Check("item marcado continua marcado", ((ToolStripMenuItem)cms.Items[4]).Checked, "");
        cms.Items[0].PerformClick();
        Check("clique chega ao item da DevExpress", cliques == 1, cliques + " clique(s)");

        // foto do menu aberto pelo gerenciador
        var f = new Form { Text = "dx", Size = new Size(400, 300), StartPosition = FormStartPosition.Manual, Location = new Point(80, 80), TopMost = true };
        f.Show(); Application.DoEvents();
        dark.GetType("Xpz.Dark.Fase2").GetMethod("Processar", E).Invoke(null, new object[] { f });
        var mgr = (IDXMenuManager)md.GetField("Instancia", E).GetValue(null);
        mgr.ShowPopupMenu(menu, f, new Point(20, 20));
        for (int i = 0; i < 8; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(50); }
        Point p = f.PointToScreen(new Point(16, 16));
        using (var b = new Bitmap(240, 150)) { using (var g = Graphics.FromImage(b)) g.CopyFromScreen(p, Point.Empty, b.Size); b.Save(Path.Combine(saida, "popup-devexpress.png"));
            Color lateral = b.GetPixel(10, 40);
            Check("lateral do menu escura (sem a faixa clara)", (lateral.R + lateral.G + lateral.B) / 3 < 90, lateral.R + "," + lateral.G + "," + lateral.B); }
        // Fecha o menu antes de sair: um menu WinForms deixado aberto num processo de teste
        // (sem laco de mensagens) derruba o processo na saida - acontece com ContextMenuStrip
        // puro tambem, nao e da Xpz.Dark (conferido em 28/10).
        foreach (IntPtr h in Janelas()) { var dd = Control.FromHandle(h) as ToolStripDropDown; if (dd != null) dd.Close(); }
        for (int i = 0; i < 4; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(30); }

        // Dica (tooltip) das arvores: o controlador padrao do DevExpress, com as cores do tema
        dark.GetType("Xpz.Dark.Grades").GetMethod("Dica", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { f });
        var dica = DevExpress.Utils.ToolTipController.DefaultController;
        Point pd = f.PointToScreen(new Point(60, 200));
        dica.ShowHint("Variavel &Total - Numeric(10.2)", pd);
        for (int i = 0; i < 8; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(50); }
        int clarosDica = 0, totalDica = 0;
        using (var b = new Bitmap(200, 50))
        {
            using (var g = Graphics.FromImage(b)) g.CopyFromScreen(new Point(pd.X, pd.Y + 2), Point.Empty, b.Size);
            b.Save(Path.Combine(saida, "dica-devexpress.png"));
            for (int y = 4; y < b.Height; y += 2) for (int x = 4; x < b.Width; x += 2) { Color c = b.GetPixel(x, y); totalDica++; if (c.R > 230 && c.G > 230 && c.B > 180) clarosDica++; }
        }
        dica.HideHint();
        Check("dica do DevExpress sem o fundo amarelo-claro", clarosDica * 100 / Math.Max(1, totalDica) < 5, clarosDica + "/" + totalDica + " pontos claros");
        f.Close();
        Console.WriteLine(falhas == 0 ? "TODOS OS TESTES PASSARAM" : falhas + " FALHA(S)");
        return falhas;
    }
}
