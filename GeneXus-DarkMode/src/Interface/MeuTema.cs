// GxDarkTheme - janela "Meu tema" (ideia da tela Personalizado do Xpz.Dark, de Filipe Castro).
// O usuario escolhe o tom do fundo, a cor de destaque e as 9 cores do codigo; a previa muda na hora e "Aplicar" grava
// as chaves meu_* no GxDarkTheme.settings e liga o tema "Meu tema" com a IDE aberta. Nada vai para as configuracoes do GeneXus.
// O tom do fundo define matiz e saturacao; o brilho de cada area continua o do GeneXus Next (mesma conta dos outros temas),
// por isso nenhuma escolha deixa texto ilegivel.
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
            NAccent = Color.FromArgb(44, 141, 206), NSel = Color.FromArgb(44, 60, 80), NFg = Color.FromArgb(220, 226, 235),
            NFgDim = Color.FromArgb(138, 144, 155), NBorder = Color.FromArgb(51, 61, 71);
        // sintaxe do GeneXus Next (VS Code Dark+) por token, para "partir do GeneXus Next"
        static readonly string[] SintaxeNext = { "D4D4D4", "4EC9B0", "569CD6", "C586C0", "DCDCAA", "9CDCFE", "CE9178", "B5CEA8", "6A9955" };

        Color fundo, destaque;
        readonly Dictionary<string, Color> sintaxe = new Dictionary<string, Color>();
        readonly Dictionary<string, Amostra> amostras = new Dictionary<string, Amostra>();
        readonly Dictionary<string, TextBox> hexes = new Dictionary<string, TextBox>();
        readonly TableLayoutPanel grade;
        readonly Panel previa;
        readonly ComboBox cbBase;
        readonly Label aviso;
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
            FormBorderStyle = FormBorderStyle.Sizable; MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.None;
            Font = new Font("Segoe UI", 9f);
            BackColor = DarkEngine.On ? DarkEngine.Panel : SystemColors.Control;
            ForeColor = DarkEngine.On ? DarkEngine.Fg : SystemColors.ControlText;
            int u = Font.Height;

            SuspendLayout();
            grade = new TableLayoutPanel { Dock = DockStyle.Left, ColumnCount = 3, AutoScroll = true, BackColor = Color.Transparent, Width = u * 24,
                                           Padding = new Padding(u, (u * 3) / 4, u / 2, u / 2) };
            grade.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            grade.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            grade.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            Texto("Escolha as cores e veja a prévia ao lado. \"Aplicar\" liga o Meu tema na hora, com o GeneXus aberto.", true);
            Titulo("Começar a partir de", true);
            cbBase = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Font = Font, Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top, Margin = new Padding(0, 0, 0, 2) };
            cbBase.Items.Add("(minhas cores salvas)");
            foreach (Tema t in Temas.Todos) if (!t.IsPersonalizado) cbBase.Items.Add(t);
            cbBase.SelectedIndexChanged += delegate { if (!carregando) Partir(cbBase.SelectedItem as Tema); };
            Linha(cbBase);

            Titulo("Cores da IDE", true);
            LinhaCor("fundo", "Tom do fundo");
            LinhaCor("destaque", "Destaque (seleção, foco, abas)");
            Texto("O tom muda a cor de todos os fundos; o brilho segue o do GeneXus Next, para nada ficar ilegível.", true);

            Titulo("Cores do código", true);
            for (int i = 0; i < Temas.Tokens.Length; i++) LinhaCor(Temas.Tokens[i], Temas.NomesTokens[i]);

            previa = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            typeof(Panel).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(previa, true, null);
            previa.Paint += PintarPrevia;
            previa.Resize += delegate { previa.Invalidate(); };

            // botoes
            FlowLayoutPanel botoes = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                                                           BackColor = Color.Transparent, Padding = new Padding(u - 3, u / 3, u - 3, (u * 2) / 3) };
            Button fechar = Botao("Fechar"); fechar.Click += delegate { Close(); };
            Button aplicar = Botao("Aplicar e usar"); aplicar.Click += delegate { Aplicar(); };
            Button padrao = Botao("Voltar ao padrão"); padrao.Click += delegate { Partir(Temas.Next); };
            botoes.Controls.Add(fechar); botoes.Controls.Add(aplicar); botoes.Controls.Add(padrao);
            aviso = new Label { AutoSize = true, BackColor = Color.Transparent, Margin = new Padding(0, u / 3 + 2, u, 0), ForeColor = DarkEngine.On ? DarkEngine.FgDim : SystemColors.GrayText };
            botoes.Controls.Add(aviso);
            CancelButton = fechar;
            AcceptButton = aplicar;

            Controls.Add(previa);
            Controls.Add(grade);
            Controls.Add(botoes);
            ResumeLayout(false);

            CarregarSalvas();
            Rectangle area = Screen.FromPoint(Cursor.Position).WorkingArea;
            int alt = Math.Min(area.Height - u * 2, grade.GetPreferredSize(new Size(grade.Width, 0)).Height + botoes.GetPreferredSize(Size.Empty).Height + u * 3);
            ClientSize = new Size(Math.Min(area.Width - u * 2, u * 56), Math.Max(u * 30, alt));
            MinimumSize = new Size(u * 44, u * 24);
            FormClosed += delegate { aberta = null; };
        }

        // ---------- dados
        void CarregarSalvas()
        {
            Tema p = Temas.Personalizado;
            Color f, d;
            if (!DarkEngine.LerCor(DarkEngine.SettingsValue(DarkEngine.ChaveFundo), out f)) f = Temas.FundoPadrao;
            if (!DarkEngine.LerCor(DarkEngine.SettingsValue(DarkEngine.ChaveDestaque), out d)) d = Temas.DestaquePadrao;
            fundo = f; destaque = d;
            foreach (string k in Temas.Tokens) sintaxe[k] = p.Sintaxe[k];
            carregando = true; try { cbBase.SelectedIndex = 0; } finally { carregando = false; }
            Atualizar();
        }

        void Partir(Tema t)
        {
            if (t == null) { CarregarSalvas(); return; }
            fundo = t.M(NEditor);
            destaque = t.M(NAccent);
            for (int i = 0; i < Temas.Tokens.Length; i++)
            {
                string k = Temas.Tokens[i];
                sintaxe[k] = t.Sintaxe != null ? t.Sintaxe[k] : Cor.Hex(SintaxeNext[i]);
            }
            Atualizar();
            aviso.Text = "Cores copiadas de " + t.Nome + " (ainda não aplicadas)";
        }

        Color Valor(string k) { return k == "fundo" ? fundo : k == "destaque" ? destaque : sintaxe[k]; }
        void Definir(string k, Color c)
        {
            c = Color.FromArgb(255, c.R, c.G, c.B);
            if (k == "fundo") fundo = c; else if (k == "destaque") destaque = c; else sintaxe[k] = c;
            Atualizar();
            aviso.Text = "Alterado (ainda não aplicado)";
        }

        Tema Previa() { return Temas.Previa(fundo, destaque, sintaxe); }

        void Atualizar()
        {
            Tema p = Previa();
            foreach (KeyValuePair<string, Amostra> kv in amostras)
            {
                // o tom do fundo mostra a cor que o editor vai ter de fato (brilho do Next); o destaque idem
                kv.Value.Cor = kv.Key == "fundo" ? p.M(NEditor) : kv.Key == "destaque" ? p.M(NAccent) : Valor(kv.Key);
                TextBox tb = hexes[kv.Key];
                if (!tb.Focused) tb.Text = DarkEngine.CorTexto(Valor(kv.Key));
            }
            previa.Invalidate();
        }

        void Aplicar()
        {
            try
            {
                DarkEngine.SalvarPersonalizado(fundo, destaque, new Dictionary<string, Color>(sintaxe), true);
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

        // ---------- montagem
        void Linha(Control c)
        {
            int r = grade.RowCount++;
            grade.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            grade.Controls.Add(c, 0, r);
            grade.SetColumnSpan(c, 3);
        }

        void Titulo(string t, bool espacoAntes)
        {
            Linha(new Label { Text = t, AutoSize = true, BackColor = Color.Transparent, UseMnemonic = false, Font = new Font(Font, FontStyle.Bold),
                              Margin = new Padding(0, espacoAntes ? Font.Height * 2 / 3 : 0, 0, Font.Height / 4) });
        }

        void Texto(string t, bool suave)
        {
            Label l = new Label { Text = t, AutoSize = true, MaximumSize = new Size(Font.Height * 21, 0), BackColor = Color.Transparent, UseMnemonic = false,
                                  Margin = new Padding(0, 0, 0, Font.Height / 5) };
            if (suave) l.ForeColor = DarkEngine.On ? DarkEngine.FgDim : SystemColors.GrayText;
            Linha(l);
        }

        void LinhaCor(string chave, string nome)
        {
            int u = Font.Height;
            int r = grade.RowCount++;
            grade.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Amostra b = new Amostra { Size = new Size(u * 2, u + 4), Cursor = Cursors.Hand, Margin = new Padding(0, 2, u / 2, 2), AccessibleName = nome };
            b.Click += delegate { Escolher(chave, nome); };
            TextBox tb = new TextBox { Width = u * 5, Margin = new Padding(0, 2, u / 2, 2), CharacterCasing = CharacterCasing.Upper, MaxLength = 7,
                                       BorderStyle = BorderStyle.FixedSingle };
            tb.Leave += delegate { LerHex(chave, tb); };
            tb.KeyDown += delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { LerHex(chave, tb); e.SuppressKeyPress = true; } };
            Label l = new Label { Text = nome, AutoSize = true, BackColor = Color.Transparent, UseMnemonic = false, Margin = new Padding(0, u / 5 + 1, 0, 0), Cursor = Cursors.Hand };
            l.Click += delegate { Escolher(chave, nome); };
            grade.Controls.Add(b, 0, r); grade.Controls.Add(tb, 1, r); grade.Controls.Add(l, 2, r);
            amostras[chave] = b; hexes[chave] = tb;
        }

        void LerHex(string chave, TextBox tb)
        {
            Color c;
            if (DarkEngine.LerCor(tb.Text, out c)) { if (c.ToArgb() != Valor(chave).ToArgb()) Definir(chave, c); }
            else tb.Text = DarkEngine.CorTexto(Valor(chave));
        }

        void Escolher(string chave, string nome)
        {
            using (ColorDialog d = new ColorDialog { FullOpen = true, AnyColor = true, Color = Valor(chave) })
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
            Color cEditor = p.M(NEditor), cPanel = p.M(NPanel), cChrome = p.M(NChrome), cAccent = p.M(NAccent), cSel = p.M(NSel),
                  cFg = p.M(NFg), cDim = p.M(NFgDim), cBorder = p.M(NBorder);
            int u = Font.Height, m = u / 2;
            int topo = (u * 3) / 4 + u + 6;   // espaco para a legenda "Previa (aproximada)"
            Rectangle r = new Rectangle(m, topo, previa.ClientSize.Width - m * 2 - u / 2, previa.ClientSize.Height - topo - m);
            if (r.Width < u * 10 || r.Height < u * 8) return;
            using (Font fUi = new Font("Segoe UI", 8.5f))
            using (Font fCod = new Font("Consolas", 9.5f))
            {
                int hBarra = u + 6, hOut = u * 4, lArv = Math.Min(u * 9, r.Width / 3);
                Rectangle barra = new Rectangle(r.X, r.Y, r.Width, hBarra);
                Rectangle arv = new Rectangle(r.X, barra.Bottom, lArv, r.Height - hBarra);
                Rectangle ed = new Rectangle(arv.Right, barra.Bottom, r.Right - arv.Right, r.Height - hBarra - hOut);
                Rectangle saida = new Rectangle(arv.Right, ed.Bottom, ed.Width, hOut);
                using (SolidBrush b = new SolidBrush(cChrome)) g.FillRectangle(b, barra);
                using (SolidBrush b = new SolidBrush(cPanel)) { g.FillRectangle(b, arv); g.FillRectangle(b, saida); }
                using (SolidBrush b = new SolidBrush(cEditor)) g.FillRectangle(b, ed);
                TextRenderer.DrawText(g, "Arquivo   Editar   Exibir   Ferramentas", fUi, new Point(barra.X + m, barra.Y + 3), cFg, TextFormatFlags.NoPrefix);

                // KB Explorer
                string[] itens = { "Root Module", "  Pedido", "  PedidoWW", "  ProcCalculaTotal", "  Cliente" };
                int y = arv.Y + 4, alt = fUi.Height + 4;
                for (int i = 0; i < itens.Length && y + alt <= arv.Bottom; i++, y += alt)
                {
                    if (i == 3)
                    {
                        using (SolidBrush b = new SolidBrush(cSel)) g.FillRectangle(b, arv.X, y - 1, arv.Width, alt);
                        using (SolidBrush b = new SolidBrush(cAccent)) g.FillRectangle(b, arv.X, y - 1, 3, alt);
                    }
                    TextRenderer.DrawText(g, itens[i], fUi, new Rectangle(arv.X + m, y, arv.Width - m, alt), i == 3 ? cFg : cDim, TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
                }

                // aba do editor com a linha de destaque
                Rectangle aba = new Rectangle(ed.X, ed.Y, Math.Min(u * 9, ed.Width), u + 4);
                using (SolidBrush b = new SolidBrush(cEditor)) g.FillRectangle(b, aba);
                using (SolidBrush b = new SolidBrush(cAccent)) g.FillRectangle(b, aba.X, aba.Y, aba.Width, 2);
                TextRenderer.DrawText(g, "ProcCalculaTotal", fUi, new Point(aba.X + m, aba.Y + 3), cFg, TextFormatFlags.NoPrefix);
                using (Pen pen = new Pen(cBorder)) g.DrawLine(pen, ed.X, aba.Bottom, ed.Right, aba.Bottom);

                // codigo
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
                    if (i == 3) using (SolidBrush b = new SolidBrush(cSel)) g.FillRectangle(b, ed.X, ay, ed.Width, aCod);
                    TextRenderer.DrawText(g, (i + 1).ToString().PadLeft(2), fCod, new Point(xNum, ay), cDim, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
                    int x = xCod;
                    object[] l = linhas[i];
                    for (int j = 0; j < l.Length; j += 2)
                    {
                        string s = (string)l[j];
                        Color c = sintaxe[(string)l[j + 1]];
                        TextRenderer.DrawText(g, s, fCod, new Point(x, ay), c, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
                        x += TextRenderer.MeasureText(g, s, fCod, Size.Empty, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding).Width;
                    }
                }

                // Output
                using (Pen pen = new Pen(cBorder)) g.DrawLine(pen, saida.X, saida.Y, saida.Right, saida.Y);
                TextRenderer.DrawText(g, "Output", fUi, new Point(saida.X + m, saida.Y + 3), cFg, TextFormatFlags.NoPrefix);
                TextRenderer.DrawText(g, "Build ProcCalculaTotal: sucesso", fUi, new Point(saida.X + m, saida.Y + 3 + fUi.Height + 4), cDim, TextFormatFlags.NoPrefix);

                using (Pen pen = new Pen(cBorder)) { g.DrawRectangle(pen, r.X, r.Y, r.Width - 1, r.Height - 1); g.DrawLine(pen, arv.Right, arv.Y, arv.Right, arv.Bottom); }
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
