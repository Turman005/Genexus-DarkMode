// Mede desempenho e confere o resultado da Fase 2 da Xpz.Dark FORA do GeneXus.
// Monta uma janela com milhares de controles WinForms comuns, chama o mesmo codigo
// da DLL (por reflexao - Fase2 e internal) e cronometra. A janela fica fora da tela.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

static class TesteFase2
{
    static int falhas;
    static void Check(string nome, bool ok, string det)
    {
        Console.WriteLine("  {0} {1,-52} {2}", ok ? "[ok] " : "FALHA", nome, det);
        if (!ok) falhas++;
    }

    const string GX = @"C:\Program Files (x86)\GeneXus\GeneXus18-U10";

    [STAThread]
    static int Main(string[] a)
    {
        AppDomain.CurrentDomain.AssemblyResolve += (s, e) =>
        {
            string arq = System.IO.Path.Combine(GX, new AssemblyName(e.Name).Name + ".dll");
            return System.IO.File.Exists(arq) ? Assembly.LoadFrom(arq) : null;
        };
        Assembly dark = Assembly.LoadFrom(a[0]);
        Type fase2 = dark.GetType("Xpz.Dark.Fase2", true);
        MethodInfo iniciar = fase2.GetMethod("Iniciar", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        MethodInfo processar = fase2.GetMethod("Processar", BindingFlags.Static | BindingFlags.NonPublic);

        var cfg = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);   // tudo ausente = tudo ligado
        iniciar.Invoke(null, new object[] { cfg });

        // ---------------------------------------------------- janela de teste
        var f = new Form { Text = "teste fase 2", StartPosition = FormStartPosition.Manual,
                           Location = new Point(-32000, -32000), ShowInTaskbar = false, Size = new Size(1200, 900) };
        var ts = new ToolStrip(); ts.Items.Add("Arquivo"); ts.Items.Add("Editar"); f.Controls.Add(ts);
        var pg = new PropertyGrid { Dock = DockStyle.Right, Width = 300, SelectedObject = f }; f.Controls.Add(pg);
        var tv = new TreeView { Dock = DockStyle.Left, Width = 250 };
        for (int i = 0; i < 200; i++) tv.Nodes.Add("no " + i).Nodes.Add("filho");
        f.Controls.Add(tv);
        var dgv = new DataGridView { Width = 300, Height = 200 };
        dgv.Columns.Add("a", "A"); dgv.Columns.Add("b", "B"); for (int i = 0; i < 50; i++) dgv.Rows.Add("x", "y");
        f.Controls.Add(dgv);
        var link = new LinkLabel { Text = "link" }; f.Controls.Add(link);
        var erro = new TextBox { BackColor = Color.FromArgb(255, 200, 200) };   // cor DE PROPOSITO: nao pode mudar
        f.Controls.Add(erro);

        var host = new Panel { Dock = DockStyle.Fill, AutoScroll = true }; f.Controls.Add(host);
        int n = 0;
        for (int i = 0; i < 400; i++)
        {
            var p = new Panel { Width = 280, Height = 60, Top = i * 62 };
            p.Controls.Add(new Label { Text = "rotulo " + i, Left = 2 });
            p.Controls.Add(new TextBox { Left = 60 });
            var cb = new ComboBox { Left = 160, DropDownStyle = ComboBoxStyle.DropDownList }; cb.Items.Add("a"); p.Controls.Add(cb);
            p.Controls.Add(new Button { Text = "ok", Left = 230, Width = 40 });
            host.Controls.Add(p);
            n += 5;
        }
        // --- designer de tela falso (mesmos nomes de tipo do GeneXus)
        var areaDoc = new Panel { Dock = DockStyle.Bottom, Height = 200 };      // como DocumentPartControl
        f.Controls.Add(areaDoc);
        var mfe = new Artech.Packages.Editors.Web.MultiFormHtmlEditor { Dock = DockStyle.Fill };
        var frame = new System.Windows.Forms.Design.DesignerFrame { Dock = DockStyle.Fill };
        var pagina = new Artech.Patterns.WorkWithDevices.Editor.Layout.Controls.RootForm { Width = 400, Height = 150 };
        var lblDes = new Label { Text = "Nome", BackColor = Color.FromArgb(236, 240, 255) };
        var txtDes = new TextBox();
        var btnDes = new Button { Text = "Confirmar" };
        pagina.Controls.Add(lblDes); pagina.Controls.Add(txtDes); pagina.Controls.Add(btnDes);
        // estrutura real (censo 27/10): MultiFormHtmlEditor > WebLayoutEditor? > LayoutMainPanel(branco D) > DesignerFrame > OverlayControl > RootForm
        var lmp = new Artech.Patterns.WorkWithDevices.Editor.LayoutMainPanel { Dock = DockStyle.Fill, BackColor = Color.White };
        var overlay = new System.Windows.Forms.Design.DesignerFrame.OverlayControl { Dock = DockStyle.Fill };
        var tagNav = new Artech.Common.Controls.TagNavigator { Dock = DockStyle.Top, Height = 29 };
        var tagBtn = new Button { Text = "<" }; tagNav.Controls.Add(tagBtn);
        overlay.Controls.Add(pagina); frame.Controls.Add(overlay); lmp.Controls.Add(frame); lmp.Controls.Add(tagNav);
        mfe.Controls.Add(lmp); areaDoc.Controls.Add(mfe);
        pagina.BackColor = SystemColors.Control;   // no GeneXus a RootForm tem cor DEFINIDA

        // --- arvore da janela Options (desenha texto sempre preto)
        var gxTree = new Artech.FrameworkDE.Controls.GxTreeView { DrawMode = TreeViewDrawMode.OwnerDrawAll }; f.Controls.Add(gxTree);
        // --- grades com cores proprias (tipos falsos com a mesma API)
        var spg = new VisualHint.SmartPropertyGrid.PropertyGrid(); f.Controls.Add(spg);
        var tlist = new Artech.Common.Controls.Editors.CustomTree.CustomTreeList(); f.Controls.Add(tlist);

        // --- abas das partes (Crownwood falso, mesmas propriedades)
        var abas = new Crownwood.Magic.Controls.TabControl { Width = 300, Height = 30 };
        f.Controls.Add(abas);
        // --- rotulo desativado em painel, GroupBox, combo que o GeneXus repoe a preto
        var painelLbl = new Panel { Width = 200, Height = 40 };
        var lblOff = new Label { Text = "desativado", Enabled = false };
        painelLbl.Controls.Add(lblOff); f.Controls.Add(painelLbl);
        var grupo = new GroupBox { Text = "Grupo" }; f.Controls.Add(grupo);
        var comboEv = new ComboBox(); f.Controls.Add(comboEv);

        int totalAntes = Contar(f);
        Console.WriteLine("controles na janela de teste: " + totalAntes);

        // ---------------------------------------------------- passada inicial
        var sw = Stopwatch.StartNew();
        processar.Invoke(null, new object[] { f });
        sw.Stop();
        double msInicial = sw.Elapsed.TotalMilliseconds;
        Console.WriteLine("passada inicial: {0:0.0} ms  ({1:0.000} ms por controle)", msInicial, msInicial / totalAntes);

        // ---------------------------------------------------- handles (titulo, rolagem)
        f.Show(); Application.DoEvents();

        // ---------------------------------------------------- controle novo por EVENTO
        // Separar o custo do NOSSO codigo do custo do proprio WinForms (criar 101
        // janelas do Windows numa tela visivel). Linha de base: o mesmo acrescimo
        // num conteiner que a Fase2 NAO observa.
        var semGancho = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-32000, -32000),
                                   ShowInTaskbar = false, Size = new Size(400, 400) };
        semGancho.Show(); Application.DoEvents();
        var swBase = Stopwatch.StartNew();
        var basePanel = new Panel { Width = 200, Height = 40 };
        for (int i = 0; i < 100; i++) basePanel.Controls.Add(new TextBox());
        semGancho.Controls.Add(basePanel);
        swBase.Stop();

        FieldInfo ticks = fase2.GetField("_ticksTotal", BindingFlags.Static | BindingFlags.NonPublic);
        long t0 = (long)ticks.GetValue(null);
        var sw2 = Stopwatch.StartNew();
        var novo = new Panel { Width = 200, Height = 40 };
        for (int i = 0; i < 100; i++) novo.Controls.Add(new TextBox());
        host.Controls.Add(novo);
        sw2.Stop();
        double nosso = ((long)ticks.GetValue(null) - t0) * 1000.0 / Stopwatch.Frequency;
        Console.WriteLine("101 controles adicionados depois:");
        Console.WriteLine("   total com a Fase2            : {0:0.0} ms", sw2.Elapsed.TotalMilliseconds);
        Console.WriteLine("   linha de base SEM a Fase2    : {0:0.0} ms   (o proprio WinForms)", swBase.Elapsed.TotalMilliseconds);
        Console.WriteLine("   so o codigo da Fase2         : {0:0.0} ms   ({1:0.000} ms por controle)", nosso, nosso / 101);
        semGancho.Close();

        // ---------------------------------------------------- 2a passada: tudo ja visto
        var sw3 = Stopwatch.StartNew();
        processar.Invoke(null, new object[] { f });
        sw3.Stop();
        Console.WriteLine("reprocessar janela ja vista: {0:0.000} ms (deve ser ~0: marca de 'ja visto')", sw3.Elapsed.TotalMilliseconds);

        Console.WriteLine();
        Console.WriteLine("resultado:");
        // cores do tema em uso (lidas da propria Xpz.Dark)
        Type tpal = dark.GetType("Xpz.Dark.Paleta", true);
        Color escuro = (Color)tpal.GetField("FundoEntrada").GetValue(null), painel = (Color)tpal.GetField("FundoPainel").GetValue(null);
        Color linkTema = (Color)tpal.GetField("Link").GetValue(null);
        Console.WriteLine("tema em uso: entrada " + Show(escuro) + ", painel " + Show(painel));
        var tb = (TextBox)((Panel)host.Controls[0]).Controls[1];
        Check("TextBox: fundo escuro", tb.BackColor.ToArgb() == escuro.ToArgb(), Show(tb.BackColor));
        Check("TextBox: texto claro", tb.ForeColor.R > 200, Show(tb.ForeColor));
        Check("Panel: fundo de painel", ((Panel)host.Controls[0]).BackColor.ToArgb() == painel.ToArgb(), Show(((Panel)host.Controls[0]).BackColor));
        var bt = (Button)((Panel)host.Controls[0]).Controls[3];
        Check("Button: plano e escuro", bt.FlatStyle == FlatStyle.Flat && bt.BackColor.R < 80, bt.FlatStyle + " " + Show(bt.BackColor));
        var cbx = (ComboBox)((Panel)host.Controls[0]).Controls[2];
        Check("ComboBox: plano", cbx.FlatStyle == FlatStyle.Flat, cbx.FlatStyle.ToString());
        Check("TreeView: fundo escuro", tv.BackColor.ToArgb() == escuro.ToArgb(), Show(tv.BackColor));
        Check("PropertyGrid: area de propriedades escura", pg.ViewBackColor.ToArgb() == escuro.ToArgb(), Show(pg.ViewBackColor));
        Check("DataGridView: celulas escuras", dgv.DefaultCellStyle.BackColor.ToArgb() == escuro.ToArgb(), Show(dgv.DefaultCellStyle.BackColor));
        Check("LinkLabel: link na cor de destaque do tema", link.LinkColor.ToArgb() == linkTema.ToArgb(), Show(link.LinkColor));
        Check("ToolStrip: renderizador escuro", ts.Renderer.GetType().Name == "RendererEscuro", ts.Renderer.GetType().Name);
        Check("cor DE PROPOSITO preservada (rosa de erro)", erro.BackColor.ToArgb() == Color.FromArgb(255, 200, 200).ToArgb(), Show(erro.BackColor));
        var tbNovo = (TextBox)novo.Controls[50];
        Check("controle adicionado DEPOIS tambem escureceu", tbNovo.BackColor.ToArgb() == escuro.ToArgb(), Show(tbNovo.BackColor));

        Console.WriteLine();
        Console.WriteLine("designer de tela:");
        Check("raiz do designer: texto de fabrica (preto)", mfe.ForeColor.ToArgb() == SystemColors.ControlText.ToArgb(), Show(mfe.ForeColor));
        Check("moldura (DesignerFrame) escura", frame.BackColor.ToArgb() == painel.ToArgb(), Show(frame.BackColor));
        Check("area branca em volta (LayoutMainPanel) escura", lmp.BackColor.ToArgb() == painel.ToArgb(), Show(lmp.BackColor));
        Check("camada do designer (OverlayControl) escura", overlay.BackColor.ToArgb() == painel.ToArgb(), Show(overlay.BackColor));
        Check("barra de navegacao do designer: tratamento normal", tagBtn.FlatStyle == FlatStyle.Flat, tagBtn.FlatStyle.ToString());
        Check("pagina desenhada: fundo de fabrica", pagina.BackColor.ToArgb() == SystemColors.Control.ToArgb(), Show(pagina.BackColor));
        Check("label da tela: fundo do GeneXus intacto", lblDes.BackColor.ToArgb() == Color.FromArgb(236, 240, 255).ToArgb(), Show(lblDes.BackColor));
        Check("label da tela: texto preto (legivel)", lblDes.ForeColor.ToArgb() == SystemColors.ControlText.ToArgb(), Show(lblDes.ForeColor));
        Check("caixa de texto da tela: intocada", txtDes.BackColor.ToArgb() == SystemColors.Window.ToArgb(), Show(txtDes.BackColor));
        Check("botao da tela: intocado", btnDes.FlatStyle == FlatStyle.Standard, btnDes.FlatStyle.ToString());
        lblDes.BackColor = Color.FromArgb(236, 240, 255);   // o GeneXus repoe: nada nosso reage
        Check("GeneXus repoe cor na tela: nada reage", lblDes.ForeColor.ToArgb() == SystemColors.ControlText.ToArgb(), Show(lblDes.ForeColor));

        Console.WriteLine("legibilidade:");
        Check("aba ativa: texto claro", abas.TextColor.R > 200, Show(abas.TextColor));
        Check("aba inativa: legivel (>=4.5:1 no painel)", Contraste(abas.TextInactiveColor, painel) >= 4.5, Show(abas.TextInactiveColor) + " " + Contraste(abas.TextInactiveColor, painel).ToString("0.0") + ":1");
        Check("rotulo desativado: fundo transparente", lblOff.BackColor == Color.Transparent, lblOff.BackColor.Name);
        Check("GroupBox plano (titulo obedece a cor)", grupo.FlatStyle == FlatStyle.Flat, grupo.FlatStyle.ToString());
        comboEv.ForeColor = Color.Black;                     // o GeneXus repoe preto
        Check("combo que volta a preto: clareia de novo", comboEv.ForeColor.R > 200, Show(comboEv.ForeColor));
        Check("arvore de Options: desenho padrao (texto obedece a cor)", gxTree.DrawMode == TreeViewDrawMode.Normal, gxTree.DrawMode.ToString());
        Check("grade VisualHint: fundo escuro", spg.GridBackColor.ToArgb() == escuro.ToArgb(), Show(spg.GridBackColor));
        Check("grade VisualHint: texto claro", spg.GridForeColor.R > 200, Show(spg.GridForeColor));
        Check("grade VisualHint: faixa de categoria escura", spg.DrawManager.Cat1.R < 80, Show(spg.DrawManager.Cat1));
        Check("arvore DevExpress: estilo plano", tlist.LookAndFeel.Style.ToString() == "Flat" && !tlist.LookAndFeel.UseDefaultLookAndFeel, tlist.LookAndFeel.Style.ToString());
        Check("arvore DevExpress: linhas escuras e texto claro", tlist.Appearance.Row.BackColor.ToArgb() == escuro.ToArgb() && tlist.Appearance.Row.ForeColor.R > 200 && tlist.Appearance.Row.Options.UseBackColor, Show(tlist.Appearance.Row.BackColor) + " / " + Show(tlist.Appearance.Row.ForeColor));
        Check("arvore DevExpress: cabecalho escuro", tlist.Appearance.HeaderPanel.BackColor.R < 80, Show(tlist.Appearance.HeaderPanel.BackColor));

        Console.WriteLine("sintaxe do editor (fundo 30,30,30):");
        Type aj = dark.GetType("Xpz.Dark.Ajuste", true);
        MethodInfo ajustar = aj.GetMethod("Ajustar", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        var casos = new[] { Tuple.Create("palavra reservada", Color.FromArgb(0, 0, 255)), Tuple.Create("comentario", Color.FromArgb(0, 128, 0)),
                            Tuple.Create("string", Color.FromArgb(163, 21, 21)), Tuple.Create("variavel", Color.FromArgb(128, 0, 128)),
                            Tuple.Create("texto preto", Color.Black), Tuple.Create("ja legivel (laranja)", Color.FromArgb(255, 160, 40)) };
        foreach (var c in casos)
        {
            Color r = (Color)ajustar.Invoke(null, new object[] { c.Item2, "x", escuro, Color.FromArgb(222, 222, 222), 4.5 });
            double k = Contraste(r, escuro);
            double dh = Math.Abs(c.Item2.GetHue() - r.GetHue());
            bool matiz = c.Item2.GetSaturation() < 0.1 || dh < 12 || dh > 348;
            Check(c.Item1 + ": contraste e matiz", k >= 4.5 && matiz, Show(c.Item2) + " -> " + Show(r) + "  " + k.ToString("0.0") + ":1");
        }
        Color laranja = (Color)ajustar.Invoke(null, new object[] { Color.FromArgb(255, 160, 40), "x", escuro, Color.FromArgb(222, 222, 222), 4.5 });
        Check("cor ja legivel fica igual", laranja.ToArgb() == Color.FromArgb(255, 160, 40).ToArgb(), Show(laranja));

        Type ed = dark.GetType("Xpz.Dark.Editor", true);
        bool ligado = (bool)ed.GetProperty("Ligado", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).GetValue(null, null);
        Check("editor: renderizador escuro criado (Actipro carregou)", ligado, ligado.ToString());

        f.Close();

        // --- foto da tela de configuracao, pagina por pagina
        Type tela = dark.GetType("Xpz.Dark.TelaConfiguracao", true);
        var t = (Form)Activator.CreateInstance(tela, true);
        t.StartPosition = FormStartPosition.Manual; t.Location = new Point(-32000, -32000);
        t.Show(); Application.DoEvents();
        MethodInfo mostrar = tela.GetMethod("Mostrar", BindingFlags.Instance | BindingFlags.NonPublic);
        for (int i = 0; i < 5; i++)
        {
            mostrar.Invoke(t, new object[] { i }); Application.DoEvents();
            using (var bmp = new Bitmap(t.Width, t.Height))
            {
                t.DrawToBitmap(bmp, new Rectangle(0, 0, t.Width, t.Height));
                bmp.Save(System.IO.Path.Combine(a[1], "tela-config-" + i + ".png"));
            }
        }
        t.Close();
        Check("tela de configuracao abre e desenha as 5 paginas", true, a[1]);

        Console.WriteLine();
        Console.WriteLine(falhas == 0 ? "TODOS OS TESTES PASSARAM" : falhas + " FALHA(S)");
        return falhas;
    }

    static int Contar(Control c) { int n = 1; foreach (Control x in c.Controls) n += Contar(x); return n; }
    static string Show(Color c) { return string.Format("{0},{1},{2}", c.R, c.G, c.B); }
    static double Lin(int v) { double s = v / 255.0; return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4); }
    static double LR(Color c) { return 0.2126 * Lin(c.R) + 0.7152 * Lin(c.G) + 0.0722 * Lin(c.B); }
    static double Contraste(Color a, Color b) { double x = LR(a), y = LR(b); return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05); }
}

