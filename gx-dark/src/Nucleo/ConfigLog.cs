// Xpz.Dark - dark.ini, log e repintura.

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
    //  Repintura, configuracao e log
    // =====================================================================
    internal static class Repintar
    {
        public static void Tudo(string quando)
        {
            try
            {
                var janelas = new List<Form>();
                foreach (Form f in Application.OpenForms) if (!Fase2.OutraThread(f)) janelas.Add(f);   // copia: a colecao muda na subida
                foreach (Form f in janelas)
                {
                    try { f.Invalidate(true); f.Update(); } catch { }
                }
                Log.Linha(quando + ": repintadas " + janelas.Count + " janela(s)");
            }
            catch (Exception ex) { Log.Linha(quando + ": repintura FALHOU: " + Log.Curto(ex)); }
        }
    }

    internal static class Config
    {
        public static string Pasta()
        {
            // XPZDARK_PASTA: perfil isolado dos testes (Testar-Dark.ps1) - sem ele, os testes
            // liam o dark.ini do usuario e a foto da tela de configuracao mudava com o tema dele.
            string d = Environment.GetEnvironmentVariable("XPZDARK_PASTA");
            if (string.IsNullOrEmpty(d)) d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Xpz.Dark");
            if (!Directory.Exists(d)) Directory.CreateDirectory(d);
            return d;
        }

        /// <summary>null = sem dark.ini = experimento desligado.</summary>
        public static Dictionary<string, string> Ler()
        {
            string arq = Path.Combine(Pasta(), "dark.ini");
            if (!File.Exists(arq)) return null;
            var cfg = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string linha in File.ReadAllLines(arq))
            {
                string s = linha.Trim();
                if (s.Length == 0 || s.StartsWith("#")) continue;
                int i = s.IndexOf('=');
                if (i <= 0) continue;
                cfg[s.Substring(0, i).Trim()] = s.Substring(i + 1).Trim();
            }
            return cfg;
        }

        public static string Valor(Dictionary<string, string> cfg, string chave)
        {
            string v; return cfg.TryGetValue(chave, out v) ? v : null;
        }

        /// <summary>Como Sim, mas chave AUSENTE conta como ligada. Para chaves novas:
        /// o instalador preserva o dark.ini do usuario, que nao as tera.</summary>
        public static bool SimPadrao(Dictionary<string, string> cfg, string chave)
        {
            return Valor(cfg, chave) == null || Sim(cfg, chave);
        }

        public static bool Sim(Dictionary<string, string> cfg, string chave)
        {
            string v = Valor(cfg, chave);
            return v != null && new[] { "sim", "s", "true", "1", "yes" }.Contains(v.ToLowerInvariant());
        }
    }

    internal static class Log
    {
        public static void Linha(string msg)
        {
            try
            {
                File.AppendAllText(Path.Combine(Config.Pasta(), "dark.log"),
                    DateTime.Now.ToString("HH:mm:ss.fff") + "  " + msg + Environment.NewLine);
            }
            catch { }   // log que derruba o IDE seria pior que nao ter log
        }

        public static string Curto(Exception ex)
        {
            Exception b = ex.GetBaseException();
            return b.GetType().Name + ": " + b.Message;
        }
    }
}
