// Xpz.Dark - diagnostico da moldura (de onde vem a cor de cada componente Infragistics).

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
    //  Diagnostico: de onde a moldura tira a cor, perguntado aos objetos vivos
    // =====================================================================
    /// <summary>
    /// A fase 1 provou que a tabela global Office2007 nao manda na moldura: ficou
    /// Black a sessao inteira e nada mudou. Analise estatica de fora ja errou duas
    /// vezes. Aqui, dentro do processo, cada gerenciador do Infragistics responde
    /// qual estilo usa e quais aparencias o GeneXus preencheu, com os valores.
    ///
    /// So LE. Aparencia do Infragistics e criada sob demanda ao ser lida - por isso
    /// consulta antes o "Has&lt;Nome&gt;" correspondente e so le a que ja existe.
    /// Ler uma vazia criaria um objeto vazio, inofensivo, mas mudaria o "Has".
    /// </summary>
    internal static class Diagnostico
    {
        private static readonly string[] Interessantes =
        {
            "DockManager", "TabbedMdiManager", "ToolbarsManager", "UltraTree", "UltraGrid",
            "ExplorerBar", "StatusBar", "UltraTabControl", "UltraTabStripControl", "UltraPanel",
        };
        private static StreamWriter _w;
        private static int _linhas;
        private const int MAX_LINHAS = 2500;

        public static void Executar()
        {
            string arq = Path.Combine(Config.Pasta(), "dark-diagnostico.log");
            try
            {
                using (_w = new StreamWriter(arq, false))
                {
                    _linhas = 0;
                    Escrever("diagnostico " + DateTime.Now.ToString("dd/MM HH:mm:ss"));
                    var vistos = new HashSet<object>(new PorReferencia());
                    var achados = new List<KeyValuePair<object, string>>();

                    var janelas = new List<Form>();
                    foreach (Form f in Application.OpenForms) if (!Fase2.OutraThread(f)) janelas.Add(f);
                    Escrever("janelas abertas: " + janelas.Count);
                    foreach (Form f in janelas)
                    {
                        Escrever("  janela " + f.GetType().FullName + "  \"" + f.Text + "\"");
                        foreach (object o in CamposDe(f)) Considerar(o, f, vistos, achados);
                        object cont = LerCampo(f, "components");
                        if (cont is System.ComponentModel.IContainer)
                            foreach (System.ComponentModel.IComponent c in ((System.ComponentModel.IContainer)cont).Components)
                                Considerar(c, f, vistos, achados);
                        Arvore(f, f, vistos, achados, 0);
                    }

                    Escrever("");
                    Escrever("componentes Infragistics de moldura encontrados: " + achados.Count);
                    foreach (var kv in achados.Take(60))
                    {
                        object o = kv.Key;
                        Escrever("");
                        Escrever("== " + Cadeia(o.GetType()) + "   em " + kv.Value);
                        Sondar(o, "   ", 0);
                        if (TemNaCadeia(o.GetType(), "UltraDockManager")) Paineis(o);
                        if (_linhas > MAX_LINHAS) { Escrever("... corte: limite de linhas"); break; }
                    }
                }
                Log.Linha("diagnostico gravado em dark-diagnostico.log");
            }
            catch (Exception ex) { Log.Linha("diagnostico FALHOU: " + Log.Curto(ex)); }
            finally { _w = null; }
        }

        private static void Considerar(object o, Form f, HashSet<object> vistos, List<KeyValuePair<object, string>> achados)
        {
            if (o == null || !vistos.Add(o)) return;
            Type t = o.GetType();
            if (t.IsValueType || t == typeof(string)) return;
            if (!TemNaCadeia(t, "Infragistics.")) return;
            foreach (string n in Interessantes)
                for (Type b = t; b != null; b = b.BaseType)
                    if (b.Name.IndexOf(n, StringComparison.Ordinal) >= 0)
                    {
                        achados.Add(new KeyValuePair<object, string>(o, f.GetType().Name));
                        return;
                    }
        }

        private static void Arvore(Control c, Form f, HashSet<object> vistos, List<KeyValuePair<object, string>> achados, int nivel)
        {
            if (c == null || nivel > 15) return;
            Considerar(c, f, vistos, achados);
            var filhos = new Control[c.Controls.Count];
            c.Controls.CopyTo(filhos, 0);
            foreach (Control x in filhos) Arvore(x, f, vistos, achados, nivel + 1);
        }

        /// <summary>Estilos, interruptores de tema e aparencias preenchidas.</summary>
        private static void Sondar(object o, string recuo, int prof)
        {
            PropertyInfo[] props;
            try { props = o.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance); }
            catch { return; }

            foreach (PropertyInfo p in props.OrderBy(x => x.Name))
            {
                if (_linhas > MAX_LINHAS) return;
                if (!p.CanRead || p.GetIndexParameters().Length > 0) continue;
                Type pt = p.PropertyType;
                try
                {
                    if (pt.IsEnum && (p.Name.EndsWith("Style") || p.Name == "UseOsThemes" || p.Name == "UseAppStyling" ||
                                      p.Name == "UseFlatMode" || p.Name == "ColorScheme"))
                    {
                        Escrever(recuo + p.Name + " = " + p.GetValue(o, null));
                    }
                    else if (pt == typeof(bool) && (p.Name == "UseAppStyling" || p.Name == "UseOsThemes"))
                    {
                        Escrever(recuo + p.Name + " = " + p.GetValue(o, null));
                    }
                    else if (!pt.IsValueType && pt.Name.IndexOf("Appearance", StringComparison.Ordinal) >= 0 &&
                             pt.Name.IndexOf("Collection", StringComparison.Ordinal) < 0)
                    {
                        PropertyInfo has = o.GetType().GetProperty("Has" + p.Name, BindingFlags.Public | BindingFlags.Instance);
                        if (has != null && has.PropertyType == typeof(bool) && !(bool)has.GetValue(o, null)) continue;
                        object ap = p.GetValue(o, null);
                        string cores = CoresDe(ap);
                        if (cores != null) Escrever(recuo + p.Name + ": " + cores);
                    }
                    else if (prof < 2 && !pt.IsValueType && pt != typeof(string) &&
                             (pt.Name.EndsWith("Settings") || pt.Name.EndsWith("SettingsBase")))
                    {
                        object v = p.GetValue(o, null);
                        if (v != null)
                        {
                            Escrever(recuo + p.Name + "  [" + v.GetType().Name + "]");
                            Sondar(v, recuo + "   ", prof + 1);
                        }
                    }
                }
                catch { }
            }
        }

        /// <summary>Os paineis do docking tem configuracao propria, que vence a geral.</summary>
        private static void Paineis(object dock)
        {
            try
            {
                var panes = dock.GetType().GetProperty("ControlPanes").GetValue(dock, null) as System.Collections.IEnumerable;
                if (panes == null) return;
                int n = 0;
                foreach (object pane in panes)
                {
                    if (++n > 12) { Escrever("   ... (demais paineis omitidos)"); break; }
                    object txt = pane.GetType().GetProperty("Text") != null ? pane.GetType().GetProperty("Text").GetValue(pane, null) : "?";
                    Escrever("   painel \"" + txt + "\"");
                    PropertyInfo s = pane.GetType().GetProperty("Settings");
                    if (s != null) Sondar(s.GetValue(pane, null), "      ", 1);
                }
            }
            catch (Exception ex) { Escrever("   paineis: " + Log.Curto(ex)); }
        }

        private static string CoresDe(object ap)
        {
            if (ap == null) return null;
            var partes = new List<string>();
            foreach (string n in new[] { "BackColor", "BackColor2", "ForeColor", "BorderColor", "BackColorDisabled", "ForeColorDisabled" })
            {
                PropertyInfo p = ap.GetType().GetProperty(n);
                if (p == null || p.PropertyType != typeof(Color)) continue;
                var c = (Color)p.GetValue(ap, null);
                if (!c.IsEmpty) partes.Add(n + "=" + (c.IsNamedColor ? c.Name : string.Format("{0},{1},{2}", c.R, c.G, c.B)));
            }
            PropertyInfo g = ap.GetType().GetProperty("BackGradientStyle");
            if (g != null) { object v = g.GetValue(ap, null); if (v != null && v.ToString() != "Default") partes.Add("Gradiente=" + v); }
            return partes.Count == 0 ? null : string.Join("  ", partes.ToArray());
        }

        private static IEnumerable<object> CamposDe(object o)
        {
            var r = new List<object>();
            for (Type t = o.GetType(); t != null && t != typeof(Form); t = t.BaseType)
                foreach (FieldInfo f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    try { object v = f.GetValue(o); if (v != null) r.Add(v); } catch { }
                }
            return r;
        }

        private static object LerCampo(object o, string nome)
        {
            for (Type t = o.GetType(); t != null; t = t.BaseType)
            {
                FieldInfo f = t.GetField(nome, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (f != null) { try { return f.GetValue(o); } catch { return null; } }
            }
            return null;
        }

        private static bool TemNaCadeia(Type t, string trecho)
        {
            for (Type b = t; b != null; b = b.BaseType)
                if ((b.FullName ?? "").IndexOf(trecho, StringComparison.Ordinal) >= 0) return true;
            return false;
        }

        private static string Cadeia(Type t)
        {
            var nomes = new List<string>();
            for (Type b = t; b != null && b != typeof(object) && nomes.Count < 4; b = b.BaseType) nomes.Add(b.FullName);
            return string.Join("  <  ", nomes.ToArray());
        }

        private static void Escrever(string s)
        {
            _linhas++;
            if (_w != null) _w.WriteLine(s);
        }

        private sealed class PorReferencia : IEqualityComparer<object>
        {
            public new bool Equals(object a, object b) { return ReferenceEquals(a, b); }
            public int GetHashCode(object o) { return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(o); }
        }
    }
}
