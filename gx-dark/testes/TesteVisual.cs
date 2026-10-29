// Janela com os componentes que a auditoria de 27/10 achou claros no GeneXus.
// A Xpz.Dark e ligada ANTES (gancho CBT): a janela tem que nascer escura.
// Depois: foto (tela-visual.png) + auditoria + conferencias objetivas.
using System; using System.Drawing; using System.IO; using System.Reflection; using System.Collections.Generic; using System.Windows.Forms;

static class T
{
    const string GX = @"C:\Program Files (x86)\GeneXus\GeneXus18-U10";
    static int falhas;
    static void Check(string n, bool ok, string d) { Console.WriteLine("  {0} {1,-55} {2}", ok ? "[ok] " : "FALHA", n, d); if (!ok) falhas++; }

    [STAThread] static int Main(string[] a)
    {
        AppDomain.CurrentDomain.AssemblyResolve += (s, e) => { string p = Path.Combine(GX, new AssemblyName(e.Name).Name + ".dll"); return File.Exists(p) ? Assembly.LoadFrom(p) : null; };
        return Rodar(a[0], a[1]);
    }

    static int Rodar(string dll, string saida)
    {
        Assembly dark = Assembly.LoadFrom(dll);
        Type fase2 = dark.GetType("Xpz.Dark.Fase2", true);
        fase2.GetMethod("Iniciar", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
             .Invoke(null, new object[] { new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) });

        var f = new Form { Text = "teste visual", Size = new Size(640, 420), StartPosition = FormStartPosition.CenterScreen, TopMost = true };
        var sb = new StatusBar { ShowPanels = true };
        sb.Panels.Add(new StatusBarPanel { Text = @"C:\KBs\kbteste1", AutoSize = StatusBarPanelAutoSize.Spring });
        sb.Panels.Add(new StatusBarPanel { Text = "INS", Width = 50, Alignment = HorizontalAlignment.Center });
        f.Controls.Add(sb);
        var tb = new TextBox { Left = 10, Top = 10, Width = 250, Text = "Filter" };
        var cb = new ComboBox { Left = 10, Top = 40, Width = 250 }; cb.Items.Add("Start"); cb.SelectedIndex = 0;
        f.Controls.Add(tb); f.Controls.Add(cb);
        // grade Infragistics vazia com fundo branco (como a Work With Objects)
        var grade = new Infragistics.Win.UltraWinGrid.UltraGrid { Left = 10, Top = 75, Width = 300, Height = 200 };
        grade.DisplayLayout.Appearance.BackColor = Color.White;
        f.Controls.Add(grade);
        // barra de rolagem da Actipro (a do editor)
        var rol = new ActiproSoftware.WinUICore.ScrollBar { Orientation = Orientation.Vertical }; rol.Bounds = new Rectangle(330, 10, 17, 260); rol.Maximum = 100; rol.LargeChange = 20;
        f.Controls.Add(rol);

        f.Show(); f.Activate(); Application.DoEvents();
        System.Threading.Thread.Sleep(500); Application.DoEvents();

        Console.WriteLine("rolagem bounds=" + rol.Bounds + " dpi=" + f.DeviceDpi);
        string modo = Environment.GetEnvironmentVariable("MODO") ?? "";
        if (modo.Contains("w")) { foreach (Control c in new Control[] { tb, cb }) { AllowDarkModeForWindow(c.Handle, true); SetWindowTheme(c.Handle, "DarkMode_CFD", null); } }
        if (modo.Contains("s")) { tb.BorderStyle = BorderStyle.FixedSingle; }
        if (modo.Contains("c")) { cb.FlatStyle = FlatStyle.Standard; AllowDarkModeForWindow(cb.Handle, true); SetWindowTheme(cb.Handle, "DarkMode_CFD", null); }
        Application.DoEvents(); System.Threading.Thread.Sleep(300); Application.DoEvents();
        Check("janela escurecida pelo gancho (antes de mostrar)", f.BackColor.R < 80, Show(f.BackColor));
        Check("barra de status: paineis desenhados por nos", sb.Panels[0].Style == StatusBarPanelStyle.OwnerDraw, sb.Panels[0].Style.ToString());
        Check("grade Infragistics: area vazia escura", grade.DisplayLayout.Appearance.BackColor.R < 80, Show(grade.DisplayLayout.Appearance.BackColor));
        Check("rolagem Actipro: renderizador escuro", rol.Renderer != null && rol.Renderer.GetType().Name == "RolagemEscura", rol.Renderer == null ? "-" : rol.Renderer.GetType().Name);

        // foto
        using (var bmp = new Bitmap(f.Width, f.Height))
        {
            using (var g = Graphics.FromImage(bmp)) g.CopyFromScreen(f.Location, Point.Empty, f.Size);
            // a foto de comparacao e so a area interna, sem foco em caixa de texto: barra de
            // titulo (ativa/inativa), selecao de texto e o que esta atras variam entre rodadas
            f.ActiveControl = grade; Application.DoEvents();
            Rectangle interna = f.RectangleToScreen(f.ClientRectangle);
            using (var foto = new Bitmap(interna.Width, interna.Height))
            {
                using (var gf = Graphics.FromImage(foto)) gf.CopyFromScreen(interna.Location, Point.Empty, interna.Size);
                foto.Save(Path.Combine(saida, "tela-visual" + modo + ".png"));
            }
            Color pBarra = bmp.GetPixel(f.Width / 2, f.Height - 20);
            Check("barra de status: fundo escuro na tela", pBarra.R < 90, Show(pBarra));
            Point pr = f.PointToScreen(new Point(rol.Left + 8, rol.Top + 200));
            Color pRol = bmp.GetPixel(pr.X - f.Left, pr.Y - f.Top);
            Check("rolagem: trilho escuro na tela", pRol.R < 90, Show(pRol));
        }

        // menu das abas: o handler troca o estilo para System
        var cm = new Infragistics.Win.IGControls.IGContextMenu();
        Type menu = dark.GetType("Xpz.Dark.MenuTema", true);
        var args = new FakeArgs { ContextMenu = cm };
        menu.GetMethod("AoMenuDaAba", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { null, args });
        Check("menu das abas: estilo System (desenho do Windows)", cm.Style.ToString() == "System", cm.Style.ToString());

        // auditoria sobre a janela
        dark.GetType("Xpz.Dark.Auditoria", true).GetMethod("VarrerAgora", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Invoke(null, null);
        string log = File.ReadAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Xpz.Dark\dark-auditoria.log"));
        int i = log.LastIndexOf("== ");
        string ultima = i >= 0 ? log.Substring(i) : log;
        bool limpa = !ultima.Contains("teste visual");
        Check("auditoria: nenhuma area clara na janela de teste", limpa, limpa ? "" : "\n" + ultima);
        f.Close();
        Console.WriteLine(falhas == 0 ? "TODOS OS TESTES PASSARAM" : falhas + " FALHA(S)");
        return falhas;
    }
    [System.Runtime.InteropServices.DllImport("uxtheme.dll", EntryPoint = "#133")] static extern bool AllowDarkModeForWindow(IntPtr h, bool a);
    [System.Runtime.InteropServices.DllImport("uxtheme.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)] static extern int SetWindowTheme(IntPtr h, string a, string b);
    static string Show(Color c) { return c.R + "," + c.G + "," + c.B; }
}
public class FakeArgs : EventArgs { public Infragistics.Win.IGControls.IGContextMenu ContextMenu { get; set; } }
