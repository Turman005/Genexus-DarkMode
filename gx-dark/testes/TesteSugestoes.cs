// Lista de sugestoes do editor (IntelliPrompt): a IntelliPromptMemberListBox REAL do Actipro
// (o SyntaxEditor pede licenca fora do GeneXus; a lista nao), dentro de um popup que imita o
// do Actipro (fundo SystemColors.Control + borda 3D). Confere fundo, selecao, texto e quadro.
using System; using System.Collections.Generic; using System.Drawing; using System.IO; using System.Reflection; using System.Windows.Forms;

static class T
{
    const string GX = @"C:\Program Files (x86)\GeneXus\GeneXus18-U10";
    static int falhas;
    static void Check(string n, bool ok, string d) { Console.WriteLine("  {0} {1,-56} {2}", ok ? "[ok] " : "FALHA", n, d); if (!ok) falhas++; }
    static string S(Color c) { return c.R + "," + c.G + "," + c.B; }
    static bool Perto(Color a, Color b, int tol) { return Math.Abs(a.R - b.R) <= tol && Math.Abs(a.G - b.G) <= tol && Math.Abs(a.B - b.B) <= tol; }

    sealed class PopupFalso : Form
    {
        public PopupFalso() { FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true; StartPosition = FormStartPosition.Manual; Padding = new Padding(2); }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.FillRectangle(SystemBrushes.Control, ClientRectangle);
            ControlPaint.DrawBorder3D(e.Graphics, ClientRectangle, Border3DStyle.Raised);
        }
    }

    static Bitmap Icone(Color c) { var b = new Bitmap(16, 16); using (var g = Graphics.FromImage(b)) { g.Clear(Color.Transparent); using (var br = new SolidBrush(c)) g.FillRectangle(br, 3, 3, 10, 10); } return b; }

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
        Func<string, Color> P = n => (Color)dark.GetType("Xpz.Dark.Paleta").GetField(n, E).GetValue(null);

        Assembly se = Assembly.LoadFrom(Path.Combine(GX, "ActiproSoftware.SyntaxEditor.Net20.dll"));
        Type tLista = se.GetType("ActiproSoftware.SyntaxEditor.IntelliPromptMemberListBox", true);
        Type tItem = se.GetType("ActiproSoftware.SyntaxEditor.IntelliPromptMemberListItem", true);
        var lista = (ListBox)Activator.CreateInstance(tLista, true);
        lista.Dock = DockStyle.Fill; lista.BorderStyle = BorderStyle.None; lista.DrawMode = DrawMode.OwnerDrawFixed; lista.ItemHeight = 18;
        lista.IntegralHeight = false;
        var il = new ImageList { ImageSize = new Size(16, 16), ColorDepth = ColorDepth.Depth32Bit };
        foreach (var c in new[] { Color.Orange, Color.DeepSkyBlue, Color.LimeGreen }) il.Images.Add(Icone(c));
        tLista.GetProperty("ImageList").SetValue(lista, il, null);
        string[] nomes = { "Load", "Save", "Delete", "Check", "Insert", "Update" };
        for (int i = 0; i < nomes.Length; i++) lista.Items.Add(Activator.CreateInstance(tItem, nomes[i], i % 3));

        var popup = new PopupFalso { Size = new Size(200, 150), Location = new Point(120, 120) };
        popup.Controls.Add(lista);
        popup.Show(); Application.DoEvents();
        lista.SelectedIndex = 1;
        dark.GetType("Xpz.Dark.Fase2").GetMethod("Processar", E).Invoke(null, new object[] { popup });
        popup.Refresh();
        for (int i = 0; i < 8; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(40); }

        Rectangle r = popup.RectangleToScreen(popup.ClientRectangle);
        using (var b = new Bitmap(r.Width, r.Height))
        {
            using (var g = Graphics.FromImage(b)) g.CopyFromScreen(r.Location, Point.Empty, r.Size);
            b.Save(Path.Combine(saida, "sugestoes.png"));
            Point origem = lista.Location;   // dentro do popup (padding 2)
            Color fundoItem = b.GetPixel(origem.X + 150, origem.Y + 9);           // item 0, a direita do texto
            Color fundoSel = b.GetPixel(origem.X + 150, origem.Y + 18 + 9);       // item 1 (selecionado)
            Color vazio = b.GetPixel(origem.X + 100, origem.Y + 18 * 6 + 10);     // abaixo do ultimo item
            Color quadro = b.GetPixel(1, 60);                                       // borda 3D do popup
            Check("fundo dos itens = fundo de entrada do tema", Perto(fundoItem, P("FundoEntrada"), 6), S(fundoItem) + " (tema " + S(P("FundoEntrada")) + ")");
            Check("item selecionado = selecao do tema", Perto(fundoSel, P("Selecao"), 6), S(fundoSel) + " (tema " + S(P("Selecao")) + ")");
            Check("area abaixo dos itens escura", Paleta(vazio) < 0.2, S(vazio));
            Check("quadro do popup sem a borda 3D clara", Paleta(quadro) < 0.3, S(quadro));
            int claros = 0;
            for (int x = origem.X + 22; x < origem.X + 60; x++) for (int y = origem.Y + 2; y < origem.Y + 16; y++) if (Paleta(b.GetPixel(x, y)) > 0.55) claros++;
            Check("texto do item legivel (pontos claros sobre o escuro)", claros > 8, claros + " pontos de texto");
            int icone = 0;
            for (int x = origem.X + 3; x < origem.X + 17; x++) for (int y = origem.Y + 3; y < origem.Y + 15; y++) { Color c = b.GetPixel(x, y); if (c.R > 200 && c.G > 120 && c.B < 80) icone++; }
            Check("icone do item desenhado", icone > 20, icone + " pontos laranja");
        }
        popup.Close();

        // Dica de parametros / QuickInfo: MarkupLabel real do Actipro, fundo Info como no editor
        Type tMl = Assembly.LoadFrom(Path.Combine(GX, "ActiproSoftware.WinUICore.Net20.dll")).GetType("ActiproSoftware.MarkupLabel.MarkupLabel", true);
        var ml = (Control)Activator.CreateInstance(tMl);
        ml.BackColor = SystemColors.Info; ml.ForeColor = SystemColors.InfoText; ml.Dock = DockStyle.Fill;
        ml.Text = "<b>Call</b>(&amp;ProdutoId, &amp;ProdutoNome)";
        var dicaF = new PopupFalso { Size = new Size(300, 24), Location = new Point(120, 300) };
        dicaF.Padding = new Padding(1);
        dicaF.Controls.Add(ml);
        dicaF.Show(); Application.DoEvents();
        dark.GetType("Xpz.Dark.Fase2").GetMethod("Processar", E).Invoke(null, new object[] { dicaF });
        ml.BackColor = SystemColors.Info;   // o Actipro repoe a cor ao mostrar de novo
        dicaF.Refresh();
        for (int i = 0; i < 6; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(40); }
        Rectangle rd = ml.RectangleToScreen(ml.ClientRectangle);
        using (var b = new Bitmap(rd.Width, rd.Height))
        {
            using (var g = Graphics.FromImage(b)) g.CopyFromScreen(rd.Location, Point.Empty, rd.Size);
            b.Save(Path.Combine(saida, "dica-editor.png"));
            int claros = 0, texto = 0, total = 0;
            for (int y = 0; y < b.Height; y++) for (int x = 0; x < b.Width; x++)
            { Color c = b.GetPixel(x, y); total++; double l = Paleta(c); if (c.R > 230 && c.G > 230 && c.B > 180) claros++; if (l > 0.55) texto++; }
            Check("dica do editor sem o fundo amarelo-claro (mesmo reposto)", claros == 0, claros + "/" + total + " pontos claros");
            Check("dica do editor com texto claro legivel", texto > 30, texto + " pontos de texto");
        }
        dicaF.Close();
        Console.WriteLine(falhas == 0 ? "TODOS OS TESTES PASSARAM" : falhas + " FALHA(S)");
        return falhas;
    }

    static double Paleta(Color c) { return (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255.0; }
}
