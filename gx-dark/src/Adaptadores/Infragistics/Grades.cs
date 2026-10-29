// Xpz.Dark - UltraGrid (grade de objetos) e barras de rolagem Infragistics.

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
        // ------------------------------------------- Infragistics UltraGrid
        // As linhas vem do .isl (escuras); a AREA VAZIA abaixo delas e a aparencia geral
        // da grade, que o GeneXus poe branca. So troca se estiver clara.
        private static bool UltraGrid(Control g)
        {
            bool m = false;
            dynamic ap = ((dynamic)g).DisplayLayout.Appearance;
            Color fundo = (Color)ap.BackColor;
            if (fundo.IsEmpty || Paleta.FundoClaro(fundo)) { ap.BackColor = Paleta.FundoEntrada; m = true; }
            if (!((Color)ap.BackColor2).IsEmpty && Paleta.FundoClaro((Color)ap.BackColor2)) { ap.BackColor2 = Paleta.FundoEntrada; m = true; }
            if (Paleta.FundoClaro(g.BackColor)) { g.BackColor = Paleta.FundoEntrada; m = true; }
            m |= Rolagem(((dynamic)g).DisplayLayout.ScrollBarLook);
            return m;
        }

        // ------------------------------------------- barra de rolagem Infragistics
        // Estilo Office2010: segue a tabela de cor global (Black, ligada na fase 1) e as
        // aparencias. O Standard parecia o obvio, mas com o tema do Windows ativo ele usa
        // a barra do sistema e ignora as cores (medido 28/10, TesteRolIg).
        private static bool Rolagem(dynamic look)
        {
            if (look == null) return false;
            bool m = false;
            object estilo = look.ViewStyle;
            if (estilo.ToString() != "Office2010") { look.ViewStyle = (dynamic)Enum.Parse(estilo.GetType(), "Office2010"); m = true; }
            Color trilho = Paleta.RolagemTrilho, polegar = Paleta.RolagemPolegar;
            m |= ApIg(look.Appearance, trilho, Paleta.TextoApagado, trilho);
            m |= ApIg(look.TrackAppearance, trilho, Color.Empty, trilho);
            m |= ApIg(look.ButtonAppearance, trilho, Paleta.TextoApagado, trilho);
            m |= ApIg(look.ThumbAppearance, polegar, Color.Empty, polegar);
            return m;
        }

        private static bool ApIg(dynamic ap, Color fundo, Color texto, Color borda)
        {
            bool m = false;
            if (!Familia.Igual((Color)ap.BackColor, fundo)) { ap.BackColor = fundo; m = true; }
            if (!((Color)ap.BackColor2).IsEmpty) { ap.BackColor2 = Color.Empty; m = true; }
            if (!texto.IsEmpty && !Familia.Igual((Color)ap.ForeColor, texto)) { ap.ForeColor = texto; m = true; }
            if (!borda.IsEmpty && !Familia.Igual((Color)ap.BorderColor, borda)) { ap.BorderColor = borda; m = true; }
            return m;
        }
    }
}
