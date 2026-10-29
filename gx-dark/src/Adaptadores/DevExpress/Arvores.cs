// Xpz.Dark - arvores DevExpress (Variables, Structure).

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
    internal static partial class Grades
    {
        // ------------------------------------------- DevExpress TreeList
        private static bool TreeList(Control tl)
        {
            bool m = false;
            dynamic t = tl;

            // Com o estilo do Windows/Office2003 o DevExpress ignora parte das cores.
            dynamic lf = t.LookAndFeel;
            if ((bool)lf.UseDefaultLookAndFeel) { lf.UseDefaultLookAndFeel = false; m = true; }
            if ((bool)lf.UseWindowsXPTheme) { lf.UseWindowsXPTheme = false; m = true; }
            object estilo = lf.Style;
            if (estilo.ToString() != "Flat") { lf.Style = (dynamic)Enum.Parse(estilo.GetType(), "Flat"); m = true; }

            dynamic a = t.Appearance;
            Color linhaSel = Paleta.Selecao, semFoco = Paleta.SelecaoSemFoco;
            m |= Ap(a.Empty, Paleta.FundoEntrada, Color.Empty, Color.Empty);
            m |= Ap(a.Row, Paleta.FundoEntrada, Paleta.Texto, Color.Empty);
            m |= Ap(a.EvenRow, Paleta.FundoEntrada, Paleta.Texto, Color.Empty);
            m |= Ap(a.OddRow, Paleta.FundoEntrada, Paleta.Texto, Color.Empty);
            m |= Ap(a.FocusedRow, linhaSel, Color.White, Color.Empty);
            m |= Ap(a.FocusedCell, linhaSel, Color.White, Color.Empty);
            m |= Ap(a.SelectedRow, linhaSel, Color.White, Color.Empty);
            m |= Ap(a.HideSelectionRow, semFoco, Paleta.Texto, Color.Empty);
            m |= Ap(a.HeaderPanel, Paleta.FundoPainel, Paleta.Texto, Paleta.Borda);
            m |= Ap(a.FooterPanel, Paleta.FundoPainel, Paleta.Texto, Paleta.Borda);
            m |= Ap(a.GroupFooter, Paleta.FundoPainel, Paleta.Texto, Paleta.Borda);
            m |= Ap(a.GroupButton, Paleta.FundoPainel, Paleta.Texto, Paleta.Borda);
            m |= Ap(a.Preview, Paleta.FundoEntrada, Paleta.TextoApagado, Color.Empty);
            m |= Ap(a.HorzLine, Paleta.Linha, Color.Empty, Color.Empty);
            m |= Ap(a.VertLine, Paleta.Linha, Color.Empty, Color.Empty);
            m |= Ap(a.TreeLine, Paleta.Borda, Paleta.Borda, Color.Empty);
            m |= Cor(tl, "BackColor", Paleta.FundoEntrada);
            m |= Cor(tl, "ForeColor", Paleta.Texto);
            // menus da arvore (cabecalho de coluna, botao direito) no visual escuro
            m |= MenusDevExpress.Ligar(tl);
            m |= Dica(tl);
            return m;
        }

        /// <summary>
        /// Dica (tooltip) ao parar o mouse num no da arvore: a janela do DevExpress usa o
        /// fundo amarelo-claro do sistema (Info). As cores ficam no ToolTipController - o da
        /// arvore, ou o padrao do DevExpress quando ela nao tem um proprio.
        /// </summary>
        private static bool Dica(Control tl)
        {
            object ctrl = null;
            PropertyInfo p = tl.GetType().GetProperty("ToolTipController");
            if (p != null) ctrl = p.GetValue(tl, null);
            if (ctrl == null)
            {
                Type t = tl.GetType().Assembly.GetType("DevExpress.Utils.ToolTipController", false);
                if (t == null)
                    foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                        if ((t = asm.GetType("DevExpress.Utils.ToolTipController", false)) != null) break;
                PropertyInfo d = t == null ? null : t.GetProperty("DefaultController", BindingFlags.Static | BindingFlags.Public);
                if (d != null) ctrl = d.GetValue(null, null);
            }
            if (ctrl == null) return false;
            dynamic c = ctrl;
            bool m = Ap(c.Appearance, Paleta.FundoElevado, Paleta.Texto, Paleta.Borda);
            m |= Ap(c.AppearanceTitle, Paleta.FundoElevado, Paleta.Texto, Paleta.Borda);
            return m;
        }

        /// <summary>Uma AppearanceObject do DevExpress: cor + a opcao Use* que a liga.</summary>
        private static bool Ap(dynamic ap, Color fundo, Color texto, Color borda)
        {
            bool m = false;
            if (!fundo.IsEmpty)
            {
                if (!Familia.Igual((Color)ap.BackColor, fundo)) { ap.BackColor = fundo; m = true; }
                if (!((Color)ap.BackColor2).IsEmpty) { ap.BackColor2 = Color.Empty; m = true; }
                if (!(bool)ap.Options.UseBackColor) { ap.Options.UseBackColor = true; m = true; }
            }
            if (!texto.IsEmpty)
            {
                if (!Familia.Igual((Color)ap.ForeColor, texto)) { ap.ForeColor = texto; m = true; }
                if (!(bool)ap.Options.UseForeColor) { ap.Options.UseForeColor = true; m = true; }
            }
            if (!borda.IsEmpty)
            {
                if (!Familia.Igual((Color)ap.BorderColor, borda)) { ap.BorderColor = borda; m = true; }
                if (!(bool)ap.Options.UseBorderColor) { ap.Options.UseBorderColor = true; m = true; }
            }
            return m;
        }
    }
}
