// Icones da moldura: os Icon estaticos do DockManager e do TabbedMdiManager (fechar, alfinete)
// sao trocados por versoes claras. Busca pelo TIPO (a U14 e ofuscada) e campos "static
// readonly" - a 0.13.1 os pulava (regressao vista no log da U14, 28/10).
using System; using System.Collections.Generic; using System.Drawing; using System.IO; using System.Reflection;
static class T
{
    static int falhas;
    static void Check(string n, bool ok, string d) { Console.WriteLine("  {0} {1,-58} {2}", ok ? "[ok] " : "FALHA", n, d); if (!ok) falhas++; }
    static int Main(string[] a)
    {
        string gx = a[1];
        AppDomain.CurrentDomain.AssemblyResolve += (s, e) => { string p = Path.Combine(gx, new AssemblyName(e.Name).Name + ".dll"); return File.Exists(p) ? Assembly.LoadFrom(p) : null; };
        Assembly dark = Assembly.LoadFrom(a[0]);
        Assembly fw = Assembly.LoadFrom(Path.Combine(gx, "Artech.FrameworkDE.dll"));
        BindingFlags E = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        var antes = new Dictionary<FieldInfo, Icon>();
        foreach (string tipo in new[] { "Artech.FrameworkDE.DockManager", "Artech.FrameworkDE.TabbedMdiManager" })
            foreach (FieldInfo f in fw.GetType(tipo, true).GetFields(E))
                if (f.FieldType == typeof(Icon)) antes[f] = (Icon)f.GetValue(null);
        Check(Path.GetFileName(gx) + ": icones fixos encontrados", antes.Count == 4, antes.Count + " campos");
        dark.GetType("Xpz.Dark.Icones").GetMethod("Aplicar", E).Invoke(null, null);
        int trocados = 0;
        foreach (var kv in antes) if (!ReferenceEquals(kv.Key.GetValue(null), kv.Value)) trocados++;
        Check(Path.GetFileName(gx) + ": icones escuros trocados pelos claros", antes.Count > 0 && trocados == antes.Count, trocados + " de " + antes.Count);
        Console.WriteLine(falhas == 0 ? "TODOS OS TESTES PASSARAM" : falhas + " FALHA(S)");
        return falhas;
    }
}
