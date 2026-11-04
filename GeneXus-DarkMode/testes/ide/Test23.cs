using System.ComponentModel; using System; using System.IO; using System.Text; using System.Reflection; using System.Drawing; using System.Drawing.Imaging; using System.Resources;
using System.Collections; using System.Collections.Generic; using System.Windows.Forms; using System.Runtime.InteropServices; using System.Threading; using System.Xml;
using Infragistics.Win; using Infragistics.Win.UltraWinToolbars; using Infragistics.Win.UltraWinTree;
public class FakePartFrame : Artech.FrameworkDE.Controls.GxTabPage {
  private readonly ImageList m_ImageList = new ImageList();
  public static int Px = 20;
  public FakePartFrame(string n) : base(n) { Title = n; }
  public void SetDefault(bool isDefault) {
    Type im = Assembly.Load("Artech.Core.UI").GetType("Artech.Core.UI.Resources.Images", true);
    Func<string, Icon> I = (s) => (Icon)im.GetProperty(s, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).GetValue(null, null);
    Bitmap val = new Bitmap(Px, Px);
    using (Graphics g = Graphics.FromImage(val)) { g.DrawIcon(I("Default"), new Rectangle(0, 0, Px, Px)); g.DrawIcon(isDefault ? I("DefaultEnabledOverlay") : I("DefaultDisabledOverlay"), new Rectangle(0, 0, Px, Px)); }
    m_ImageList.Images.Clear(); m_ImageList.ImageSize = new Size(Px, Px); m_ImageList.Images.Add(val);
    ImageList = m_ImageList; ImageIndex = 0;
  }
}
public static class T23 {
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
  static void Pump(int n) { for (int i = 0; i < n; i++) { Application.DoEvents(); Thread.Sleep(20); } }
  static ResourceManager RM(string asm, string type) {
    Assembly a = Assembly.Load(asm); Type t = a.GetType(type);
    if (t == null) return new ResourceManager(type, a);
    PropertyInfo p = t.GetProperty("ResourceManager", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
    if (p == null || !typeof(ResourceManager).IsAssignableFrom(p.PropertyType)) return new ResourceManager(type, a);
    return (ResourceManager)p.GetValue(null, null);
  }
  static Image Img(string asm, string type, string key) { object o = RM(asm, type).GetObject(key); if (o == null) { W("recurso ausente: " + key); return new Bitmap(16, 16); } Icon i = o as Icon; if (i != null) { using (Icon x = new Icon(i, 16, 16)) return x.ToBitmap(); } return new Bitmap((Image)o); }
  const string CU = "Artech.Core.UI", CI = "Artech.Core.UI.Resources.Images", GR = "Artech.Genexus.UI.Resources", GI = "Artech.Genexus.UI.Resources.Images", CP = "Artech.Core.UI.Properties.Resources";
  static Type Eng, Tog, Jr, Ico;
  static object Call(Type t, string m, params object[] a) { return t.GetMethod(m, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Invoke(null, a); }
  static object SF(Type t, string n) { FieldInfo f = t.GetField(n, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic); if (f != null) return f.GetValue(null); PropertyInfo p = t.GetProperty(n, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic); return p == null ? null : p.GetValue(null, null); }
  static string Hex(Color c) { return c.IsEmpty ? "empty" : c.IsKnownColor ? c.Name : string.Format("#{0:X2}{1:X2}{2:X2}{3:X2}", c.A, c.R, c.G, c.B); }
  static string PixHash(Image im) {
    if (im == null) return "null";
    try { using (Bitmap b = new Bitmap(im)) { long h = 17; for (int y = 0; y < b.Height; y++) for (int x = 0; x < b.Width; x++) h = h * 31 + b.GetPixel(x, y).ToArgb(); return b.Width + "x" + b.Height + ":" + h.ToString("X"); } } catch (Exception ex) { return "err " + ex.GetType().Name; }
  }
  static string ListHash(ImageList l) { if (l == null) return "null"; StringBuilder sb = new StringBuilder(l.ColorDepth + "/" + l.ImageSize + "/n" + l.Images.Count); for (int i = 0; i < l.Images.Count; i++) sb.Append("|" + PixHash(l.Images[i])); return sb.ToString(); }
  static object P(object o, string n) { if (o == null) return null; PropertyInfo p = null; try { p = o.GetType().GetProperty(n, BindingFlags.Instance | BindingFlags.Public); } catch (AmbiguousMatchException) { } return p == null ? null : p.GetValue(o, null); }
  static void Style(Dictionary<string, string> d, string k, DataGridViewCellStyle s) { if (s == null) { d[k] = "null"; return; } d[k] = Hex(s.BackColor) + " " + Hex(s.ForeColor) + " " + Hex(s.SelectionBackColor) + " " + Hex(s.SelectionForeColor) + " " + s.Font; }
  static void App(Dictionary<string, string> d, string k, object ap) {
    if (ap == null) return;
    StringBuilder sb = new StringBuilder();
    foreach (string n in new string[] { "BackColor", "BackColor2", "ForeColor", "BorderColor", "BackGradientStyle", "GradientMode", "Font" }) { object v = null; try { v = P(ap, n); } catch { } if (v != null) sb.Append(n + "=" + (v is Color ? Hex((Color)v) : v.ToString()) + " "); }
    object op = null; try { op = P(ap, "Options"); } catch { }
    if (op != null) foreach (string n in new string[] { "UseBackColor", "UseForeColor", "UseBorderColor", "UseFont" }) { object v = P(op, n); if (v != null) sb.Append(n + "=" + v + " "); }
    d[k] = sb.ToString();
  }
  static void Dump(Control c, string path, Dictionary<string, string> d, Control skip) {
    if (c == null || c == skip || c.GetType().Name == "ToggleButton") return;
    string k = path + "/" + c.GetType().Name + (string.IsNullOrEmpty(c.Name) ? "" : "#" + c.Name);
    StringBuilder sb = new StringBuilder();
    sb.Append("Back=" + Hex(c.BackColor) + " Fore=" + Hex(c.ForeColor) + " Font=" + c.Font + " BgImg=" + (c.BackgroundImage == null ? "-" : "img"));
    foreach (string n in new string[] { "BorderStyle", "FlatStyle", "UseVisualStyleBackColor", "Appearance", "RenderMode", "GridColor", "BackgroundColor", "EnableHeadersVisualStyles", "LineColor", "OwnerDraw", "DrawMode", "ColorTab", "HotTextColor", "TextColor", "TextInactiveColor", "ImageIndex", "LookAndFeel" }) {
      try { PropertyInfo p = null; try { p = c.GetType().GetProperty(n, BindingFlags.Instance | BindingFlags.Public); } catch (AmbiguousMatchException) { continue; } if (p == null || !p.CanRead || p.GetIndexParameters().Length > 0) continue; object v = p.GetValue(c, null); sb.Append(" " + n + "=" + (v is Color ? Hex((Color)v) : v == null ? "null" : v.ToString())); } catch { }
    }
    foreach (string n in new string[] { "Image", "ImageList", "StateImageList", "SmallImageList", "SelectImageList", "StateImageList" }) {
      try { PropertyInfo p = null; try { p = c.GetType().GetProperty(n, BindingFlags.Instance | BindingFlags.Public); } catch (AmbiguousMatchException) { continue; } if (p == null) continue; object v = p.GetValue(c, null);
        if (v is Image) d[k + "." + n] = PixHash((Image)v); else if (v is ImageList) d[k + "." + n] = ListHash((ImageList)v); else if (v == null) d[k + "." + n] = "null"; } catch { }
    }
    d[k] = sb.ToString();
    ButtonBase bb = c as ButtonBase; if (bb != null) d[k + ".Flat"] = Hex(bb.FlatAppearance.BorderColor) + " " + bb.FlatAppearance.BorderSize + " " + Hex(bb.FlatAppearance.MouseOverBackColor) + " " + Hex(bb.FlatAppearance.MouseDownBackColor) + " " + Hex(bb.FlatAppearance.CheckedBackColor);
    DataGridView g = c as DataGridView;
    if (g != null) { Style(d, k + ".Def", g.DefaultCellStyle); Style(d, k + ".Alt", g.AlternatingRowsDefaultCellStyle); Style(d, k + ".ColHdr", g.ColumnHeadersDefaultCellStyle); Style(d, k + ".RowHdr", g.RowHeadersDefaultCellStyle); Style(d, k + ".Rows", g.RowsDefaultCellStyle); for (int i = 0; i < g.Columns.Count; i++) Style(d, k + ".Col" + i, g.Columns[i].DefaultCellStyle); }
    ToolStrip ts = c as ToolStrip;
    if (ts != null) { d[k + ".Renderer"] = ts.Renderer.GetType().FullName + " " + ts.RenderMode; for (int i = 0; i < ts.Items.Count; i++) { ToolStripItem it = ts.Items[i]; d[k + ".item" + i] = Hex(it.BackColor) + " " + Hex(it.ForeColor) + " " + PixHash(it.Image); } }
    UltraTree ut = c as UltraTree;
    if (ut != null) { App(d, k + ".App", ut.Appearance); int i = 0; foreach (UltraTreeNode n in ut.Nodes[0].Nodes) { d[k + ".node" + i] = PixHash(n.LeftImages.Count > 0 ? n.LeftImages[0] as Image : null) + " " + PixHash(n.Override.NodeAppearance.Image as Image); i++; } }
    if (c.GetType().FullName == "DevExpress.XtraTreeList.TreeList") {
      object aps = P(c, "Appearance");
      foreach (PropertyInfo p in aps.GetType().GetProperties()) { if (p.GetIndexParameters().Length > 0) continue; object ap = null; try { ap = p.GetValue(aps, null); } catch { } if (ap != null && ap.GetType().Name.StartsWith("Appearance")) App(d, k + ".ap." + p.Name, ap); }
    }
    if (c.GetType().FullName.StartsWith("ActiproSoftware.SyntaxEditor.SyntaxEditor")) {
      try { object r = P(c, "Renderer"); d[k + ".Renderer"] = r == null ? "null" : r.GetType().FullName; } catch { }
      try { object doc = P(c, "Document"); object lang = P(doc, "Language"); object hs = P(lang, "HighlightingStyles"); int i = 0; StringBuilder sb2 = new StringBuilder(); foreach (object st in (IEnumerable)hs) { sb2.Append(P(st, "Key") + ":" + Hex((Color)P(st, "ForeColor")) + "/" + Hex((Color)P(st, "BackColor")) + " "); i++; } d[k + ".styles"] = "n=" + i + " " + sb2; } catch (Exception ex) { d[k + ".styles"] = "err " + ex.Message; }
    }
    int j = 0; foreach (Control kd in c.Controls) { if (kd.GetType().Name == "ToggleButton") continue; Dump(kd, k + "[" + (j++) + "]", d, skip); }
  }
  static Dictionary<string, string> Globals() {
    Dictionary<string, string> d = new Dictionary<string, string>();
    d["TS.Renderer"] = ToolStripManager.Renderer.GetType().FullName + " " + ToolStripManager.RenderMode;
    d["O2010"] = Office2010ColorTable.ColorScheme + " " + Office2010ColorTable.CustomBlendColor; d["O2007"] = Office2007ColorTable.ColorScheme + " " + Office2007ColorTable.CustomBlendColor;
    Assembly iw = typeof(UIElement).Assembly;
    foreach (string tn in new string[] { "Infragistics.Win.Office2010ColorTable", "Infragistics.Win.Office2007ColorTable", "Infragistics.Win.Office2003ColorTable", "Infragistics.Win.VisualStudio2005ColorTable" }) {
      Type t = iw.GetType(tn); if (t == null) continue; long h = 0; int n = 0;
      foreach (FieldInfo f in t.GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)) { if (f.FieldType != typeof(Color[])) continue; Color[] a = f.GetValue(null) as Color[]; if (a == null) continue; foreach (Color c in a) h = h * 31 + c.ToArgb(); n++; }
      d["IG." + t.Name] = n + ":" + h.ToString("X");
    }
    return d;
  }
  static int Diff(string tag, Dictionary<string, string> a, Dictionary<string, string> b, int show) {
    int n = 0; List<string> keys = new List<string>(a.Keys); foreach (string k in b.Keys) if (!a.ContainsKey(k)) keys.Add(k);
    foreach (string k in keys) { string x, y; a.TryGetValue(k, out x); b.TryGetValue(k, out y); if (x == y) continue; n++; if (n <= show) W("  DIF " + tag + " " + k + "\r\n      antes: " + x + "\r\n      agora: " + y); }
    return n;
  }
  static Rectangle Mask = Rectangle.Empty;
  static int PixDiff(Bitmap a, Bitmap b, out Rectangle box) {
    int n = 0; box = Rectangle.Empty; int x0 = int.MaxValue, y0 = int.MaxValue, x1 = -1, y1 = -1;
    if (a.Size != b.Size) return -1;
    BitmapData da = a.LockBits(new Rectangle(Point.Empty, a.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb), db = b.LockBits(new Rectangle(Point.Empty, b.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
    int[] pa = new int[a.Width * a.Height], pb = new int[pa.Length]; Marshal.Copy(da.Scan0, pa, 0, pa.Length); Marshal.Copy(db.Scan0, pb, 0, pb.Length); a.UnlockBits(da); b.UnlockBits(db);
    for (int y = 0; y < a.Height; y++) for (int x = 0; x < a.Width; x++) { if (Mask.Contains(x, y)) continue; if (pa[y * a.Width + x] != pb[y * a.Width + x]) { n++; x0 = Math.Min(x0, x); y0 = Math.Min(y0, y); x1 = Math.Max(x1, x); y1 = Math.Max(y1, y); } }
    if (n > 0) box = Rectangle.FromLTRB(x0, y0, x1 + 1, y1 + 1);
    return n;
  }
  static Form f; static UltraToolbarsManager tm;
  static void AddBtn(UltraToolbar tb, string key, Image img) { ButtonTool bt = new ButtonTool(key); bt.SharedProps.Caption = key; bt.SharedProps.AppearancesSmall.Appearance.Image = img; bt.SharedProps.DisplayStyle = ToolDisplayStyle.ImageOnlyOnToolbars; tm.Tools.Add(bt); tb.Tools.AddTool(key); }
  static Form Small(string name) {
    Form x = new Form(); x.Name = name; x.StartPosition = FormStartPosition.Manual; x.Location = new Point(-6000, -5000); x.ClientSize = new Size(420, 200); x.ShowInTaskbar = false; x.FormBorderStyle = FormBorderStyle.None;
    Panel p = new Panel(); p.Name = "p"; p.Dock = DockStyle.Fill; x.Controls.Add(p);
    Button b = new Button(); b.Name = "b"; b.Text = "OK"; b.SetBounds(8, 8, 90, 30); p.Controls.Add(b);
    TextBox t = new TextBox(); t.Name = "t"; t.Text = "texto"; t.SetBounds(110, 10, 150, 26); p.Controls.Add(t);
    Label l = new Label(); l.Name = "l"; l.Text = "Rotulo"; l.SetBounds(270, 12, 100, 22); p.Controls.Add(l);
    DataGridView g = new DataGridView(); g.Name = "g"; g.SetBounds(8, 50, 400, 140); g.Columns.Add("a", "Nome"); g.Columns.Add("b", "Tipo"); g.Rows.Add("Cliente", "Trn"); g.Rows.Add("Proc", "Prc"); p.Controls.Add(g);
    return x;
  }
  static void Build() {
    f = new Form(); f.Name = "main"; f.StartPosition = FormStartPosition.Manual; f.Location = new Point(-6000, -6000); f.ClientSize = new Size(1150, 800); f.ShowInTaskbar = false; f.FormBorderStyle = Environment.GetEnvironmentVariable("T23_CAPTION") == "0" ? FormBorderStyle.None : FormBorderStyle.Sizable; f.Text = "T23";
    f.Font = new Font("Segoe UI", 9f, GraphicsUnit.Point);
    tm = (UltraToolbarsManager)Activator.CreateInstance(Assembly.Load("Artech.FrameworkDE").GetType("Artech.FrameworkDE.ToolBarManager", true)); tm.DockWithinContainer = f;
    UltraToolbar mb = new UltraToolbar("Menu"); tm.Toolbars.Add(mb); mb.IsMainMenuBar = true; mb.DockedPosition = DockedPosition.Top; mb.DockedRow = 0;
    foreach (string m in new string[] { "File", "Edit", "View", "Insert", "Build", "Tools", "Window", "Help" }) { PopupMenuTool p = new PopupMenuTool("m" + m); p.SharedProps.Caption = m; tm.Tools.Add(p); mb.Tools.AddTool("m" + m); }
    UltraToolbar tb = new UltraToolbar("Std"); tm.Toolbars.Add(tb); tb.DockedPosition = DockedPosition.Top; tb.DockedRow = 1;
    AddBtn(tb, "Save", Img(CU, CI, "ActionSave")); AddBtn(tb, "Copy", Img(CU, CI, "ActionCopy")); AddBtn(tb, "Undo", Img(CU, CI, "ActionUndo")); AddBtn(tb, "Find", Img(CU, CI, "ActionFind"));
    AddBtn(tb, "Build", Img(GR, GI, "ActionBuild")); AddBtn(tb, "Run", Img(GR, GI, "ActionRun")); AddBtn(tb, "Delete", Img(CU, CP, "ActionDelete"));
    int top = 80;
    UltraTree tree = new UltraTree(); tree.Name = "tree"; tree.SetBounds(4, top, 230, 260);
    UltraTreeNode root = tree.Nodes.Add("kb", "Root Module"); root.Override.NodeAppearance.Image = Img(GR, GI, "ObjectModule");
    string[][] kids = { new[] { "Folder", "Pasta" }, new[] { "ObjectTransaction", "Cliente" }, new[] { "ObjectProcedure", "ProcCalcula" }, new[] { "ObjectWebpanel", "WPHome" }, new[] { "ObjectDataProvider", "DPMenu" }, new[] { "ObjectTable", "TabCliente" } };
    int i = 0; foreach (string[] k in kids) { UltraTreeNode n = root.Nodes.Add("n" + i, k[1]); if (i % 2 == 0) n.LeftImages.Add(Img(GR, GI, k[0])); else n.Override.NodeAppearance.Image = Img(GR, GI, k[0]); i++; }
    root.Expanded = true; f.Controls.Add(tree);
    DevExpress.XtraTreeList.TreeList tl = new DevExpress.XtraTreeList.TreeList(); tl.Name = "dxtl"; tl.SetBounds(240, top, 250, 200);
    ImageList il = new ImageList(); il.ColorDepth = ColorDepth.Depth8Bit; il.ImageSize = new Size(16, 16);
    string[] ks = { "AttributeKey", "AttributeForeign", "AttributeFormula", "Variable" }; foreach (string k in ks) il.Images.Add(k, Img(GR, GI, k));
    tl.SelectImageList = il; DevExpress.XtraTreeList.Columns.TreeListColumn c0 = tl.Columns.Add(); c0.Caption = "Name"; c0.VisibleIndex = 0; c0.Width = 200;
    tl.BeginUnboundLoad(); for (int j = 0; j < ks.Length; j++) { DevExpress.XtraTreeList.Nodes.TreeListNode nd = tl.AppendNode(new object[] { ks[j] }, null); nd.ImageIndex = j; nd.SelectImageIndex = j; } tl.EndUnboundLoad();
    f.Controls.Add(tl);
    ToolStrip ts = new ToolStrip(); ts.Name = "ts"; ts.Dock = DockStyle.None; ts.Location = new Point(240, top + 210); ts.AutoSize = true; ts.GripStyle = ToolStripGripStyle.Hidden;
    ts.Items.Add(new ToolStripButton("", Img(CU, CP, "StatusWarning_small")) { Name = "warn" }); ts.Items.Add(new ToolStripButton("", Img(CU, CP, "StatusError_small")) { Name = "err" }); ts.Items.Add(new ToolStripButton("", Img(CU, CI, "ActionPrintPreview")) { Name = "prev" });
    f.Controls.Add(ts);
    TabControl tc = new TabControl(); tc.Name = "tabs"; tc.SetBounds(500, top, 640, 260);
    TabPage p1 = new TabPage("Geral"); p1.Name = "p1"; tc.TabPages.Add(p1); TabPage p2 = new TabPage("Arvore"); p2.Name = "p2"; tc.TabPages.Add(p2);
    DataGridView g = new DataGridView(); g.Name = "grid"; g.SetBounds(4, 4, 380, 200); g.Columns.Add("a", "Nome"); g.Columns.Add("b", "Tipo"); g.Columns[1].DefaultCellStyle.ForeColor = Color.DarkBlue;
    for (int r = 0; r < 6; r++) g.Rows.Add("Obj" + r, r % 2 == 0 ? "Transaction" : "Procedure"); p1.Controls.Add(g);
    Button b = new Button(); b.Name = "btn"; b.Text = "Executar"; b.SetBounds(392, 4, 110, 30); p1.Controls.Add(b);
    Button b2 = new Button(); b2.Name = "btnOff"; b2.Text = "Desab."; b2.Enabled = false; b2.SetBounds(392, 40, 110, 30); p1.Controls.Add(b2);
    TextBox tx = new TextBox(); tx.Name = "tx"; tx.Text = "valor"; tx.SetBounds(392, 78, 200, 26); p1.Controls.Add(tx);
    CheckBox cb = new CheckBox(); cb.Name = "chk"; cb.Text = "Opcao"; cb.Checked = true; cb.SetBounds(392, 110, 150, 24); p1.Controls.Add(cb);
    GroupBox gb = new GroupBox(); gb.Name = "grp"; gb.Text = "Grupo"; gb.SetBounds(392, 138, 230, 80); p1.Controls.Add(gb);
    Label lb = new Label(); lb.Name = "lbl"; lb.Text = "Rotulo"; lb.SetBounds(10, 30, 150, 22); gb.Controls.Add(lb);
    TreeView tv = new TreeView(); tv.Name = "tv"; tv.SetBounds(4, 4, 300, 200); tv.CheckBoxes = false;
    ImageList ti = new ImageList(); ti.ColorDepth = ColorDepth.Depth32Bit; ti.ImageSize = new Size(16, 16); ti.Images.Add(Img(GR, GI, "ObjectTransaction")); ti.Images.Add(Img(GR, GI, "ObjectProcedure")); tv.ImageList = ti;
    ImageList si = new ImageList(); si.ColorDepth = ColorDepth.Depth32Bit; si.ImageSize = new Size(16, 16); si.Images.Add(Img(CU, CP, "StatusWarning_small")); si.Images.Add(Img(CU, CP, "StatusError_small")); tv.StateImageList = si;
    TreeNode tn0 = tv.Nodes.Add("Cliente"); tn0.ImageIndex = 0; tn0.StateImageIndex = 0; TreeNode tn1 = tv.Nodes.Add("Proc"); tn1.ImageIndex = 1; tn1.SelectedImageIndex = 1; tn1.StateImageIndex = 1;
    p2.Controls.Add(tv);
    ListView lv = new ListView(); lv.Name = "lv"; lv.View = View.Details; lv.SetBounds(310, 4, 300, 200); lv.Columns.Add("Nome", 140); lv.Columns.Add("Tipo", 120); lv.Items.Add(new ListViewItem(new[] { "A", "Trn" })); lv.Items.Add(new ListViewItem(new[] { "B", "Prc" })); p2.Controls.Add(lv);
    Crownwood.Magic.Controls.TabControl ct = new Crownwood.Magic.Controls.TabControl(); ct.Name = "crown"; ct.Bounds = new Rectangle(4, top + 270, 700, 80);
    ct.TabPages.Add(new Artech.FrameworkDE.Controls.GxTabPage("Structure") { Title = "Structure" }); FakePartFrame wl = new FakePartFrame("Web Layout"); ct.TabPages.Add(wl);
    foreach (string n in new string[] { "Rules", "Events", "Variables" }) ct.TabPages.Add(new Artech.FrameworkDE.Controls.GxTabPage(n) { Title = n });
    ct.SelectedIndex = 1; f.Controls.Add(ct); wl.SetDefault(true);
    Panel pn = new Panel(); pn.Name = "panel"; pn.BorderStyle = BorderStyle.FixedSingle; pn.SetBounds(710, top + 270, 430, 80); Label l2 = new Label(); l2.Name = "l2"; l2.Text = "Painel com texto"; l2.SetBounds(6, 6, 200, 22); pn.Controls.Add(l2); f.Controls.Add(pn);

    // ---- Crownwood como no GeneXus: pai com cor propria (BackColor do TabControl = SystemColors.Control explicito, nao serializado) ----
    UserControl host = new UserControl(); host.Name = "partHost"; host.BackColor = Color.FromArgb(83, 83, 83); host.SetBounds(4, top + 360, 700, 90); f.Controls.Add(host);
    Crownwood.Magic.Controls.TabControl ct2 = new Crownwood.Magic.Controls.TabControl(); ct2.Name = "crown2"; ct2.Appearance = Crownwood.Magic.Controls.TabControl.VisualAppearance.MultiBox; ct2.Dock = DockStyle.Fill;
    foreach (string n in new string[] { "Structure", "Web Layout", "Rules", "Events", "Variables", "Patterns" }) ct2.TabPages.Add(new Artech.FrameworkDE.Controls.GxTabPage(n) { Title = n });
    host.Controls.Add(ct2); ct2.SelectedIndex = 1;
    // ---- Output do GeneXus (GxOutput real; se nao der, copia fiel da barra) ----
    Control outc = null;
    if (Environment.GetEnvironmentVariable("T23_REALOUT") == "1") try { outc = (Control)Activator.CreateInstance(Assembly.Load("Artech.Core.UI").GetType("Artech.Core.UI.Controls.GxOutput", true)); outc.Name = "gxOutput"; W("GxOutput real criado"); }
    catch (Exception ex) { W("GxOutput real falhou (" + (ex.InnerException ?? ex).GetType().Name + ": " + (ex.InnerException ?? ex).Message + "); usando copia da barra"); outc = null; }
    if (outc == null) {
      UserControl uo = new UserControl(); uo.Name = "gxOutput"; uo.BackColor = Color.Transparent;
      ComponentResourceManager rm = new System.ComponentModel.ComponentResourceManager(Assembly.Load("Artech.Core.UI").GetType("Artech.Core.UI.Controls.GxOutput", true));
      ToolStrip ot = new ToolStrip(); ot.Name = "toolStrip1"; ot.GripStyle = ToolStripGripStyle.Hidden;
      try { ot.Renderer = (ToolStripRenderer)Assembly.Load("Artech.FrameworkDE").GetType("Artech.Common.Controls.Style.CustomToolStripRenderer", true).GetMethod("CreateInstance").Invoke(null, null); } catch (Exception ex) { W("CustomToolStripRenderer: " + ex.Message); }
      Func<string, Image> RI = (k) => { try { return rm.GetObject(k + ".Image") as Image; } catch { return null; } };
      ToolStripButton bAuto = new ToolStripButton(); bAuto.Name = "btnAutoScroll"; bAuto.Alignment = ToolStripItemAlignment.Right; bAuto.Checked = true; bAuto.Image = RI("btnAutoScroll") ?? Img(CU, CP, "ActionAutoscroll");
      ToolStripLabel lab = new ToolStripLabel("Show:"); lab.Name = "tboxOutputFrom";
      ToolStripComboBox cbo = new ToolStripComboBox(); cbo.Name = "cboxOutputFrom"; cbo.DropDownStyle = ComboBoxStyle.DropDownList; cbo.Items.Add("General"); cbo.SelectedIndex = 0;
      ToolStripButton bClear = new ToolStripButton(); bClear.Name = "btnClear"; bClear.DisplayStyle = ToolStripItemDisplayStyle.Image; bClear.Image = RI("btnClear");
      ToolStripButton b1 = new ToolStripButton(); b1.Name = "toolStripButton1"; b1.DisplayStyle = ToolStripItemDisplayStyle.Image; b1.Image = RI("toolStripButton1");
      ToolStripButton b4 = new ToolStripButton(); b4.Name = "toolStripButton4"; b4.DisplayStyle = ToolStripItemDisplayStyle.Image; b4.Image = RI("toolStripButton4");
      ToolStripButton bSave = new ToolStripButton(); bSave.Name = "saveLogButton"; bSave.Alignment = ToolStripItemAlignment.Right; bSave.Checked = true; bSave.DisplayStyle = ToolStripItemDisplayStyle.Image; bSave.Image = RI("saveLogButton");
      ToolStripButton b3 = new ToolStripButton(); b3.Name = "toolStripButton3"; b3.Alignment = ToolStripItemAlignment.Right; b3.DisplayStyle = ToolStripItemDisplayStyle.Image; b3.Image = RI("toolStripButton3");
      ot.Items.AddRange(new ToolStripItem[] { bAuto, lab, cbo, new ToolStripSeparator() { Name = "sepOutputFrom" }, bClear, new ToolStripSeparator() { Name = "toolStripSeparator1" }, b1, new ToolStripSeparator() { Name = "toolStripSeparator2", Alignment = ToolStripItemAlignment.Right }, bSave, b3, b4 });
      Panel src = new Panel(); src.Name = "pnlSource"; src.Dock = DockStyle.Fill; uo.Controls.Add(src); uo.Controls.Add(ot); ot.Dock = DockStyle.Top;
      outc = uo;
    }
    outc.SetBounds(710, top + 360, 430, 160); f.Controls.Add(outc);
    if (Environment.GetEnvironmentVariable("T23_ACTIPRO") == "1") {
      try { Type st = Assembly.Load("ActiproSoftware.SyntaxEditor.Net20").GetType("ActiproSoftware.SyntaxEditor.SyntaxEditor", true); Control se = (Control)Activator.CreateInstance(st); se.Name = "syn"; se.SetBounds(4, top + 360, 700, 180); se.Text = "// comentario\r\nFor each Cliente\r\n   &x = ClienteId\r\nEndfor"; f.Controls.Add(se); W("SyntaxEditor criado"); }
      catch (Exception ex) { W("SyntaxEditor falhou: " + ex.GetType().Name + " " + ex.Message); }
    }
    f.Show(); Pump(10);
  }
  static void LoadLightIsl() {
    string p = Environment.GetEnvironmentVariable("GXDARK_LIGHT_ISL"); if (string.IsNullOrEmpty(p) || !File.Exists(p)) { W("sem ISL claro"); return; }
    XmlDocument doc = new XmlDocument(); doc.Load(p); int scaled = 16; using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) scaled = (int)Math.Round(16 * g.DpiY / 96f);
    foreach (XmlNode n in doc.SelectNodes("//sharedObject[@name='ScrollBar']/properties/property")) { int dd; if (n.FirstChild != null && int.TryParse(n.FirstChild.Value, out dd)) n.FirstChild.Value = scaled.ToString(); }
    using (MemoryStream ms = new MemoryStream()) { doc.Save(ms); ms.Position = 0; Infragistics.Win.AppStyling.StyleManager.Load(ms); }
  }
  static void Passes(int n) { for (int k = 0; k < n; k++) { Call(Eng, "Pass"); Pump(3); } foreach (Form x in new List<Form>(OpenForms())) { try { x.Refresh(); } catch { } } Pump(4); }
  static List<Form> OpenForms() { List<Form> l = new List<Form>(); foreach (Form x in Application.OpenForms) l.Add(x); return l; }
  static Dictionary<string, string> State(Form x) { Dictionary<string, string> d = new Dictionary<string, string>(); Dump(x, "", d, null); return d; }
  static Dictionary<string, string> All() { Dictionary<string, string> d = State(f); foreach (KeyValuePair<string, string> kv in Globals()) d[kv.Key] = kv.Value; return d; }
  static bool On() { return (bool)SF(Eng, "On"); }
  static Control Btn() { return (Control)SF(Tog, "Instance"); }
  static Rectangle BtnRect() { Control b = Btn(); if (b == null || !b.Visible) return Rectangle.Empty; Rectangle r = b.RectangleToScreen(b.ClientRectangle); r.Offset(-f.Left, -f.Top); return r; }
  static void Gc() { for (int i = 0; i < 3; i++) { GC.Collect(); GC.WaitForPendingFinalizers(); } Pump(2); }
  static int Gdi() { return GetGuiResources(GetCurrentProcess(), 0); }
  static int User() { return GetGuiResources(GetCurrentProcess(), 1); }
  static void SaveCrop(Bitmap shot, Rectangle r, string name) { if (r.IsEmpty) return; Rectangle rr = Rectangle.Inflate(r, 6, 3); rr.Intersect(new Rectangle(Point.Empty, shot.Size)); using (Bitmap c = shot.Clone(rr, shot.PixelFormat)) c.Save(Path.Combine(Dir, name)); }
  static string ModeInFile() { try { foreach (string l in File.ReadAllLines(Environment.GetEnvironmentVariable("GXDARK_SETTINGS"))) if (l.StartsWith("mode=")) return l; } catch { } return "(sem mode)"; }
  static void Click() { Control b = Btn(); Type bt = b.GetType(); MouseEventArgs e = new MouseEventArgs(MouseButtons.Left, 1, 3, 3, 0);
    bt.GetMethod("OnMouseDown", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(b, new object[] { e }); bt.GetMethod("OnMouseUp", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(b, new object[] { e }); Pump(6); }
  static void Shots(string tag) {
    Rectangle r = BtnRect(); f.Refresh(); Pump(2);
    using (Bitmap s = Snap(f)) { s.Save(Path.Combine(Dir, "t23-" + tag + ".png")); SaveCrop(s, r, "t23-btn-" + tag + ".png"); }
    Call(Tog, "SetHover", true); f.Refresh(); Pump(2); using (Bitmap s = Snap(f)) SaveCrop(s, r, "t23-btn-" + tag + "-hover.png"); Call(Tog, "SetHover", false); f.Refresh(); Pump(2);
  }

  [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }
  [StructLayout(LayoutKind.Sequential)] struct TBIX { public int cb; public RECT title; [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)] public int[] st; [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)] public RECT[] rc; }
  [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int m, IntPtr w, ref TBIX t);
  static Rectangle CloseBtn(Form x) { TBIX t = new TBIX(); t.cb = Marshal.SizeOf(typeof(TBIX)); t.st = new int[6]; t.rc = new RECT[6]; SendMessage(x.Handle, 0x033F, IntPtr.Zero, ref t); RECT r = t.rc[5]; return Rectangle.FromLTRB(r.L, r.T, r.R, r.B); }
  static void Align(string tag) {
    Control b = Btn(); if (b == null) { W("alinhamento " + tag + ": sem botao"); return; }
    Rectangle bs = b.RectangleToScreen(b.ClientRectangle); Rectangle cb = CloseBtn(f);
    Control dk = b.Parent; int top = int.MaxValue, bot = int.MinValue, txt = 0;
    UIElement root = ((IUltraControlElement)dk).MainUIElement; Queue<UIElement> q = new Queue<UIElement>(); q.Enqueue(root);
    while (q.Count > 0) { UIElement e = q.Dequeue(); if (e is TextUIElement && e.Rect.Top < 40 && e.Rect.Height > 0) { Rectangle r = dk.RectangleToScreen(e.Rect); if (r.Top < bs.Bottom + 10) { top = Math.Min(top, r.Top); bot = Math.Max(bot, r.Bottom); txt++; } } if (e.ChildElements != null) foreach (UIElement k in e.ChildElements) q.Enqueue(k); }
    double bcx = bs.Left + bs.Width / 2.0, bcy = bs.Top + bs.Height / 2.0, ccx = cb.Left + cb.Width / 2.0, tcy = (top + bot) / 2.0;
    W("alinhamento " + tag + ": botao=" + bs + " fechar(caption)=" + cb + " centroX botao=" + bcx + " fechar=" + ccx + " dX=" + (bcx - ccx) + " | textos do menu y=" + top + ".." + bot + " (" + txt + ") centroY texto=" + tcy + " botao=" + bcy + " dY=" + (bcy - tcy));
    f.Refresh(); Pump(2);
    using (Bitmap s = Snap(f)) { int h = Math.Min(s.Height, bs.Bottom - f.Top + 40); Rectangle rr = new Rectangle(Math.Max(0, s.Width - 260), 0, Math.Min(260, s.Width), h); using (Bitmap c = s.Clone(rr, s.PixelFormat)) c.Save(Path.Combine(Dir, "t23-align-" + tag + ".png")); }
  }
  static void Strips(string tag) {
    f.Refresh(); Pump(2);
    using (Bitmap s = Snap(f)) {
      foreach (string n in new string[] { "partHost", "gxOutput" }) { Control c = f.Controls[n]; if (c == null) continue; Rectangle r = c.RectangleToScreen(c.ClientRectangle); r.Offset(-f.Left, -f.Top); r.Height = Math.Min(r.Height, 60); r.Intersect(new Rectangle(Point.Empty, s.Size)); using (Bitmap x = s.Clone(r, s.PixelFormat)) x.Save(Path.Combine(Dir, "t23-" + n + "-" + tag + ".png")); }
    }
  }
  public static void Run() {
    if (Environment.GetEnvironmentVariable("T16_DPIAWARE") == "1") SetProcessDPIAware();
    Application.EnableVisualStyles();
    System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(Assembly.Load("Artech.Common.Helpers").GetType("Artech.Common.Helpers.GDI.GdiHelper").TypeHandle);
    foreach (string a in new string[] { "Artech.Core.UI", "Artech.FrameworkDE", "MagicLocalLibrary" }) { try { Assembly.Load(a); } catch (Exception ex) { W("load " + a + ": " + ex.Message); } }
    Assembly ea = Assembly.LoadFrom(Path.Combine(Dir, Dll)); Eng = ea.GetType("GxDarkTheme.DarkEngine"); Tog = ea.GetType("GxDarkTheme.ToggleButton"); Jr = ea.GetType("GxDarkTheme.Journal"); Ico = ea.GetType("GxDarkTheme.IconSwap");
    using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) { W("dll=" + Dll + " DPI=" + g.DpiX + " mascara=" + Call(Tog, "MaskSize")); FakePartFrame.Px = (int)Math.Round(16 * g.DpiX / 96f); }
    W("settings inicial: " + ModeInFile() + " | icone (sem icone= no arquivo)=" + Call(Eng, "IconOption"));
    LoadLightIsl();
    Build();
    Pump(10); f.Refresh(); Pump(10); f.Refresh(); Pump(4); using (Bitmap warm = Snap(f)) { } Pump(4); f.Refresh(); Pump(4);
    Strips("L0"); Dictionary<string, string> L0 = All(); Bitmap L0s = Snap(f); L0s.Save(Path.Combine(Dir, "t23-L0.png"));
    W("L0: " + L0.Count + " propriedades");
    Call(Eng, "Start");
    for (int k = 0; k < 4; k++) { Call(Eng, "Walk", f, 0); Call(Eng, "Pass"); Pump(4); }
    Passes(4);
    W("On=" + On() + " botao=" + (Btn() == null ? "null" : Btn().Bounds + " visivel=" + Btn().Visible + " pai=" + Btn().Parent.GetType().Name + " larguraPai=" + Btn().Parent.ClientSize.Width) + " barra=" + SF(Tog, "LastMenu") + " tip=" + SF(Tog, "TipText"));
    Dictionary<string, string> D1 = All(); Mask = BtnRect(); Bitmap D1s = Snap(f); Shots("D1"); Align("D1"); Strips("D1");
    W("D1 vs L0: " + Diff("D1~L0", L0, D1, 0) + " propriedades diferentes (esperado: muitas)");
    int gdi2 = 0, usr2 = 0; long sumL = 0, sumD = 0; int badL = 0, badD = 0, badLp = 0, badDp = 0;
    Form f3 = null, f5 = null;
    for (int cyc = 1; cyc <= 20; cyc++) {
      if (Environment.GetEnvironmentVariable("T23_TEMAS") == "1") {
        // v2: passa por um tema do Xpz.Dark e volta ao Next ainda no escuro; o diario tem de devolver tudo igual mesmo assim
        string[] ts = { "ardosia", "grafite", "meianoite", "ametista", "brasa", "ambar", "oliva" };
        Call(Eng, "SetTheme", ts[cyc % ts.Length], false); Passes(3);
        if (cyc <= 2) W("ciclo " + cyc + ": tema " + ts[cyc % ts.Length] + " aplicado (" + SF(Eng, "LastToggleMs") + " ms)");
        Call(Eng, "SetTheme", "next", false); Passes(3);
      }
      if (cyc == 1) Click(); else Call(Eng, "Toggle");
      sumL += (long)SF(Eng, "LastToggleMs");
      Passes(3);
      if (On()) W("ERRO: ainda escuro apos alternar (ciclo " + cyc + ")");
      if (cyc == 1) { W("apos clique: On=" + On() + " tip=" + SF(Tog, "TipText") + " arquivo: " + ModeInFile() + " Journal n=" + SF(Jr, "Count") + " devolvidas=" + SF(Jr, "Restored") + " mantidas=" + SF(Jr, "Skipped")); Shots("L1"); Align("L1"); Strips("L1"); }
      Mask = BtnRect();
      Dictionary<string, string> Lc = All(); int dp = Diff("L" + cyc + "~L0", L0, Lc, cyc <= 2 || cyc == 20 ? 40 : 5);
      Rectangle box; int px; using (Bitmap s = Snap(f)) { px = PixDiff(L0s, s, out box); if (cyc == 20) s.Save(Path.Combine(Dir, "t23-L20.png")); if (cyc == 20) Strips("L20"); if (cyc == 1 && px > 0) s.Save(Path.Combine(Dir, "t23-L1-full.png")); }
      if (dp > 0) badL++; if (px != 0) badLp++;
      if (cyc <= 2 || cyc == 20 || dp > 0 || px != 0) W("ciclo " + cyc + " CLARO: difs propriedades=" + dp + " pixels diferentes de L0=" + px + (px > 0 ? " caixa=" + box : "") + " (" + SF(Eng, "LastToggleMs") + " ms)");
      if (cyc == 6) {
        Form f4 = Small("fx"); f4.Show(); Pump(4); Dictionary<string, string> ref4 = State(f4); f4.Close(); f4.Dispose();
        if (f3 != null) { W("janela criada no ESCURO (ciclo 5), agora claro: difs vs janela nunca tematizada=" + Diff("f3", ref4, State(f3), 20)); f3.Close(); f3.Dispose(); f3 = null; }
        f5 = Small("fx"); f5.Show(); Passes(3); W("janela nova aberta no CLARO: difs vs nunca tematizada=" + Diff("f5", ref4, State(f5), 20));
      }
      Call(Eng, "Toggle"); sumD += (long)SF(Eng, "LastToggleMs");
      for (int k = 0; k < 2; k++) { Call(Eng, "Walk", f, 0); Call(Eng, "Pass"); Pump(3); }
      Passes(3);
      if (!On()) W("ERRO: ainda claro apos alternar (ciclo " + cyc + ")");
      if (cyc == 6 && f5 != null) { W("janela aberta no claro, agora escuro: Back painel=" + Hex(f5.Controls[0].BackColor) + " grade=" + Hex(((DataGridView)f5.Controls[0].Controls["g"]).DefaultCellStyle.BackColor)); f5.Close(); f5.Dispose(); f5 = null; }
      if (cyc == 5) { f3 = Small("fx"); f3.Show(); Passes(3); W("janela nova aberta no ESCURO: Back painel=" + Hex(f3.Controls[0].BackColor)); }
      Mask = BtnRect();
      Dictionary<string, string> Dc = All(); int dd = Diff("D" + cyc + "~D1", D1, Dc, cyc <= 2 || cyc == 20 ? 40 : 5);
      int pd; using (Bitmap s = Snap(f)) { pd = PixDiff(D1s, s, out box); if (cyc == 20) s.Save(Path.Combine(Dir, "t23-D20.png")); if (cyc == 1 && pd > 0) s.Save(Path.Combine(Dir, "t23-D2-full.png")); }
      if (dd > 0) badD++; if (pd != 0) badDp++;
      if (cyc <= 2 || cyc == 20 || dd > 0 || pd != 0) W("ciclo " + cyc + " ESCURO: difs propriedades vs D1=" + dd + " pixels diferentes de D1=" + pd + (pd > 0 ? " caixa=" + box : "") + " (" + SF(Eng, "LastToggleMs") + " ms)");
      if (cyc == 1 || cyc == 20) { Strips("D" + (cyc + 1)); if (cyc == 20) Align("D21"); }
      if (cyc == 2) { Gc(); gdi2 = Gdi(); usr2 = User(); W("recursos apos ciclo 2: GDI=" + gdi2 + " USER=" + usr2 + " Journal=" + SF(Jr, "Count")); }
    }
    Gc(); W("recursos apos ciclo 20: GDI=" + Gdi() + " (delta " + (Gdi() - gdi2) + ") USER=" + User() + " (delta " + (User() - usr2) + ") Journal=" + SF(Jr, "Count"));
    W("RESUMO: ciclos claro com difs de propriedade=" + badL + " com difs de pixel=" + badLp + " | ciclos escuro com difs de propriedade=" + badD + " com difs de pixel=" + badDp + " | media claro=" + (sumL / 20) + " ms escuro=" + (sumD / 20) + " ms");
    W("arquivo final: " + ModeInFile());
    Call(Eng, "Toggle"); Passes(3); Shots("Lfim"); W("fim modo=" + (On() ? "escuro" : "claro") + " arquivo: " + ModeInFile());
    f.Close();
    W("fim");
  }
}
