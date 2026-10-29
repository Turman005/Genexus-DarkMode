// Xpz.Dark - controles WinForms com desenho proprio: combo, barra de rolagem avulsa,
// TabControl, PropertyGrid, DataGridView, caixa em faixa de ferramentas.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

// REGRA DE TODAS AS SUBCLASSES (NativeWindow) DA XPZ.DARK: nunca chamar ReleaseHandle no
// HandleDestroyed. O .NET solta a subclasse sozinho no WM_NCDESTROY. Soltar antes, com outra
// subclasse por cima (ex.: as sugestoes automaticas do Windows numa caixa de texto), faz o
// WinForms perder a destruicao: o controle fica com um handle morto e o Dispose estoura
// "Cross-thread operation not valid" (Tools > Options, 28/10 - reproduzido no TesteThreads).

namespace Xpz.Dark
{
    // =====================================================================
    //  Combo plano com seta e borda escuras
    // =====================================================================
    /// <summary>
    /// ComboBox com FlatStyle.Flat obedece BackColor no campo, mas desenha a caixa da
    /// seta e a borda com cores claras fixas; nenhum tema do Windows muda isso (medido
    /// 28/10). Depois do desenho normal, pinta por cima a borda e a caixa da seta.
    /// </summary>
    internal sealed class ComboEscuro : NativeWindow
    {
        private readonly ComboBox _c;
        private ComboEscuro(ComboBox c) { _c = c; }

        public static void Ligar(ComboBox c)
        {
            var ce = new ComboEscuro(c);
            if (c.IsHandleCreated) ce.AssignHandle(c.Handle);
            c.HandleCreated += delegate { if (ce.Handle != c.Handle) { try { ce.ReleaseHandle(); } catch { } ce.AssignHandle(c.Handle); } };
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            // WM_PAINT. Flat (os nossos) e Popup (o das faixas de ferramentas, ex. Show do Output).
            if (m.Msg != 0x000F || _c.FlatStyle == FlatStyle.Standard || _c.FlatStyle == FlatStyle.System || !Disjuntor.Ligada(Parte.Combo)) return;
            try
            {
                using (Graphics g = Graphics.FromHwnd(Handle))
                {
                    Rectangle r = _c.ClientRectangle;
                    int largura = SystemInformation.VerticalScrollBarWidth;
                    var seta = new Rectangle(r.Right - largura - 1, r.Top + 1, largura, r.Height - 2);
                    using (var b = new SolidBrush(_c.Enabled ? Paleta.FundoElevado : Paleta.FundoPainel)) g.FillRectangle(b, seta);
                    int cx = seta.X + seta.Width / 2, cy = seta.Y + seta.Height / 2;
                    var tri = new[] { new Point(cx - 4, cy - 2), new Point(cx + 4, cy - 2), new Point(cx, cy + 2) };
                    using (var b = new SolidBrush(_c.Enabled ? Paleta.Texto : Paleta.TextoApagado)) g.FillPolygon(b, tri);
                    using (var p = new Pen(_c.Focused ? Paleta.Link : Paleta.Borda))
                        g.DrawRectangle(p, r.X, r.Y, r.Width - 1, r.Height - 1);
                }
            }
            catch { }
        }
    }

    // =====================================================================
    //  Contorno de foco das caixas de texto
    // =====================================================================
    /// <summary>
    /// A borda simples da caixa de texto (FixedSingle) sai sempre cinza: nao da para saber
    /// qual campo esta com o cursor. Depois do desenho do Windows, pinta por cima: destaque do
    /// tema com foco, borda do tema sem foco. Com o visual do Windows a caixa NAO tem area de
    /// borda separada (medido 28/10: janela = area cliente) - a borda vem no WM_PAINT; sem
    /// ele, no WM_NCPAINT. Pinta nos dois.
    /// </summary>
    internal sealed class BordaFoco : NativeWindow
    {
        [DllImport("user32.dll")] private static extern IntPtr GetWindowDC(IntPtr h);
        [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr h, IntPtr dc);
        [DllImport("user32.dll")] private static extern bool RedrawWindow(IntPtr h, IntPtr r, IntPtr rgn, uint flags);
        private const uint RDW_INVALIDATE = 0x1, RDW_FRAME = 0x400;

