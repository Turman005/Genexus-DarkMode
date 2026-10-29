// Xpz.Dark - FASE 3: o editor de codigo (Source, Rules, Events, Conditions...).
//
// O editor e o SyntaxEditor 4.0 da Actipro. Duas coisas decidem a cor:
//
//   1. O RENDERIZADOR: fundo da area de texto, margem dos numeros de linha,
//      selecao, guias. Aqui um VisualStudio2005SyntaxEditorRenderer nosso, com
//      fundos escuros, posto em cada editor.
//
//   2. Os ESTILOS DE REALCE da linguagem (palavra reservada, variavel, comentario,
//      string...). As cores sao do GeneXus (Definitions\*.xml so declaram as
//      chaves). Cada cor e AJUSTADA, nao trocada: mantem o matiz (azul continua
//      azul, verde continua verde) e so clareia ate o contraste minimo com o fundo
//      (WCAG 4.5 por padrao). Preto/cinza viram o texto claro da paleta.
//
// IDEMPOTENTE: a cor original de cada estilo fica guardada. Se o GeneXus trocar o
// estilo (ex.: o usuario mudou a cor nas opcoes), o novo valor vira o original e
// e ajustado de novo - nunca se clareia o que ja foi clareado.
//
// DESEMPENHO: os editores sao lembrados por WeakReference e revisitados no tick
// de 1s da fase 2 - dezenas de estilos por editor, microssegundos.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

using ActiproSoftware.Drawing;
using ActiproSoftware.SyntaxEditor;

namespace Xpz.Dark
{
    internal static class Editor
    {
        public static bool Ligado { get; private set; }

        private static bool _ajustarSintaxe = true;
        private static VisualStudio2005SyntaxEditorRenderer _renderer;
        private static readonly List<WeakReference> _editores = new List<WeakReference>();
        private static readonly HashSet<string> _renderersVistos = new HashSet<string>();
        private static int _estilosAjustados;

        private sealed class Cores { public Color Fore, Back, FeitoFore, FeitoBack; public int Geracao; }
        // Sobe a cada troca de tema: estilo de outra geracao e recalculado a partir do ORIGINAL.
        private static int _geracao;
        private static bool _sintaxeDoTema = true;
        private static readonly ConditionalWeakTable<HighlightingStyle, Cores> _estilos = new ConditionalWeakTable<HighlightingStyle, Cores>();
        private static readonly ConditionalWeakTable<SyntaxLanguage, object> _fundoLinguagem = new ConditionalWeakTable<SyntaxLanguage, object>();

        public static void Ligar(Dictionary<string, string> cfg)
        {
            if (!Disjuntor.Ligada(Parte.Editor)) { Log.Linha("fase 3: editor nao ligado (parte desligada)"); return; }
            try
            {
                _ajustarSintaxe = Config.SimPadrao(cfg, "EditorAjustarSintaxe");
                _sintaxeDoTema = Config.SimPadrao(cfg, "EditorSintaxeDoTema");
                _renderer = CriarRenderer();
                Ligado = true;
                Log.Linha(string.Format("fase 3: editor de codigo escuro ligado (fundo {0}, contraste minimo {1}, sintaxe {2})",
                    Paleta.Texto3(Paleta.FundoCodigo), Paleta.ContrasteCodigo, _ajustarSintaxe ? "ajustada" : "intocada"));
            }
            catch (Exception ex) { Ligado = false; Log.Linha("fase 3: editor FALHOU ao ligar: " + Log.Curto(ex)); }
        }

        public static bool Eh(Control c) { return c is SyntaxEditor; }

        private static RolagemEscura _rolagem;
        public static bool EhRolagem(Control c) { return c is ActiproSoftware.WinUICore.ScrollBar; }

        public static void Rolagem(Control c)
        {
            var sb = (ActiproSoftware.WinUICore.ScrollBar)c;
            if (_rolagem == null) _rolagem = new RolagemEscura();
            if (!object.ReferenceEquals(sb.Renderer, _rolagem)) { sb.Renderer = _rolagem; sb.Invalidate(); }
        }

