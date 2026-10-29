// Comparador de versoes (History > Compare): a arvore de diferencas REAL do GeneXus
// (StructDiffPane) com linhas igual, inserida, apagada, alterada e lado vazio, e o seletor
// de revisao (ComboBoxEdit do DevExpress). As cores vem de um servico de comparacao falso
// com as cores-padrao CLARAS do GeneXus (como na KB). Confere por pixel: nada claro, cada
// tipo de linha com o seu tom, e a preferencia do usuario intocada. Foto: comparador.png.
using System; using System.Collections.Generic; using System.Drawing; using System.Drawing.Drawing2D; using System.IO; using System.Reflection; using System.Runtime.Remoting; using System.Runtime.Remoting.Messaging; using System.Runtime.Remoting.Proxies; using System.Windows.Forms;
using Artech.Architecture.Common.Comparer; using Artech.Architecture.Common.Services; using Artech.Architecture.UI.Framework.Services; using Artech.Architecture.UI.Framework.Services.Comparer;

/// <summary>Implementa qualquer interface: so responde o que o teste precisa.</summary>
class Falso : RealProxy, IRemotingTypeInfo
{
    readonly Func<string, object[], object> _resp;
    public Falso(Type t, Func<string, object[], object> resp) : base(t) { _resp = resp; }
    public string TypeName { get { return GetProxiedType().FullName; } set { } }
    public bool CanCastTo(Type t, object o) { return true; }
    public override IMessage Invoke(IMessage msg)
    {
        var m = (IMethodCallMessage)msg;
        Type ret = ((MethodInfo)m.MethodBase).ReturnType;
        object r = _resp(m.MethodName, m.Args);
        if (r == null && ret.IsValueType && ret != typeof(void)) r = Activator.CreateInstance(ret);
        return new ReturnMessage(r, null, 0, m.LogicalCallContext, m);
    }
}

static class T
{
    const string GX = @"C:\Program Files (x86)\GeneXus\GeneXus18-U10";
    static int falhas;
    static void Check(string n, bool ok, string d) { Console.WriteLine("  {0} {1,-58} {2}", ok ? "[ok] " : "FALHA", n, d); if (!ok) falhas++; }

    // cores-padrao do GeneXus (Tools > Options > Comparer): claras
    static readonly Color Inserido = Color.FromArgb(0xDD, 0xF7, 0xBC), Apagado = Color.FromArgb(0xFF, 0xC8, 0xC8), Alterado = Color.FromArgb(0xC8, 0xDC, 0xFF);

    [STAThread] static int Main(string[] a)
    {
        AppDomain.CurrentDomain.AssemblyResolve += (s, e) => { string p = Path.Combine(GX, new AssemblyName(e.Name).Name + ".dll"); return File.Exists(p) ? Assembly.LoadFrom(p) : null; };
        return Rodar(a[0], a[1]);
    }

    static bool Escura(Color c) { return !c.IsEmpty && (c.R + c.G + c.B) / 3 < 100; }
    static string Rgb(Color c) { return c.IsEmpty ? "(vazia)" : c.R + "," + c.G + "," + c.B; }

    static Color CorDe(NodeType t) { return t == NodeType.Added ? Inserido : t == NodeType.Deleted ? Apagado : t == NodeType.Modified ? Alterado : SystemColors.Window; }

    static void ServicoFalso()
    {
        var cores = (IComparerColors)new Falso(typeof(IComparerColors), (n, a) =>
        {
            switch (n)
            {
                case "GetBackColorForEditType": return CorDe((NodeType)a[0]);
                case "GetBackBrushForEditType": return new SolidBrush(CorDe((NodeType)a[0]));
                case "get_OrphanEmptySideBackBrush": return new HatchBrush(HatchStyle.BackwardDiagonal, Color.FromArgb(0xB3, 0xB3, 0xB3), SystemColors.Window);
                case "GetBrushForFocusedLine": return a[0];
                case "get_EqualBackColor": return SystemColors.Window;
                case "get_EqualBackBrush": return SystemBrushes.Window;
            }
            return null;
        }).GetTransparentProxy();
        object comparador = new Falso(typeof(IGxService), (n, a) => n == "get_ColorOptions" ? cores : null).GetTransparentProxy();
        Guid guid = (Guid)typeof(UIServicesGuid).GetField("SComparerService", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).GetValue(null);
        var prov = (IGxServiceProvider)new Falso(typeof(IGxServiceProvider), (n, a) => n == "GetService" && a.Length == 1 && (Guid)a[0] == guid ? comparador : null).GetTransparentProxy();
        Services.Provider = prov;
    }

