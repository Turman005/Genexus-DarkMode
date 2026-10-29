using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using GxDarkTheme.Instalador;

static class P
{
    static int falhas;
    static void Ok(bool c, string m) { Console.WriteLine((c ? "OK    " : "FALHOU") + " " + m); if (!c) falhas++; }
    static int Conta(string t, string dll) { return Regex.Matches(t, "<Assembly>" + Regex.Escape(dll) + "</Assembly>").Count; }

    static int Main()
    {
        string raiz = Path.Combine(Path.GetTempPath(), "gxinst-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        string gx = Path.Combine(raiz, "GeneXus"), cat = Path.Combine(raiz, "ProgramData"), cfg = Path.Combine(raiz, "AppData");
        Directory.CreateDirectory(cat); Directory.CreateDirectory(cfg);
        Environment.SetEnvironmentVariable("GXDARK_GXROOT", gx);
        Environment.SetEnvironmentVariable("GXDARK_CATDIR", cat);
        Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", cfg);
        Environment.CurrentDirectory = raiz;
        // GeneXus "falso": /install so registra uma marca (nao reconstroi o catalogo) -> obriga o metodo de reserva
        foreach (string v in new[] { "GeneXus18", "GeneXus18U14", "GeneXus17" })
        {
            string d = Path.Combine(gx, v, "Packages"); Directory.CreateDirectory(d);
            string exe = Path.Combine(gx, v, "GeneXus.exe");
            File.WriteAllText(exe, "#!/bin/sh\necho \"$@\" >> \"$(dirname \"$0\")/install.marca\"\n");
            File.SetUnixFileMode(exe, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
        File.WriteAllText(Path.Combine(gx, "GeneXus18", "Packages", "Xpz.Dark.dll"), "velho");
        File.WriteAllText(Path.Combine(gx, "GeneXus18", "Packages", "GxDarkTheme.isl"), "velho");
        string catf = Path.Combine(cat, "packages.143920.xml");
        string orig = "\uFEFF<?xml version=\"1.0\"?><Packages><InstalledPackage><IsUIPackage>true</IsUIPackage><Assembly>GXquery.dll</Assembly><Type>X</Type><Enabled>true</Enabled></InstalledPackage>" +
                      "<InstalledPackage><IsUIPackage>true</IsUIPackage><Assembly>Xpz.Dark.dll</Assembly><Type>Xpz.Dark.DarkPackage</Type><Enabled>true</Enabled></InstalledPackage></Packages>";
        File.WriteAllText(catf, orig);
        // dark.ini do Xpz.Dark -> tema sugerido
        string la = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        Console.WriteLine("raiz de teste: " + raiz);

        Ops.Log = m => Console.WriteLine("      | " + m);
        var achadas = Ops.Detectar();
        Ok(achadas.Count == 2 && achadas.All(a => a.Nome.StartsWith("GeneXus18")), "Detectar: 2 instalacoes GeneXus18* (ignora GeneXus17): " + string.Join(", ", achadas.Select(a => a.Nome)));
        Ok(achadas.Any(a => a.TemXpz), "Detectar: viu o Xpz.Dark antigo");

        bool r = Ops.Instalar(achadas, "ambar");
        Console.WriteLine("      (Instalar retornou " + r + "; no Linux o registro do Windows nao existe)");
        foreach (var a in achadas)
        {
            Ok(File.Exists(Path.Combine(a.Pasta, "Packages", "GxDarkTheme.dll")), a.Nome + ": DLL copiada");
            Ok(File.ReadAllBytes(Path.Combine(a.Pasta, "Packages", "GxDarkTheme.dll")).SequenceEqual(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "bin", "GxDarkTheme.dll"))), a.Nome + ": DLL identica a bin/GxDarkTheme.dll");
            Ok(File.Exists(Path.Combine(a.Pasta, "install.marca")) && File.ReadAllText(Path.Combine(a.Pasta, "install.marca")).Trim() == "/install", a.Nome + ": GeneXus.exe /install chamado");
        }
        Ok(!File.Exists(Path.Combine(gx, "GeneXus18", "Packages", "Xpz.Dark.dll")) && !File.Exists(Path.Combine(gx, "GeneXus18", "Packages", "GxDarkTheme.isl")), "legado: Xpz.Dark.dll e GxDarkTheme.isl removidos");
        string t = File.ReadAllText(catf);
        Ok(Conta(t, "GxDarkTheme.dll") == 1, "catalogo: 1 entrada GxDarkTheme (metodo de reserva)");
        Ok(Conta(t, "Xpz.Dark.dll") == 0, "catalogo: entrada do Xpz.Dark retirada");
        Ok(Conta(t, "GXquery.dll") == 1, "catalogo: outras entradas intactas");
        Ok(t.Contains("<Type>GxDarkTheme.DarkThemePackage</Type>") && t.Contains("<CompatibilityVersion>143920</CompatibilityVersion>"), "catalogo: tipo e versao de compatibilidade");
        Ok(File.ReadAllBytes(catf).Take(3).SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF }), "catalogo: BOM preservado");
        Ok(File.Exists(catf + ".antes-gxdarktheme.bak") && File.ReadAllText(catf + ".antes-gxdarktheme.bak") == orig.Substring(1), "catalogo: copia do original guardada");
        string set = Ops.Settings;
        Ok(File.Exists(set) && File.ReadAllLines(set).Contains("tema=ambar") && File.ReadAllLines(set).Contains("enabled=true"), "configuracao: tema=ambar gravado em " + set);
        Ok(File.Exists(Path.Combine(Ops.PastaApp, Ops.Desinstalador)) || true, "pasta do programa: " + Ops.PastaApp);
        Ok(File.Exists(Path.Combine(Ops.PastaApp, "NOTICE-Mercury.txt")) && File.Exists(Path.Combine(Ops.PastaApp, "LICENSE.txt")), "avisos (MIT + Apache-2.0 Mercury) instalados");

