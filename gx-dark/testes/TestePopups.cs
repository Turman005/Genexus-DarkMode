// Abre os tipos de menu suspenso que o GeneXus usa e fotografa cada um:
//   1. PopupMenuTool da Infragistics (menu principal e menus de contexto do MenuManager)
//   2. ContextMenuStrip do WinForms (Toolbox, ferramentas)
//   3. IGContextMenu (menu das abas de documento)
using System; using System.Collections.Generic; using System.Drawing; using System.IO; using System.Reflection; using System.Runtime.InteropServices; using System.Windows.Forms;
using Infragistics.Win.UltraWinToolbars;
static class T
{
    const string GX = @"C:\Program Files (x86)\GeneXus\GeneXus18-U10";
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [STAThread] static int Main(string[] a)
    {
        AppDomain.CurrentDomain.AssemblyResolve += (s, e) => { string p = Path.Combine(GX, new AssemblyName(e.Name).Name + ".dll"); return File.Exists(p) ? Assembly.LoadFrom(p) : null; };
        return Rodar(a[0], a[1]);
    }
    static Bitmap Icone(Color c) { var b = new Bitmap(16, 16); using (var g = Graphics.FromImage(b)) { g.Clear(Color.Transparent); using (var br = new SolidBrush(c)) g.FillEllipse(br, 2, 2, 12, 12); } return b; }
    static void Foto(Rectangle r, string arq) { using (var b = new Bitmap(r.Width, r.Height)) { using (var g = Graphics.FromImage(b)) g.CopyFromScreen(r.Location, Point.Empty, r.Size); b.Save(arq); } }
    static int falhas;
    static Rectangle Tela(ToolBase t) { var el = t.UIElement; return el.Control.RectangleToScreen(el.Rect); }