        private static readonly ConditionalWeakTable<TextBox, BordaFoco> _ligadas = new ConditionalWeakTable<TextBox, BordaFoco>();
        private readonly TextBox _t;
        private BordaFoco(TextBox t) { _t = t; }

        public static void Ligar(TextBox t)
        {
            BordaFoco b;
            if (_ligadas.TryGetValue(t, out b)) return;
            b = new BordaFoco(t);
            _ligadas.Add(t, b);
            // caixa ja na tela (talvez ja com o cursor): repinta para o contorno aparecer
            if (t.IsHandleCreated) { b.AssignHandle(t.Handle); t.Invalidate(); }
            t.HandleCreated += delegate { if (b.Handle != t.Handle) { try { b.ReleaseHandle(); } catch { } b.AssignHandle(t.Handle); } };
            EventHandler moldura = delegate { try { if (t.IsHandleCreated) { RedrawWindow(t.Handle, IntPtr.Zero, IntPtr.Zero, RDW_INVALIDATE | RDW_FRAME); t.Invalidate(); } } catch { } };
            t.GotFocus += moldura;
            t.LostFocus += moldura;
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if ((m.Msg != 0x0085 && m.Msg != 0x000F) || !Disjuntor.Ligada(Parte.Foco) || _t.BorderStyle != BorderStyle.FixedSingle) return;   // WM_NCPAINT, WM_PAINT
            IntPtr dc = IntPtr.Zero;
            try
            {
                dc = GetWindowDC(Handle);
                if (dc == IntPtr.Zero) return;
                using (Graphics g = Graphics.FromHdc(dc))
                using (var p = new Pen(_t.Focused ? Paleta.Destaque : Paleta.Borda))
                    g.DrawRectangle(p, 0, 0, _t.Width - 1, _t.Height - 1);
            }
            catch (Exception ex) { Disjuntor.Falhou(Parte.Foco, ex); }
            finally { if (dc != IntPtr.Zero) ReleaseDC(Handle, dc); }
        }
    }

    // =====================================================================
    //  Borda simples (WS_BORDER) de controle de terceiros
    // =====================================================================
    /// <summary>
    /// O Properties (SmartPropertyGrid com BorderStyle FixedSingle) pede ao Windows a borda
    /// simples, que sai BRANCA no escuro (29/10). Depois do desenho da moldura pelo Windows,
    /// pinta por cima a borda do tema.
    /// </summary>
    internal sealed class BordaSimples : NativeWindow
    {
        [DllImport("user32.dll")] private static extern IntPtr GetWindowDC(IntPtr h);
        [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr h, IntPtr dc);
        [DllImport("user32.dll")] private static extern bool RedrawWindow(IntPtr h, IntPtr r, IntPtr rgn, uint flags);
        private const uint RDW_INVALIDATE = 0x1, RDW_FRAME = 0x400;

        private static readonly ConditionalWeakTable<Control, BordaSimples> _ligadas = new ConditionalWeakTable<Control, BordaSimples>();
        private readonly Control _c;
        private BordaSimples(Control c) { _c = c; }

        public static void Ligar(Control c)
        {
            BordaSimples b;
            if (_ligadas.TryGetValue(c, out b)) return;
            b = new BordaSimples(c);
            _ligadas.Add(c, b);
            if (c.IsHandleCreated) { b.AssignHandle(c.Handle); RedrawWindow(c.Handle, IntPtr.Zero, IntPtr.Zero, RDW_INVALIDATE | RDW_FRAME); }
            c.HandleCreated += delegate { if (b.Handle != c.Handle) { try { b.ReleaseHandle(); } catch { } b.AssignHandle(c.Handle); } };
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if ((m.Msg != 0x0085 && m.Msg != 0x000F) || !Disjuntor.Ligada(Parte.Janelas)) return;   // WM_NCPAINT, WM_PAINT
            IntPtr dc = IntPtr.Zero;
            try
            {
                dc = GetWindowDC(Handle);
                if (dc == IntPtr.Zero) return;
                using (Graphics g = Graphics.FromHdc(dc))
                using (var p = new Pen(Paleta.Borda))
                    g.DrawRectangle(p, 0, 0, _c.Width - 1, _c.Height - 1);
            }
            catch (Exception ex) { Disjuntor.Falhou(Parte.Janelas, ex); }
            finally { if (dc != IntPtr.Zero) ReleaseDC(Handle, dc); }
        }
    }

