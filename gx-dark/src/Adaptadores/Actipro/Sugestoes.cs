// Xpz.Dark - lista de sugestoes do editor de codigo (IntelliPrompt do Actipro SyntaxEditor).

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Xpz.Dark
{
    // =====================================================================
    //  Lista de membros (o "&Variavel." que abre a lista de metodos)
    // =====================================================================
    /// <summary>
    /// A IntelliPromptMemberListBox e um ListBox desenhado pelo proprio Actipro com cores
    /// FIXAS do sistema (SystemColors.Window / WindowText / Highlight): nenhuma propriedade
    /// muda isso. O desenho de cada item chega ao ListBox como WM_DRAWITEM refletido pelo
    /// WinForms; esta janela atende a mensagem antes dele e desenha com a paleta do tema
    /// (fundo, selecao, icone, texto). O quadro do popup em volta (borda 3D cinza) e pintado
    /// por <see cref="QuadroEscuro"/>.
    /// </summary>
    internal sealed class ListaSugestao : NativeWindow
    {
        private const int WM_REFLECT_DRAWITEM = 0x2000 + 0x002B;
        private const int ODS_SELECTED = 0x0001;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct DRAWITEMSTRUCT
        {
            public int CtlType, CtlID, itemID, itemAction, itemState;
            public IntPtr hwndItem, hDC;
            public RECT rcItem;
            public IntPtr itemData;
        }

        private static readonly ConditionalWeakTable<ListBox, ListaSugestao> _ligadas = new ConditionalWeakTable<ListBox, ListaSugestao>();
        private readonly ListBox _l;
        private ListaSugestao(ListBox l) { _l = l; }

        public static bool Eh(Control c)
        {
            return c is ListBox && c.GetType().FullName == "ActiproSoftware.SyntaxEditor.IntelliPromptMemberListBox";
        }

        public static void Ligar(ListBox l)
        {
            ListaSugestao s;
            if (_ligadas.TryGetValue(l, out s)) return;
            s = new ListaSugestao(l);
            _ligadas.Add(l, s);
            l.BackColor = Paleta.FundoEntrada;   // area abaixo do ultimo item
            l.ForeColor = Paleta.Texto;
            if (l.IsHandleCreated) { s.AssignHandle(l.Handle); l.Invalidate(); }
            l.HandleCreated += delegate { if (s.Handle != l.Handle) { try { s.ReleaseHandle(); } catch { } s.AssignHandle(l.Handle); } };
            Form popup = l.Parent as Form;
            if (popup != null) QuadroEscuro.Ligar(popup);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_REFLECT_DRAWITEM && Disjuntor.Ligada(Parte.Editor))
            {
                try { Desenhar((DRAWITEMSTRUCT)Marshal.PtrToStructure(m.LParam, typeof(DRAWITEMSTRUCT))); m.Result = (IntPtr)1; return; }
                catch (Exception ex) { Disjuntor.Falhou(Parte.Editor, ex); }
            }
            base.WndProc(ref m);
        }

        private void Desenhar(DRAWITEMSTRUCT d)
        {
            var r = Rectangle.FromLTRB(d.rcItem.Left, d.rcItem.Top, d.rcItem.Right, d.rcItem.Bottom);
            using (Graphics g = Graphics.FromHdc(d.hDC))
            {
                if (d.itemID < 0 || d.itemID >= _l.Items.Count)
                {
                    using (var b = new SolidBrush(Paleta.FundoEntrada)) g.FillRectangle(b, r);
                    return;
                }
                bool selecionado = (d.itemState & ODS_SELECTED) != 0;
                // Selecao "virtual" (o texto digitado so comeca o nome): o Actipro mostra so um
                // contorno pontilhado, sem preencher - mesmo comportamento aqui.
                bool virtual_ = selecionado && Ler<bool>(_l, "IsSelectionVirtual");
                bool cheio = selecionado && !virtual_;
                using (var b = new SolidBrush(cheio ? Paleta.Selecao : Paleta.FundoEntrada)) g.FillRectangle(b, r);
                if (virtual_)
                    using (var p = new Pen(Paleta.Destaque) { DashStyle = DashStyle.Dot })
                        g.DrawRectangle(p, r.Left, r.Top, r.Width - 1, r.Height - 1);

                object item = _l.Items[d.itemID];
                int x = r.Left + 2;
                ImageList il = Ler<ImageList>(_l, "ImageList");
                if (il != null)
                {
                    int img = Ler<int>(item, "ImageIndex");
                    if (img >= 0 && img < il.Images.Count) il.Draw(g, x, r.Top + (r.Height - il.ImageSize.Height) / 2, img);
                    x += il.ImageSize.Width + 4;
                }
                string texto = Ler<string>(item, "Text") ?? item.ToString();
                TextRenderer.DrawText(g, texto, _l.Font, new Rectangle(x, r.Top, Math.Max(0, r.Right - x), r.Height), Paleta.Texto,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            }
        }

        private static T Ler<T>(object o, string nome)
        {
            PropertyInfo p = o.GetType().GetProperty(nome, BindingFlags.Instance | BindingFlags.Public);
            object v = p == null ? null : p.GetValue(o, null);
            return v is T ? (T)v : default(T);
        }
    }

    // =====================================================================
    //  Quadro do popup da lista
    // =====================================================================
    /// <summary>
    /// O popup que segura a lista pinta o fundo com SystemBrushes.Control e uma borda 3D
    /// (cinza claro em volta da lista escura). Depois do desenho dele, pinta por cima so a
    /// parte que nao e de nenhum filho: fundo da lista e borda de 1 px do tema.
    /// </summary>
    internal sealed class QuadroEscuro : NativeWindow
    {
        private static readonly ConditionalWeakTable<Control, QuadroEscuro> _ligados = new ConditionalWeakTable<Control, QuadroEscuro>();
        private readonly Control _c;
        private QuadroEscuro(Control c) { _c = c; }

        public static void Ligar(Control c)
        {
            QuadroEscuro q;
            if (_ligados.TryGetValue(c, out q)) return;
            q = new QuadroEscuro(c);
            _ligados.Add(c, q);
            if (c.IsHandleCreated) { q.AssignHandle(c.Handle); c.Invalidate(); }
            c.HandleCreated += delegate { if (q.Handle != c.Handle) { try { q.ReleaseHandle(); } catch { } q.AssignHandle(c.Handle); } };
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if ((m.Msg != 0x000F && m.Msg != 0x0014) || !Disjuntor.Ligada(Parte.Editor)) return;   // WM_PAINT, WM_ERASEBKGND
            try
            {
                using (Graphics g = Graphics.FromHwnd(Handle))
                {
                    Rectangle r = _c.ClientRectangle;
                    using (var regiao = new Region(r))
                    {
                        foreach (Control f in _c.Controls) if (f.Visible) regiao.Exclude(f.Bounds);
                        using (var b = new SolidBrush(Paleta.FundoEntrada)) g.FillRegion(b, regiao);
                    }
                    using (var p = new Pen(Paleta.Borda)) g.DrawRectangle(p, 0, 0, r.Width - 1, r.Height - 1);
                }
            }
            catch (Exception ex) { Disjuntor.Falhou(Parte.Editor, ex); }
        }
    }
}

