// Xpz.Dark - comparador de versoes (History > Compare, Comparer).
//
// De onde vem cada cor (IL do GeneXus 18 U10, 28/10):
//   - As diferencas pedem a cor a UIServices.Comparer.ColorOptions, a classe de opcoes
//     do pacote Comparer (Tools > Options > Comparer). Inserido/apagado/alterado sao
//     preferencias do usuario (verde, vermelho, azul CLAROS); linha igual e lado vazio
//     sao SystemColors.Window - branco fixo, nao ha propriedade.
//   - As propriedades de cor GRAVAM a preferencia (SetColor -> SetUserSetting). Por isso
//     nada aqui escreve nelas: o tom vem da preferencia do usuario, o brilho vem do tema,
//     e so o desenho muda.
//
// O que se faz:
//   1. Arvore de diferencas (Web Layout, Variables, Structure...): a StructDiffPane pinta
//      as celulas, o recuo e os icones em tres eventos de desenho da TreeList. Os tres
//      sao trocados por versoes nossas, que desenham o mesmo com fundo escuro. Se a
//      parte "comparador" cair, ou der erro, chamam o desenho original do GeneXus.
//   2. Editor de diferencas (Events, Rules, Source...): o GeneXus poe a cor clara em cada
//      linha do documento (DocumentLine.BackColor); a revisita do editor troca pelo tom
//      escuro. Vale tambem para o painel de detalhe da linha.
//   3. Faixas tracejadas (linha que so existe do outro lado): pinceis em cache no objeto
//      de opcoes e no painel - trocados por tracejados escuros, so em memoria.

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

using ActiproSoftware.SyntaxEditor;
using Artech.Architecture.Common.Comparer;
using Artech.Architecture.UI.Framework.Services;
using Artech.Architecture.UI.Framework.Services.Comparer;

namespace Xpz.Dark
{
    internal static class Comparador
    {
        private const BindingFlags Inst = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const string Ns = "Artech.FrameworkDE.Comparer.";

        public static bool EhArvore(Control c) { return c.GetType().FullName == Ns + "StructDiffPane"; }

        /// <summary>Editor dentro de um painel de diferencas (SourceDiffPane, DetailDiffPane).</summary>
        public static bool EhEditorDeDiferenca(Control ed)
        {
            for (Control p = ed.Parent; p != null; p = p.Parent)
            {
                string n = p.GetType().FullName;
                if (n.StartsWith(Ns, StringComparison.Ordinal) && n.EndsWith("DiffPane", StringComparison.Ordinal)) return true;
            }
            return false;
        }

        // ------------------------------------------------------------ cores
        /// <summary>
        /// Fundo escuro com o TOM da cor do usuario: verde continua verde, vermelho continua
        /// vermelho, e um tom proprio escolhido em Tools > Options tambem e respeitado.
        /// Cor neutra (branco da linha igual) = o proprio fundo.
        /// </summary>
        public static Color Tingir(Color usuario, Color fundo)
        {
            if (usuario.IsEmpty || usuario.A == 0) return fundo;
            double h, s, l;
            Ajuste.Hsl(usuario, out h, out s, out l);
            if (s < 0.08) return fundo;
            return Paleta.Misturar(fundo, Ajuste.DeHsl(h, 0.70, 0.50), 0.26);
        }

        private static Color DoUsuario(NodeType t)
        {
            // Sem o servico (fora do GeneXus, ou pacote Comparer ausente): sem tom.
            IComparerColors o;
            try { o = UIServices.Comparer == null ? null : UIServices.Comparer.ColorOptions; } catch { o = null; }
            return o == null ? Color.Empty : o.GetBackColorForEditType(t);
        }

        /// <summary>Tracejado do lado vazio: o mesmo desenho do GeneXus, em tons do tema.</summary>
        private static HatchBrush Tracejado(Color fundo, Color tom)
        {
            return new HatchBrush(HatchStyle.BackwardDiagonal, Paleta.Misturar(fundo, tom.IsEmpty ? Paleta.Texto : tom, 0.22), fundo);
        }

        // ------------------------------------------------------------ editor de diferencas
        private static readonly ConditionalWeakTable<SyntaxEditor, object> _logados = new ConditionalWeakTable<SyntaxEditor, object>();

