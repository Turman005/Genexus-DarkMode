// Xpz.Dark - tela de configuracao do tema (menu "Tema Escuro").
//
// Edita o dark.ini e mais nada. As cores sao aplicadas na SUBIDA do GeneXus, por
// isso a tela avisa: o que mudar aqui vale ao reabrir. A previa do editor mostra
// o resultado ja com as cores escolhidas, antes de salvar.
//
// Sem dependencia da Actipro: a previa usa Ajuste (a mesma conta do editor).

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Xpz.Dark
{
    /// <summary>Le e grava o dark.ini inteiro, comentado. Chave desconhecida e preservada.</summary>
    internal static class ArquivoConfig
    {
        public static readonly string[] ChavesCor =
            { "CorFundoCodigo", "CorFundoEntrada", "CorFundoPainel", "CorTexto", "CorSelecao", "CorLink" };

        public static Dictionary<string, string> Padrao()
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Ativo", "sim" },
                { "Office2007ColorScheme", "Black" }, { "Office2010ColorScheme", "Black" },
                { "CarregarIsl", "sim" }, { "ClarearIconesMoldura", "sim" },
                { "Fase2", "sim" }, { "Botoes", "sim" }, { "CombosPlanos", "sim" }, { "TituloEscuro", "sim" },
                { "BarrasRolagemEscuras", "sim" }, { "ToolStripsEscuros", "sim" }, { "FundoWebLayout", "sim" },
                { "OutputEscuro", "sim" }, { "PaginasWebEscuras", "sim" }, { "MenusNativosEscuros", "sim" }, { "Auditoria", "nao" }, { "EscurecerAntesDeMostrar", "sim" },
                { "EditorEscuro", "sim" }, { "EditorAjustarSintaxe", "sim" }, { "EditorContraste", "4.5" },
                { "Tema", Temas.Padrao }, { "EditorSintaxeDoTema", "sim" },
                { "Diagnostico", "nao" }, { "Censo", "nao" },
            };
            Color[] seis = Paleta.SeisDoTema(Temas.Achar(Temas.Padrao));
            for (int i = 0; i < ChavesCor.Length; i++) d[ChavesCor[i]] = Paleta.Texto3(seis[i]);
            return d;
        }

        /// <summary>O que esta no disco, completado com o padrao.</summary>
        public static Dictionary<string, string> Atual()
        {
            var d = Padrao();
            var lido = Config.Ler();
            if (lido == null) { d["Ativo"] = "nao"; return d; }
            foreach (var kv in lido) d[kv.Key] = kv.Value;
            return d;
        }

        private static readonly string[][] Secoes =
        {
            new[] { "GERAL", "Ativo", "Liga/desliga tudo. nao = o GeneXus fica como veio de fabrica." },
            new[] { "MOLDURA (fase 1)", "CarregarIsl", "Tema escuro das barras, menus, paineis e abas de documento (tema-escuro.isl)." },
            new[] { null, "ClarearIconesMoldura", "Clarear os icones de traco escuro da moldura (alfinete, fechar, setas)." },
            new[] { null, "Office2007ColorScheme", "Tabela de cor global do Infragistics: Black, Blue ou Silver." },
            new[] { null, "Office2010ColorScheme", "Idem, estilo 2010." },
            new[] { "JANELAS E CONTROLES (fase 2)", "Fase2", "PropertyGrid, caixas de texto, listas, arvores, dialogos." },
            new[] { null, "Botoes", "Botoes planos (com o visual do Windows eles ignoram a cor de fundo)." },
            new[] { null, "CombosPlanos", "Combos planos, pelo mesmo motivo." },
            new[] { null, "TituloEscuro", "Barra de titulo escura nas janelas (Windows 10 2004+ / 11)." },
            new[] { null, "BarrasRolagemEscuras", "Barras de rolagem escuras em arvores, listas e caixas de texto." },
            new[] { null, "ToolStripsEscuros", "Menus e faixas de ferramentas WinForms, inclusive menus de contexto." },
            new[] { null, "FundoWebLayout", "Escurecer so a moldura em volta do Web Layout. Os controles da tela ficam como o GeneXus desenha." },
            new[] { null, "OutputEscuro", "Janela Output escura (editor Monaco dentro do IDE)." },
            new[] { null, "PaginasWebEscuras", "Start Page e Launchpad escuros (inverte a pagina web; imagens ficam normais)." },
            new[] { "EDITOR DE CODIGO (fase 3)", "EditorEscuro", "Fundo escuro no Source, Rules, Events, Conditions..." },
            new[] { null, "EditorAjustarSintaxe", "Clarear as cores da sintaxe mantendo o matiz (azul segue azul)." },
            new[] { null, "EditorContraste", "Contraste minimo do texto do codigo: 4.5 (normal) ou 7 (alto)." },
            new[] { null, "EditorSintaxeDoTema", "Cores de sintaxe do tema (estilo VS Code). nao = clarear as cores do GeneXus." },
            new[] { "CORES", "Tema", "Tema: ardosia, grafite, meianoite, ametista, brasa, ambar, oliva - ou Personalizado (usa as Cor* abaixo)." },
            new[] { null, "CorFundoCodigo", "Fundo do editor de codigo (R, G, B)." },
            new[] { null, "CorFundoEntrada", "Fundo de caixas de texto, listas e grades." },
            new[] { null, "CorFundoPainel", "Fundo de paineis e dialogos." },
            new[] { null, "CorTexto", "Texto." },
            new[] { null, "CorSelecao", "Selecao." },
            new[] { null, "CorLink", "Links e destaques." },
            new[] { "DIAGNOSTICO", "Diagnostico", "10s apos a subida, grava de onde vem cada cor em dark-diagnostico.log." },
            new[] { null, "Censo", "Grava em dark-censo.log que controles existem em cada janela." },
            new[] { null, "Auditoria", "Fotografa as janelas e anota em dark-auditoria.log o que ainda esta claro." },
            new[] { null, "MenusNativosEscuros", "Pede ao Windows menus de contexto nativos escuros." },
            new[] { null, "EscurecerAntesDeMostrar", "Escurece cada janela nova antes de ela aparecer (sem piscar branco)." },
        };

        public static void Gravar(Dictionary<string, string> v)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Xpz.Dark - dark mode do IDE do GeneXus. EXPERIMENTO, so na U10.");
            sb.AppendLine("# Gerado pela tela Tema Escuro > Configuracoes em " + DateTime.Now.ToString("dd/MM/yyyy HH:mm") + ".");
            sb.AppendLine("# Pode editar a mao: vale ao reabrir o GeneXus. Apagar este arquivo = desligar.");
            var escritas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string[] s in Secoes)
            {
                if (s[0] != null) { sb.AppendLine(); sb.AppendLine("# ------------------------------------------------------------ " + s[0]); }
                string valor;
                if (!v.TryGetValue(s[1], out valor)) continue;
                sb.AppendLine("# " + s[2]);
                sb.AppendLine(s[1] + " = " + valor);
                escritas.Add(s[1]);
            }
            var resto = v.Keys.Where(k => !escritas.Contains(k)).OrderBy(k => k).ToList();
            if (resto.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("# ------------------------------------------------------------ OUTRAS (preservadas)");
                foreach (string k in resto) sb.AppendLine(k + " = " + v[k]);
            }
            File.WriteAllText(Path.Combine(Config.Pasta(), "dark.ini"), sb.ToString(), new UTF8Encoding(false));
        }
    }

    internal sealed class TelaConfiguracao : Form
    {
        // A tela usa sempre a paleta escura PADRAO: ela precisa ficar legivel mesmo
        // com o tema desligado ou com cores personalizadas ruins.
        private static readonly Color CFundo = Color.FromArgb(37, 37, 38), CLado = Color.FromArgb(30, 30, 30),
            CEntrada = Color.FromArgb(51, 51, 55), CBorda = Color.FromArgb(70, 70, 76), CTexto = Color.FromArgb(230, 230, 230),
            CApagado = Color.FromArgb(160, 160, 160), CDestaque = Color.FromArgb(0, 122, 204), CRealce = Color.FromArgb(62, 62, 66);

        private readonly Dictionary<string, string> _v;
        private readonly Dictionary<string, CheckBox> _checks = new Dictionary<string, CheckBox>();
        private readonly Dictionary<string, Button> _cores = new Dictionary<string, Button>();
        private readonly List<ItemNav> _nav = new List<ItemNav>();
        private readonly List<Panel> _paginas = new List<Panel>();
        private ComboBox _tema, _contraste;
        private Panel _previa;
        private bool _carregandoTema;
        private Label _descTema;
        private Label _aviso;

        public TelaConfiguracao()
        {
            _v = ArquivoConfig.Atual();

            Text = "Tema escuro do GeneXus — configurações";
            Font = new Font("Segoe UI", 9f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(820, 640);
            BackColor = CFundo; ForeColor = CTexto;

            // ------------------------------------------------ barra inferior
            var rodape = new Panel { Dock = DockStyle.Bottom, Height = 52, BackColor = CLado };
            var aviso = _aviso = new Label
            {
                Text = "O tema vale na hora; as demais opções, ao reabrir o GeneXus.", ForeColor = CApagado, AutoSize = false,
                Left = 16, Top = 0, Width = 300, Height = 52, TextAlign = ContentAlignment.MiddleLeft
            };
            rodape.Controls.Add(aviso);
            var cancelar = Botao("Cancelar", false); cancelar.Click += delegate { Close(); };
            var salvar = Botao("Salvar", true); salvar.Click += delegate { Salvar(); };
            var pasta = Botao("Abrir pasta", false); pasta.Click += delegate { AbrirPasta(); };
            var padrao = Botao("Restaurar padrão", false); padrao.Width = 130; padrao.Click += delegate { RestaurarPadrao(); };
            int x = 820 - 16;
            foreach (Button b in new[] { cancelar, salvar, pasta, padrao })
            {
                x -= b.Width; b.Left = x; b.Top = 12; rodape.Controls.Add(b); x -= 8;
            }
            CancelButton = cancelar;

            // ------------------------------------------------ navegacao
            var lado = new Panel { Dock = DockStyle.Left, Width = 200, BackColor = CLado, Padding = new Padding(0, 16, 0, 0) };
            var marca = new Label
            {
                Text = "Tema Escuro", Font = new Font("Segoe UI Semibold", 13f), ForeColor = CTexto,
                AutoSize = false, Dock = DockStyle.Top, Height = 34, Padding = new Padding(16, 0, 0, 0)
            };
            var sub = new Label
            {
                Text = "Xpz.Dark · GeneXus 18", ForeColor = CApagado, AutoSize = false,
                Dock = DockStyle.Top, Height = 30, Padding = new Padding(17, 0, 0, 8)
            };

            var conteudo = new Panel { Dock = DockStyle.Fill, BackColor = CFundo, Padding = new Padding(28, 20, 28, 8) };

            string[] titulos = { "Geral", "Editor de código", "Janelas e controles", "Temas", "Diagnóstico" };
            Func<Panel>[] montar = { PaginaGeral, PaginaEditor, PaginaJanelas, PaginaCores, PaginaDiagnostico };
            var itens = new List<ItemNav>();
            for (int i = 0; i < titulos.Length; i++)
            {
                Panel p = montar[i]();
                p.Dock = DockStyle.Fill; p.Visible = i == 0;
                conteudo.Controls.Add(p); _paginas.Add(p);
                var it = new ItemNav(titulos[i]) { Dock = DockStyle.Top, Height = 36 };
                int idx = i;
                it.Click += delegate { Mostrar(idx); };
                itens.Add(it); _nav.Add(it);
            }
            // Dock=Top empilha de baixo para cima: adicionar em ordem inversa.
            for (int i = itens.Count - 1; i >= 0; i--) lado.Controls.Add(itens[i]);
            lado.Controls.Add(sub);
            lado.Controls.Add(marca);
            _nav[0].Selecionado = true;

            Controls.Add(conteudo);
            Controls.Add(lado);
            Controls.Add(rodape);
            AtualizarHabilitados();
        }

        [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int valor, int tamanho);

        [System.Runtime.InteropServices.DllImport("uxtheme.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hwnd, string app, string lista);

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                int v = 1;
                if (DwmSetWindowAttribute(Handle, 20, ref v, 4) != 0) DwmSetWindowAttribute(Handle, 19, ref v, 4);
            }
            catch { }
        }

        private static void RolagemEscura(Control c)
        {
            EventHandler h = delegate { try { SetWindowTheme(c.Handle, "DarkMode_Explorer", null); } catch { } };
            if (c.IsHandleCreated) h(c, EventArgs.Empty); else c.HandleCreated += h;
        }

        // ------------------------------------------------------------ paginas
        private Panel Pagina(string titulo, string descricao, out FlowLayoutPanel corpo)
        {
            var p = new Panel { BackColor = CFundo };
            corpo = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false,
                AutoScroll = true, BackColor = CFundo, Padding = new Padding(0, 8, 0, 0)
            };
            RolagemEscura(corpo);
            var d = new Label { Text = descricao, ForeColor = CApagado, AutoSize = false, Dock = DockStyle.Top, Height = 40 };
            var t = new Label { Text = titulo, Font = new Font("Segoe UI Semibold", 14f), ForeColor = CTexto, AutoSize = false, Dock = DockStyle.Top, Height = 34 };
            p.Controls.Add(corpo); p.Controls.Add(d); p.Controls.Add(t);
            return p;
        }

        private Panel PaginaGeral()
        {
            FlowLayoutPanel c;
            var p = Pagina("Geral", "Liga e desliga cada parte do tema. Desligar uma parte devolve aquela área ao visual de fábrica.", out c);
            Opcao(c, "Ativo", "Ativar o tema escuro", "Desmarcado, a extensão não altera nada no GeneXus (esta tela continua no menu).");
            Opcao(c, "CarregarIsl", "Barras, menus e painéis escuros", "Moldura do IDE: menu principal, barras de ferramentas, KB Explorer e abas de documento.");
            Opcao(c, "ClarearIconesMoldura", "Clarear ícones da moldura", "Alfinete, fechar e setas dos painéis ficam claros para aparecer no fundo escuro.");
            Opcao(c, "Fase2", "Janelas e controles escuros", "Propriedades, caixas de texto, listas, árvores e diálogos.");
            Opcao(c, "EditorEscuro", "Editor de código escuro", "Source, Rules, Events, Conditions e demais partes de texto.");
            Opcao(c, "OutputEscuro", "Output escuro", "A janela de mensagens do build e da especificação.");
            Opcao(c, "PaginasWebEscuras", "Start Page e Launchpad escuros", "Páginas web dentro do IDE. As imagens continuam com as cores normais.");
            Opcao(c, "FundoWebLayout", "Fundo escuro em volta do Web Layout", "Só a moldura em volta da tela desenhada. Os controles da tela (labels, campos, grids) ficam como o GeneXus desenha.");
            return p;
        }

        private Panel PaginaEditor()
        {
            FlowLayoutPanel c;
            var p = Pagina("Editor de código", "O fundo escurece e as cores da sintaxe são clareadas mantendo o tom: o azul das palavras reservadas continua azul, o verde dos comentários continua verde.", out c);
            Opcao(c, "EditorEscuro", "Editor de código escuro", "Fundo, margem dos números de linha, seleção e guias.");
            Opcao(c, "EditorAjustarSintaxe", "Ajustar as cores da sintaxe", "Clareia só o necessário para atingir o contraste mínimo. Desmarcado, mantém as cores do GeneXus (podem ficar escuras demais).");
            Opcao(c, "EditorSintaxeDoTema", "Cores de sintaxe do tema", "Cada tema traz a sua paleta (estilo VS Code). Desmarcado, clareia as cores originais do GeneXus.");
            c.Controls.Add(Rotulo("Contraste mínimo do código", true));
            _contraste = Combo(new[] { "Normal — 4,5:1 (recomendado)", "Alto — 7:1" });
            _contraste.SelectedIndex = Valor("EditorContraste").StartsWith("7") ? 1 : 0;
            _contraste.SelectedIndexChanged += delegate { _previa.Invalidate(); };
            c.Controls.Add(_contraste);
            c.Controls.Add(Rotulo("Prévia (cores típicas do GeneXus, já ajustadas)", true));
            _previa = new PainelPrevia(this) { Width = 520, Height = 190, Margin = new Padding(0, 4, 0, 0) };
            c.Controls.Add(_previa);
            return p;
        }

        private Panel PaginaJanelas()
        {
            FlowLayoutPanel c;
            var p = Pagina("Janelas e controles", "Ajustes dos controles comuns do Windows. Só cores neutras são trocadas: vermelho de erro e azul de link ficam como o GeneXus definiu.", out c);
            Opcao(c, "Botoes", "Botões planos", "Com o visual do Windows o botão ignora a cor de fundo e fica claro.");
            Opcao(c, "CombosPlanos", "Combos planos", "Pelo mesmo motivo dos botões.");
            Opcao(c, "TituloEscuro", "Barra de título escura", "Nas janelas e diálogos (Windows 10 2004+ e Windows 11).");
            Opcao(c, "BarrasRolagemEscuras", "Barras de rolagem escuras", "Em árvores, listas e caixas de texto.");
            Opcao(c, "ToolStripsEscuros", "Menus de contexto escuros", "Menus e faixas de ferramentas WinForms.");
            return p;
        }

        private Panel PaginaCores()
        {
            FlowLayoutPanel c;
            var p = Pagina("Temas", "Cada tema muda a moldura (menus, barras, KB Explorer), as janelas e as cores do código. A troca é na hora - também pelo menu Tools > Tema escuro.", out c);
            c.Controls.Add(Rotulo("Tema", true));
            var nomesTema = Temas.Todos.Select(x => x.Nome).ToList(); nomesTema.Add("Personalizado");
            _tema = Combo(nomesTema.ToArray());
            _tema.DrawMode = DrawMode.OwnerDrawFixed; _tema.ItemHeight = 22; _tema.DropDownHeight = 260;
            _tema.DrawItem += DesenharItemTema;
            string t = Valor("Tema");
            int idx = Temas.Todos.FindIndex(x => string.Equals(x.Id, t, StringComparison.OrdinalIgnoreCase));
            if (string.Equals(t, "Personalizado", StringComparison.OrdinalIgnoreCase)) idx = Temas.Todos.Count;
            else if (idx < 0) idx = Temas.Todos.IndexOf(Temas.DoConfig(_v));
            _tema.SelectedIndex = idx;
            _tema.SelectedIndexChanged += delegate { AplicarTema(); };
            c.Controls.Add(_tema);
            _descTema = Rotulo("", false); _descTema.Height = 26;
            c.Controls.Add(_descTema);
            c.Controls.Add(Rotulo("Cores das janelas (mudar uma cor passa o tema para Personalizado)", true));

            string[] nomes = { "Fundo do editor de código", "Fundo de campos, listas e grades", "Fundo de painéis e diálogos", "Texto", "Seleção", "Links e destaques" };
            for (int i = 0; i < nomes.Length; i++)
            {
                string chave = ArquivoConfig.ChavesCor[i];
                var linha = new Panel { Width = 520, Height = 34, BackColor = CFundo, Margin = new Padding(0, 2, 0, 0) };
                linha.Controls.Add(new Label { Text = nomes[i], ForeColor = CTexto, Left = 0, Top = 8, Width = 300, AutoSize = false });
                var b = new Button { Left = 320, Top = 3, Width = 170, Height = 28, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
                b.FlatAppearance.BorderColor = CBorda;
                PintarAmostra(b, CorDe(chave));
                b.Click += delegate { EscolherCor(chave, b); };
                linha.Controls.Add(b);
                _cores[chave] = b;
                c.Controls.Add(linha);
            }
            c.Controls.Add(Rotulo("No Personalizado, a moldura e a sintaxe seguem o tema Ardósia.", false));
            AtualizarDescricao();
            return p;
        }

        private Panel PaginaDiagnostico()
        {
            FlowLayoutPanel c;
            var p = Pagina("Diagnóstico", "Saúde da extensão e registros para entender o que ficou claro. Ficam em " + Config.Pasta(), out c);
            c.Controls.Add(Rotulo("Saúde da extensão (versão " + typeof(TelaConfiguracao).Assembly.GetName().Version.ToString(3) + ")", true));
            foreach (string linha in Saude())
            {
                var l = Rotulo(linha, false); l.AutoSize = true; l.MaximumSize = new Size(520, 0); l.Margin = new Padding(0, 4, 0, 0);
                if (linha.StartsWith("⚠")) l.ForeColor = Color.FromArgb(226, 192, 141);
                c.Controls.Add(l);
            }
            Opcao(c, "Diagnostico", "Diagnóstico da moldura", "10 s após a subida, grava de onde vem cada cor (dark-diagnostico.log).");
            Opcao(c, "Censo", "Censo de controles", "Que controles existem em cada janela e de que família (dark-censo.log).");
            Opcao(c, "Auditoria", "Varredura do que ainda está claro", "Fotografa as janelas do GeneXus e anota em dark-auditoria.log cada área clara e o controle dono dela.");
            Opcao(c, "MenusNativosEscuros", "Menus nativos do Windows escuros", "Menus de contexto que não são do GeneXus, e sim do próprio Windows.");
            var varrer = Botao("Varrer agora", true); varrer.Width = 140; varrer.Margin = new Padding(0, 12, 0, 0);
            varrer.Click += delegate
            {
                // A propria tela sai da frente para a foto pegar o GeneXus.
                Hide(); Application.DoEvents(); System.Threading.Thread.Sleep(300);
                try { Auditoria.VarrerAgora(); } finally { Show(); }
                Abrir(Path.Combine(Config.Pasta(), "dark-auditoria.log"));
            };
            c.Controls.Add(varrer);
            var abrirLog = Botao("Abrir dark.log", false); abrirLog.Width = 140; abrirLog.Margin = new Padding(0, 8, 0, 0);
            abrirLog.Click += delegate { Abrir(Path.Combine(Config.Pasta(), "dark.log")); };
            c.Controls.Add(abrirLog);
            return p;
        }

        // ------------------------------------------------------------ pecas
        private void Opcao(FlowLayoutPanel c, string chave, string titulo, string descricao)
        {
            var cb = new CheckBox
            {
                Text = titulo, Checked = Sim(chave), ForeColor = CTexto, AutoSize = true,
                FlatStyle = FlatStyle.Flat, Margin = new Padding(0, 10, 0, 0), Cursor = Cursors.Hand
            };
            cb.FlatAppearance.BorderColor = CBorda;
            cb.FlatAppearance.CheckedBackColor = CDestaque;
            cb.CheckedChanged += delegate
            {
                // A mesma chave pode aparecer em duas paginas (ex.: EditorEscuro).
                foreach (var outro in _todos.Where(o => o.Key == chave && o.Value != cb)) outro.Value.Checked = cb.Checked;
                AtualizarHabilitados();
            };
            _todos.Add(new KeyValuePair<string, CheckBox>(chave, cb));
            _checks[chave] = cb;
            c.Controls.Add(cb);
            c.Controls.Add(new Label
            {
                Text = descricao, ForeColor = CApagado, AutoSize = false, Width = 520, Height = 34,
                Margin = new Padding(20, 0, 0, 0)
            });
        }
        private readonly List<KeyValuePair<string, CheckBox>> _todos = new List<KeyValuePair<string, CheckBox>>();

        private Label Rotulo(string texto, bool forte)
        {
            return new Label
            {
                Text = texto, ForeColor = forte ? CTexto : CApagado, AutoSize = false, Width = 520, Height = forte ? 26 : 40,
                Margin = new Padding(0, forte ? 14 : 10, 0, 0),
                Font = forte ? new Font("Segoe UI Semibold", 9f) : Font, TextAlign = ContentAlignment.BottomLeft
            };
        }

        private ComboBox Combo(string[] itens)
        {
            var cb = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, Width = 300, BackColor = CEntrada, ForeColor = CTexto };
            RolagemEscura(cb);
            cb.Items.AddRange(itens);
            return cb;
        }

        private Button Botao(string texto, bool principal)
        {
            var b = new Button
            {
                Text = texto, Width = 104, Height = 28, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
                BackColor = principal ? CDestaque : CEntrada, ForeColor = Color.White, UseVisualStyleBackColor = false
            };
            b.FlatAppearance.BorderColor = principal ? CDestaque : CBorda;
            b.FlatAppearance.MouseOverBackColor = principal ? Color.FromArgb(28, 151, 234) : CRealce;
            return b;
        }

        private void Mostrar(int idx)
        {
            for (int i = 0; i < _paginas.Count; i++) { _paginas[i].Visible = i == idx; _nav[i].Selecionado = i == idx; }
        }

        private void AtualizarHabilitados()
        {
            bool ativo = Marcado("Ativo");
            foreach (var kv in _todos) if (kv.Key != "Ativo") kv.Value.Enabled = ativo;
            if (_contraste != null) _contraste.Enabled = ativo && Marcado("EditorEscuro") && Marcado("EditorAjustarSintaxe");
            if (_previa != null) _previa.Invalidate();
            if (_previa != null) _previa.Invalidate();
        }

        private bool Marcado(string chave) { CheckBox cb; return _checks.TryGetValue(chave, out cb) ? cb.Checked : Sim(chave); }

        // ------------------------------------------------------------ cores
        private Color CorDe(string chave)
        {
            Color c;
            if (Paleta.TentarCor(Valor(chave), out c)) return c;
            Paleta.TentarCor(ArquivoConfig.Padrao()[chave], out c);
            return c;
        }

        private static void PintarAmostra(Button b, Color c)
        {
            b.BackColor = c;
            b.ForeColor = Paleta.Lum(c) > 0.5 ? Color.Black : Color.White;
            b.Text = Paleta.Texto3(c);
            b.Tag = c;
        }

        private Color Amostra(string chave) { return (Color)_cores[chave].Tag; }

        private void EscolherCor(string chave, Button b)
        {
            using (var d = new ColorDialog { Color = (Color)b.Tag, FullOpen = true, AnyColor = true })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                PintarAmostra(b, d.Color);
                _carregandoTema = true; _tema.SelectedIndex = Temas.Todos.Count; _carregandoTema = false;
                AtualizarDescricao();
                if (_previa != null) _previa.Invalidate();
            }
        }

        /// <summary>Tema escolhido na lista; null = Personalizado.</summary>
        private Tema TemaEscolhido { get { return _tema != null && _tema.SelectedIndex >= 0 && _tema.SelectedIndex < Temas.Todos.Count ? Temas.Todos[_tema.SelectedIndex] : null; } }

        private void AplicarTema()
        {
            AtualizarDescricao();
            if (_carregandoTema) return;
            Tema t = TemaEscolhido;
            if (t != null)
            {
                Color[] seis = Paleta.SeisDoTema(t);
                for (int i = 0; i < seis.Length; i++) PintarAmostra(_cores[ArquivoConfig.ChavesCor[i]], seis[i]);
            }
            if (_previa != null) _previa.Invalidate();
        }

        private void AtualizarDescricao()
        {
            if (_descTema == null) return;
            Tema t = TemaEscolhido;
            _descTema.Text = t != null ? t.Descricao : "Cores escolhidas por você, abaixo.";
        }

        /// <summary>Item da lista de temas: tres bolinhas com as cores do tema + o nome.</summary>
        private void DesenharItemTema(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            bool sel = (e.State & DrawItemState.Selected) != 0;
            using (var b = new SolidBrush(sel ? CRealce : CEntrada)) e.Graphics.FillRectangle(b, e.Bounds);
            int x = e.Bounds.X + 6, cy = e.Bounds.Y + e.Bounds.Height / 2;
            Tema t = e.Index < Temas.Todos.Count ? Temas.Todos[e.Index] : null;
            var g = e.Graphics;
            var antes = g.SmoothingMode;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            Color[] cores = t != null ? t.Amostra : new[] { Amostra("CorFundoCodigo"), Amostra("CorFundoPainel"), Amostra("CorLink") };
            foreach (Color c in cores)
            {
                using (var b = new SolidBrush(c)) g.FillEllipse(b, x, cy - 6, 12, 12);
                using (var p = new Pen(CBorda)) g.DrawEllipse(p, x, cy - 6, 12, 12);
                x += 15;
            }
            g.SmoothingMode = antes;
            TextRenderer.DrawText(g, _tema.Items[e.Index].ToString(), _tema.Font, new Point(x + 6, cy - 8), CTexto, TextFormatFlags.NoPrefix);
        }

        // ------------------------------------------------------------ acoes
        private void Salvar()
        {
            try
            {
                foreach (var kv in _checks) _v[kv.Key] = kv.Value.Checked ? "sim" : "nao";
                _v["EditorContraste"] = _contraste.SelectedIndex == 1 ? "7" : "4.5";
                Tema escolhido = TemaEscolhido;
                _v["Tema"] = escolhido != null ? escolhido.Id : "Personalizado";
                foreach (string k in ArquivoConfig.ChavesCor) _v[k] = Paleta.Texto3(Amostra(k));
                ArquivoConfig.Gravar(_v);
                Log.Linha("configuracao salva pela tela");
                // Tema: aplica na hora. O resto (ligar/desligar partes) vale ao reabrir.
                bool aoVivo = escolhido != null && Fase2.Ligada && Marcado("Ativo") &&
                              (Paleta.Personalizado || Paleta.TemaAtual.Id != escolhido.Id);
                if (aoVivo) Fase2.TrocarTema(escolhido);
                // Sem MessageBox: a caixa do Windows e clara (a auditoria pegou em 27/10).
                _aviso.Text = aoVivo ? "Tema aplicado. Outras mudanças valem ao reabrir o GeneXus."
                                     : "Configuração salva. Feche e abra o GeneXus para aplicar.";
                _aviso.ForeColor = Color.FromArgb(137, 209, 133);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Não consegui salvar:\n" + ex.Message, "Tema escuro", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void RestaurarPadrao()
        {
            var p = ArquivoConfig.Padrao();
            foreach (var kv in _todos) { string v; if (p.TryGetValue(kv.Key, out v)) kv.Value.Checked = v == "sim"; }
            _contraste.SelectedIndex = 0;
            _tema.SelectedIndex = Temas.Todos.IndexOf(Temas.Achar(Temas.Padrao));
            AplicarTema();
        }

        /// <summary>Linhas do quadro de saude: modo seguro, compatibilidade, partes desligadas.</summary>
        private static List<string> Saude()
        {
            var l = new List<string>();
            l.Add(ModoSeguro.Ativo ? "⚠ Modo seguro nesta sessão: " + ModoSeguro.Motivo + ". Reabra o GeneXus sem Shift para voltar ao normal."
                                   : "✓ Sessão normal (para abrir sem a extensão: segure Shift ao abrir o GeneXus).");
            if (Compatibilidade.Total > 0)
                l.Add((Compatibilidade.Aprovados == Compatibilidade.Total ? "✓ " : "⚠ ") + "Compatibilidade com esta versão do GeneXus: " +
                      Compatibilidade.Aprovados + " de " + Compatibilidade.Total + " pontos.");
            else l.Add("Compatibilidade: não verificada nesta sessão (tema desligado ou modo seguro).");
            var desligadas = Disjuntor.Desligadas.ToList();
            if (desligadas.Count == 0) l.Add("✓ Todas as partes ativas.");
            foreach (var kv in desligadas) l.Add("⚠ Desligada nesta sessão: " + kv.Key + " — " + kv.Value);
            return l;
        }

        private void AbrirPasta() { Abrir(Config.Pasta()); }

        private void Abrir(string caminho)
        {
            try { Process.Start(new ProcessStartInfo(caminho) { UseShellExecute = true }); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Tema escuro"); }
        }

        private string Valor(string chave) { string v; return _v.TryGetValue(chave, out v) ? v ?? "" : ""; }
        private bool Sim(string chave) { string v = Valor(chave).ToLowerInvariant(); return v == "sim" || v == "s" || v == "true" || v == "1" || v == "yes"; }

        // ------------------------------------------------------------ previa do editor
        private sealed class PainelPrevia : Panel
        {
            private readonly TelaConfiguracao _t;
            public PainelPrevia(TelaConfiguracao t) { _t = t; DoubleBuffered = true; }

            // Cores tipicas do editor do GeneXus em fundo branco.
            private static readonly Color PReservada = Color.FromArgb(0, 0, 255), PComentario = Color.FromArgb(0, 128, 0),
                PString = Color.FromArgb(163, 21, 21), PVariavel = Color.FromArgb(128, 0, 128), PNumero = Color.FromArgb(0, 0, 0),
                PFuncao = Color.FromArgb(0, 0, 139);

            protected override void OnPaint(PaintEventArgs e)
            {
                bool escuro = _t.Marcado("Ativo") && _t.Marcado("EditorEscuro");
                bool ajusta = _t.Marcado("EditorAjustarSintaxe");
                Color fundo = escuro ? _t.Amostra("CorFundoCodigo") : Color.White;
                Color texto = escuro ? _t.Amostra("CorTexto") : Color.Black;
                double k = _t._contraste.SelectedIndex == 1 ? 7 : 4.5;
                Tema tema = _t.TemaEscolhido;
                bool doTema = escuro && ajusta && tema != null && _t.Marcado("EditorSintaxeDoTema");
                // cor original do GeneXus -> token do tema (mesma tabela do editor)
                var porCor = new Dictionary<int, string>
                {
                    { PReservada.ToArgb(), "palavra" }, { PComentario.ToArgb(), "comentario" }, { PString.ToArgb(), "string" },
                    { PVariavel.ToArgb(), "variavel" }, { PNumero.ToArgb(), "numero" }, { PFuncao.ToArgb(), "funcao" },
                };
                if (doTema) texto = Ajuste.Ajustar(tema.Sintaxe["texto"], "x", fundo, texto, k);
                Func<Color, Color> aj = c =>
                {
                    if (!escuro) return c;
                    string tok; Color tc;
                    if (doTema && porCor.TryGetValue(c.ToArgb(), out tok) && tema.Sintaxe.TryGetValue(tok, out tc)) return Ajuste.Ajustar(tc, "x", fundo, texto, k);
                    return ajusta ? Ajuste.Ajustar(c, "x", fundo, texto, k) : c;
                };

                var g = e.Graphics;
                g.Clear(fundo);
                using (var fonte = new Font("Consolas", 10f))
                using (var margem = new SolidBrush(escuro ? Paleta.Misturar(texto, fundo, 0.42) : Color.FromArgb(43, 145, 175)))
                {
                    var linhas = new[]
                    {
                        new[] { Tuple.Create("// Busca os pedidos em aberto do cliente", aj(PComentario)) },
                        new[] { Tuple.Create("For Each ", aj(PReservada)), Tuple.Create("Pedido ", texto), Tuple.Create("Order ", aj(PReservada)), Tuple.Create("PedDat", texto) },
                        new[] { Tuple.Create("    Where ", aj(PReservada)), Tuple.Create("CliCod = ", texto), Tuple.Create("&CliCod", aj(PVariavel)) },
                        new[] { Tuple.Create("    &Total ", aj(PVariavel)), Tuple.Create("+= ", texto), Tuple.Create("PedVal", texto) },
                        new[] { Tuple.Create("    &Msg ", aj(PVariavel)), Tuple.Create("= ", texto), Tuple.Create("Format", aj(PFuncao)), Tuple.Create("(", texto), Tuple.Create("\"Pedido %1\"", aj(PString)), Tuple.Create(", PedCod)", texto) },
                        new[] { Tuple.Create("EndFor", aj(PReservada)) },
                        new[] { Tuple.Create("If ", aj(PReservada)), Tuple.Create("&Total ", aj(PVariavel)), Tuple.Create("> ", texto), Tuple.Create("1000", aj(PNumero)) },
                        new[] { Tuple.Create("    Do ", aj(PReservada)), Tuple.Create("'AvisaGerente'", aj(PString)) },
                        new[] { Tuple.Create("EndIf", aj(PReservada)) },
                    };
                    float y = 8, alt = fonte.GetHeight(g) + 3;
                    for (int i = 0; i < linhas.Length; i++, y += alt)
                    {
                        g.DrawString((i + 1).ToString().PadLeft(2), fonte, margem, 6, y);
                        float x = 40;
                        foreach (var p in linhas[i])
                        {
                            TextRenderer.DrawText(g, p.Item1, fonte, new Point((int)x, (int)y), p.Item2, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
                            x += TextRenderer.MeasureText(g, p.Item1, fonte, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width;
                        }
                    }
                }
                using (var pen = new Pen(CBorda)) g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            }
        }

        // ------------------------------------------------------------ item de navegacao
        private sealed class ItemNav : Control
        {
            private bool _sel, _sobre;
            public ItemNav(string texto)
            {
                Text = texto; Cursor = Cursors.Hand;
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                Font = new Font("Segoe UI", 9.75f);
            }
            public bool Selecionado { get { return _sel; } set { _sel = value; Invalidate(); } }
            protected override void OnMouseEnter(EventArgs e) { _sobre = true; Invalidate(); base.OnMouseEnter(e); }
            protected override void OnMouseLeave(EventArgs e) { _sobre = false; Invalidate(); base.OnMouseLeave(e); }
            protected override void OnPaint(PaintEventArgs e)
            {
                Color fundo = _sel ? Color.FromArgb(45, 45, 48) : _sobre ? Color.FromArgb(40, 40, 42) : CLado;
                e.Graphics.Clear(fundo);
                if (_sel) using (var b = new SolidBrush(CDestaque)) e.Graphics.FillRectangle(b, 0, 6, 3, Height - 12);
                TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(18, 0, Width - 18, Height),
                    _sel ? Color.White : CApagado, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
            }
        }

        // ------------------------------------------------------------ abrir
        public static void Exibir(IWin32Window dono)
        {
            try
            {
                using (var t = new TelaConfiguracao())
                {
                    if (dono != null) t.ShowDialog(dono); else t.ShowDialog();
                }
            }
            catch (Exception ex) { Log.Linha("tela de configuracao FALHOU: " + Log.Curto(ex)); MessageBox.Show(ex.Message, "Tema escuro"); }
        }
    }
}