    static int Rodar(string dll, string saida)
    {
        Assembly dark = Assembly.LoadFrom(dll);
        BindingFlags E = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, I = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        ServicoFalso();
        Check("servico falso entrega as cores claras do GeneXus", UIServices.Comparer.ColorOptions.GetBackColorForEditType(NodeType.Added) == Inserido, "");

        var cfg = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { { "Tema", "ardosia" } };
        dark.GetType("Xpz.Dark.Fase2").GetMethod("Iniciar", E).Invoke(null, new object[] { cfg });

        // ---------------------------------------------- arvore de diferencas real
        Type tp = Type.GetType("Artech.FrameworkDE.Comparer.StructDiffPane, Artech.FrameworkDE", true);
        var pane = (Control)Activator.CreateInstance(tp, (DiffViewPart)0, new[] { new DiffColumn("Name", 200) }, false);
        pane.Dock = DockStyle.Fill;
        dynamic tl = tp.GetField("treList", I).GetValue(pane);
        var linhas = (DiffViewLines)tp.GetField("m_ViewLines", I).GetValue(pane);
        // (tipo, texto); null = lado vazio (linha que so existe do outro lado)
        var dados = new List<Tuple<NodeType?, string>>
        {
            Tuple.Create((NodeType?)NodeType.Equal, "RootForm"), Tuple.Create((NodeType?)NodeType.Added, "Inserida"),
            Tuple.Create((NodeType?)NodeType.Deleted, "Apagada"), Tuple.Create((NodeType?)NodeType.Modified, "Alterada"),
            Tuple.Create((NodeType?)null, ""), Tuple.Create((NodeType?)NodeType.Equal, "ErrorViewer1"),
        };
        int n = 0;
        foreach (var d in dados)
        {
            if (d.Item1 == null) linhas.AddEmptyLine(); else linhas.AddLine(d.Item2, ++n, d.Item1.Value);
            tl.AppendNode(new object[] { d.Item2 }, null);
        }
        tl.EndUnboundLoad();

        var f = new Form { Text = "Comparer", StartPosition = FormStartPosition.Manual, Location = new Point(100, 100), ClientSize = new Size(420, 200), ShowInTaskbar = false, TopMost = true };
        var cima = new Panel { Dock = DockStyle.Top, Height = 24 };
        Type tc = Type.GetType("DevExpress.XtraEditors.ComboBoxEdit, DevExpress.XtraEditors.v7.3", true);
        var revisao = (Control)Activator.CreateInstance(tc);
        revisao.Dock = DockStyle.Fill; revisao.Text = "Revision 7 - 10/24/2025 8:43 AM";
        cima.Controls.Add(revisao);
        f.Controls.Add(pane); f.Controls.Add(cima);
        f.Show();
        dark.GetType("Xpz.Dark.Fase2").GetMethod("Processar", E).Invoke(null, new object[] { f });
        for (int i = 0; i < 20; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(25); }
        f.Refresh(); Application.DoEvents();

        Rectangle area = f.RectangleToScreen(f.ClientRectangle);
        using (var foto = new Bitmap(area.Width, area.Height))
        {
            using (var g = Graphics.FromImage(foto)) g.CopyFromScreen(area.Location, Point.Empty, area.Size);
            foto.Save(Path.Combine(saida, "comparador.png"));

            // uma coluna de pixels na direita da arvore (sem texto): o fundo de cada linha
            Rectangle arv = f.RectangleToClient(((Control)tl).RectangleToScreen(((Control)tl).ClientRectangle));
            int x = arv.Right - 20, claros = 0;
            var fundos = new List<Color>();
            Color destaque = (Color)dark.GetType("Xpz.Dark.Paleta").GetField("Link", E).GetValue(null);   // contorno da linha com foco
            for (int y = arv.Top + 1; y < arv.Bottom - 1; y++)
            {
                Color c = foto.GetPixel(x, y);
                if ((c.R + c.G + c.B) / 3 > 160 && c.ToArgb() != destaque.ToArgb()) claros++;
                if (fundos.Count == 0 || fundos[fundos.Count - 1].ToArgb() != c.ToArgb()) fundos.Add(c);
            }
            Check("arvore: nenhum fundo claro", claros == 0, claros + " pixels claros");
            Check("linha inserida: tom verde", fundos.Exists(c => c.G > c.R + 12 && c.G > c.B + 12), "");
            Check("linha apagada: tom vermelho", fundos.Exists(c => c.R > c.G + 12 && c.R > c.B + 12), "");
            Check("linha alterada: tom azul", fundos.Exists(c => c.B > c.R + 12 && c.B > c.G + 4), "");

            Rectangle rv = f.RectangleToClient(revisao.RectangleToScreen(revisao.ClientRectangle));
            int clarosRev = 0;
            for (int xx = rv.Left + 2; xx < rv.Right - 30; xx += 3) { Color c = foto.GetPixel(xx, rv.Top + rv.Height / 2 + 5); if ((c.R + c.G + c.B) / 3 > 160) clarosRev++; }
            Check("seletor de revisao (ComboBoxEdit) escuro", clarosRev < 4, clarosRev + " pixels claros");
        }
        // ---------------------------------------------- editor de diferencas (Events, Rules...)
        // O documento montado como a SourceDiffPane monta (IL, 28/10): marca de estilo por linha
        // diferente (fundo da preferencia, letra vazia), tracejado do lado vazio e linha ignorada
        // com cor propria. No print da U14 a marca verde-clara deixava a letra apagada.
        var doc = new ActiproSoftware.SyntaxEditor.Document();
        doc.Text = "Event 'Confirmar'\r\n    Msg('inserida')\r\n\r\n    Msg('ignorada')\r\nEndEvent";
        var camada = new ActiproSoftware.SyntaxEditor.SpanIndicatorLayer("Diff", 1000);
        doc.SpanIndicatorLayers.Add(camada);
        var linha = doc.Lines[1];
        var marca = new ActiproSoftware.SyntaxEditor.HighlightingStyleSpanIndicator("", new ActiproSoftware.SyntaxEditor.HighlightingStyle("", "", Color.Empty, Inserido));
        camada.Add(marca, new ActiproSoftware.SyntaxEditor.TextRange(linha.StartOffset, linha.EndOffset));
        Type tOrfa = Type.GetType("Artech.FrameworkDE.Comparer.Helpers.OrphanLineIndicator, Artech.FrameworkDE", true);
        var orfa = (ActiproSoftware.SyntaxEditor.SpanIndicator)Activator.CreateInstance(tOrfa, new HatchBrush(HatchStyle.BackwardDiagonal, Color.FromArgb(0xB3, 0xB3, 0xB3), Inserido));
        camada.Add(orfa, new ActiproSoftware.SyntaxEditor.TextRange(doc.Lines[2].StartOffset, doc.Lines[2].StartOffset));
        doc.Lines[3].BackColor = Color.FromArgb(0xFF, 0xBC, 0xBC);   // linha ignorada (m_IgnoreColor)
        var cont = (int[])dark.GetType("Xpz.Dark.Comparador").GetMethod("Documento", E).Invoke(null, new object[] { doc });
        Color fundoMarca = marca.HighlightingStyle.BackColor;
        Check("editor: marca da linha inserida escura e verde", Escura(fundoMarca) && fundoMarca.G > fundoMarca.R + 8, Rgb(fundoMarca));
        Check("editor: letra da marca segue a do tema (vazia)", marca.HighlightingStyle.ForeColor.IsEmpty, "");
        var pincel = tOrfa.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)[0].GetValue(orfa) as HatchBrush;
        Check("editor: tracejado do lado vazio escuro", pincel != null && Escura(pincel.BackgroundColor) && Escura(pincel.ForegroundColor), pincel == null ? "sem pincel" : Rgb(pincel.BackgroundColor) + " / " + Rgb(pincel.ForegroundColor));
        Check("editor: linha ignorada escura", Escura(doc.Lines[3].BackColor), Rgb(doc.Lines[3].BackColor));
        Check("editor: contagem (1 linha, 1 marca, 1 tracejado)", cont[0] == 1 && cont[1] == 1 && cont[2] == 1, cont[0] + "/" + cont[1] + "/" + cont[2]);
        var n2 = (int[])dark.GetType("Xpz.Dark.Comparador").GetMethod("Documento", E).Invoke(null, new object[] { doc });
        Check("editor: segunda passada nao mexe (idempotente)", n2[0] + n2[1] + n2[2] == 0, n2[0] + "/" + n2[1] + "/" + n2[2]);

        MethodInfo ligada = dark.GetType("Xpz.Dark.Disjuntor").GetMethod("Ligada", E);
        foreach (string parte in new[] { "grades", "comparador de versoes", "janelas" })
            Check("parte '" + parte + "' segue ligada (nenhum erro)", (bool)ligada.Invoke(null, new object[] { parte }), "");
        Check("preferencia do usuario intocada (cor do servico)", UIServices.Comparer.ColorOptions.GetBackColorForEditType(NodeType.Added) == Inserido, "");
        f.Close();
        Console.WriteLine(falhas == 0 ? "TODOS OS TESTES PASSARAM" : falhas + " FALHA(S)");
        return falhas;
    }
}