    // =====================================================================
    //  Barra de rolagem avulsa do Windows
    // =====================================================================
    /// <summary>
    /// VScrollBar/HScrollBar sao o controle nativo "SCROLLBAR", que ignora BackColor e o
    /// tema DarkMode_Explorer (este so vale para a barra que faz parte de uma janela, como a
    /// da lista). Depois de cada desenho nativo, repinta trilho, polegar e setas com as
    /// cores do tema. A posicao do polegar sai de Value/Minimum/Maximum/LargeChange, a
    /// mesma conta do Windows.
    /// </summary>
    internal sealed class RolagemNativa : NativeWindow
    {
        private static readonly ConditionalWeakTable<ScrollBar, RolagemNativa> _ligadas = new ConditionalWeakTable<ScrollBar, RolagemNativa>();
        private readonly ScrollBar _s;
        private RolagemNativa(ScrollBar s) { _s = s; }

        public static void Ligar(ScrollBar s)
        {
            RolagemNativa r;
            if (_ligadas.TryGetValue(s, out r)) return;
            r = new RolagemNativa(s);
            _ligadas.Add(s, r);
            if (s.IsHandleCreated) r.AssignHandle(s.Handle);
            s.HandleCreated += delegate { if (r.Handle != s.Handle) { try { r.ReleaseHandle(); } catch { } r.AssignHandle(s.Handle); } };
            s.ValueChanged += delegate { s.Invalidate(); };
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            // WM_PAINT, e depois de mouse/teclado (o nativo redesenha direto, sem WM_PAINT)
            if (m.Msg == 0x000F || m.Msg == 0x0202 || m.Msg == 0x0201 || m.Msg == 0x0200 || m.Msg == 0x02A3 || m.Msg == 0x0101)
            {
                if (Disjuntor.Ligada(Parte.Rolagem)) { try { Desenhar(); } catch (Exception ex) { Disjuntor.Falhou(Parte.Rolagem, ex); } }
            }
        }

        private void Desenhar()
        {
            using (Graphics g = Graphics.FromHwnd(Handle))
                DesenhoRolagem.Barra(g, _s.Width, _s.Height, _s is VScrollBar, _s.Enabled, _s.Minimum, _s.Maximum, _s.Value, _s.LargeChange);
        }
    }

    // =====================================================================
    //  Barras de rolagem do DevExpress (arvores Variables, Structure)
    // =====================================================================
    /// <summary>
    /// A barra do DevExpress 7.3 (VTLScrollBar/HTLScrollBar das arvores) e desenhada pelo
    /// "look and feel" dele, sempre clara (247,243,235) - nao ha cor para trocar. Ela tem os
    /// mesmos Minimum/Maximum/Value/LargeChange da barra do WinForms: o WM_PAINT inteiro
    /// passa a ser desenhado aqui (sem pintar por cima, sem piscar); mouse e teclado
    /// continuam com o DevExpress.
    /// </summary>
    internal sealed class RolagemDevExpress : NativeWindow
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct PAINTSTRUCT { public IntPtr hdc; public bool fErase; public int l, t, r, b; public bool fRestore, fIncUpdate; [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] rgbReserved; }
        [DllImport("user32.dll")] private static extern IntPtr BeginPaint(IntPtr h, out PAINTSTRUCT ps);
        [DllImport("user32.dll")] private static extern bool EndPaint(IntPtr h, ref PAINTSTRUCT ps);

        private static readonly ConditionalWeakTable<Control, RolagemDevExpress> _ligadas = new ConditionalWeakTable<Control, RolagemDevExpress>();
        private readonly Control _s;
        private readonly PropertyInfo _min, _max, _valor, _passo;
        private readonly bool _vertical;

        private RolagemDevExpress(Control s)
        {
            _s = s;
            Type t = s.GetType();
            _min = t.GetProperty("Minimum"); _max = t.GetProperty("Maximum"); _valor = t.GetProperty("Value"); _passo = t.GetProperty("LargeChange");
            _vertical = Familia.Deriva(t, "DevExpress.XtraEditors.VScrollBarBase");
        }

        public static bool Eh(Control c) { return Familia.Deriva(c.GetType(), "DevExpress.XtraEditors.ScrollBarBase"); }

