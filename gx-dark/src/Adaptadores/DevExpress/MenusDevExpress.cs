// Xpz.Dark - menus da DevExpress (Variables, Structure) no mesmo visual escuro.
//
// A arvore DevExpress (TreeList) mostra os menus dela (cabecalho de coluna, botao
// direito) por um IDXMenuManager. O padrao desenha no estilo Office 2003: fundo claro e
// a coluna de icones a esquerda em degrade claro - a "lateral branca" relatada em 28/10.
// Este gerenciador monta o MESMO menu num ContextMenuStrip do WinForms, que a extensao
// ja desenha escuro (RendererEscuro): mesmos itens, icones, separadores, submenus,
// marcacao e clique (DXMenuItem.GenerateClickEvent - o mesmo caminho do menu original).
//
// Arquivo separado de proposito: so ele referencia a DevExpress.Utils na compilacao; a
// classe so e carregada quando ha uma arvore DevExpress.

using System;
using System.Drawing;
using System.Windows.Forms;

using DevExpress.Utils.Menu;

namespace Xpz.Dark
{
    internal sealed class MenusDevExpress : IDXMenuManager
    {
        public static readonly MenusDevExpress Instancia = new MenusDevExpress();

        private static bool _logado;

        /// <summary>Poe este gerenciador na arvore SO se ela usa o padrao da DevExpress
        /// (MenuManager vazio). Um gerenciador que o proprio GeneXus tenha posto e mantido -
        /// trocar perderia o comportamento dele. false = nada mudou.</summary>
        public static bool Ligar(object treeList)
        {
            var p = treeList.GetType().GetProperty("MenuManager");
            if (p == null || !p.CanWrite) return false;
            object atual = p.GetValue(treeList, null);
            if (atual == (object)Instancia) return false;
            if (atual != null)
            {
                if (!_logado) { _logado = true; Log.Linha("menus DevExpress: a arvore ja tem gerenciador proprio (" + atual.GetType().FullName + ") - mantido"); }
                return false;
            }
            p.SetValue(treeList, Instancia, null);
            if (!_logado) { _logado = true; Log.Linha("menus DevExpress: menus da arvore passam a ser desenhados escuros"); }
            return true;
        }

        public void ShowPopupMenu(DXPopupMenu menu, Control control, Point pos)
        {
            if (menu == null || control == null) return;
            if (!Disjuntor.Ligada(Parte.Grades)) { Padrao(menu, control, pos); return; }
            try
            {
                var cms = new ContextMenuStrip { ShowImageMargin = true };
                Montar(menu.Items, cms.Items);
                // Descarta o menu depois de fechar - sem exception dentro da mensagem do Windows:
                // se a janela dona ja fechou, BeginInvoke nela estoura dentro do callback e o
                // processo cai (0xC000041D, pego pelo TesteMenusDx em 28/10).
                cms.Closed += delegate
                {
                    try
                    {
                        if (!control.IsDisposed && control.IsHandleCreated) control.BeginInvoke((MethodInvoker)cms.Dispose);
                        else cms.Dispose();
                    }
                    catch { }
                };
                cms.Show(control, pos);
            }
            catch (Exception ex)
            {
                Disjuntor.Falhou(Parte.Grades, ex);
                Padrao(menu, control, pos);
            }
        }

        /// <summary>Se algo der errado, o menu original da DevExpress (claro, mas funciona).</summary>
        private static void Padrao(DXPopupMenu menu, Control control, Point pos)
        {
            try { new StandardMenuManager().ShowPopupMenu(menu, control, pos); } catch { }
        }

        private static void Montar(DXMenuItemCollection origem, ToolStripItemCollection destino)
        {
            foreach (DXMenuItem it in origem)
            {
                if (it == null || !it.Visible) continue;
                if (it.BeginGroup && destino.Count > 0) destino.Add(new ToolStripSeparator());
                var novo = new ToolStripMenuItem(it.Caption, it.Image) { Enabled = it.Enabled };
                if (it.Shortcut != Shortcut.None) novo.ShortcutKeyDisplayString = new KeysConverter().ConvertToString((Keys)it.Shortcut);
                var chk = it as DXMenuCheckItem;
                if (chk != null) novo.Checked = chk.Checked;
                var sub = it as DXSubMenuItem;
                if (sub != null && sub.Items.Count > 0) Montar(sub.Items, novo.DropDownItems);
                else
                {
                    DXMenuItem alvo = it;
                    novo.Click += delegate { alvo.GenerateClickEvent(); };
                }
                destino.Add(novo);
            }
        }

        public IDXMenuManager Clone(Form newForm) { return this; }
        public void DisposeManager() { }
    }
}
