// Xpz.Dark - ADAPTADORES: quem trata cada familia de controle, e em que ordem.
//
// Cada adaptador diz a que biblioteca pertence, a que PARTE do disjuntor responde,
// em que ETAPA roda, se ATENDE um controle e como o APLICA. A Fase2.Pintar so percorre
// esta lista - a regra de "que controle e de quem" mora aqui, num lugar so.
//
// Etapas, na ordem:
//   Exclusivo        trata o controle sozinho e encerra (editor, paginas web, arvores
//                    DevExpress, grade Infragistics, rolagem Actipro, barra de status)
//   AntesDoTema      roda e segue (ex.: rolagem Infragistics, antes de a familia
//                    Infragistics ser deixada para o .isl)
//   -- controles de biblioteca com tema proprio param aqui (o .isl cuida) --
//   Especial         casos particulares de WinForms/Crownwood/VisualHint, antes da
//                    regra geral de cor
//   -- regra geral: so cor padrao/neutra --
//   Janela           o que depende do handle do Windows (titulo, bordas, desenho por cima)
//
// Um erro num adaptador conta so para a parte dele (Disjuntor) e nao impede os outros.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Xpz.Dark
{
    internal enum Etapa { Exclusivo, AntesDoTema, Especial, Janela }

    internal interface IAdaptador
    {
        string Nome { get; }
        string Biblioteca { get; }
        string Parte { get; }
        Etapa Etapa { get; }
        bool Atende(Control c);
        /// <summary>true = mudou algo (entra na contagem de desempenho).</summary>
        bool Aplicar(Control c);
    }

    /// <summary>Adaptador simples: nome + duas funcoes. Os da Xpz.Dark sao todos assim.</summary>
    internal sealed class Adaptador : IAdaptador
    {
        private readonly Func<Control, bool> _atende, _aplicar;
        public string Nome { get; private set; }
        public string Biblioteca { get; private set; }
        public string Parte { get; private set; }
        public Etapa Etapa { get; private set; }

        public Adaptador(Etapa etapa, string biblioteca, string nome, string parte, Func<Control, bool> atende, Func<Control, bool> aplicar)
        {
            Etapa = etapa; Biblioteca = biblioteca; Nome = nome; Parte = parte; _atende = atende; _aplicar = aplicar;
        }

        public bool Atende(Control c) { return _atende(c); }
        public bool Aplicar(Control c) { return _aplicar(c); }
        public override string ToString() { return Biblioteca + "/" + Nome; }
    }

    internal static partial class Fase2
    {
        private static List<IAdaptador> _adaptadores;

        /// <summary>A lista, na ordem em que rodam. Lida na primeira pintura (as opcoes
        /// do dark.ini - botoes, combos, rolagem - ja estao carregadas).</summary>
        internal static List<IAdaptador> Adaptadores
        {
            get
            {
                if (_adaptadores != null) return _adaptadores;
                var l = new List<IAdaptador>
                {
                    // ---------------------------------------------- Exclusivo
                    new Adaptador(Etapa.Exclusivo, "Actipro", "editor de codigo", Parte.Editor,
                        c => Editor.Ligado && Editor.Eh(c), c => { Editor.Escurecer(c); return true; }),
                    new Adaptador(Etapa.Exclusivo, "Chromium", "paginas web (Output, Start Page)", Parte.Web,
                        c => Web.Eh(c), c => { Web.Registrar(c); return false; }),
                    new Adaptador(Etapa.Exclusivo, "DevExpress", "arvores (Variables, Structure)", Parte.Grades,
                        c => Grades.EhTreeList(c), c => { Grades.Registrar(c); return true; }),
                    new Adaptador(Etapa.Exclusivo, "DevExpress", "caixas e paineis (seletor de revisao do comparador)", Parte.Grades,
                        c => Grades.EhCaixaDx(c) || Grades.EhPainelDx(c), c => { Grades.Registrar(c); return true; }),
                    new Adaptador(Etapa.Exclusivo, "DevExpress", "barras de rolagem das arvores", Parte.Rolagem,
                        c => _rolagem && RolagemDevExpress.Eh(c), c => { RolagemDevExpress.Ligar(c); return true; }),
                    new Adaptador(Etapa.Exclusivo, "Infragistics", "grade de objetos", Parte.Grades,
                        c => Grades.EhUltraGrid(c), c => { Grades.Registrar(c); return true; }),
                    new Adaptador(Etapa.Exclusivo, "Actipro", "barras de rolagem do editor", Parte.Editor,
                        c => Editor.Ligado && Editor.EhRolagem(c), c => { Editor.Rolagem(c); return true; }),
                    new Adaptador(Etapa.Exclusivo, "Actipro", "lista de sugestoes do editor (IntelliPrompt)", Parte.Editor,
                        c => Editor.Ligado && ListaSugestao.Eh(c), c => { ListaSugestao.Ligar((ListBox)c); return true; }),
                    new Adaptador(Etapa.Exclusivo, "Actipro", "dicas do editor (parametros, QuickInfo)", Parte.Editor,
                        c => Editor.Ligado && DicaEditor.Eh(c), c => { DicaEditor.Ligar(c); return true; }),
                    new Adaptador(Etapa.Exclusivo, "WinForms", "barra de status antiga", Parte.BarraStatus,
                        c => c is StatusBar, c => { BarraStatus.Escurecer((StatusBar)c); return true; }),

                    // ---------------------------------------------- AntesDoTema
                    new Adaptador(Etapa.AntesDoTema, "Infragistics", "barras de rolagem (KB Explorer)", Parte.Grades,
                        c => Grades.TemRolagemIg(c), c => { Grades.Registrar(c); return false; }),

                    // ---------------------------------------------- Especial
                    new Adaptador(Etapa.Especial, "VisualHint", "Properties / Options", Parte.Grades,
                        c => Grades.EhSmart(c), c => { Grades.Registrar(c); return true; }),
                    new Adaptador(Etapa.Especial, "GeneXus", "arvore do comparador de versoes", Parte.Comparador,
                        // Web Layout, Variables, Structure... no History > Compare: fundo branco fixo.
                        c => Comparador.EhArvore(c), c => Comparador.Ligar(c)),
                    new Adaptador(Etapa.Especial, "WinForms", "PropertyGrid", Parte.Janelas,
                        c => c is PropertyGrid, c =>
                        {
                            var pg = (PropertyGrid)c;
                            PintarPropertyGrid(pg);
                            // Alguns PropertyGrid do GeneXus repoem as cores ao trocar o objeto.
                            pg.SelectedObjectsChanged += delegate { try { PintarPropertyGrid(pg); } catch { } };
                            return true;
                        }),
                    new Adaptador(Etapa.Especial, "Crownwood", "abas das partes do objeto", Parte.Abas,
                        // Structure, Web Layout, Rules, Events...: cores de texto proprias que ficavam pretas.
                        c => c.GetType().FullName == "Crownwood.Magic.Controls.TabControl", c => PintarAbas(c)),
                    new Adaptador(Etapa.Especial, "WinForms", "titulo de GroupBox", Parte.Janelas,
                        // Titulo de GroupBox ignora ForeColor com o estilo visual do Windows.
                        c => c is GroupBox && ((GroupBox)c).FlatStyle != FlatStyle.Flat,
                        c => { ((GroupBox)c).FlatStyle = FlatStyle.Flat; return true; }),
                    new Adaptador(Etapa.Especial, "WinForms", "rotulo desativado legivel", Parte.Janelas,
                        // O WinForms escurece a cor de fundo para desenhar o texto apagado - em
                        // fundo escuro ele some. Com fundo transparente usa o cinza do sistema.
                        c => c is Label && !(c is LinkLabel) && FundoEscuroAoRedor(c) && (Paleta.FundoClaro(c.BackColor) || !Explicito(c, true)),
                        c => { c.BackColor = Color.Transparent; return true; }),
                    new Adaptador(Etapa.Especial, "WinForms", "faixas de ferramentas e menus", Parte.Janelas,
                        c => c is ToolStrip && _renderer != null, c => PintarFaixa((ToolStrip)c)),
                    new Adaptador(Etapa.Especial, "WinForms", "arvores (TreeView, GxTreeView)", Parte.Janelas,
                        c => c is TreeView, c => PintarArvore((TreeView)c)),
                    new Adaptador(Etapa.Especial, "WinForms", "DataGridView", Parte.Janelas,
                        c => c is DataGridView, c => PintarGrade((DataGridView)c)),
                    new Adaptador(Etapa.Especial, "WinForms", "links", Parte.Janelas,
                        c => c is LinkLabel, c =>
                        {
                            var ll = (LinkLabel)c;
                            ll.LinkColor = Paleta.Link; ll.ActiveLinkColor = Paleta.Link;
                            ll.VisitedLinkColor = Color.FromArgb(197, 134, 192);
                            return true;
                        }),
                    new Adaptador(Etapa.Especial, "WinForms", "botoes planos", Parte.Janelas,
                        c =>
                        {
                            var bt = c as ButtonBase;
                            return bt != null && _botoes && !(c is CheckBox) && !(c is RadioButton) &&
                                   (bt.FlatStyle == FlatStyle.Standard || bt.FlatStyle == FlatStyle.System);
                        },
                        c =>
                        {
                            var bt = (ButtonBase)c;
                            bt.FlatStyle = FlatStyle.Flat;
                            bt.FlatAppearance.BorderColor = Paleta.Borda;
                            bt.FlatAppearance.MouseOverBackColor = Paleta.Realce;
                            bt.FlatAppearance.MouseDownBackColor = Paleta.Pressionado;
                            bt.UseVisualStyleBackColor = false;
                            bt.BackColor = Paleta.FundoElevado;
                            bt.ForeColor = Paleta.Texto;
                            return true;
                        }),
                    new Adaptador(Etapa.Especial, "WinForms", "combos planos", Parte.Janelas,
                        // Com estilo visual do Windows o combo ignora BackColor. Plano, obedece.
                        c => c is ComboBox && _combos && ((ComboBox)c).FlatStyle == FlatStyle.Standard,
                        c => { ((ComboBox)c).FlatStyle = FlatStyle.Flat; return true; }),
                    new Adaptador(Etapa.Especial, "WinForms", "paginas de TabControl", Parte.Janelas,
                        c => c is TabPage && ((TabPage)c).UseVisualStyleBackColor,
                        c => { var tp = (TabPage)c; tp.UseVisualStyleBackColor = false; tp.BackColor = Paleta.FundoPainel; return true; }),

                    // ---------------------------------------------- Janela (handle do Windows)
                    new Adaptador(Etapa.Janela, "Win32", "barra de titulo escura", Parte.Janelas,
                        c => c is Form && _titulo, c => { QuandoTiverHandle(c, TituloEscuro); return false; }),
                    new Adaptador(Etapa.Janela, "WinForms", "borda das caixas de texto", Parte.Janelas,
                        // A borda 3D do Windows e branca no escuro e o tema "DarkMode_CFD" nao pega
                        // em TextBox WinForms (medido 28/10). A borda simples sai cinza e discreta.
                        c => c is TextBox && ((TextBox)c).BorderStyle == BorderStyle.Fixed3D,
                        c => { ((TextBox)c).BorderStyle = BorderStyle.FixedSingle; return true; }),
                    new Adaptador(Etapa.Janela, "VisualHint", "borda do Properties", Parte.Janelas,
                        // BorderStyle FixedSingle = borda simples do Windows, branca no escuro.
                        c => Grades.EhSmart(c) && Convert.ToString(c.GetType().GetProperty("BorderStyle").GetValue(c, null)) == "FixedSingle",
                        c => { BordaSimples.Ligar(c); return false; }),
                    new Adaptador(Etapa.Janela, "WinForms", "contorno de foco das caixas de texto", Parte.Foco,
                        // A caixa com o cursor ganha borda na cor de destaque (como o VS Code).
                        c => c is TextBox && ((TextBox)c).BorderStyle == BorderStyle.FixedSingle,
                        c => { BordaFoco.Ligar((TextBox)c); return false; }),
                    new Adaptador(Etapa.Janela, "WinForms", "seta e borda dos combos", Parte.Combo,
                        // Combo plano: a caixa da seta e a borda saem brancas - desenhadas por cima.
                        c => c is ComboBox && _combos, c => { ComboEscuro.Ligar((ComboBox)c); return false; }),
                    new Adaptador(Etapa.Janela, "WinForms", "cabecalho de TabControl", Parte.Abas,
                        // ex.: aba "General" do Properties
                        c => c is TabControl, c => { AbasWinForms.Ligar((TabControl)c); return true; }),
                    new Adaptador(Etapa.Janela, "WinForms", "barra de rolagem avulsa", Parte.Rolagem,
                        // VScrollBar/HScrollBar (ex.: Web Layout) nao aceitam o tema escuro do Windows.
                        c => c is ScrollBar && _rolagem, c => { RolagemNativa.Ligar((ScrollBar)c); return true; }),
                    new Adaptador(Etapa.Janela, "Win32", "barras de rolagem das janelas", Parte.Janelas,
                        c => _rolagem && (c is TreeView || c is ListView || c is ListBox || c is DataGridView ||
                                          (c is TextBoxBase && ((TextBoxBase)c).Multiline) ||
                                          (c is ScrollableControl && ((ScrollableControl)c).AutoScroll)),
                        c => { RolagemEscuraQuandoPuder(c); return false; }),
                };
                return _adaptadores = l;
            }
        }

        /// <summary>Roda um adaptador protegido pelo disjuntor da parte dele.</summary>
        private static bool Executar(IAdaptador a, Control c)
        {
            if (!Disjuntor.Ligada(a.Parte)) return false;
            try { return a.Aplicar(c); }
            catch (Exception ex) { Disjuntor.Falhou(a.Parte, ex); return false; }
        }

        private static bool Atende(IAdaptador a, Control c)
        {
            try { return a.Atende(c); } catch { return false; }
        }

        private static bool PintarFaixa(ToolStrip ts)
        {
            bool mudou = false;
            if (ts.Renderer != _renderer) { ts.Renderer = _renderer; mudou = true; }
            if (Paleta.TextoEscuro(ts.ForeColor)) { ts.ForeColor = Paleta.Texto; mudou = true; }
            // Caixa de texto/combo dentro da faixa (Filter do Properties, Show do Output): a cor
            // mora no ITEM, que o GeneXus repoe em branco. Escurece o item e reage quando mudar.
            foreach (ToolStripItem it in ts.Items) ItemHospedado(it);
            ts.ItemAdded += delegate(object s, ToolStripItemEventArgs e) { ItemHospedado(e.Item); };
            return mudou;
        }

        private static bool PintarArvore(TreeView tv)
        {
            bool mudou = false;
            if (tv.LineColor.ToArgb() == Color.Black.ToArgb()) { tv.LineColor = Paleta.Borda; mudou = true; }
            // GxTreeView (arvore da janela Options) desenha o texto SEMPRE com
            // SystemColors.ControlText - preto. Desenho padrao do Windows usa ForeColor.
            if (tv.DrawMode != TreeViewDrawMode.Normal && tv.GetType().FullName == "Artech.FrameworkDE.Controls.GxTreeView")
            {
                tv.DrawMode = TreeViewDrawMode.Normal; mudou = true;
            }
            return mudou;
        }
    }
}
