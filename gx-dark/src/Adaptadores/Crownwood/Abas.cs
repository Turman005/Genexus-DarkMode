// Xpz.Dark - abas das partes do objeto (Crownwood Magic TabControl, aparencia MultiBox).

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

namespace Xpz.Dark
{
    internal static partial class Fase2
    {
        private static bool PintarAbas(Control c)
        {
            if (!Disjuntor.Ligada(Parte.Abas)) return false;
            var cores = new Dictionary<string, Color>
            {
                { "TextColor", Paleta.Texto }, { "TextInactiveColor", Paleta.TextoInativo },
                { "HotTextColor", Color.White }, { "ButtonActiveColor", Paleta.Texto },
                { "ButtonInactiveColor", Paleta.TextoApagado },
            };
            bool mudou = false;
            foreach (var kv in cores)
            {
                try
                {
                    PropertyInfo p = c.GetType().GetProperty(kv.Key, BindingFlags.Public | BindingFlags.Instance);
                    if (p != null && p.CanWrite && p.PropertyType == typeof(Color)) { p.SetValue(c, kv.Value, null); mudou = true; }
                }
                catch { }
            }
            // A Crownwood DERIVA as cores das bordas e da faixa clareando/escurecendo o
            // fundo (ControlPaint.Light/LightLight/Dark/DarkDark e um "_backIDE"). Com fundo
            // escuro o "LightLight" vira quase branco: as bordas e a caixa claras das abas
            // Web Layout/Rules/Events (28/10). Grava os campos com as cores do tema.
            try
            {
                if (c.BackColor.ToArgb() != Paleta.FundoCodigo.ToArgb()) c.BackColor = Paleta.FundoCodigo;   // recalcula os derivados...
                CamposDasAbas(c);                                                                            // ...e os troca
                if (!_abasComEvento.TryGetValue(c, out _nada))
                {
                    _abasComEvento.Add(c, new object());
                    // O GeneXus repoe o fundo depois (a aba ativa voltava branca no IDE, 28/10).
                    c.BackColorChanged += delegate { if (Paleta.FundoClaro(c.BackColor)) c.BackColor = Paleta.FundoCodigo; CamposDasAbas(c); };
                }
                // A aba SELECIONADA e pintada com a cor da PAGINA: painel do tema, que se liga
                // ao conteudo; as outras ficam na faixa mais escura.
                PropertyInfo pp = c.GetType().GetProperty("TabPages", BindingFlags.Public | BindingFlags.Instance);
                var paginas = pp == null ? null : pp.GetValue(c, null) as System.Collections.IEnumerable;
                if (paginas != null)
                    foreach (object o in paginas)
                    {
                        var pg = o as Control;
                        if (pg != null && pg.BackColor.ToArgb() != Paleta.FundoPainel.ToArgb()) pg.BackColor = Paleta.FundoPainel;
                    }
                AbaDestaque.Ligar(c);
                if (!_abasLogadas)
                {
                    _abasLogadas = true;
                    PropertyInfo st = c.GetType().GetProperty("Style"), ap = c.GetType().GetProperty("Appearance");
                    Log.Linha("abas das partes: estilo " + (st == null ? "?" : st.GetValue(c, null)) + ", aparencia " + (ap == null ? "?" : ap.GetValue(c, null)));
                }
                mudou = true;
            }
            catch (Exception ex) { Disjuntor.Falhou(Parte.Abas, ex); }
            c.Invalidate();
            return mudou;
        }
        private static bool _abasLogadas;
        private static object _nada;
        private static readonly ConditionalWeakTable<Control, object> _abasComEvento = new ConditionalWeakTable<Control, object>();