namespace Xpz.Dark
{
    // =====================================================================
    //  Dicas do editor (parametros da funcao, QuickInfo)
    // =====================================================================
    /// <summary>
    /// A dica que aparece ao digitar "Funcao(" (e a QuickInfo) e um MarkupLabel do Actipro num
    /// popup, com fundo SystemColors.Info (amarelo-claro) e texto InfoText (auditoria 28/10:
    /// MarkupLabel 301x20 dentro do popup "bd"). O texto do MarkupLabel vem do ForeColor do
    /// proprio controle: basta trocar as duas cores - e trocar de novo se o Actipro as repuser.
    /// O quadro do popup NAO e pintado por cima: ali o Actipro desenha as setas "1 de 2" das
    /// sobrecargas.
    /// </summary>
    internal static class DicaEditor
    {
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<System.Windows.Forms.Control, object> _ligadas =
            new System.Runtime.CompilerServices.ConditionalWeakTable<System.Windows.Forms.Control, object>();

        public static bool Eh(System.Windows.Forms.Control c)
        {
            if (c.GetType().FullName != "ActiproSoftware.MarkupLabel.MarkupLabel") return false;
            for (System.Windows.Forms.Control p = c.Parent; p != null; p = p.Parent)
                if (Familia.Deriva(p.GetType(), "ActiproSoftware.WinUICore.PopupControl")) return true;
            return c.BackColor.ToArgb() == System.Drawing.SystemColors.Info.ToArgb();
        }

        public static void Ligar(System.Windows.Forms.Control c)
        {
            object x;
            if (_ligadas.TryGetValue(c, out x)) return;
            _ligadas.Add(c, new object());
            Pintar(c);
            System.EventHandler repor = delegate { Pintar(c); };
            c.BackColorChanged += repor;
            c.ForeColorChanged += repor;
            System.Windows.Forms.Control popup = c.Parent;
            if (popup != null && Familia.Deriva(popup.GetType(), "ActiproSoftware.WinUICore.PopupControl")) popup.BackColor = Paleta.FundoElevado;
        }

        private static void Pintar(System.Windows.Forms.Control c)
        {
            if (!Disjuntor.Ligada(Parte.Editor)) return;
            try
            {
                if (c.BackColor.ToArgb() != Paleta.FundoElevado.ToArgb()) c.BackColor = Paleta.FundoElevado;
                if (c.ForeColor.ToArgb() != Paleta.Texto.ToArgb()) c.ForeColor = Paleta.Texto;
            }
            catch (System.Exception ex) { Disjuntor.Falhou(Parte.Editor, ex); }
        }
    }
}
