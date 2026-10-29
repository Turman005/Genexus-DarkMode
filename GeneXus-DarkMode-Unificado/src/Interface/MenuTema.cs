// GxDarkTheme - menu Ferramentas/Tools > "Tema escuro" (ideia e textos do Xpz.Dark, de itsFilipe) e menu do botao sol/lua.
// Os itens sao criados em memoria no UltraToolbarsManager da janela principal e retirados ANTES de o GeneXus gravar o
// layout das barras (WM_CLOSE da janela principal), para nada nosso ficar salvo no perfil do usuario.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using Infragistics.Win.UltraWinToolbars;

namespace GxDarkTheme
{
    internal static class MenuTema
    {
        const string ChaveSub = "GxDarkTheme.Menu", ChaveCfg = "GxDarkTheme.Config", ChaveModo = "GxDarkTheme.Modo", ChaveTema = "GxDarkTheme.Tema.";
        static Timer timer;
        static int ticks, falhas;
        static UltraToolbarsManager mgr;
        static Form main;
        static bool marcando, ligado, menusLogados, fechando;
        static Vigia vigia;

        public static void Iniciar()
        {
            if (Environment.GetEnvironmentVariable("GXDARK_NOMENU") == "1") { DarkEngine.Log("Menu Tema escuro desligado (GXDARK_NOMENU=1)"); return; }
            DarkEngine.TemaMudou += delegate { Marcar(); };
            timer = new Timer { Interval = 3000 };
            timer.Tick += delegate
            {
                ticks++;
                if (ticks == 20) timer.Interval = 30000;
                if (fechando) return;
                try { Garantir(); }
                catch (Exception ex)
                {
                    DarkEngine.Log("Menu Tema escuro: " + ex.GetType().Name + ": " + ex.Message);
                    if (++falhas >= 5) { timer.Stop(); DarkEngine.Log("Menu Tema escuro desligado nesta sessao (5 falhas); o botao direito no sol/lua continua escolhendo o tema"); }
                }
            };
            timer.Start();
        }

        static Form AcharMain()
        {
            foreach (Form f in Application.OpenForms)
                if (f.GetType().Name == "MainWindow" && !f.IsDisposed && !f.InvokeRequired) return f;
            return null;
        }

        static UltraToolbarsManager AcharGerenciador(Form f)
        {
            for (Type b = f.GetType(); b != null && b != typeof(Form); b = b.BaseType)
                foreach (FieldInfo fi in b.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly))
                {
                    if (!typeof(UltraToolbarsManager).IsAssignableFrom(fi.FieldType)) continue;
                    UltraToolbarsManager m = fi.GetValue(f) as UltraToolbarsManager;
                    if (m != null) return m;
                }
            return null;
        }

        static void Garantir()
        {
            Form f = AcharMain();
            if (f == null) return;
            if (f != main) { main = f; mgr = AcharGerenciador(f); if (vigia != null) vigia.ReleaseHandle(); vigia = new Vigia(f); }
            if (mgr == null) { if (ticks == 10) DarkEngine.Log("Menu Tema escuro: gerenciador de barras da janela principal nao achado"); return; }
            PopupMenuTool tools = MenuTools();
            if (tools == null) return;
            if (!mgr.Tools.Exists(ChaveSub))
            {
                foreach (Tema t in Temas.Todos)
                {
                    StateButtonTool st = new StateButtonTool(ChaveTema + t.Id);
                    st.SharedProps.Caption = t.Nome;
                    st.SharedProps.ToolTipText = t.Descricao;
                    st.MenuDisplayStyle = StateButtonMenuDisplayStyle.DisplayCheckmark;
                    mgr.Tools.Add(st);
                }
                StateButtonTool modo = new StateButtonTool(ChaveModo);
                modo.SharedProps.Caption = "Modo escuro ligado (Ctrl+Shift+F12)";
                modo.MenuDisplayStyle = StateButtonMenuDisplayStyle.DisplayCheckmark;
                mgr.Tools.Add(modo);
                ButtonTool cfg = new ButtonTool(ChaveCfg);
                cfg.SharedProps.Caption = "Configurações...";
                cfg.SharedProps.ToolTipText = Produto.NomeExibicao + " " + Produto.Versao;
                mgr.Tools.Add(cfg);
                PopupMenuTool sub = new PopupMenuTool(ChaveSub);
                sub.SharedProps.Caption = "Tema escuro";
                mgr.Tools.Add(sub);
                foreach (Tema t in Temas.Todos) sub.Tools.AddTool(ChaveTema + t.Id);
                sub.Tools.AddTool(ChaveModo).InstanceProps.IsFirstInGroup = true;
                sub.Tools.AddTool(ChaveCfg).InstanceProps.IsFirstInGroup = true;
            }
            bool tem = false;
            foreach (ToolBase t in tools.Tools) if (t.Key == ChaveSub) { tem = true; break; }
            if (!tem)
            {
                tools.Tools.AddTool(ChaveSub).InstanceProps.IsFirstInGroup = true;
                DarkEngine.Log("Menu: 'Tema escuro' posto no menu " + (tools.SharedProps.Caption ?? "").Replace("&", ""));
            }
            if (!ligado) { mgr.ToolClick += AoClicar; ligado = true; }
            Marcar();
        }

