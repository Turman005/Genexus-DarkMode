// GxDarkTheme - comparador de versoes (History / Team Development > Compare, aba "Comparer").
// Porte do Comparador.cs do Xpz.Dark (itsFilipe), integrado ao motor unificado:
//   - cores da paleta do tema ativo (trocam ao vivo com o tema);
//   - o TOM de cada diferenca vem das preferencias do usuario (Tools > Options > Comparer:
//     inserido/apagado/alterado); o brilho vem do tema. Nada e gravado nas preferencias.
//   - tudo o que e trocado fica registrado e volta EXATO no modo claro (RestoreLight).
// Partes:
//   1. Arvore de diferencas (StructDiffPane: Web Layout, Variables, Structure...): os 3 eventos de
//      desenho da TreeList (celula, recuo, icones) trocados pelos nossos; erro ou modo claro = desenho
//      original do GeneXus; o claro devolve os delegates originais.
//   2. Editores de diferenca (SourceDiffPane / DetailDiffPane = faixa de linha de baixo): cor de fundo
//      das linhas do documento, estilos das marcas e tracejados do lado vazio.
//   3. Pinceis/cores em cache: SourceDiffPane (cor de linha ignorada, tracejado do orfao) e o
//      pincel do lado vazio no objeto de opcoes do comparador.
//   4. Barra de visao geral (DiffOverview): pintada num bitmap e remapeada (fundo branco -> fundo do
//      tema, caixa azul-clara da janela visivel e marcas -> tons escuros legiveis).
// Busca por TIPO e ASSINATURA (os nomes sao ofuscados no GeneXus 18), sem dependencia de compilacao com
// os tipos do comparador nem com o DevExpress (reflexao) - o teste do box usa replicas com as mesmas formas.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ActiproSoftware.SyntaxEditor;

namespace GxDarkTheme
{
    public static class ComparerTheme
    {
        const BindingFlags Inst = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        public const string Ns = "Artech.FrameworkDE.Comparer.";

        // ------------------------------------------------------------ estatisticas (log/testes)
        public static int Trees, Lines, Marks, Hatches, PaneFields, OptionSwaps, Overviews, Restored;
        public static bool Failed;

        // ------------------------------------------------------------ cores
        // cores padrao do GeneXus (se o servico de opcoes nao existir, ex.: teste do box)
        public static readonly Color DefInserted = Color.FromArgb(0xDD, 0xF7, 0xBC), DefDeleted = Color.FromArgb(0xFF, 0xC8, 0xC8), DefModified = Color.FromArgb(0xC8, 0xDC, 0xFF);

        // 0 Equal, 1 Added, 2 Deleted, 3 Modified (Artech.Architecture.Common.Comparer.NodeType)
        public static Func<int, Color> UserColor = UserColorFromOptions;

        public static Color TreeBack { get { return DarkEngine.Input; } }
        public static Color CodeBack { get { return DarkEngine.Editor; } }
        public static Color Text { get { return DarkEngine.Fg; } }

        public static Color Mix(Color a, Color b, double t)
        {
            return Color.FromArgb(255, (int)Math.Round(a.R + (b.R - a.R) * t), (int)Math.Round(a.G + (b.G - a.G) * t), (int)Math.Round(a.B + (b.B - a.B) * t));
        }

        public static bool Neutral(Color c) { double h, s, l; Cor.ToHsl(c, out h, out s, out l); return s < 0.08; }
        public static bool Light(Color c) { return !c.IsEmpty && c.A > 0 && Cor.Luminancia(c) >= 0.40; }

        // fundo escuro com o TOM da cor do usuario (verde continua verde...); cor neutra = o proprio fundo
        public static Color Tint(Color user, Color back)
        {
            if (user.IsEmpty || user.A == 0) return back;
            double h, s, l; Cor.ToHsl(user, out h, out s, out l);
            if (s < 0.08) return back;
            return Mix(back, Cor.FromHsl(h, 0.70, 0.50), 0.26);
        }

        public static HatchBrush Hatch(Color back, Color tone)
        {
            return new HatchBrush(HatchStyle.BackwardDiagonal, Mix(back, tone.IsEmpty ? DarkEngine.FgDim : tone, 0.30), back);
        }

