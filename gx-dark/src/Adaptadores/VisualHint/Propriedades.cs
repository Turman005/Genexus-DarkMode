// Xpz.Dark - painel Properties e Options (VisualHint SmartPropertyGrid).

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
        // ------------------------------------------- VisualHint SmartPropertyGrid
        private static bool Smart(Control g)
        {
            bool m = false;
            Color categoria = Paleta.CabecalhoGrupo;
            m |= Cor(g, "GridBackColor", Paleta.FundoEntrada);
            m |= Cor(g, "GridForeColor", Paleta.Texto);
            m |= Cor(g, "GridColor", Paleta.Linha);
            m |= Cor(g, "GridDisabledForeColor", Paleta.TextoApagado);
            m |= Cor(g, "GridHighlightColor", Paleta.Realce);
            m |= Cor(g, "GridSelectedHighlightColor", Paleta.Selecao);
            m |= Cor(g, "SelectedBkgColor", Paleta.Selecao);
            m |= Cor(g, "HighlightedTextColor", Color.White);
            m |= Cor(g, "CommentsBackColor", Paleta.FundoPainel);
            m |= Cor(g, "CommentsForeColor", Paleta.Texto);
            m |= Cor(g, "RootCategoryTextColor", Paleta.Link);
            m |= Cor(g, "SubCategoryTextColor", Paleta.Link);
            m |= Cor(g, "BackColor", Paleta.FundoPainel);
            // A grade interna rola com a barra NATIVA do Windows (WS_VSCROLL): tema escuro do
            // Windows nela (auditoria 28/10: faixa clara de 16 px na direita do Properties).
            foreach (Control filho in g.Controls)
                if (filho.GetType().Name == "InternalGrid") Fase2.RolagemEscuraQuandoPuder(filho);

            // Faixa das categorias ("After Build", "Misc"): fica no gerenciador de desenho.
            PropertyInfo pdm = g.GetType().GetProperty("DrawManager");
            object dm = pdm == null ? null : pdm.GetValue(g, null);
            if (dm != null)
            {
                FieldInfo f1 = Campo(dm.GetType(), "_categoryBkgColor1");
                bool precisa = f1 == null || !Familia.Igual((Color)f1.GetValue(dm), categoria);
                if (precisa)
                {
                    MethodInfo cat = dm.GetType().GetMethod("SetCategoryBackgroundColors", new[] { typeof(Color), typeof(Color) });
                    MethodInfo sub = dm.GetType().GetMethod("SetSubCategoryBackgroundColors", new[] { typeof(Color), typeof(Color) });
                    if (cat != null) { cat.Invoke(dm, new object[] { categoria, categoria }); m = true; }
                    if (sub != null) sub.Invoke(dm, new object[] { categoria, categoria });
                }
            }
            return m;
        }

        private static FieldInfo Campo(Type t, string nome)
        {
            for (Type b = t; b != null; b = b.BaseType)
            {
                FieldInfo f = b.GetField(nome, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (f != null) return f;
            }
            return null;
        }
    }
}