        static PopupMenuTool MenuTools()
        {
            string[] nomes = { "tools", "ferramentas", "herramientas" };
            foreach (UltraToolbar tb in mgr.Toolbars)
                foreach (ToolBase t in tb.Tools)
                {
                    PopupMenuTool p = t as PopupMenuTool;
                    if (p == null) continue;
                    string cap = (p.SharedProps.Caption ?? "").Replace("&", "").Trim().ToLowerInvariant();
                    string key = (p.Key ?? "").ToLowerInvariant();
                    foreach (string n in nomes) if (cap == n || key.EndsWith(n)) return p;
                }
            if (!menusLogados)
            {
                menusLogados = true;
                List<string> l = new List<string>();
                foreach (UltraToolbar tb in mgr.Toolbars) foreach (ToolBase t in tb.Tools) if (l.Count < 60) l.Add(tb.Key + "/" + t.Key + "=\"" + t.SharedProps.Caption + "\"");
                DarkEngine.Log("Menu: menu Tools nao achado. Itens: " + string.Join("; ", l.ToArray()));
            }
            return null;
        }

        static void Marcar()
        {
            if (mgr == null) return;
            marcando = true;
            try
            {
                foreach (Tema t in Temas.Todos)
                    if (mgr.Tools.Exists(ChaveTema + t.Id)) { StateButtonTool st = (StateButtonTool)mgr.Tools[ChaveTema + t.Id]; if (st.Checked != (t == DarkEngine.TemaAtual)) st.Checked = t == DarkEngine.TemaAtual; }
                if (mgr.Tools.Exists(ChaveModo)) { StateButtonTool m = (StateButtonTool)mgr.Tools[ChaveModo]; if (m.Checked != DarkEngine.On) m.Checked = DarkEngine.On; }
            }
            catch { }
            finally { marcando = false; }
        }

        static void AoClicar(object sender, ToolClickEventArgs e)
        {
            if (marcando || e.Tool == null) return;
            string k = e.Tool.Key ?? "";
            if (!k.StartsWith("GxDarkTheme.", StringComparison.Ordinal)) return;
            try
            {
                if (k.StartsWith(ChaveTema, StringComparison.Ordinal)) Escolher(k.Substring(ChaveTema.Length));
                else if (k == ChaveModo) DarkEngine.Toggle();
                else if (k == ChaveCfg) Configuracao.Exibir(main);
            }
            catch (Exception ex) { DarkEngine.Log("Menu Tema escuro (clique): " + ex.Message); }
            Marcar();
        }

        static void Escolher(string id)
        {
            // o clique ainda esta dentro do evento do menu: troca depois de o menu fechar
            Form f = main ?? AcharMain();
            MethodInvoker a = delegate { DarkEngine.SetTheme(id, true); if (!DarkEngine.On) DarkEngine.SetMode(true, true); };
            if (f != null && f.IsHandleCreated) f.BeginInvoke(a); else a();
        }

        // ---- botao direito no sol/lua: o mesmo menu, em ContextMenuStrip (independe do menu Tools) ----
        public static void MostrarNoBotao(Control dono, Point p)
        {
            ContextMenuStrip cm = new ContextMenuStrip();
            cm.Items.Add(new ToolStripLabel("Tema escuro") { Font = new Font(cm.Font, FontStyle.Bold) });
            foreach (Tema t in Temas.Todos)
            {
                Tema tt = t;
                ToolStripMenuItem it = new ToolStripMenuItem(t.Nome) { Checked = t == DarkEngine.TemaAtual, ToolTipText = t.Descricao };
                it.Click += delegate { Escolher(tt.Id); };
                cm.Items.Add(it);
            }
            cm.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem modo = new ToolStripMenuItem("Modo escuro ligado") { Checked = DarkEngine.On, ShortcutKeyDisplayString = "Ctrl+Shift+F12" };
            modo.Click += delegate { DarkEngine.Toggle(); };
            cm.Items.Add(modo);
            ToolStripMenuItem cfg = new ToolStripMenuItem("Configurações...");
            cfg.Click += delegate { Configuracao.Exibir(dono.FindForm()); };
            cm.Items.Add(cfg);
            cm.Closed += delegate { dono.BeginInvoke((MethodInvoker)delegate { cm.Dispose(); }); };
            cm.Show(dono, p);
        }

        // retira os itens antes de o GeneXus gravar o layout das barras; se o fechamento for cancelado o timer os repoe
        static void Retirar()
        {
            if (mgr == null) return;
            try
            {
                PopupMenuTool tools = MenuTools();
                if (tools != null)
                    for (int i = tools.Tools.Count - 1; i >= 0; i--) if (tools.Tools[i].Key == ChaveSub) tools.Tools.RemoveAt(i);
                List<ToolBase> nossos = new List<ToolBase>();
                foreach (ToolBase t in mgr.Tools) if ((t.Key ?? "").StartsWith("GxDarkTheme.", StringComparison.Ordinal)) nossos.Add(t);
                foreach (ToolBase t in nossos) mgr.Tools.Remove(t);
            }
            catch (Exception ex) { DarkEngine.Log("Menu Tema escuro (retirar ao fechar): " + ex.Message); }
        }

        sealed class Vigia : NativeWindow
        {
            readonly Form f;
            public Vigia(Form f) { this.f = f; if (f.IsHandleCreated) AssignHandle(f.Handle); f.HandleDestroyed += delegate { ReleaseHandle(); }; }
            protected override void WndProc(ref Message m)
            {
                if (m.Msg == 0x0010 /*WM_CLOSE*/ || (m.Msg == 0x0112 /*WM_SYSCOMMAND*/ && ((int)m.WParam & 0xFFF0) == 0xF060 /*SC_CLOSE*/) || m.Msg == 0x0011 /*WM_QUERYENDSESSION*/)
                {
                    fechando = true;
                    Retirar();
                    base.WndProc(ref m);
                    // fechamento cancelado (ex.: "salvar alteracoes?" -> Cancelar): repoe na proxima passada
                    try { if (!f.IsDisposed) f.BeginInvoke((MethodInvoker)delegate { fechando = false; }); } catch { }
                    return;
                }
                base.WndProc(ref m);
            }
        }
    }
}
