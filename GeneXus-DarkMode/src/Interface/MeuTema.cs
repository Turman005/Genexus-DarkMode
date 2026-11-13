// GxDarkTheme - janela "Meu tema" (ideia da tela Personalizado do Xpz.Dark, de Filipe Castro).
// O usuario escolhe o tom do fundo, a cor de destaque, as 9 cores do codigo, cores exatas opcionais por area e as cores
// das linhas do Output; a previa muda na hora e "Aplicar" grava as chaves meu_* no GxDarkTheme.settings e liga o tema
// "Meu tema" com a IDE aberta. Nada vai para as configuracoes do GeneXus. Exportar/Importar troca o tema por um arquivo.
// O tom do fundo define matiz e saturacao; o brilho de cada area continua o do GeneXus Next (mesma conta dos outros temas).
// Uma cor exata substitui a conta so naquela area (fundo precisa ser escuro e texto claro: ver DarkEngine.CorFixaValida).
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace GxDarkTheme
{
    internal sealed class MeuTema : Form
    {
        static MeuTema aberta;

        // cores Next de referencia (as mesmas de DarkEngine.paletaNext)
        static readonly Color NEditor = Color.FromArgb(17, 21, 28), NPanel = Color.FromArgb(24, 31, 42), NChrome = Color.FromArgb(36, 45, 60),
            NInput = Color.FromArgb(31, 39, 52), NAccent = Color.FromArgb(44, 141, 206), NSel = Color.FromArgb(44, 60, 80), NFg = Color.FromArgb(220, 226, 235),
            NFgDim = Color.FromArgb(138, 144, 155), NBorder = Color.FromArgb(51, 61, 71), NSelTexto = Color.FromArgb(38, 79, 120);
        // sintaxe do GeneXus Next (VS Code Dark+) por token, para "partir do GeneXus Next"
        static readonly string[] SintaxeNext = { "D4D4D4", "4EC9B0", "569CD6", "C586C0", "DCDCAA", "9CDCFE", "CE9178", "B5CEA8", "6A9955" };

        // chaves da tela: "fundo", "destaque", "s:<token>", "a:<area>", "o:<linha do Output>"
        Escolha esc = new Escolha();
        readonly Dictionary<string, Amostra> amostras = new Dictionary<string, Amostra>();
        readonly Dictionary<string, TextBox> hexes = new Dictionary<string, TextBox>();
        readonly Dictionary<string, CheckBox> marcas = new Dictionary<string, CheckBox>();
        readonly TableLayoutPanel grade;
        readonly Panel previa;
        readonly ComboBox cbBase;
        readonly Label aviso, alerta;
        bool carregando;

        public static void Exibir(IWin32Window dono)
        {
            if (aberta != null && !aberta.IsDisposed) { aberta.Activate(); return; }
            aberta = new MeuTema();
            aberta.Show(dono);
        }

        MeuTema()
        {
            Text = "Meu tema - " + Produto.NomeExibicao;
            FormBorderStyle = FormBorderStyle.Sizable; MaximizeBox = true; MinimizeBox = false; ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.None;
            Font = new Font("Segoe UI", 9f);
            BackColor = DarkEngine.On ? DarkEngine.Panel : SystemColors.Control;
            ForeColor = DarkEngine.On ? DarkEngine.Fg : SystemColors.ControlText;
            int u = Font.Height;

            SuspendLayout();
            grade = new TableLayoutPanel { Dock = DockStyle.Left, ColumnCount = 4, AutoScroll = true, BackColor = Color.Transparent, Width = u * 27,
                                           Padding = new Padding(u, (u * 3) / 4, u, u / 2) };
            grade.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            grade.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            grade.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            grade.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            Texto("Escolha as cores e veja a prévia ao lado. \"Aplicar e usar\" liga o Meu tema na hora, com o GeneXus aberto.", true);
            Titulo("Começar a partir de", true);
            cbBase = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Font = Font, Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top, Margin = new Padding(0, 0, 0, 2) };
            cbBase.Items.Add("(minhas cores salvas)");
            foreach (Tema t in Temas.Todos) if (!t.IsPersonalizado) cbBase.Items.Add(t);
            cbBase.SelectedIndexChanged += delegate { if (!carregando) Partir(cbBase.SelectedItem as Tema); };
            Linha(cbBase);

            Titulo("Tom e destaque", true);
            LinhaCor("fundo", "Tom do fundo", false);
            LinhaCor("destaque", "Destaque (foco, abas, links)", false);
            Texto("O tom tinge todos os fundos; o brilho segue o do GeneXus Next, para nada ficar ilegível.", true);

            Titulo("Cores exatas (opcional)", true);
            Texto("Marque para usar a cor exata naquela área. Desmarcado = automático, pelo tom. Fundo precisa ser escuro e texto, claro.", true);
            foreach (string[] a in Temas.Areas) LinhaCor("a:" + a[0], a[2], true);

            Titulo("Cores do código", true);
            for (int i = 0; i < Temas.Tokens.Length; i++) LinhaCor("s:" + Temas.Tokens[i], Temas.NomesTokens[i], false);

            Titulo("Cores do Output", true);
            Texto("Marque para trocar a cor da linha. Desmarcado = cor padrão do tema.", true);
            foreach (string[] o in Temas.OutputTokens) LinhaCor("o:" + o[0], o[2], true);

            previa = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            typeof(Panel).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(previa, true, null);
            previa.Paint += PintarPrevia;
            previa.Resize += delegate { previa.Invalidate(); };

            // botoes
            FlowLayoutPanel botoes = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                                                           BackColor = Color.Transparent, Padding = new Padding(u - 3, u / 4, u - 3, (u * 2) / 3) };
            Button fechar = Botao("Fechar"); fechar.Click += delegate { Close(); };
            Button aplicar = Botao("Aplicar e usar"); aplicar.Click += delegate { Aplicar(); };
            Button padrao = Botao("Voltar ao padrão"); padrao.Click += delegate { Partir(Temas.Next); };
            Button importar = Botao("Importar..."); importar.Click += delegate { Importar(); };
            Button exportar = Botao("Exportar..."); exportar.Click += delegate { Exportar(); };
            botoes.Controls.Add(fechar); botoes.Controls.Add(aplicar); botoes.Controls.Add(padrao); botoes.Controls.Add(importar); botoes.Controls.Add(exportar);
            CancelButton = fechar;

            // avisos acima dos botoes: estado (aplicado/alterado) e alerta de contraste baixo
            TableLayoutPanel rodape = new TableLayoutPanel { Dock = DockStyle.Bottom, ColumnCount = 1, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = Color.Transparent,
                                                             Padding = new Padding(u, u / 4, u, 0) };
            alerta = new Label { AutoSize = true, BackColor = Color.Transparent, ForeColor = Color.FromArgb(255, 176, 64), UseMnemonic = false, Margin = Padding.Empty };
            aviso = new Label { AutoSize = true, BackColor = Color.Transparent, UseMnemonic = false, Margin = Padding.Empty, ForeColor = DarkEngine.On ? DarkEngine.FgDim : SystemColors.GrayText };
            rodape.Controls.Add(alerta, 0, 0); rodape.Controls.Add(aviso, 0, 1);

            Controls.Add(previa);
            Controls.Add(grade);
            Controls.Add(rodape);
            Controls.Add(botoes);
            ResumeLayout(false);

            CarregarSalvas();
            Rectangle area = Screen.FromPoint(Cursor.Position).WorkingArea;
            ClientSize = new Size(Math.Min(area.Width - u * 2, u * 62), Math.Min(area.Height - u * 2, u * 44));
            MinimumSize = new Size(u * 48, u * 28);
            FormClosed += delegate { aberta = null; };
        }

        // ---------- dados
        void CarregarSalvas()
        {
            esc = DarkEngine.LerEscolha(DarkEngine.SettingsValue);
            foreach (string k in Temas.Tokens) if (!esc.Sintaxe.ContainsKey(k)) esc.Sintaxe[k] = Temas.Personalizado.Sintaxe[k];
            carregando = true; try { cbBase.SelectedIndex = 0; } finally { carregando = false; }
            Atualizar();
            aviso.Text = "";
        }

        void Partir(Tema t)
        {
            if (t == null) { CarregarSalvas(); return; }
            Escolha e = new Escolha { Fundo = t.M(NEditor), Destaque = t.M(NAccent) };
            for (int i = 0; i < Temas.Tokens.Length; i++)
                e.Sintaxe[Temas.Tokens[i]] = t.Sintaxe != null ? t.Sintaxe[Temas.Tokens[i]] : Cor.Hex(SintaxeNext[i]);
            esc = e;
            Atualizar();
            aviso.Text = "Cores copiadas de " + t.Nome + " (ainda não aplicadas)";
        }

        static string Id(string chave) { return chave.Substring(2); }
        static string[] Area(string id) { foreach (string[] a in Temas.Areas) if (a[0] == id) return a; return null; }

        // cor mostrada na tela; p = tema de previa (para as areas automaticas, que saem da conta)
        Color Valor(string k, Tema p)
        {
            if (k == "fundo") return esc.Fundo;
            if (k == "destaque") return esc.Destaque;
            if (k.StartsWith("s:")) return esc.Sintaxe[Id(k)];
            if (k.StartsWith("a:")) { string[] a = Area(Id(k)); return esc.Fixas.ContainsKey(a[0]) ? esc.Fixas[a[0]] : p.M(Cor.Hex(a[1])); }
            return esc.Output.ContainsKey(Id(k)) ? esc.Output[Id(k)] : Temas.OutputCor(p, Id(k));
        }

        bool Definir(string k, Color c)
        {
            c = Color.FromArgb(255, c.R, c.G, c.B);
            if (k == "fundo") esc.Fundo = c;
            else if (k == "destaque") esc.Destaque = c;
            else if (k.StartsWith("s:")) esc.Sintaxe[Id(k)] = c;
            else if (k.StartsWith("a:"))
            {
                string[] a = Area(Id(k));
                if (!DarkEngine.CorFixaValida(a[3], c))
                {
                    aviso.Text = a[2] + ": " + DarkEngine.CorTexto(c) + (a[3] == "t" ? " é escura demais para texto (escolha uma cor mais clara)" : " é clara demais para fundo (escolha uma cor mais escura)");
                    Atualizar();
                    return false;
                }
                esc.Fixas[a[0]] = c;
            }
            else esc.Output[Id(k)] = c;
            Atualizar();
            aviso.Text = "Alterado (ainda não aplicado)";
            return true;
        }

        Tema Previa() { return Temas.Previa(esc); }

        void Atualizar()
        {
            // cores do codigo que faltarem (arquivo importado incompleto, settings antigo): as atuais do Meu tema
            foreach (string k in Temas.Tokens) if (!esc.Sintaxe.ContainsKey(k)) esc.Sintaxe[k] = Temas.Personalizado.Sintaxe[k];
            Tema p = Previa();
            // o tom mostra o fundo que ele produz pela conta, mesmo que alguma area tenha cor exata
            Escolha soTom = esc.Copia(); soTom.Fixas.Clear();
            Tema pt = Temas.Previa(soTom);
            carregando = true;
            try
            {
                foreach (KeyValuePair<string, Amostra> kv in amostras)
                {
                    string k = kv.Key;
                    // o tom do fundo e o destaque mostram a cor que a IDE vai ter de fato (brilho do Next)
                    kv.Value.Cor = k == "fundo" ? pt.M(NEditor) : k == "destaque" ? pt.M(NAccent) : Valor(k, p);
                    TextBox tb = hexes[k];
                    if (!tb.Focused) tb.Text = DarkEngine.CorTexto(Valor(k, p));
                    CheckBox cb;
                    if (marcas.TryGetValue(k, out cb))
                    {
                        bool marcado = k.StartsWith("a:") ? esc.Fixas.ContainsKey(Id(k)) : esc.Output.ContainsKey(Id(k));
                        cb.Checked = marcado;
                        tb.ForeColor = marcado ? (DarkEngine.On ? DarkEngine.Fg : SystemColors.WindowText) : (DarkEngine.On ? DarkEngine.FgDim : SystemColors.GrayText);
                    }
                }
            }
            finally { carregando = false; }
            Contraste(p);
            previa.Invalidate();
        }

        // alerta (nao bloqueia): texto do codigo sobre o fundo do editor e texto da interface sobre os paineis
        void Contraste(Tema p)
        {
            List<string> l = new List<string>();
            Color ed = p.M(NEditor), pn = p.M(NPanel), fg = p.M(NFg);
            double c1 = Cor.Contraste(esc.Sintaxe["texto"], ed), c2 = Cor.Contraste(esc.Sintaxe["comentario"], ed), c3 = Cor.Contraste(fg, pn);
            if (c1 < 4.5) l.Add("texto do código sobre o editor " + c1.ToString("0.0") + ":1");
            if (c2 < 3.0) l.Add("comentário sobre o editor " + c2.ToString("0.0") + ":1");
            if (c3 < 4.5) l.Add("texto da interface sobre os painéis " + c3.ToString("0.0") + ":1");
            alerta.Text = l.Count == 0 ? "" : "Contraste baixo: " + string.Join("; ", l.ToArray()) + " (o recomendado é 4,5:1)";
        }

        void Aplicar()
        {
            try
            {
                DarkEngine.SalvarPersonalizado(esc, true);
                if (!DarkEngine.On) DarkEngine.SetMode(true, true);
                aviso.Text = "Meu tema aplicado e salvo";
                BackColor = DarkEngine.On ? DarkEngine.Panel : SystemColors.Control;
                ForeColor = DarkEngine.On ? DarkEngine.Fg : SystemColors.ControlText;
                carregando = true; try { cbBase.SelectedIndex = 0; } finally { carregando = false; }
                Invalidate(true);
            }
            catch (Exception ex)
            {
                DarkEngine.Log("Meu tema (aplicar): " + ex.GetType().Name + ": " + ex.Message);
                MessageBox.Show(this, "Não consegui aplicar o tema:\n" + ex.Message, "Meu tema", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        const string FiltroArquivo = "Meu tema (*.gxtema)|*.gxtema|Texto (*.txt)|*.txt|Todos os arquivos (*.*)|*.*";

        void Exportar()
        {
            using (SaveFileDialog d = new SaveFileDialog { Filter = FiltroArquivo, FileName = "MeuTema.gxtema", Title = "Exportar Meu tema" })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try { DarkEngine.ExportarEscolha(esc, d.FileName); aviso.Text = "Exportado: " + d.FileName; }
                catch (Exception ex) { MessageBox.Show(this, "Não consegui exportar:\n" + ex.Message, "Meu tema", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            }
        }

        void Importar()
        {
            using (OpenFileDialog d = new OpenFileDialog { Filter = FiltroArquivo, Title = "Importar Meu tema" })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    Escolha e = DarkEngine.ImportarEscolha(d.FileName);
                    foreach (string k in Temas.Tokens) if (!e.Sintaxe.ContainsKey(k)) e.Sintaxe[k] = esc.Sintaxe[k];
                    esc = e;
                    Atualizar();
                    aviso.Text = "Importado de " + System.IO.Path.GetFileName(d.FileName) + " (ainda não aplicado)";
                }
                catch (Exception ex) { MessageBox.Show(this, "Não consegui importar:\n" + ex.Message, "Meu tema", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            }
        }

        // ---------- montagem
        void Linha(Control c)
        {
            int r = grade.RowCount++;
            grade.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            grade.Controls.Add(c, 0, r);
            grade.SetColumnSpan(c, 4);
        }

        void Titulo(string t, bool espacoAntes)
        {
            Linha(new Label { Text = t, AutoSize = true, BackColor = Color.Transparent, UseMnemonic = false, Font = new Font(Font, FontStyle.Bold),
                              Margin = new Padding(0, espacoAntes ? Font.Height * 2 / 3 : 0, 0, Font.Height / 4) });
        }

        void Texto(string t, bool suave)
        {
            Label l = new Label { Text = t, AutoSize = true, MaximumSize = new Size(Font.Height * 24, 0), BackColor = Color.Transparent, UseMnemonic = false,
                                  Margin = new Padding(0, 0, 0, Font.Height / 4) };
            if (suave) l.ForeColor = DarkEngine.On ? DarkEngine.FgDim : SystemColors.GrayText;
            Linha(l);
        }

        // linha: [amostra][codigo #RRGGBB][marca opcional][nome]
        void LinhaCor(string chave, string nome, bool marcavel)
        {
            int u = Font.Height;
            int r = grade.RowCount++;
            grade.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Amostra b = new Amostra { Size = new Size(u * 2, u + 4), Cursor = Cursors.Hand, Margin = new Padding(0, 2, u / 2, 2), AccessibleName = nome };
            b.Click += delegate { Escolher(chave); };
            TextBox tb = new TextBox { Width = u * 5, Margin = new Padding(0, 2, u / 2, 2), CharacterCasing = CharacterCasing.Upper, MaxLength = 7,
                                       BorderStyle = BorderStyle.FixedSingle };
            tb.Leave += delegate { LerHex(chave, tb); };
            tb.KeyDown += delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { LerHex(chave, tb); e.SuppressKeyPress = true; } };
            Label l = new Label { Text = nome, AutoSize = true, BackColor = Color.Transparent, UseMnemonic = false, Margin = new Padding(0, u / 5 + 1, 0, 0), Cursor = Cursors.Hand };
            l.Click += delegate { Escolher(chave); };
            grade.Controls.Add(b, 0, r); grade.Controls.Add(tb, 1, r);
            if (marcavel)
            {
                CheckBox cb = new CheckBox { Text = "", AutoSize = false, Size = new Size(u + 2, u + 2), BackColor = Color.Transparent, Margin = new Padding(0, u / 5, u / 3, 0),
                                             AccessibleName = "Usar cor própria: " + nome };
                cb.CheckedChanged += delegate { if (!carregando) Marcar(chave, cb.Checked); };
                grade.Controls.Add(cb, 2, r); grade.Controls.Add(l, 3, r);
                marcas[chave] = cb;
            }
            else { grade.Controls.Add(l, 2, r); grade.SetColumnSpan(l, 2); }
            amostras[chave] = b; hexes[chave] = tb;
        }

        // marcar = passa a usar a cor que esta na tela; desmarcar = volta ao automatico/padrao
        void Marcar(string chave, bool marcado)
        {
            if (marcado) { if (!Definir(chave, Valor(chave, Previa()))) Atualizar(); return; }
            if (chave.StartsWith("a:")) esc.Fixas.Remove(Id(chave)); else esc.Output.Remove(Id(chave));
            Atualizar();
            aviso.Text = "Alterado (ainda não aplicado)";
        }

        void LerHex(string chave, TextBox tb)
        {
            Color c;
            Tema p = Previa();
            if (DarkEngine.LerCor(tb.Text, out c)) { if (c.ToArgb() != Valor(chave, p).ToArgb() || marcas.ContainsKey(chave) && !marcas[chave].Checked) Definir(chave, c); }
            else tb.Text = DarkEngine.CorTexto(Valor(chave, p));
        }

        void Escolher(string chave)
        {
            using (ColorDialog d = new ColorDialog { FullOpen = true, AnyColor = true, Color = Valor(chave, Previa()) })
                if (d.ShowDialog(this) == DialogResult.OK) Definir(chave, d.Color);
        }

        Button Botao(string t)
        {
            int u = Font.Height;
            return new Button { Text = t, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlatStyle = FlatStyle.Flat, UseVisualStyleBackColor = false,
                                MinimumSize = new Size(u * 6, u + u / 2 + 4), Padding = new Padding(u / 2, 0, u / 2, 0), Margin = new Padding(3) };
        }

        // ---------- previa: mini IDE (barra, KB Explorer, editor, Output) com as cores calculadas pelo mesmo M() do motor
        void PintarPrevia(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Tema p = Previa();
            Color cEditor = p.M(NEditor), cPanel = p.M(NPanel), cChrome = p.M(NChrome), cInput = p.M(NInput), cAccent = p.M(NAccent), cSel = p.M(NSel),
                  cFg = p.M(NFg), cDim = p.M(NFgDim), cBorder = p.M(NBorder), cSelTexto = p.M(NSelTexto);
            int u = Font.Height, m = u / 2;
            int topo = (u * 3) / 4 + u + 6;   // espaco para a legenda "Previa (aproximada)"
            Rectangle r = new Rectangle(m, topo, previa.ClientSize.Width - m * 2 - u / 2, previa.ClientSize.Height - topo - m);
            if (r.Width < u * 10 || r.Height < u * 10) return;
            const TextFormatFlags F = TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
            using (Font fUi = new Font("Segoe UI", 8.5f))
            using (Font fUiB = new Font("Segoe UI", 8.5f, FontStyle.Bold))
            using (Font fUiS = new Font("Segoe UI", 8.5f, FontStyle.Underline))
            using (Font fCod = new Font("Consolas", 9.5f))
            {
                int aUi = fUi.Height + 3;
                int hBarra = u + 6, hOut = aUi * 7 + 8, lArv = Math.Min(u * 9, r.Width / 3);
                Rectangle barra = new Rectangle(r.X, r.Y, r.Width, hBarra);
                Rectangle arv = new Rectangle(r.X, barra.Bottom, lArv, r.Height - hBarra);
                Rectangle ed = new Rectangle(arv.Right, barra.Bottom, r.Right - arv.Right, r.Height - hBarra - hOut);
                Rectangle saida = new Rectangle(arv.Right, ed.Bottom, ed.Width, hOut);
                using (SolidBrush b = new SolidBrush(cChrome)) g.FillRectangle(b, barra);
                using (SolidBrush b = new SolidBrush(cPanel)) g.FillRectangle(b, arv);
                using (SolidBrush b = new SolidBrush(cEditor)) { g.FillRectangle(b, ed); g.FillRectangle(b, saida); }
                TextRenderer.DrawText(g, "Arquivo   Editar   Exibir   Ferramentas", fUi, new Point(barra.X + m, barra.Y + 4), cFg, F);

                // KB Explorer: campo de busca + itens
                Rectangle busca = new Rectangle(arv.X + 4, arv.Y + 4, arv.Width - 8, aUi + 2);
                using (SolidBrush b = new SolidBrush(cInput)) g.FillRectangle(b, busca);
                using (Pen pen = new Pen(cBorder)) g.DrawRectangle(pen, busca);
                TextRenderer.DrawText(g, "Buscar", fUi, new Point(busca.X + 4, busca.Y + 2), cDim, F);
                string[] itens = { "Root Module", "  Pedido", "  PedidoWW", "  ProcCalculaTotal", "  Cliente" };
                int y = busca.Bottom + 4;
                for (int i = 0; i < itens.Length && y + aUi <= arv.Bottom; i++, y += aUi)
                {
                    if (i == 3)
                    {
                        using (SolidBrush b = new SolidBrush(cSel)) g.FillRectangle(b, arv.X, y - 1, arv.Width, aUi);
                        using (SolidBrush b = new SolidBrush(cAccent)) g.FillRectangle(b, arv.X, y - 1, 3, aUi);
                    }
                    TextRenderer.DrawText(g, itens[i], fUi, new Rectangle(arv.X + m, y, arv.Width - m, aUi), i == 3 ? cFg : cDim, F | TextFormatFlags.EndEllipsis);
                }

                // aba do editor com a linha de destaque
                Rectangle aba = new Rectangle(ed.X, ed.Y, Math.Min(u * 9, ed.Width), u + 4);
                using (SolidBrush b = new SolidBrush(cChrome)) g.FillRectangle(b, ed.X, ed.Y, ed.Width, aba.Height);
                using (SolidBrush b = new SolidBrush(cEditor)) g.FillRectangle(b, aba);
                using (SolidBrush b = new SolidBrush(cAccent)) g.FillRectangle(b, aba.X, aba.Y, aba.Width, 2);
                TextRenderer.DrawText(g, "ProcCalculaTotal", fUi, new Point(aba.X + m, aba.Y + 4), cFg, F);

                // codigo (a palavra "Format" aparece selecionada)
                object[][] linhas =
                {
                    new object[] { "// Soma os pedidos em aberto do cliente", "comentario" },
                    new object[] { "For Each ", "palavra", "Pedido ", "texto", "Order ", "controle", "PedDat", "texto" },
                    new object[] { "    Where ", "controle", "CliCod = ", "texto", "&CliCod", "variavel" },
                    new object[] { "    &Total ", "variavel", "+= PedVal * ", "texto", "1.05", "numero" },
                    new object[] { "    &Msg ", "variavel", "= ", "texto", "Format", "funcao", "(", "texto", "\"Pedido %1\"", "string", ", PedCod)", "texto" },
                    new object[] { "EndFor", "palavra" },
                    new object[] { "Sub ", "tipo", "'AvisaGerente'", "string" },
                    new object[] { "EndSub", "tipo" },
                };
                int ay = aba.Bottom + 4, aCod = fCod.Height + 2, xNum = ed.X + 4, xCod = ed.X + u * 2 + 4;
                for (int i = 0; i < linhas.Length && ay + aCod <= ed.Bottom; i++, ay += aCod)
                {
                    TextRenderer.DrawText(g, (i + 1).ToString().PadLeft(2), fCod, new Point(xNum, ay), cDim, F);
                    int x = xCod;
                    object[] l = linhas[i];
                    for (int j = 0; j < l.Length; j += 2)
                    {
                        string s = (string)l[j];
                        Size tam = TextRenderer.MeasureText(g, s, fCod, Size.Empty, F);
                        if (i == 4 && s == "Format") using (SolidBrush b = new SolidBrush(cSelTexto)) g.FillRectangle(b, x, ay, tam.Width, aCod);
                        TextRenderer.DrawText(g, s, fCod, new Point(x, ay), esc.Sintaxe[(string)l[j + 1]], F);
                        x += tam.Width;
                    }
                }

                // Output (fundo = o do editor, como no GeneXus)
                using (SolidBrush b = new SolidBrush(cChrome)) g.FillRectangle(b, saida.X, saida.Y, saida.Width, aUi + 2);
                TextRenderer.DrawText(g, "Output", fUi, new Point(saida.X + m, saida.Y + 2), cFg, F);
                string[][] saidas =
                {
                    new[] { "secao", "==== Build ProcCalculaTotal ====" },
                    new[] { "info", "Especificando ProcCalculaTotal..." },
                    new[] { "aviso", "warning: variável &Aux não é usada" },
                    new[] { "erro", "error: PedVal não pertence à tabela Pedido" },
                    new[] { "destaque", "Build concluído com 1 erro" },
                    new[] { "link", "Learn more" },
                };
                int oy = saida.Y + aUi + 5;
                foreach (string[] s in saidas)
                {
                    if (oy + aUi > saida.Bottom) break;
                    TextRenderer.DrawText(g, s[1], s[0] == "erro" ? fUiB : s[0] == "link" ? fUiS : fUi, new Point(saida.X + m, oy), Valor("o:" + s[0], p), F);
                    oy += aUi;
                }

                using (Pen pen = new Pen(cBorder))
                {
                    g.DrawRectangle(pen, r.X, r.Y, r.Width - 1, r.Height - 1);
                    g.DrawLine(pen, arv.Right, arv.Y, arv.Right, arv.Bottom);
                    g.DrawLine(pen, ed.X, aba.Bottom, ed.Right, aba.Bottom);
                    g.DrawLine(pen, saida.X, saida.Y, saida.Right, saida.Y);
                }
            }
            TextRenderer.DrawText(g, "Prévia (aproximada): as cores reais aparecem na IDE ao aplicar", Font, new Point(m, (u * 3) / 4), DarkEngine.On ? DarkEngine.FgDim : SystemColors.GrayText, TextFormatFlags.NoPrefix);
        }

        // quadradinho de cor que se desenha sozinho: o motor recolore BackColor de botoes e paineis, entao a cor fica num campo proprio
        internal sealed class Amostra : Control
        {
            Color cor = Color.Black;
            public Amostra() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true); TabStop = false; }
            public Color Cor { get { return cor; } set { cor = value; Invalidate(); } }
            protected override void OnPaint(PaintEventArgs e)
            {
                Rectangle r = ClientRectangle; r.Width--; r.Height--;
                using (SolidBrush b = new SolidBrush(cor)) e.Graphics.FillRectangle(b, r);
                using (Pen p = new Pen(Color.FromArgb(140, 140, 140))) e.Graphics.DrawRectangle(p, r);
            }
            protected override void OnPaintBackground(PaintEventArgs e) { }
        }
    }
}