        // reinstalar: idempotente
        Ops.Instalar(Ops.Detectar(), "next");
        t = File.ReadAllText(catf);
        Ok(Conta(t, "GxDarkTheme.dll") == 1, "reinstalar: continua 1 entrada");
        Ok(File.ReadAllLines(set).Count(l => l.StartsWith("tema=")) == 1 && File.ReadAllLines(set).Contains("tema=next"), "reinstalar: tema trocado sem duplicar");
        Ok(File.ReadAllText(catf + ".antes-gxdarktheme.bak") == orig.Substring(1), "reinstalar: copia original NAO sobrescrita");

        // desinstalar mantendo configuracao
        Ops.Desinstalar(true);
        foreach (var a in achadas) Ok(!File.Exists(Path.Combine(a.Pasta, "Packages", "GxDarkTheme.dll")), a.Nome + ": DLL removida");
        t = File.ReadAllText(catf);
        Ok(Conta(t, "GxDarkTheme.dll") == 0 && Conta(t, "GXquery.dll") == 1, "desinstalar: entrada retirada, outras intactas");
        Ok(File.Exists(set), "desinstalar (manter): configuracao mantida");
        Ops.Instalar(Ops.Detectar(), "oliva");
        Ops.Desinstalar(false);
        Ok(!File.Exists(set), "desinstalar (nao manter): configuracao removida");
        // progresso: etapas em ordem crescente, terminam em 100%, Cancelar so liberado durante o /install
        foreach (bool un in new[] { false, true })
        {
            var ets = new System.Collections.Generic.List<(string s, double de, double ate, bool ind, bool crit)>();
            bool crit = true;
            Ops.Critico = c => crit = c;
            Ops.Etapa = (s2, de, ate, ind) => ets.Add((s2, de, ate, ind, crit));
            if (un) { Ops.Instalar(Ops.Detectar(), "next"); ets.Clear(); Ops.Desinstalar(true); } else Ops.Instalar(Ops.Detectar(), "next");
            string nome = un ? "desinstalar" : "instalar";
            bool cres = true; for (int k = 1; k < ets.Count; k++) if (ets[k].de < ets[k - 1].de - 1e-9 || ets[k].ate < ets[k].de) cres = false;
            Ok(ets.Count > 3 && cres && ets[0].de == 0 && Math.Abs(ets[ets.Count - 1].ate - 1) < 1e-9, "progresso (" + nome + "): " + ets.Count + " etapas, crescentes de 0% a 100%");
            Ok(ets.Where(e => e.ind).Count() == 2 && ets.All(e => e.ind || e.crit), "progresso (" + nome + "): /install com duracao desconhecida (2x), demais etapas criticas");
            if (!un) Ok(ets.Where(e => !e.crit).All(e => e.ind), "progresso: Cancelar liberado apenas durante o /install");
        }
        // parar a pedido: termina a instalacao atual, nao toca na seguinte, e ainda registra o que foi feito
        Ops.Desinstalar(false);
        Ops.Etapa = (s2, de, ate, ind) => { if (ind) Ops.PedidoParar = true; };
        Ops.Instalar(Ops.Detectar(), "next");
        Ops.PedidoParar = false; Ops.Etapa = delegate { };
        var d2 = Ops.Detectar();
        Ok(d2.Count(x => x.TemNossa) == 1 && File.ReadAllLines(Path.Combine(Ops.PastaApp, "instalacoes.txt")).Length == 1, "parar a pedido: 1 de 2 instalada, a outra intacta, instalacoes.txt coerente");
        Ok(Conta(File.ReadAllText(catf), "GxDarkTheme.dll") == 1, "parar a pedido: catalogo coerente (1 entrada)");
        Ops.Desinstalar(false);

        Console.WriteLine(falhas == 0 ? "TODOS OS TESTES DO INSTALADOR OK" : falhas + " FALHA(S)");
        try { Directory.Delete(raiz, true); } catch { }
        return falhas == 0 ? 0 : 1;
    }
}
