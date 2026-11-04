// Testes que rodam fora do Windows (box/CI): equivalencia do gerador de ISL com o GxDarkTheme.isl v1,
// identidade do tema Next, contraste e ausencia de fundos claros em todos os temas.
using System; using System.Collections.Generic; using System.Drawing; using System.IO; using System.Linq; using System.Xml; using GxDarkTheme;
static class P
{
    static int falhas;
    static void Ok(bool c, string m) { Console.WriteLine((c ? "OK    " : "FALHA ") + m); if (!c) falhas++; }
    static string Canon(XmlElement e)
    {
        var at = e.Attributes.Cast<XmlAttribute>().OrderBy(a => a.Name, StringComparer.Ordinal).Select(a => a.Name + "=" + a.Value);
        string txt = string.Concat(e.ChildNodes.OfType<XmlText>().Select(t => t.Value.Trim()));
        var kids = e.ChildNodes.OfType<XmlElement>().Select(Canon).ToList();
        if (e.Name == "styles" || e.Name == "componentStyles" || e.Name == "states" || e.Name == "resources") kids.Sort(StringComparer.Ordinal);
        return "<" + e.Name + " " + string.Join(" ", at) + ">" + txt + string.Join("", kids) + "</" + e.Name + ">";
    }
    static void Diff(XmlElement a, XmlElement b, string path, List<string> o)
    {
        if (o.Count > 15) return;
        string sa = Canon(a), sb = Canon(b);
        if (sa == sb) return;
        var aa = a.Attributes.Cast<XmlAttribute>().ToDictionary(x => x.Name, x => x.Value);
        var bb = b.Attributes.Cast<XmlAttribute>().ToDictionary(x => x.Name, x => x.Value);
        foreach (var k in aa.Keys.Union(bb.Keys)) { aa.TryGetValue(k, out var va); bb.TryGetValue(k, out var vb); if (va != vb) o.Add(path + " @" + k + ": gerado=" + va + " v1=" + vb); }
        var ka = a.ChildNodes.OfType<XmlElement>().ToList(); var kb = b.ChildNodes.OfType<XmlElement>().ToList();
        string Key(XmlElement x) => x.Name + "[" + (x.GetAttribute("role") + x.GetAttribute("name")) + "]";
        var da = ka.GroupBy(Key).ToDictionary(g => g.Key, g => g.ToList()); var db = kb.GroupBy(Key).ToDictionary(g => g.Key, g => g.ToList());
        foreach (var k in da.Keys.Union(db.Keys))
        {
            if (!da.ContainsKey(k)) { o.Add(path + "/" + k + " so no v1"); continue; }
            if (!db.ContainsKey(k)) { o.Add(path + "/" + k + " so no gerado"); continue; }
            for (int i = 0; i < Math.Min(da[k].Count, db[k].Count); i++) Diff(da[k][i], db[k][i], path + "/" + k, o);
        }
        string ta = string.Concat(a.ChildNodes.OfType<XmlText>().Select(t => t.Value.Trim())), tb = string.Concat(b.ChildNodes.OfType<XmlText>().Select(t => t.Value.Trim()));
        if (ta != tb) o.Add(path + " texto: " + ta + " / " + tb);
    }
    static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--map")
        {
            // --map <tema> <entrada.bin> <saida.bin>: pixels ARGB (int32 little-endian) pelo M() do tema (previas/testes de icones)
            Tema tm = Temas.Achar(args[1]);
            byte[] raw = File.ReadAllBytes(args[2]);
            int[] px = new int[raw.Length / 4];
            Buffer.BlockCopy(raw, 0, px, 0, px.Length * 4);
            tm.MapPixels(px);
            Buffer.BlockCopy(px, 0, raw, 0, px.Length * 4);
            File.WriteAllBytes(args[3], raw);
            return 0;
        }
        if (args.Length > 0 && args[0] == "--sintaxe")
        {
            foreach (Tema t in Temas.Todos)
                if (t.Sintaxe != null) foreach (string k in Temas.Tokens) { var c = t.Sintaxe[k]; Console.WriteLine($"{t.Id} {k} #{c.R:X2}{c.G:X2}{c.B:X2}"); }
            return 0;
        }
        string silver = args[0], v1 = args[1], roles = args[2], outDir = args.Length > 3 ? args[3] : null;
        GeradorIsl.RolesConhecidos = new HashSet<string>(File.ReadAllLines(roles).Select(l => l.Trim()).Where(l => l.Length > 0));
        string sx = File.ReadAllText(silver);
        // 1) Next == GxDarkTheme.isl v1
        XmlDocument g = GeradorIsl.Gerar(sx, Temas.Next);
        XmlDocument r = new XmlDocument(); r.Load(v1);
        var difs = new List<string>(); Diff(g.DocumentElement, r.DocumentElement, "", difs);
        foreach (var d in difs) Console.WriteLine("   " + d);
        Ok(difs.Count == 0, "ISL gerado em memoria (tema Next) identico ao GxDarkTheme.isl da v1 (" + GeradorIsl.Adicionados + " roles adicionados)");
        // 2) identidade do Next
        bool id = true; var rnd = new Random(1);
        for (int i = 0; i < 200000; i++) { int c = rnd.Next() | unchecked((int)0xFF000000); if (Temas.Next.M(c) != c) id = false; }
        Ok(id && Temas.Next.MapCss("a{color:#11151C;b:#dce2eb}") == "a{color:#11151C;b:#dce2eb}", "Tema Next: M() e identidade (200.000 cores + CSS)");
        // 3) por tema: luminancia preservada, sem fundo claro, contraste texto/fundo
        Color[] fundos = { C(17,21,28), C(24,31,42), C(36,45,60), C(31,39,52), C(52,72,96), C(44,60,80), C(51,61,71), C(31,78,121), C(40,52,70) };
        Color[] textos = { C(220,226,235), C(240,244,250), C(190,198,210), C(150,157,168), C(91,167,255) };
        foreach (Tema t in Temas.Todos)
        {
            double maxErr = 0; double minC = 99; bool claro = false; string pior = "";
            foreach (var f in fundos) { var m = t.M(f); maxErr = Math.Max(maxErr, Math.Abs(Cor.Luminancia(m) - Cor.Luminancia(f)) / Math.Max(Cor.Luminancia(f), 0.002)); if (Cor.Luminancia(m) > 0.2) claro = true;
                foreach (var x in textos) { double cr = Cor.Contraste(t.M(x), m); if (cr < minC) { minC = cr; pior = Hx(t.M(x)) + " sobre " + Hx(m); } } }
            XmlDocument gt = GeradorIsl.Gerar(sx, t);
            int clarosIsl = 0;
            foreach (XmlNode n in gt.SelectNodes("//*[@backColor]")) { var v = ((XmlElement)n).GetAttribute("backColor"); var p = v.Split(','); if (p.Length == 3) { var c = C(int.Parse(p[0]), int.Parse(p[1]), int.Parse(p[2])); if (Cor.Luminancia(c) > 0.25) clarosIsl++; } }
            Ok(!claro && clarosIsl == 0 && minC >= 3.0, $"Tema {t.Id,-9}: fundos escuros (ISL: {clarosIsl} claros), luminancia preservada (erro rel. max {maxErr:P1}), contraste minimo {minC:F2}:1 ({pior})");
            if (outDir != null) { Directory.CreateDirectory(outDir); File.WriteAllText(Path.Combine(outDir, "isl-" + t.Id + ".isl"), GeradorIsl.ParaTexto(gt));
                File.WriteAllLines(Path.Combine(outDir, "paleta-" + t.Id + ".txt"), new[] { "Editor","Panel","Chrome","Input","Hover","Sel","Border","MenuHot","Accent","Fg","FgBright","FgSoft","FgDim","Link","HotBorder" }
                    .Zip(new[] { C(17,21,28),C(24,31,42),C(36,45,60),C(31,39,52),C(52,72,96),C(44,60,80),C(51,61,71),C(31,78,121),C(44,141,206),C(220,226,235),C(240,244,250),C(190,198,210),C(138,144,155),C(91,167,255),C(78,104,136) }, (a, b) => a + "=" + Hx(t.M(b)))); }
        }
        Console.WriteLine(falhas == 0 ? "TODOS OS TESTES OK" : falhas + " FALHA(S)");
        return falhas == 0 ? 0 : 1;
    }
    static Color C(int r, int g, int b) => Color.FromArgb(r, g, b);
    static string Hx(Color c) => "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2");
}
