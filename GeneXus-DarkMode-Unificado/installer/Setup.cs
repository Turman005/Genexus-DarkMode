// GxDarkTheme - instalador/desinstalador (assistente WinForms em pt-BR).
// Uma unica .exe: "Setup-GxDarkTheme.exe" instala; a copia em %ProgramFiles%\GxDarkTheme\Desinstalar-GxDarkTheme.exe
// (chamada por "Aplicativos instalados" do Windows) desinstala com /uninstall.
// Nunca fecha o GeneXus: se ele estiver aberto, pede para o usuario fechar e tentar de novo.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("Instalador do GxDarkTheme")]
[assembly: AssemblyProduct(GxDarkTheme.Produto.Nome)]
[assembly: AssemblyCompany("Turman005 e itsFilipe")]
[assembly: AssemblyCopyright("Copyright (c) 2026 Turman005 e Filipe Castro - MIT")]
[assembly: AssemblyVersion("2.0.0.0")]
[assembly: AssemblyFileVersion("2.0.2.0")]

namespace GxDarkTheme.Instalador
{
    // ---------------------------------------------------------------- rotulo de uma linha com reticencias no MEIO (caminhos)
    sealed class RotuloCaminho : Control
    {
        public RotuloCaminho() { SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true); TabStop = false; }
        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }
        protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); if (Parent != null) Parent.PerformLayout(); }
        public override Size GetPreferredSize(Size proposed) { return new Size(Math.Max(1, proposed.Width), Font.Height + 2); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, ForeColor, BackColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.PathEllipsis | TextFormatFlags.NoPrefix);
        }
    }

    // ---------------------------------------------------------------- assistente (layout responsivo: tudo em TableLayoutPanel/FlowLayoutPanel)
    public sealed class Assistente : Form
    {
        static readonly Color Escuro = Color.FromArgb(24, 31, 42), TextoClaro = Color.FromArgb(240, 244, 250), TextoSuave = Color.FromArgb(170, 180, 195);
        const string FonteUi = "Segoe UI";

        readonly bool desinstalar;
        readonly Panel corpo = new Panel { Dock = DockStyle.Fill };
        readonly Label titulo = new Label(), subtitulo = new Label();
        readonly Button voltar = new Button(), avancar = new Button(), cancelar = new Button();
        readonly Dictionary<Control, float> fontesBase = new Dictionary<Control, float>();
        float escala = 1f;              // escala extra (troca de monitor com outro DPI); o DPI inicial e tratado pelo AutoScale
        Size minimoBase;

        int pagina;
        List<GxInstalacao> instalacoes;
        ListView lista;
        RadioButton[] radios;
        CheckBox manter;
        string temaEscolhido = "next";
        bool temaLido;

        // progresso
        TextBox log;
        Label status, percentual;
        RotuloCaminho detalhe;
        ProgressBar barra, barraIndet;
        Button detalhes;
        System.Windows.Forms.Timer relogio;
        double faixaDe, faixaAte; bool indeterminado; DateTime inicioEtapa;
        bool trabalhando, terminou, resultado, critico = true;
        readonly System.Text.StringBuilder textoLog = new System.Text.StringBuilder();

        public Assistente(bool desinstalar)
        {
            this.desinstalar = desinstalar;
            SuspendLayout();
            Text = desinstalar ? "Desinstalar o GxDarkTheme" : "Instalação do GxDarkTheme " + Produto.Versao;
            Font = new Font(FonteUi, 9f);
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96f, 96f);
            ClientSize = new Size(700, 500);
            minimoBase = new Size(560, 430);
            MinimumSize = minimoBase;
            FormBorderStyle = FormBorderStyle.Sizable; MaximizeBox = true; MinimizeBox = true; ShowIcon = true;
            StartPosition = FormStartPosition.CenterScreen;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            // cabecalho escuro: titulo + subtitulo, ambos quebram linha
            TableLayoutPanel topo = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = Escuro, ColumnCount = 1, RowCount = 2, Padding = new Padding(20, 12, 20, 12) };
            topo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            Rot(titulo, TextoClaro, 13f, FontStyle.Regular); titulo.Margin = new Padding(0, 0, 0, 2);
            Rot(subtitulo, TextoSuave, 9f, FontStyle.Regular); subtitulo.Margin = new Padding(1, 0, 0, 0);
            topo.Controls.Add(titulo, 0, 0); topo.Controls.Add(subtitulo, 0, 1);

            // rodape: linha + botoes alinhados a direita (crescem com o texto/DPI)
            TableLayoutPanel base_ = new TableLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 2, RowCount = 2, Padding = new Padding(12, 0, 12, 10) };
            base_.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f)); base_.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            Label linha = new Label { Height = 1, Dock = DockStyle.Top, BackColor = Color.FromArgb(210, 214, 220), Margin = new Padding(-12, 0, -12, 10), AutoSize = false };
            base_.Controls.Add(linha, 0, 0); base_.SetColumnSpan(linha, 2);
            Label marca = new Label { Text = "GxDarkTheme " + Produto.Versao, AutoSize = true, ForeColor = SystemColors.GrayText, Anchor = AnchorStyles.Left, Margin = new Padding(0, 0, 8, 0) };
            base_.Controls.Add(marca, 0, 1);
            FlowLayoutPanel botoes = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Anchor = AnchorStyles.Right, Margin = Padding.Empty };
            foreach (Button b in new Button[] { voltar, avancar, cancelar })
            {
                b.AutoSize = true; b.AutoSizeMode = AutoSizeMode.GrowAndShrink; b.MinimumSize = new Size(92, 28); b.Padding = new Padding(6, 0, 6, 0); b.UseVisualStyleBackColor = true;
                botoes.Controls.Add(b);
            }
            voltar.Text = "< Voltar"; avancar.Text = "Avançar >"; cancelar.Text = "Cancelar";
            cancelar.Margin = new Padding(12, 3, 0, 3);
            base_.Controls.Add(botoes, 1, 1);

            Controls.Add(corpo); Controls.Add(base_); Controls.Add(topo);
            voltar.Click += delegate { Ir(pagina - 1); };
            avancar.Click += delegate { Avancar(); };
            cancelar.Click += delegate { Cancelar(); };
            FormClosing += AoFechar;
            AcceptButton = avancar;
            ResumeLayout(true);
            Ir(0);
        }

        // ---------- utilidades de layout
        void Rot(Label l, Color cor, float pt, FontStyle st)
        {
            l.AutoSize = true; l.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top; l.ForeColor = cor; l.BackColor = Color.Transparent;
            Fonte(l, pt, st);
        }
        void Fonte(Control c, float pt, FontStyle st) { Fonte(c, pt, st, FonteUi); }
        void Fonte(Control c, float pt, FontStyle st, string familia)
        {
            fontesBase[c] = pt;
            c.Font = new Font(familia, pt * escala, st);
        }
        int Px(int v) { return (int)Math.Round(v * escala * DpiAtual / 96f); }
        float dpiInicial;
        float DpiAtual { get { if (dpiInicial <= 0) { try { using (Graphics g = CreateGraphics()) dpiInicial = g.DpiX; } catch { dpiInicial = 96f; } } return dpiInicial; } }

        static TableLayoutPanel Grade() { return Grade(false); }
        static TableLayoutPanel Grade(bool rolar)
        {
            TableLayoutPanel t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(20, 14, 20, 8), AutoScroll = rolar };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            return t;
        }
        static void Linha(TableLayoutPanel t, Control c, bool expande)
        {
            t.RowCount++;
            t.RowStyles.Add(expande ? new RowStyle(SizeType.Percent, 100f) : new RowStyle(SizeType.AutoSize));
            t.Controls.Add(c, 0, t.RowCount - 1);
        }
        Label Paragrafo(string texto)
        {
            Label l = new Label { Text = texto, AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top, Margin = new Padding(0, 0, 0, 10), UseMnemonic = false };
            return l;
        }
        void Cabecalho(string t, string sub) { titulo.Text = t; subtitulo.Text = sub; subtitulo.Visible = !string.IsNullOrEmpty(sub); }
        void Mostrar(Control pag)
        {
            // foco no botao principal: evita que o foco caia num RadioButton (o WinForms marca o radio que recebe foco)
            if (avancar.Visible && avancar.Enabled) ActiveControl = avancar;
            corpo.SuspendLayout();
            List<Control> velhos = new List<Control>();
            foreach (Control c in corpo.Controls) velhos.Add(c);
            corpo.Controls.Clear();
            foreach (Control c in velhos) c.Dispose();
            List<Control> mortos = new List<Control>();
            foreach (Control c in fontesBase.Keys) if (c.IsDisposed) mortos.Add(c);
            foreach (Control c in mortos) fontesBase.Remove(c);
            corpo.Controls.Add(pag);
            corpo.ResumeLayout(true);
            if (avancar.Visible && avancar.Enabled) ActiveControl = avancar;
        }

        // ---------- escala por monitor (PerMonitorV2): WM_DPICHANGED
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint f);
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr h);
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }
        uint dpiJanela;

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try { dpiJanela = GetDpiForWindow(Handle); } catch { dpiJanela = 0; }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            // janela aberta num monitor com DPI diferente do DPI do sistema (o AutoScale so conhece o do sistema)
            if (dpiJanela > 0 && Math.Abs(dpiJanela - DpiAtual) > 1) AplicarEscala(dpiJanela / DpiAtual);
            CaberNaTela();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x02E0) // WM_DPICHANGED
            {
                int novo = (int)((long)m.WParam & 0xFFFF);
                float f = dpiJanela > 0 ? novo / (float)dpiJanela : 1f;
                dpiJanela = (uint)novo;
                if (Math.Abs(f - 1f) > 0.01f) AplicarEscala(f);
                RECT r = (RECT)System.Runtime.InteropServices.Marshal.PtrToStructure(m.LParam, typeof(RECT));
                SetWindowPos(Handle, IntPtr.Zero, r.L, r.T, Math.Max(r.R - r.L, MinimumSize.Width), Math.Max(r.B - r.T, MinimumSize.Height), 0x0014);
                m.Result = IntPtr.Zero;
                return;
            }
            base.WndProc(ref m);
        }

        /// <summary>Escala fontes, tamanhos minimos e a janela por um fator (troca de monitor; tambem usado pelo teste de renderizacao).</summary>
        public void AplicarEscala(float f)
        {
            // tamanhos calculados antes de trocar a fonte (algumas versoes do WinForms reescalam o form ao trocar a fonte)
            Size novo = new Size((int)(Width * f), (int)(Height * f)), novoMin = new Size((int)(MinimumSize.Width * f), (int)(MinimumSize.Height * f));
            SuspendLayout();
            escala *= f;
            Font = new Font(FonteUi, 9f * escala);
            foreach (KeyValuePair<Control, float> kv in new List<KeyValuePair<Control, float>>(fontesBase))
                if (!kv.Key.IsDisposed) kv.Key.Font = new Font(kv.Key.Font.FontFamily, kv.Value * escala, kv.Key.Font.Style);
            foreach (Button b in new Button[] { voltar, avancar, cancelar }) b.MinimumSize = new Size((int)(b.MinimumSize.Width * f), (int)(b.MinimumSize.Height * f));
            MinimumSize = novoMin;
            Size = novo;
            ResumeLayout(true);
            if (!trabalhando && !terminou) Ir(pagina);
            else
            {
                // paginas de progresso/conclusao nao sao reconstruidas: so reajusta o que tem altura fixa
                foreach (Control c in new Control[] { barra, linhaInfo, barraIndet })
                    if (c != null && !c.IsDisposed) c.Height = Math.Max(4, (int)Math.Round(c.Height * f));
                if (percentual != null && !percentual.IsDisposed) LarguraPercentual();
                if (detalhes != null && !detalhes.IsDisposed) detalhes.MinimumSize = new Size((int)(detalhes.MinimumSize.Width * f), (int)(detalhes.MinimumSize.Height * f));
            }
        }

        void CaberNaTela()
        {
            Rectangle area = Screen.FromControl(this).WorkingArea;
            if (Width > area.Width || Height > area.Height)
            {
                Size = new Size(Math.Min(Width, area.Width), Math.Min(Height, area.Height));
                Location = new Point(Math.Max(area.Left, Math.Min(Left, area.Right - Width)), Math.Max(area.Top, Math.Min(Top, area.Bottom - Height)));
            }
        }

        // ---------- navegacao
        int PaginaProgresso { get { return desinstalar ? 1 : 4; } }

        void Ir(int p)
        {
            if (p < 0) return;
            pagina = p;
            voltar.Visible = true; cancelar.Visible = true;
            voltar.Enabled = p > 0 && p != PaginaProgresso; avancar.Enabled = true; cancelar.Enabled = true; avancar.Text = "Avançar >";
            if (desinstalar) { if (p == 0) PagDesinstalar(); else PagProgresso(); return; }
            switch (p)
            {
                case 0: PagBoasVindas(); break;
                case 1: PagInstalacoes(); break;
                case 2: PagTema(); break;
                case 3: PagResumo(); break;
                default: PagProgresso(); break;
            }
        }

        void PagBoasVindas()
        {
            Cabecalho("Bem-vindo ao GxDarkTheme " + Produto.Versao, "Tema escuro para a IDE do GeneXus 18");
            TableLayoutPanel t = Grade();
            Linha(t, Paragrafo("Tema escuro para a IDE do GeneXus 18: GeneXus Next (padrão) e mais 7 temas, ícones do GeneXus Next em todos os temas, " +
                  "botão sol/lua (Ctrl+Shift+F12) e o menu Ferramentas > Tema escuro para trocar de tema com o GeneXus aberto."), false);
            Linha(t, Paragrafo("Tudo é aplicado em memória enquanto o GeneXus roda: nenhum arquivo do GeneXus, nenhuma KB e nenhuma cor do Windows é alterada. " +
                  "Para desinstalar: Configurações do Windows > Aplicativos instalados > GxDarkTheme."), false);
            Linha(t, Paragrafo("Autores: " + Produto.Autores + ". Licença MIT. Ícones: @genexus/mercury 1.5.0 (Apache-2.0), modificados. " +
                  "Projeto independente, sem vínculo com a GeneXus S.A."), false);
            TextBox lic = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, WordWrap = true, Dock = DockStyle.Fill, MinimumSize = new Size(0, Px(90)), BackColor = SystemColors.Window };
            Fonte(lic, 8.5f, FontStyle.Regular, "Consolas");
            try { lic.Text = Ops.Texto("LICENSE.txt").Replace("\r\n", "\n").Replace("\n", "\r\n") + "\r\n\r\n" + Ops.Texto("NOTICE-Mercury.txt").Replace("\r\n", "\n").Replace("\n", "\r\n"); } catch { }
            Linha(t, lic, true);
            Mostrar(t);
            lic.Select(0, 0);
        }

        void PagInstalacoes()
        {
            Cabecalho("Onde instalar", "Escolha as instalações do GeneXus 18 que vão receber o tema escuro");
            TableLayoutPanel t = Grade();
            instalacoes = Ops.Detectar();
            if (instalacoes.Count == 0)
            {
                Linha(t, Paragrafo("Nenhuma instalação do GeneXus 18 foi encontrada (pastas GeneXus18* com GeneXus.exe em Arquivos de Programas\\GeneXus)."), false);
                Mostrar(t); avancar.Enabled = false; return;
            }
            Linha(t, Paragrafo("Instalações do GeneXus 18 encontradas. A extensão é copiada para a pasta Packages de cada uma marcada."), false);
            lista = new ListView { View = View.Details, CheckBoxes = true, FullRowSelect = true, HeaderStyle = ColumnHeaderStyle.Nonclickable, ShowItemToolTips = true, Dock = DockStyle.Fill, MinimumSize = new Size(0, Px(110)), HideSelection = true, Margin = new Padding(0, 0, 0, 10) };
            lista.Columns.Add("Instalação"); lista.Columns.Add("Versão"); lista.Columns.Add("Pasta"); lista.Columns.Add("Situação");
            bool xpz = false;
            foreach (GxInstalacao g in instalacoes)
            {
                string sit = g.TemNossa ? "já instalado" : g.TemXpz ? "tem Xpz.Dark" : "";
                ListViewItem it = new ListViewItem(new string[] { g.Nome, g.Versao, g.Pasta, sit }) { Checked = Escolhida(g), Tag = g, ToolTipText = g.Pasta };
                lista.Items.Add(it);
                xpz |= g.TemXpz;
            }
            lista.Resize += delegate { AjustarColunas(); };
            Linha(t, lista, true);
            Linha(t, Paragrafo((xpz ? "A extensão antiga Xpz.Dark será removida (o GxDarkTheme já inclui os temas dela; o tema escolhido nela é mantido). " : "") +
                  "Arquivos antigos (GxDarkTheme.isl, Xpz.Dark.dll) e a entrada do Xpz.Dark na lista de pacotes do GeneXus também são removidos."), false);
            Mostrar(t);
            AjustarColunas();
        }

        bool Escolhida(GxInstalacao g)
        {
            if (instalacoesMarcadas.Count == 0) return true;
            foreach (GxInstalacao x in instalacoesMarcadas) if (string.Equals(x.Pasta, g.Pasta, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        void AjustarColunas()
        {
            if (lista == null || lista.IsDisposed || lista.Columns.Count < 4) return;
            int w = lista.ClientSize.Width;
            if (w <= 0) return;
            lista.BeginUpdate();
            int c0 = Math.Max(Px(90), TextRenderer.MeasureText("GeneXus18U99 ", lista.Font).Width);
            foreach (ListViewItem it in lista.Items) c0 = Math.Max(c0, TextRenderer.MeasureText(it.Text + "    ", lista.Font).Width + Px(20));
            int c1 = TextRenderer.MeasureText("18.0.999999.99 ", lista.Font).Width;
            int c3 = TextRenderer.MeasureText("tem Xpz.Dark   ", lista.Font).Width;
            int c2 = Math.Max(Px(80), w - c0 - c1 - c3 - 2);
            lista.Columns[0].Width = c0; lista.Columns[1].Width = c1; lista.Columns[2].Width = c2; lista.Columns[3].Width = c3;
            lista.EndUpdate();
        }

        void PagTema()
        {
            Cabecalho("Tema padrão", "O tema que o GeneXus usa ao abrir");
            if (!temaLido) { temaLido = true; string cfg = Ops.TemaAtualConfigurado(); Tema achado = string.IsNullOrEmpty(cfg) ? null : Temas.Achar(cfg); if (achado != null) temaEscolhido = achado.Id; }
            string atual = temaEscolhido;
            TableLayoutPanel t = Grade(true);
            Linha(t, Paragrafo("Dá para trocar a qualquer momento, com o GeneXus aberto, em Ferramentas > Tema escuro ou com o botão direito no sol/lua."), false);
            TableLayoutPanel g = new TableLayoutPanel { ColumnCount = 3, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top, Margin = Padding.Empty };
            g.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); g.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); g.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            radios = new RadioButton[Temas.Todos.Count];
            int lado = Math.Max(12, Font.Height);
            for (int i = 0; i < Temas.Todos.Count; i++)
            {
                Tema tm = Temas.Todos[i];
                g.RowCount++; g.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                Panel amostra = new Panel { Size = new Size(lado, lado), BackColor = tm.M(Color.FromArgb(36, 45, 60)), Margin = new Padding(0, 5, 8, 5), Anchor = AnchorStyles.Top };
                Panel acento = new Panel { Size = new Size(lado / 2, lado / 2), Location = new Point(lado / 4, lado / 4), BackColor = tm.M(Color.FromArgb(44, 141, 206)) };
                amostra.Controls.Add(acento);
                RadioButton r = new RadioButton { Text = tm.Nome, Tag = tm.Id, AutoSize = true, Margin = new Padding(0, 3, 12, 3), Anchor = AnchorStyles.Left | AnchorStyles.Top };
                Fonte(r, 9f, FontStyle.Bold);
                Label d = new Label { Text = tm.Descricao, AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top, Margin = new Padding(0, 5, 0, 5), ForeColor = SystemColors.ControlDarkDark, UseMnemonic = false };
                RadioButton alvo = r;
                r.CheckedChanged += delegate { if (alvo.Checked) temaEscolhido = (string)alvo.Tag; };
                d.Click += delegate { alvo.Checked = true; }; amostra.Click += delegate { alvo.Checked = true; };
                g.Controls.Add(amostra, 0, i); g.Controls.Add(r, 1, i); g.Controls.Add(d, 2, i);
                radios[i] = r;
            }
            Linha(t, g, false);
            Linha(t, new Panel { Height = 1, Margin = Padding.Empty }, true);
            Mostrar(t);
            // marca depois de tudo montado (marcar antes de o radio estar no container nao e confiavel)
            foreach (RadioButton r in radios) if ((string)r.Tag == atual) r.Checked = true;
        }

        void PagResumo()
        {
            Cabecalho("Pronto para instalar", "Confira e clique em Instalar");
            TableLayoutPanel t = Grade();
            Linha(t, Paragrafo("O instalador vai copiar GxDarkTheme.dll para a pasta Packages, remover o Xpz.Dark e arquivos antigos, rodar \"GeneXus.exe /install\" " +
                          "para atualizar a lista de pacotes (1-2 minutos por instalação) e registrar o desinstalador em Aplicativos instalados."), false);
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("Tema padrão: " + Temas.Achar(temaEscolhido).Nome);
            sb.AppendLine();
            sb.AppendLine("Instalar em:");
            foreach (GxInstalacao g in Selecao()) sb.AppendLine("   " + g.Nome + (string.IsNullOrEmpty(g.Versao) ? "" : "  (" + g.Versao + ")") + "\r\n      " + g.Pasta);
            sb.AppendLine();
            sb.AppendLine("Pasta do programa: " + Ops.PastaApp);
            TextBox resumo = new TextBox { Multiline = true, ReadOnly = true, WordWrap = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, Text = sb.ToString(), BackColor = SystemColors.Window, MinimumSize = new Size(0, Px(80)) };
            Linha(t, resumo, true);
            Mostrar(t);
            avancar.Text = "Instalar";
        }

        void PagDesinstalar()
        {
            Cabecalho("Desinstalar o GxDarkTheme", "Remove o tema escuro de todas as instalações do GeneXus 18");
            TableLayoutPanel t = Grade();
            Linha(t, Paragrafo("Remove a extensão de todas as instalações do GeneXus 18, tira a entrada da lista de pacotes do GeneXus e devolve ao original " +
                  "o que versões antigas do \"Ativar Dark Mode\" tinham gravado no Windows/editor. O GeneXus volta ao visual original."), false);
            manter = new CheckBox { Text = "Manter minhas configurações", AutoSize = true, Checked = true, Margin = new Padding(0, 4, 0, 2) };
            Linha(t, manter, false);
            Label obs = Paragrafo("Guarda o tema escolhido para uma futura reinstalação (%APPDATA%\\GeneXus\\GeneXus\\18\\" + Produto.ArquivoConfig + ").");
            obs.ForeColor = SystemColors.GrayText; obs.Margin = new Padding(18, 0, 0, 10);
            Linha(t, obs, false);
            Linha(t, new Panel { Height = 1, Margin = Padding.Empty }, true);
            Mostrar(t);
            avancar.Text = "Desinstalar";
        }

        List<GxInstalacao> Marcadas()
        {
            List<GxInstalacao> l = new List<GxInstalacao>();
            if (lista != null && !lista.IsDisposed) foreach (ListViewItem it in lista.CheckedItems) l.Add((GxInstalacao)it.Tag);
            return l;
        }

        void Avancar()
        {
            if (terminou) { Close(); return; }
            if (!desinstalar && pagina == 1 && Marcadas().Count == 0) { MessageBox.Show(this, "Marque pelo menos uma instalação.", Text); return; }
            if (!desinstalar && pagina == 1) instalacoesMarcadas = Marcadas();
            if ((desinstalar && pagina == 0) || (!desinstalar && pagina == 3))
            {
                while (true)
                {
                    List<Process> abertos = Ops.GeneXusAbertos();
                    if (abertos.Count == 0) break;
                    DialogResult r = MessageBox.Show(this, "O GeneXus está aberto (" + abertos.Count + " janela(s)).\r\n\r\nSalve o seu trabalho e feche o GeneXus; depois clique em Repetir.\r\n" +
                        "O instalador nunca fecha o GeneXus sozinho.", Text, MessageBoxButtons.RetryCancel, MessageBoxIcon.Warning);
                    if (r != DialogResult.Retry) return;
                }
            }
            Ir(pagina + 1);
        }
        List<GxInstalacao> instalacoesMarcadas = new List<GxInstalacao>();

        // a lista da pagina 1 e descartada ao trocar de pagina; o resumo e a instalacao usam a selecao guardada
        List<GxInstalacao> Selecao() { return instalacoesMarcadas; }

        // ---------- pagina de progresso (igual a um instalador padrao)
        void PagProgresso()
        {
            Cabecalho(desinstalar ? "Desinstalando o GxDarkTheme" : "Instalando o GxDarkTheme", "Aguarde enquanto o " + (desinstalar ? "desinstalador" : "instalador") + " trabalha");
            MontarProgresso();
            IniciarTrabalho();
        }

        TableLayoutPanel linhaInfo;

        // largura fixa para "100%" (o texto nao pula conforme o numero muda)
        void LarguraPercentual()
        {
            percentual.MinimumSize = new Size(TextRenderer.MeasureText("100%", Font).Width + Px(4), 0);
        }

        void MontarProgresso()
        {
            voltar.Enabled = false; avancar.Enabled = false; cancelar.Enabled = false; avancar.Text = "Avançar >";
            TableLayoutPanel t = Grade();
            status = Paragrafo("Preparando...");
            status.Margin = new Padding(0, 4, 0, 8);
            Linha(t, status, false);
            barra = new ProgressBar { Minimum = 0, Maximum = 1000, Style = ProgressBarStyle.Continuous, Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top, Height = Math.Max(Px(20), Font.Height + 6), Margin = new Padding(0, 0, 0, 4) };
            Linha(t, barra, false);
            TableLayoutPanel info = linhaInfo = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, AutoSize = false, Height = Font.Height + 6, Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top, Margin = new Padding(0, 0, 0, 4) };
            info.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            info.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f)); info.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            detalhe = new RotuloCaminho { Dock = DockStyle.Fill, ForeColor = SystemColors.GrayText, Margin = new Padding(0, 0, 8, 0) };
            percentual = new Label { Text = "0%", AutoSize = true, Anchor = AnchorStyles.Right, Margin = Padding.Empty, TextAlign = ContentAlignment.MiddleRight };
            LarguraPercentual();
            info.Controls.Add(detalhe, 0, 0); info.Controls.Add(percentual, 1, 0);
            Linha(t, info, false);
            barraIndet = new ProgressBar { Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 30, Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top, Height = Math.Max(Px(6), 4), Margin = new Padding(0, 0, 0, 8), Visible = false };
            Linha(t, barraIndet, false);
            detalhes = new Button { Text = "Mostrar detalhes", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, MinimumSize = new Size(Px(120), Px(26)), Anchor = AnchorStyles.Left, Margin = new Padding(0, 4, 0, 6), UseVisualStyleBackColor = true };
            detalhes.Click += delegate { AlternarDetalhes(); };
            Linha(t, detalhes, false);
            log = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, WordWrap = true, Dock = DockStyle.Fill, Visible = false, BackColor = SystemColors.Window, Text = textoLog.ToString() };
            Fonte(log, 8.5f, FontStyle.Regular, "Consolas");
            Linha(t, log, true);
            Mostrar(t);
            relogio = new System.Windows.Forms.Timer { Interval = 200 };
            relogio.Tick += delegate { Avancar_Relogio(); };
            relogio.Start();
        }

        void AlternarDetalhes()
        {
            log.Visible = !log.Visible;
            detalhes.Text = log.Visible ? "Ocultar detalhes" : "Mostrar detalhes";
            if (log.Visible) { log.SelectionStart = log.TextLength; log.ScrollToCaret(); }
        }

        void IniciarTrabalho()
        {
            trabalhando = true;
            List<GxInstalacao> alvos = Selecao();
            bool manterCfg = manter == null || manter.Checked;
            string tema = temaEscolhido;
            Ops.PedidoParar = false;
            Ops.Log = delegate(string m) { Ui(delegate { NovaLinha(m); }); };
            Ops.Etapa = delegate(string s, double de, double ate, bool indet) { Ui(delegate { NovaEtapa(s, de, ate, indet); }); };
            Ops.Critico = delegate(bool c) { Ui(delegate { critico = c; cancelar.Enabled = !c && !desinstalar && !Ops.PedidoParar; }); };
            Thread th = new Thread(delegate()
            {
                bool r = false;
                try { r = desinstalar ? Ops.Desinstalar(manterCfg) : Ops.Instalar(alvos, tema); }
                catch (Exception ex) { Ops.Log("ERRO: " + ex.Message); }
                Ui(delegate { Fim(r); });
            });
            th.IsBackground = true;
            th.SetApartmentState(ApartmentState.STA);
            th.Start();
        }

        void Ui(MethodInvoker a) { try { if (!IsDisposed) BeginInvoke(a); } catch { } }

        void NovaLinha(string m)
        {
            textoLog.Append(m).Append("\r\n");
            if (log != null && !log.IsDisposed) log.AppendText(m + "\r\n");
            if (detalhe != null && !detalhe.IsDisposed && m.Trim().Length > 0) detalhe.Text = m.Trim();
        }

        void NovaEtapa(string s, double de, double ate, bool indet)
        {
            if (status == null || status.IsDisposed) return;
            status.Text = s;
            faixaDe = de; faixaAte = ate; indeterminado = indet; inicioEtapa = DateTime.Now;
            barraIndet.Visible = indet;
            Pos(de);
        }

        void Pos(double v)
        {
            int n = (int)Math.Round(Math.Max(0, Math.Min(1, v)) * 1000);
            if (n < barra.Value) return;           // a barra geral nunca anda para tras
            barra.Value = n;
            percentual.Text = (n / 10) + "%";
        }

        // durante o GeneXus.exe /install (duracao desconhecida) a barra geral continua andando devagar dentro da faixa da etapa
        void Avancar_Relogio()
        {
            if (!indeterminado || barra == null || barra.IsDisposed) return;
            double t = (DateTime.Now - inicioEtapa).TotalSeconds;
            Pos(faixaDe + (faixaAte - faixaDe) * 0.97 * (1 - Math.Exp(-t / 45.0)));
        }

        void Fim(bool r)
        {
            trabalhando = false; terminou = true; resultado = r;
            if (relogio != null) relogio.Stop();
            string arq = Path.Combine(Path.GetTempPath(), desinstalar ? "GxDarkTheme-desinstalacao.log" : "GxDarkTheme-instalacao.log");
            try { File.WriteAllText(arq, textoLog.ToString(), System.Text.Encoding.UTF8); } catch { }
            MontarConcluido(r, arq);
        }

        void MontarConcluido(bool r, string arq)
        {
            bool parou = Ops.PedidoParar;
            if (r) Cabecalho(desinstalar ? "Desinstalação concluída" : "Instalação concluída", desinstalar ? "O GxDarkTheme foi removido" : "O GxDarkTheme está pronto para usar");
            else Cabecalho(parou ? "Instalação interrompida" : "Concluído com avisos", "Veja os detalhes abaixo");
            TableLayoutPanel t = Grade();
            string msg;
            if (desinstalar) msg = r ? "O GxDarkTheme foi removido de todas as instalações do GeneXus 18. Ao abrir o GeneXus, o visual original volta." :
                                       "A desinstalação terminou, mas alguns passos tiveram avisos ou erros. Veja os detalhes.";
            else if (r) msg = "Abra o GeneXus normalmente. O tema escolhido (" + Temas.Achar(temaEscolhido).Nome + ") já vem ligado.\r\n\r\n" +
                              "Para trocar de tema: Ferramentas > Tema escuro, ou botão direito no sol/lua à direita da barra de menus. Ctrl+Shift+F12 alterna claro/escuro.";
            else msg = parou ? "A instalação foi interrompida a pedido depois da etapa que estava em andamento. As instalações já concluídas funcionam; rode o instalador de novo para as demais."
                             : "A instalação terminou, mas alguns passos tiveram avisos ou erros. Veja os detalhes.";
            Linha(t, Paragrafo(msg), false);
            Label reg = Paragrafo("Registro salvo em " + arq);
            reg.ForeColor = SystemColors.GrayText;
            Linha(t, reg, false);
            barra = new ProgressBar { Minimum = 0, Maximum = 1000, Value = r ? 1000 : Math.Max(0, Math.Min(1000, barra != null ? barra.Value : 0)), Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top, Height = Math.Max(Px(20), Font.Height + 6), Margin = new Padding(0, 0, 0, 8) };
            Linha(t, barra, false);
            detalhes = new Button { Text = "Mostrar detalhes", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, MinimumSize = new Size(Px(120), Px(26)), Anchor = AnchorStyles.Left, Margin = new Padding(0, 4, 0, 6), UseVisualStyleBackColor = true };
            detalhes.Click += delegate { AlternarDetalhes(); };
            Linha(t, detalhes, false);
            log = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, WordWrap = true, Dock = DockStyle.Fill, Visible = !r, BackColor = SystemColors.Window, Text = textoLog.ToString() };
            Fonte(log, 8.5f, FontStyle.Regular, "Consolas");
            if (!r) detalhes.Text = "Ocultar detalhes";
            Linha(t, log, true);
            Mostrar(t);
            status = null; detalhe = null; percentual = null; barraIndet = null;
            voltar.Visible = false; cancelar.Visible = false;
            avancar.Text = "Concluir"; avancar.Enabled = true; AcceptButton = avancar; avancar.Focus();
        }

        // ---------- cancelar/fechar
        void Cancelar()
        {
            if (terminou) { Close(); return; }
            if (!trabalhando) { Close(); return; }
            PedirParada();
        }

        void PedirParada()
        {
            if (critico || desinstalar || Ops.PedidoParar) return;
            if (MessageBox.Show(this, "Parar a instalação depois da etapa atual?\r\n\r\nO GeneXus.exe /install em andamento termina normalmente; as instalações seguintes não são alteradas.",
                Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            Ops.PedidoParar = true;
            cancelar.Enabled = false;
            if (status != null && !status.IsDisposed) status.Text = status.Text + "  (parando depois desta etapa...)";
        }

        void AoFechar(object s, FormClosingEventArgs e)
        {
            if (!trabalhando) return;
            e.Cancel = true;      // nunca fecha no meio do trabalho
            if (!critico) PedirParada();
        }
    }

    static class Programa
    {
        [STAThread]
        static int Main(string[] args)
        {
            bool un = false, quiet = false; string tema = null;
            foreach (string a in args)
            {
                string l = a.ToLowerInvariant();
                if (l == "/uninstall" || l == "/desinstalar") un = true;
                else if (l == "/quiet" || l == "/silencioso" || l == "/s") quiet = true;
                else if (l.StartsWith("/tema=")) tema = l.Substring(6);
            }
            if (Path.GetFileName(Application.ExecutablePath).Equals(Ops.Desinstalador, StringComparison.OrdinalIgnoreCase)) un = true;
            if (quiet)
            {
                // modo silencioso (para administradores/testes): nunca fecha o GeneXus
                Ops.Log = delegate(string m) { Console.WriteLine(m); };
                if (Ops.GeneXusAbertos().Count > 0) { Console.WriteLine("GeneXus aberto: feche-o e rode de novo."); return 2; }
                if (un) return Ops.Desinstalar(true) ? 0 : 1;
                return Ops.Instalar(Ops.Detectar(), Temas.Achar(tema) != null ? tema : (Ops.TemaAtualConfigurado() ?? "next")) ? 0 : 1;
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Assistente(un));
            return 0;
        }
    }
}
