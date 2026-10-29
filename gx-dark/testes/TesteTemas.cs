// Janela imitando o GeneXus (menu e barra Infragistics, arvore, abas, propriedades,
// barra de status). Liga a Xpz.Dark e troca os 7 temas AO VIVO, fotografando cada um
// (tema-<id>.png) e uma prancha com todos (temas-todos.png). Tambem fotografa a
// pagina Temas e a previa do editor da tela de configuracao em cada tema.
using System; using System.Collections.Generic; using System.Drawing; using System.IO; using System.Linq;
using System.Reflection; using System.Windows.Forms;
using Infragistics.Win.UltraWinToolbars; using Infragistics.Win.UltraWinTree; using Infragistics.Win.UltraWinTabControl;

class MainWindow : Form { }

static class T
{
    const string GX = @"C:\Program Files (x86)\GeneXus\GeneXus18-U10";
    [STAThread] static int Main(string[] a)
    {
        AppDomain.CurrentDomain.AssemblyResolve += (s, e) => { string p = Path.Combine(GX, new AssemblyName(e.Name).Name + ".dll"); return File.Exists(p) ? Assembly.LoadFrom(p) : null; };
        return Rodar(a[0], a[1]);
    }

    static int Rodar(string dll, string saida)
    {
        Assembly dark = Assembly.LoadFrom(dll);
        var cfg = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { { "Tema", "grafite" } };
        Type paleta = dark.GetType("Xpz.Dark.Paleta", true), fase2 = dark.GetType("Xpz.Dark.Fase2", true), cores = dark.GetType("Xpz.Dark.Cores", true);
        Type temas = dark.GetType("Xpz.Dark.Temas", true);
        BindingFlags E = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        paleta.GetMethod("Carregar", E).Invoke(null, new object[] { cfg });

        // ---------------------------------------------------------------- a janela
        var f = new MainWindow { Text = "kbteste1 - GeneXus 18", Size = new Size(1000, 600), StartPosition = FormStartPosition.Manual, Location = new Point(40, 40), TopMost = true };   // por cima: a foto e da tela
        var tm = new UltraToolbarsManager { DockWithinContainer = f };
        var menu = new UltraToolbar("MainMenuBar") { IsMainMenuBar = true };
        var barra = new UltraToolbar("Build");
        tm.Toolbars.AddRange(new[] { menu, barra });
        foreach (var n in new[] { "File", "Edit", "View", "Build", "Knowledge Manager", "Window", "Tools", "Help" })
        { var p = new PopupMenuTool("m" + n); p.SharedProps.Caption = n; tm.Tools.Add(p); menu.Tools.AddTool("m" + n); }
        foreach (var n in new[] { "Release", "Build All", "Run" })
        { var b = new ButtonTool("b" + n); b.SharedProps.Caption = n; tm.Tools.Add(b); barra.Tools.AddTool("b" + n); }

        var sb = new StatusBar { ShowPanels = true };
        sb.Panels.Add(new StatusBarPanel { Text = @"C:\KBs\kbteste1", AutoSize = StatusBarPanelAutoSize.Spring });
        sb.Panels.Add(new StatusBarPanel { Text = "INS", Width = 50 });
        f.Controls.Add(sb);

        var arvore = new UltraTree { Dock = DockStyle.Left, Width = 230 };
        var raiz = arvore.Nodes.Add("kbteste1");
        foreach (var n in new[] { "Main Programs", "Root Module", "CadCliWP", "CadProdWP", "Cliente", "Produto", "Venda", "Domains", "Images" }) raiz.Nodes.Add(n);
        raiz.Expanded = true; raiz.Nodes[3].Selected = true;
        var props = new PropertyGrid { Dock = DockStyle.Right, Width = 260, SelectedObject = new Button { Text = "Confirmar" } };
        var abas = new UltraTabControl { Dock = DockStyle.Fill };
        foreach (var n in new[] { "Start Page", "CadProdWP", "CadCliWP", "Cliente" }) abas.Tabs.Add(n, n);
        abas.SelectedTab = abas.Tabs[1];
        var codigo = new TextBox { Multiline = true, Dock = DockStyle.Fill, Font = new Font("Consolas", 10f),
            Text = "Event 'Confirmar'\r\n    ProdutoCadastro.Call(&ProdutoId, &ProdutoNome)\r\n    Msg('Produto cadastrado!')\r\nEndevent" };
        abas.Tabs[1].TabPage.Controls.Add(codigo);
        f.Controls.Add(abas); f.Controls.Add(props); f.Controls.Add(arvore);
        tm.Toolbars["MainMenuBar"].DockedPosition = DockedPosition.Top;

        fase2.GetMethod("Iniciar", E).Invoke(null, new object[] { cfg });
        cores.GetMethod("CarregarIsl", E).Invoke(null, new object[] { "teste" });
        f.Show(); f.Activate(); Application.DoEvents();
        fase2.GetMethod("Processar", E).Invoke(null, new object[] { f });
        Application.DoEvents(); System.Threading.Thread.Sleep(300); Application.DoEvents();

        // ---------------------------------------------------------------- troca ao vivo
        var lista = (System.Collections.IList)temas.GetField("Todos", E).GetValue(null);
        var fotos = new List<Tuple<string, Bitmap>>();
        MethodInfo trocar = fase2.GetMethod("TrocarTema", E);
        foreach (object t in lista)
        {
            string id = (string)t.GetType().GetField("Id").GetValue(t), nome = (string)t.GetType().GetField("Nome").GetValue(t);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            trocar.Invoke(null, new[] { t });
            Application.DoEvents(); System.Threading.Thread.Sleep(250); Application.DoEvents();
            sw.Stop();
            // so a area interna: a barra de titulo (ativa/inativa) e o que esta atras variam entre rodadas
            Rectangle area = f.RectangleToScreen(f.ClientRectangle);
            var bmp = new Bitmap(area.Width, area.Height);
            using (var g = Graphics.FromImage(bmp)) g.CopyFromScreen(area.Location, Point.Empty, area.Size);
            bmp.Save(Path.Combine(saida, "tema-" + id + ".png"));
            Color fundoCodigo = bmp.GetPixel(abas.Left + 300 + 8, area.Height - 120);
            Console.WriteLine("{0,-12} troca em {1,4} ms   area de codigo = {2},{3},{4}", nome, sw.ElapsedMilliseconds, fundoCodigo.R, fundoCodigo.G, fundoCodigo.B);
            fotos.Add(Tuple.Create(nome, bmp));
        }
        f.Close();

        // prancha 3x3 em miniatura
        int w = 480, h = 288;
        using (var pr = new Bitmap(w * 3 + 40, (h + 30) * 3 + 20))
        using (var g = Graphics.FromImage(pr))
        {
            g.Clear(Color.FromArgb(12, 12, 14));
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            for (int i = 0; i < fotos.Count; i++)
            {
                int x = 10 + (i % 3) * (w + 10), y = 10 + (i / 3) * (h + 30);
                g.DrawImage(fotos[i].Item2, new Rectangle(x, y + 22, w, h));
                TextRenderer.DrawText(g, fotos[i].Item1, new Font("Segoe UI Semibold", 11f), new Point(x, y), Color.White);
            }
            pr.Save(Path.Combine(saida, "temas-todos.png"));
        }
        return 0;
    }
}
