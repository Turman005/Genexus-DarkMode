// Disjuntor, checagem de compatibilidade e modo seguro da Xpz.Dark.
// Mexe so na pasta de perfil (XPZDARK_PASTA nos testes; senao %LOCALAPPDATA%\Xpz.Dark) e a deixa como achou.
using System; using System.Collections; using System.IO; using System.Linq; using System.Reflection;

static class T
{
    const string GX = @"C:\Program Files (x86)\GeneXus\GeneXus18-U10";
    static int falhas;
    static void Check(string n, bool ok, string d) { Console.WriteLine("  {0} {1,-60} {2}", ok ? "[ok] " : "FALHA", n, d); if (!ok) falhas++; }

    [STAThread] static int Main(string[] a)
    {
        AppDomain.CurrentDomain.AssemblyResolve += (s, e) => { string p = Path.Combine(GX, new AssemblyName(e.Name).Name + ".dll"); return File.Exists(p) ? Assembly.LoadFrom(p) : null; };
        Assembly dark = Assembly.LoadFrom(a[0]);
        BindingFlags E = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        // ------------------------------------------------ disjuntor
        Type dj = dark.GetType("Xpz.Dark.Disjuntor", true);
        MethodInfo falhou = dj.GetMethod("Falhou", E), ligada = dj.GetMethod("Ligada", E), executar = dj.GetMethod("Executar", E);
        for (int i = 0; i < 4; i++) falhou.Invoke(null, new object[] { "teste", new InvalidOperationException("x") });
        Check("disjuntor: 4 falhas, parte continua ligada", (bool)ligada.Invoke(null, new object[] { "teste" }), "");
        falhou.Invoke(null, new object[] { "teste", new InvalidOperationException("x") });
        Check("disjuntor: 5a falha desliga a parte", !(bool)ligada.Invoke(null, new object[] { "teste" }), "");
        bool rodou = false;
        bool ret = (bool)executar.Invoke(null, new object[] { "teste", (Action)(() => rodou = true) });
        Check("disjuntor: parte desligada nao executa", !ret && !rodou, "");
        for (int i = 0; i < 10; i++) falhou.Invoke(null, new object[] { "janelas", new InvalidOperationException("x") });
        Check("disjuntor: 'janelas' tolera mais (10 falhas, ainda ligada)", (bool)ligada.Invoke(null, new object[] { "janelas" }), "");
        bool r2 = (bool)executar.Invoke(null, new object[] { "outra", (Action)(() => { throw new Exception("boom"); }) });
        Check("disjuntor: Executar engole a excecao e devolve false", !r2, "");

        // ------------------------------------------------ compatibilidade
        Type cp = dark.GetType("Xpz.Dark.Compatibilidade", true);
        string resumo = (string)cp.GetMethod("Verificar", E).Invoke(null, null);
        Console.WriteLine("  " + resumo);
        var itens = (IList)cp.GetField("Itens", E).GetValue(null);
        // Fora do GeneXus, AppDomain.BaseDirectory nao e a pasta da instalacao: os dois
        // itens de ARQUIVO (isl, Monaco) podem falhar aqui e so aqui.
        var deArquivo = new[] { ".isl original", "Output: tema do Monaco" };
        int falharam = 0;
        foreach (object it in itens)
        {
            bool ok = (bool)it.GetType().GetField("Ok").GetValue(it);
            string desc = (string)it.GetType().GetField("Descricao").GetValue(it);
            if (!ok && !deArquivo.Any(p => desc.StartsWith(p))) { falharam++; Console.WriteLine("      faltando: " + desc); }
        }
        Check("compatibilidade: todos os pontos de tipo/campo/funcao presentes na U10", falharam == 0, itens.Count + " pontos");

        // ------------------------------------------------ outro tema escuro instalado
        Type msT = dark.GetType("Xpz.Dark.ModoSeguro", true);
        MethodInfo outro = msT.GetMethod("OutroTemaEscuro", E);
        Check("sem outro tema escuro: nada detectado", outro.Invoke(null, null) == null, "");
        string pacotes = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Packages"), falso = Path.Combine(pacotes, "GxDarkTheme.dll");
        Directory.CreateDirectory(pacotes); File.WriteAllText(falso, "falso");
        try { Check("GxDarkTheme na Packages: detectado (Xpz.Dark sai do caminho)", (string)outro.Invoke(null, null) == "GxDarkTheme.dll", ""); }
        finally { File.Delete(falso); Directory.Delete(pacotes); }

        // ------------------------------------------------ modo seguro (marca de abertura interrompida)
        string pasta = Environment.GetEnvironmentVariable("XPZDARK_PASTA");
        if (string.IsNullOrEmpty(pasta)) pasta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Xpz.Dark");
        Directory.CreateDirectory(pasta);
        string marca = Path.Combine(pasta, "abrindo.marca");
        bool havia = File.Exists(marca);
        try
        {
            File.WriteAllText(marca, "teste");
            Type ms = dark.GetType("Xpz.Dark.ModoSeguro", true);
            ms.GetMethod("Avaliar", E).Invoke(null, null);
            bool ativo = (bool)ms.GetProperty("Ativo", E).GetValue(null, null);
            Check("modo seguro: marca de abertura interrompida liga o modo seguro", ativo, (string)ms.GetProperty("Motivo", E).GetValue(null, null));
            Check("modo seguro: a marca e apagada (a proxima abertura e normal)", !File.Exists(marca), "");
        }
        finally { if (!havia && File.Exists(marca)) File.Delete(marca); }

        Console.WriteLine(falhas == 0 ? "TODOS OS TESTES PASSARAM" : falhas + " FALHA(S)");
        return falhas;
    }
}