        /// <summary>Cores derivadas da Crownwood com as do tema. Chamado tambem a cada
        /// desenho da faixa (AbaDestaque): quem recalcular os derivados perde na hora.</summary>
        internal static void CamposDasAbas(Control c)
        {
            var campos = new[] { "_backLightLight", "_backLight", "_backDark", "_backDarkDark", "_backIDE" };
            var cores = new[] { Paleta.Borda, Paleta.Borda, Paleta.Linha, Paleta.FundoCodigo, Paleta.FundoCodigo };
            for (int i = 0; i < campos.Length; i++)
            {
                FieldInfo f = c.GetType().GetField(campos[i], BindingFlags.Instance | BindingFlags.NonPublic);
                if (f != null && f.FieldType == typeof(Color) && ((Color)f.GetValue(c)).ToArgb() != cores[i].ToArgb()) f.SetValue(c, cores[i]);
            }
            // O GeneXus usa a aparencia MultiBox (TabbedWindowControl.tabbedGroups1_TabControlCreated):
            // nela a aba ATIVA e preenchida com TabPage.ColorTab, que por padrao e BRANCO - era a
            // caixa branca de Events/Rules (28/10). As outras so sao preenchidas se ColorTab nao
            // for branco: a ativa recebe a cor do tema, as demais ficam em branco (= sem fundo).
            PropertyInfo pp = c.GetType().GetProperty("TabPages", BindingFlags.Public | BindingFlags.Instance);
            var paginas = pp == null ? null : pp.GetValue(c, null) as System.Collections.IEnumerable;
            if (paginas == null) return;
            foreach (object o in paginas)
            {
                PropertyInfo ct = o.GetType().GetProperty("ColorTab"), sel = o.GetType().GetProperty("Selected");
                if (ct == null || sel == null || ct.PropertyType != typeof(Color)) continue;
                Color alvo = (bool)sel.GetValue(o, null) ? Paleta.FundoElevado : Color.White;
                if (((Color)ct.GetValue(o, null)).ToArgb() != alvo.ToArgb()) ct.SetValue(o, alvo, null);
            }
        }
    }

    // =====================================================================
    //  Aba ativa das partes com faixa de destaque
    // =====================================================================
    /// <summary>
    /// Na Crownwood a aba ativa so muda de fundo; no escuro a diferenca some. Depois do
    /// desenho normal, pinta uma faixa de 2px na cor de destaque do tema sob a aba ativa
    /// (como o VS Code). Os retangulos das abas vem do campo privado _tabRects.
    /// </summary>
    internal sealed class AbaDestaque : NativeWindow
    {
        private static readonly ConditionalWeakTable<Control, AbaDestaque> _ligadas = new ConditionalWeakTable<Control, AbaDestaque>();
        private readonly Control _c;
        private AbaDestaque(Control c) { _c = c; }

        public static void Ligar(Control c)
        {
            AbaDestaque a;
            if (_ligadas.TryGetValue(c, out a)) return;
            a = new AbaDestaque(c);
            _ligadas.Add(c, a);
            if (c.IsHandleCreated) a.AssignHandle(c.Handle);
            c.HandleCreated += delegate { if (a.Handle != c.Handle) { try { a.ReleaseHandle(); } catch { } a.AssignHandle(c.Handle); } };
        }

        protected override void WndProc(ref Message m)
        {
            bool ligada = Disjuntor.Ligada(Parte.Abas);
            if (m.Msg == 0x000F && ligada) { try { Fase2.CamposDasAbas(_c); } catch (Exception ex) { Disjuntor.Falhou(Parte.Abas, ex); } }   // antes de a Crownwood desenhar
            base.WndProc(ref m);
            if (m.Msg != 0x000F || !ligada) return;   // WM_PAINT
            try
            {
                Type t = _c.GetType();
                var rects = t.GetField("_tabRects", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_c) as System.Collections.IList;
                int sel = (int)t.GetProperty("SelectedIndex").GetValue(_c, null);
                if (rects == null || sel < 0 || sel >= rects.Count) return;
                var r = (Rectangle)rects[sel];
                if (r.Width <= 0) return;
                FieldInfo fTopo = t.GetField("_positionAtTop", BindingFlags.Instance | BindingFlags.NonPublic);
                bool topo = fTopo == null || (bool)fTopo.GetValue(_c);
                using (Graphics g = Graphics.FromHwnd(Handle))
                using (var b = new SolidBrush(Paleta.Link))
                    g.FillRectangle(b, r.X + 1, topo ? r.Bottom - 2 : r.Top, r.Width - 2, 2);
            }
            catch { }
        }
    }
}
