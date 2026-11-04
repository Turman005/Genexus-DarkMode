﻿// GxDarkTheme - restos claros da moldura e janelas que ainda saiam claras.
// Porte de correcoes do Xpz.Dark (Filipe Castro, 04/11), achadas com um censo das bordas de cada controle
// (foto da janela + quem pinta cada linha clara) e testadas no GeneXus 18 U14.
//   - Tabelas globais Office2007/2010 da Infragistics: o que sobra de claro na coluna Black.
//   - Titulo dos paineis acoplados: linha clara de 1px pintada pelo filtro de desenho do GeneXus.
//   - Cabecalho das arvores CustomTreeList (Variables, Work With Objects): cores ESTATICAS claras.
//   - Contorno de 1px: grade interna do Properties (branco fixo) e combo de barra (Output "Show:").
//   - TabControl do WinForms: faixa ao lado das abas e moldura da pagina (Properties do Web Layout).
//   - Painel de conteudo do KB Explorer (degrade claro do renderizador proprio).
//   - Tela desenhada (Web Layout): texto de fabrica na raiz - labels/botoes ilegiveis.
//   - Menus do botao direito nas abas de documento e no titulo dos paineis.
//   - Janela nova escurecida antes de aparecer (Find abria branco e piscava).
//   - Layout de Procedure fora do tema por padrao (setting layout_procedure=true para escurecer).
// Tudo em memoria, no processo do GeneXus, e reversivel: o modo claro devolve os originais.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace GxDarkTheme
{
    public static partial class DarkEngine
    {
        // =====================================================================
        //  1. Tabelas globais: restos claros da coluna Black
        // =====================================================================
        // O esquema Black e "moldura escura, conteudo claro": a coluna dele nas tabelas Office2007/2010
        // tem ~580 cores claras (Office2010.toolBorder branco, tabBorderColor, scrollbarBorderColor 237,
        // editorBackColor 198...). Onde o .isl nao declara a cor de um elemento, a Infragistics pinta com
        // a tabela: borda clara da aba ativa, da lista de janelas, dos combos da barra. PatchIgColorTables
        // acerta cores escolhidas a dedo; aqui so o que SOBROU claro, e so na coluna do esquema Black
        // (indice = esquema resolvido - 1, medido na v11.1: 2007 Blue/Black/Silver = 0/1/2; 2010
        // Silver/Blue/Black = 0/1/2). O original vai para igOrig: IgLight devolve no modo claro.
        static readonly HashSet<FieldInfo> restosNossos = new HashSet<FieldInfo>();
        static readonly string[] palavrasBorda = { "border", "glow", "line", "separator", "edge", "outline", "underline", "frame" };

        static void RestosDaTabela()
        {
            int n = 0;
            Assembly iw = typeof(Infragistics.Win.Appearance).Assembly;
            foreach (string tn in new string[] { "Infragistics.Win.Office2007ColorTable", "Infragistics.Win.Office2010ColorTable" })
            {
                try
                {
                    Type t = iw.GetType(tn);
                    if (t == null) continue;
                    object esquema = t.GetProperty("ColorSchemeResolved", BindingFlags.Public | BindingFlags.Static).GetValue(null, null);
                    if (esquema == null || esquema.ToString() != "Black") continue;
                    int idx = Convert.ToInt32(esquema) - 1;
                    foreach (FieldInfo f in t.GetFields(BindingFlags.Static | BindingFlags.NonPublic))
                    {
                        if (f.FieldType != typeof(Color[]) || f.IsLiteral) continue;
                        // cor que PatchIgColorTables ja acertou fica como esta
                        if (igOrig.ContainsKey(f) && !restosNossos.Contains(f)) continue;
                        Color[] a = f.GetValue(null) as Color[];
                        if (a == null || idx < 0 || idx >= a.Length) continue;
                        if (!igOrig.ContainsKey(f)) { igOrig[f] = (Color[])a.Clone(); restosNossos.Add(f); }
                        Color novo = Resto(f.Name, igOrig[f][idx]);   // sempre do original: troca de tema recalcula
                        if (novo.ToArgb() != a[idx].ToArgb()) { a[idx] = novo; n++; }
                    }
                }
                catch (Exception ex) { LogOnce("restos:" + tn, "Restos claros (" + tn + "): " + ex.GetType().Name + ": " + ex.Message); }
            }
            if (n > 0) Log("Restos claros: " + n + " cores claras da coluna Black das tabelas Office2007/2010 no tom do tema");
        }

        // Texto escuro vira claro; borda ou fundo CLARO vira o tom do tema. O que ja e escuro na coluna
        // Black (a moldura) fica. Alfa preservado (transparente continua transparente).
        static Color Resto(string nome, Color o)
        {
            if (o.IsEmpty || o.A == 0) return o;
            double luz = (0.299 * o.R + 0.587 * o.G + 0.114 * o.B) / 255.0;
            bool neutra = Math.Max(o.R, Math.Max(o.G, o.B)) - Math.Min(o.R, Math.Min(o.G, o.B)) < 28;
            string n = nome.ToLowerInvariant();
            Color c;
            if (n.Contains("fore") || n.Contains("text"))
            {
                if (luz >= 0.5) return o;
                c = Fg;
            }
            else
            {
                if (luz <= 0.45) return o;
                bool borda = false;
                foreach (string p in palavrasBorda) if (n.Contains(p)) { borda = true; break; }
                c = borda ? (neutra ? Border : HotBorder) : (neutra ? Panel : HotBg);
            }
            return o.A < 255 ? Color.FromArgb(o.A, c) : c;
        }

        // =====================================================================
        //  Visita de cada controle (Walk) e de cada passada (Pass)
        // =====================================================================
        static void RestosVisit(Control c)
        {
            if (TituloPainel.EhPainel(c)) TituloPainel.Ligar(c);
            CabecalhoArvore.Visitar(c);
            string n = c.GetType().FullName;
            // grade interna do Properties: LightColorDrawManager.DrawGridNonClientArea pinta um retangulo
            // Color.WHITE fixo na area nao-cliente (lido no IL); BorderStyle=None na grade de fora nao o tira
            if (n == "VisualHint.SmartPropertyGrid.InternalGrid") Contorno.Ligar(c, false);
            // combo de barra (Output "Show:"): o WinForms pinta a borda com SystemColors.Window (branco)
            // quando o combo nao tem foco nem mouse em cima
            else if (n == "System.Windows.Forms.ToolStripComboBox+ToolStripComboBoxControl") Contorno.Ligar(c, true);
            ToolStripContentPanel cp = c as ToolStripContentPanel;
            if (cp != null) PainelDeConteudo.Visitar(cp);
            TabControl tc = c as TabControl;
            if (tc != null) MolduraDasAbas.Ligar(tc);
            ComboBox cb = c as ComboBox;
            if (cb != null) TextoDoCombo(cb);
            TelaDesenhada.Visitar(c);
        }

        // Combo de eventos do editor (headerComboBox): o GeneXus devolve o texto PRETO ao trocar a lista, e a lista
        // aberta (desenhada pelo Windows com a cor de texto do combo) ficava preta no fundo escuro - so o item sob o
        // mouse saia branco. Corrige na hora em que a cor volta (ForeColorChanged) e a cada passada.
        static readonly HashSet<ComboBox> combosVigiados = new HashSet<ComboBox>();
        static void TextoDoCombo(ComboBox cb)
        {
            if (!On) return;
            if (IsDarkText(cb.ForeColor)) cb.ForeColor = Fg;
            if (combosVigiados.Add(cb))
            {
                cb.ForeColorChanged += delegate { try { if (On && IsDarkText(cb.ForeColor)) cb.ForeColor = Fg; } catch { } };
                cb.Disposed += delegate { combosVigiados.Remove(cb); };
            }
        }

        static bool restosIniciados;
        static void RestosPass()
        {
            if (!restosIniciados)
            {
                restosIniciados = true;
                JanelaNova.Ligar();
                // Layout de Procedure (designer de relatorio XtraReports): fora do tema por padrao, como o
                // Web Layout - escurecido, a pagina ficava ilegivel (texto embaralhado, faixas). Volta com
                // "layout_procedure=true" no GxDarkTheme.settings.
                if (SettingsValue("layout_procedure") != "true") { reportOk = false; Log("Layout de Procedure fora do tema (layout_procedure=true para escurecer)"); }
            }
            if (!On)
            {
                CabecalhoArvore.Devolver();
                PainelDeConteudo.Devolver();
                TelaDesenhada.Devolver();
            }
            else MenusDeContexto.Ligar();
        }

        /// <summary>Janela nova, ainda antes do primeiro desenho (gancho JanelaNova): mesmo tratamento da passada.</summary>
        internal static void AntesDeMostrar(Form f)
        {
            if (!On || inPass || f == null || f.IsDisposed || f.InvokeRequired || IsExcludedForm(f)) return;
            inPass = true;
            try { DarkTitle(f.Handle); Walk(f, 0); }
            catch (Exception ex) { LogOnce("antes-de-mostrar:" + f.GetType().FullName, "Janela nova (antes de mostrar) " + f.GetType().FullName + ": " + ex.Message); }
            finally { inPass = false; }
        }
    }

    // =====================================================================
    //  2. Titulo dos paineis acoplados (Properties, Output, KB Explorer...)
    // =====================================================================
    /// <summary>
    /// Linha clara de 1px no topo do titulo de TODOS os paineis acoplados (212,212,214 num tema,
    /// 185,200,214 noutro: muda com o tema). Nao vem do .isl nem da tabela global: fora do GeneXus o
    /// mesmo painel, com o mesmo estilo e tema, sai escuro. Quem pinta e o filtro de desenho do proprio
    /// GeneXus (Artech.FrameworkDE.DockManager implementa IUIElementDrawFilter; codigo ofuscado).
    /// Depois de cada WM_PAINT do DockableWindow, repinta a moldura de 1px do titulo
    /// (PaneCaptionUIElement) na cor de borda do tema. No modo claro nao pinta nada.
    /// </summary>
    internal sealed class TituloPainel : NativeWindow
    {
        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr h);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr h, IntPtr dc);

        static readonly Dictionary<Control, TituloPainel> ligados = new Dictionary<Control, TituloPainel>();
        static PropertyInfo raizProp;
        static bool falhou;
        readonly Control c;
        TituloPainel(Control c) { this.c = c; }

        public static bool EhPainel(Control c) { return !falhou && c.GetType().FullName == "Infragistics.Win.UltraWinDock.DockableWindow"; }

        public static void Ligar(Control c)
        {
            if (ligados.ContainsKey(c) || !c.IsHandleCreated) return;
            TituloPainel t = new TituloPainel(c);
            ligados[c] = t;
            t.AssignHandle(c.Handle);
            c.HandleCreated += delegate { if (t.Handle != c.Handle) { try { t.ReleaseHandle(); } catch { } t.AssignHandle(c.Handle); } };
            c.Disposed += delegate { ligados.Remove(c); };
            c.Invalidate();
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg != 0x000F || falhou || !DarkEngine.On) return;   // WM_PAINT
            IntPtr dc = IntPtr.Zero;
            try
            {
                Rectangle r = Titulo(c);
                if (r.Width < 4 || r.Height < 4) return;
                dc = GetDC(Handle);
                if (dc == IntPtr.Zero) return;
                using (Graphics g = Graphics.FromHdc(dc))
                using (Pen p = new Pen(DarkEngine.Border))
                    g.DrawRectangle(p, r.X, r.Y, r.Width - 1, r.Height - 1);
            }
            catch (Exception ex) { falhou = true; DarkEngine.Log("Titulo dos paineis (moldura) desligado: " + ex.GetType().Name + ": " + ex.Message); }
            finally { if (dc != IntPtr.Zero) ReleaseDC(Handle, dc); }
        }

        /// <summary>Retangulo do PaneCaptionUIElement (coordenadas do controle), ou vazio.</summary>
        internal static Rectangle Titulo(Control c)
        {
            if (raizProp == null) raizProp = c.GetType().GetProperty("ControlUIElement", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            object raiz = raizProp == null ? null : raizProp.GetValue(c, null);
            Infragistics.Win.UIElement cap = Achar(raiz as Infragistics.Win.UIElement, 0);
            return cap == null ? Rectangle.Empty : cap.Rect;
        }

        static Infragistics.Win.UIElement Achar(Infragistics.Win.UIElement el, int nivel)
        {
            if (el == null || nivel > 4) return null;
            if (el.GetType().Name == "PaneCaptionUIElement") return el;
            foreach (Infragistics.Win.UIElement f in el.ChildElements) { Infragistics.Win.UIElement r = Achar(f, nivel + 1); if (r != null) return r; }
            return null;
        }
    }

    // =====================================================================
    //  Contorno de 1px repintado depois do desenho do controle
    // =====================================================================
    /// <summary>
    /// Controle que pinta a propria moldura com uma cor clara FIXA (sem propriedade para trocar):
    /// depois de cada desenho, repinta o contorno de 1px na cor de borda do tema (acento com foco).
    /// cliente=false: moldura na area nao-cliente (DC da janela, WM_NCPAINT/WM_PAINT);
    /// cliente=true: moldura dentro da area cliente (WM_PAINT). No modo claro nao pinta nada.
    /// </summary>
    internal sealed class Contorno : NativeWindow
    {
        [DllImport("user32.dll")] static extern IntPtr GetWindowDC(IntPtr h);
        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr h);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr h, IntPtr dc);
        [DllImport("user32.dll")] static extern bool RedrawWindow(IntPtr h, IntPtr r, IntPtr rgn, uint flags);

        static readonly Dictionary<Control, Contorno> ligados = new Dictionary<Control, Contorno>();
        static bool falhou;
        readonly Control c;
        readonly bool cliente;
        Contorno(Control c, bool cliente) { this.c = c; this.cliente = cliente; }

        public static void Ligar(Control c, bool cliente)
        {
            if (falhou || ligados.ContainsKey(c) || !c.IsHandleCreated) return;
            Contorno t = new Contorno(c, cliente);
            ligados[c] = t;
            t.AssignHandle(c.Handle);
            c.HandleCreated += delegate { if (t.Handle != c.Handle) { try { t.ReleaseHandle(); } catch { } t.AssignHandle(c.Handle); } };
            c.Disposed += delegate { ligados.Remove(c); };
            EventHandler foco = delegate { try { if (c.IsHandleCreated) { RedrawWindow(c.Handle, IntPtr.Zero, IntPtr.Zero, 0x401); c.Invalidate(); } } catch { } };
            c.GotFocus += foco; c.LostFocus += foco;
            RedrawWindow(c.Handle, IntPtr.Zero, IntPtr.Zero, 0x401);   // RDW_INVALIDATE | RDW_FRAME
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if ((m.Msg != 0x000F && m.Msg != 0x0085) || falhou || !DarkEngine.On) return;   // WM_PAINT, WM_NCPAINT
            if (cliente && m.Msg != 0x000F) return;
            IntPtr dc = IntPtr.Zero;
            try
            {
                dc = cliente ? GetDC(Handle) : GetWindowDC(Handle);
                if (dc == IntPtr.Zero) return;
                Rectangle r = cliente ? c.ClientRectangle : new Rectangle(0, 0, c.Width, c.Height);
                using (Graphics g = Graphics.FromHdc(dc))
                using (Pen p = new Pen(c.Focused ? DarkEngine.Accent : DarkEngine.Border))
                    g.DrawRectangle(p, r.X, r.Y, r.Width - 1, r.Height - 1);
            }
            catch (Exception ex) { falhou = true; DarkEngine.Log("Contorno de 1px desligado: " + ex.GetType().Name + ": " + ex.Message); }
            finally { if (dc != IntPtr.Zero) ReleaseDC(Handle, dc); }
        }
    }

    // =====================================================================
    //  Moldura e faixa do TabControl do WinForms
    // =====================================================================
    /// <summary>
    /// O TabControl do WinForms pinta, pelo visual do Windows, a faixa vazia ao lado das abas (240) e a
    /// moldura em volta da pagina (255/227) - o Properties de um controle do Web Layout (aba "General",
    /// TabControlDesigner) ficava com moldura branca. As abas ja sao desenhadas pelo motor (DrawTab); depois
    /// de cada WM_PAINT, a faixa e a moldura sao repintadas no escuro. (Porte do AbasWinForms do Xpz.Dark.)
    /// </summary>
    internal sealed class MolduraDasAbas : NativeWindow
    {
        static readonly Dictionary<TabControl, MolduraDasAbas> ligados = new Dictionary<TabControl, MolduraDasAbas>();
        static bool falhou;
        readonly TabControl t;
        MolduraDasAbas(TabControl t) { this.t = t; }

        public static void Ligar(TabControl t)
        {
            if (falhou || ligados.ContainsKey(t) || !t.IsHandleCreated) return;
            MolduraDasAbas m = new MolduraDasAbas(t);
            ligados[t] = m;
            m.AssignHandle(t.Handle);
            t.HandleCreated += delegate { if (m.Handle != t.Handle) { try { m.ReleaseHandle(); } catch { } m.AssignHandle(t.Handle); } };
            t.Disposed += delegate { ligados.Remove(t); };
            t.Invalidate();
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg != 0x000F || falhou || !DarkEngine.On || t.TabCount == 0 || t.Alignment != TabAlignment.Top) return;   // WM_PAINT
            try
            {
                Rectangle ultima = t.GetTabRect(t.TabCount - 1), area = t.DisplayRectangle;
                using (Graphics g = Graphics.FromHwnd(Handle))
                using (SolidBrush faixa = new SolidBrush(DarkEngine.Chrome))
                using (Pen borda = new Pen(DarkEngine.Panel, 4))
                {
                    g.FillRectangle(faixa, ultima.Right + 2, 0, Math.Max(0, t.Width - ultima.Right - 2), ultima.Bottom + 2);   // faixa a direita das abas
                    g.DrawRectangle(borda, Rectangle.Inflate(area, 2, 2));                                                     // moldura em volta da pagina
                }
            }
            catch (Exception ex) { falhou = true; DarkEngine.Log("Moldura do TabControl desligada: " + ex.GetType().Name + ": " + ex.Message); }
        }
    }

    // =====================================================================
    //  Janela nova escurecida antes do primeiro desenho
    // =====================================================================
    /// <summary>
    /// A passada de 700 ms acha a janela nova depois de ela ja aparecer branca: o Find (Ctrl+F) abria
    /// branco, piscava e so entao escurecia. Um gancho CBT DA PROPRIA THREAD avisa no momento em que a
    /// janela e ATIVADA - antes do primeiro WM_PAINT. So olha janelas desta thread; custo: uma chamada por
    /// ativacao. O gancho sai antes de o .NET encerrar (uma ativacao durante o fechamento chamaria codigo
    /// gerenciado ja desmontado). (Porte do Gancho do Xpz.Dark, 27/10.)
    /// </summary>
    internal static class JanelaNova
    {
        delegate IntPtr CbtProc(int codigo, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] static extern IntPtr SetWindowsHookEx(int tipo, CbtProc proc, IntPtr mod, uint thread);
        [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr h, int codigo, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr h);
        [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
        const int WH_CBT = 5, HCBT_ACTIVATE = 5;
        static CbtProc proc;   // referencia viva: o GC nao pode recolher o delegate
        static IntPtr gancho;
        static bool falhou;

        public static void Ligar()
        {
            try
            {
                proc = Proc;
                gancho = SetWindowsHookEx(WH_CBT, proc, IntPtr.Zero, GetCurrentThreadId());
                DarkEngine.Log(gancho == IntPtr.Zero ? "Janela nova: gancho NAO instalado" : "Janela nova: escurecida antes de aparecer (gancho CBT da thread)");
                Application.ApplicationExit += delegate { Desligar(); };
                AppDomain.CurrentDomain.ProcessExit += delegate { Desligar(); };
                AppDomain.CurrentDomain.DomainUnload += delegate { Desligar(); };
            }
            catch (Exception ex) { DarkEngine.Log("Janela nova (gancho): " + ex.Message); }
        }

        static void Desligar()
        {
            IntPtr h = gancho;
            gancho = IntPtr.Zero;
            if (h != IntPtr.Zero) { try { UnhookWindowsHookEx(h); } catch { } }
        }

        static IntPtr Proc(int codigo, IntPtr w, IntPtr l)
        {
            if (Environment.HasShutdownStarted) return CallNextHookEx(IntPtr.Zero, codigo, w, l);
            if (codigo == HCBT_ACTIVATE && !falhou && DarkEngine.On)
            {
                try { DarkEngine.AntesDeMostrar(Control.FromHandle(w) as Form); }
                catch (Exception ex) { falhou = true; DarkEngine.Log("Janela nova (gancho) desligada: " + ex.Message); }
            }
            return CallNextHookEx(gancho, codigo, w, l);
        }
    }

    // =====================================================================
    //  Painel de conteudo de ToolStripContainer (KB Explorer)
    // =====================================================================
    /// <summary>
    /// O painel de conteudo com renderizador proprio pinta o fundo com o degrade claro da tabela
    /// Professional padrao (217,226,242): a faixa clara de 2px a esquerda do KB Explorer. Passa a
    /// usar o renderizador global (o escuro do tema); no modo claro volta ao que era.
    /// </summary>
    internal static class PainelDeConteudo
    {
        static readonly Dictionary<ToolStripContentPanel, ToolStripRenderMode> originais = new Dictionary<ToolStripContentPanel, ToolStripRenderMode>();

        public static void Visitar(ToolStripContentPanel p)
        {
            if (!DarkEngine.On || p.RenderMode == ToolStripRenderMode.ManagerRenderMode) return;
            if (!originais.ContainsKey(p)) { originais[p] = p.RenderMode; p.Disposed += delegate { originais.Remove(p); }; }
            p.RenderMode = ToolStripRenderMode.ManagerRenderMode;
            p.Invalidate();
        }

        public static void Devolver()
        {
            if (originais.Count == 0) return;
            foreach (KeyValuePair<ToolStripContentPanel, ToolStripRenderMode> kv in new List<KeyValuePair<ToolStripContentPanel, ToolStripRenderMode>>(originais))
                try { if (!kv.Key.IsDisposed) { kv.Key.RenderMode = kv.Value; kv.Key.Invalidate(); } } catch { }
            originais.Clear();
        }
    }

    // =====================================================================
    //  Tela desenhada (Web Layout, Win Form): texto de fabrica na raiz
    // =====================================================================
    /// <summary>
    /// O motor nao mexe nos controles da tela desenhada (IsDesignHosted), mas os conteineres EM VOLTA
    /// dela recebem texto claro, e label/botao da tela sem cor propria HERDA a cor do pai: texto claro
    /// sobre o fundo azul-claro do GeneXus, ilegivel. A raiz da tela (1o controle hospedado no designer)
    /// recebe de volta o texto de fabrica, se nao tiver cor propria; no modo claro volta a herdar.
    /// (Mesma correcao do Designer.cs do Xpz.Dark, 27/10.)
    /// </summary>
    internal static class TelaDesenhada
    {
        static readonly List<Control> raizes = new List<Control>();
        static readonly MethodInfo serTexto = typeof(Control).GetMethod("ShouldSerializeForeColor", BindingFlags.Instance | BindingFlags.NonPublic);

        static bool Hospedado(Control c) { try { return c.Site != null && c.Site.DesignMode; } catch { return false; } }

        public static void Visitar(Control pai)
        {
            if (!DarkEngine.On || Hospedado(pai)) return;
            foreach (Control k in pai.Controls)
            {
                if (!Hospedado(k) || raizes.Contains(k)) continue;
                bool propria = false;
                try { propria = serTexto != null && (bool)serTexto.Invoke(k, null); } catch { }
                if (propria) continue;
                k.ForeColor = SystemColors.ControlText;
                raizes.Add(k);
                Control raiz = k;
                k.Disposed += delegate { raizes.Remove(raiz); };
                DarkEngine.LogOnce("tela-desenhada", "Tela desenhada (Web Layout/Win Form): texto de fabrica na raiz (" + k.GetType().Name + ")");
            }
        }

        public static void Devolver()
        {
            if (raizes.Count == 0) return;
            foreach (Control r in raizes.ToArray()) try { if (!r.IsDisposed) r.ResetForeColor(); } catch { }
            raizes.Clear();
        }
    }

    // =====================================================================
    //  Menus do botao direito nas abas de documento e no titulo dos paineis
    // =====================================================================
    /// <summary>
    /// Os dois sao IGContextMenu da Infragistics, que desenham os itens com as cores CLARAS do Windows.
    /// Com Style = System o desenho passa ao Windows, que - com o modo escuro de menus do processo -
    /// desenha escuro. Aba: no InitializeContextMenu do gerenciador de abas (o mesmo que o GeneXus usa
    /// para montar o menu; o nosso roda depois). Painel: no Popup do menu do gerenciador de paineis.
    /// No modo claro, cada menu volta ao estilo de antes ao abrir. (Porte do MenuTema.cs do Xpz.Dark.)
    /// </summary>
    internal static class MenusDeContexto
    {
        static bool abas, paineis;
        static int tentativas;
        static readonly Dictionary<object, object> estiloOriginal = new Dictionary<object, object>();

        public static void Ligar()
        {
            // o menu do gerenciador de paineis pode nascer so mais tarde: tenta a cada ~3,5 s por ~10 min
            if ((abas && paineis) || tentativas > 860) return;
            if (++tentativas % 5 != 0) return;
            try
            {
                if (!abas)
                {
                    object g = Achar("Infragistics.Win.UltraWinTabbedMdi.UltraTabbedMdiManager");
                    EventInfo ev = g == null ? null : g.GetType().GetEvent("InitializeContextMenu");
                    if (ev != null)
                    {
                        ev.AddEventHandler(g, Delegate.CreateDelegate(ev.EventHandlerType, typeof(MenusDeContexto).GetMethod("AoMenuDaAba", BindingFlags.Static | BindingFlags.NonPublic)));
                        abas = true;
                        DarkEngine.Log("Menu das abas de documento: desenho passado ao Windows (escuro)");
                    }
                }
                if (!paineis)
                {
                    object d = Achar("Infragistics.Win.UltraWinDock.UltraDockManager");
                    PropertyInfo p = d == null ? null : d.GetType().GetProperty("ContextMenu", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    Menu menu = p == null ? null : p.GetValue(d, null) as Menu;
                    ContextMenu cm = menu as ContextMenu;
                    if (cm != null)
                    {
                        cm.Popup += delegate { Estilo(cm); };
                        paineis = true;
                        DarkEngine.Log("Menu do titulo dos paineis: desenho passado ao Windows (escuro)");
                    }
                }
            }
            catch (Exception ex) { abas = paineis = true; DarkEngine.Log("Menus do botao direito (abas/paineis) desligados: " + ex.GetType().Name + ": " + ex.Message); }
        }

        static void AoMenuDaAba(object sender, EventArgs e)
        {
            try
            {
                PropertyInfo p = e.GetType().GetProperty("ContextMenu");
                object menu = p == null ? null : p.GetValue(e, null);
                if (menu != null) Estilo(menu);
            }
            catch { }
        }

        // escuro: Style = System; claro: o estilo que o menu tinha
        static void Estilo(object menu)
        {
            try
            {
                PropertyInfo ps = menu.GetType().GetProperty("Style");
                if (ps == null) return;
                object atual = ps.GetValue(menu, null);
                if (!estiloOriginal.ContainsKey(menu)) estiloOriginal[menu] = atual;
                object alvo = DarkEngine.On ? Enum.Parse(ps.PropertyType, "System") : estiloOriginal[menu];
                if (!Equals(atual, alvo)) ps.SetValue(menu, alvo, null);
            }
            catch { }
        }

        /// <summary>Componente da janela principal: campo dela ou da lista de componentes dela.</summary>
        static object Achar(string tipoBase)
        {
            foreach (Form f in Application.OpenForms)
            {
                if (f.GetType().Name != "MainWindow" || f.InvokeRequired) continue;
                for (Type b = f.GetType(); b != null; b = b.BaseType)
                    foreach (FieldInfo fi in b.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly))
                    {
                        object v;
                        try { v = fi.GetValue(f); } catch { continue; }
                        if (v == null) continue;
                        if (Deriva(v.GetType(), tipoBase)) return v;
                        System.ComponentModel.IContainer cont = v as System.ComponentModel.IContainer;
                        if (cont != null)
                            foreach (System.ComponentModel.IComponent c in cont.Components)
                                if (c != null && Deriva(c.GetType(), tipoBase)) return c;
                    }
            }
            return null;
        }

        static bool Deriva(Type t, string nome) { for (Type b = t; b != null; b = b.BaseType) if (b.FullName == nome) return true; return false; }
    }

    // =====================================================================
    //  3. Cabecalho das arvores CustomTreeList (Variables, Work With Objects)
    // =====================================================================
    /// <summary>
    /// O cabecalho das colunas e desenhado por um painter PROPRIO (CustomTreeListPainter), nao pela
    /// Appearance.HeaderPanel do DevExpress, com quatro cores que sao propriedades ESTATICAS da classe
    /// (valem para todas as arvores): de fabrica fundo 245, borda 192 e hover amarelo - a linha clara no
    /// topo do cabecalho da aba Variables. Escuro: cores do tema; modo claro: as de fabrica.
    /// </summary>
    internal static class CabecalhoArvore
    {
        const string Tipo = "Artech.Common.Controls.Editors.CustomTree.CustomTreeList";
        static readonly string[] Props = { "ColumnHeaderDefaultBackColor", "ColumnHeaderDefaultBorderColor", "ColumnHeaderHoveredBackColor", "ColumnHeaderHoveredBorderColor" };
        static Type tipo;
        static Color[] originais;
        static bool aplicado, falhou;

        public static void Visitar(Control c)
        {
            if (falhou || !DarkEngine.On) return;
            if (tipo == null)
            {
                for (Type b = c.GetType(); b != null; b = b.BaseType) if (b.FullName == Tipo) { tipo = b; break; }
                if (tipo == null) return;
            }
            else if (!tipo.IsInstanceOfType(c)) return;
            try
            {
                Color[] alvo = { DarkEngine.Chrome, DarkEngine.Border, DarkEngine.HotBg, DarkEngine.HotBorder };
                if (originais == null)
                {
                    originais = new Color[Props.Length];
                    for (int i = 0; i < Props.Length; i++) originais[i] = Ler(Props[i]);
                }
                bool mudou = false;
                for (int i = 0; i < Props.Length; i++)
                    if (Ler(Props[i]).ToArgb() != alvo[i].ToArgb()) { Gravar(Props[i], alvo[i]); mudou = true; }
                if (mudou) c.Invalidate();
                if (!aplicado) { aplicado = true; DarkEngine.Log("Cabecalho das arvores CustomTreeList (Variables, Work With Objects): cores escuras (estaticas, em memoria)"); }
            }
            catch (Exception ex) { falhou = true; DarkEngine.Log("Cabecalho das arvores desligado: " + ex.GetType().Name + ": " + ex.Message); }
        }

        public static void Devolver()
        {
            if (!aplicado || originais == null) return;
            aplicado = false;
            try { for (int i = 0; i < Props.Length; i++) Gravar(Props[i], originais[i]); } catch { }
        }

        static Color Ler(string p) { return (Color)tipo.GetProperty(p, BindingFlags.Public | BindingFlags.Static).GetValue(null, null); }
        static void Gravar(string p, Color v) { tipo.GetProperty(p, BindingFlags.Public | BindingFlags.Static).SetValue(null, v, null); }
    }
}