        public static void Escurecer(Control c)
        {
            var ed = c as SyntaxEditor;
            if (ed == null) return;
            lock (_editores) _editores.Add(new WeakReference(ed));
            Aplicar(ed);
            // O GeneXus pode trocar o documento/linguagem ao abrir outra parte.
            ed.VisibleChanged += delegate { if (ed.Visible) Aplicar(ed); };
            ed.Enter += delegate { Aplicar(ed); };
        }

        /// <summary>Tema novo: renderizador novo e estilos recalculados do original.</summary>
        public static void TrocarTema()
        {
            if (!Ligado) return;
            _geracao++;
            _renderer = CriarRenderer();
            if (_rolagem != null) _rolagem = new RolagemEscura();
            Revisitar();
        }

        /// <summary>Chamado no tick de 1s. true = havia editor vivo.</summary>
        public static bool Revisitar()
        {
            if (!Ligado) return false;
            bool algum = false;
            lock (_editores)
            {
                for (int i = _editores.Count - 1; i >= 0; i--)
                {
                    var ed = _editores[i].Target as SyntaxEditor;
                    if (ed == null || ed.IsDisposed) { _editores.RemoveAt(i); continue; }
                    algum = true;
                    Aplicar(ed);
                }
            }
            return algum;
        }

        private static void Aplicar(SyntaxEditor ed)
        {
            if (!Disjuntor.Ligada(Parte.Editor)) return;
            try
            {
                if (!object.ReferenceEquals(ed.Renderer, _renderer))
                {
                    // Para o diagnostico: que renderizador o GeneXus usava.
                    string tipo = ed.Renderer == null ? "(padrao global)" : ed.Renderer.GetType().FullName;
                    if (_renderersVistos.Add(tipo)) Log.Linha("fase 3: editor " + ed.GetType().Name + " usava renderizador " + tipo);
                    ed.Renderer = _renderer;
                }
                if (ed.BackColor.ToArgb() != Paleta.FundoCodigo.ToArgb()) ed.BackColor = Paleta.FundoCodigo;
                if (ed.ForeColor.ToArgb() != Paleta.Texto.ToArgb()) ed.ForeColor = Paleta.Texto;

                if (_ajustarSintaxe && ed.Document != null && ed.Document.Language != null)
                {
                    int antes = _estilosAjustados;
                    Linguagem(ed.Document.Language, 0);
                    if (_estilosAjustados != antes) ed.Invalidate();
                }
                // History > Compare: as linhas diferentes vem com fundo claro, linha a linha.
                if (Comparador.EhEditorDeDiferenca(ed)) Comparador.Linhas(ed);
            }
            catch (Exception ex) { Disjuntor.Falhou(Parte.Editor, ex); }
        }

        private static void Linguagem(SyntaxLanguage l, int nivel)
        {
            if (l == null || nivel > 4) return;
            object x;
            if (!_fundoLinguagem.TryGetValue(l, out x))
            {
                _fundoLinguagem.Add(l, new object());
                Log.Linha("fase 3: linguagem " + l.Key + " com " + l.HighlightingStyles.Count + " estilos");
            }
            if (!l.BackColor.IsEmpty && Paleta.Lum(l.BackColor) > 0.5) l.BackColor = Color.Empty;
            foreach (HighlightingStyle s in l.HighlightingStyles) Estilo(s);
            foreach (SyntaxLanguage f in l.ChildLanguages) Linguagem(f, nivel + 1);
        }

        private static bool Igual(Color a, Color b) { return a.IsEmpty == b.IsEmpty && a.ToArgb() == b.ToArgb(); }