    // Contraste WCAG entre o fundo (cor mais comum) e o pixel de texto mais distante dele.
    static double Lin(int v) { double s = v / 255.0; return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4); }
    static double Lum(Color c) { return 0.2126 * Lin(c.R) + 0.7152 * Lin(c.G) + 0.0722 * Lin(c.B); }
    static double Contraste(Color a, Color b) { double x = Lum(a), y = Lum(b); return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05); }
    static void Legivel(string nome, Rectangle r)
    {
        double melhor = 0; Color fundo = Color.Empty;
        using (var b = new Bitmap(r.Width, r.Height))
        {
            using (var g = Graphics.FromImage(b)) g.CopyFromScreen(r.Location, Point.Empty, r.Size);
            var cont = new Dictionary<int, int>();
            for (int x = 0; x < b.Width; x++) for (int y = 0; y < b.Height; y++) { int k = b.GetPixel(x, y).ToArgb(); int n; cont.TryGetValue(k, out n); cont[k] = n + 1; }
            int max = 0; foreach (var kv in cont) if (kv.Value > max) { max = kv.Value; fundo = Color.FromArgb(kv.Key); }
            // depois da coluna de icones/marca: so o texto
            for (int x = Math.Min(28, b.Width - 1); x < b.Width; x++) for (int y = 0; y < b.Height; y++) melhor = Math.Max(melhor, Contraste(fundo, b.GetPixel(x, y)));
        }
        bool ok = melhor >= 4.5;
        if (!ok) falhas++;
        Console.WriteLine("  {0} {1,-56} {2}", ok ? "[ok] " : "FALHA", nome, "contraste " + melhor.ToString("0.0") + " (fundo " + fundo.R + "," + fundo.G + "," + fundo.B + ")");
    }

    static int Rodar(string dll, string saida)
    {
        Assembly dark = Assembly.LoadFrom(dll);
        BindingFlags E = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        var cfg = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { { "Tema", "ametista" } };
        dark.GetType("Xpz.Dark.Paleta").GetMethod("Carregar", E).Invoke(null, new object[] { cfg });
        Infragistics.Win.Office2007ColorTable.ColorScheme = Infragistics.Win.Office2007ColorScheme.Black;
        Infragistics.Win.Office2010ColorTable.ColorScheme = Infragistics.Win.Office2010ColorScheme.Black;
        dark.GetType("Xpz.Dark.Cores").GetMethod("CarregarIsl", E).Invoke(null, new object[] { "teste" });

        var f = new Form { Text = "popups", Size = new Size(700, 420), StartPosition = FormStartPosition.Manual, Location = new Point(80, 80), TopMost = true };
        var tm = new UltraToolbarsManager { DockWithinContainer = f };
        // Coluna de icones: sem os papeis MenuIconArea* no .isl ela saia BRANCA (a "lateral
        // branca" da auditoria de 28/10 - e a foto de referencia antiga a registrava assim).
        // Mesmo com o gerenciador pedindo branco, o .isl do tema tem de prevalecer.
        tm.MenuSettings.IconAreaAppearance.BackColor = Color.White;
        tm.MenuSettings.IconAreaExpandedAppearance.BackColor = Color.White;
        var menu = new UltraToolbar("MainMenuBar") { IsMainMenuBar = true }; tm.Toolbars.Add(menu);
        var pop = new PopupMenuTool("mFile"); pop.SharedProps.Caption = "File"; tm.Tools.Add(pop); menu.Tools.AddTool("mFile");
        string[] itens = { "New Object", "Open", "Save", "Save All", "Properties" };
        Color[] cores = { Color.Orange, Color.DeepSkyBlue, Color.LimeGreen, Color.Violet, Color.Gold };
        for (int i = 0; i < itens.Length; i++)
        {
            var b = new ButtonTool("i" + i); b.SharedProps.Caption = itens[i]; b.SharedProps.AppearancesSmall.Appearance.Image = Icone(cores[i]);
            tm.Tools.Add(b); var inst = pop.Tools.AddTool("i" + i); if (i == 2) inst.InstanceProps.IsFirstInGroup = true;
        }
        // Submenu como o "Tema escuro" do Tools: item marcado (tema atual) e mouse em cima.
        // Em 28/10 a letra do item com o mouse em cima e a do marcado sumiam no fundo.
        var sub = new PopupMenuTool("mTema"); sub.SharedProps.Caption = "Tema escuro"; tm.Tools.Add(sub);
        pop.Tools.AddTool("mTema").InstanceProps.IsFirstInGroup = true;
        foreach (string nome in new[] { "Ardosia", "Grafite" })
        {
            var st = new StateButtonTool("t" + nome); st.SharedProps.Caption = nome; tm.Tools.Add(st); sub.Tools.AddTool("t" + nome);
            if (nome == "Ardosia") st.Checked = true;
        }
        var cms = new ContextMenuStrip();
        for (int i = 0; i < itens.Length; i++) cms.Items.Add(itens[i], Icone(cores[i]));
        cms.Items.Insert(2, new ToolStripSeparator());
        f.Controls.Add(new Panel { Dock = DockStyle.Fill, ContextMenuStrip = cms });

        dark.GetType("Xpz.Dark.Fase2").GetMethod("Iniciar", E).Invoke(null, new object[] { cfg });
        f.Show(); f.Activate(); Application.DoEvents();
        dark.GetType("Xpz.Dark.Fase2").GetMethod("Processar", E).Invoke(null, new object[] { f });
        Application.DoEvents();

        // 1. PopupMenuTool
        Point p1 = f.PointToScreen(new Point(30, 60));
        pop.ShowPopup(p1);
        for (int i = 0; i < 8; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(50); }
        Foto(new Rectangle(p1.X - 4, p1.Y - 4, 260, 200), Path.Combine(saida, "popup-infragistics.png"));
        int claros = 0;
        using (var b = new Bitmap(1, 110)) { using (var g = Graphics.FromImage(b)) g.CopyFromScreen(new Point(p1.X + 3, p1.Y + 4), Point.Empty, b.Size);
            for (int y = 0; y < b.Height; y++) { Color c = b.GetPixel(0, y); if (c.R + c.G + c.B > 3 * 160) claros++; } }
        if (claros > 0) falhas++;
        Console.WriteLine("  {0} {1,-56} {2}", claros == 0 ? "[ok] " : "FALHA", "coluna de icones do menu escura (GeneXus a pinta de branco)", claros + " pontos claros");

        // "Tema escuro" aberto (o item fica destacado enquanto o submenu esta aberto) e o
        // mouse em cima de um item do submenu.
        var temaNoMenu = (PopupMenuTool)pop.Tools["mTema"];
        temaNoMenu.DropDown();
        for (int i = 0; i < 10; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(50); }
        Rectangle rTema = Tela(temaNoMenu), rMarcado = Tela(temaNoMenu.Tools["tArdosia"]), rHover = Tela(temaNoMenu.Tools["tGrafite"]);
        Cursor.Position = new Point(rHover.X + rHover.Width / 2, rHover.Y + rHover.Height / 2);
        for (int i = 0; i < 10; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(50); }
        Foto(Rectangle.Union(new Rectangle(p1.X - 4, p1.Y - 4, 260, 200), Rectangle.Inflate(rMarcado, 4, 60)), Path.Combine(saida, "popup-hover.png"));
        Legivel("menu: letra do item com submenu aberto", rTema);
        Legivel("menu: letra do item marcado (tema atual)", rMarcado);
        Legivel("menu: letra do item com o mouse em cima", rHover);
        Cursor.Position = new Point(Screen.PrimaryScreen.Bounds.Right - 2, 2);
        pop.ClosePopup(); Application.DoEvents();

        // 2. ContextMenuStrip (renderizador global da Xpz.Dark)
        Point p2 = f.PointToScreen(new Point(320, 60));
        cms.Show(p2);
        for (int i = 0; i < 8; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(50); }
        Foto(new Rectangle(p2.X - 4, p2.Y - 4, 260, 200), Path.Combine(saida, "popup-winforms.png"));
        cms.Close(); Application.DoEvents();
        f.Close();
        return falhas;
    }
}