        static object Options()
        {
            try
            {
                Type t = Type.GetType("Artech.Architecture.UI.Framework.Services.UIServices, Artech.Architecture.UI.Framework", false);
                if (t == null) return null;
                PropertyInfo pc = t.GetProperty("Comparer", BindingFlags.Static | BindingFlags.Public);
                object cmp = pc == null ? null : pc.GetValue(null, null);
                if (cmp == null) return null;
                PropertyInfo po = FindProp(cmp.GetType(), "ColorOptions");
                return po == null ? null : po.GetValue(cmp, null);
            }
            catch { return null; }
        }

        static PropertyInfo FindProp(Type t, string name)
        {
            PropertyInfo p = t.GetProperty(name, Inst);
            if (p != null) return p;
            foreach (Type i in t.GetInterfaces()) { p = i.GetProperty(name); if (p != null) return p; }
            return null;
        }

        static Color UserColorFromOptions(int type)
        {
            try
            {
                object o = Options();
                if (o != null)
                {
                    MethodInfo m = null;
                    foreach (Type i in o.GetType().GetInterfaces()) { m = i.GetMethod("GetBackColorForEditType"); if (m != null) break; }
                    if (m == null) m = o.GetType().GetMethod("GetBackColorForEditType", Inst);
                    if (m != null) return (Color)m.Invoke(o, new object[] { Enum.ToObject(m.GetParameters()[0].ParameterType, type) });
                }
            }
            catch { }
            return type == 1 ? DefInserted : type == 2 ? DefDeleted : type == 3 ? DefModified : SystemColors.Window;
        }

        // ------------------------------------------------------------ registro para voltar ao claro
        sealed class Rec
        {
            public WeakReference Target; public Func<object, object> Get; public Action<object, object> Set; public object Orig, Ours;
        }
        static readonly List<Rec> recs = new List<Rec>();
        static readonly HashSet<string> recKeys = new HashSet<string>();

        static void Record(object target, Func<object, object> get, Action<object, object> set, object ours)
        {
            object orig = get(target);
            set(target, ours);
            recs.Add(new Rec { Target = new WeakReference(target), Get = get, Set = set, Orig = orig, Ours = ours });
        }

        // ------------------------------------------------------------ entrada pelo motor (Walk)
        public static bool Is(Control c)
        {
            string n = c.GetType().FullName;
            return n != null && n.StartsWith(Ns, StringComparison.Ordinal);
        }