        public static void Ligar(Control s)
        {
            RolagemDevExpress r;
            if (_ligadas.TryGetValue(s, out r)) return;
            r = new RolagemDevExpress(s);
            if (r._min == null || r._max == null || r._valor == null || r._passo == null) return;
            _ligadas.Add(s, r);
            if (s.IsHandleCreated) { r.AssignHandle(s.Handle); s.Invalidate(); }
            s.HandleCreated += delegate { if (r.Handle != s.Handle) { try { r.ReleaseHandle(); } catch { } r.AssignHandle(s.Handle); } };
        }

        protected override void WndProc(ref Message m)
        {
            if ((m.Msg == 0x000F || m.Msg == 0x0014) && Disjuntor.Ligada(Parte.Rolagem))   // WM_PAINT, WM_ERASEBKGND
            {
                try
                {
                    if (m.Msg == 0x0014) { m.Result = (IntPtr)1; return; }
                    PAINTSTRUCT ps;
                    IntPtr dc = BeginPaint(Handle, out ps);
                    try
                    {
                        using (Graphics g = Graphics.FromHdc(dc))
                            DesenhoRolagem.Barra(g, _s.Width, _s.Height, _vertical, _s.Enabled,
                                (int)_min.GetValue(_s, null), (int)_max.GetValue(_s, null), (int)_valor.GetValue(_s, null), (int)_passo.GetValue(_s, null));
                    }
                    finally { EndPaint(Handle, ref ps); }
                    return;
                }
                catch (Exception ex) { Disjuntor.Falhou(Parte.Rolagem, ex); }
            }
            base.WndProc(ref m);
        }
    }

    /// <summary>Desenho das barras de rolagem escuras (polegar arredondado, setas
    /// discretas) - o mesmo para a barra nativa e as da Actipro.</summary>
    internal static class DesenhoRolagem
    {
        /// <summary>Barra inteira: trilho, duas setas e o polegar na posicao do valor.</summary>
        public static void Barra(Graphics g, int w, int h, bool vertical, bool habilitada, int minimo, int maximo, int valor, int passo)
        {
            int seta = vertical ? Math.Min(SystemInformation.VerticalScrollBarArrowHeight, h / 2) : Math.Min(SystemInformation.HorizontalScrollBarArrowWidth, w / 2);
            int trilho = (vertical ? h : w) - 2 * seta;
            using (var b = new SolidBrush(Paleta.RolagemTrilho)) g.FillRectangle(b, 0, 0, w, h);
            Color corSeta = habilitada ? Paleta.TextoApagado : Paleta.Borda;
            Seta(g, vertical ? new Rectangle(0, 0, w, seta) : new Rectangle(0, 0, seta, h), vertical, true, corSeta);
            Seta(g, vertical ? new Rectangle(0, h - seta, w, seta) : new Rectangle(w - seta, 0, seta, h), vertical, false, corSeta);
            int faixa = maximo - minimo + 1;
            if (!habilitada || trilho <= 8 || faixa <= 0 || passo >= faixa) return;
            int menor = vertical ? SystemInformation.VerticalScrollBarThumbHeight : SystemInformation.HorizontalScrollBarThumbWidth;
            int tam = Math.Max(menor, (int)((long)trilho * passo / faixa));
            int curso = Math.Max(1, faixa - passo);
            int pos = seta + (int)((long)(trilho - tam) * Math.Max(0, Math.Min(curso, valor - minimo)) / curso);
            Rectangle p = vertical ? new Rectangle(0, pos, w, tam) : new Rectangle(pos, 0, tam, h);
            Polegar(g, p, vertical, Paleta.RolagemPolegar);
        }

        public static void Polegar(Graphics g, Rectangle bounds, bool vertical, Color cor)
        {
            Rectangle r = vertical ? Rectangle.Inflate(bounds, -3, -1) : Rectangle.Inflate(bounds, -1, -3);
            if (r.Width <= 0 || r.Height <= 0) return;
            var antes = g.SmoothingMode;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            int raio = Math.Min(r.Width, r.Height) / 2;
            using (var caminho = new System.Drawing.Drawing2D.GraphicsPath())
            using (var b = new SolidBrush(cor))
            {
                if (raio < 1) g.FillRectangle(b, r);
                else
                {
                    int d = raio * 2;
                    caminho.AddArc(r.X, r.Y, d, d, 180, 90);
                    caminho.AddArc(r.Right - d, r.Y, d, d, 270, 90);
                    caminho.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
                    caminho.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
                    caminho.CloseFigure();
                    g.FillPath(b, caminho);
                }
            }
            g.SmoothingMode = antes;
        }

        public static void Seta(Graphics g, Rectangle bounds, bool vertical, bool inicio, Color cor)
        {
            int cx = bounds.X + bounds.Width / 2, cy = bounds.Y + bounds.Height / 2, t = Math.Max(3, Math.Min(bounds.Width, bounds.Height) / 4);
            Point[] seta = vertical
                ? (inicio ? new[] { new Point(cx - t, cy + t / 2), new Point(cx + t, cy + t / 2), new Point(cx, cy - t / 2 - 1) }
                          : new[] { new Point(cx - t, cy - t / 2), new Point(cx + t, cy - t / 2), new Point(cx, cy + t / 2 + 1) })
                : (inicio ? new[] { new Point(cx + t / 2, cy - t), new Point(cx + t / 2, cy + t), new Point(cx - t / 2 - 1, cy) }
                          : new[] { new Point(cx - t / 2, cy - t), new Point(cx - t / 2, cy + t), new Point(cx + t / 2 + 1, cy) });
            var antes = g.SmoothingMode;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (var b = new SolidBrush(cor)) g.FillPolygon(b, seta);
            g.SmoothingMode = antes;
        }
    }

    // =====================================================================
    //  TabControl do WinForms (cabecalho)
    // =====================================================================
    /// <summary>
    /// O TabControl do WinForms ignora BackColor nas abas e na faixa. As abas passam a ser
    /// desenhadas por nos (OwnerDrawFixed); depois do desenho normal, a faixa vazia ao lado
    /// das abas e a moldura clara em volta da pagina sao repintadas no escuro.
    /// </summary>
    internal sealed class AbasWinForms : NativeWindow
    {
        private static readonly ConditionalWeakTable<TabControl, AbasWinForms> _ligadas = new ConditionalWeakTable<TabControl, AbasWinForms>();
        private readonly TabControl _t;
        private AbasWinForms(TabControl t) { _t = t; }

        public static void Ligar(TabControl t)
        {
            AbasWinForms a;
            if (_ligadas.TryGetValue(t, out a)) return;
            a = new AbasWinForms(t);
            _ligadas.Add(t, a);
            if (t.DrawMode != TabDrawMode.OwnerDrawFixed) t.DrawMode = TabDrawMode.OwnerDrawFixed;
            t.DrawItem += Desenhar;
            if (t.IsHandleCreated) a.AssignHandle(t.Handle);
            t.HandleCreated += delegate { if (a.Handle != t.Handle) { try { a.ReleaseHandle(); } catch { } a.AssignHandle(t.Handle); } };
        }

        private static void Desenhar(object sender, DrawItemEventArgs e)
        {
            var t = (TabControl)sender;
            if (e.Index < 0 || e.Index >= t.TabPages.Count) return;
            if (!Disjuntor.Ligada(Parte.Abas))
            {
                // parte desligada: as abas continuam OwnerDraw - desenho simples do sistema
                e.DrawBackground();
                TextRenderer.DrawText(e.Graphics, t.TabPages[e.Index].Text, t.Font, e.Bounds, SystemColors.ControlText, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }
            bool sel = e.Index == t.SelectedIndex;
            Rectangle r = e.Bounds;
            using (var b = new SolidBrush(sel ? Paleta.FundoPainel : Paleta.FundoCodigo)) e.Graphics.FillRectangle(b, r);
            if (sel) using (var b = new SolidBrush(Paleta.Link)) e.Graphics.FillRectangle(b, r.X, r.Y, r.Width, 2);
            TextRenderer.DrawText(e.Graphics, t.TabPages[e.Index].Text, t.Font, r, sel ? Paleta.Texto : Paleta.TextoInativo,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg != 0x000F || _t.TabCount == 0 || _t.Alignment != TabAlignment.Top || !Disjuntor.Ligada(Parte.Abas)) return;   // WM_PAINT
            try
            {
                Rectangle ultima = _t.GetTabRect(_t.TabCount - 1), area = _t.DisplayRectangle;
                using (Graphics g = Graphics.FromHwnd(Handle))
                using (var faixa = new SolidBrush(Paleta.FundoCodigo))
                using (var borda = new Pen(Paleta.FundoPainel, 4))
                {
                    // faixa vazia a direita das abas
                    g.FillRectangle(faixa, ultima.Right + 2, 0, Math.Max(0, _t.Width - ultima.Right - 2), ultima.Bottom + 2);
                    // moldura clara em volta da pagina
                    g.DrawRectangle(borda, Rectangle.Inflate(area, 2, 2));
                }
            }
            catch { }
        }
    }

    internal static partial class Fase2
    {
        private static void ItemHospedado(ToolStripItem it)
        {
            var host = it as ToolStripControlHost;
            if (host == null || !(host.Control is TextBoxBase || host.Control is ComboBox)) return;
            EventHandler escurecer = delegate
            {
                if (_pintando) return;
                _pintando = true;
                try
                {
                    if (host.BackColor.ToArgb() != Paleta.FundoEntrada.ToArgb()) host.BackColor = Paleta.FundoEntrada;
                    if (host.ForeColor.ToArgb() != Paleta.Texto.ToArgb()) host.ForeColor = Paleta.Texto;
                }
                catch { }
                finally { _pintando = false; }
            };
            escurecer(it, EventArgs.Empty);
            it.BackColorChanged += escurecer;
            it.ForeColorChanged += escurecer;
            host.Control.BackColorChanged += escurecer;
        }

        private static bool PintarPropertyGrid(PropertyGrid pg)
        {
            // Pelo nome: algumas destas so existem a partir do .NET 4.7.2.
            var cores = new Dictionary<string, Color>
            {
                { "ViewBackColor", Paleta.FundoEntrada }, { "ViewForeColor", Paleta.Texto },
                { "LineColor", Paleta.Linha }, { "CategoryForeColor", Paleta.Texto },
                { "CategorySplitterColor", Paleta.Linha }, { "HelpBackColor", Paleta.FundoPainel },
                { "HelpForeColor", Paleta.Texto }, { "HelpBorderColor", Paleta.Borda },
                { "CommandsBackColor", Paleta.FundoPainel }, { "CommandsForeColor", Paleta.Texto },
                { "CommandsBorderColor", Paleta.Borda }, { "DisabledItemForeColor", Paleta.TextoApagado },
                { "ViewBorderColor", Paleta.Borda }, { "SelectedItemWithFocusBackColor", Paleta.Selecao },
                { "SelectedItemWithFocusForeColor", Paleta.Texto },
            };
            foreach (var kv in cores)
            {
                try
                {
                    PropertyInfo p = pg.GetType().GetProperty(kv.Key, BindingFlags.Public | BindingFlags.Instance);
                    if (p != null && p.CanWrite) p.SetValue(pg, kv.Value, null);
                }
                catch { }
            }
            pg.BackColor = Paleta.FundoPainel;
            // ToolStripRenderer nao e publico no .NET 4.7.1. A faixa de ferramentas do
            // PropertyGrid e um controle filho, e a recursao ja chega nela; isto e reforco.
            if (_renderer != null)
            {
                try
                {
                    PropertyInfo r = typeof(PropertyGrid).GetProperty("ToolStripRenderer",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (r != null && r.CanWrite) r.SetValue(pg, _renderer, null);
                }
                catch { }
            }
            return true;
        }

        private static bool PintarGrade(DataGridView g)
        {
            g.BackgroundColor = Paleta.FundoEntrada;
            g.GridColor = Paleta.Linha;
            g.DefaultCellStyle.BackColor = Paleta.FundoEntrada;
            g.DefaultCellStyle.ForeColor = Paleta.Texto;
            g.DefaultCellStyle.SelectionBackColor = Paleta.Selecao;
            g.DefaultCellStyle.SelectionForeColor = Paleta.Texto;
            g.EnableHeadersVisualStyles = false;
            g.ColumnHeadersDefaultCellStyle.BackColor = Paleta.FundoPainel;
            g.ColumnHeadersDefaultCellStyle.ForeColor = Paleta.Texto;
            g.RowHeadersDefaultCellStyle.BackColor = Paleta.FundoPainel;
            g.RowHeadersDefaultCellStyle.ForeColor = Paleta.Texto;
            return true;
        }
    }
}
