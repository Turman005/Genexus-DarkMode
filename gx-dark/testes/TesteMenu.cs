// Testa o MenuTema da Xpz.Dark contra o UltraToolbarsManager REAL (Infragistics 11.1).
using System;
using System.Reflection;
using System.Windows.Forms;
using Infragistics.Win.UltraWinToolbars;

class MainWindow : Form
{
    public UltraToolbarsManager toolBarManager;
    public PopupMenuTool tools;
    public MainWindow()
    {
        toolBarManager = new UltraToolbarsManager();
        toolBarManager.DockWithinContainer = this;
        var tb = new UltraToolbar("MainMenuBar") { IsMainMenuBar = true };
        toolBarManager.Toolbars.Add(tb);
        tools = new GxPopup("ToolsPopup"); tools.SharedProps.Caption = "&Tools";
        var help = new PopupMenuTool("HelpPopup"); help.SharedProps.Caption = "&Help";
        var opt = new ButtonTool("Options"); opt.SharedProps.Caption = "Options...";
        toolBarManager.Tools.AddRange(new ToolBase[] { tools, help, opt });
        tools.Tools.AddTool("Options");
        tb.Tools.AddTool("ToolsPopup"); tb.Tools.AddTool("HelpPopup");
    }
}

static class TesteMenu
{
    const string GX = @"C:\Program Files (x86)\GeneXus\GeneXus18-U10";
    [STAThread]
    static int Main(string[] a)
    {
        AppDomain.CurrentDomain.AssemblyResolve += (s, e) =>
        {
            string arq = System.IO.Path.Combine(GX, new AssemblyName(e.Name).Name + ".dll");
            return System.IO.File.Exists(arq) ? Assembly.LoadFrom(arq) : null;
        };
        return Rodar(a[0]);
    }
    static int Rodar(string dll)
    {
        var w = new MainWindow { StartPosition = FormStartPosition.Manual, Location = new System.Drawing.Point(-32000, -32000), ShowInTaskbar = false };
        w.Show(); Application.DoEvents();
        Assembly dark = Assembly.LoadFrom(dll);
        Type menu = dark.GetType("Xpz.Dark.MenuTema", true);
        MethodInfo garantir = menu.GetMethod("Garantir", BindingFlags.Static | BindingFlags.NonPublic);
        garantir.Invoke(null, null); garantir.Invoke(null, null);   // 2x: nao pode duplicar
        int n = 0; bool primeiroDoGrupo = false;
        foreach (ToolBase t in w.tools.Tools) if (t.Key == "XpzDarkMenu") { n++; primeiroDoGrupo = t.InstanceProps.IsFirstInGroup; }
        var sub = (PopupMenuTool)w.toolBarManager.Tools["XpzDarkMenu"];
        int temas = 0, marcados = 0; bool cfg = false;
        foreach (ToolBase t in sub.Tools)
        {
            if (t.Key.StartsWith("XpzDarkTema.")) { temas++; if (((StateButtonTool)t).Checked) marcados++; }
            if (t.Key == "XpzDarkConfig") cfg = t.InstanceProps.IsFirstInGroup;
        }
        string cap = sub.SharedProps.Caption;
        Console.WriteLine("  {0} submenu 'Tema escuro' no Tools (1 vez)              n={1}", n == 1 ? "[ok] " : "FALHA", n);
        Console.WriteLine("  {0} separador antes do submenu                      {1}", primeiroDoGrupo ? "[ok] " : "FALHA", primeiroDoGrupo);
        Console.WriteLine("  {0} 7 temas, exatamente 1 marcado                    temas={1} marcados={2}", temas == 7 && marcados == 1 ? "[ok] " : "FALHA", temas, marcados);
        Console.WriteLine("  {0} 'Configuracoes...' no fim, separado              {1}", cfg ? "[ok] " : "FALHA", cfg);
        // --- clique num tema: tem que FICAR nele (bug de 28/10: voltava ao Ardosia)
        // Perfil isolado (XPZDARK_PASTA, posto pelo Testar-Dark.ps1): o clique GRAVA o dark.ini, e
        // nunca pode ser o do usuario - com o GeneXus dele aberto, a copia/restauracao antiga
        // corria contra a escolha de tema que ele fazia no mesmo minuto (28/10).
        string pasta = Environment.GetEnvironmentVariable("XPZDARK_PASTA");
        if (string.IsNullOrEmpty(pasta)) { Console.WriteLine("  FALHA XPZDARK_PASTA ausente: rode pelo Testar-Dark.ps1"); return 1; }
        string ini = System.IO.Path.Combine(pasta, "dark.ini");
        System.IO.Directory.CreateDirectory(pasta);
        string[] antes = System.IO.File.Exists(ini) ? System.IO.File.ReadAllLines(ini) : null;
        System.IO.File.WriteAllLines(ini, new[] { "Ativo = sim", "Tema = ardosia" });
        bool ficou = false; string noIni = "?";
        try
        {
            var cfgT = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { { "Tema", "ardosia" } };
            dark.GetType("Xpz.Dark.Fase2").GetMethod("Iniciar", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Invoke(null, new object[] { cfgT });
            garantir.Invoke(null, null);
            var st = (StateButtonTool)w.toolBarManager.Tools["XpzDarkTema.ametista"];
            st.Checked = true;   // o que a Infragistics faz no clique: muda Checked e dispara ToolClick
            Application.DoEvents();
            garantir.Invoke(null, null);
            object tema = dark.GetType("Xpz.Dark.Paleta").GetField("TemaAtual").GetValue(null);
            string id = (string)tema.GetType().GetField("Id").GetValue(tema);
            foreach (string l in System.IO.File.ReadAllLines(ini)) if (l.StartsWith("Tema =")) noIni = l;
            int marc = 0; string qual = "";
            foreach (ToolBase t in sub.Tools) if (t.Key.StartsWith("XpzDarkTema.") && ((StateButtonTool)t).Checked) { marc++; qual = t.Key; }
            ficou = id == "ametista" && noIni.Contains("ametista") && marc == 1 && qual.EndsWith("ametista");
            Console.WriteLine("  {0} clique em Ametista: fica em Ametista                tema={1} ini=[{2}] marcado={3}", ficou ? "[ok] " : "FALHA", id, noIni, qual);
        }
        finally { if (antes != null) System.IO.File.WriteAllLines(ini, antes); else System.IO.File.Delete(ini); }
        w.Close();
        return n == 1 && primeiroDoGrupo && temas == 7 && marcados == 1 && cfg && ficou ? 0 : 1;
    }
}

// Como o GeneXus: tipo DERIVADO do PopupMenuTool.
[Serializable]
class GxPopup : PopupMenuTool
{
    public GxPopup(string k) : base(k) { }
    protected GxPopup(System.Runtime.Serialization.SerializationInfo i, System.Runtime.Serialization.StreamingContext c) : base(i, c) { }
}