        private static void Estilo(HighlightingStyle s)
        {
            Cores c;
            if (_estilos.TryGetValue(s, out c))
            {
                bool intacto = Igual(s.ForeColor, c.FeitoFore) && Igual(s.BackColor, c.FeitoBack);
                if (intacto && c.Geracao == _geracao) return;                                    // ja ajustado
                if (!intacto) { c.Fore = s.ForeColor; c.Back = s.BackColor; }                    // o GeneXus trocou: novo original
            }
            else
            {
                c = new Cores { Fore = s.ForeColor, Back = s.BackColor };
                _estilos.Add(s, c);
            }
            Color fundo = Ajuste.FundoDoEstilo(c.Back);
            Color fundoTexto = fundo.IsEmpty ? Paleta.FundoCodigo : fundo;
            Color texto;
            // Paleta de sintaxe do tema (estilo VS Code) para os estilos conhecidos do
            // GeneXus; o resto (HTML, CSS...) segue a regra de clarear mantendo o tom.
            // Mesmo a cor do tema passa pelo contraste minimo.
            string token = Temas.TokenDe(s.Key);
            Color doTema;
            if (_sintaxeDoTema && token != null && !Paleta.Personalizado && Paleta.TemaAtual.Sintaxe.TryGetValue(token, out doTema))
                texto = Ajuste.Ajustar(doTema, s.Key, fundoTexto, Paleta.Texto, Paleta.ContrasteCodigo);
            else
                texto = Ajuste.TextoDoEstilo(c.Fore, s.Key, fundoTexto);
            if (!Igual(s.BackColor, fundo)) s.BackColor = fundo;
            if (!Igual(s.ForeColor, texto)) s.ForeColor = texto;
            c.FeitoFore = s.ForeColor; c.FeitoBack = s.BackColor; c.Geracao = _geracao;
            _estilosAjustados++;
        }

        // ------------------------------------------------------------ renderer
        private static VisualStudio2005SyntaxEditorRenderer CriarRenderer()
        {
            var r = new RendererDoEditor();
            Color fundo = Paleta.FundoCodigo, margem = Paleta.Somar(Paleta.FundoCodigo, 6);
            r.ResetAllPropertiesOnSystemColorChange = false;
            r.TextAreaBackgroundFill = new SolidColorBackgroundFill(fundo);
            r.TextAreaDisabledBackgroundFill = new SolidColorBackgroundFill(Paleta.FundoPainel);
            r.LineNumberMarginBackgroundFill = new SolidColorBackgroundFill(fundo);
            r.LineNumberMarginDisabledBackgroundFill = new SolidColorBackgroundFill(Paleta.FundoPainel);
            r.LineNumberMarginForeColor = Paleta.Misturar(Paleta.Texto, fundo, 0.42);
            r.LineNumberMarginBorderColor = fundo;
            r.IndicatorMarginBackgroundFill = new SolidColorBackgroundFill(margem);
            r.IndicatorMarginBorderColor = Paleta.Linha;
            r.SelectionMarginBackgroundFill = new SolidColorBackgroundFill(fundo);
            r.SelectionMarginDisabledBackgroundFill = new SolidColorBackgroundFill(Paleta.FundoPainel);
            r.SelectionMarginBorderColor = fundo;
            r.UserMarginBackgroundFill = new SolidColorBackgroundFill(margem);
            r.UserMarginDisabledBackgroundFill = new SolidColorBackgroundFill(Paleta.FundoPainel);
            r.UserMarginForeColor = Paleta.TextoApagado;
            r.UserMarginBorderColor = Paleta.Linha;
            r.WordWrapMarginBackgroundFill = new SolidColorBackgroundFill(fundo);
            r.WordWrapMarginBorderColor = fundo;
            r.SplitterBackgroundFill = new SolidColorBackgroundFill(Paleta.FundoPainel);
            r.ScrollBarBlockBackgroundFill = new SolidColorBackgroundFill(Paleta.FundoPainel);
            r.SelectionFocusedBackColor = Paleta.Selecao;
            r.SelectionFocusedBorderColor = Paleta.Selecao;
            r.SelectionUnfocusedBackColor = Paleta.Somar(fundo, 28);
            r.SelectionUnfocusedBorderColor = Paleta.Somar(fundo, 28);
            r.CurrentLineHighlightBackColor = Paleta.Somar(fundo, 12);
            r.CurrentLineHighlightBorderColor = Paleta.Somar(fundo, 26);
            r.IndentationGuideColor = Paleta.Somar(fundo, 34);
            r.OutliningIndicatorBackColor = fundo;
            r.OutliningIndicatorForeColor = Paleta.TextoApagado;
            r.WhitespaceSpaceForeColor = Paleta.Somar(fundo, 45);
            r.WhitespaceTabForeColor = Paleta.Somar(fundo, 45);
            r.WordWrapGlyphColor = Paleta.TextoApagado;
            r.ContentDividerColor = Paleta.Borda;
            return r;
        }

    }

