using System; using System.IO; using System.Text; using System.Reflection; using System.Drawing; using System.Drawing.Imaging;
using System.Collections; using System.Collections.Generic; using System.Windows.Forms; using System.Runtime.InteropServices; using System.Threading;
using ActiproSoftware.SyntaxEditor;
public static class T24 {
  public static string Gx, Out, Dir, Dll;
  public static void W(string s) { File.AppendAllText(Out, s + "\r\n"); }
  public static void Hook() { AppDomain.CurrentDomain.AssemblyResolve += R; }
  static Assembly R(object s, ResolveEventArgs e) {
    string n = new AssemblyName(e.Name).Name;
    foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies()) if (a.GetName().Name == n) return a;
    foreach (string d in new string[] { Gx, Path.Combine(Gx, "Packages") }) foreach (string x in new string[] { ".dll", ".exe" }) { string p = Path.Combine(d, n + x); if (File.Exists(p)) return Assembly.LoadFrom(p); }
    return null;
  }
  [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr h, IntPtr hdc, int flags);
  [DllImport("user32.dll")] static extern int GetGuiResources(IntPtr p, int f);
  [DllImport("kernel32.dll")] static extern IntPtr GetCurrentProcess();
  static Bitmap Snap(Control c) { Bitmap b = new Bitmap(c.Width, c.Height); using (Graphics g = Graphics.FromImage(b)) { IntPtr dc = g.GetHdc(); PrintWindow(c.Handle, dc, 2); g.ReleaseHdc(dc); } return b; }
  static Bitmap Screen(Control c) { Rectangle r = c.RectangleToScreen(c.ClientRectangle); Bitmap b = new Bitmap(Math.Max(1, r.Width), Math.Max(1, r.Height)); using (Graphics g = Graphics.FromImage(b)) g.CopyFromScreen(r.Location, Point.Empty, r.Size); return b; }
  static void Pump(int n) { for (int i = 0; i < n; i++) { Application.DoEvents(); Thread.Sleep(20); } }
  static Type Eng, Tog, Jr, Mlp;
  static object Call(Type t, string m, params object[] a) { return t.GetMethod(m, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Invoke(null, a); }
  static object SF(Type t, string n) { if (t == null) return null; FieldInfo f = t.GetField(n, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic); if (f != null) return f.GetValue(null); PropertyInfo p = t.GetProperty(n, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic); return p == null ? null : p.GetValue(null, null); }
  static string Hex(Color c) { return c.IsEmpty ? "empty" : c.IsKnownColor ? c.Name : string.Format("#{0:X2}{1:X2}{2:X2}", c.R, c.G, c.B); }
  static string H6(Color c) { return string.Format("{0:X2}{1:X2}{2:X2}", c.R, c.G, c.B); }
  static bool On() { return (bool)SF(Eng, "On"); }
  static int Gdi() { return GetGuiResources(GetCurrentProcess(), 0); }
  static void Gc() { for (int i = 0; i < 3; i++) { GC.Collect(); GC.WaitForPendingFinalizers(); } Pump(2); }

  static Form f; static SyntaxEditor se1, se2; static ListBox lb; static Form pop;
  const string HtmlText = "<html>\r\n<body>\r\n<div class=\"x\">Ola &amp; mundo</div>\r\n<script type=\"text/javascript\"><![CDATA[\r\n// comentario em JS\r\nfunction gxRel(id) {\r\n  var nome = \"relatorio\", n = 42;\r\n  if (id != null) return this.Show(nome + n);\r\n  return false;\r\n}\r\n]]></script>\r\n<style>\r\n.Titulo { color: red; font-size: 12px; }\r\n</style>\r\n</body>\r\n</html>\r\n";
  const string CssText = "/* tema */\r\n.Form {\r\n  background-color: #FFFFFF;\r\n  font-family: 'Segoe UI', Arial;\r\n  border: 1px solid #333;\r\n}\r\n.Attribute { color: #000080; padding: 4px; }\r\n";

  static void Langs(SyntaxLanguage l, string path, Dictionary<string, string> d, int depth) {
    if (l == null || depth > 4) return;
    string k = path + "/" + l.Key;
    StringBuilder sb = new StringBuilder("Back=" + Hex(l.BackColor) + " ");
    foreach (HighlightingStyle st in l.HighlightingStyles) sb.Append(st.Key + ":" + Hex(st.ForeColor) + "/" + Hex(st.BackColor) + " ");
    d[k] = sb.ToString();
    for (int i = 0; i < l.ChildLanguages.Count; i++) Langs(l.ChildLanguages[i], k, d, depth + 1);
  }
  static Dictionary<string, string> State() {
    Dictionary<string, string> d = new Dictionary<string, string>();
    Langs(se1.Document.Language, "se1", d, 0); Langs(se2.Document.Language, "se2", d, 0);
    d["se1.ed"] = Hex(se1.BackColor) + " " + Hex(se1.ForeColor) + " " + (se1.Renderer == null ? "null" : se1.Renderer.GetType().Name);
    d["se2.ed"] = Hex(se2.BackColor) + " " + Hex(se2.ForeColor) + " " + (se2.Renderer == null ? "null" : se2.Renderer.GetType().Name);
    if (lb != null && !lb.IsDisposed) { d["lb"] = Hex(lb.BackColor) + " " + Hex(lb.ForeColor); Form pf = lb.FindForm(); if (pf != null) d["lb.form"] = Hex(pf.BackColor) + " " + Hex(pf.ForeColor); }
    return d;
  }
  static int Diff(string tag, Dictionary<string, string> a, Dictionary<string, string> b, int show) {
    int n = 0; List<string> keys = new List<string>(a.Keys); foreach (string k in b.Keys) if (!a.ContainsKey(k)) keys.Add(k);
    foreach (string k in keys) { string x, y; a.TryGetValue(k, out x); b.TryGetValue(k, out y); if (x == y) continue; n++; if (n <= show) W("  DIF " + tag + " " + k + "\r\n      antes: " + x + "\r\n      agora: " + y); }
    return n;
  }
  static string StyleOf(SyntaxLanguage l, string key) { foreach (HighlightingStyle st in l.HighlightingStyles) if (st.Key == key) return H6(st.ForeColor); return "-"; }
  static SyntaxLanguage Child(SyntaxLanguage l, string key) { if (l == null) return null; if (l.Key == key) return l; for (int i = 0; i < l.ChildLanguages.Count; i++) { SyntaxLanguage c = Child(l.ChildLanguages[i], key); if (c != null) return c; } return null; }

  static void Passes(int n) { for (int k = 0; k < n; k++) { Call(Eng, "Pass"); Pump(3); } Pump(3); }
  static void WalkAll() { foreach (Form x in OpenForms()) { try { Call(Eng, "Walk", x, 0); } catch { } } }
  static List<Form> OpenForms() { List<Form> l = new List<Form>(); foreach (Form x in Application.OpenForms) l.Add(x); return l; }

  // conta pixels por cor exata (ex.: WhiteSmoke) e pixels claros "de fundo" num bitmap
  static int CountColor(Bitmap b, Rectangle r, Color c) { int n = 0; r.Intersect(new Rectangle(Point.Empty, b.Size)); for (int y = r.Top; y < r.Bottom; y++) for (int x = r.Left; x < r.Right; x++) if (b.GetPixel(x, y).ToArgb() == c.ToArgb()) n++; return n; }
  static int CountLight(Bitmap b, Rectangle r) { int n = 0; r.Intersect(new Rectangle(Point.Empty, b.Size)); for (int y = r.Top; y < r.Bottom; y++) for (int x = r.Left; x < r.Right; x++) { Color p = b.GetPixel(x, y); if (p.R > 225 && p.G > 225 && p.B > 225) n++; } return n; }
  static string Dominant(Bitmap b, Rectangle r) { Dictionary<int, int> h = new Dictionary<int, int>(); r.Intersect(new Rectangle(Point.Empty, b.Size)); for (int y = r.Top; y < r.Bottom; y++) for (int x = r.Left; x < r.Right; x++) { int v = b.GetPixel(x, y).ToArgb(); int c; h.TryGetValue(v, out c); h[v] = c + 1; } int best = 0, bc = 0; foreach (KeyValuePair<int, int> kv in h) if (kv.Value > bc) { bc = kv.Value; best = kv.Key; } int tot = Math.Max(1, r.Width * r.Height); return H6(Color.FromArgb(best)) + "(" + (100 * bc / tot) + "%)"; }

  // linhas do texto (em pixels do editor) onde esta o bloco <script> CDATA
  static Rectangle ScriptBand() {
    try { Rectangle a = se1.SelectedView.GetCharacterBounds(se1.Document.Lines[5].StartOffset); Rectangle b = se1.SelectedView.GetCharacterBounds(se1.Document.Lines[9].StartOffset); return Rectangle.FromLTRB(a.Left + 40, a.Top, se1.ClientSize.Width - 30, b.Bottom); } catch (Exception ex) { W("ScriptBand: " + ex.Message); return new Rectangle(80, 80, 300, 60); }
  }

  static string Editors(string tag) {
    for (int w = 0; w < 3; w++) { f.Refresh(); Pump(3); using (Bitmap x = Snap(se1)) { } using (Bitmap x = Snap(se2)) { } }
    StringBuilder sb = new StringBuilder();
    using (Bitmap s = Snap(se1)) {
      s.Save(Path.Combine(Dir, "t24-se1-" + tag + ".png"));
      Rectangle band = ScriptBand();
      sb.Append("se1 bloco CDATA " + band + ": WhiteSmoke=" + CountColor(s, band, Color.WhiteSmoke) + " pixels claros=" + CountLight(s, band) + " cor dominante=" + Dominant(s, band));
    }
    using (Bitmap s = Snap(se2)) { s.Save(Path.Combine(Dir, "t24-se2-" + tag + ".png")); Rectangle r = new Rectangle(60, 4, se2.ClientSize.Width - 90, se2.ClientSize.Height - 30); sb.Append(" | se2 CSS: pixels claros=" + CountLight(s, r) + " cor dominante=" + Dominant(s, r)); }
    return sb.ToString();
  }
  static string ListPix(string tag) {
    if (pop == null || !pop.Visible || lb == null) return "lista nao visivel";
    lb.Invalidate(); lb.Update(); Pump(3);
    using (Bitmap s = Snap(lb)) {
      s.Save(Path.Combine(Dir, "t24-list-" + tag + ".png")); using (Bitmap pp = Snap(pop)) pp.Save(Path.Combine(Dir, "t24-pop-" + tag + ".png"));
      int ih = lb.ItemHeight; int sel = lb.SelectedIndex;
      Rectangle selR = sel >= 0 ? lb.GetItemRectangle(sel) : Rectangle.Empty; Rectangle oth = lb.GetItemRectangle(sel == 0 ? 1 : 0);
      Rectangle selText = Rectangle.FromLTRB(selR.Left + ih + 5, selR.Top + 1, selR.Right - 2, selR.Bottom - 1), othText = Rectangle.FromLTRB(oth.Left + ih + 5, oth.Top + 1, oth.Right - 2, oth.Bottom - 1);
      Color edge = s.GetPixel(Math.Min(s.Width - 1, selR.Right - 1), selR.Top + ih / 2);
      return "item=" + ih + "px sel=" + sel + " fundo item=" + Dominant(s, othText) + " fundo selecionado=" + Dominant(s, selText) + " borda sel=" + H6(edge) + " claros(item nao sel)=" + CountLight(s, othText) + " / pintados pelo tema=" + SF(Mlp, "Drawn");
    }
  }

  public static void Run() {
    if (Environment.GetEnvironmentVariable("T16_DPIAWARE") == "1") SetProcessDPIAware();
    Application.EnableVisualStyles();
    // licenca do Actipro em tempo de execucao: vem do recurso *.licenses de um assembly carregado (como no GeneXus)
    foreach (string an in new string[] { "Artech.Common.Controls", "Artech.Core.UI", "Artech.FrameworkDE" }) { try { Assembly.Load(an); } catch (Exception ex) { W("load " + an + ": " + ex.Message); } }
    try { Assembly.LoadFrom(Path.Combine(Gx, "GeneXus.exe")); } catch (Exception ex) { W("load GeneXus.exe: " + ex.Message); }
    Assembly ea = Assembly.LoadFrom(Path.Combine(Dir, Dll)); Eng = ea.GetType("GxDarkTheme.DarkEngine"); Tog = ea.GetType("GxDarkTheme.ToggleButton"); Jr = ea.GetType("GxDarkTheme.Journal"); Mlp = ea.GetType("GxDarkTheme.MemberListPainter");
    W("dll=" + Dll + " MemberListPainter=" + (Mlp != null));
    string pk = Path.Combine(Gx, "Packages");
    f = new Form(); f.Text = "T24"; f.StartPosition = FormStartPosition.Manual; f.Location = new Point(40, 40); f.Size = new Size(900, 640);
    se1 = new SyntaxEditor(); se1.Dock = DockStyle.Top; se1.Height = 360; se2 = new SyntaxEditor(); se2.Dock = DockStyle.Fill;
    f.Controls.Add(se2); f.Controls.Add(se1);
    se1.Document.LoadLanguageFromXml(Path.Combine(pk, "HTMLDefinition.xml"), 0); se1.Document.Text = HtmlText;
    se2.Document.Text = CssText;
    W("passo: f.Show(); Pump(10);");f.Show(); Pump(10);
    W("se1 linguagem=" + se1.Document.Language.Key + " filhas: " + Kids(se1.Document.Language));
    Dictionary<string, string> L0pre = State();
    // CSS carregado depois (como a previa CSS do Theme: LoadLanguageFromXml tardio) -> primeiro no claro para o estado L0
    W("passo: se2.Document.LoadLanguageFromX");se2.Document.LoadLanguageFromXml(Path.Combine(pk, "CSSDefinition.xml"), 0); Pump(4);
    // auto-completar
    ImageList il = SyntaxEditor.ReflectionImageList;
    IntelliPromptMemberList ml = se1.IntelliPrompt.MemberList; ml.ImageList = il; ml.Clear();
    string[] names = { "AttachEvent", "Caption", "Class", "Enabled", "GetValue", "Height", "Refresh", "SetFocus", "Show", "Visible" };
    for (int i = 0; i < names.Length; i++) ml.Add(new IntelliPromptMemberListItem(names[i], i * 3 % Math.Max(1, il.Images.Count), "descricao " + names[i]));
    W("passo: se1.Focus();");se1.Focus(); se1.SelectedView.Selection.SelectRange(se1.Document.Lines[7].StartOffset + 22, 0); Pump(3);
    W("passo: try { ml.Show(); }");try { ml.Show(); } catch (Exception ex) { W("MemberList.Show: " + ex.Message); }
    W("passo: popup");Pump(10);
    try { object cl = typeof(IntelliPromptMemberList).GetField("m", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ml); pop = cl as Form; foreach (Control c in pop.Controls) if (c is ListBox) lb = (ListBox)c; } catch (Exception ex) { W("popup: " + ex.Message); }
    if (lb != null) { lb.SelectedIndex = 3; Pump(4); }
    W("popup=" + (pop == null ? "null" : pop.GetType().FullName + " visivel=" + pop.Visible + " " + pop.Bounds) + " lista=" + (lb == null ? "null" : lb.GetType().FullName + " itens=" + lb.Items.Count + " DrawMode=" + lb.DrawMode));
    W("passo: Dictionary<string, string> L0 ");Dictionary<string, string> L0 = State();
    W("L0 editores: " + Editors("L0")); string lpL0 = ListPix("L0"); W("L0 lista: " + lpL0);
    W("L0 JScript ReservedWordStyle=" + StyleOf(Child(se1.Document.Language, "JScript"), "ReservedWordStyle") + " CSS PropertyNameStyle(se2)=" + StyleOf(se2.Document.Language, "PropertyDefaultStyle"));
    // escuro
    W("passo: Call(Eng, 'Start');");Call(Eng, "Start");
    for (int k = 0; k < 4; k++) { Call(Eng, "Walk", f, 0); Call(Eng, "Pass"); Pump(4); }
    Passes(3);
    W("depois de Start/Pass (sem Walk na lista): painter ligado em " + SF(Mlp, "Count") + " lista(s)");
    WalkAll(); Passes(3);
    W("depois de Walk de todas as janelas: painter ligado em " + SF(Mlp, "Count") + " lista(s)");
    Dictionary<string, string> D1 = State();
    W("D1 vs L0: " + Diff("D1~L0", L0, D1, 0) + " propriedades diferentes");
    foreach (KeyValuePair<string, string> kv in D1) W("  D1 " + kv.Key + " = " + kv.Value);
    W("D1 editores: " + Editors("D1")); W("D1 lista: " + ListPix("D1"));
    SyntaxLanguage js = Child(se1.Document.Language, "JScript");
    W("D1 JScript: Back=" + (js == null ? "?" : Hex(js.BackColor)) + " palavra-chave=" + StyleOf(js, "ReservedWordStyle") + " string=" + StyleOf(js, "StringDefaultStyle") + " comentario=" + StyleOf(js, "CommentDefaultStyle") + " numero=" + StyleOf(js, "NumberStyle") + " identificador=" + StyleOf(js, "DefaultStyle"));
    try { using (Bitmap s = Snap(f)) s.Save(Path.Combine(Dir, "t24-form-D1.png")); } catch { }
    int gdi2 = 0; int badL = 0, badD = 0, badLp = 0, badDp = 0; string lp0 = lpL0, lpD = null;
    for (int cyc = 1; cyc <= 20; cyc++) {
      Call(Eng, "Toggle"); Passes(3);
      if (On()) W("ERRO: ainda escuro (ciclo " + cyc + ")");
      Dictionary<string, string> Lc = State(); int dp = Diff("L" + cyc + "~L0", L0, Lc, cyc <= 2 || cyc == 20 ? 30 : 5); if (dp > 0) badL++;
      string lpl = ListPix("L" + cyc); if (StripDrawn(lpl) != StripDrawn(lp0)) { badLp++; if (badLp <= 3) W("  lista claro ciclo " + cyc + ": " + lpl + " (L0: " + lp0 + ")"); }
      if (cyc == 1 || cyc == 20) { W("L" + cyc + " editores: " + Editors("L" + cyc)); W("L" + cyc + " lista: " + lpl); W("L" + cyc + " JScript palavra-chave=" + StyleOf(Child(se1.Document.Language, "JScript"), "ReservedWordStyle") + " Back=" + Hex(Child(se1.Document.Language, "JScript").BackColor)); }
      Call(Eng, "Toggle"); for (int k = 0; k < 2; k++) { Call(Eng, "Walk", f, 0); Call(Eng, "Pass"); Pump(3); } WalkAll(); Passes(3);
      if (!On()) W("ERRO: ainda claro (ciclo " + cyc + ")");
      Dictionary<string, string> Dc = State(); int dd = Diff("D" + cyc + "~D1", D1, Dc, cyc <= 2 || cyc == 20 ? 30 : 5); if (dd > 0) badD++;
      string lpd = ListPix("D" + (cyc + 1)); if (lpD == null) lpD = ListPix("D1x"); if (StripDrawn(lpd) != StripDrawn(lpD)) { badDp++; if (badDp <= 3) W("  lista escuro ciclo " + cyc + ": " + lpd + " (D1: " + lpD + ")"); }
      if (cyc == 20) { W("D21 editores: " + Editors("D21")); W("D21 lista: " + lpd); try { using (Bitmap s = Snap(f)) s.Save(Path.Combine(Dir, "t24-form-D21.png")); } catch { } }
      if (cyc == 2) { Gc(); gdi2 = Gdi(); W("GDI apos ciclo 2: " + gdi2 + " Journal=" + SF(Jr, "Count")); }
    }
    Gc(); W("GDI apos ciclo 20: " + Gdi() + " (delta " + (Gdi() - gdi2) + ") Journal=" + SF(Jr, "Count"));
    W("RESUMO: ciclos claro com difs de propriedade=" + badL + " lista claro diferente=" + badLp + " | ciclos escuro com difs=" + badD + " lista escura diferente=" + badDp);
    // CSS carregado com o escuro ja ligado (objeto aberto depois)
    se2.Document.LoadLanguageFromXml(Path.Combine(pk, "CSSDefinition.xml"), 0); Pump(3); Passes(3); WalkAll(); Passes(2);
    W("CSS recarregado no escuro: PropertyNameStyle=" + StyleOf(se2.Document.Language, "PropertyDefaultStyle") + " ValueDefaultStyle=" + StyleOf(se2.Document.Language, "ValueDefaultStyle") + " DefaultStyle(seletor)=" + StyleOf(se2.Document.Language, "DefaultStyle") + " | " + Editors("D22"));
    Call(Eng, "Toggle"); Passes(3); W("claro final: " + Editors("Lfim") + " | lista: " + ListPix("Lfim"));
    try { ml.Abort(); } catch { }
    f.Close(); W("fim");
  }
  static string StripDrawn(string s) { int i = s.IndexOf(" / pintados"); return i < 0 ? s : s.Substring(0, i); }
  static string Kids(SyntaxLanguage l) { StringBuilder sb = new StringBuilder(); for (int i = 0; i < l.ChildLanguages.Count; i++) sb.Append(l.ChildLanguages[i].Key + "(" + Hex(l.ChildLanguages[i].BackColor) + ") "); return sb.ToString(); }
}