        /// <summary>
        /// Chamado na revisita do editor (1 s). So troca o que ainda esta claro. Tres fontes de
        /// cor (IL, 28/10): a linha (DocumentLine.BackColor - linhas ignoradas e o painel de
        /// detalhe), a MARCA de cada linha diferente (HighlightingStyleSpanIndicator com um
        /// HighlightingStyle novo: fundo da preferencia, letra vazia = a do tema, clara - era o
        /// verde-claro com letra apagada do print da U14) e o tracejado do lado vazio
        /// (OrphanLineIndicator, com pincel proprio).
        /// </summary>
        public static void Linhas(SyntaxEditor ed)
        {
            if (!Disjuntor.Ligada(Parte.Comparador) || ed.Document == null) return;
            try
            {
                int[] n = Documento(ed.Document);
                int linhas = n[0], marcas = n[1], tracejados = n[2];
                PinceisDoPainel(ed);
                Opcoes();
                if (linhas + marcas + tracejados == 0) return;
                ed.Invalidate();
                object x;
                if (!_logados.TryGetValue(ed, out x))
                {
                    _logados.Add(ed, new object());
                    Control p = ed.Parent;
                    while (p != null && !p.GetType().Name.EndsWith("DiffPane", StringComparison.Ordinal)) p = p.Parent;
                    Log.Linha(string.Format("comparador: editor de diferencas ({0}) escurecido: {1} linha(s), {2} marca(s), {3} tracejado(s)",
                        p == null ? "?" : p.GetType().Name, linhas, marcas, tracejados));
                }
            }
            catch (Exception ex) { Disjuntor.Falhou(Parte.Comparador, ex); }
        }

        /// <summary>O trabalho do <see cref="Linhas"/> sobre o documento (testavel sem o editor,
        /// que pede licenca fora do GeneXus). Devolve linhas, marcas e tracejados trocados.</summary>
        internal static int[] Documento(Document doc)
        {
            int linhas = 0, marcas = 0, tracejados = 0;
            foreach (DocumentLine dl in doc.Lines)
            {
                Color c = dl.BackColor;
                if (!Claro(c)) continue;
                dl.BackColor = Escuro(c);
                linhas++;
            }
            foreach (SpanIndicatorLayer camada in doc.SpanIndicatorLayers)
                for (int i = 0; i < camada.Count; i++)
                {
                    SpanIndicator si = camada[i];
                    var hs = si as HighlightingStyleSpanIndicator;
                    if (hs != null && hs.HighlightingStyle != null)
                    {
                        HighlightingStyle st = hs.HighlightingStyle;
                        bool m = false;
                        if (Claro(st.BackColor)) { st.BackColor = Escuro(st.BackColor); m = true; }
                        if (Paleta.TextoEscuro(st.ForeColor)) { st.ForeColor = Paleta.Texto; m = true; }
                        if (m) marcas++;
                    }
                    else if (si.GetType().FullName.StartsWith(Ns, StringComparison.Ordinal))
                    {
                        FieldInfo fb = CampoDoTipo(si.GetType(), "Brush");
                        var br = fb == null ? null : fb.GetValue(si) as HatchBrush;
                        if (br != null && Claro(br.BackgroundColor))
                        {
                            fb.SetValue(si, Tracejado(Paleta.FundoCodigo, Neutro(br.BackgroundColor) ? Color.Empty : br.BackgroundColor));
                            tracejados++;
                        }
                    }
                }
            return new[] { linhas, marcas, tracejados };
        }

        private static bool Claro(Color c) { return !c.IsEmpty && c.A > 0 && Paleta.Lum(c) >= 0.45; }
        private static bool Neutro(Color c) { double h, s, l; Ajuste.Hsl(c, out h, out s, out l); return s < 0.08; }
        /// <summary>Tom da cor no fundo do editor; cor neutra (branco) = sem fundo proprio.</summary>
        private static Color Escuro(Color c) { return Neutro(c) ? Color.Empty : Tingir(c, Paleta.FundoCodigo); }

        private static readonly ConditionalWeakTable<Control, object> _paineis = new ConditionalWeakTable<Control, object>();

        /// <summary>SourceDiffPane: cor das linhas ignoradas e tracejado do orfao incluido.</summary>
        private static void PinceisDoPainel(Control ed)
        {
            Control p = ed.Parent;
            while (p != null && p.GetType().FullName != Ns + "SourceDiffPane") p = p.Parent;
            object x;
            if (p == null || _paineis.TryGetValue(p, out x)) return;
            _paineis.Add(p, new object());
            // Unicos campos Color e Brush do painel (U10: m_IgnoreColor e m_IncludedOrphanBrush).
            FieldInfo ign = CampoDoTipo(p.GetType(), "Color"), orf = CampoDoTipo(p.GetType(), "Brush");
            if (ign != null) ign.SetValue(p, Tingir((Color)ign.GetValue(p), Paleta.FundoCodigo));
            if (orf != null) orf.SetValue(p, Tracejado(Paleta.FundoCodigo, DoUsuario(NodeType.Added)));
        }

        private static HatchBrush _vazio;
        private static FieldInfo _campoVazio;
        private static Color _vazioFundo;