    /// <summary>
    /// O editor desenha as PROPRIAS barras de rolagem pelo renderizador dele (nao pelos
    /// controles ScrollBar - medido no IDE em 28/10: continuavam brancas). Herda do
    /// renderizador VS2005 e repassa so as barras para o desenho escuro.
    /// </summary>
    internal sealed class RendererDoEditor : VisualStudio2005SyntaxEditorRenderer
    {
        private static readonly RolagemEscura _rol = new RolagemEscura();
        public override void DrawScrollBarBackground(PaintEventArgs e, Rectangle bounds, ActiproSoftware.WinUICore.ScrollBar scrollBar) { _rol.DrawScrollBarBackground(e, bounds, scrollBar); }
        public override void DrawScrollBarButton(PaintEventArgs e, Rectangle bounds, ActiproSoftware.WinUICore.ScrollBarButton button) { _rol.DrawScrollBarButton(e, bounds, button); }
        public override void DrawScrollBarThumb(PaintEventArgs e, Rectangle bounds, ActiproSoftware.WinUICore.ScrollBarThumb thumb) { _rol.DrawScrollBarThumb(e, bounds, thumb); }
    }

    /// <summary>
    /// Barras de rolagem do editor: a Actipro desenha as suas (WindowsXPScrollBarRenderer,
    /// claras). Esta desenha no estilo do Visual Studio escuro: trilho na cor do fundo,
    /// polegar cinza arredondado, setas discretas.
    /// </summary>
    internal sealed class RolagemEscura : ActiproSoftware.WinUICore.ScrollBarRenderer
    {
        public override void DrawScrollBarBackground(PaintEventArgs e, Rectangle bounds, ActiproSoftware.WinUICore.ScrollBar scrollBar)
        {
            using (var b = new SolidBrush(Paleta.RolagemTrilho)) e.Graphics.FillRectangle(b, bounds);
        }

        public override void DrawScrollBarThumb(PaintEventArgs e, Rectangle bounds, ActiproSoftware.WinUICore.ScrollBarThumb thumb)
        {
            bool vertical = thumb.ScrollBar != null && thumb.ScrollBar.Orientation == Orientation.Vertical;
            DesenhoRolagem.Polegar(e.Graphics, bounds, vertical, thumb.IsMouseCaptured ? Paleta.RolagemPolegarAtivo : Paleta.RolagemPolegar);
        }

        public override void DrawScrollBarButton(PaintEventArgs e, Rectangle bounds, ActiproSoftware.WinUICore.ScrollBarButton button)
        {
            using (var b = new SolidBrush(Paleta.RolagemTrilho)) e.Graphics.FillRectangle(b, bounds);
            var sb = button.ScrollBar;
            if (sb == null) return;
            bool vertical = sb.Orientation == Orientation.Vertical;
            // Qual ponta: a seta aponta para o lado em que o botao esta.
            bool inicio = vertical ? bounds.Top + bounds.Height / 2 < sb.Height / 2 : bounds.Left + bounds.Width / 2 < sb.Width / 2;
            DesenhoRolagem.Seta(e.Graphics, bounds, vertical, inicio, button.IsMouseCaptured ? Paleta.Texto : Paleta.TextoApagado);
        }
    }

}
