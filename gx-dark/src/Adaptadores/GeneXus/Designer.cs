// Xpz.Dark - designer de tela (Web Layout): fica como o GeneXus desenha.

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
    //  Designer de tela (Web Layout): fica como o GeneXus desenha
    // =====================================================================
    /// <summary>
    /// O Web Layout e o DESENHO da tela do usuario: label, campo, grid, grupo. Na
    /// 1a versao a fase 2 escurecia esses controles, o GeneXus repunha o fundo azul
    /// claro dele e o texto (ja claro) sumia. Agora:
    ///   - nada dentro do designer e pintado;
    ///   - a raiz recebe de volta o texto de fabrica (preto), senao herdaria o
    ///     texto claro dos paineis escuros em volta;
    ///   - com FundoWebLayout, so a MOLDURA (o que fica em volta da pagina) escurece,
    ///     e cada filho direto dela recebe a cor de fabrica, para a pagina nao herdar
    ///     o escuro.
    /// </summary>
    internal static class Designer
    {
        public static bool Eh(Type t)
        {
            string n = t.FullName ?? "";
            // TabControlDesigner e o Properties de um controle do Web Layout (aba "General",
            // caixa "Filter") - e do IDE, nao da tela desenhada (28/10).
            if (n == "Artech.Patterns.WorkWithDevices.Editor.TabControlDesigner") return false;
            return n.StartsWith("Artech.Packages.Editors.Web.", StringComparison.Ordinal) ||
                   n.StartsWith("Artech.Patterns.WorkWithDevices.Editor.", StringComparison.Ordinal) ||
                   n.StartsWith("System.Windows.Forms.Design.", StringComparison.Ordinal);
        }

        /// <summary>So o que fica EM VOLTA da pagina. A pagina e o resto ficam de fabrica.</summary>
        private static bool EhMoldura(Control c)
        {
            string n = c.GetType().FullName;
            // Medido no censo de 27/10: o branco em volta da pagina e o LayoutMainPanel
            // (branco DEFINIDO) e o DesignerFrame/OverlayControl (herdam dele). A pagina
            // (RootForm) tem cor propria e fica.
            return n == "Artech.Packages.Editors.Web.MultiFormHtmlEditor" ||
                   n == "Artech.Patterns.WorkWithDevices.Editor.WebLayoutEditor" ||
                   n == "Artech.Patterns.WorkWithDevices.Editor.LayoutMainPanel" ||
                   n == "Artech.Patterns.WorkWithDevices.Editor.ActionBar" ||
                   n == "System.Windows.Forms.Design.DesignerFrame" ||
                   n == "System.Windows.Forms.Design.DesignerFrame+OverlayControl";
        }

        public static void Raiz(Control r)
        {
            Fase2.GuardarOriginal(r);
            if (!Fase2.Explicito(r, false)) r.ForeColor = Fase2.TextoDeFabrica(r.Parent ?? r);
            if (Fase2.FundoWebLayout && EhMoldura(r)) Moldura(r);
            else if (!Fase2.Explicito(r, true)) r.BackColor = Fase2.FundoDeFabrica(r.Parent ?? r);
            Log.Linha("designer de tela preservado: " + r.GetType().Name + (Fase2.FundoWebLayout ? " (moldura escura)" : ""));
        }

        private static void Moldura(Control m)
        {
            Fase2.MarcarVisto(m);
            Fase2.GuardarOriginal(m);
            Color fabrica = Fase2.FundoDeFabrica(m);
            // A barra de acoes ("<No action group selected>") e cinza AppWorkspace com
            // texto branco: vira o cinza escuro de botao, o texto continua legivel.
            if (m.GetType().Name == "ActionBar") m.BackColor = Paleta.FundoElevado;
            else if (Paleta.FundoClaro(fabrica) || !Fase2.Explicito(m, true)) m.BackColor = Paleta.FundoPainel;
            Fase2.RolagemEscuraQuandoPuder(m);   // barras de rolagem do Web Layout

            Control[] filhos;
            try { filhos = new Control[m.Controls.Count]; m.Controls.CopyTo(filhos, 0); } catch { filhos = new Control[0]; }
            foreach (Control f in filhos) Filho(f, fabrica);
            ControlEventHandler h = delegate(object s, ControlEventArgs e) { try { Filho(e.Control, fabrica); } catch { } };
            m.ControlAdded += h;
        }

        private static void Filho(Control f, Color fabricaDoPai)
        {
            if (Fase2.JaVisto(f)) return;
            if (EhMoldura(f)) { Moldura(f); return; }
            // Barra de navegacao (TagNavigator), faixa de ferramentas: sao do IDE, nao da
            // tela desenhada - tratamento normal da fase 2.
            if (!Eh(f.GetType())) { Fase2.Processar(f); return; }
            Fase2.MarcarVisto(f);
            // A pagina: se herdava a cor do pai, recebe a cor que o pai tinha de fabrica.
            if (!Fase2.Explicito(f, true)) f.BackColor = fabricaDoPai;
        }
    }
}
