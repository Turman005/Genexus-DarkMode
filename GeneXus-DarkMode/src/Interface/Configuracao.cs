// GxDarkTheme - tela de configuracoes (Ferramentas > Tema escuro > Configuracoes..., ou botao direito no sol/lua).
// Grava so o arquivo %APPDATA%\GeneXus\GeneXus\18\GxDarkTheme.settings.
// Layout responsivo: TableLayoutPanel, textos que quebram linha e botoes do tamanho do texto. Todas as medidas saem da
// altura da fonte (que ja acompanha o DPI), entao nada e cortado em 100/125/150/200%. As cores vem do motor (tema ativo).
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace GxDarkTheme
{
    internal sealed class Configuracao : Form
    {
        static Configuracao aberta;
        readonly CheckBox chkAtivo, chkIcones, chkEscuro;
        readonly ComboBox cbTema, cbBotao;
        readonly TableLayoutPanel grade, botoes;
        bool carregando;

        // multiplicador das fontes; so o teste de renderizacao muda (simula 125/150/200% num ambiente a 96 dpi)
        internal static float EscalaFonte = 1f;

        public static void Exibir(IWin32Window dono)
        {
            if (aberta != null && !aberta.IsDisposed) { aberta.Activate(); return; }
            aberta = new Configuracao();
            aberta.Show(dono);
        }

        Configuracao()
        {
            Text = Produto.NomeExibicao + " - Configurações";
            FormBorderStyle = FormBorderStyle.Sizable; MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            // medidas calculadas pela fonte (em pontos, ja escalada pelo DPI): sem AutoScale para nao escalar duas vezes
            AutoScaleMode = AutoScaleMode.None;
            Font = Fonte(9f, FontStyle.Regular);
            BackColor = DarkEngine.Panel; ForeColor = DarkEngine.Fg;
            int u = Font.Height;   // unidade de espaco (~15 px a 100%)

            SuspendLayout();
            grade = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoScroll = true, BackColor = Color.Transparent,
                                           Padding = new Padding(u, (u * 3) / 4, u, (u * 3) / 4) };
            grade.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            grade.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            Titulo("Tema", false);
            cbTema = Combo();
            foreach (Tema t in Temas.Todos) cbTema.Items.Add(t);
            Label desc = Texto("", true);
            desc.Margin = new Padding(0, u / 5, 0, u / 2);
            cbTema.SelectedIndexChanged += delegate
            {
                Tema t = cbTema.SelectedItem as Tema;
                if (t == null) return;
                desc.Text = t.Descricao;
                if (!carregando) DarkEngine.SetTheme(t.Id, true);
            };

            chkEscuro = Caixa("Modo escuro ligado agora (Ctrl+Shift+F12 ou clique no sol/lua)");
            chkEscuro.CheckedChanged += delegate { if (!carregando && chkEscuro.Checked != DarkEngine.On) DarkEngine.SetMode(chkEscuro.Checked, true); };

            Titulo("Ícone do botão sol/lua", true);
            cbBotao = Combo();
            cbBotao.Items.AddRange(new object[] { "Sol âmbar / lua suave (padrão)", "Contorno", "Lua azul" });
            cbBotao.SelectedIndexChanged += delegate
            {
                if (carregando) return;
                DarkEngine.SettingsWrite("icone", cbBotao.SelectedIndex == 1 ? "contorno" : cbBotao.SelectedIndex == 2 ? "azul" : "ambar");
                try { ToggleButton.RefreshAll(); } catch { }
            };

            Titulo("Ao abrir o GeneXus (vale na próxima abertura)", true);
            chkAtivo = Caixa("Extensão ativa (desmarcado: o GeneXus abre exatamente como veio de fábrica)");
            chkIcones = Caixa("Ícones do GeneXus Next (Mercury) em todos os temas");
            chkAtivo.CheckedChanged += delegate { if (!carregando) DarkEngine.SettingsWrite("enabled", chkAtivo.Checked ? "true" : "false"); };
            chkIcones.CheckedChanged += delegate { if (!carregando) DarkEngine.SettingsWrite("icons", chkIcones.Checked ? "true" : "false"); };

            Titulo("Autores", true);
            Texto("Gabriel Turman", false).Margin = new Padding(0, 0, 0, 0);
            Texto("Filipe Castro", false).Margin = new Padding(0, 0, 0, u / 2);

            Texto(Produto.NomeExibicao + ", versão " + Produto.Versao, true).Margin = new Padding(0, u / 4, 0, 0);
            Texto("Shift segurado ao abrir o GeneXus = abre sem o tema (modo seguro).", true);

            // linha que absorve a sobra de altura (os botoes ficam sempre embaixo)
            Linha(new Panel { Height = 0, Margin = Padding.Empty, BackColor = Color.Transparent }, 2);
            grade.RowStyles[grade.RowStyles.Count - 1] = new RowStyle(SizeType.Percent, 100f);

            // botoes: Abrir o log e Pasta das configuracoes a esquerda, Fechar a direita; cada um do tamanho do seu texto
            botoes = new TableLayoutPanel { Dock = DockStyle.Bottom, ColumnCount = 4, RowCount = 1, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                                            BackColor = Color.Transparent, Padding = new Padding(u - 3, u / 3, u - 3, (u * 2) / 3) };
            botoes.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); botoes.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            botoes.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f)); botoes.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            botoes.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Button log = Botao("Abrir o log", 0);
            log.Click += delegate { try { string p = Path.Combine(Path.GetTempPath(), "GxDarkTheme.log"); if (File.Exists(p)) Process.Start("notepad.exe", "\"" + p + "\""); } catch { } };
            Button pasta = Botao("Pasta das configurações", 1);
            pasta.Click += delegate { try { string d = Path.GetDirectoryName(DarkEngine.SettingsPath()); if (Directory.Exists(d)) Process.Start("explorer.exe", "\"" + d + "\""); } catch { } };
            Button fechar = Botao("Fechar", 3);
            fechar.Click += delegate { Close(); };
            CancelButton = fechar;

            Controls.Add(grade);
            Controls.Add(botoes);
            ResumeLayout(false);

            Carregar();
            Ajustar();
            DarkEngine.TemaMudou += AoMudar;
            FormClosed += delegate { DarkEngine.TemaMudou -= AoMudar; aberta = null; };
        }

        // tamanho inicial e minimo calculados com a fonte real (a fonte em pontos ja vem escalada pelo DPI)
        void Ajustar()
        {
            int u = Font.Height;
            Size borda = Size - ClientSize;
            int largBotoes = botoes.GetPreferredSize(Size.Empty).Width;
            int largMin = Math.Max(largBotoes, u * 24);
            int larg = Math.Max(largMin, u * 33);
            int alt = grade.GetPreferredSize(new Size(larg, 0)).Height + botoes.GetPreferredSize(new Size(larg, 0)).Height;
            // altura minima = tudo visivel na largura inicial; mais estreita, os textos quebram e a grade ganha rolagem
            int altMin = alt;
            Rectangle area = Screen.FromControl(this).WorkingArea;
            MinimumSize = new Size(Math.Min(largMin + borda.Width, area.Width), Math.Min(altMin + borda.Height, area.Height));
            ClientSize = new Size(Math.Min(larg, area.Width - borda.Width), Math.Min(alt, area.Height - borda.Height));
        }

        void AoMudar(object s, EventArgs e) { try { if (!IsDisposed) Carregar(); } catch { } }

        void Carregar()
        {
            carregando = true;
            try
            {
                cbTema.SelectedItem = DarkEngine.TemaAtual;
                chkEscuro.Checked = DarkEngine.On;
                string ic = DarkEngine.IconOption();
                cbBotao.SelectedIndex = ic == "contorno" ? 1 : ic == "azul" ? 2 : 0;
                chkAtivo.Checked = DarkEngine.SettingsValue("enabled") != "false";
                chkIcones.Checked = DarkEngine.SettingsValue("icons") != "false";
                BackColor = DarkEngine.On ? DarkEngine.Panel : SystemColors.Control;
                ForeColor = DarkEngine.On ? DarkEngine.Fg : SystemColors.ControlText;
                foreach (Control c in grade.Controls) if (c.Tag as string == "suave") c.ForeColor = DarkEngine.On ? DarkEngine.FgDim : SystemColors.GrayText;
            }
            finally { carregando = false; }
        }

        // ---------- montagem
        static Font Fonte(float pt, FontStyle st) { return new Font("Segoe UI", pt * EscalaFonte, st); }

        void Linha(Control c, int colunas)
        {
            int r = grade.RowCount++;
            grade.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            grade.Controls.Add(c, colunas == 2 ? 0 : 1, r);
            if (colunas == 2) grade.SetColumnSpan(c, 2);
        }

        void Titulo(string t, bool espacoAntes)
        {
            Label l = new Label { Text = t, AutoSize = true, BackColor = Color.Transparent, Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top, UseMnemonic = false,
                                  Font = Fonte(9f, FontStyle.Bold), Margin = new Padding(0, espacoAntes ? Font.Height * 2 / 3 : 0, 0, Font.Height / 4) };
            Linha(l, 2);
        }

        // texto que quebra linha na largura disponivel
        Label Texto(string t, bool suave)
        {
            Label l = new Label { Text = t, AutoSize = true, BackColor = Color.Transparent, Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top, UseMnemonic = false,
                                  Margin = new Padding(0, 0, 0, Font.Height / 5) };
            if (suave) { l.ForeColor = DarkEngine.FgDim; l.Tag = "suave"; }
            Linha(l, 2);
            return l;
        }

        ComboBox Combo()
        {
            // fonte explicita: a altura do combo sai da fonte que ele tem ao ser criado
            ComboBox c = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Font = Font, Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top, Margin = new Padding(0, 0, 0, 2) };
            c.ItemHeight = Font.Height + 2;
            c.Height = Font.Height + 8;   // o Windows calcula sozinho pela fonte; outros WinForms (Mono) usam este valor
            Linha(c, 2);
            return c;
        }

        // caixa de marcar + rotulo que quebra linha (o CheckBox do WinForms nao quebra texto); clicar no rotulo marca/desmarca
        CheckBox Caixa(string t)
        {
            int u = Font.Height;
            CheckBox c = new CheckBox { Text = "", AutoSize = false, Size = new Size(u + 2, u + 2), BackColor = Color.Transparent, AccessibleName = t,
                                        Anchor = AnchorStyles.Left | AnchorStyles.Top, Margin = new Padding(0, u / 5, u / 3, 0) };
            Label l = new Label { Text = t, AutoSize = true, BackColor = Color.Transparent, Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top, UseMnemonic = false,
                                  Margin = new Padding(0, u / 5 + 2, 0, u / 4), Cursor = Cursors.Hand };
            l.Click += delegate { if (c.Enabled) { c.Checked = !c.Checked; c.Focus(); } };
            int r = grade.RowCount++;
            grade.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            grade.Controls.Add(c, 0, r);
            grade.Controls.Add(l, 1, r);
            return c;
        }

        Button Botao(string t, int coluna)
        {
            int u = Font.Height;
            Button b = new Button { Text = t, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlatStyle = FlatStyle.Flat, UseVisualStyleBackColor = false,
                                    MinimumSize = new Size(u * 6, u + u / 2 + 4), Padding = new Padding(u / 2, 0, u / 2, 0),
                                    Anchor = coluna == 3 ? AnchorStyles.Right : AnchorStyles.Left, Margin = new Padding(3, 3, coluna == 0 ? u / 2 : 3, 3) };
            botoes.Controls.Add(b, coluna, 0);
            return b;
        }
    }
}
