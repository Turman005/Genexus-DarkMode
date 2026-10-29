// Gera o .isl de cada tema a partir do .isl do GeneXus e confere:
//   - XML valido (um atributo repetido derruba a moldura inteira - aconteceu em 28/10);
//   - nenhum hover/pressionado sem fundo fora dos 7 papeis de texto (o "hover amarelo");
//   - nenhum fundo claro sobrando (backColor com luminancia alta).
using System; using System.Collections; using System.IO; using System.Linq; using System.Reflection;
using System.Text.RegularExpressions; using System.Xml;

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
        Type temas = dark.GetType("Xpz.Dark.Temas", true), ger = dark.GetType("Xpz.Dark.GeradorIsl", true);
        string original = File.ReadAllText(Path.Combine(GX, "GenexusOffice2010Silver.isl"));
        var semCaixa = new[] { "CheckEditor", "EditorControl", "GridCell", "LiveTileViewGroupTextArea", "ScheduleOwnerHeader", "TreeNodeExpansionIndicator", "UltraGroupBoxExpansionIndicator" };
        foreach (object t in (IList)temas.GetField("Todos", E).GetValue(null))
        {
            string id = (string)t.GetType().GetField("Id").GetValue(t);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            string isl = (string)ger.GetMethod("Gerar", E).Invoke(null, new[] { original, t });
            sw.Stop();
            bool xmlOk = true; string erro = "";
            try { new XmlDocument().LoadXml(isl.TrimStart('\uFEFF')); } catch (Exception ex) { xmlOk = false; erro = ex.Message; }
            int hoverSemFundo = 0;
            foreach (Match m in Regex.Matches(isl, "<style role=\"([^\"]+)\"[^>]*>(.*?)</style>", RegexOptions.Singleline))
            {
                if (semCaixa.Contains(m.Groups[1].Value)) continue;
                foreach (Match st in Regex.Matches(m.Groups[2].Value, "<state name=\"(HotTracked|Pressed|HotTrackSelected)\"([^>]*)"))
                {
                    // Excecao de proposito: Pressed do MenuItem = item MARCADO do menu (so a letra;
                    // fundo pareceria hover preso). Mas a letra tem de estar la. A lista de barras (MenuItemToolbarList) e copia do MenuItem.
                    if ((m.Groups[1].Value == "MenuItem" || m.Groups[1].Value == "MenuItemToolbarList") && st.Groups[1].Value == "Pressed" && st.Groups[2].Value.Contains("foreColor=")) continue;
                    if (!st.Groups[2].Value.Contains("backColor=")) hoverSemFundo++;
                }
            }
            int claros = 0;
            foreach (Match m in Regex.Matches(isl, "backColor=\"(\\d+), (\\d+), (\\d+)\""))
            {
                double lum = (0.2126 * int.Parse(m.Groups[1].Value) + 0.7152 * int.Parse(m.Groups[2].Value) + 0.0722 * int.Parse(m.Groups[3].Value)) / 255.0;
                if (lum > 0.6) claros++;
            }
            Check(id + ": XML valido", xmlOk, erro + "  (" + sw.ElapsedMilliseconds + " ms)");
            Check(id + ": todo hover/pressionado com fundo do tema", hoverSemFundo == 0, hoverSemFundo + " sem fundo");
            Check(id + ": nenhum fundo claro", claros == 0, claros + " claros");
            Check(id + ": nenhum componente com o visual (claro) do Windows", !isl.Contains("useOsThemes=\"True\""), "");
            Check(id + ": coluna de icones dos menus declarada", isl.Contains("<style role=\"MenuIconArea\">") && isl.Contains("<style role=\"MenuIconAreaNonRecentlyUsed\">"), "");
        }
        Console.WriteLine(falhas == 0 ? "TODOS OS TESTES PASSARAM" : falhas + " FALHA(S)");
        return falhas;
    }
}
