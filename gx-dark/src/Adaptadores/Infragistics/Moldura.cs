// Xpz.Dark - moldura: tabelas de cor globais da Infragistics e o .isl do tema.

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
    //  Cores: tabelas globais do Infragistics e o .isl
    // =====================================================================
    internal static class Cores
    {
        private static Assembly _igWin;

        /// <summary>
        /// A Infragistics2.Win no MESMO contexto de carga que o GeneXus usa. Carregar
        /// por caminho (LoadFrom) criaria uma segunda copia, e gravar a tabela
        /// estatica dela nao mudaria nada na copia que o IDE desenha.
        /// </summary>
        private static Assembly IgWin()
        {
            if (_igWin != null) return _igWin;
            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
                if (a.GetName().Name.StartsWith("Infragistics2.Win.v", StringComparison.Ordinal) &&
                    a.GetType("Infragistics.Win.Office2007ColorTable", false) != null)
                    return _igWin = a;

            // Ainda nao carregada: pelo nome exato que a FrameworkDE referencia.
            Assembly fw = Assembly.Load("Artech.FrameworkDE");
            foreach (AssemblyName an in fw.GetReferencedAssemblies())
                if (an.Name.StartsWith("Infragistics2.Win.v", StringComparison.Ordinal) && an.Name.Count(c => c == '.') == 3)
                {
                    Assembly a = Assembly.Load(an);
                    if (a.GetType("Infragistics.Win.Office2007ColorTable", false) != null) return _igWin = a;
                }
            return null;
        }

        /// <summary>Devolve true se precisou mudar alguma tabela.</summary>
        public static bool Aplicar(Dictionary<string, string> cfg, string quando)
        {
            bool m = false;
            m |= Tabela("Infragistics.Win.Office2007ColorTable", Config.Valor(cfg, "Office2007ColorScheme"), quando);
            m |= Tabela("Infragistics.Win.Office2010ColorTable", Config.Valor(cfg, "Office2010ColorScheme"), quando);
            return m;
        }

        private static bool Tabela(string tipo, string alvo, string quando)
        {
            if (string.IsNullOrEmpty(alvo)) return false;
            try
            {
                Assembly ig = IgWin();
                if (ig == null) { Log.Linha(quando + ": Infragistics2.Win nao encontrada"); return false; }
                Type t = ig.GetType(tipo, true);
                PropertyInfo p = t.GetProperty("ColorScheme", BindingFlags.Public | BindingFlags.Static);
                PropertyInfo r = t.GetProperty("ColorSchemeResolved", BindingFlags.Public | BindingFlags.Static);

                object desejado = Enum.Parse(p.PropertyType, alvo, true);
                object atual = p.GetValue(null, null);
                if (Equals(atual, desejado)) return false;

                object resolvidoAntes = r != null ? r.GetValue(null, null) : null;
                p.SetValue(null, desejado, null);
                object resolvidoDepois = r != null ? r.GetValue(null, null) : null;
                Log.Linha(string.Format("{0}: {1}.ColorScheme {2} (resolvido {3}) -> {4} (resolvido {5})",
                    quando, t.Name, atual, resolvidoAntes, desejado, resolvidoDepois));
                return true;
            }
            catch (Exception ex) { Log.Linha(quando + ": " + tipo + " FALHOU: " + Log.Curto(ex)); return false; }
        }

        private static string _islOriginal, _islGerado, _islTema;

        /// <summary>
        /// Carrega o .isl do TEMA ATUAL: gerado na hora a partir do .isl do proprio
        /// GeneXus (GeradorIsl). Se a geracao falhar, cai no tema.isl fixo da pasta de
        /// configuracao (o escuro neutro de 27/10).
        /// </summary>
        public static void CarregarIsl(string quando)
        {
            if (!Disjuntor.Ligada(Parte.Moldura)) return;
            try
            {
                Assembly ig = IgWin();
                Type sm = ig == null ? null : ig.GetType("Infragistics.Win.AppStyling.StyleManager", false);
                if (sm == null) { Log.Linha(quando + ": StyleManager nao encontrado"); return; }

                string texto = null;
                try
                {
                    Tema t = Paleta.TemaAtual;
                    if (_islGerado == null || _islTema != t.Id)
                    {
                        if (_islOriginal == null) _islOriginal = File.ReadAllText(GeradorIsl.Original());
                        var sw = System.Diagnostics.Stopwatch.StartNew();
                        _islGerado = GeradorIsl.Gerar(_islOriginal, t);
                        _islTema = t.Id;
                        Log.Linha(quando + ": .isl do tema " + t.Nome + " gerado em " + sw.ElapsedMilliseconds + " ms");
                    }
                    texto = _islGerado;
                }
                catch (Exception ex) { Log.Linha(quando + ": geracao do .isl FALHOU (" + Log.Curto(ex) + ") - usando tema.isl fixo"); }

                if (texto != null)
                {
                    using (var ms = new MemoryStream(new System.Text.UTF8Encoding(true).GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(texto)).ToArray()))
                        sm.GetMethod("Load", new[] { typeof(Stream) }).Invoke(null, new object[] { ms });
                    Log.Linha(quando + ": .isl do tema carregado");
                    return;
                }
                string isl = Path.Combine(Config.Pasta(), "tema.isl");
                if (!File.Exists(isl)) { Log.Linha(quando + ": sem tema.isl"); return; }
                sm.GetMethod("Load", new[] { typeof(string) }).Invoke(null, new object[] { isl });
                Log.Linha(quando + ": .isl fixo carregado");
            }
            catch (Exception ex) { Log.Linha(quando + ": .isl FALHOU: " + Log.Curto(ex)); }
        }

        public static void Retrato(string quando)
        {
            try
            {
                Assembly ig = IgWin();
                if (ig == null) { Log.Linha(quando + ": Infragistics2.Win ainda nao carregada"); return; }
                foreach (string tn in new[] { "Infragistics.Win.Office2007ColorTable", "Infragistics.Win.Office2010ColorTable" })
                {
                    Type t = ig.GetType(tn, false);
                    if (t == null) continue;
                    object v = t.GetProperty("ColorScheme").GetValue(null, null);
                    PropertyInfo r = t.GetProperty("ColorSchemeResolved");
                    Log.Linha(string.Format("{0}: {1} = {2} (resolvido {3})", quando, t.Name, v,
                        r != null ? r.GetValue(null, null) : "?"));
                }
                Log.Linha(quando + ": janelas abertas = " + Application.OpenForms.Count);
            }
            catch (Exception ex) { Log.Linha(quando + ": retrato FALHOU: " + Log.Curto(ex)); }
        }
    }
}