        /// <summary>
        /// Lado vazio (OrphanEmptySideBackBrush): pincel em cache (m_EmptyBackBrush) no objeto
        /// de opcoes, criado do branco. Nao e preferencia gravada; o GeneXus o recria se
        /// descartar - a revisita poe o escuro de novo.
        /// </summary>
        private static void Opcoes()
        {
            IComparerColors o;
            try { o = UIServices.Comparer == null ? null : UIServices.Comparer.ColorOptions; } catch { o = null; }
            if (o == null) return;
            if (_campoVazio == null || _campoVazio.DeclaringType != o.GetType())
            {
                // O campo e o que guarda o pincel que OrphanEmptySideBackBrush devolve (U10:
                // m_EmptyBackBrush). Achado pelo VALOR: ha varios Brush, e na U14 nomes ofuscados.
                Brush atual = o.OrphanEmptySideBackBrush;
                _campoVazio = null;
                foreach (FieldInfo c in o.GetType().GetFields(Inst | BindingFlags.DeclaredOnly))
                    if (typeof(Brush).IsAssignableFrom(c.FieldType) && ReferenceEquals(c.GetValue(o), atual)) { _campoVazio = c; break; }
                if (_campoVazio == null) return;
            }
            FieldInfo f = _campoVazio;
            if (_vazio == null || _vazioFundo.ToArgb() != Paleta.FundoCodigo.ToArgb())
            {
                _vazio = Tracejado(Paleta.FundoCodigo, Color.Empty);
                _vazioFundo = Paleta.FundoCodigo;
            }
            if (!ReferenceEquals(f.GetValue(o), _vazio)) f.SetValue(o, _vazio);
        }

        // ------------------------------------------------------------ arvore de diferencas
        // Busca por TIPO e ASSINATURA, nao por nome: a U14 vem ofuscada (o campo "treList" da
        // U10 la se chama "TVVymdTpbk"; os tipos e as assinaturas sao os mesmos).

        /// <summary>O unico campo cujo tipo e (ou deriva de) <paramref name="tipo"/>, pelo nome curto.</summary>
        private static FieldInfo CampoDoTipo(Type t, string tipo)
        {
            FieldInfo achado = null;
            foreach (FieldInfo f in t.GetFields(Inst | BindingFlags.DeclaredOnly))
            {
                bool eh = false;
                for (Type b = f.FieldType; b != null && !eh; b = b.BaseType) eh = b.Name == tipo;
                if (!eh) continue;
                if (achado != null) return null;   // dois candidatos: nao adivinha
                achado = f;
            }
            return achado;
        }

        /// <summary>O unico metodo (object, X) da classe, com X = <paramref name="args"/> pelo nome curto.</summary>
        private static MethodInfo Desenho(Type t, string args)
        {
            MethodInfo achado = null;
            foreach (MethodInfo m in t.GetMethods(Inst | BindingFlags.DeclaredOnly))
            {
                ParameterInfo[] p = m.GetParameters();
                if (p.Length != 2 || p[0].ParameterType != typeof(object) || p[1].ParameterType.Name != args || m.ReturnType != typeof(void)) continue;
                if (achado != null) return null;
                achado = m;
            }
            return achado;
        }

        /// <summary>O que falta na arvore de diferencas desta versao (null = tudo presente). Usado
        /// tambem pela checagem de compatibilidade da abertura.</summary>
        internal static string Falta(Type pane)
        {
            if (CampoDoTipo(pane, "TreeList") == null) return "campo da lista (TreeList)";
            if (CampoDoTipo(pane, "DiffViewLines") == null) return "campo das linhas (DiffViewLines)";
            foreach (string e in new[] { "CustomDrawNodeCell", "CustomDrawNodeIndent", "CustomDrawNodeImages" })
                if (Desenho(pane, e + "EventArgs") == null) return "desenho " + e;
            return null;
        }

        private static readonly ConditionalWeakTable<Control, Arvore> _arvores = new ConditionalWeakTable<Control, Arvore>();

        public static bool Ligar(Control pane)
        {
            Arvore a;
            if (_arvores.TryGetValue(pane, out a)) return false;
            a = Arvore.Criar(pane);
            _arvores.Add(pane, a);
            a.Lista.Invalidate();
            return true;
        }

        /// <summary>Os tres desenhos da StructDiffPane, refeitos com fundo escuro.</summary>
        private sealed class Arvore
        {
            public Control Lista;
            private Control _pane;
            private FieldInfo _linhas;
            private Delegate _gxCelula, _gxRecuo, _gxIcones;
            private static Size? _icone;

