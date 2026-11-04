// Teste de replica do comparador (box: mono + xvfb). Nada roda no PC do Gabriel.
// Uso: mono TesteComparador.exe <pasta-das-previas>
using System; using System.Collections.Generic; using System.Drawing; using System.Drawing.Drawing2D; using System.Drawing.Imaging; using System.IO; using System.Windows.Forms;
using ActiproSoftware.SyntaxEditor;
using Artech.Architecture.Common.Comparer; using Artech.FrameworkDE.Comparer; using Artech.FrameworkDE.Comparer.Helpers;
using GxDarkTheme;

static class TesteComparador
{
    static int ok, fail; static double minC = 99;
    static void Check(bool c, string what) { if (c) ok++; else { fail++; Console.WriteLine("FALHA: " + what); } }
    static readonly string[] Temas8 = { "next", "meianoite", "ambar", "ametista", "ardosia", "brasa", "grafite", "oliva" };

    static Bitmap Icon16(Color c) { var b = new Bitmap(16, 16); using (var g = Graphics.FromImage(b)) { g.Clear(Color.Transparent); using (var br = new SolidBrush(c)) g.FillRectangle(br, 2, 2, 12, 12); } return b; }

    static StructDiffPane Pane(bool left)
    {
        var p = new StructDiffPane();
        Image ic = Icon16(Color.FromArgb(0x2C, 0x8D, 0xCE)), ic2 = Icon16(Color.FromArgb(0xE0, 0x9F, 0x3E));
        p.Add("CadastroUsuarioParceiro", 0, NodeType.Equal, 1, ic);
        p.Add("MainTable", 1, NodeType.Equal, 2, ic);
        p.Add(left ? "TextBlockNome" : "TextBlockNomeCompleto", 2, NodeType.Modified, 3, ic2);
        p.Add("UsuarioNome", 2, NodeType.Equal, 4, ic2);
        if (left) p.Add("", 2, NodeType.Added, -1, null); else p.Add("UsuarioParceiroCnpj", 2, NodeType.Added, 5, ic2);
        if (left) p.Add("UsuarioTelefone", 2, NodeType.Deleted, 6, ic2); else p.Add("", 2, NodeType.Deleted, -1, null);
        p.Add("TableActions", 1, NodeType.Equal, 7, ic);
        p.Add("BtnConfirmar", 2, NodeType.Modified, 8, ic2);
        p.Add("BtnFechar", 2, NodeType.Equal, 9, ic2);
        p.Tree.FocusedNode = p.Tree.Rows[2];
        return p;
    }

    // simula o DxTreeTheme: letra clara no escuro, preta no claro
    static void Appear(StructDiffPane p) { p.Tree.Row.ForeColor = DarkEngine.On ? DarkEngine.Fg : Color.Black; }

    static Bitmap RenderTree(StructDiffPane p, int w)
    {
        Appear(p);
        var b = new Bitmap(w, p.Tree.Rows.Count * p.Tree.RowH, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(b)) { g.Clear(Color.Magenta); p.Tree.Render(g, new Rectangle(0, 0, w, b.Height)); }
        return b;
    }

