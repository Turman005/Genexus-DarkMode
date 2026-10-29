// Xpz.Dark - TEMAS: conjuntos de cores trocaveis, inclusive a MOLDURA.
//
// Cada tema tem tres coisas:
//   - o TOM dos fundos (matiz + saturacao): cinza neutro, azul-ardosia, roxo...
//     Os fundos sao uma "rampa" desse tom, do mais escuro (editor) ao mais claro
//     (botao, borda);
//   - a cor de DESTAQUE (selecao, hover, links, a barra ativa);
//   - a paleta de SINTAXE do editor (palavra reservada, variavel, string...).
//
// A moldura (menus, barras, KB Explorer, abas) vem do .isl do GeneXus. Em vez de
// um .isl escuro fixo, o GeradorIsl reescreve o GenexusOffice2010Silver.isl da
// instalacao NA HORA com a rampa e o destaque do tema - a mesma conta do
// Gerar-IslEscuro.py (fundo/borda/texto por elemento, texto pelo contraste),
// agora parametrizada.
//
// Nomes proprios (nao os de produtos comerciais). A paleta Ardosia segue a
// referencia do projeto Genexus-DarkMode (Turman005): fundos azul-ardosia e
// sintaxe no estilo VS Code Dark+.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Xpz.Dark
{
    internal sealed class Tema
    {
        public string Id, Nome, Descricao;
        public double Matiz, Saturacao;          // tom dos fundos (graus, 0-1)
        public double Acento, SatAcento;         // destaque (graus, 0-1)
        public Dictionary<string, Color> Sintaxe = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);

        public Color Fundo(double l) { return Ajuste.DeHsl(Matiz / 360.0, Saturacao, l); }
        public Color Destaque(double s, double l) { return Ajuste.DeHsl(Acento / 360.0, s, l); }

        // ESTADOS DE INTERACAO - definidos UMA vez aqui e usados por todas as bibliotecas
        // (.isl da moldura, menus WinForms, botoes, grades, arvores). Antes cada parte tinha
        // a sua conta e o hover saia em tons diferentes (P1.8, 28/10).
        public Color Hover { get { return Destaque(SatAcento * 0.42, 0.24); } }
        public Color Pressionado { get { return Destaque(SatAcento * 0.60, 0.33); } }
        public Color Selecionado { get { return Destaque(SatAcento * 0.60, 0.30); } }
        public Color BordaDestaque { get { return Destaque(SatAcento * 0.65, 0.42); } }

        /// <summary>Amostra para a tela de configuracao: fundo, painel, destaque.</summary>
        public Color[] Amostra { get { return new[] { Fundo(0.085), Fundo(0.13), Destaque(SatAcento, 0.55) }; } }
    }

    internal static class Temas
    {
        public const string Padrao = "ardosia";
        public static readonly List<Tema> Todos = new List<Tema>();

        // tokens da sintaxe, na ordem do construtor S(...)
        private static readonly string[] Tokens =
            { "texto", "palavra", "controle", "tipo", "funcao", "variavel", "string", "numero", "comentario" };

        static Temas()
        {
            Todos.Add(Novo("ardosia", "Ardósia", "Azul-ardósia, sóbrio. Sintaxe estilo VS Code.", 216, 0.26, 205, 0.70,
                S("D4D4D4", "569CD6", "C586C0", "4EC9B0", "DCDCAA", "9CDCFE", "CE9178", "B5CEA8", "6A9955")));
            Todos.Add(Novo("grafite", "Grafite", "Cinza neutro, sem tom.", 0, 0.0, 207, 0.65,
                S("D4D4D4", "569CD6", "C586C0", "4EC9B0", "DCDCAA", "9CDCFE", "CE9178", "B5CEA8", "6A9955")));
            Todos.Add(Novo("meianoite", "Meia-noite", "Azul-marinho com destaque ciano.", 218, 0.45, 190, 0.75,
                S("D6DEEB", "82AAFF", "C792EA", "7FDBCA", "82AAFF", "ADDB67", "ECC48D", "F78C6C", "7A9C9C")));
            Todos.Add(Novo("ametista", "Ametista", "Quase preto com destaque roxo neon.", 265, 0.22, 270, 0.80,
                S("E2DDF0", "BD93F9", "FF79C6", "8BE9FD", "50FA7B", "F1FA8C", "FFB86C", "BD93F9", "8C84B8")));
            Todos.Add(Novo("brasa", "Brasa", "Escuro quente com destaque vermelho.", 0, 0.10, 4, 0.75,
                S("E6DCDA", "F07178", "FF9E64", "FFCB6B", "82AAFF", "F2C6A0", "C3E88D", "F78C6C", "8A929E")));
            Todos.Add(Novo("ambar", "Âmbar", "Escuro neutro com destaque laranja.", 30, 0.10, 32, 0.80,
                S("D9D7CE", "FFB454", "F29668", "E6B673", "FFD173", "95E6CB", "AAD94C", "D2A6FF", "8A909A")));
            Todos.Add(Novo("oliva", "Oliva", "Verde escuro suave, tons terrosos.", 140, 0.10, 145, 0.55,
                S("D3C6AA", "A7C080", "E69875", "83C092", "DBBC7F", "7FBBB3", "D699B6", "E67E80", "8F9A86")));
        }

        private static Tema Novo(string id, string nome, string desc, double matiz, double sat, double acento, double satAc, Dictionary<string, Color> s)
        {
            return new Tema { Id = id, Nome = nome, Descricao = desc, Matiz = matiz, Saturacao = sat, Acento = acento, SatAcento = satAc, Sintaxe = s };
        }

        private static Dictionary<string, Color> S(params string[] hex)
        {
            var d = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < Tokens.Length && i < hex.Length; i++)
                d[Tokens[i]] = Color.FromArgb(Convert.ToInt32(hex[i].Substring(0, 2), 16), Convert.ToInt32(hex[i].Substring(2, 2), 16), Convert.ToInt32(hex[i].Substring(4, 2), 16));
            return d;
        }

        public static Tema Achar(string id)
        {
            if (!string.IsNullOrEmpty(id))
                foreach (Tema t in Todos) if (string.Equals(t.Id, id.Trim(), StringComparison.OrdinalIgnoreCase)) return t;
            return null;
        }

        /// <summary>Tema do dark.ini. "Personalizado" usa o tema padrao como base.
        /// Valor desconhecido = tema padrao.</summary>
        public static Tema DoConfig(Dictionary<string, string> cfg)
        {
            string v = cfg == null ? null : Config.Valor(cfg, "Tema");
            Tema t = Achar(v);
            if (t != null) return t;
            // "Escuro" era so o valor padrao da 1a tela (27/10), nao uma escolha: vai para o padrao novo.
            return Achar(Padrao);
        }

        /// <summary>Chave do estilo de realce do GeneXus -> token da paleta do tema.</summary>
        public static string TokenDe(string chaveEstilo)
        {
            switch (chaveEstilo)
            {
                case "DefaultStyle": return "texto";
                case "ReservedWordStyle": return "palavra";
                case "KeyWordStyle": return "controle";
                case "DefWordStyle": return "tipo";
                case "FcnReservedWordStyle":
                case "FcnSomeGenReservedWordStyle": return "funcao";
                case "VariableStyle":
                case "VariableReplaceStyle": return "variavel";
                case "OperatorStyle": return "texto";
                case "NumberStyle": return "numero";
                case "StringDelimiterStyle":
                case "StringDefaultStyle": return "string";
                case "CommentDelimiterStyle":
                case "CommentDefaultStyle": return "comentario";
                default: return null;
            }
        }
    }

    // =====================================================================
    //  .isl do tema, gerado a partir do .isl do proprio GeneXus
    // =====================================================================
    internal static class GeradorIsl
    {
        private static readonly Dictionary<string, Color> Nomes = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase)
        {
            { "black", Color.FromArgb(0, 0, 0) }, { "brown", Color.FromArgb(165, 42, 42) }, { "darkgray", Color.FromArgb(169, 169, 169) },
            { "darkorange", Color.FromArgb(255, 140, 0) }, { "gainsboro", Color.FromArgb(220, 220, 220) }, { "gray", Color.FromArgb(128, 128, 128) },
            { "lightgray", Color.FromArgb(211, 211, 211) }, { "peru", Color.FromArgb(205, 133, 63) }, { "silver", Color.FromArgb(192, 192, 192) },
            { "white", Color.FromArgb(255, 255, 255) }, { "whitesmoke", Color.FromArgb(245, 245, 245) },
            { "gradientactivecaption", Color.FromArgb(185, 209, 234) },
        };

        private static readonly string[] AtrFundo = { "backColor", "backColor2" };
        private static readonly string[] AtrBorda = { "borderColor", "borderColor2", "borderColor3DBase" };

        private static readonly Regex ReTag = new Regex("<[A-Za-z][\\w]*(?:\\s+[\\w:]+=\"[^\"]*\")+\\s*/?>", RegexOptions.Compiled);
        private static readonly Regex ReAtr = new Regex("([\\w:]+)=\"([^\"]*)\"", RegexOptions.Compiled);
        private static readonly Regex ReProp = new Regex("<property name=\"(\\w*Color\\w*)\"([^>]*)>([^<]*)</property>", RegexOptions.Compiled);

        /// <summary>O .isl original da instalacao (a pasta do GeneXus.exe).</summary>
        public static string Original()
        {
            string arq = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GenexusOffice2010Silver.isl");
            if (File.Exists(arq)) return arq;
            // Fora do GeneXus (teste): a pasta de onde a Infragistics foi carregada.
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                if (a.GetName().Name.StartsWith("Infragistics2.Win.v", StringComparison.Ordinal))
                {
                    try { string d = Path.Combine(Path.GetDirectoryName(a.Location), "GenexusOffice2010Silver.isl"); if (File.Exists(d)) return d; } catch { }
                }
            return arq;
        }

        public static string Gerar(string islOriginal, Tema t)
        {
            var g = new Conta(t);
            string s = islOriginal;
            s = Regex.Replace(s, "(<styleLibrary\\b[^>]*?office2007ColorScheme=\")[^\"]*(\")", "${1}Black${2}");
            s = Regex.Replace(s, "(<styleLibrary\\b[^>]*?office2010ColorScheme=\")[^\"]*(\")", "${1}Black${2}");
            s = ReTag.Replace(s, g.Tag);
            s = ReProp.Replace(s, g.Propriedade);
            s = ReEstilo.Replace(s, g.HoverSemFundo);
            s = AreaDeIcones(s, t);
            s = MarcadoLegivel(s, t);
            // 8 componentes (UltraButton, UltraComboEditor, UltraListView, UltraScrollBar,
            // UltraTabControl...) pediam o visual do WINDOWS - claro, por cima das cores do
            // .isl: abas brancas na foto do teste de temas (28/10). Visual da Infragistics.
            s = s.Replace("useOsThemes=\"True\"", "useOsThemes=\"False\"");
            s = ListaDeBarras(s);
            return s;
        }


        // A coluna de icones dos menus (MenuIconArea*) nao esta no .isl Silver do GeneXus:
        // sem papel declarado, a Infragistics usa a cor do proprio controle - a "lateral
        // branca" dos menus (auditoria de 28/10). Declarados, valem ate sobre a aparencia
        // do proprio gerenciador. Fundo do menu, como no VS Code.
        private static readonly string[] PapeisIcones = { "MenuIconArea", "MenuIconAreaRecentlyUsed", "MenuIconAreaNonRecentlyUsed" };
        private static string AreaDeIcones(string s, Tema t)
        {
            Color f = new Conta(t).Fundo(Color.WhiteSmoke); string fundo = f.R + ", " + f.G + ", " + f.B;
            var sb = new System.Text.StringBuilder();
            foreach (string p in PapeisIcones)
            {
                if (s.Contains("<style role=\"" + p + "\"")) continue;
                sb.Append("        <style role=\"").Append(p).Append("\">\n          <states>\n            <state name=\"Normal\" backColor=\"")
                  .Append(fundo).Append("\" backColor2=\"").Append(fundo).Append("\" backGradientStyle=\"None\" backHatchStyle=\"None\" />\n          </states>\n        </style>\n");
            }
            int fim = s.IndexOf("      </styles>", StringComparison.Ordinal);
            return sb.Length == 0 || fim < 0 ? s : s.Insert(fim, sb.ToString());
        }

        // Item MARCADO de menu (StateButtonTool com Checked, ex.: o tema atual no submenu "Tema
        // escuro"): para a Infragistics ele esta PRESSIONADO (estado Pressed; um estado
        // "Checked" nao muda nada - medido). O papel MenuItem nao declara Pressed, e a letra
        // vinha da tabela Office 2010 - escura no fundo escuro (contraste 1,5 no TestePopups,
        // 28/10). So a letra: fundo de pressionado num item marcado pareceria hover preso.
        private static string MarcadoLegivel(string s, Tema t)
        {
            int ini = s.IndexOf("<style role=\"MenuItem\">", StringComparison.Ordinal);
            int fim = ini < 0 ? -1 : s.IndexOf("</style>", ini, StringComparison.Ordinal);
            if (fim < 0 || s.IndexOf("name=\"Pressed\"", ini, fim - ini, StringComparison.Ordinal) >= 0) return s;
            int estados = s.IndexOf("<states>", ini, fim - ini, StringComparison.Ordinal);
            if (estados < 0) return s;
            Color c = Ajuste.DeHsl(t.Matiz / 360.0, t.Saturacao * 0.30, 0.90);
            return s.Insert(estados + "<states>".Length, "\n            <state name=\"Pressed\" foreColor=\"" + c.R + ", " + c.G + ", " + c.B + "\" />");
        }

        // Botao direito na barra de ferramentas: lista das barras (Edit, Formatting, Build...).
        // O menu (ToolbarListMenu) e os itens (MenuItemToolbarList) tem papeis proprios que o
        // .isl do GeneXus nao declara: o fundo saia na cor do controle - BRANCO, com a letra
        // clara do tema (29/10; o papel do menu e o 34 do StyleUtils.Role da Infragistics).
        // Mesmos estilos do menu suspenso e do item de menu comuns.
        private static string ListaDeBarras(string s)
        {
            s = Copiar(s, "PopupMenu", "ToolbarListMenu");
            return Copiar(s, "MenuItem", "MenuItemToolbarList");
        }

        private static string Copiar(string s, string de, string para)
        {
            if (s.Contains("role=\"" + para + "\"")) return s;
            Match m = Regex.Match(s, "<style role=\"" + de + "\"[^>]*>.*?</style>", RegexOptions.Singleline);
            if (!m.Success) return s;
            string copia = m.Value.Replace("role=\"" + de + "\"", "role=\"" + para + "\"");
            return s.Insert(m.Index + m.Length, "\n        " + copia);
        }

        // Papel (<style role="...">) com seus estados.
        private static readonly Regex ReEstilo = new Regex("<style role=\"([^\"]+)\"[^>]*>.*?</style>", RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex ReEstadoHover = new Regex("<state name=\"(HotTracked|Pressed|HotTrackSelected)\"([^>]*?)(/?>)", RegexOptions.Compiled);
        // Papeis de TEXTO: hover ali e so mudar a cor da letra; fundo pintaria uma caixa.
        private static readonly HashSet<string> SemCaixa = new HashSet<string>(StringComparer.Ordinal)
            { "EditorControl", "CheckEditor", "GridCell", "LiveTileViewGroupTextArea", "ScheduleOwnerHeader", "TreeNodeExpansionIndicator", "UltraGroupBoxExpansionIndicator" };

        private sealed class Conta
        {
            private readonly Tema _t;
            private readonly Color _presumido;
            public Conta(Tema t) { _t = t; _presumido = t.Fundo(0.16); }

            private static bool Ler(string v, out Color c)
            {
                c = Color.Empty;
                string s = (v ?? "").Trim();
                if (s.Length == 0 || s.Equals("Transparent", StringComparison.OrdinalIgnoreCase)) return false;
                string[] p = s.Split(',');
                try
                {
                    if (p.Length == 3) { c = Color.FromArgb(int.Parse(p[0].Trim()), int.Parse(p[1].Trim()), int.Parse(p[2].Trim())); return true; }
                    if (p.Length == 4) { c = Color.FromArgb(int.Parse(p[0].Trim()), int.Parse(p[1].Trim()), int.Parse(p[2].Trim()), int.Parse(p[3].Trim())); return true; }
                }
                catch { return false; }
                return Nomes.TryGetValue(s, out c);
            }

            // A transparencia ("A, R, G, B") e mantida: a celula da grade e branca a 40% sobre a
            // linha, e a cor de selecao da linha aparece ATRAVES dela. Opaca, a celula cobria a
            // selecao - Ctrl+clique no History selecionava sem mostrar (TesteSelecao, 29/10).
            private static string Escrever(Color c) { return (c.A < 255 ? c.A + ", " : "") + c.R + ", " + c.G + ", " + c.B; }
            // So a cor TRANSLUCIDA (as da grade). A totalmente transparente ("0, 0, 0, 0", fundo do
            // botao da barra de titulo dos paineis) segue opaca como antes da 0.13.3 - o menu das
            // abas apareceu preto logo depois daquela versao e esta era a outra mudanca dela.
            private static Color Alfa(Color original, Color nova) { return original.A > 0 && original.A < 255 ? Color.FromArgb(original.A, nova) : nova; }
            private static bool Neutra(Color c) { return Math.Max(c.R, Math.Max(c.G, c.B)) - Math.Min(c.R, Math.Min(c.G, c.B)) < 28; }
            private static double Limitar(double v, double a, double b) { return v < a ? a : v > b ? b : v; }

            public Color Fundo(Color c)
            {
                double h, s, l; Ajuste.Hsl(c, out h, out s, out l);
                if (Neutra(c)) return _t.Fundo(Math.Min(0.26, 0.085 + (1.0 - l) * 0.40));
                return _t.Destaque(Math.Min(s, 0.85) * _t.SatAcento, Limitar(l * 0.55, 0.16, 0.42));
            }

            public Color Borda(Color c)
            {
                double h, s, l; Ajuste.Hsl(c, out h, out s, out l);
                if (Neutra(c)) return _t.Fundo(0.20 + (1.0 - l) * 0.30);
                return _t.Destaque(Math.Min(s, 0.85) * _t.SatAcento, Limitar(l * 0.55 + 0.06, 0.22, 0.48));
            }

            public Color Texto(Color c, Color fundoNovo)
            {
                double h, s, l; Ajuste.Hsl(c, out h, out s, out l);
                Color novo;
                if (Neutra(c))
                {
                    double alvo = l > 0.6 ? Math.Max(l, 0.86) : 0.92 - l * 0.65;
                    novo = Ajuste.DeHsl(_t.Matiz / 360.0, _t.Saturacao * 0.30, alvo);
                }
                else novo = _t.Destaque(Math.Min(s, 0.9), Math.Max(0.66, 1.0 - l * 0.6));
                if (l < 0.25)
                {
                    double h2, s2, l2; Ajuste.Hsl(novo, out h2, out s2, out l2);
                    while (Paleta.Contraste(novo, fundoNovo) < 4.5 && l2 < 0.97) { l2 = Math.Min(0.97, l2 + 0.03); novo = Ajuste.DeHsl(h2, s2, l2); }
                }
                return novo;
            }

            public string Tag(Match m)
            {
                string tag = m.Value;
                var attrs = new Dictionary<string, string>();
                foreach (Match a in ReAtr.Matches(tag)) attrs[a.Groups[1].Value] = a.Groups[2].Value;
                bool tem = false;
                foreach (string k in attrs.Keys) if (Array.IndexOf(AtrFundo, k) >= 0 || Array.IndexOf(AtrBorda, k) >= 0 || k == "foreColor") { tem = true; break; }
                if (!tem) return tag;

                var novos = new Dictionary<string, Color>();
                Color c, fundoNovo = Color.Empty;
                foreach (string a in AtrFundo)
                    if (attrs.ContainsKey(a) && Ler(attrs[a], out c)) { novos[a] = Alfa(c, Fundo(c)); if (a == "backColor") fundoNovo = Fundo(c); }
                foreach (string a in AtrBorda)
                    if (attrs.ContainsKey(a) && Ler(attrs[a], out c)) novos[a] = Alfa(c, Borda(c));
                if (attrs.ContainsKey("foreColor") && Ler(attrs["foreColor"], out c))
                    novos["foreColor"] = Texto(c, fundoNovo.IsEmpty ? _presumido : fundoNovo);

                // Estado de interacao: a cor e a do TEMA (a mesma de todas as bibliotecas), nao
                // a conversao da cor original de cada papel. Selecionado so quando a selecao
                // original era colorida (azul de lista/arvore); selecao neutra (aba ativa)
                // segue a rampa de fundos.
                string estado;
                if (tag.StartsWith("<state") && attrs.TryGetValue("name", out estado))
                {
                    Color token = Color.Empty;
                    if (estado == "HotTracked") token = _t.Hover;
                    else if (estado == "Pressed" || estado == "HotTrackSelected") token = _t.Pressionado;
                    // selecao semitransparente (escurecer 24% a linha da grade) tambem e selecao
                    // de verdade: no escuro, "escurecer" nao se ve - usa a cor do tema, opaca.
                    else if (estado == "Selected" && attrs.ContainsKey("backColor") && Ler(attrs["backColor"], out c) && (!Neutra(c) || c.A < 255)) token = _t.Selecionado;
                    if (!token.IsEmpty && novos.ContainsKey("backColor"))
                    {
                        foreach (string a in AtrFundo) if (novos.ContainsKey(a)) novos[a] = token;
                        foreach (string a in AtrBorda) if (novos.ContainsKey(a)) novos[a] = _t.BordaDestaque;
                        if (attrs.ContainsKey("foreColor") && Ler(attrs["foreColor"], out c)) novos["foreColor"] = Texto(c, token);
                    }
                }

                return ReAtr.Replace(tag, am =>
                {
                    Color n;
                    return novos.TryGetValue(am.Groups[1].Value, out n) ? am.Groups[1].Value + "=\"" + Escrever(n) + "\"" : am.Value;
                });
            }

            /// <summary>
            /// 87 papeis do .isl do GeneXus declaram o hover/pressionado SEM cor de fundo
            /// (ToolbarItem, TreeNode, botoes...). Sem cor declarada, a Infragistics usa a da
            /// tabela Office 2010 - dourada/amarela em qualquer tema (o "hover amarelo" do
            /// Ametista, 28/10). Declara o fundo e a borda na cor de destaque do tema.
            /// </summary>
            /// <remarks>
            /// O mesmo vale para a LETRA: o hover do MenuItem so declara o fundo, e a letra vinha
            /// da tabela Office 2010, que e escura - letra escura no fundo escuro do tema (o item
            /// "Tema escuro" com o mouse em cima, 28/10). Declara tambem a letra do tema.
            /// </remarks>
            public string HoverSemFundo(Match m)
            {
                if (SemCaixa.Contains(m.Groups[1].Value)) return m.Value;
                return ReEstadoHover.Replace(m.Value, e =>
                {
                    string atrs = e.Groups[2].Value;
                    bool pressionado = e.Groups[1].Value != "HotTracked";
                    // so acrescenta o que falta: atributo repetido invalida o XML inteiro
                    string extra = "";
                    if (!atrs.Contains("backColor="))
                    {
                        extra += " backColor=\"" + Escrever(pressionado ? _t.Pressionado : _t.Hover) + "\"";
                        if (!atrs.Contains("borderColor=")) extra += " borderColor=\"" + Escrever(_t.BordaDestaque) + "\"";
                    }
                    if (!atrs.Contains("foreColor=")) extra += " foreColor=\"" + Escrever(LetraClara()) + "\"";
                    return extra.Length == 0 ? e.Value : "<state name=\"" + e.Groups[1].Value + "\"" + atrs + extra + e.Groups[3].Value;
                });
            }

            /// <summary>A letra clara do tema (a mesma do Texto da paleta, levemente tingida).</summary>
            private Color LetraClara() { return Ajuste.DeHsl(_t.Matiz / 360.0, _t.Saturacao * 0.30, 0.90); }

            public string Propriedade(Match m)
            {
                string nome = m.Groups[1].Value, resto = m.Groups[2].Value;
                Color c;
                if (!Ler(m.Groups[3].Value, out c)) return m.Value;
                Color n = nome.Contains("BackColor") ? Fundo(c) : nome.Contains("BorderColor") ? Borda(c) : Texto(c, _presumido);
                return "<property name=\"" + nome + "\"" + resto + ">" + Escrever(n) + "</property>";
            }
        }
    }
}