            public static Arvore Criar(Control pane)
            {
                Type t = pane.GetType();
                string falta = Falta(t);
                if (falta != null) throw new MissingMemberException(t.FullName, falta);
                var a = new Arvore { _pane = pane, Lista = (Control)CampoDoTipo(t, "TreeList").GetValue(pane), _linhas = CampoDoTipo(t, "DiffViewLines") };
                // Monta tudo antes de trocar: ou os tres trocam, ou nenhum.
                var trocas = new[]
                {
                    a.Troca("CustomDrawNodeCell", "Celula"),
                    a.Troca("CustomDrawNodeIndent", "Recuo"),
                    a.Troca("CustomDrawNodeImages", "Icones"),
                };
                a._gxCelula = trocas[0].Item2; a._gxRecuo = trocas[1].Item2; a._gxIcones = trocas[2].Item2;
                foreach (var tr in trocas)
                {
                    tr.Item1.RemoveEventHandler(a.Lista, tr.Item2);
                    tr.Item1.AddEventHandler(a.Lista, tr.Item3);
                }
                return a;
            }

            private Tuple<EventInfo, Delegate, Delegate> Troca(string evento, string nosso)
            {
                EventInfo ev = Lista.GetType().GetEvent(evento);
                MethodInfo gx = Desenho(_pane.GetType(), evento + "EventArgs");
                if (ev == null || gx == null) throw new MissingMethodException(_pane.GetType().FullName, evento);
                return Tuple.Create(ev, Delegate.CreateDelegate(ev.EventHandlerType, _pane, gx),
                                        Delegate.CreateDelegate(ev.EventHandlerType, this, typeof(Arvore).GetMethod(nosso, Inst)));
            }

            // Cada desenho: o nosso, ou - parte desligada / erro - o original do GeneXus.
            private void Celula(object s, EventArgs e) { Desenhar(s, e, _gxCelula, 0); }
            private void Recuo(object s, EventArgs e) { Desenhar(s, e, _gxRecuo, 1); }
            private void Icones(object s, EventArgs e) { Desenhar(s, e, _gxIcones, 2); }

            private void Desenhar(object s, EventArgs ev, Delegate gx, int qual)
            {
                if (Disjuntor.Ligada(Parte.Comparador))
                {
                    try { Nosso((dynamic)ev, qual); return; }
                    catch (Exception ex) { Disjuntor.Falhou(Parte.Comparador, ex); }
                }
                gx.DynamicInvoke(s, ev);
            }

            private void Nosso(dynamic e, int qual)
            {
                object no = e.Node;
                Rectangle b = e.Bounds;
                Graphics g = e.Graphics;
                using (Brush fundo = Fundo(no)) g.FillRectangle(fundo, b);
                if (qual == 2)
                {
                    var sn = ((dynamic)no).Tag as StructNode;
                    var r = new Rectangle(b.Location, TamanhoIcone());
                    if (sn != null && sn.Icon != null) g.DrawIcon(sn.Icon, r);
                    else if (sn != null && sn.Image != null) g.DrawImage(sn.Image, r);
                }
                Foco(no, g, b);
                if (qual == 0)
                {
                    Rectangle t = b;
                    t.Inflate(-2, 0);
                    e.Appearance.DrawString(e.Cache, (string)e.CellText, t);
                }
                e.Handled = true;
            }

            /// <summary>A mesma regra do GetBrushForNode do GeneXus: numero -1 = lado vazio.</summary>
            private Brush Fundo(object no)
            {
                var linhas = _linhas.GetValue(_pane) as DiffViewLines;
                int id = (int)((dynamic)no).Id;
                DiffViewLine l = linhas == null || id < 0 || id >= linhas.Count ? null : linhas[id];
                if (l != null && l.Number == -1) return Tracejado(Paleta.FundoEntrada, Color.Empty);
                Color c = l == null ? Paleta.FundoEntrada : Tingir(DoUsuario(l.LineType), Paleta.FundoEntrada);
                return new SolidBrush(c);
            }

            /// <summary>Linha com foco: contorno de 1 px em cima e embaixo, na cor de destaque.</summary>
            private void Foco(object no, Graphics g, Rectangle b)
            {
                if (!ReferenceEquals(((dynamic)Lista).FocusedNode, no)) return;
                using (var p = new SolidBrush(Paleta.Destaque))
                {
                    g.FillRectangle(p, b.X, b.Y, b.Width, 1);
                    g.FillRectangle(p, b.X, b.Bottom - 1, b.Width, 1);
                }
            }

            /// <summary>O tamanho de icone do GeneXus (escala de DPI), como no desenho original.</summary>
            private static Size TamanhoIcone()
            {
                if (_icone == null)
                {
                    Size s = new Size(16, 16);
                    try
                    {
                        Type t = Type.GetType("Artech.Common.Helpers.GDI.ScreenDPI, Artech.Common.Helpers", false);
                        PropertyInfo p = t == null ? null : t.GetProperty("DefaultImageSize", BindingFlags.Static | BindingFlags.Public);
                        if (p != null) s = (Size)p.GetValue(null, null);
                    }
                    catch { }
                    _icone = s;
                }
                return _icone.Value;
            }
        }
    }
}
