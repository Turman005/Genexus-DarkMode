// Xpz.Dark - menu Tools > Tema escuro (UltraToolbarsManager da janela principal).

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
    // =====================================================================
    //  Menu Tools > Tema escuro...
    // =====================================================================
    internal static class MenuTema
    {
        private const string Chave = "XpzDarkConfig", ChaveSub = "XpzDarkMenu", ChaveTema = "XpzDarkTema.";

        private static Type TipoIg(Assembly preferida, string nome)
        {
            Type t = preferida.GetType("Infragistics.Win.UltraWinToolbars." + nome, false);
            if (t != null) return t;
            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
                if ((t = a.GetType("Infragistics.Win.UltraWinToolbars." + nome, false)) != null) return t;
            throw new InvalidOperationException("tipo Infragistics ausente: " + nome);
        }

        // Mudar Checked de um StateButtonTool DISPARA ToolClick na Infragistics: sem esta
        // trava, desmarcar o tema antigo "clicava" nele e o tema voltava (28/10).
        private static bool _marcando;

        private static void MarcarTemaAtual(dynamic mgr)
        {
            _marcando = true;
            try
            {
                foreach (Tema t in Temas.Todos)
                {
                    dynamic st = mgr.Tools[ChaveTema + t.Id];
                    bool marcado = !Paleta.Personalizado && t.Id == Paleta.TemaAtual.Id;
                    if ((bool)st.Checked != marcado) st.Checked = marcado;
                }
            }
            finally { _marcando = false; }
        }
        private static Timer _timer;
        private static int _ticks;
        private static object _gerenciador;
        private static bool _eventoLigado, _menusLogados, _abasLigadas;

        // ------------------------------------------- menu de contexto das abas
        // O botao direito numa aba de documento abre um IGContextMenu (Infragistics)
        // que desenha os itens com as cores CLARAS do Windows. Com Style = System o
        // desenho volta ao Windows, que - com o modo escuro de menus pedido no inicio -
        // desenha escuro (medido 27/10). O evento e o mesmo que o GeneXus usa para
        // montar o menu; o nosso roda depois dele.
        private static void LigarMenuDasAbas()
        {
            if (!Fase2.MenusNativos) { _abasLigadas = true; return; }
            object abas = AcharComponente("Infragistics.Win.UltraWinTabbedMdi.UltraTabbedMdiManager");
            if (abas == null) { if (_ticks == 10) Log.Linha("menu das abas: gerenciador de abas NAO achado"); return; }
            EventInfo ev = abas.GetType().GetEvent("InitializeContextMenu");
            if (ev == null) { _abasLigadas = true; Log.Linha("menu das abas: evento InitializeContextMenu ausente"); return; }
            ev.AddEventHandler(abas, Delegate.CreateDelegate(ev.EventHandlerType,
                typeof(MenuTema).GetMethod("AoMenuDaAba", BindingFlags.Static | BindingFlags.NonPublic)));
            _abasLigadas = true;
            Log.Linha("menu das abas: desenho passado ao Windows (escuro)");
        }

        private static void AoMenuDaAba(object sender, EventArgs e)
        {
            try
            {
                dynamic a = e;
                dynamic menu = a.ContextMenu;
                object estilo = menu.Style;
                menu.Style = (dynamic)Enum.Parse(estilo.GetType(), "System");
                if (_menusAbaVistos++ < 3) Registrar(menu, estilo);
            }
            catch (Exception ex) { Log.Linha("menu das abas FALHOU: " + Log.Curto(ex)); }
        }

        // ------------------------------------------- menu da barra de titulo dos paineis
        // Botao direito no titulo do Properties/Output (Dockable, Hide, Floating, Auto Hide):
        // outro IGContextMenu, o do gerenciador de paineis - claro (29/10). Mesmo remedio do
        // menu das abas, no Popup (roda antes de o menu decidir quem desenha os itens).
        private static bool _painelLigado;
        private static void LigarMenuDoPainel()
        {
            if (!Fase2.MenusNativos) { _painelLigado = true; return; }
            object dock = AcharComponente("Infragistics.Win.UltraWinDock.UltraDockManager");
            if (dock == null) { if (_ticks == 10) Log.Linha("menu dos paineis: gerenciador de paineis NAO achado"); return; }
            // propriedade interna, criada na primeira leitura (o mesmo menu que o gerenciador mostra)
            PropertyInfo p = dock.GetType().GetProperty("ContextMenu", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var menu = p == null ? null : p.GetValue(dock, null) as ContextMenu;
            _painelLigado = true;
            if (menu == null || menu.GetType().GetProperty("Style") == null) { Log.Linha("menu dos paineis: menu nao achado"); return; }
            menu.Popup += delegate
            {
                try { dynamic m = menu; object e = m.Style; m.Style = (dynamic)Enum.Parse(e.GetType(), "System"); }
                catch (Exception ex) { Log.Linha("menu dos paineis FALHOU: " + Log.Curto(ex)); }
            };
            Log.Linha("menu dos paineis: desenho passado ao Windows (escuro)");
        }

        // Diagnostico do menu das abas (29/10: itens PRETOS que ficam cinza depois do mouse).
        // Grava no dark.log o estilo e, com o menu ja na tela, as cores do fundo dos itens -
        // sem o usuario precisar parar o GeneXus para um teste.
        private static int _menusAbaVistos;
        [DllImport("user32.dll")] private static extern IntPtr FindWindow(string classe, string titulo);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr h, out RECT r);
        [StructLayout(LayoutKind.Sequential)] private struct RECT { public int L, T, R, B; }

        private static void Registrar(dynamic menu, object estiloAntes)
        {
            int itens = 0, donos = 0;
            foreach (MenuItem mi in ((Menu)menu).MenuItems) { itens++; if (mi.OwnerDraw) donos++; }
            Log.Linha("menu das abas: estilo " + estiloAntes + " -> " + menu.Style + ", " + itens + " itens (" + donos + " desenhados pelo GeneXus/Infragistics antes de abrir)");
            var t = new Timer { Interval = 500 };
            t.Tick += delegate
            {
                t.Stop(); t.Dispose();
                try
                {
                    RECT r; IntPtr h = FindWindow("#32768", null);
                    if (h == IntPtr.Zero || !GetWindowRect(h, out r) || r.R - r.L < 20) { Log.Linha("menu das abas: fechou antes da foto"); return; }
                    var cores = new SortedDictionary<string, int>();
                    using (var b = new Bitmap(1, r.B - r.T))
                    {
                        using (Graphics g = Graphics.FromImage(b)) g.CopyFromScreen(new Point(r.L + (r.R - r.L) * 3 / 4, r.T), Point.Empty, b.Size);
                        for (int y = 0; y < b.Height; y++) { Color c = b.GetPixel(0, y); string k = c.R + "," + c.G + "," + c.B; int n; cores.TryGetValue(k, out n); cores[k] = n + 1; }
                    }
                    Log.Linha("menu das abas na tela: fundo dos itens = " + string.Join("; ", cores.OrderByDescending(kv => kv.Value).Take(4).Select(kv => kv.Key + " (" + kv.Value + " px)")));
                }
                catch (Exception ex) { Log.Linha("menu das abas: foto falhou: " + Log.Curto(ex)); }
            };
            t.Start();
        }

        /// <summary>Componente da janela principal: campo da janela ou da lista de componentes dela.</summary>
        private static object AcharComponente(string tipoBase)
        {
            foreach (Form f in Application.OpenForms)
            {
                if (f.GetType().Name != "MainWindow" || Fase2.OutraThread(f)) continue;
                for (Type b = f.GetType(); b != null; b = b.BaseType)
                    foreach (FieldInfo fi in b.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly))
                    {
                        object v;
                        try { v = fi.GetValue(f); } catch { continue; }
                        if (v == null) continue;
                        if (Familia.Deriva(v.GetType(), tipoBase)) return v;
                        var cont = v as System.ComponentModel.IContainer;
                        if (cont != null)
                            foreach (System.ComponentModel.IComponent c in cont.Components)
                                if (c != null && Familia.Deriva(c.GetType(), tipoBase)) return c;
                    }
            }
            return null;
        }

        public static void Iniciar()
        {
            _timer = new Timer { Interval = 3000 };
            _timer.Tick += delegate
            {
                _ticks++;
                Disjuntor.Executar(Parte.Menu, Garantir);
                // Primeiro minuto a cada 3s (o GeneXus ainda monta menus); depois a cada 30s.
                if (_ticks == 20) _timer.Interval = 30000;
            };
            _timer.Start();
        }

        private static object Gerenciador()
        {
            if (_gerenciador != null) return _gerenciador;
            foreach (Form f in Application.OpenForms)
            {
                if (f.GetType().Name != "MainWindow" || Fase2.OutraThread(f)) continue;
                for (Type b = f.GetType(); b != null; b = b.BaseType)
                    foreach (FieldInfo fi in b.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly))
                        if (Familia.Deriva(fi.FieldType, "Infragistics.Win.UltraWinToolbars.UltraToolbarsManager"))
                        {
                            object v = fi.GetValue(f);
                            if (v != null) return _gerenciador = v;
                        }
            }
            return null;
        }

        private static void Garantir()
        {
            dynamic mgr = Gerenciador();
            if (mgr == null)
            {
                if (_ticks == 10) Log.Linha("menu: gerenciador de menus da janela principal NAO achado");
                return;
            }

            Assembly igTb = ((object)mgr).GetType().Assembly;
            if (!(bool)mgr.Tools.Exists(Chave))
            {
                dynamic botao = Activator.CreateInstance(TipoIg(igTb, "ButtonTool"), Chave);
                botao.SharedProps.Caption = "Configurações...";
                botao.SharedProps.ToolTipText = "Configurações do tema escuro do IDE (Xpz.Dark)";
                mgr.Tools.Add(botao);
            }
            // Um item por tema (com marca no tema em uso): troca na hora.
            foreach (Tema t in Temas.Todos)
            {
                string k = ChaveTema + t.Id;
                if ((bool)mgr.Tools.Exists(k)) continue;
                dynamic st = Activator.CreateInstance(TipoIg(igTb, "StateButtonTool"), k);
                st.SharedProps.Caption = t.Nome;
                st.SharedProps.ToolTipText = t.Descricao;
                mgr.Tools.Add(st);
            }
            if (!(bool)mgr.Tools.Exists(ChaveSub))
            {
                dynamic sub = Activator.CreateInstance(TipoIg(igTb, "PopupMenuTool"), ChaveSub);
                sub.SharedProps.Caption = "Tema escuro";
                mgr.Tools.Add(sub);
                foreach (Tema t in Temas.Todos) sub.Tools.AddTool(ChaveTema + t.Id);
                dynamic cfgItem = sub.Tools.AddTool(Chave);
                cfgItem.InstanceProps.IsFirstInGroup = true;
            }
            MarcarTemaAtual(mgr);

            if (!_eventoLigado)
            {
                EventInfo ev = ((object)mgr).GetType().GetEvent("ToolClick");
                Delegate d = Delegate.CreateDelegate(ev.EventHandlerType, typeof(MenuTema).GetMethod("AoClicar", BindingFlags.Static | BindingFlags.NonPublic));
                ev.AddEventHandler((object)mgr, d);
                _eventoLigado = true;
            }

            if (!_abasLigadas && Disjuntor.Ligada(Parte.MenuAbas)) LigarMenuDasAbas();
            if (!_painelLigado && Disjuntor.Ligada(Parte.MenuAbas)) LigarMenuDoPainel();

            dynamic popup = MenuTools(mgr);
            if (popup == null) return;
            bool tem = false;
            foreach (dynamic t in popup.Tools) if ((string)t.Key == ChaveSub) { tem = true; break; }
            if (!tem)
            {
                dynamic inst = popup.Tools.AddTool(ChaveSub);
                inst.InstanceProps.IsFirstInGroup = true;
                Log.Linha("menu: submenu 'Tema escuro' posto no menu " + ((string)popup.SharedProps.Caption).Replace("&", ""));
            }
        }

        private static void EscolherTema(string id)
        {
            Tema t = Temas.Achar(id);
            if (t == null) return;
            try
            {
                var v = ArquivoConfig.Atual();
                bool ativo = v.ContainsKey("Ativo") && v["Ativo"] != "nao";
                v["Tema"] = t.Id;
                ArquivoConfig.Gravar(v);
                if (ativo && Fase2.Ligada) Fase2.TrocarTema(t);
                else Log.Linha("tema " + t.Nome + " gravado; vale ao reabrir (tema desligado agora)");
                if (_gerenciador != null) MarcarTemaAtual((dynamic)_gerenciador);
            }
            catch (Exception ex) { Log.Linha("troca de tema FALHOU: " + Log.Curto(ex)); }
        }

        private static dynamic MenuTools(dynamic mgr)
        {
            var nomes = new[] { "tools", "ferramentas", "herramientas" };
            foreach (dynamic tb in mgr.Toolbars)
                foreach (dynamic t in tb.Tools)
                {
                    object o = t;
                    // O GeneXus usa um tipo DERIVADO do PopupMenuTool - comparar o nome exato
                    // fazia a busca falhar em silencio (27/10).
                    if (!Familia.Deriva(o.GetType(), "Infragistics.Win.UltraWinToolbars.PopupMenuTool")) continue;
                    string cap = ((string)t.SharedProps.Caption ?? "").Replace("&", "").Trim().ToLowerInvariant();
                    string key = ((string)t.Key ?? "").ToLowerInvariant();
                    if (nomes.Contains(cap) || nomes.Any(nm => key.EndsWith(nm))) return t;
                }
            if (!_menusLogados)
            {
                _menusLogados = true;
                var l = new List<string>();
                foreach (dynamic tb in mgr.Toolbars)
                    foreach (dynamic t in tb.Tools)
                        l.Add((string)tb.Key + "/" + (string)t.Key + "=\"" + (string)t.SharedProps.Caption + "\" (" + ((object)t).GetType().Name + ")");
                Log.Linha("menu: menu Tools NAO achado. Itens das barras: " + string.Join("; ", l.Take(60).ToArray()));
            }
            return null;
        }

        private static void AoClicar(object sender, EventArgs e)
        {
            try
            {
                if (_marcando) return;
                dynamic a = e;
                string key = (string)a.Tool.Key;
                if (key.StartsWith(ChaveTema)) { EscolherTema(key.Substring(ChaveTema.Length)); return; }
                if (key != Chave) return;
                Form dono = Form.ActiveForm;
                if (dono == null) foreach (Form f in Application.OpenForms) { if (Fase2.OutraThread(f)) continue; dono = f; break; }
                TelaConfiguracao.Exibir(dono);
            }
            catch (Exception ex) { Log.Linha("menu: clique FALHOU: " + Log.Curto(ex)); }
        }
    }
}
