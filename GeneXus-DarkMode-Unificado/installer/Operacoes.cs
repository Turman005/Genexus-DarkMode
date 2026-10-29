// GxDarkTheme - operacoes do instalador (sem interface): detectar, copiar, catalogo, desinstalar, limpeza legada.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace GxDarkTheme.Instalador
{
    // ---------------------------------------------------------------- operacoes (sem interface)
    public sealed class GxInstalacao
    {
        public string Pasta, Nome, Versao;
        public bool TemNossa, TemXpz;
        public override string ToString() { return Nome + "  (" + Versao + ")  -  " + Pasta + (TemNossa ? "  [ja instalado]" : "") + (TemXpz ? "  [tem Xpz.Dark]" : ""); }
    }

    public static class Ops
    {
        public static Action<string> Log = delegate { };
        // progresso para a interface: (texto da etapa, inicio 0..1, fim 0..1, duracao desconhecida?)
        public static Action<string, double, double, bool> Etapa = delegate { };
        // true = etapa critica (Cancelar desabilitado); false = da para parar depois da etapa atual
        public static Action<bool> Critico = delegate { };
        // pedido de parar: so e atendido ENTRE duas instalacoes do GeneXus (nunca no meio de uma)
        public static volatile bool PedidoParar;
        public const string Dll = Produto.Nome + ".dll";
        public const string Tipo = "GxDarkTheme.DarkThemePackage";
        public static string PastaApp { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Produto.Nome); } }
        public const string Desinstalador = "Desinstalar-GxDarkTheme.exe";
        public static string CfgDir { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"GeneXus\GeneXus\18"); } }
        public static string Settings { get { return Path.Combine(CfgDir, Produto.ArquivoConfig); } }
        public static string CatDir { get { string ov = Environment.GetEnvironmentVariable("GXDARK_CATDIR"); if (!string.IsNullOrEmpty(ov)) return ov; return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"GeneXus\GeneXus\18"); } }
        const string ChaveUninst = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\" + Produto.ChaveDesinstalar;

        public static List<GxInstalacao> Detectar()
        {
            List<GxInstalacao> r = new List<GxInstalacao>();
            HashSet<string> vistas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<string> raizes = new List<string>();
            foreach (Environment.SpecialFolder sf in new Environment.SpecialFolder[] { Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolder.ProgramFiles })
            {
                try { string p = Path.Combine(Environment.GetFolderPath(sf), "GeneXus"); if (Directory.Exists(p)) raizes.Add(p); } catch { }
            }
            foreach (DriveInfo d in DriveInfo.GetDrives())
            {
                try { if (d.DriveType == DriveType.Fixed && d.IsReady) { string p = Path.Combine(d.RootDirectory.FullName, "GeneXus"); if (Directory.Exists(p)) raizes.Add(p); } } catch { }
            }
            string extra = Environment.GetEnvironmentVariable("GXDARK_GXROOT");
            if (!string.IsNullOrEmpty(extra) && Directory.Exists(extra)) raizes.Add(extra);
            foreach (string raiz in raizes)
            {
                string[] dirs;
                try { dirs = Directory.GetDirectories(raiz, "GeneXus18*"); } catch { continue; }
                foreach (string dir in dirs)
                {
                    string exe = Path.Combine(dir, "GeneXus.exe");
                    if (!File.Exists(exe) || !vistas.Add(Path.GetFullPath(dir))) continue;
                    GxInstalacao g = new GxInstalacao { Pasta = dir, Nome = Path.GetFileName(dir) };
                    try { g.Versao = FileVersionInfo.GetVersionInfo(exe).FileVersion; } catch { g.Versao = "?"; }
                    g.TemNossa = File.Exists(Path.Combine(dir, "Packages", Dll));
                    g.TemXpz = File.Exists(Path.Combine(dir, "Packages", Produto.LegadoXpzDll));
                    r.Add(g);
                }
            }
            return r;
        }

        public static List<Process> GeneXusAbertos()
        {
            List<Process> l = new List<Process>();
            foreach (string n in new string[] { "GeneXus", "GeneXus.Server", "GXquery" })
                try { l.AddRange(Process.GetProcessesByName(n)); } catch { }
            List<Process> r = new List<Process>();
            foreach (Process p in l) if (p.ProcessName.Equals("GeneXus", StringComparison.OrdinalIgnoreCase)) r.Add(p);
            return r;
        }

        static Stream Recurso(string nome) { return typeof(Ops).Assembly.GetManifestResourceStream("payload." + nome); }
        public static string Texto(string nome) { using (Stream s = Recurso(nome)) using (StreamReader r = new StreamReader(s, Encoding.UTF8)) return r.ReadToEnd(); }
        static void Extrair(string nome, string destino) { using (Stream s = Recurso(nome)) using (FileStream f = File.Create(destino)) s.CopyTo(f); }

        // ---- catalogo de pacotes do GeneXus (%ProgramData%\GeneXus\GeneXus\18\packages.<versao>.xml) ----
        static IEnumerable<string> Catalogos()
        {
            if (!Directory.Exists(CatDir)) yield break;
            foreach (string f in Directory.GetFiles(CatDir, "packages.*.xml"))
                if (Regex.IsMatch(Path.GetFileName(f), @"^packages\.\d+\.xml$", RegexOptions.IgnoreCase)) yield return f;
        }
        static string LerCat(string f, out bool bom)
        {
            byte[] b = File.ReadAllBytes(f);
            bom = b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF;
            string t = new UTF8Encoding(false).GetString(b);
            return bom ? t.Substring(1) : t;
        }
        static void GravarCat(string f, string t, bool bom)
        {
            string bk = f + ".antes-gxdarktheme.bak";
            if (!File.Exists(bk)) { File.Copy(f, bk); Log("  copia do catalogo guardada: " + bk); }
            File.WriteAllText(f, t, new UTF8Encoding(bom));
        }
        static string PadraoEntrada(string dll) { return @"(?s)<InstalledPackage>(?:(?!</InstalledPackage>).)*?<Assembly>" + Regex.Escape(dll) + @"</Assembly>.*?</InstalledPackage>"; }

        public static bool CatalogoTem(string dll)
        {
            foreach (string f in Catalogos()) { bool bom; if (Regex.IsMatch(LerCat(f, out bom), PadraoEntrada(dll), RegexOptions.IgnoreCase)) return true; }
            return false;
        }

        public static int TirarDoCatalogo(string dll)
        {
            int n = 0;
            foreach (string f in Catalogos())
            {
                try
                {
                    bool bom; string t = LerCat(f, out bom);
                    string novo = Regex.Replace(t, PadraoEntrada(dll), "", RegexOptions.IgnoreCase);
                    if (novo != t) { GravarCat(f, novo, bom); n++; Log("  entrada de " + dll + " retirada de " + Path.GetFileName(f)); }
                }
                catch (Exception ex) { Log("  AVISO: catalogo " + f + ": " + ex.Message); }
            }
            return n;
        }

        // reserva (mesmo metodo do Install-Dark.ps1 do Xpz.Dark): acrescenta a entrada quando o /install nao a criou
        public static bool AcrescentarNoCatalogo()
        {
            bool ok = false;
            foreach (string f in Catalogos())
            {
                try
                {
                    bool bom; string t = LerCat(f, out bom);
                    if (Regex.IsMatch(t, PadraoEntrada(Dll), RegexOptions.IgnoreCase)) { ok = true; continue; }
                    int idx = t.LastIndexOf("</Packages>", StringComparison.Ordinal);
                    if (idx < 0) { Log("  AVISO: formato inesperado em " + Path.GetFileName(f) + " (sem </Packages>); nada alterado"); continue; }
                    string compat = Regex.Match(Path.GetFileName(f), @"\d+").Value;
                    if (compat != Compat.Versao.ToString()) Log("  AVISO: " + Path.GetFileName(f) + " e de outra versao de compatibilidade (" + compat + "); a extensao foi compilada para " + Compat.Versao + ". Recompile com scripts\\Build.ps1 se o GeneXus nao carregar.");
                    string e = "<InstalledPackage><IsUIPackage>true</IsUIPackage><Assembly>" + Dll + "</Assembly><Type>" + Tipo +
                               "</Type><Enabled>true</Enabled><ManualDisabled>false</ManualDisabled><CompatibilityVersion>" + compat + "</CompatibilityVersion></InstalledPackage>";
                    GravarCat(f, t.Substring(0, idx) + e + t.Substring(idx), bom);
                    Log("  entrada acrescentada em " + Path.GetFileName(f));
                    ok = true;
                }
                catch (Exception ex) { Log("  AVISO: catalogo " + f + ": " + ex.Message); }
            }
            return ok;
        }

        public static bool RodarInstall(GxInstalacao g)
        {
            Log("  GeneXus.exe /install (atualiza a lista de pacotes; pode levar 1-2 minutos)...");
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(Path.Combine(g.Pasta, "GeneXus.exe"), "/install") { UseShellExecute = false, WorkingDirectory = g.Pasta };
                using (Process p = Process.Start(psi))
                {
                    if (!p.WaitForExit(6 * 60 * 1000)) { Log("  AVISO: /install passou de 6 minutos; continuando sem esperar (o processo nao foi encerrado)"); return false; }
                    Log("  /install terminou (codigo " + p.ExitCode + ")");
                    return true;
                }
            }
            catch (Exception ex) { Log("  AVISO: /install falhou: " + ex.Message); return false; }
        }

        // ---- limpeza do que versoes antigas deixaram ----
        public static void LimparLegado(GxInstalacao g)
        {
            string pk = Path.Combine(g.Pasta, "Packages");
            foreach (string f in new string[] { Produto.LegadoXpzDll, "Xpz.Dark.pdb", Produto.Nome + ".isl" })
            {
                string p = Path.Combine(pk, f);
                try { if (File.Exists(p)) { File.Delete(p); Log("  removido " + p); } } catch (Exception ex) { Log("  AVISO: nao removeu " + p + ": " + ex.Message); }
            }
        }

        public static void ConfigurarTema(string tema)
        {
            Directory.CreateDirectory(CfgDir);
            List<string> l = new List<string>();
            if (File.Exists(Settings))
                foreach (string raw in File.ReadAllLines(Settings))
                {
                    int i = raw.IndexOf('=');
                    string k = i > 0 ? raw.Substring(0, i).Trim().ToLowerInvariant() : "";
                    if (k == "tema" || k == "enabled" || k == "aberturas_incompletas") continue;
                    l.Add(raw);
                }
            l.Add("tema=" + tema);
            l.Add("enabled=true");
            File.WriteAllLines(Settings, l.ToArray());
            Log("Configuracao: " + Settings + " (tema=" + tema + ")");
        }

        public static string TemaAtualConfigurado()
        {
            try
            {
                if (File.Exists(Settings))
                    foreach (string raw in File.ReadAllLines(Settings)) { int i = raw.IndexOf('='); if (i > 0 && raw.Substring(0, i).Trim().ToLowerInvariant() == "tema") return raw.Substring(i + 1).Trim().ToLowerInvariant(); }
                string ini = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Xpz.Dark\dark.ini");
                if (File.Exists(ini))
                    foreach (string raw in File.ReadAllLines(ini)) { string l = raw.Trim(); int i = l.IndexOf('='); if (i > 0 && l.Substring(0, i).Trim().Equals("Tema", StringComparison.OrdinalIgnoreCase)) { string v = l.Substring(i + 1).Trim(); int c = v.IndexOf(';'); return (c >= 0 ? v.Substring(0, c) : v).Trim().ToLowerInvariant(); } }
            }
            catch { }
            return null;
        }

        // ---- instalar ----
        public static bool Instalar(List<GxInstalacao> alvos, string tema)
        {
            bool tudo = true;
            string app = PastaApp;
            Critico(true);
            Etapa("Preparando a pasta do programa...", 0.0, 0.05, false);
            Directory.CreateDirectory(app);
            foreach (string f in new string[] { "LICENSE.txt", "THIRD-PARTY-NOTICES.md", "NOTICE-Mercury.txt", "LEIA-ME.md" })
                try { Extrair(f, Path.Combine(app, f)); } catch (Exception ex) { Log("AVISO: " + f + ": " + ex.Message); }
            string self = typeof(Ops).Assembly.Location;
            string un = Path.Combine(app, Desinstalador);
            if (!string.Equals(Path.GetFullPath(self), Path.GetFullPath(un), StringComparison.OrdinalIgnoreCase)) File.Copy(self, un, true);
            Log("Pasta do programa: " + app);

            List<string> feitas = new List<string>();
            int total = Math.Max(1, alvos.Count), i = -1;
            foreach (GxInstalacao g in alvos)
            {
                i++;
                double b0 = 0.05 + 0.80 * i / total, sp = 0.80 / total;
                string qual = g.Nome + (alvos.Count > 1 ? " (" + (i + 1) + " de " + alvos.Count + ")" : "");
                if (i > 0 && PedidoParar) { Log(""); Log("Interrompido a pedido: " + (alvos.Count - i) + " instalacao(oes) do GeneXus nao foram alteradas."); tudo = false; break; }
                Critico(true);
                Etapa("Copiando arquivos para " + qual + "...", b0, b0 + sp * 0.10, false);
                Log("");
                Log("== " + g.Nome + " (" + g.Pasta + ")");
                try
                {
                    LimparLegado(g);
                    string destino = Path.Combine(g.Pasta, "Packages", Dll);
                    Extrair(Dll, destino);
                    Log("  copiado " + destino);
                    feitas.Add(g.Pasta);
                }
                catch (Exception ex) { Log("  ERRO ao copiar: " + ex.Message); tudo = false; continue; }
                Etapa("Removendo o Xpz.Dark da lista de pacotes...", b0 + sp * 0.10, b0 + sp * 0.15, false);
                TirarDoCatalogo(Produto.LegadoXpzDll);
                Critico(false);
                Etapa("Registrando no GeneXus: " + qual + "... (pode levar 1-2 minutos)", b0 + sp * 0.15, b0 + sp * 0.95, true);
                RodarInstall(g);
                Critico(true);
                Etapa("Conferindo o registro em " + qual + "...", b0 + sp * 0.95, b0 + sp, false);
                if (CatalogoTem(Dll)) Log("  catalogo de pacotes OK (" + Dll + " registrado)");
                else
                {
                    Log("  o /install nao registrou a extensao: usando o metodo de reserva (acrescentar ao catalogo, com copia)");
                    if (!AcrescentarNoCatalogo()) { Log("  ERRO: catalogo de pacotes nao encontrado em " + CatDir + " (abra o GeneXus uma vez e rode o instalador de novo)"); tudo = false; }
                }
            }
            Critico(true);
            Etapa("Gravando a configuração...", 0.85, 0.90, false);
            File.WriteAllLines(Path.Combine(app, "instalacoes.txt"), feitas.ToArray());
            ConfigurarTema(tema);
            Etapa("Desfazendo ajustes de versões antigas...", 0.90, 0.95, false);
            int n = Legado.Restaurar(Log);
            if (n > 0) Log("Configuracoes da versao antiga do Ativar devolvidas ao original (" + n + " grupo(s))");
            Etapa("Registrando o desinstalador...", 0.95, 1.0, false);
            try { RegistrarDesinstalador(app, un); } catch (Exception ex) { Log("ERRO ao registrar o desinstalador: " + ex.Message); tudo = false; }
            return tudo;
        }

        static void RegistrarDesinstalador(string app, string un)
        {
            using (RegistryKey k = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64).CreateSubKey(ChaveUninst))
            {
                k.SetValue("DisplayName", "GxDarkTheme (tema escuro para GeneXus 18)");
                k.SetValue("DisplayVersion", Produto.Versao);
                k.SetValue("Publisher", "Turman005 e itsFilipe");
                k.SetValue("InstallLocation", app);
                k.SetValue("DisplayIcon", un + ",0");
                k.SetValue("UninstallString", "\"" + un + "\" /uninstall");
                k.SetValue("QuietUninstallString", "\"" + un + "\" /uninstall /quiet");
                k.SetValue("URLInfoAbout", "https://github.com/Turman005/Genexus-DarkMode");
                k.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
                k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                k.SetValue("EstimatedSize", 2048, RegistryValueKind.DWord);
            }
            Log("Registrado em Configuracoes > Aplicativos instalados");
        }

        // ---- desinstalar ----
        public static bool Desinstalar(bool manterConfig)
        {
            bool tudo = true;
            Critico(true);
            Etapa("Procurando as instalações do GeneXus 18...", 0.0, 0.05, false);
            List<GxInstalacao> todas = Detectar();
            int total = 0, i = -1;
            foreach (GxInstalacao g in todas) if (File.Exists(Path.Combine(g.Pasta, "Packages", Dll))) total++;
            foreach (GxInstalacao g in todas)
            {
                string dll = Path.Combine(g.Pasta, "Packages", Dll);
                bool tinha = File.Exists(dll);
                LimparLegado(g);
                if (!tinha) continue;
                i++;
                double b0 = 0.05 + 0.75 * i / total, sp = 0.75 / total;
                string qual = g.Nome + (total > 1 ? " (" + (i + 1) + " de " + total + ")" : "");
                Etapa("Removendo arquivos de " + qual + "...", b0, b0 + sp * 0.10, false);
                Log("");
                Log("== " + g.Nome + " (" + g.Pasta + ")");
                try { File.Delete(dll); Log("  removido " + dll); }
                catch (Exception ex) { Log("  ERRO ao remover " + dll + ": " + ex.Message); tudo = false; continue; }
                Etapa("Atualizando o GeneXus: " + qual + "... (pode levar 1-2 minutos)", b0 + sp * 0.10, b0 + sp, true);
                RodarInstall(g);
            }
            Etapa("Limpando a lista de pacotes do GeneXus...", 0.80, 0.88, false);
            TirarDoCatalogo(Dll);
            TirarDoCatalogo(Produto.LegadoXpzDll);
            Etapa("Desfazendo ajustes de versões antigas...", 0.88, 0.94, false);
            int n = Legado.Restaurar(Log);
            if (n > 0) Log("Configuracoes da versao antiga do Ativar devolvidas ao original (" + n + " grupo(s))");
            Etapa("Removendo o registro do programa...", 0.94, 1.0, false);
            if (!manterConfig)
            {
                try { if (File.Exists(Settings)) { File.Delete(Settings); Log("Configuracao removida: " + Settings); } } catch (Exception ex) { Log("AVISO: " + ex.Message); }
            }
            else Log("Configuracao mantida: " + Settings);
            try { RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64).DeleteSubKeyTree(ChaveUninst, false); Log("Removido de Aplicativos instalados"); } catch (Exception ex) { Log("AVISO: " + ex.Message); }
            // a pasta do programa (inclusive esta .exe) e apagada logo depois que o desinstalador fechar
            try
            {
                string app = PastaApp;
                if (Directory.Exists(app))
                {
                    foreach (string f in Directory.GetFiles(app)) { try { if (!f.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) File.Delete(f); } catch { } }
                    ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", "/c ping 127.0.0.1 -n 3 > nul & rd /s /q \"" + app + "\"") { CreateNoWindow = true, UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden };
                    Process.Start(psi);
                }
            }
            catch { }
            return tudo;
        }
    }

    // ---------------------------------------------------------------- Restaurar-Original.ps1 (porte para C#)
    // Desfaz o que a versao ANTIGA do Ativar gravava fora da extensao, so onde o valor ainda e o escuro dela.
    public static class Legado
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool SetSysColors(int c, int[] e, int[] v);
        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        static extern IntPtr SendMessageTimeout(IntPtr h, uint m, UIntPtr w, string l, uint f, uint t, out UIntPtr r);

        public static int Restaurar(Action<string> log)
        {
            string bkDir = Path.Combine(Ops.CfgDir, "DarkModeBackup");
            if (!Directory.Exists(bkDir)) return 0;
            int did = 0;
            try { did += Editor(bkDir, log); } catch (Exception ex) { log("AVISO (cores do editor): " + ex.Message); }
            try { did += Cores(bkDir, log); } catch (Exception ex) { log("AVISO (cores do Windows): " + ex.Message); }
            try { did += Dwm(bkDir, log); } catch (Exception ex) { log("AVISO (tema dos apps): " + ex.Message); }
            try { string old = bkDir + ".restaurado-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"); Directory.Move(bkDir, old); log("Backup antigo guardado em " + old); } catch { }
            return did;
        }

        static int Editor(string bkDir, Action<string> log)
        {
            string cfg = Path.Combine(Ops.CfgDir, "TextEditorConfig_Default.xml"), bk = Path.Combine(bkDir, "TextEditorConfig_Default.xml");
            if (!File.Exists(cfg) || !File.Exists(bk)) return 0;
            string cur = File.ReadAllText(cfg);
            if (!cur.Contains("<ForeColor>-11100970</ForeColor>") && !cur.Contains("Key=\"DefaultStyle\"")) return 0;
            System.Xml.XmlDocument x = new System.Xml.XmlDocument(); x.LoadXml(cur);
            System.Xml.XmlDocument b = new System.Xml.XmlDocument(); b.Load(bk);
            System.Xml.XmlNode c = x.SelectSingleNode("/TextEditorConfig/HighlightingStyles"), o = b.SelectSingleNode("/TextEditorConfig/HighlightingStyles");
            if (c == null || o == null) return 0;
            File.Copy(cfg, Path.Combine(bkDir, "TextEditorConfig_Default.escuro-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".xml"), true);
            x.DocumentElement.ReplaceChild(x.ImportNode(o, true), c);
            x.Save(cfg);
            log("Cores originais do editor restauradas (TextEditorConfig_Default.xml)");
            return 1;
        }

        static readonly string[,] Escuro = {
            { "Scrollbar", "36 45 60", "0" }, { "ActiveTitle", "36 45 60", "2" }, { "InactiveTitle", "24 31 42", "3" }, { "Menu", "24 31 42", "4" },
            { "Window", "17 21 28", "5" }, { "WindowFrame", "51 61 71", "6" }, { "MenuText", "240 244 250", "7" }, { "WindowText", "220 226 235", "8" },
            { "TitleText", "240 244 250", "9" }, { "ActiveBorder", "36 45 60", "10" }, { "InactiveBorder", "24 31 42", "11" }, { "AppWorkspace", "17 21 28", "12" },
            { "Hilight", "38 79 120", "13" }, { "HilightText", "255 255 255", "14" }, { "ButtonFace", "24 31 42", "15" }, { "ButtonShadow", "12 15 20", "16" },
            { "GrayText", "110 122 138", "17" }, { "ButtonText", "240 244 250", "18" }, { "InactiveTitleText", "138 144 155", "19" }, { "ButtonHilight", "61 73 87", "20" },
            { "ButtonDkShadow", "8 10 14", "21" }, { "ButtonLight", "51 61 71", "22" }, { "InfoText", "220 226 235", "23" }, { "InfoWindow", "36 45 60", "24" },
            { "ButtonAlternateFace", "31 39 52", "25" }, { "HotTrackingColor", "91 167 255", "26" }, { "GradientActiveTitle", "36 45 60", "27" },
            { "GradientInactiveTitle", "24 31 42", "28" }, { "MenuHilight", "31 78 121", "29" }, { "MenuBar", "36 45 60", "30" } };

        static int Cores(string bkDir, Action<string> log)
        {
            string bk = Path.Combine(bkDir, "SystemColors.txt");
            if (!File.Exists(bk)) return 0;
            List<int> idx = new List<int>(), val = new List<int>();
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Control Panel\Colors", true))
            {
                if (k == null) return 0;
                foreach (string line in File.ReadAllLines(bk))
                {
                    int i = line.IndexOf('=');
                    if (i <= 0) continue;
                    string n = line.Substring(0, i), v = line.Substring(i + 1);
                    int row = -1;
                    for (int r = 0; r < Escuro.GetLength(0); r++) if (Escuro[r, 0] == n) row = r;
                    if (row < 0) continue;
                    string now = k.GetValue(n) as string;
                    if (now != Escuro[row, 1] || now == v) continue;
                    k.SetValue(n, v);
                    string[] p = v.Split(' ');
                    idx.Add(int.Parse(Escuro[row, 2]));
                    val.Add(int.Parse(p[0]) + (int.Parse(p[1]) << 8) + (int.Parse(p[2]) << 16));
                }
            }
            if (idx.Count == 0) return 0;
            SetSysColors(idx.Count, idx.ToArray(), val.ToArray());
            log("Cores originais do Windows restauradas (" + idx.Count + " itens)");
            return 1;
        }

        static int Dwm(string bkDir, Action<string> log)
        {
            string bk = Path.Combine(bkDir, "WindowsTheme.txt");
            if (!File.Exists(bk)) return 0;
            Dictionary<string, long> nossos = new Dictionary<string, long> { { "AppsUseLightTheme", 0 }, { "ColorPrevalence", 1 }, { "AccentColor", -12833500 }, { "AccentColorInactive", -14016744 } };
            int changed = 0;
            foreach (string line in File.ReadAllLines(bk))
            {
                string[] p = line.Split(new char[] { '|' }, 3);
                if (p.Length < 3 || !nossos.ContainsKey(p[1])) continue;
                string path = p[0].Replace("HKCU:\\", "").Replace("HKEY_CURRENT_USER\\", "");
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(path, true))
                {
                    if (k == null) continue;
                    object now = k.GetValue(p[1]);
                    if (now == null) continue;
                    if ((Convert.ToInt64(now) & 0xFFFFFFFFL) != (nossos[p[1]] & 0xFFFFFFFFL)) continue;
                    if (p[2] == "ABSENT") k.DeleteValue(p[1], false);
                    else k.SetValue(p[1], BitConverter.ToInt32(BitConverter.GetBytes(long.Parse(p[2])), 0), RegistryValueKind.DWord);
                    changed++;
                }
            }
            if (changed == 0) return 0;
            UIntPtr r;
            SendMessageTimeout((IntPtr)0xffff, 0x1A, UIntPtr.Zero, "ImmersiveColorSet", 2, 3000, out r);
            log("Tema claro dos apps e barra de titulo originais restaurados");
            return 1;
        }
    }

}
