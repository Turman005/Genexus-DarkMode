// Xpz.Dark - censo: que controles existem em cada janela, e de que familia.

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
    //  Censo: que controles existem em cada janela, e de que familia
    // =====================================================================
    /// <summary>
    /// Para identificar o que ainda fica claro - em especial as abas das PARTES do
    /// objeto (Web Layout / Rules / Events...), que o diagnostico da fase 1 nao
    /// achou como Infragistics, e a barra de status. So LE. Vai para dark-censo.log.
    /// </summary>
    internal static class Censo
    {
        public static void Executar()
        {
            string arq = Path.Combine(Config.Pasta(), "dark-censo.log");
            try
            {
                using (var w = new StreamWriter(arq, false))
                {
                    w.WriteLine("censo " + DateTime.Now.ToString("dd/MM HH:mm:ss"));
                    var janelas = new List<Form>();
                    foreach (Form f in Application.OpenForms) if (!Fase2.OutraThread(f)) janelas.Add(f);

                    var global = new Dictionary<string, int>();
                    bool arvoreDocFeita = false;
                    foreach (Form f in janelas)
                    {
                        var porTipo = new Dictionary<string, int>();
                        var amostra = new Dictionary<string, string>();
                        Contar(f, porTipo, amostra, 0);
                        w.WriteLine();
                        w.WriteLine("== janela " + f.GetType().FullName + "  \"" + f.Text + "\"");
                        foreach (var kv in porTipo.OrderByDescending(x => x.Value).Take(40))
                        {
                            w.WriteLine(string.Format("   {0,5}  {1}   {2}", kv.Value, kv.Key, amostra[kv.Key]));
                            int g; global.TryGetValue(kv.Key, out g); global[kv.Key] = g + kv.Value;
                        }
                        // A arvore de UM documento aberto mostra onde estao as abas das partes.
                        if (!arvoreDocFeita && f.GetType().Name == "DocumentMdiView")
                        {
                            arvoreDocFeita = true;
                            w.WriteLine("   --- arvore deste documento (ate 6 niveis) ---");
                            Arvore(w, f, 0);
                        }
                    }
                    // O designer de tela, com origem de cada cor (D = definida, h = herdada),
                    // para saber o que e moldura e o que e a pagina desenhada.
                    foreach (Form f in janelas)
                    {
                        Control d = Achar(f, "Artech.Packages.Editors.Web.MultiFormHtmlEditor", 0);
                        if (d == null) continue;
                        w.WriteLine();
                        w.WriteLine("== designer de tela em \"" + f.Text + "\" (ate 7 niveis)");
                        ArvoreDesigner(w, d, 0);
                        break;
                    }

                    w.WriteLine();
                    w.WriteLine("== TOTAL por tipo (todas as janelas)");
                    foreach (var kv in global.OrderByDescending(x => x.Value).Take(60))
                        w.WriteLine(string.Format("   {0,6}  {1}", kv.Value, kv.Key));
                }
                Log.Linha("censo gravado em dark-censo.log");
            }
            catch (Exception ex) { Log.Linha("censo FALHOU: " + Log.Curto(ex)); }
        }

        private static Control Achar(Control c, string tipo, int nivel)
        {
            if (c == null || nivel > 20) return null;
            if (c.GetType().FullName == tipo) return c;
            Control[] filhos;
            try { filhos = new Control[c.Controls.Count]; c.Controls.CopyTo(filhos, 0); } catch { return null; }
            foreach (Control f in filhos) { Control r = Achar(f, tipo, nivel + 1); if (r != null) return r; }
            return null;
        }

        private static void ArvoreDesigner(StreamWriter w, Control c, int nivel)
        {
            if (c == null || nivel > 7) return;
            w.WriteLine(string.Format("   {0}{1}  [{2}x{3}] dock={4} fundo={5}{6} texto={7}{8}{9}", new string(' ', nivel * 2),
                c.GetType().FullName, c.Width, c.Height, c.Dock,
                Cor(c.BackColor), Fase2.Explicito(c, true) ? "(D)" : "(h)",
                Cor(c.ForeColor), Fase2.Explicito(c, false) ? "(D)" : "(h)",
                string.IsNullOrEmpty(c.Text) ? "" : "  \"" + (c.Text.Length > 25 ? c.Text.Substring(0, 25) : c.Text) + "\""));
            Control[] filhos;
            try { filhos = new Control[c.Controls.Count]; c.Controls.CopyTo(filhos, 0); } catch { return; }
            foreach (Control f in filhos.Take(12)) ArvoreDesigner(w, f, nivel + 1);
        }

        /// <summary>Janela aberta depois do censo (dialogo de Opcoes etc.): acrescenta.</summary>
        public static void Janela(Form f)
        {
            try
            {
                using (var w = new StreamWriter(Path.Combine(Config.Pasta(), "dark-censo.log"), true))
                {
                    var porTipo = new Dictionary<string, int>();
                    var amostra = new Dictionary<string, string>();
                    Contar(f, porTipo, amostra, 0);
                    w.WriteLine();
                    w.WriteLine("== janela TARDIA " + DateTime.Now.ToString("HH:mm:ss") + " " + f.GetType().FullName + "  \"" + f.Text + "\"");
                    foreach (var kv in porTipo.OrderByDescending(x => x.Value).Take(40))
                        w.WriteLine(string.Format("   {0,5}  {1}   {2}", kv.Value, kv.Key, amostra[kv.Key]));
                    w.WriteLine("   --- arvore (ate 6 niveis) ---");
                    Arvore(w, f, 0);
                }
            }
            catch (Exception ex) { Log.Linha("censo tardio FALHOU: " + Log.Curto(ex)); }
        }

        internal static string Familia(Type t)
        {
            for (Type b = t; b != null; b = b.BaseType)
            {
                string n = b.FullName ?? "";
                if (n.StartsWith("Infragistics.")) return "[Infragistics]";
                if (n.StartsWith("ActiproSoftware.")) return "[Actipro]";
                if (n.StartsWith("DevExpress.")) return "[DevExpress]";
            }
            // primeiro ancestral WinForms, para saber do que um controle do GeneXus deriva
            for (Type b = t; b != null; b = b.BaseType)
                if ((b.FullName ?? "").StartsWith("System.Windows.Forms."))
                    return b == t ? "[WinForms]" : "[deriva de " + b.Name + "]";
            return "[?]";
        }

        private static void Contar(Control c, Dictionary<string, int> porTipo, Dictionary<string, string> amostra, int nivel)
        {
            if (c == null || nivel > 30) return;
            string k = c.GetType().FullName;
            int n; porTipo.TryGetValue(k, out n); porTipo[k] = n + 1;
            if (!amostra.ContainsKey(k))
                amostra[k] = Familia(c.GetType()) + string.Format("  fundo={0} texto={1}", Cor(c.BackColor), Cor(c.ForeColor));
            Control[] filhos;
            try { filhos = new Control[c.Controls.Count]; c.Controls.CopyTo(filhos, 0); } catch { return; }
            foreach (Control f in filhos) Contar(f, porTipo, amostra, nivel + 1);
        }

        private static void Arvore(StreamWriter w, Control c, int nivel)
        {
            if (c == null || nivel > 6) return;
            w.WriteLine(string.Format("   {0}{1}  {2}  [{3}x{4}]  fundo={5}{6}", new string(' ', nivel * 2), c.GetType().FullName,
                Familia(c.GetType()), c.Width, c.Height, Cor(c.BackColor),
                string.IsNullOrEmpty(c.Text) ? "" : "  \"" + (c.Text.Length > 30 ? c.Text.Substring(0, 30) : c.Text) + "\""));
            Control[] filhos;
            try { filhos = new Control[c.Controls.Count]; c.Controls.CopyTo(filhos, 0); } catch { return; }
            foreach (Control f in filhos.Take(25)) Arvore(w, f, nivel + 1);
        }

        internal static string Cor(Color c)
        {
            if (c.IsEmpty) return "-";
            return c.IsNamedColor ? c.Name : string.Format("{0},{1},{2}", c.R, c.G, c.B);
        }
    }
}