        // chamado a cada passada para os controles do namespace do comparador (e seus editores)
        public static void Visit(Control c)
        {
            if (!DarkEngine.On || Failed) return;
            string n = c.GetType().FullName ?? "";
            try
            {
                if (n == Ns + "StructDiffPane") AttachTree(c);
                else if (n == Ns + "DiffOverview") AttachOverview(c);
                if (n.EndsWith("DiffPane", StringComparison.Ordinal) && n != Ns + "StructDiffPane")
                {
                    if (n == Ns + "SourceDiffPane") PaneBrushes(c);
                    foreach (FieldInfo f in c.GetType().GetFields(Inst | BindingFlags.DeclaredOnly))
                    {
                        SyntaxEditor ed = null;
                        try { ed = f.GetValue(c) as SyntaxEditor; } catch { }
                        if (ed != null) Editor(ed);
                    }
                    foreach (Control k in c.Controls) { SyntaxEditor ed = k as SyntaxEditor; if (ed != null) Editor(ed); }
                }
                Seen = true;
            }
            catch (Exception ex)
            {
                DarkEngine.LogOnce("cmp:" + n + ":" + ex.GetType().Name, "Comparador: " + n + ": " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        // ------------------------------------------------------------ editores de diferenca
        static readonly HashSet<SyntaxEditor> loggedEd = new HashSet<SyntaxEditor>();

        public static void Editor(SyntaxEditor ed)
        {
            if (ed.Document == null) return;
            int[] n = Document(ed.Document);
            if (n[0] + n[1] + n[2] == 0) return;
            try { ed.Invalidate(); } catch { }
            if (loggedEd.Add(ed))
                DarkEngine.Log("Comparador: editor de diferencas (" + (ed.Parent == null ? "?" : ed.Parent.GetType().Name) + ") escurecido: " + n[0] + " linha(s), " + n[1] + " marca(s), " + n[2] + " tracejado(s)");
        }

        static Color DarkOf(Color c) { return Neutral(c) ? Color.Empty : Tint(c, CodeBack); }

        // o trabalho sobre o documento (testavel no box: o Document do Actipro funciona sem o editor)
        public static int[] Document(Document doc)
        {
            int lines = 0, marks = 0, hatches = 0;
            foreach (DocumentLine dl in doc.Lines)
            {
                Color c = dl.BackColor;
                if (!Light(c)) continue;
                Record(dl, o => ((DocumentLine)o).BackColor, (o, v) => ((DocumentLine)o).BackColor = (Color)v, DarkOf(c));
                lines++;
            }
            foreach (SpanIndicatorLayer layer in doc.SpanIndicatorLayers)
                for (int i = 0; i < layer.Count; i++)
                {
                    SpanIndicator si = layer[i];
                    HighlightingStyleSpanIndicator hs = si as HighlightingStyleSpanIndicator;
                    if (hs != null && hs.HighlightingStyle != null)
                    {
                        HighlightingStyle st = hs.HighlightingStyle;
                        bool m = false;
                        if (Light(st.BackColor)) { Record(st, o => ((HighlightingStyle)o).BackColor, (o, v) => ((HighlightingStyle)o).BackColor = (Color)v, DarkOf(st.BackColor)); m = true; }
                        if (DarkEngine.IsDarkText(st.ForeColor)) { Record(st, o => ((HighlightingStyle)o).ForeColor, (o, v) => ((HighlightingStyle)o).ForeColor = (Color)v, Text); m = true; }
                        if (m) marks++;
                    }
                    else if (si.GetType().FullName.StartsWith(Ns, StringComparison.Ordinal))
                    {
                        FieldInfo fb = FieldOf(si.GetType(), "Brush");
                        HatchBrush br = fb == null ? null : fb.GetValue(si) as HatchBrush;
                        if (br != null && Light(br.BackgroundColor))
                        {
                            Record(si, o => fb.GetValue(o), (o, v) => fb.SetValue(o, v), Hatch(CodeBack, Neutral(br.BackgroundColor) ? Color.Empty : br.BackgroundColor));
                            hatches++;
                        }
                    }
                }
            Lines += lines; Marks += marks; Hatches += hatches;
            return new[] { lines, marks, hatches };
        }

        // SourceDiffPane: unicos campos Color (linha ignorada) e Brush (orfao incluido)
        static void PaneBrushes(Control p)
        {
            FieldInfo ign = FieldOf(p.GetType(), "Color"), orf = FieldOf(p.GetType(), "Brush");
            if (ign != null)
            {
                Color c = (Color)ign.GetValue(p);
                if (Light(c)) { Record(p, o => ign.GetValue(o), (o, v) => ign.SetValue(o, v), Neutral(c) ? CodeBack : Tint(c, CodeBack)); PaneFields++; }
            }
            if (orf != null)
            {
                HatchBrush hb = orf.GetValue(p) as HatchBrush;
                SolidBrush sb = orf.GetValue(p) as SolidBrush;
                bool light = hb != null ? Light(hb.BackgroundColor) : sb != null && Light(sb.Color);
                if (light) { Record(p, o => orf.GetValue(o), (o, v) => orf.SetValue(o, v), Hatch(CodeBack, UserColor(1))); PaneFields++; }
            }
        }

        public static bool Seen;
        // uma vez por passada (depois dos formularios), so se algum comparador ja apareceu
        public static void Tick()
        {
            if (!DarkEngine.On || Failed) return;
            try { SwapOptionsBrush(); } catch (Exception ex) { DarkEngine.LogOnce("cmp:opt", "Comparador: pincel do lado vazio: " + ex.Message); }
            for (int i = trees.Count - 1; i >= 0; i--) if (trees[i].Pane.IsDisposed) trees.RemoveAt(i);
            for (int i = overviews.Count - 1; i >= 0; i--) if (overviews[i].Owner.IsDisposed) overviews.RemoveAt(i);
            if (recs.Count > 20000) recs.RemoveAll(r => !r.Target.IsAlive);
        }

        // pincel do lado vazio (OrphanEmptySideBackBrush) em cache no objeto de opcoes: achado pelo VALOR
        static FieldInfo emptyField;
        public static object OptionsOverride;   // so para teste
        static void SwapOptionsBrush()
        {
            object o = OptionsOverride ?? Options();
            if (o == null) return;
            PropertyInfo pe = FindProp(o.GetType(), "OrphanEmptySideBackBrush");
            if (pe == null) return;
            Brush cur = pe.GetValue(o, null) as Brush;
            if (cur == null) return;
            HatchBrush hb = cur as HatchBrush; SolidBrush sb = cur as SolidBrush;
            bool light = hb != null ? Light(hb.BackgroundColor) : sb != null && Light(sb.Color);
            if (!light) return;
            if (emptyField == null || emptyField.DeclaringType != o.GetType())
            {
                emptyField = null;
                for (Type t = o.GetType(); t != null && emptyField == null; t = t.BaseType)
                    foreach (FieldInfo f in t.GetFields(Inst | BindingFlags.DeclaredOnly))
                        if (typeof(Brush).IsAssignableFrom(f.FieldType) && ReferenceEquals(f.GetValue(o), cur)) { emptyField = f; break; }
                if (emptyField == null) { DarkEngine.LogOnce("cmp:empty", "Comparador: pincel do lado vazio nao encontrado (fica claro)"); return; }
            }
            FieldInfo ef = emptyField;
            Record(o, x => ef.GetValue(x), (x, v) => ef.SetValue(x, v), Hatch(TreeBack, Color.Empty));
            OptionSwaps++;
        }

        public static FieldInfo FieldOf(Type t, string typeName)
        {
            FieldInfo found = null;
            foreach (FieldInfo f in t.GetFields(Inst | BindingFlags.DeclaredOnly))
            {
                bool hit = false;
                for (Type b = f.FieldType; b != null && !hit; b = b.BaseType) hit = b.Name == typeName;
                if (!hit) continue;
                if (found != null) return null;   // dois candidatos: nao adivinha
                found = f;
            }
            return found;
        }

        static MethodInfo DrawMethod(Type t, string args)
        {
            MethodInfo found = null;
            foreach (MethodInfo m in t.GetMethods(Inst | BindingFlags.DeclaredOnly))
            {
                ParameterInfo[] p = m.GetParameters();
                if (p.Length != 2 || p[0].ParameterType != typeof(object) || p[1].ParameterType.Name != args || m.ReturnType != typeof(void)) continue;
                if (found != null) return null;
                found = m;
            }
            return found;
        }

        // o que falta na arvore desta versao do GeneXus (null = tudo presente)
        public static string Missing(Type pane)
        {
            if (FieldOf(pane, "TreeList") == null) return "campo da lista (TreeList)";
            if (FieldOf(pane, "DiffViewLines") == null) return "campo das linhas (DiffViewLines)";
            foreach (string e in new[] { "CustomDrawNodeCell", "CustomDrawNodeIndent", "CustomDrawNodeImages" })
                if (DrawMethod(pane, e + "EventArgs") == null) return "desenho " + e;
            return null;
        }

        // ------------------------------------------------------------ arvore de diferencas
        static readonly List<Tree> trees = new List<Tree>();
        static readonly HashSet<string> missingLogged = new HashSet<string>();

        static void AttachTree(Control pane)
        {
            foreach (Tree t in trees) if (ReferenceEquals(t.Pane, pane)) return;
            string miss = Missing(pane.GetType());
            if (miss != null)
            {
                if (missingLogged.Add(miss)) DarkEngine.Log("Comparador: arvore de diferencas fica com o desenho original (falta " + miss + ")");
                return;
            }
            Tree tr = Tree.Create(pane);
            trees.Add(tr);
            Trees++;
            try { tr.List.Invalidate(); } catch { }
            DarkEngine.LogOnce("cmp:tree", "Comparador: arvore de diferencas escura (desenho de celula, recuo e icones trocados)");
        }

        public sealed class Tree
        {
            public Control Pane, List;
            FieldInfo linesField;
            EventInfo[] evs = new EventInfo[3];
            Delegate[] gx = new Delegate[3], ours = new Delegate[3];
            static Size? iconSize;
            public bool Detached;

            public static Tree Create(Control pane)
            {
                Type t = pane.GetType();
                Tree a = new Tree { Pane = pane, List = (Control)FieldOf(t, "TreeList").GetValue(pane), linesField = FieldOf(t, "DiffViewLines") };
                string[] names = { "CustomDrawNodeCell", "CustomDrawNodeIndent", "CustomDrawNodeImages" };
                string[] mine = { "Cell", "Indent", "Images" };
                // monta tudo antes de trocar: ou os tres trocam, ou nenhum
                for (int i = 0; i < 3; i++)
                {
                    EventInfo ev = a.List.GetType().GetEvent(names[i]);
                    MethodInfo m = DrawMethod(t, names[i] + "EventArgs");
                    if (ev == null || m == null) throw new MissingMethodException(t.FullName, names[i]);
                    a.evs[i] = ev;
                    a.gx[i] = Delegate.CreateDelegate(ev.EventHandlerType, pane, m);
                    a.ours[i] = Delegate.CreateDelegate(ev.EventHandlerType, a, typeof(Tree).GetMethod(mine[i], Inst));
                }
                for (int i = 0; i < 3; i++) { a.evs[i].RemoveEventHandler(a.List, a.gx[i]); a.evs[i].AddEventHandler(a.List, a.ours[i]); }
                return a;
            }

            public void Detach()
            {
                if (Detached) return;
                Detached = true;
                for (int i = 0; i < 3; i++)
                {
                    try { evs[i].RemoveEventHandler(List, ours[i]); } catch { }
                    try { evs[i].AddEventHandler(List, gx[i]); } catch { }
                }
                try { if (!List.IsDisposed) List.Invalidate(); } catch { }
            }

            void Cell(object s, EventArgs e) { Draw(s, e, 0); }
            void Indent(object s, EventArgs e) { Draw(s, e, 1); }
            void Images(object s, EventArgs e) { Draw(s, e, 2); }

            static object P(object o, string n) { PropertyInfo p = o.GetType().GetProperty(n, Inst); return p == null ? null : p.GetValue(o, null); }

            void Draw(object s, EventArgs ev, int which)
            {
                if (DarkEngine.On && !Failed)
                {
                    try { Ours(ev, which); return; }
                    catch (Exception ex)
                    {
                        Exception x = ex is TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex;
                        DarkEngine.LogOnce("cmp:draw", "Comparador: desenho escuro da arvore falhou (" + x.GetType().Name + ": " + x.Message + "); volta ao desenho original");
                        Failed = true;
                    }
                }
                gx[which].DynamicInvoke(s, ev);
            }

            void Ours(EventArgs e, int which)
            {
                Type et = e.GetType();
                object node = P(e, "Node");
                Rectangle b = (Rectangle)P(e, "Bounds");
                Graphics g = (Graphics)P(e, "Graphics");
                using (Brush back = BackFor(node)) g.FillRectangle(back, b);
                if (which == 2)
                {
                    object sn = node == null ? null : P(node, "Tag");
                    Rectangle r = new Rectangle(b.Location, IconSize());
                    Icon ic = sn == null ? null : P(sn, "Icon") as Icon;
                    Image im = sn == null ? null : P(sn, "Image") as Image;
                    if (ic != null) g.DrawIcon(ic, r); else if (im != null) g.DrawImage(im, r);
                }
                Focus(node, g, b);
                if (which == 0)
                {
                    Rectangle t = b; t.Inflate(-2, 0);
                    object ap = P(e, "Appearance");
                    Font f = (ap == null ? null : P(ap, "Font") as Font) ?? List.Font;
                    TextRenderer.DrawText(g, P(e, "CellText") as string ?? "", f, t, Text,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                }
                et.GetProperty("Handled").SetValue(e, true, null);
            }

            // mesma regra do GeneXus: numero -1 = lado vazio (tracejado); senao o tom do tipo da linha
            public Brush BackFor(object node)
            {
                object lines = linesField.GetValue(Pane);
                int id = node == null ? -1 : Convert.ToInt32(P(node, "Id"));
                object line = null;
                if (lines != null && id >= 0 && id < Convert.ToInt32(P(lines, "Count")))
                    line = lines.GetType().GetMethod("get_Item", new[] { typeof(int) }).Invoke(lines, new object[] { id });
                if (line != null && Convert.ToInt32(P(line, "Number")) == -1) return Hatch(TreeBack, Color.Empty);
                Color c = line == null ? TreeBack : Tint(UserColor(Convert.ToInt32(P(line, "LineType"))), TreeBack);
                return new SolidBrush(c);
            }

            void Focus(object node, Graphics g, Rectangle b)
            {
                if (node == null || !ReferenceEquals(P(List, "FocusedNode"), node)) return;
                using (SolidBrush p = new SolidBrush(DarkEngine.Accent))
                {
                    g.FillRectangle(p, b.X, b.Y, b.Width, 1);
                    g.FillRectangle(p, b.X, b.Bottom - 1, b.Width, 1);
                }
            }

            static Size IconSize()
            {
                if (iconSize == null)
                {
                    Size s = new Size(16, 16);
                    try
                    {
                        Type t = Type.GetType("Artech.Common.Helpers.GDI.ScreenDPI, Artech.Common.Helpers", false);
                        PropertyInfo p = t == null ? null : t.GetProperty("DefaultImageSize", BindingFlags.Static | BindingFlags.Public);
                        if (p != null) s = (Size)p.GetValue(null, null);
                    }
                    catch { }
                    iconSize = s;
                }
                return iconSize.Value;
            }
        }

        // ------------------------------------------------------------ barra de visao geral
        static readonly List<OverviewFilter> overviews = new List<OverviewFilter>();

        static void AttachOverview(Control c)
        {
            if (!c.IsHandleCreated) return;
            foreach (OverviewFilter f in overviews) if (ReferenceEquals(f.Owner, c)) return;
            overviews.Add(new OverviewFilter(c));
            Overviews++;
            try { c.Invalidate(); } catch { }
            DarkEngine.LogOnce("cmp:ov", "Comparador: barra de visao geral escura (pintura remapeada)");
        }

        // remapeamento de uma pintura clara do GeneXus para o tema (puro: testado no box)
        static readonly Dictionary<int, int> remapCache = new Dictionary<int, int>();
        static int remapKey;
        public static Color RemapPixel(Color c)
        {
            Color bg = TreeBack;
            double h, s, l; Cor.ToHsl(c, out h, out s, out l);
            if (s < 0.08)
            {
                // cinzas: branco -> fundo, preto -> texto apagado (inversao suave)
                return Mix(DarkEngine.FgDim, bg, l);
            }
            if (l >= 0.60) return Mix(bg, Cor.FromHsl(h, 0.70, 0.50), 0.42);   // tons claros (marcas, caixa da janela visivel)
            return Cor.FromHsl(h, Math.Min(s, 0.80), Math.Max(l, 0.55));          // cores fortes (bordas): visiveis no escuro
        }

        public static void Remap(Bitmap bmp)
        {
            int key = TreeBack.ToArgb() ^ (DarkEngine.FgDim.ToArgb() * 31);
            if (key != remapKey) { remapCache.Clear(); remapKey = key; }
            Rectangle all = new Rectangle(0, 0, bmp.Width, bmp.Height);
            BitmapData d = bmp.LockBits(all, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            try
            {
                int n = d.Stride / 4 * bmp.Height;
                int[] px = new int[n];
                Marshal.Copy(d.Scan0, px, 0, n);
                for (int i = 0; i < n; i++)
                {
                    int v = px[i] | unchecked((int)0xFF000000), r;
                    if (!remapCache.TryGetValue(v, out r)) { r = RemapPixel(Color.FromArgb(v)).ToArgb(); remapCache[v] = r; }
                    px[i] = r;
                }
                Marshal.Copy(px, 0, d.Scan0, n);
            }
            finally { bmp.UnlockBits(d); }
        }

        public sealed class OverviewFilter : NativeWindow
        {
            public readonly Control Owner;
            bool broken;
            [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }
            [StructLayout(LayoutKind.Sequential)] struct PAINTSTRUCT { public IntPtr hdc; public bool fErase; public RECT rc; public bool fRestore; public bool fIncUpdate; [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] r; }
            [DllImport("user32.dll")] static extern IntPtr BeginPaint(IntPtr h, out PAINTSTRUCT ps);
            [DllImport("user32.dll")] static extern bool EndPaint(IntPtr h, ref PAINTSTRUCT ps);
            static MethodInfo onPaint, onPaintBg;

            public OverviewFilter(Control c)
            {
                Owner = c;
                if (onPaint == null)
                {
                    onPaint = typeof(Control).GetMethod("OnPaint", BindingFlags.NonPublic | BindingFlags.Instance);
                    onPaintBg = typeof(Control).GetMethod("OnPaintBackground", BindingFlags.NonPublic | BindingFlags.Instance);
                }
                AssignHandle(c.Handle);
                c.HandleDestroyed += delegate { try { ReleaseHandle(); } catch { } };
            }

            public void Detach() { try { ReleaseHandle(); } catch { } try { if (!Owner.IsDisposed) Owner.Invalidate(); } catch { } }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == 0x0014 /*WM_ERASEBKGND*/ && DarkEngine.On && !broken) { m.Result = (IntPtr)1; return; }
                if (m.Msg != 0x000F || m.WParam != IntPtr.Zero || broken || !DarkEngine.On || Owner.IsDisposed) { base.WndProc(ref m); return; }
                PAINTSTRUCT ps;
                IntPtr hdc = BeginPaint(m.HWnd, out ps);
                bool ok = false;
                try
                {
                    Rectangle r = Owner.ClientRectangle;
                    if (r.Width <= 0 || r.Height <= 0 || hdc == IntPtr.Zero) ok = true;
                    else
                    {
                        using (Bitmap bmp = Paint(Owner))
                        using (Graphics wg = Graphics.FromHdc(hdc)) wg.DrawImageUnscaled(bmp, 0, 0);
                        ok = true;
                    }
                }
                catch (Exception ex)
                {
                    broken = true;
                    Exception e = ex is TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex;
                    DarkEngine.Log("Comparador: pintura escura da visao geral falhou (" + e.GetType().Name + ": " + e.Message + "); volta ao desenho original");
                }
                finally
                {
                    if (!ok)
                    {
                        try
                        {
                            if (hdc != IntPtr.Zero)
                                using (Graphics wg = Graphics.FromHdc(hdc))
                                using (PaintEventArgs pe = new PaintEventArgs(wg, Owner.ClientRectangle))
                                { onPaintBg.Invoke(Owner, new object[] { pe }); onPaint.Invoke(Owner, new object[] { pe }); }
                        }
                        catch { }
                    }
                    EndPaint(m.HWnd, ref ps);
                }
                m.Result = IntPtr.Zero;
            }

            // pinta o controle (desenho original) num bitmap e remapeia para o tema
            public static Bitmap Paint(Control owner)
            {
                Rectangle r = owner.ClientRectangle;
                Bitmap bmp = new Bitmap(Math.Max(1, r.Width), Math.Max(1, r.Height), PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(bmp))
                using (PaintEventArgs pe = new PaintEventArgs(g, r))
                {
                    if (onPaint == null)
                    {
                        onPaint = typeof(Control).GetMethod("OnPaint", BindingFlags.NonPublic | BindingFlags.Instance);
                        onPaintBg = typeof(Control).GetMethod("OnPaintBackground", BindingFlags.NonPublic | BindingFlags.Instance);
                    }
                    onPaintBg.Invoke(owner, new object[] { pe });
                    onPaint.Invoke(owner, new object[] { pe });
                }
                Remap(bmp);
                return bmp;
            }
        }

        // ------------------------------------------------------------ volta ao claro (exato)
        public static void Undo()
        {
            int n = 0;
            foreach (Tree t in trees) { t.Detach(); n++; }
            trees.Clear();
            foreach (OverviewFilter f in overviews) f.Detach();
            overviews.Clear();
            // ordem inversa: o original mais antigo e o que fica
            for (int i = recs.Count - 1; i >= 0; i--)
            {
                Rec r = recs[i];
                object t = r.Target.Target;
                if (t == null) continue;
                try
                {
                    object cur = r.Get(t);
                    if (Equals(cur, r.Ours) || ReferenceEquals(cur, r.Ours)) { r.Set(t, r.Orig); n++; }
                }
                catch { }
            }
            recs.Clear();
            loggedEd.Clear();
            Failed = false;
            Restored += n;
            if (n > 0) DarkEngine.Log("Comparador: " + n + " item(ns) devolvido(s) ao original (claro)");
        }

        public static int Pending { get { return recs.Count + trees.Count + overviews.Count; } }
    }
}
