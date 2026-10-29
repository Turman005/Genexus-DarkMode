// Lista de adaptadores (src/Nucleo/Adaptadores.cs): cobertura por biblioteca e etapa, e
// isolamento - um adaptador com erro nao impede os outros e conta so na parte dele.
using System; using System.Collections; using System.Collections.Generic; using System.Drawing; using System.IO; using System.Linq; using System.Reflection; using System.Windows.Forms;

static class T
{
    const string GX = @"C:\Program Files (x86)\GeneXus\GeneXus18-U10";
    static int falhas;
    static void Check(string n, bool ok, string d) { Console.WriteLine("  {0} {1,-58} {2}", ok ? "[ok] " : "FALHA", n, d); if (!ok) falhas++; }

    [STAThread] static int Main(string[] a)
    {
        AppDomain.CurrentDomain.AssemblyResolve += (s, e) => { string p = Path.Combine(GX, new AssemblyName(e.Name).Name + ".dll"); return File.Exists(p) ? Assembly.LoadFrom(p) : null; };
        Assembly dark = Assembly.LoadFrom(a[0]);
        BindingFlags E = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        Type f2 = dark.GetType("Xpz.Dark.Fase2", true);
        f2.GetMethod("Iniciar", E).Invoke(null, new object[] { new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) });

        var lista = (IList)f2.GetProperty("Adaptadores", E).GetValue(null, null);
        var bibl = new HashSet<string>(); var etapas = new HashSet<string>();
        foreach (object ad in lista)
        {
            bibl.Add((string)ad.GetType().GetProperty("Biblioteca").GetValue(ad, null));
            etapas.Add(ad.GetType().GetProperty("Etapa").GetValue(ad, null).ToString());
        }
        Console.WriteLine("  " + lista.Count + " adaptadores; bibliotecas: " + string.Join(", ", bibl.OrderBy(x => x).ToArray()));
        Check("as 4 etapas estao representadas", etapas.Count == 4, string.Join(",", etapas.ToArray()));
        foreach (string b in new[] { "Actipro", "Chromium", "DevExpress", "Infragistics", "VisualHint", "Crownwood", "WinForms", "Win32" })
            Check("biblioteca coberta: " + b, bibl.Contains(b), "");

        // adaptador quebrado no inicio da etapa Especial
        Type tAd = dark.GetType("Xpz.Dark.Adaptador", true), tEt = dark.GetType("Xpz.Dark.Etapa", true);
        Func<Control, bool> sempre = c => true, explode = c => { throw new InvalidOperationException("adaptador de teste quebrado"); };
        object quebrado = Activator.CreateInstance(tAd, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
            new object[] { Enum.Parse(tEt, "Especial"), "Teste", "quebrado", "parte de teste", sempre, explode }, null);
        int idx = 0; for (int i = 0; i < lista.Count; i++) if (lista[i].GetType().GetProperty("Etapa").GetValue(lista[i], null).ToString() == "Especial") { idx = i; break; }
        lista.Insert(idx, quebrado);

        var f = new Form { Location = new Point(-32000, -32000), StartPosition = FormStartPosition.Manual, ShowInTaskbar = false };
        var bt = new Button { Text = "ok" }; var tb = new TextBox(); var gb = new GroupBox { Text = "g" };
        f.Controls.Add(bt); f.Controls.Add(tb); f.Controls.Add(gb);
        for (int i = 0; i < 6; i++) f.Controls.Add(new Panel());   // mais controles: o disjuntor desliga na 5a falha
        f2.GetMethod("Processar", E).Invoke(null, new object[] { f });
        Check("com um adaptador quebrado, o botao ainda fica plano", bt.FlatStyle == FlatStyle.Flat, bt.FlatStyle.ToString());
        Check("... o GroupBox ainda fica plano", gb.FlatStyle == FlatStyle.Flat, gb.FlatStyle.ToString());
        Check("... a caixa de texto ainda escurece (regra geral)", tb.BackColor.R < 80, tb.BackColor.ToString());
        bool desligada = !(bool)dark.GetType("Xpz.Dark.Disjuntor").GetMethod("Ligada", E).Invoke(null, new object[] { "parte de teste" });
        Check("o erro desligou so a parte do adaptador quebrado", desligada, "");
        bool janelas = (bool)dark.GetType("Xpz.Dark.Disjuntor").GetMethod("Ligada", E).Invoke(null, new object[] { "janelas" });
        Check("... e nao a parte 'janelas'", janelas, "");
        f.Close();
        Console.WriteLine(falhas == 0 ? "TODOS OS TESTES PASSARAM" : falhas + " FALHA(S)");
        return falhas;
    }
}
