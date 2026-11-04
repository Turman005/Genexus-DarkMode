// Teste 25: lista do autocompletar nativo ("Auto-Suggest Dropdown") - escuro em 2 temas, troca ao vivo e volta ao claro exata.
// (A) lista REAL do Windows: TextBox WinForms com AutoCompleteMode (igual as celulas Name/Type do GeneXus)
// (B) replica com a MESMA classe de janela e os mesmos filhos (SysListView32 + ScrollBar + ScrollBar SBS_SIZEGRIP)
using System; using System.IO; using System.Text; using System.Reflection; using System.Drawing; using System.Drawing.Imaging;
using System.Collections.Generic; using System.Windows.Forms; using System.Runtime.InteropServices; using System.Threading;
public class T25Form : Form { protected override bool ShowWithoutActivation { get { return true; } } }
public static class T25 {
  public static string Gx, Out, Dir, Dll;
  public static void W(string s) { File.AppendAllText(Out, s + "\r\n"); }
  public static void Hook() { AppDomain.CurrentDomain.AssemblyResolve += R; }
  static Assembly R(object s, ResolveEventArgs e) {
    string n = new AssemblyName(e.Name).Name;
    foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies()) if (a.GetName().Name == n) return a;
    foreach (string d in new string[] { Gx, Path.Combine(Gx, "Packages") }) foreach (string x in new string[] { ".dll", ".exe" }) { string p = Path.Combine(d, n + x); if (File.Exists(p)) return Assembly.LoadFrom(p); }
    return null;
  }
  [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }
  [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] struct WNDCLASSEX { public int cbSize; public int style; public IntPtr lpfnWndProc; public int cbClsExtra; public int cbWndExtra; public IntPtr hInstance; public IntPtr hIcon; public IntPtr hCursor; public IntPtr hbrBackground; public string lpszMenuName; public string lpszClassName; public IntPtr hIconSm; }
  [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] struct LVITEM { public int mask; public int iItem; public int iSubItem; public int state; public int stateMask; public string pszText; public int cchTextMax; public int iImage; public IntPtr lParam; public int iIndent; public int iGroupId; public int cColumns; public IntPtr puColumns; public IntPtr piColFmt; public int iGroup; }
  [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] struct LVCOLUMN { public int mask; public int fmt; public int cx; public string pszText; public int cchTextMax; public int iSubItem; public int iImage; public int iOrder; public int cxMin; public int cxDefault; public int cxIdeal; }
  delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern ushort RegisterClassEx(ref WNDCLASSEX c);
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr CreateWindowEx(int ex, string cls, string name, int style, int x, int y, int w, int h, IntPtr parent, IntPtr menu, IntPtr inst, IntPtr p);
  [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern IntPtr GetModuleHandle(string n);
  [DllImport("kernel32.dll", CharSet = CharSet.Ansi)] static extern IntPtr GetProcAddress(IntPtr m, string n);
  [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int m, IntPtr w, IntPtr l);
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SendMessage(IntPtr h, int m, IntPtr w, ref LVITEM l);
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SendMessage(IntPtr h, int m, IntPtr w, ref LVCOLUMN l);
  [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, int m, IntPtr w, IntPtr l);
  [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int c);
  [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] static extern bool DestroyWindow(IntPtr h);
  [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr h, IntPtr hdc, int flags);
  [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] static extern bool EnumThreadWindows(int tid, EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr p, EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] static extern int GetWindowThreadProcessId(IntPtr h, out int pid);
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder sb, int n);
  [DllImport("user32.dll", EntryPoint = "GetWindowLong")] static extern IntPtr GWL32(IntPtr h, int i);
  [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")] static extern IntPtr GWL64(IntPtr h, int i);
  [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int i);
  [DllImport("kernel32.dll")] static extern int GetCurrentThreadId();
  [DllImport("kernel32.dll")] static extern int GetCurrentProcessId();
  [DllImport("user32.dll")] static extern int GetGuiResources(IntPtr p, int f);
  [DllImport("kernel32.dll")] static extern IntPtr GetCurrentProcess();
  [DllImport("user32.dll")] static extern bool RedrawWindow(IntPtr h, IntPtr r, IntPtr rgn, int f);
  static IntPtr WndProcOf(IntPtr h) { return IntPtr.Size == 8 ? GWL64(h, -4) : GWL32(h, -4); }
  static string Cls(IntPtr h) { StringBuilder sb = new StringBuilder(64); GetClassName(h, sb, 64); return sb.ToString(); }
  static void Pump(int n) { for (int i = 0; i < n; i++) { Application.DoEvents(); Thread.Sleep(25); } }
  static Type Eng, Ast;
  static object Call(Type t, string m, params object[] a) { foreach (MethodInfo mi in t.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)) if (mi.Name == m && mi.GetParameters().Length == a.Length) return mi.Invoke(null, a); throw new Exception("metodo " + m); }
  static object SF(Type t, string n) { FieldInfo f = t.GetField(n, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic); if (f != null) return f.GetValue(null); PropertyInfo p = t.GetProperty(n, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic); return p == null ? null : p.GetValue(null, null); }
  static Color PC(string n) { return (Color)SF(Eng, n); }
  static string H6(Color c) { return string.Format("{0:X2}{1:X2}{2:X2}", c.R, c.G, c.B); }
  static string Cr(IntPtr v) { int x = (int)(long)v; if (x == -1) return "CLR_NONE"; return string.Format("{0:X2}{1:X2}{2:X2}", x & 255, (x >> 8) & 255, (x >> 16) & 255); }
  static void Passes(int n) { for (int k = 0; k < n; k++) { Call(Eng, "Pass"); Pump(3); } }
  static int fails;
  static void Check(bool ok, string what) { if (!ok) fails++; W((ok ? "  OK   " : "  FALHA ") + what); }

  static string[] Names = { "OrdemApontamentoSecUserID", "OrdemDeServicoGrupoEconomicoID", "OrdemDeServicoID", "OrdemDeServicoMaquinaFusoID", "OrdemDeServicoSecUserID", "OrdemServicoAcoes", "OrdemServicoAcoesDescricao", "OrdemServicoAcoesID", "OrdemServicoApontamentoAcoes", "OrdemServicoApontamentoColaboradorID", "OrdemServicoApontamentoColaboradorNome", "OrdemServicoApontamentoDataHoraFim", "OrdemServicoApontamentoDataHoraInicio", "OrdemServicoApontamentoDefeitoDescricao", "OrdemServicoApontamentoDefeitoID", "OrdemServicoApontamentoID", "OrdemServicoApontamentoObservacao", "OrdemServicoApontamentoSituacao" };

  // ---- replica (B) ----
  static IntPtr bDrop, bLv, bBar, bGrip; static bool registered;
  static IntPtr MakeReplica(Form owner, int x, int y) {
    IntPtr inst = GetModuleHandle(null);
    if (!registered) {
      WNDCLASSEX c = new WNDCLASSEX(); c.cbSize = Marshal.SizeOf(typeof(WNDCLASSEX)); c.lpfnWndProc = GetProcAddress(GetModuleHandle("user32.dll"), "DefWindowProcW"); c.hInstance = inst; c.hbrBackground = (IntPtr)(5 + 1); c.lpszClassName = "Auto-Suggest Dropdown"; c.style = 0x0008 | 0x00020000; // DBLCLKS|DROPSHADOW
      W("RegisterClassEx(Auto-Suggest Dropdown) = " + RegisterClassEx(ref c)); registered = true;
    }
    int w = 300, h = 260, sb = SystemInformation.VerticalScrollBarWidth;
    IntPtr d = CreateWindowEx(0x80 | 0x8, "Auto-Suggest Dropdown", "", unchecked((int)0x80000000) | 0x00800000 | 0x02000000, x, y, w, h, owner.Handle, IntPtr.Zero, inst, IntPtr.Zero);
    IntPtr bar = CreateWindowEx(0, "ScrollBar", "", 0x50000005, w - 2 - sb, 0, sb, h - 2 - sb, d, IntPtr.Zero, inst, IntPtr.Zero);
    IntPtr grip = CreateWindowEx(0, "ScrollBar", "", 0x5000000C, w - 2 - sb, h - 2 - sb, sb, sb, d, IntPtr.Zero, inst, IntPtr.Zero);
    IntPtr lv = CreateWindowEx(0, "SysListView32", "", 0x54204005, 0, 0, w + 168, h - 2, d, IntPtr.Zero, inst, IntPtr.Zero);
    SendMessage(lv, 0x1036, (IntPtr)0x20, (IntPtr)0x20);
    SendMessage(lv, 0x0030, owner.Font.ToHfont(), IntPtr.Zero);
    LVCOLUMN col = new LVCOLUMN(); col.mask = 0x2; col.cx = w - 40; SendMessage(lv, 0x1061, IntPtr.Zero, ref col);
    for (int i = 0; i < Names.Length; i++) { LVITEM it = new LVITEM(); it.mask = 1; it.iItem = i; it.pszText = Names[i]; SendMessage(lv, 0x104D, IntPtr.Zero, ref it); }
    LVITEM s = new LVITEM(); s.stateMask = 3; s.state = 3; SendMessage(lv, 0x102B, (IntPtr)2, ref s);
    bLv = lv; bBar = bar; bGrip = grip;
    ShowWindow(d, 4); Pump(4);
    return d;
  }

  static IntPtr FindDrop(bool visibleOnly) {
    IntPtr found = IntPtr.Zero; int me = GetCurrentProcessId();
    EnumWindows(delegate(IntPtr h, IntPtr l) { int pid; GetWindowThreadProcessId(h, out pid); if (pid == me && h != bDrop && Cls(h) == "Auto-Suggest Dropdown" && (!visibleOnly || IsWindowVisible(h))) { found = h; return false; } return true; }, IntPtr.Zero);
    return found;
  }
  static void Kids(IntPtr d, out IntPtr lv, out IntPtr bar, out IntPtr grip) {
    IntPtr a = IntPtr.Zero, b = IntPtr.Zero, g = IntPtr.Zero;
    EnumChildWindows(d, delegate(IntPtr k, IntPtr l) { string c = Cls(k); if (c == "SysListView32") a = k; else if (c == "ScrollBar") { if ((GetWindowLong(k, -16) & 0x18) != 0) g = k; else b = k; } return true; }, IntPtr.Zero);
    lv = a; bar = b; grip = g;
  }

  static Bitmap Snap(IntPtr h) { return SnapRaw(h); }
  static Bitmap SnapRaw(IntPtr h) {
    Bitmap first = Snap1(h, 2); if (!Blank(first)) return first;
    W("   (captura toda preta: visivel=" + IsWindowVisible(h) + "; repetindo)"); first.Dispose(); Pump(10);
    first = Snap1(h, 2); if (!Blank(first)) return first; first.Dispose(); return Snap1(h, 0);
  }
  static bool Blank(Bitmap b) { int c = b.GetPixel(b.Width / 2, b.Height / 2).ToArgb(); for (int y = 0; y < b.Height; y += 7) for (int x = 0; x < b.Width; x += 7) if (b.GetPixel(x, y).ToArgb() != c) return false; return true; }
  static Bitmap Snap1(IntPtr h, int flags) {
    RECT r; GetWindowRect(h, out r); int w = Math.Max(1, r.R - r.L), ht = Math.Max(1, r.B - r.T);
    RedrawWindow(h, IntPtr.Zero, IntPtr.Zero, 0x1 | 0x4 | 0x80 | 0x400 | 0x100); Pump(3);
    Bitmap b = new Bitmap(w, ht, PixelFormat.Format32bppArgb);
    using (Graphics g = Graphics.FromImage(b)) { IntPtr dc = g.GetHdc(); PrintWindow(h, dc, flags); g.ReleaseHdc(dc); }
    return b;
  }
  static Rectangle Rel(IntPtr parent, IntPtr child) { RECT p, c; GetWindowRect(parent, out p); GetWindowRect(child, out c); return Rectangle.FromLTRB(c.L - p.L, c.T - p.T, c.R - p.L, c.B - p.T); }
  static string Dominant(Bitmap b, Rectangle r) { Dictionary<int, int> h = new Dictionary<int, int>(); r.Intersect(new Rectangle(Point.Empty, b.Size)); for (int y = r.Top; y < r.Bottom; y++) for (int x = r.Left; x < r.Right; x++) { int v = b.GetPixel(x, y).ToArgb(); int c; h.TryGetValue(v, out c); h[v] = c + 1; } int best = 0, bc = 0; foreach (KeyValuePair<int, int> kv in h) if (kv.Value > bc) { bc = kv.Value; best = kv.Key; } return H6(Color.FromArgb(best)); }
  static int Light(Bitmap b, Rectangle r, Rectangle skip) { int n = 0; r.Intersect(new Rectangle(Point.Empty, b.Size)); for (int y = r.Top; y < r.Bottom; y++) for (int x = r.Left; x < r.Right; x++) { if (skip.Contains(x, y)) continue; Color p = b.GetPixel(x, y); if (p.R > 225 && p.G > 225 && p.B > 225) n++; } return n; }
  static string Hash(Bitmap b) { long hsh = 17; for (int y = 0; y < b.Height; y++) for (int x = 0; x < b.Width; x++) hsh = hsh * 31 + b.GetPixel(x, y).ToArgb(); return hsh.ToString("X"); }
  static Rectangle ItemRect(IntPtr lv, int i) { RECT r = new RECT(); r.L = 0; IntPtr p = Marshal.AllocHGlobal(16); try { Marshal.StructureToPtr(r, p, false); SendMessage(lv, 0x100E, (IntPtr)i, p); r = (RECT)Marshal.PtrToStructure(p, typeof(RECT)); } finally { Marshal.FreeHGlobal(p); } return Rectangle.FromLTRB(r.L, r.T, r.R, r.B); }
  static int SelIndex(IntPtr lv) { return (int)(long)SendMessage(lv, 0x100C, (IntPtr)(-1), (IntPtr)2); }

  class St { public string Props, Hash; public string Item, SelBg, Bar, Grip; public int Light, Residue; }
  static St Measure(string tag, IntPtr d) {
    IntPtr lv, bar, grip; Kids(d, out lv, out bar, out grip);
    St s = new St();
    s.Props = "lv bk=" + Cr(SendMessage(lv, 0x1000, IntPtr.Zero, IntPtr.Zero)) + " textbk=" + Cr(SendMessage(lv, 0x1025, IntPtr.Zero, IntPtr.Zero)) + " text=" + Cr(SendMessage(lv, 0x1023, IntPtr.Zero, IntPtr.Zero))
      + " wndproc(drop)=" + WndProcOf(d).ToString("X") + " wndproc(lv)=" + WndProcOf(lv).ToString("X") + (bar != IntPtr.Zero ? " wndproc(bar)=" + WndProcOf(bar).ToString("X") : " sem-bar") + (grip != IntPtr.Zero ? " wndproc(grip)=" + WndProcOf(grip).ToString("X") : " sem-grip");
    using (Bitmap b = Snap(d)) {
      b.Save(Path.Combine(Dir, "t25-" + tag + ".png"));
      Rectangle lr = Rel(d, lv); if (bar != IntPtr.Zero) lr.Width = Math.Min(lr.Right, Rel(d, bar).Left) - lr.Left; lr.Intersect(new Rectangle(1, 1, b.Width - 2, b.Height - 2)); int sel = SelIndex(lv);
      Rectangle ir = ItemRect(lv, sel == 0 ? 1 : 0); ir.Offset(lr.Location); ir.Width = Math.Min(ir.Width, lr.Width);
      Rectangle sr = sel >= 0 ? ItemRect(lv, sel) : Rectangle.Empty; if (sel >= 0) { sr.Offset(lr.Location); sr.Width = Math.Min(sr.Width, lr.Width); }
      s.Item = Dominant(b, ir); s.SelBg = sel >= 0 ? Dominant(b, sr) : "-";
      s.Light = Light(b, lr, sr);
      s.Bar = bar != IntPtr.Zero ? Dominant(b, Rel(d, bar)) : "-"; s.Grip = grip != IntPtr.Zero ? Dominant(b, Rel(d, grip)) : "-";
      s.Hash = Hash(b); s.Residue = Residue(b);
    }
    W(tag + ": " + s.Props + " | pixels: item=" + s.Item + " selecionado=" + s.SelBg + " rolagem=" + s.Bar + " alca=" + s.Grip + " claros(lista, sem selecao)=" + s.Light + " residuo-escuro=" + s.Residue + " hash=" + s.Hash);
    return s;
  }
  static void ExpectDark(string tag, St s) {
    string inp = H6(PC("Input")), sel = H6(PC("Sel"));
    Check(s.Props.StartsWith("lv bk=" + inp + " textbk=" + inp), tag + " cores da ListView = Input " + inp);
    Check(s.Item == inp, tag + " fundo dos itens = Input " + inp + " (lido " + s.Item + ")");
    Check(s.SelBg == "-" || s.SelBg == sel, tag + " selecao plana = Sel " + sel + " (lido " + s.SelBg + ")");
    Check(s.Light == 0, tag + " nenhum pixel claro na lista (" + s.Light + ")");
    Check(s.Grip == "-" || s.Grip == inp, tag + " alca escura = " + inp + " (lido " + s.Grip + ")");
    Color bar = s.Bar == "-" ? Color.Black : ColorTranslator.FromHtml("#" + s.Bar);
    Check(bar.R < 120 && bar.G < 120 && bar.B < 120, tag + " barra de rolagem escura (lido " + s.Bar + ")");
  }
  static void ExpectSame(string tag, St a, St b) {
    Check(a.Props == b.Props, tag + " propriedades/subclasses iguais ao claro original" + (a.Props == b.Props ? "" : "\r\n      antes: " + a.Props + "\r\n      agora: " + b.Props));
    Check(a.Item == b.Item && a.SelBg == b.SelBg && a.Bar == b.Bar && a.Grip == b.Grip, tag + " cores identicas ao claro original (item " + b.Item + ", selecao " + b.SelBg + ", rolagem " + b.Bar + ", alca " + b.Grip + ")");
    Check(b.Residue == a.Residue, tag + " nenhum pixel com cor dos temas escuros alem dos do claro original (" + b.Residue + " vs " + a.Residue + ")");
    W("   hash " + (a.Hash == b.Hash ? "identico" : "diferente (" + a.Hash + " vs " + b.Hash + "; a replica sem dono pinta a ListView em etapas, por isso o hash e informativo)"));
  }
  // cores de fundo/selecao/rolagem de todos os temas escuros usados no teste: nenhuma pode sobrar no claro
  static readonly int[] Escuras = { 0x182740, 0x203A67, 0x2A2622, 0x403A33, 0x272727, 0x3B3B3B, 0x171717, 0x1A1A1A };
  static int Residue(Bitmap b) { var set = new System.Collections.Generic.HashSet<int>(Escuras); int n = 0; for (int y = 0; y < b.Height; y++) for (int x = 0; x < b.Width; x++) if (set.Contains(b.GetPixel(x, y).ToArgb() & 0xFFFFFF)) n++; return n; }

  public static void Run() {
    Application.EnableVisualStyles();
    Assembly ea = Assembly.LoadFrom(Path.Combine(Dir, Dll)); Eng = ea.GetType("GxDarkTheme.DarkEngine"); Ast = ea.GetType("GxDarkTheme.AutoSuggestTheme");
    W("dll=" + Dll + " versao=" + System.Diagnostics.FileVersionInfo.GetVersionInfo(Path.Combine(Dir, Dll)).FileVersion + " IntPtr=" + IntPtr.Size);
    T25Form f = new T25Form(); f.Text = "T25 autocompletar"; f.StartPosition = FormStartPosition.Manual; f.Location = new Point(30, 30); f.Size = new Size(380, 140); f.TopMost = true; f.Font = new Font("Segoe UI", 9f);
    TextBox tb = new TextBox(); tb.SetBounds(10, 10, 320, 24); tb.AutoCompleteMode = AutoCompleteMode.Suggest; tb.AutoCompleteSource = AutoCompleteSource.CustomSource;
    AutoCompleteStringCollection src = new AutoCompleteStringCollection(); src.AddRange(Names); tb.AutoCompleteCustomSource = src; f.Controls.Add(tb);
    f.Show(); Pump(6);
    // (B) replica, criada ANTES do motor (pega pela varredura)
    bDrop = MakeReplica(f, 30, 180);
    // (A) lista real do Windows: foco na thread e caracteres postados so para esta caixa (sem teclado global)
    IntPtr aDrop = IntPtr.Zero;
    tb.Focus(); Pump(3);
    foreach (char ch in "Ord") { PostMessage(tb.Handle, 0x0102, (IntPtr)ch, IntPtr.Zero); Pump(4); }
    for (int i = 0; i < 40 && aDrop == IntPtr.Zero; i++) { Pump(2); aDrop = FindDrop(true); }
    W("(A) lista real do Windows: " + (aDrop == IntPtr.Zero ? "nao apareceu (sem foco de primeiro plano) - teste segue so com a replica" : "aberta, hwnd=" + aDrop.ToString("X") + " thread da IDE=" + (GetWindowThreadProcessId(aDrop, out fails) == GetCurrentThreadId())));
    fails = 0;
    if (aDrop != IntPtr.Zero) { IntPtr lv, b, g; Kids(aDrop, out lv, out b, out g); SendMessage(lv, 0x0100, (IntPtr)0x28, IntPtr.Zero); Pump(4); }
    St bL0 = Measure("B-L0-claro", bDrop); St aL0 = aDrop != IntPtr.Zero ? Measure("A-L0-claro", aDrop) : null;
    int gdi0 = GetGuiResources(GetCurrentProcess(), 0);

    W("== escuro, tema meianoite (Start) ==");
    Call(Eng, "Start"); Passes(3);
    W("gancho de eventos ligado=" + SF(Ast, "EventHookOn") + " listas sob tema=" + SF(Ast, "Count"));
    St bD1 = Measure("B-D1-meianoite", bDrop); ExpectDark("B-D1", bD1);
    if (aDrop != IntPtr.Zero) ExpectDark("A-D1", Measure("A-D1-meianoite", aDrop));
    W("itens desenhados pelo tema ate aqui=" + SF(Ast, "Painted"));

    W("== a lista do Windows recoloca as cores do sistema ao reaparecer (simulado) ==");
    SendMessage(bLv, 0x1001, IntPtr.Zero, (IntPtr)0xFFFFFF); SendMessage(bLv, 0x1026, IntPtr.Zero, (IntPtr)0xFFFFFF); SendMessage(bLv, 0x1024, IntPtr.Zero, IntPtr.Zero);
    ShowWindow(bDrop, 0); Pump(2); ShowWindow(bDrop, 4); Pump(3);
    ExpectDark("B-D1-reaparecida", Measure("B-D1b-reaparecida", bDrop));

    W("== nova lista criada com o escuro ligado: so o gancho WinEvent (sem passada do motor) ==");
    IntPtr c2 = MakeReplica(f, 360, 180); IntPtr keep = bDrop; bDrop = c2; Pump(6);
    ExpectDark("C-gancho", Measure("C-nova-pelo-gancho", c2)); bDrop = keep; DestroyWindow(c2); Pump(3);

    W("== troca de tema ao vivo: ambar ==");
    Call(Eng, "SetTheme", "ambar", false); Passes(3);
    St bD2 = Measure("B-D2-ambar", bDrop); ExpectDark("B-D2", bD2);
    Check(bD2.Item != bD1.Item, "B-D2 cor mudou com o tema (" + bD1.Item + " -> " + bD2.Item + ")");
    if (aDrop != IntPtr.Zero) ExpectDark("A-D2", Measure("A-D2-ambar", aDrop));
    Call(Eng, "SetTheme", "grafite", false); Passes(3); ExpectDark("B-D3", Measure("B-D3-grafite", bDrop));

    for (int cyc = 1; cyc <= 3; cyc++) {
      W("== claro (ciclo " + cyc + ") ==");
      Call(Eng, "SetMode", false, false); Passes(3);
      ExpectSame("B-L" + cyc, bL0, Measure("B-L" + cyc + "-claro", bDrop));
      if (aDrop != IntPtr.Zero && IsWindowVisible(aDrop)) { St al = Measure("A-L" + cyc + "-claro", aDrop); Check(al.Props == aL0.Props, "A-L" + cyc + " propriedades/subclasses iguais ao claro original"); Check(al.Item == aL0.Item && al.SelBg == aL0.SelBg && al.Bar == aL0.Bar && al.Grip == aL0.Grip, "A-L" + cyc + " cores identicas ao claro original (a lista real segue o mouse, por isso o hash nao e comparado)"); Check(al.Residue == aL0.Residue, "A-L" + cyc + " nenhum pixel com cor dos temas escuros alem dos do claro original (" + al.Residue + " vs " + aL0.Residue + ", texto suavizado)"); }
      Call(Eng, "SetMode", true, false); Passes(3);
      ExpectDark("B-D" + (cyc + 3), Measure("B-D" + (cyc + 3) + "-escuro", bDrop));
    }
    Call(Eng, "SetMode", false, false); Passes(3);
    ExpectSame("B-Lfim", bL0, Measure("B-Lfim-claro", bDrop));
    GC.Collect(); GC.WaitForPendingFinalizers(); Pump(3);
    W("GDI: inicio=" + gdi0 + " fim=" + GetGuiResources(GetCurrentProcess(), 0));
    W("RESUMO: " + (fails == 0 ? "TODOS OS TESTES DO AUTOCOMPLETAR OK" : fails + " FALHA(S)"));
    try { Call(Ast, "Unhook"); } catch { }
    DestroyWindow(bDrop); f.Close(); W("fim");
  }
}