    static string Hash(Bitmap b)
    {
        var d = b.LockBits(new Rectangle(0, 0, b.Width, b.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        int[] px = new int[d.Stride / 4 * b.Height]; System.Runtime.InteropServices.Marshal.Copy(d.Scan0, px, 0, px.Length); b.UnlockBits(d);
        long h = 1469598103934665603; foreach (int v in px) { h ^= v; h *= 1099511628211; } return h.ToString("X16");
    }

    static double MaxLum(Bitmap b) { double m = 0; for (int y = 0; y < b.Height; y++) for (int x = 0; x < b.Width; x++) m = Math.Max(m, Cor.Luminancia(b.GetPixel(x, y))); return m; }
    static bool Same(Color a, Color b) { return a.R == b.R && a.G == b.G && a.B == b.B; }
    static bool Near(Color a, Color b, int t) { return Math.Abs(a.R - b.R) <= t && Math.Abs(a.G - b.G) <= t && Math.Abs(a.B - b.B) <= t; }

    static Document Doc()
    {
        var d = new Document(); d.Text = "Event 'Confirmar'\n    &Usuario.Nome = &Nome\n    &Usuario.Cnpj = &Cnpj\n    Commit\nEndEvent";
        d.Lines[1].BackColor = Color.FromArgb(0xC8, 0xDC, 0xFF); d.Lines[2].BackColor = Color.FromArgb(0xDD, 0xF7, 0xBC); d.Lines[3].BackColor = SystemColors.Window;
        var layer = new SpanIndicatorLayer("diff", 1000); d.SpanIndicatorLayers.Add(layer);
        layer.Add(new HighlightingStyleSpanIndicator("m", new HighlightingStyle("s", null, Color.FromArgb(0x10, 0x10, 0x10), Color.FromArgb(0xFF, 0xC8, 0xC8))), new TextRange(0, 5));
        layer.Add(new OrphanLineIndicator(new HatchBrush(HatchStyle.BackwardDiagonal, Color.FromArgb(0xB3, 0xB3, 0xB3), SystemColors.Window)), new TextRange(40, 41));
        return d;
    }

    static string Snap(Document d)
    {
        var s = new System.Text.StringBuilder();
        foreach (DocumentLine l in d.Lines) s.Append(l.BackColor.ToArgb()).Append(',');
        foreach (SpanIndicatorLayer L in d.SpanIndicatorLayers) for (int i = 0; i < L.Count; i++)
        {
            var hs = L[i] as HighlightingStyleSpanIndicator; var o = L[i] as OrphanLineIndicator;
            if (hs != null) s.Append(hs.HighlightingStyle.BackColor.ToArgb()).Append('/').Append(hs.HighlightingStyle.ForeColor.ToArgb()).Append(',');
            if (o != null) s.Append(o.B.GetHashCode()).Append(',');
        }
        return s.ToString();
    }

    [STAThread]
    static int Main(string[] a)
    {
        string outDir = a.Length > 0 ? a[0] : ".";
        Directory.CreateDirectory(outDir);
        ComparerTheme.UserColor = t => StructDiffPane.Opts.GetBackColorForEditType((NodeType)t);   // = Tools > Options > Comparer (replica)
        ComparerTheme.OptionsOverride = StructDiffPane.Opts;

        // ---------- 1. deteccao (formas do IL) ----------
        Check(ComparerTheme.Missing(typeof(StructDiffPane)) == null, "StructDiffPane: campos e desenhos achados por tipo/assinatura");

        // ---------- 2. arvore: claro de referencia ----------
        DarkEngine.On = false;
        StructDiffPane L = Pane(true), R = Pane(false);
        Bitmap light0 = RenderTree(L, 420); string hLight = Hash(light0);
        Delegate[] gxCell = L.Tree.Handlers("cell");
        Brush emptyOrig = StructDiffPane.Opts.OrphanEmptySideBackBrush;
        var src = new SourceDiffPane(); Color ignOrig = src.Ignore; Brush orfOrig = src.Orphan;
        Document doc = Doc(); string docLight = Snap(doc);

        // ---------- 3. escuro em cada tema ----------
        DarkEngine.On = true;
        bool attached = false;
        foreach (string id in Temas8)
        {
            DarkEngine.SetTheme(id);
            if (!attached) { ComparerTheme.Visit(L); ComparerTheme.Visit(R); ComparerTheme.Visit(src); ComparerTheme.Tick(); ComparerTheme.Document(doc); attached = true; }
            Check(L.Tree.Handlers("cell").Length == 1 && L.Tree.Handlers("cell")[0].Target is ComparerTheme.Tree, id + ": desenho de celula trocado pelo nosso (1 handler)");
            Bitmap d = RenderTree(L, 420);
            Color bg = DarkEngine.Input;
            Color tAdd = ComparerTheme.Tint(Color.FromArgb(0xDD, 0xF7, 0xBC), bg), tDel = ComparerTheme.Tint(Color.FromArgb(0xFF, 0xC8, 0xC8), bg), tMod = ComparerTheme.Tint(Color.FromArgb(0xC8, 0xDC, 0xFF), bg);
            int rh = L.Tree.RowH;
            Check(Same(d.GetPixel(410, 0 * rh + 10), bg), id + ": linha igual = fundo do tema " + d.GetPixel(410, 10));
            Check(Same(d.GetPixel(410, 3 * rh + 10), bg), id + ": linha igual (2)");
            Check(Same(d.GetPixel(410, 5 * rh + 10), tDel), id + ": linha apagada = tom vermelho escuro");
            Check(Same(d.GetPixel(410, 7 * rh + 10), tMod), id + ": linha alterada = tom azul escuro");
            Check(Same(d.GetPixel(410, 2 * rh), DarkEngine.Accent) && Same(d.GetPixel(410, 3 * rh - 1), DarkEngine.Accent), id + ": linha com foco = contorno na cor de destaque");
            Check(Same(d.GetPixel(2, 2 * rh + 10), tMod), id + ": recuo pintado com o tom (sem branco)");
            Check(MaxLum(d) < 0.80, id + ": nada branco na arvore (so o texto e claro) (lum max " + MaxLum(d).ToString("0.00") + ")");
            foreach (Color t in new[] { bg, tAdd, tDel, tMod })
                Check(Cor.Contraste(DarkEngine.Fg, t) >= 4.5, id + ": contraste texto/fundo " + Cor.Contraste(DarkEngine.Fg, t).ToString("0.0") + " >= 4.5 (WCAG AA)");
            foreach (Color t in new[] { bg, tAdd, tDel, tMod }) minC = Math.Min(minC, Cor.Contraste(DarkEngine.Fg, t));
            Check(Cor.Luminancia(tAdd) < 0.12 && Cor.Luminancia(tDel) < 0.12 && Cor.Luminancia(tMod) < 0.12, id + ": tons escuros (sem verde/vermelho claro)");
            double ha, sa, la; Cor.ToHsl(tAdd, out ha, out sa, out la); Check(ha > 70 && ha < 130, id + ": inserido continua verde (h=" + (int)ha + ")");
            Cor.ToHsl(tDel, out ha, out sa, out la); Check(ha < 20 || ha > 320, id + ": apagado continua vermelho (h=" + (int)ha + ")");
            // texto legivel: existem pixels de texto claros na linha alterada
            bool txt = false; for (int x = 40; x < 300 && !txt; x++) for (int y = 2 * rh + 3; y < 3 * rh - 3; y++) if (Cor.Luminancia(d.GetPixel(x, y)) > 0.45) { txt = true; break; }
            Check(txt, id + ": texto claro desenhado na linha alterada");
            // documento / faixa de baixo acompanham o tema? (sao tingidos na hora da troca: aqui so o primeiro tema)
            // barra de visao geral
            var ov = new DiffOverview(L.Lines) { Size = new Size(18, 300) };
            using (Bitmap ob = ComparerTheme.OverviewFilter.Paint(ov))
            {
                Check(Same(ob.GetPixel(9, 280), bg), id + ": visao geral: fundo branco -> fundo do tema " + ob.GetPixel(9, 280));
                Check(MaxLum(ob) < 0.45, id + ": visao geral sem branco/azul-claro (lum max " + MaxLum(ob).ToString("0.00") + ")");
                Color box = ob.GetPixel(9, 100); Cor.ToHsl(box, out ha, out sa, out la); Check(ha > 190 && ha < 225, id + ": janela visivel continua azul (h=" + (int)ha + ")");
            }
            d.Dispose();
        }

        // ---------- 4. documento (faixa de baixo / codigo), paineis e opcoes ----------
        DarkEngine.SetTheme("next");
        string docDark = Snap(doc);
        Check(docDark != docLight, "documento: linhas/marcas trocadas");
        Check(doc.Lines[1].BackColor == ComparerTheme.Tint(Color.FromArgb(0xC8, 0xDC, 0xFF), DarkEngine.Editor) || Cor.Luminancia(doc.Lines[1].BackColor) < 0.12, "documento: linha alterada escura " + doc.Lines[1].BackColor);
        Check(Cor.Luminancia(doc.Lines[2].BackColor) < 0.12, "documento: linha inserida (faixa verde-clara) escura " + doc.Lines[2].BackColor);
        Check(doc.Lines[3].BackColor.IsEmpty, "documento: linha branca (igual) = sem fundo proprio");
        var hsi = (HighlightingStyleSpanIndicator)doc.SpanIndicatorLayers[0][0];
        Check(Cor.Luminancia(hsi.HighlightingStyle.BackColor) < 0.12 && Same(hsi.HighlightingStyle.ForeColor, DarkEngine.Fg), "marca: fundo escuro e letra do tema");
        var oi = (OrphanLineIndicator)doc.SpanIndicatorLayers[0][1];
        Check(oi.B is HatchBrush && Cor.Luminancia(((HatchBrush)oi.B).BackgroundColor) < 0.12, "tracejado do orfao escuro");
        Check(Cor.Luminancia(src.Ignore) < 0.12 && Cor.Luminancia(((HatchBrush)src.Orphan).BackgroundColor) < 0.12, "SourceDiffPane: cor ignorada e orfao escuros");
        Check(!ReferenceEquals(StructDiffPane.Opts.OrphanEmptySideBackBrush, emptyOrig) && Cor.Luminancia(((HatchBrush)StructDiffPane.Opts.OrphanEmptySideBackBrush).BackgroundColor) < 0.12, "opcoes: pincel do lado vazio escuro (so em memoria)");
        Check(ComparerTheme.Document(doc)[0] == 0, "segunda passada nao troca de novo (idempotente)");
        ComparerTheme.Tick(); ComparerTheme.Visit(src);

        // ---------- 5. cor do usuario respeitada (Tools > Options > Comparer) ----------
        Func<int, Color> keep = ComparerTheme.UserColor;
        ComparerTheme.UserColor = t => t == 3 ? Color.FromArgb(0xE6, 0xC8, 0xFF) : keep(t);   // alterado = lilas
        using (Bitmap d = RenderTree(L, 420))
        {
            double h, s, l; Cor.ToHsl(d.GetPixel(410, 7 * L.Tree.RowH + 10), out h, out s, out l);
            Check(h > 255 && h < 300, "cor do usuario: alterado lilas -> tom lilas escuro (h=" + (int)h + ")");
        }
        ComparerTheme.UserColor = keep;

        // ---------- 6. modo claro com o tema ligado na arvore = desenho original ----------
        DarkEngine.On = false;
        using (Bitmap d = RenderTree(L, 420)) Check(Hash(d) == hLight, "On=false: arvore usa o desenho original do GeneXus");
        DarkEngine.On = true;

        // ---------- 7. volta ao claro EXATA ----------
        ComparerTheme.Undo();
        DarkEngine.On = false;
        Check(L.Tree.Handlers("cell").Length == 1 && L.Tree.Handlers("cell")[0].Method == gxCell[0].Method && ReferenceEquals(L.Tree.Handlers("cell")[0].Target, L), "claro: delegate original do GeneXus devolvido");
        Check(L.Tree.HandlerCount == 3 && R.Tree.HandlerCount == 3, "claro: 3 handlers por arvore, nenhum nosso");
        using (Bitmap d = RenderTree(L, 420)) Check(Hash(d) == hLight, "claro: arvore identica ao original (hash)");
        Check(Snap(doc) == docLight, "claro: documento identico (linhas, marcas, tracejado)");
        Check(ReferenceEquals(StructDiffPane.Opts.OrphanEmptySideBackBrush, emptyOrig), "claro: pincel do lado vazio original (mesma instancia)");
        Check(src.Ignore == ignOrig && ReferenceEquals(src.Orphan, orfOrig), "claro: campos do SourceDiffPane originais");
        Check(ComparerTheme.Pending == 0, "claro: nada pendente");

        // ---------- 8. troca de tema ao vivo (claro -> outro tema -> claro) ----------
        DarkEngine.On = true; DarkEngine.SetTheme("brasa");
        ComparerTheme.Visit(L); ComparerTheme.Document(doc); ComparerTheme.Tick();
        using (Bitmap d = RenderTree(L, 420)) Check(Same(d.GetPixel(410, 5 * L.Tree.RowH + 10), ComparerTheme.Tint(Color.FromArgb(0xFF, 0xC8, 0xC8), DarkEngine.Input)), "troca ao vivo: brasa aplicado");
        string docBrasa = Snap(doc);
        ComparerTheme.Undo(); DarkEngine.SetTheme("oliva"); ComparerTheme.Visit(L); ComparerTheme.Document(doc);
        Check(Snap(doc) != docBrasa && Snap(doc) != docLight, "troca ao vivo: documento recalculado para o novo tema");
        ComparerTheme.Undo(); DarkEngine.On = false;
        using (Bitmap d = RenderTree(L, 420)) Check(Hash(d) == hLight, "troca ao vivo + claro: arvore identica ao original");
        Check(Snap(doc) == docLight, "troca ao vivo + claro: documento identico");

        // ---------- 9. erro no desenho -> volta ao desenho do GeneXus (sem tela quebrada) ----------
        DarkEngine.On = true; DarkEngine.SetTheme("next");
        var bad = Pane(true); ComparerTheme.Visit(bad);
        bad.Lines.Explode = true;
        bool threw = false; string hb = null;
        try { using (Bitmap d = RenderTree(bad, 420)) hb = Hash(d); } catch { threw = true; }
        Check(!threw && ComparerTheme.Failed, "erro no nosso desenho: sem excecao e parte marcada como falha");
        bad.Lines.Explode = false;
        string hbLight; using (Bitmap d = RenderTree(Pane(true), 420)) hbLight = Hash(d);   // mesma arvore, nunca ligada = desenho do GeneXus
        Check(hb == hbLight, "erro no nosso desenho: celulas pintadas pelo desenho original do GeneXus");
        ComparerTheme.Undo();
        Check(!ComparerTheme.Failed, "Undo limpa o estado de falha");

        // ---------- 10. previas ----------
        foreach (string id in new[] { "next", "meianoite", "ambar", "ametista", "ardosia", "brasa", "grafite", "oliva", "claro" })
        {
            bool dark = id != "claro";
            DarkEngine.On = dark; if (dark) DarkEngine.SetTheme(id);
            StructDiffPane pl = Pane(true), pr = Pane(false); Document dd = Doc();
            if (dark) { ComparerTheme.Visit(pl); ComparerTheme.Visit(pr); ComparerTheme.Document(dd); }
            using (Bitmap img = Preview.Draw(pl, pr, dd, dark, id)) img.Save(Path.Combine(outDir, "comparer-" + id + ".png"), ImageFormat.Png);
            ComparerTheme.Undo();
        }
        // antes x depois (bug do print: arvore branca com letra clara)
        DarkEngine.On = true; DarkEngine.SetTheme("next");
        { StructDiffPane pl = Pane(true), pr = Pane(false); Document dd = Doc();
          using (Bitmap img = Preview.Draw(pl, pr, dd, true, "next (2.0.3 - antes)", true)) img.Save(Path.Combine(outDir, "comparer-antes-2.0.3.png"), ImageFormat.Png); }

        Console.WriteLine("contraste minimo texto/fundo (8 temas, todas as cores de diferenca): " + minC.ToString("0.00"));
        Console.WriteLine("ok=" + ok + " falhas=" + fail);
        foreach (string l in DarkEngine.LogLines) Console.WriteLine("  log: " + l);
        Console.WriteLine(fail == 0 ? "TODOS OS TESTES DO COMPARADOR OK" : "HA FALHAS NO COMPARADOR");
        return fail == 0 ? 0 : 1;
    }
}
