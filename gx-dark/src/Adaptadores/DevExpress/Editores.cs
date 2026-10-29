// Xpz.Dark - caixas e paineis DevExpress (seletor de revisao do comparador, divisorias).

using System;
using System.Drawing;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace Xpz.Dark
{
    internal static partial class Grades
    {
        // ------------------------------------------- DevExpress: caixas (ComboBoxEdit...)
        public static bool EhCaixaDx(Control c) { return Familia.Deriva(c.GetType(), "DevExpress.XtraEditors.BaseEdit"); }

        /// <summary>Paineis DevExpress que ficam atras das arvores do comparador (auditoria 28/10:
        /// SplitContainerControl fundo=Window, XtraScrollableControl fundo=Control).</summary>
        public static bool EhPainelDx(Control c)
        {
            Type t = c.GetType();
            return Familia.Deriva(t, "DevExpress.XtraEditors.SplitContainerControl") ||
                   Familia.Deriva(t, "DevExpress.XtraEditors.XtraScrollableControl") ||
                   Familia.Deriva(t, "DevExpress.XtraEditors.PanelControl");
        }

        /// <summary>
        /// A cor da caixa mora em Properties.Appearance* (o ComboBoxEdit do seletor de revisao
        /// saia com fundo Window). Aparencias que nao existirem nesta versao sao puladas - uma
        /// propriedade ausente nao pode derrubar a parte "grades" inteira.
        /// </summary>
        private static bool CaixaDx(Control c)
        {
            bool m = Plano(Valor(c, "LookAndFeel"));
            object p = Valor(c, "Properties");
            if (p == null) return m;
            m |= ApSe(p, "Appearance", Paleta.FundoEntrada, Paleta.Texto, Paleta.Borda);
            m |= ApSe(p, "AppearanceFocused", Paleta.FundoEntrada, Paleta.Texto, Paleta.Destaque);
            m |= ApSe(p, "AppearanceReadOnly", Paleta.FundoEntrada, Paleta.Texto, Paleta.Borda);
            m |= ApSe(p, "AppearanceDisabled", Paleta.FundoPainel, Paleta.TextoApagado, Paleta.Borda);
            m |= ApSe(p, "AppearanceDropDown", Paleta.FundoElevado, Paleta.Texto, Paleta.Borda);
            return m;
        }

        // Paineis escurecidos por nos: na troca de tema recebem a cor nova (ja nao estao claros).
        private static readonly ConditionalWeakTable<Control, object> _paineisDx = new ConditionalWeakTable<Control, object>();

        private static bool PainelDx(Control c)
        {
            object x;
            object ap = Valor(c, "Appearance");
            Color atual = ap != null && !((Color)((dynamic)ap).BackColor).IsEmpty ? (Color)((dynamic)ap).BackColor : c.BackColor;
            bool nosso = _paineisDx.TryGetValue(c, out x);
            if (!nosso && !Paleta.FundoClaro(atual)) return false;   // cor propria escolhida: fica
            if (!nosso) _paineisDx.Add(c, new object());
            bool m = Plano(Valor(c, "LookAndFeel"));
            if (ap != null) m |= Ap(ap, Paleta.FundoEntrada, Color.Empty, Color.Empty);
            m |= Cor(c, "BackColor", Paleta.FundoEntrada);
            return m;
        }

        /// <summary>Visual "Flat": com o estilo do Windows/Office o DevExpress ignora parte das cores.</summary>
        private static bool Plano(object lookAndFeel)
        {
            if (lookAndFeel == null) return false;
            dynamic lf = lookAndFeel;
            bool m = false;
            if ((bool)lf.UseDefaultLookAndFeel) { lf.UseDefaultLookAndFeel = false; m = true; }
            if ((bool)lf.UseWindowsXPTheme) { lf.UseWindowsXPTheme = false; m = true; }
            object estilo = lf.Style;
            if (estilo.ToString() != "Flat") { lf.Style = (dynamic)Enum.Parse(estilo.GetType(), "Flat"); m = true; }
            return m;
        }

        private static bool ApSe(object alvo, string nome, Color fundo, Color texto, Color borda)
        {
            object ap = Valor(alvo, nome);
            return ap != null && Ap(ap, fundo, texto, borda);
        }

        /// <summary>A propriedade do tipo mais derivado: o DevExpress redeclara "Properties" com
        /// "new" a cada nivel (ComboBoxEdit), e a busca so por nome da AmbiguousMatchException.</summary>
        private static object Valor(object alvo, string nome)
        {
            for (Type t = alvo.GetType(); t != null; t = t.BaseType)
            {
                PropertyInfo p = t.GetProperty(nome, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (p != null && p.GetIndexParameters().Length == 0) return p.GetValue(alvo, null);
            }
            return null;
        }
    }
}