// Tipos FALSOS com os mesmos nomes completos dos do GeneXus - a Xpz.Dark decide pelo nome.
namespace Artech.Packages.Editors.Web { class MultiFormHtmlEditor : UserControl { } }
namespace System.Windows.Forms.Design { class DesignerFrame : Panel { public class OverlayControl : Control { } } }
namespace Artech.Patterns.WorkWithDevices.Editor { class LayoutMainPanel : UserControl { } }
namespace Artech.Common.Controls { class TagNavigator : UserControl { } }
namespace Artech.FrameworkDE.Controls { class GxTreeView : TreeView { } }
namespace VisualHint.SmartPropertyGrid
{
    public class DrawMgr
    {
        private Color _categoryBkgColor1 = Color.FromArgb(220, 220, 220);
        public Color Cat1 { get { return _categoryBkgColor1; } }
        public void SetCategoryBackgroundColors(Color a, Color b) { _categoryBkgColor1 = a; }
        public void SetSubCategoryBackgroundColors(Color a, Color b) { }
    }
    public class PropertyGrid : ContainerControl
    {
        public PropertyGrid() { DrawManager = new DrawMgr(); GridBackColor = Color.White; GridForeColor = Color.Black; }
        public DrawMgr DrawManager { get; set; }
        public Color GridBackColor { get; set; }
        public Color GridForeColor { get; set; }
        public Color GridColor { get; set; }
        public Color RootCategoryTextColor { get; set; }
        public Color CommentsBackColor { get; set; }
    }
}
namespace DevExpress.LookAndFeel { public enum LookAndFeelStyle { Flat, UltraFlat, Style3D, Office2003, Skin } }
namespace DevExpress.XtraTreeList
{
    public class Opcoes { public bool UseBackColor { get; set; } public bool UseForeColor { get; set; } public bool UseBorderColor { get; set; } }
    public class Aparencia { public Aparencia() { Options = new Opcoes(); } public Color BackColor { get; set; } public Color BackColor2 { get; set; } public Color ForeColor { get; set; } public Color BorderColor { get; set; } public Opcoes Options { get; set; } }
    public class Aparencias
    {
        public Aparencia Empty = new Aparencia(), Row = new Aparencia(), EvenRow = new Aparencia(), OddRow = new Aparencia(), FocusedRow = new Aparencia(),
            FocusedCell = new Aparencia(), SelectedRow = new Aparencia(), HideSelectionRow = new Aparencia(), HeaderPanel = new Aparencia(),
            FooterPanel = new Aparencia(), GroupFooter = new Aparencia(), GroupButton = new Aparencia(), Preview = new Aparencia(),
            HorzLine = new Aparencia(), VertLine = new Aparencia(), TreeLine = new Aparencia();
    }
    public class Visual { public Visual() { UseDefaultLookAndFeel = true; UseWindowsXPTheme = true; Style = DevExpress.LookAndFeel.LookAndFeelStyle.Office2003; } public bool UseDefaultLookAndFeel { get; set; } public bool UseWindowsXPTheme { get; set; } public DevExpress.LookAndFeel.LookAndFeelStyle Style { get; set; } }
    public class TreeList : Control { public TreeList() { LookAndFeel = new Visual(); Appearance = new Aparencias(); } public Visual LookAndFeel { get; set; } public Aparencias Appearance { get; set; } }
}
namespace Artech.Common.Controls.Editors.CustomTree { public class CustomTreeList : DevExpress.XtraTreeList.TreeList { } }
namespace Artech.Patterns.WorkWithDevices.Editor.Layout.Controls { class RootForm : UserControl { } }
namespace Crownwood.Magic.Controls
{
    class TabControl : Panel
    {
        public Color TextColor { get; set; }
        public Color TextInactiveColor { get; set; }
        public Color HotTextColor { get; set; }
        public Color ButtonActiveColor { get; set; }
        public Color ButtonInactiveColor { get; set; }
        public TabControl() { TextColor = Color.Black; TextInactiveColor = Color.Black; }
    }
}
