// GxDarkTheme - alternar claro/escuro AO VIVO (botao sol/lua na barra de menus, Ctrl+Shift+F12).
// Tudo em memoria e reversivel: cada propriedade alterada pelo tema e anotada (Journal) antes da primeira troca e
// devolvida ao valor original do GeneXus ao voltar para o claro. Nenhum arquivo do GeneXus/KB e alterado
// (so a linha "mode=dark|light" no GxDarkTheme.settings do usuario).
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using Infragistics.Win;

namespace GxDarkTheme
{
    internal sealed class RefEq : IEqualityComparer<object>
    {
        public static readonly RefEq I = new RefEq();
        public new bool Equals(object a, object b) { return object.ReferenceEquals(a, b); }
        public int GetHashCode(object o) { return o == null ? 0 : RuntimeHelpers.GetHashCode(o); }
    }

    // ================= diario de propriedades (valor original do GeneXus antes do tema) =================
    public static class Journal
    {
        sealed class Rec { public PropertyDescriptor Pd; public FieldInfo Fi; public object Val; public bool Ser; public bool Always; public string Name; public int Amb; }   // Amb: 0 desconhecido, 1 cor ambiente (herdada do pai), 2 cor explicita
        sealed class Entry
        {
            public object O; public Control Owner; public List<Rec> Recs = new List<Rec>(); public HashSet<string> Names = new HashSet<string>();
            public bool HasTs; public ToolStripRenderMode TsMode; public ToolStripRenderer TsRenderer;
        }
        static readonly object sync = new object();
        static readonly Dictionary<object, Entry> map = new Dictionary<object, Entry>(RefEq.I);
        static readonly List<Entry> order = new List<Entry>();
        static readonly Dictionary<Type, PropertyDescriptor[]> basicProps = new Dictionary<Type, PropertyDescriptor[]>();
        static readonly Dictionary<Type, PropertyDescriptor[]> allProps = new Dictionary<Type, PropertyDescriptor[]>();
        static readonly HashSet<string> extras = new HashSet<string> { "FlatStyle", "UseVisualStyleBackColor", "DrawMode", "BorderStyle", "EnableHeadersVisualStyles", "GridLines", "OwnerDraw" };
        public static int Count { get { lock (sync) return order.Count; } }
        public static int Skipped, Restored;

        static PropertyDescriptor[] Basic(Type t)
        {
            PropertyDescriptor[] r;
            if (basicProps.TryGetValue(t, out r)) return r;
            List<PropertyDescriptor> l = new List<PropertyDescriptor>();
            try
            {
                foreach (PropertyDescriptor pd in TypeDescriptor.GetProperties(t))
                {
                    if (pd.IsReadOnly) continue;
                    if (pd.PropertyType == typeof(Color)) l.Add(pd);
                    else if (extras.Contains(pd.Name) && (pd.PropertyType.IsEnum || pd.PropertyType == typeof(bool))) l.Add(pd);
                }
            }
            catch { }
            r = l.ToArray(); basicProps[t] = r; return r;
        }

        static PropertyDescriptor[] All(Type t)
        {
            PropertyDescriptor[] r;
            if (allProps.TryGetValue(t, out r)) return r;
            List<PropertyDescriptor> l = new List<PropertyDescriptor>();
            try
            {
                foreach (PropertyDescriptor pd in TypeDescriptor.GetProperties(t))
                {
                    if (pd.IsReadOnly || pd.Name == "Site" || pd.Name == "Tag" || pd.Name == "Name") continue;
                    l.Add(pd);
                }
            }
            catch { }
            r = l.ToArray(); allProps[t] = r; return r;
        }

        // A cor esta guardada no proprio objeto (explicita) ou vem do pai (ambiente)? ShouldSerializeValue nao distingue
        // "explicita igual ao padrao" (ex.: Crownwood TabControl = SystemColors.Control no construtor) de "ambiente".
        static readonly Dictionary<string, FieldInfo> propKeys = new Dictionary<string, FieldInfo>();
        static PropertyInfo ctlProps, itemProps;
        static MethodInfo getColor;
        public static bool? Ambient(object o, string name)
        {
            try
            {
                Type bt = o is Control ? typeof(Control) : o is ToolStripItem ? typeof(ToolStripItem) : null;
                if (bt == null) return null;
                string k = bt.Name + "." + name;
                FieldInfo key;
                if (!propKeys.TryGetValue(k, out key)) { key = bt.GetField("Prop" + name, BindingFlags.Static | BindingFlags.NonPublic); propKeys[k] = key; }
                if (key == null) return null;
                PropertyInfo pp = bt == typeof(Control) ? (ctlProps ?? (ctlProps = bt.GetProperty("Properties", BindingFlags.Instance | BindingFlags.NonPublic))) : (itemProps ?? (itemProps = bt.GetProperty("Properties", BindingFlags.Instance | BindingFlags.NonPublic)));
                if (pp == null) return null;
                object store = pp.GetValue(o, null);
                if (store == null) return null;
                if (getColor == null) getColor = store.GetType().GetMethod("GetColor", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(int) }, null);
                if (getColor == null) return null;
                Color c = (Color)getColor.Invoke(store, new object[] { key.GetValue(null) });
                return c.IsEmpty;
            }
            catch { return null; }
        }

        static Entry Get(object o, Control owner)
        {
            Entry e;
            if (!map.TryGetValue(o, out e)) { e = new Entry { O = o, Owner = owner ?? (o as Control) }; map[o] = e; order.Add(e); }
            return e;
        }

        static void Add(Entry e, PropertyDescriptor pd, bool always)
        {
            if (!e.Names.Add(pd.Name)) return;
            try
            {
                Rec r = new Rec { Pd = pd, Name = pd.Name, Always = always || pd.PropertyType != typeof(Color) };
                r.Val = pd.GetValue(e.O);
                try { r.Ser = pd.ShouldSerializeValue(e.O); } catch { r.Ser = true; }
                if (pd.PropertyType == typeof(Color) && (pd.Name == "BackColor" || pd.Name == "ForeColor")) { bool? a = Ambient(e.O, pd.Name); r.Amb = a == null ? 0 : a.Value ? 1 : 2; }
                e.Recs.Add(r);
            }
            catch { }
        }

        // cores + alguns estilos (FlatStyle, BorderStyle, DrawMode...) do objeto
        public static void Snap(object o) { Snap(o, null); }
        public static void Snap(object o, Control owner)
        {
            if (o == null || !DarkEngine.On) return;
            lock (sync)
            {
                bool isNew = !map.ContainsKey(o);
                Entry e = Get(o, owner);
                if (!isNew) return;
                foreach (PropertyDescriptor pd in Basic(o.GetType())) Add(e, pd, false);
                ToolStrip ts = o as ToolStrip;
                if (ts != null) { try { e.HasTs = true; e.TsMode = ts.RenderMode; e.TsRenderer = ts.RenderMode == ToolStripRenderMode.Custom ? ts.Renderer : null; } catch { e.HasTs = false; } }
            }
        }

        // todas as propriedades gravaveis (estilos de celula, FlatAppearance, aparencias DevExpress, renderer do editor)
        public static void SnapAll(object o, Control owner)
        {
            if (o == null || !DarkEngine.On) return;
            lock (sync)
            {
                Entry e = Get(o, owner);
                foreach (PropertyDescriptor pd in All(o.GetType())) Add(e, pd, false);
            }
        }

        public static void SnapNamed(object o, params string[] names)
        {
            if (o == null || !DarkEngine.On) return;
            lock (sync)
            {
                Entry e = Get(o, null);
                PropertyDescriptorCollection pc = null;
                try { pc = TypeDescriptor.GetProperties(o); } catch { }
                if (pc == null) return;
                foreach (string n in names) { PropertyDescriptor pd = pc.Find(n, false); if (pd != null && !pd.IsReadOnly) Add(e, pd, false); }
            }
        }

        // campos Color / Color[] (ex.: DrawManager da SmartPropertyGrid)
        public static void SnapFields(object o)
        {
            if (o == null || !DarkEngine.On) return;
            lock (sync)
            {
                Entry e = Get(o, null);
                for (Type t = o.GetType(); t != null && t != typeof(object); t = t.BaseType)
                    foreach (FieldInfo fi in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    {
                        if (fi.IsInitOnly || (fi.FieldType != typeof(Color) && fi.FieldType != typeof(Color[]))) continue;
                        if (!e.Names.Add("F:" + t.FullName + "." + fi.Name)) continue;
                        try
                        {
                            object v = fi.GetValue(o);
                            Color[] a = v as Color[];
                            e.Recs.Add(new Rec { Fi = fi, Name = fi.Name, Val = a != null ? a.Clone() : v, Always = true });
                        }
                        catch { }
                    }
            }
        }

        static bool Gone(Entry e)
        {
            try
            {
                Control c = e.O as Control;
                if (c != null && c.IsDisposed) return true;
                if (e.Owner != null && e.Owner.IsDisposed) return true;
                ToolStripItem it = e.O as ToolStripItem;
                if (it != null && it.IsDisposed) return true;
            }
            catch { return true; }
            return false;
        }

        // o GeneXus mudou a cor para um valor "claro" depois do tema: mantem o valor do GeneXus
        static HashSet<int> ours;
        static bool Ours(Color c)
        {
            if (ours == null)
            {
                HashSet<int> h = new HashSet<int>();
                try { foreach (FieldInfo fi in typeof(DarkEngine).GetFields(BindingFlags.Static | BindingFlags.Public)) if (fi.FieldType == typeof(Color)) h.Add(((Color)fi.GetValue(null)).ToArgb()); } catch { }
                foreach (int v in new int[] { 0x2C3C50, 0x606C7C, 0xA0A7B2, 0x28303C, 0x3A4452, 0x404854, 0x204A70, 0x265680, 0x264F78, 0xD4D4D4 }) h.Add(unchecked((int)0xFF000000) | v);
                ours = h;
            }
            return ours.Contains(c.ToArgb());
        }

        static bool Moved(string name, Color cur, Color orig)
        {
            if (cur.ToArgb() == orig.ToArgb()) return false;
            if (Ours(cur)) return false;   // cor do proprio tema escuro: sempre devolve
            bool fore = name.IndexOf("Fore", StringComparison.Ordinal) >= 0 || name.IndexOf("Text", StringComparison.Ordinal) >= 0 || name.IndexOf("Link", StringComparison.Ordinal) >= 0;
            return fore ? DarkEngine.IsDarkText(cur) : DarkEngine.IsLight(cur);
        }

        public static int Restore()
        {
            int n = 0, skip = 0;
            List<Entry> list;
            lock (sync) { list = new List<Entry>(order); map.Clear(); order.Clear(); }
            foreach (Entry e in list)
            {
                if (Gone(e)) continue;
                object o = e.O;
                foreach (Rec r in e.Recs)
                {
                    try
                    {
                        if (r.Fi != null)
                        {
                            Color[] a = r.Val as Color[];
                            if (a != null) { Color[] cur = r.Fi.GetValue(o) as Color[]; if (cur != null && cur.Length == a.Length) Array.Copy(a, cur, a.Length); else r.Fi.SetValue(o, a.Clone()); }
                            else if (!object.Equals(r.Fi.GetValue(o), r.Val)) r.Fi.SetValue(o, r.Val);
                            n++;
                            continue;
                        }
                        PropertyDescriptor pd = r.Pd;
                        if (r.Amb == 1)
                        {
                            // original herdava a cor do pai: so volta a herdar (nunca grava o valor que o pai tinha naquele momento)
                            bool? now = Ambient(o, r.Name);
                            if (now == true) continue;
                            object cv0 = pd.GetValue(o);
                            if (!r.Always && cv0 is Color && r.Val is Color && Moved(r.Name, (Color)cv0, (Color)r.Val) && !Ours((Color)cv0)) { skip++; continue; }
                            bool can0 = false; try { can0 = pd.CanResetValue(o); } catch { }
                            if (can0) pd.ResetValue(o); else pd.SetValue(o, Color.Empty);
                            n++; continue;
                        }
                        object cv = pd.GetValue(o);
                        bool ser = r.Ser;
                        try { ser = pd.ShouldSerializeValue(o); } catch { }
                        if (object.Equals(cv, r.Val) && ser == r.Ser) continue;
                        if (!r.Always && cv is Color && r.Val is Color && Moved(r.Name, (Color)cv, (Color)r.Val)) { skip++; continue; }
                        if (r.Amb == 2) { pd.SetValue(o, r.Val); n++; continue; }   // explicita no original (mesmo se igual ao padrao): grava o valor exato
                        if (!r.Ser)
                        {
                            bool can = false;
                            try { can = pd.CanResetValue(o); } catch { }
                            if (can)
                            {
                                pd.ResetValue(o); n++;
                                if (o is Control && r.Val is Color) continue;           // cor ambiente: segue o pai
                                if (r.Name == "UseVisualStyleBackColor") continue;      // nao definido no original: o padrao segue o BackColor
                                if (object.Equals(pd.GetValue(o), r.Val)) continue;
                            }
                        }
                        pd.SetValue(o, r.Val); n++;
                    }
                    catch { }
                }
                if (e.HasTs)
                {
                    try
                    {
                        ToolStrip ts = (ToolStrip)o;
                        if (e.TsMode == ToolStripRenderMode.Custom) { if (e.TsRenderer != null && !object.ReferenceEquals(ts.Renderer, e.TsRenderer)) { ts.Renderer = e.TsRenderer; n++; } }
                        else if (ts.RenderMode != e.TsMode || ts.Renderer is DarkTsRenderer) { ts.RenderMode = e.TsMode; n++; }
                    }
                    catch { }
                }
            }
            // UseVisualStyleBackColor volta a false quando o WinForms reaplica BackColor/FlatStyle depois: segunda passada
            foreach (Entry e in list)
            {
                if (Gone(e)) continue;
                foreach (Rec r in e.Recs)
                {
                    if (r.Pd == null || r.Name != "UseVisualStyleBackColor") continue;
                    try
                    {
                        if (!r.Ser) { bool sv = false; try { sv = r.Pd.ShouldSerializeValue(e.O); } catch { } if (sv && r.Pd.CanResetValue(e.O)) { r.Pd.ResetValue(e.O); n++; } }
                        else if (!object.Equals(r.Pd.GetValue(e.O), r.Val)) { r.Pd.SetValue(e.O, r.Val); n++; }
                    }
                    catch { }
                }
            }
            Skipped = skip; Restored = n;
            return n;
        }

        public static void Purge()
        {
            lock (sync)
            {
                List<Entry> keep = new List<Entry>();
                foreach (Entry e in order) { if (Gone(e)) map.Remove(e.O); else keep.Add(e); }
                order.Clear(); order.AddRange(keep);
            }
        }
    }


    // ================= Crownwood TabControl (abas das partes): retrato exato do original =================
    // O BackColor do Crownwood e definido no construtor (SystemColors.Control) e por isso "nao serializavel"; desfazer pelo
    // caminho generico (ResetValue) o deixava herdando a cor do pai (#535353 na barra de abas das partes). Aqui guardamos,
    // na primeira vez que o controle e visto e ANTES de qualquer tema, o BackColor exato (inclusive a cor de sistema) e todos
    // os campos derivados (_backLight/_backDark/_backIDE/..., _defaultColor) e devolvemos isso byte a byte no modo claro.
    public static class CrownOrig
    {
        sealed class Snap { public Color Back; public bool? Ambient; public List<KeyValuePair<FieldInfo, object>> F = new List<KeyValuePair<FieldInfo, object>>(); }
        static readonly ConditionalWeakTable<Control, Snap> snaps = new ConditionalWeakTable<Control, Snap>();
        public static int Taken, Restored;
        public static bool Is(Control c) { return c != null && c.GetType().FullName.StartsWith("Crownwood.Magic.Controls.TabControl", StringComparison.Ordinal); }
        public static void Take(Control c)
        {
            if (!Is(c)) return;
            Snap s;
            if (snaps.TryGetValue(c, out s)) return;
            s = new Snap();
            try
            {
                s.Back = c.BackColor;
                s.Ambient = Journal.Ambient(c, "BackColor");
                for (Type t = c.GetType(); t != null && t != typeof(Panel) && t != typeof(object); t = t.BaseType)
                    foreach (FieldInfo fi in t.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly))
                        if (!fi.IsInitOnly && (fi.FieldType == typeof(Color) || (fi.FieldType == typeof(bool) && fi.Name == "_defaultColor")))
                            s.F.Add(new KeyValuePair<FieldInfo, object>(fi, fi.GetValue(c)));
                snaps.Add(c, s);
                Taken++;
            }
            catch { }
        }
        public static bool Has(Control c) { Snap s; return c != null && snaps.TryGetValue(c, out s); }
        public static bool OrigLight(Control c) { Snap s; return c != null && snaps.TryGetValue(c, out s) && DarkEngine.IsLight(s.Back); }
        public static bool Restore(Control c)
        {
            Snap s;
            if (c == null || c.IsDisposed || !snaps.TryGetValue(c, out s)) return false;
            try
            {
                if (s.Ambient == true) { if (Journal.Ambient(c, "BackColor") != true) c.ResetBackColor(); }
                else if (c.BackColor != s.Back || c.BackColor.ToArgb() != s.Back.ToArgb()) c.BackColor = s.Back;   // setter do Crownwood: _defaultColor + DefineBackColor
                foreach (KeyValuePair<FieldInfo, object> kv in s.F) { try { if (!object.Equals(kv.Key.GetValue(c), kv.Value)) kv.Key.SetValue(c, kv.Value); } catch { } }
                Restored++;
                return true;
            }
            catch { return false; }
        }
    }

    // ================= motor: alternar ao vivo =================
    public static partial class DarkEngine
    {
        public static volatile bool On;
        static bool iconsInit, toggling, savedFlat = true, savedLine = true, origCaptured;
        static Office2007ColorScheme origO7; static Office2010ColorScheme origO10;
        static Color origBlend7, origBlend10;
        static ToolStripManagerRenderMode origTsMode = ToolStripManagerRenderMode.Professional;
        static ToolStripRenderer origTsRenderer;
        static readonly Dictionary<FieldInfo, Color[]> igOrig = new Dictionary<FieldInfo, Color[]>();
        static readonly Dictionary<FieldInfo, Color[]> igFull = new Dictionary<FieldInfo, Color[]>();   // tabelas inteiras antes do tema
        static readonly Dictionary<IntPtr, Control> themedH = new Dictionary<IntPtr, Control>();
        static readonly HashSet<IntPtr> themedHdr = new HashSet<IntPtr>();
        static readonly Dictionary<IntPtr, Control> sbThemed = new Dictionary<IntPtr, Control>();
        static readonly HashSet<Control> crownColored = new HashSet<Control>();
        static readonly HashSet<Control> paneDark = new HashSet<Control>();
        static readonly HashSet<Control> tabPaintHooked = new HashSet<Control>();
        static readonly HashSet<ToolStripDropDown> openingHooked = new HashSet<ToolStripDropDown>();
        static readonly HashSet<Control> lightSeen = new HashSet<Control>();
        static int lightFullAt = -1;
        public static int Toggles;
        public static long LastToggleMs;

        [DllImport("user32.dll")] static extern bool IsWindow(IntPtr h);

        // ---- arquivo de configuracao (mantem as outras linhas) ----
        public static string SettingsValue(string key)
        {
            try
            {
                string p = SettingsPath();
                if (!File.Exists(p)) return null;
                string r = null;
                foreach (string raw in File.ReadAllLines(p))
                {
                    int i = raw.IndexOf('=');
                    if (i <= 0) continue;
                    if (raw.Substring(0, i).Trim().Equals(key, StringComparison.OrdinalIgnoreCase)) r = raw.Substring(i + 1).Trim().ToLowerInvariant();
                }
                return r;
            }
            catch { return null; }
        }

        public static void SettingsWrite(string key, string val)
        {
            try
            {
                string p = SettingsPath();
                List<string> lines = new List<string>();
                bool done = false;
                if (File.Exists(p))
                    foreach (string raw in File.ReadAllLines(p))
                    {
                        int i = raw.IndexOf('=');
                        if (i > 0 && raw.Substring(0, i).Trim().Equals(key, StringComparison.OrdinalIgnoreCase)) { if (!done) { lines.Add(key + "=" + val); done = true; } continue; }
                        lines.Add(raw);
                    }
                if (!done) lines.Add(key + "=" + val);
                string d = Path.GetDirectoryName(p);
                if (!string.IsNullOrEmpty(d) && !Directory.Exists(d)) Directory.CreateDirectory(d);
                File.WriteAllLines(p, lines.ToArray());
            }
            catch (Exception ex) { Log("Configuracao: nao gravou " + key + "=" + val + ": " + ex.Message); }
        }

        static bool StartDark()
        {
            string m = SettingsValue("mode");
            return !(m == "light" || m == "claro");
        }

        public static string IconOption()
        {
            string v = SettingsValue("icone") ?? SettingsValue("icon");
            if (v == "contorno" || v == "a") return "contorno";
            if (v == "azul" || v == "c") return "azul";
            return "ambar";   // padrao (opcao B escolhida): sol ambar / lua suave
        }

        static void CaptureOriginals()
        {
            try { origO7 = Office2007ColorTable.ColorScheme; origBlend7 = Office2007ColorTable.CustomBlendColor; } catch { }
            try { origO10 = Office2010ColorTable.ColorScheme; origBlend10 = Office2010ColorTable.CustomBlendColor; } catch { }
            try { origTsMode = ToolStripManager.RenderMode; origTsRenderer = origTsMode == ToolStripManagerRenderMode.Custom ? ToolStripManager.Renderer : null; } catch { }
            try
            {
                Assembly iw = typeof(Infragistics.Win.Appearance).Assembly;
                foreach (string tn in new string[] { "Infragistics.Win.Office2010ColorTable", "Infragistics.Win.Office2007ColorTable", "Infragistics.Win.Office2003ColorTable", "Infragistics.Win.VisualStudio2005ColorTable" })
                {
                    Type t = iw.GetType(tn);
                    if (t == null) continue;
                    foreach (FieldInfo f in t.GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public))
                    {
                        if (f.FieldType != typeof(Color[]) || f.IsLiteral) continue;
                        Color[] a = f.GetValue(null) as Color[];
                        if (a != null) igFull[f] = (Color[])a.Clone();
                    }
                }
            }
            catch { }
            origCaptured = true;
            Log("Originais do GeneXus guardados para o modo claro (Office2007=" + origO7 + ", Office2010=" + origO10 + ", ToolStrip=" + origTsMode + ")");
        }

        // chamado por Start (depois de CheckLegacy)
        static void StartToggle()
        {
            Safe("Originais", CaptureOriginals);
            if (StartDark() && !forcarClaro) ApplyGlobals(true);
            else
            {
                On = false; savedFlat = FlatButtons.On; savedLine = LineGuard.On; FlatButtons.On = false; LineGuard.On = false;
                Log("Iniciando no modo CLARO (mode=light no arquivo de configuracao); o botao sol/lua liga o escuro ao vivo");
            }
            Safe("Atalho", KeyFilter.Install);
        }

        static void ApplyGlobals(bool first)
        {
            On = true;
            Safe("AppMode", EnableAppDarkMode);
            Safe("ISL", delegate { LoadIsl(null); });
            Safe("ToolStripRenderer", delegate { ToolStripManager.Renderer = new DarkTsRenderer(); });
            if (SettingsIcons())
            {
                if (!iconsInit) { iconsInit = true; Safe("IconesNext", IconSwap.Init); }
                else Safe("IconesNext", IconSwap.SetDark);
            }
            else if (first) Log("Icones Next desligados pelo arquivo de configuracao (icons=false)");
            FlatButtons.On = savedFlat; LineGuard.On = savedLine;
        }

        public static void Toggle() { SetMode(!On, true); }

        public static void SetMode(bool dark, bool persist)
        {
            if (!started || toggling) return;
            if (dark == On) { ToggleButton.RefreshAll(); return; }
            toggling = true;
            Stopwatch sw = Stopwatch.StartNew();
            List<IntPtr> frozen = Freeze();
            try
            {
                lightSeen.Clear();
                if (dark)
                {
                    ApplyGlobals(false);
                    browserUrls.Clear();
                    bool wasIn = inPass; inPass = false;
                    try { Pass(); } finally { inPass = wasIn; }
                    Safe("Navegadores (sub-frames)", delegate { CefAllFrames(DarkJs, false); });
                }
                else
                {
                    On = false;
                    RestoreLight();
                    lightFullAt = passCount + 1;
                    bool wasIn = inPass; inPass = false;
                    try { LightPass(true); } finally { inPass = wasIn; }
                }
            }
            catch (Exception ex) { Log("Alternar tema: " + ex.GetType().Name + ": " + ex.Message); }
            finally { Unfreeze(frozen); toggling = false; }
            if (persist) SettingsWrite("mode", dark ? "dark" : "light");
            Toggles++;
            LastToggleMs = sw.ElapsedMilliseconds;
            try { ToggleButton.RefreshAll(); } catch { }
            Log("Tema alternado ao vivo para " + (dark ? "ESCURO" : "CLARO") + " em " + sw.ElapsedMilliseconds + " ms" + (dark ? "" : " (propriedades devolvidas=" + Journal.Restored + ", mantidas pelo GeneXus=" + Journal.Skipped + ")"));
        }

        static List<IntPtr> Freeze()
        {
            List<IntPtr> l = new List<IntPtr>();
            try
            {
                List<Form> forms = new List<Form>();
                foreach (Form f in Application.OpenForms) forms.Add(f);
                foreach (Form f in forms)
                {
                    try
                    {
                        if (f.IsDisposed || !f.IsHandleCreated || f.InvokeRequired || !f.Visible || IsExcludedForm(f)) continue;
                        SendMessage(f.Handle, 0x000B /*WM_SETREDRAW*/, IntPtr.Zero, IntPtr.Zero);
                        l.Add(f.Handle);
                    }
                    catch { }
                }
            }
            catch { }
            return l;
        }

        static void Unfreeze(List<IntPtr> l)
        {
            foreach (IntPtr h in l)
            {
                try
                {
                    if (!IsWindow(h)) continue;
                    SendMessage(h, 0x000B, (IntPtr)1, IntPtr.Zero);
                    RedrawWindow(h, IntPtr.Zero, IntPtr.Zero, 0x0001 | 0x0004 | 0x0080 | 0x0100 | 0x0400); // INVALIDATE|ERASE|ALLCHILDREN|UPDATENOW|FRAME
                }
                catch { }
            }
        }

        // ---------- volta ao visual original do GeneXus ----------
        static void RestoreLight()
        {
            savedFlat = FlatButtons.On; savedLine = LineGuard.On;
            FlatButtons.On = false; LineGuard.On = false;
            if (iconsInit) Safe("Icones (claro)", IconSwap.SetLight);
            Safe("Propriedades", delegate { Journal.Restore(); });
            Safe("Abas Crownwood", CrownLight);
            if (dxSeen) Safe("DevExpress", DxUnapply);
            if (editorOk) Safe("Editor", EditorUndo);
            if (ReportLoaded()) Safe("Relatorio", ReportUndo);
            Safe("Documentacao", WikiTheme.Undo);
            Safe("Autocompletar", AutoSuggestTheme.Undo);
            Safe("Navegadores", BrowsersLight);
            Safe("Glifos", GlyphRestore);
            Safe("Listas", LvDark.Reset);
            Safe("Infragistics", IgLight);
            Safe("ToolStripRenderer", TsLight);
            Safe("AppMode", delegate { AppMode(0); });
            Safe("Titulos", TitlesLight);
            Safe("Temas de janela", HandlesLight);
            seen.Clear(); hDone.Clear(); frameDone.Clear(); crownMine.Clear(); crownColored.Clear(); spgDm.Clear(); spgBorder.Clear(); browserUrls.Clear(); paneDark.Clear();
        }

        // Crownwood guarda cores derivadas (_backIDE...) e as dos botoes de seta/fechar: recalcula a partir do BackColor devolvido
        static void CrownLight()
        {
            foreach (object o in new List<object>(crownHooked.Keys))
            {
                Control tc = o as Control;
                if (tc == null || tc.IsDisposed) { crownHooked.Remove(o); continue; }
                try
                {
                    Type t = tc.GetType();
                    if (CrownOrig.Restore(tc))
                    {
                        MethodInfo dbi0 = t.GetMethod("DefineButtonImages", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, Type.EmptyTypes, null);
                        if (dbi0 != null) dbi0.Invoke(tc, null);
                        FieldInfo fr0 = null;
                        for (Type b = t; b != null && fr0 == null; b = b.BaseType) fr0 = b.GetField("_recalculate", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                        if (fr0 != null) fr0.SetValue(tc, true);
                        foreach (Control k in tc.Controls) if (k.GetType().Name == "InertButton") k.Invalidate();
                        tc.Invalidate(true);
                        continue;
                    }
                    Color bc = tc.BackColor;
                    MethodInfo dbc = t.GetMethod("DefineBackColor", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, new Type[] { typeof(Color) }, null);
                    MethodInfo dbi = t.GetMethod("DefineButtonImages", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, Type.EmptyTypes, null);
                    if (dbc != null) dbc.Invoke(tc, new object[] { bc });
                    FieldInfo fd = null;
                    for (Type b = t; b != null && fd == null; b = b.BaseType) fd = b.GetField("_defaultColor", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                    if (fd != null) fd.SetValue(tc, bc == SystemColors.Control);
                    if (dbi != null) dbi.Invoke(tc, null);
                    FieldInfo fr = null;
                    for (Type b = t; b != null && fr == null; b = b.BaseType) fr = b.GetField("_recalculate", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                    if (fr != null) fr.SetValue(tc, true);
                    tc.Invalidate(true);
                }
                catch { }
            }
        }

        static bool ReportLoaded()
        {
            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
                if (a.FullName.StartsWith("DevExpress.XtraReports.v7.3,", StringComparison.Ordinal)) return true;
            return false;
        }
        [MethodImpl(MethodImplOptions.NoInlining)] static void DxUnapply() { DxTreeTheme.Unapply(); }
        [MethodImpl(MethodImplOptions.NoInlining)] static void EditorUndo() { EditorTheme.Undo(); }
        [MethodImpl(MethodImplOptions.NoInlining)] static void ReportUndo() { ReportTheme.Restore(); }

        static void AppMode(int mode)
        {
            if (Environment.OSVersion.Version.Major < 10) return;
            IntPtr ux = LoadLibrary("uxtheme.dll");
            if (ux == IntPtr.Zero) return;
            IntPtr p135 = GetProcAddress(ux, (IntPtr)135), p104 = GetProcAddress(ux, (IntPtr)104), p136 = GetProcAddress(ux, (IntPtr)136);
            if (p135 != IntPtr.Zero) ((SetPreferredAppModeFn)Marshal.GetDelegateForFunctionPointer(p135, typeof(SetPreferredAppModeFn)))(mode);
            if (p104 != IntPtr.Zero) ((VoidFn)Marshal.GetDelegateForFunctionPointer(p104, typeof(VoidFn)))();
            if (p136 != IntPtr.Zero) ((VoidFn)Marshal.GetDelegateForFunctionPointer(p136, typeof(VoidFn)))();
        }

        static void TitlesLight()
        {
            foreach (IntPtr h in new List<IntPtr>(darkTitled))
            {
                try
                {
                    if (!IsWindow(h)) continue;
                    int off = 0;
                    if (DwmSetWindowAttribute(h, 20, ref off, 4) != 0) DwmSetWindowAttribute(h, 19, ref off, 4);
                    int def = unchecked((int)0xFFFFFFFF);   // DWMWA_COLOR_DEFAULT
                    DwmSetWindowAttribute(h, 35, ref def, 4);
                    DwmSetWindowAttribute(h, 36, ref def, 4);
                    SetWindowPos(h, IntPtr.Zero, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0004 | 0x0010 | 0x0020);
                }
                catch { }
            }
            darkTitled.Clear();
        }

        static string OrigTheme(Control c)
        {
            string n = c.Name ?? "";
            return (n == "lstKBs" || n == "lstServers") ? "explorer" : null;
        }

        static void TrackThemed(Control c, IntPtr hdr)
        {
            try { if (c.IsHandleCreated) themedH[c.Handle] = c; } catch { }
            if (hdr != IntPtr.Zero) themedHdr.Add(hdr);
        }

        static void HandlesLight()
        {
            int n = 0;
            foreach (KeyValuePair<IntPtr, Control> kv in new List<KeyValuePair<IntPtr, Control>>(themedH))
            {
                try
                {
                    Control c = kv.Value;
                    if (c.IsDisposed || !c.IsHandleCreated || c.Handle != kv.Key) continue;
                    if (allowDarkForWindow != null) allowDarkForWindow(kv.Key, false);
                    SetWindowTheme(kv.Key, OrigTheme(c), null);
                    SetWindowPos(kv.Key, IntPtr.Zero, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0004 | 0x0010 | 0x0020);
                    n++;
                }
                catch { }
            }
            foreach (IntPtr h in themedHdr) { try { if (!IsWindow(h)) continue; if (allowDarkForWindow != null) allowDarkForWindow(h, false); SetWindowTheme(h, null, null); n++; } catch { } }
            foreach (KeyValuePair<IntPtr, Control> kv in sbThemed)
            {
                try
                {
                    if (kv.Value.IsDisposed || !IsWindow(kv.Key)) continue;
                    SetWindowTheme(kv.Key, null, null);
                    SendMessage(kv.Key, 0x2001 /*SB_SETBKCOLOR*/, IntPtr.Zero, new IntPtr(unchecked((int)0xFF000000)));   // CLR_DEFAULT
                    kv.Value.Invalidate(); n++;
                }
                catch { }
            }
            themedH.Clear(); themedHdr.Clear(); sbThemed.Clear();
            Log("Modo claro: tema do Windows devolvido em " + n + " janelas");
        }

        static void GlyphRestore()
        {
            foreach (KeyValuePair<Control, Image> kv in new List<KeyValuePair<Control, Image>>(glyphOrig))
            {
                try
                {
                    Control c = kv.Key;
                    if (c.IsDisposed) continue;
                    ButtonBase bb = c as ButtonBase; Label lb = c as Label; PictureBox pb = c as PictureBox;
                    Image cur = bb != null ? bb.Image : lb != null ? lb.Image : pb != null ? pb.Image : null;
                    if (cur == null || !IconSwap.IsMonoResult(cur)) continue;
                    if (bb != null) bb.Image = kv.Value; else if (lb != null) lb.Image = kv.Value; else if (pb != null) pb.Image = kv.Value;
                }
                catch { }
            }
            glyphOrig.Clear();
        }

        static string LightIslPath()
        {
            try { string ov = Environment.GetEnvironmentVariable("GXDARK_LIGHT_ISL"); if (!string.IsNullOrEmpty(ov) && File.Exists(ov)) return ov; } catch { }
            foreach (string d in new string[] { Application.StartupPath, AppDomain.CurrentDomain.BaseDirectory })
            {
                try { string p = Path.Combine(d, "GenexusOffice2010Silver.isl"); if (File.Exists(p)) return p; } catch { }
            }
            return null;
        }

        static void IgLight()
        {
            foreach (KeyValuePair<FieldInfo, Color[]> kv in igOrig)
            {
                try { Color[] a = kv.Key.GetValue(null) as Color[]; if (a != null && a.Length == kv.Value.Length) Array.Copy(kv.Value, a, a.Length); } catch { }
            }
            bool ide = false;
            try { ide = Environment.CommandLine.IndexOf("idestyle", StringComparison.OrdinalIgnoreCase) >= 0; } catch { }
            string p = ide ? null : LightIslPath();
            bool loaded = false;
            if (p != null)
            {
                try
                {
                    XmlDocument doc = new XmlDocument();
                    doc.Load(p);
                    int scaled = 16;
                    try { using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) scaled = (int)Math.Round(16 * g.DpiY / 96f); } catch { }
                    foreach (XmlNode n in doc.SelectNodes("//sharedObject[@name='ScrollBar']/properties/property"))
                    {
                        int dummy;
                        if (n.FirstChild != null && int.TryParse(n.FirstChild.Value, out dummy)) n.FirstChild.Value = scaled.ToString();
                    }
                    using (MemoryStream ms = new MemoryStream()) { doc.Save(ms); ms.Position = 0; Infragistics.Win.AppStyling.StyleManager.Load(ms); }
                    loaded = true;
                }
                catch (Exception ex) { Log("Modo claro: ISL original nao carregou (" + ex.Message + "); usando o estilo padrao"); }
            }
            if (!loaded) { try { Infragistics.Win.AppStyling.StyleManager.Reset(); } catch { } }
            if (origCaptured)
            {
                try { Office2007ColorTable.ColorScheme = origO7; Office2007ColorTable.CustomBlendColor = origBlend7; } catch { }
                try { Office2010ColorTable.ColorScheme = origO10; Office2010ColorTable.CustomBlendColor = origBlend10; } catch { }
                // o Infragistics recalcula parte das tabelas ao trocar o esquema: devolve as tabelas inteiras como estavam
                foreach (KeyValuePair<FieldInfo, Color[]> kv in igFull)
                {
                    try { Color[] a = kv.Key.GetValue(null) as Color[]; if (a != null && a.Length == kv.Value.Length) Array.Copy(kv.Value, a, a.Length); else kv.Key.SetValue(null, kv.Value.Clone()); } catch { }
                }
            }
            Log("Modo claro: Infragistics com o estilo original (" + (loaded ? p : ide ? "idestyle: padrao" : "padrao") + ")");
        }

        static void TsLight()
        {
            if (origTsMode == ToolStripManagerRenderMode.Custom && origTsRenderer != null) ToolStripManager.Renderer = origTsRenderer;
            else ToolStripManager.RenderMode = origTsMode == ToolStripManagerRenderMode.Custom ? ToolStripManagerRenderMode.Professional : origTsMode;
        }

        // ---------- navegadores ----------
        const string LightJs =
            "(function(){try{" +
            "if(window.__gxMonUndo)window.__gxMonUndo();" +
            "var r=document.getElementById('gxNextReport');if(r&&r.parentNode)r.parentNode.removeChild(r);" +
            "var q=document.querySelectorAll?document.querySelectorAll('[data-gxn]'):[];for(var i=0;i<q.length;i++)q[i].removeAttribute('data-gxn');" +
            "if(window.__gxDsoUndo)window.__gxDsoUndo();" +
            "if(window.__gxUndo)window.__gxUndo();" +
            "try{var de=document.documentElement,ov=de.style.overflow;de.style.overflow='hidden';void de.offsetHeight;de.style.overflow=ov;void de.offsetHeight;}catch(e){}" +
            "}catch(e){}})();";

        static void BrowsersLight()
        {
            int n = CefAllFrames(LightJs, true);
            foreach (object b in new List<object>(browserUrls.Keys))
            {
                WebBrowser wb = b as WebBrowser;
                if (wb == null || wb.IsDisposed || wb.Document == null) continue;
                try { wb.Document.InvokeScript("eval", new object[] { LightJs }); n++; } catch { }
            }
            Log("Modo claro: script de volta executado em " + n + " navegador(es)/frame(s)");
        }

        static MethodInfo FindMethod(object o, string name, Type[] args)
        {
            Type t = o.GetType();
            MethodInfo m = null;
            try { m = t.GetMethod(name, args); } catch { }
            if (m != null) return m;
            foreach (Type it in t.GetInterfaces()) { try { m = it.GetMethod(name, args); } catch { } if (m != null) return m; }
            return null;
        }

        // main=true: tambem o frame principal (no escuro o principal vai pelo Browser/RunCef)
        static int CefAllFrames(string js, bool main)
        {
            int n = 0;
            foreach (object b in new List<object>(browserHooked))
            {
                Control c = b as Control;
                if (c == null || c.IsDisposed) continue;
                try
                {
                    if (main)
                    {
                        string addr = null;
                        try { PropertyInfo pa = b.GetType().GetProperty("Address"); if (pa != null) addr = pa.GetValue(b, null) as string; } catch { }
                        if (!IsPreviewUrl(addr) && RunCefJs(b, js)) n++;
                    }
                    MethodInfo gb = FindMethod(b, "GetBrowser", Type.EmptyTypes);
                    object ib = gb == null ? null : gb.Invoke(b, null);
                    if (ib == null) continue;
                    MethodInfo names = FindMethod(ib, "GetFrameNames", Type.EmptyTypes), getf = FindMethod(ib, "GetFrame", new Type[] { typeof(string) });
                    if (names == null || getf == null) continue;
                    IEnumerable ns = names.Invoke(ib, null) as IEnumerable;
                    if (ns == null) continue;
                    foreach (object nm in ns)
                    {
                        try
                        {
                            object fr = getf.Invoke(ib, new object[] { nm });
                            if (fr == null) continue;
                            PropertyInfo pm = fr.GetType().GetProperty("IsMain");
                            if (pm == null) foreach (Type it in fr.GetType().GetInterfaces()) { pm = it.GetProperty("IsMain"); if (pm != null) break; }
                            if (pm == null || (bool)pm.GetValue(fr, null)) continue;
                            PropertyInfo pu = fr.GetType().GetProperty("Url");
                            if (pu == null) foreach (Type it in fr.GetType().GetInterfaces()) { pu = it.GetProperty("Url"); if (pu != null) break; }
                            string url = pu == null ? null : pu.GetValue(fr, null) as string;
                            if (string.IsNullOrEmpty(url) || url.StartsWith("about:", StringComparison.OrdinalIgnoreCase) || IsPreviewUrl(url)) continue;
                            MethodInfo ex = FindMethod(fr, "ExecuteJavaScriptAsync", new Type[] { typeof(string), typeof(string), typeof(int) });
                            if (ex != null) { ex.Invoke(fr, new object[] { js, url, 1 }); n++; }
                        }
                        catch { }
                    }
                }
                catch { }
            }
            return n;
        }

        // ---------- passada no modo claro: so devolve icones que o GeneXus guardou na versao Next ----------
        static void LightPass(bool force)
        {
            if (inPass) return;
            inPass = true;
            try
            {
                passCount++;
                if (passCount % 40 == 0) { lightSeen.RemoveWhere(delegate(Control x) { return x.IsDisposed; }); try { IconSwap.Purge(); } catch { } }
                if (!IconSwap.Reverse) return;
                bool full = force || passCount == lightFullAt || passCount % 15 == 0;
                List<Form> forms = new List<Form>();
                foreach (Form f in Application.OpenForms) forms.Add(f);
                foreach (Form f in forms)
                {
                    try
                    {
                        if (f.IsDisposed || !f.IsHandleCreated || f.InvokeRequired || IsExcludedForm(f)) continue;
                        LightWalk(f, 0, full);
                    }
                    catch { }
                }
            }
            catch (Exception ex) { LogOnce("lightpass:" + ex.GetType().Name, "Passada (modo claro): " + ex.Message); }
            finally { inPass = false; }
        }

        static void LightWalk(Control c, int depth, bool full)
        {
            if (c == null || c.IsDisposed || depth > 80 || c is ToggleButton) return;
            if (IsDesignHosted(c) || IsPreviewControl(c)) return;
            bool first = lightSeen.Add(c);
            if (first || full) IconSwap.LightVisit(c, true);
            string tn = c.GetType().FullName;
            if (tn.StartsWith("CefSharp.", StringComparison.Ordinal) || c is WebBrowser) return;
            Control[] kids;
            try { kids = new Control[c.Controls.Count]; c.Controls.CopyTo(kids, 0); } catch { return; }
            foreach (Control k in kids) LightWalk(k, depth + 1, full);
        }

        static void Housekeeping()
        {
            if (passCount % 40 != 39) return;
            try { Journal.Purge(); } catch { }
            tabPaintHooked.RemoveWhere(delegate(Control x) { return x.IsDisposed; });
            openingHooked.RemoveWhere(delegate(ToolStripDropDown x) { return x.IsDisposed; });
            crownColored.RemoveWhere(delegate(Control x) { return x.IsDisposed; });
            paneDark.RemoveWhere(delegate(Control x) { return x.IsDisposed; });
        }

        // ---------- atalho Ctrl+Shift+F12 ----------
        internal sealed class KeyFilter : IMessageFilter
        {
            static KeyFilter inst;
            internal static volatile bool Enabled = true;
            public static void Install()
            {
                if (inst != null) return;
                inst = new KeyFilter();
                Application.AddMessageFilter(inst);
                Log("Atalho Ctrl+Shift+F12 (alternar claro/escuro) instalado");
            }
            public static void Uninstall() { if (inst != null) { try { Application.RemoveMessageFilter(inst); } catch { } inst = null; } }
            public bool PreFilterMessage(ref Message m)
            {
                if (m.Msg != 0x0100 || (long)m.WParam != 0x7B || !Enabled) return false;
                if ((Control.ModifierKeys & (Keys.Control | Keys.Shift | Keys.Alt)) != (Keys.Control | Keys.Shift)) return false;
                if (((long)m.LParam & 0x40000000L) != 0) return true;     // repeticao da tecla segurada
                try { DarkEngine.Toggle(); } catch { }
                return true;
            }
        }
    }

    // ================= botao sol/lua no fim da barra de menus =================
    public sealed class ToggleButton : Control
    {
        static ToggleButton btn;
        static Control dock;
        static int ensureCalls;
        static bool shortcutChecked, placeLogged, hiddenLogged;
        static System.Windows.Forms.ToolTip tip;
        static readonly Dictionary<string, Bitmap> cache = new Dictionary<string, Bitmap>();
        static readonly Dictionary<string, byte[]> masks = new Dictionary<string, byte[]>();
        static readonly Dictionary<string, int> maskW = new Dictionary<string, int>();
        bool hover, pressed;

        ToggleButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Name = "gxDarkToggle";
            AccessibleRole = AccessibleRole.PushButton;
            AccessibleDescription = "Ctrl+Shift+F12";
        }

        public static Control Instance { get { return btn; } }
        public static string TipText { get { return (DarkEngine.On ? "Alternar para modo claro" : "Alternar para modo escuro") + " (Ctrl+Shift+F12). Botão direito: escolher o tema (" + DarkEngine.TemaAtual.Nome + ")"; } }

        public static void Ensure()
        {
            ensureCalls++;
            if (btn != null && !btn.IsDisposed && dock != null && !dock.IsDisposed && btn.Parent == dock) { Place(); return; }
            if (btn != null && btn.IsDisposed) btn = null;
            if (ensureCalls > 3 && ensureCalls % 3 != 0) return;
            Control d = FindDock();
            if (d == null) return;
            if (dock != null && !dock.IsDisposed) dock.Resize -= OnDockResize;
            dock = d;
            if (btn == null)
            {
                btn = new ToggleButton();
                if (tip == null) tip = new System.Windows.Forms.ToolTip { ShowAlways = true, InitialDelay = 400 };
            }
            btn.Visible = false;
            dock.Controls.Add(btn);
            dock.Resize -= OnDockResize; dock.Resize += OnDockResize;
            RefreshAll();
            Place();
            if (!shortcutChecked) { shortcutChecked = true; CheckShortcut(); }
        }

        public static void Remove()
        {
            try { if (btn != null && !btn.IsDisposed) { if (btn.Parent != null) btn.Parent.Controls.Remove(btn); btn.Dispose(); } } catch { }
            btn = null;
            try { if (dock != null && !dock.IsDisposed) dock.Resize -= OnDockResize; } catch { }
            try { if (hostForm != null && !hostForm.IsDisposed) { hostForm.Resize -= OnDockResize; hostForm.SizeChanged -= OnDockResize; UnhookDpi(hostForm); } } catch { }
            hostForm = null;
            dock = null;
        }

        static void OnDockResize(object s, EventArgs e) { try { Place(); } catch { } }

        const string DockType = "Infragistics.Win.UltraWinToolbars.UltraToolbarsDockArea";
        static Control FindDock()
        {
            List<Form> forms = new List<Form>();
            foreach (Form f in Application.OpenForms) forms.Add(f);
            foreach (Form f in forms)
            {
                try
                {
                    if (f.IsDisposed || !f.IsHandleCreated || f.InvokeRequired) continue;
                    Control d = FindDockIn(f, 0);
                    if (d != null) return d;
                }
                catch { }
            }
            return null;
        }

        static Control FindDockIn(Control c, int depth)
        {
            foreach (Control k in c.Controls)
                if (k.GetType().FullName == DockType && k.Dock == DockStyle.Top && k.Visible && MenuRect(k) != null) return k;
            if (depth >= 2) return null;
            foreach (Control k in c.Controls)
            {
                if (k.GetType().FullName == DockType) continue;
                Control r = FindDockIn(k, depth + 1);
                if (r != null) return r;
            }
            return null;
        }

        static Type tbBase;
        static UIElement Root(Control d)
        {
            IUltraControlElement ce = d as IUltraControlElement;
            return ce == null ? null : ce.MainUIElement;
        }

        static bool IsMenuBarCtx(object ctx)
        {
            if (ctx == null) return false;
            PropertyInfo p = ctx.GetType().GetProperty("IsMainMenuBar", BindingFlags.Public | BindingFlags.Instance);
            try { return p != null && (bool)p.GetValue(ctx, null); } catch { return false; }
        }

        // elemento da barra marcada como barra de menus principal
        static UIElement MenuElement(Control d, out UIElement root)
        {
            root = Root(d);
            if (root == null) return null;
            if (tbBase == null) tbBase = d.GetType().Assembly.GetType("Infragistics.Win.UltraWinToolbars.UltraToolbarBase");
            if (tbBase == null) return null;
            Queue<UIElement> q = new Queue<UIElement>();
            q.Enqueue(root);
            int guard = 0;
            while (q.Count > 0 && guard++ < 4000)
            {
                UIElement e = q.Dequeue();
                if (!object.ReferenceEquals(e, root))
                {
                    object ctx = null;
                    try { ctx = e.GetContext(tbBase, false); } catch { }
                    if (ctx != null && IsMenuBarCtx(ctx)) return e;
                }
                UIElementsCollection kids = e.ChildElements;
                if (kids != null) foreach (UIElement k in kids) q.Enqueue(k);
            }
            return null;
        }

        static Rectangle? MenuRect(Control d)
        {
            UIElement root;
            UIElement m = MenuElement(d, out root);
            if (m == null) return null;
            return m.Rect;
        }

        static void Collect(UIElement e, List<UIElement> l, int depth)
        {
            if (e == null || depth > 8) return;
            l.Add(e);
            UIElementsCollection kids = e.ChildElements;
            if (kids != null) foreach (UIElement k in kids) Collect(k, l, depth + 1);
        }

        public static int MaskSize()
        {
            int dpi = 96;
            try { using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) dpi = (int)Math.Round(g.DpiY); } catch { }
            double want = 16.0 * dpi / 96.0;
            int best = 16;
            foreach (int s in new int[] { 16, 20, 24, 32 }) if (Math.Abs(s - want) < Math.Abs(best - want)) best = s;
            return best;
        }

        public static Rectangle LastMenu;
        public static string LastAlign = "";

        [StructLayout(LayoutKind.Sequential)] struct TbRect { public int L, T, R, B; }
        [StructLayout(LayoutKind.Sequential)]
        struct TitleBarInfoEx
        {
            public int cbSize; public TbRect rcTitleBar;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)] public int[] rgstate;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)] public TbRect[] rgrect;
        }
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, ref TitleBarInfoEx t);

        // centro X (em coordenadas do dock) do botao Fechar da barra de titulo; int.MinValue se nao houver
        static int CloseCenterX()
        {
            try
            {
                Form f = dock.FindForm();
                if (f == null || !f.IsHandleCreated || f.WindowState == FormWindowState.Minimized || f.FormBorderStyle == FormBorderStyle.None) return int.MinValue;
                TitleBarInfoEx t = new TitleBarInfoEx();
                t.cbSize = Marshal.SizeOf(typeof(TitleBarInfoEx)); t.rgstate = new int[6]; t.rgrect = new TbRect[6];
                SendMessage(f.Handle, 0x033F, IntPtr.Zero, ref t);   // WM_GETTITLEBARINFOEX
                TbRect r = t.rgrect[5];
                if ((t.rgstate[5] & 0x8000) != 0 || r.R <= r.L) return int.MinValue;   // STATE_SYSTEM_INVISIBLE
                Point p = dock.PointToClient(new Point((r.L + r.R) / 2, r.T));
                return p.X;
            }
            catch { return int.MinValue; }
        }

        static Form hostForm;
        static void HookForm()
        {
            Form f = dock == null ? null : dock.FindForm();
            if (f == null || object.ReferenceEquals(f, hostForm)) return;
            try { if (hostForm != null && !hostForm.IsDisposed) { hostForm.Resize -= OnDockResize; hostForm.SizeChanged -= OnDockResize; UnhookDpi(hostForm); } } catch { }
            hostForm = f;
            try
            {
                f.Resize -= OnDockResize; f.Resize += OnDockResize;
                f.SizeChanged -= OnDockResize; f.SizeChanged += OnDockResize;
                EventInfo ei = typeof(Form).GetEvent("DpiChanged");   // .NET 4.7+
                if (ei != null) { if (dpiHandler == null) dpiHandler = Delegate.CreateDelegate(ei.EventHandlerType, typeof(ToggleButton).GetMethod("OnDpi", BindingFlags.Static | BindingFlags.NonPublic)); ei.RemoveEventHandler(f, dpiHandler); ei.AddEventHandler(f, dpiHandler); }
            }
            catch { }
        }
        static Delegate dpiHandler;
        static void UnhookDpi(Form f) { try { EventInfo ei = typeof(Form).GetEvent("DpiChanged"); if (ei != null && dpiHandler != null) ei.RemoveEventHandler(f, dpiHandler); } catch { } }
        static void OnDpi(object s, EventArgs e)
        {
            try { if (btn != null && !btn.IsDisposed) btn.BeginInvoke(new MethodInvoker(delegate { try { Place(); btn.Invalidate(); } catch { } })); } catch { }
        }

        static void Place()
        {
            if (btn == null || dock == null || dock.IsDisposed) return;
            UIElement root;
            UIElement m = MenuElement(dock, out root);
            if (m == null) { if (btn.Visible) btn.Visible = false; return; }
            Rectangle mr = m.Rect;
            LastMenu = mr;
            int ms = MaskSize();
            int side = Math.Max(ms + 4, Math.Min(mr.Height - 2, ms + 10));
            List<UIElement> all = new List<UIElement>();
            Collect(m, all, 0);
            int qcLeft = int.MaxValue, toolRight = mr.Left;
            foreach (UIElement e in all)
            {
                if (object.ReferenceEquals(e, m)) continue;
                string tn = e.GetType().Name;
                if (tn.IndexOf("QuickCustomize", StringComparison.Ordinal) >= 0) { qcLeft = Math.Min(qcLeft, e.Rect.Left); continue; }
                if (tn.IndexOf("Tool", StringComparison.Ordinal) >= 0 && e.Rect.Width < mr.Width / 2 && e.Rect.Width > 0) toolRight = Math.Max(toolRight, e.Rect.Right);
            }
            int cw = dock.ClientSize.Width;
            int right = (qcLeft != int.MaxValue ? qcLeft : mr.Right) - 2;
            if (mr.Right < cw - side - 6)
            {
                // a barra de menus nao ocupa a linha inteira: usa o fim da linha se estiver livre
                Rectangle want = new Rectangle(cw - 2 - side, mr.Top, side, mr.Height);
                bool free = true;
                if (root != null && root.ChildElements != null)
                    foreach (UIElement e in root.ChildElements)
                        if (!object.ReferenceEquals(e, m) && e.Rect.IntersectsWith(want)) { free = false; break; }
                if (free) right = cw - 2;
            }
            int x = right - side, y = mr.Top + (mr.Height - side) / 2;
            // vertical: centro da linha dos itens do menu (File, Edit, ...), nao do retangulo da barra
            int tTop = int.MaxValue, tBot = int.MinValue;
            foreach (UIElement e in all)
            {
                if (!(e is TextUIElement) || e.Rect.Height <= 0 || e.Rect.Width <= 0) continue;
                tTop = Math.Min(tTop, e.Rect.Top); tBot = Math.Max(tBot, e.Rect.Bottom);
            }
            if (tTop != int.MaxValue && tBot - tTop < mr.Height + 4)
            {
                y = (tTop + tBot + 1) / 2 - side / 2;
                y = Math.Max(mr.Top, Math.Min(y, mr.Bottom - side));
            }
            // horizontal: na coluna do botao Fechar (X) da janela, quando o fim da linha esta livre
            LastAlign = "margem";
            if (right == cw - 2)
            {
                int cx = CloseCenterX();
                if (cx != int.MinValue && cx - side / 2 > toolRight + 4 && cx + (side + 1) / 2 <= cw)
                {
                    x = cx - side / 2;
                    LastAlign = "coluna do X da janela";
                }
            }
            if (x < toolRight + 4)
            {
                if (btn.Visible) btn.Visible = false;
                if (!hiddenLogged) { hiddenLogged = true; DarkEngine.Log("Botao sol/lua: sem espaco livre no fim da barra de menus (janela estreita); oculto ate haver espaco. O atalho Ctrl+Shift+F12 continua valendo."); }
                return;
            }
            Rectangle b = new Rectangle(x, y, side, side);
            if (btn.Bounds != b) btn.Bounds = b;
            if (!btn.Visible) { btn.Visible = true; btn.BringToFront(); }
            HookForm();
            if (!placeLogged) { placeLogged = true; DarkEngine.Log("Botao sol/lua no fim da barra de menus: " + b + " (barra " + mr + ", icone " + ms + " px, alinhado: " + LastAlign + ", texto do menu y=" + (tTop == int.MaxValue ? "?" : tTop + ".." + tBot) + ")"); }
        }

        static void CheckShortcut()
        {
            try
            {
                PropertyInfo pm = dock.GetType().GetProperty("ToolbarsManager");
                object mgr = pm == null ? null : pm.GetValue(dock, null);
                if (mgr == null) return;
                IEnumerable tools = mgr.GetType().GetProperty("Tools").GetValue(mgr, null) as IEnumerable;
                if (tools == null) return;
                foreach (object t in tools)
                {
                    try
                    {
                        object sp = t.GetType().GetProperty("SharedProps").GetValue(t, null);
                        object sc = sp.GetType().GetProperty("Shortcut").GetValue(sp, null);
                        if (sc is Shortcut && (Shortcut)sc == Shortcut.CtrlShiftF12)
                        {
                            DarkEngine.KeyFilter.Enabled = false;
                            DarkEngine.Log("Atalho Ctrl+Shift+F12 ja usado pelo GeneXus (" + t.GetType().GetProperty("Key").GetValue(t, null) + "): atalho do tema desligado, use o botao sol/lua");
                            return;
                        }
                    }
                    catch { }
                }
                DarkEngine.Log("Atalho Ctrl+Shift+F12 livre (nenhuma ferramenta do GeneXus usa)");
            }
            catch { }
        }

        public static void RefreshAll()
        {
            if (btn == null || btn.IsDisposed) return;
            string t = TipText;
            btn.AccessibleName = t;
            btn.Text = t;
            try { if (tip != null) tip.SetToolTip(btn, t + " (Ctrl+Shift+F12)"); } catch { }
            btn.Invalidate();
        }

        static Color Hx(string h) { return Color.FromArgb(Convert.ToInt32(h.Substring(1, 2), 16), Convert.ToInt32(h.Substring(3, 2), 16), Convert.ToInt32(h.Substring(5, 2), 16)); }

        // cores das 3 opcoes (mesmas da previa sol-lua-preview.png)
        public static Color IconColor(string opt, bool dark, bool hover)
        {
            string c;
            if (opt == "contorno") c = dark ? DarkEngine.TH(hover ? "#F0F4FA" : "#DCE2EB") : (hover ? "#1F2A36" : "#2D3A48");
            else if (dark) c = hover ? "#F0BB6A" : "#E5A54B";              // sol ambar (no escuro)
            else if (opt == "azul") c = hover ? "#1A56B0" : "#1F63C9";      // lua azul (no claro)
            else c = hover ? "#2D3A48" : "#3D4B5C";                         // lua ardosia (no claro)
            return Hx(c);
        }

        static byte[] Mask(string name, int size, out int w)
        {
            string key = name + "_" + size;
            byte[] a;
            if (masks.TryGetValue(key, out a)) { w = maskW[key]; return a; }
            w = size;
            using (Stream s = typeof(ToggleButton).Assembly.GetManifestResourceStream("GxIcons." + key + ".png"))
            {
                if (s == null) return null;
                using (Bitmap b = new Bitmap(s))
                {
                    w = b.Width;
                    a = new byte[b.Width * b.Height];
                    for (int y = 0; y < b.Height; y++) for (int x = 0; x < b.Width; x++) a[y * b.Width + x] = b.GetPixel(x, y).R;
                }
            }
            masks[key] = a; maskW[key] = w;
            return a;
        }

        // escuro -> SOL (clicar = claro); claro -> LUA (clicar = escuro)
        public static Bitmap Icon(string opt, bool dark, bool hover, int size)
        {
            string shape = opt == "contorno" ? "A" : "B";
            string name = "toggle_" + shape + "_" + (dark ? "sun" : "moon");
            string key = opt + "|" + name + "|" + size + "|" + hover;
            Bitmap r;
            if (cache.TryGetValue(key, out r)) return r;
            int w;
            byte[] m = Mask(name, size, out w);
            if (m == null) return null;
            Color c = IconColor(opt, dark, hover);
            r = new Bitmap(w, w, PixelFormat.Format32bppArgb);
            for (int y = 0; y < w; y++) for (int x = 0; x < w; x++) { byte al = m[y * w + x]; if (al != 0) r.SetPixel(x, y, Color.FromArgb(al, c)); }
            cache[key] = r;
            return r;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            try
            {
                Graphics g = e.Graphics;
                bool dark = DarkEngine.On;
                if (hover || pressed)
                {
                    Color fill = pressed ? (dark ? DarkEngine.MenuHot : Hx("#FBD872")) : (dark ? DarkEngine.HotBg : Hx("#FDEBA6"));
                    Color bd = dark ? DarkEngine.HotBorder : Hx("#F0C65A");
                    using (SolidBrush b = new SolidBrush(fill)) g.FillRectangle(b, 0, 0, Width, Height);
                    using (Pen p = new Pen(bd)) g.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
                }
                Bitmap ic = Icon(DarkEngine.IconOption(), dark, hover, MaskSize());
                if (ic != null) g.DrawImageUnscaled(ic, (Width - ic.Width) / 2, (Height - ic.Height) / 2);
            }
            catch { }
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { pressed = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            bool was = pressed;
            pressed = false; Invalidate();
            base.OnMouseUp(e);
            if (was && e.Button == MouseButtons.Left && ClientRectangle.Contains(e.Location))
            {
                try { BeginInvoke((MethodInvoker)delegate { DarkEngine.Toggle(); }); } catch { }
            }
            else if (e.Button == MouseButtons.Right && ClientRectangle.Contains(e.Location))
            {
                try { MenuTema.MostrarNoBotao(this, e.Location); } catch (Exception ex) { DarkEngine.Log("Menu do botao sol/lua: " + ex.Message); }
            }
        }

        // para os testes: estado visual
        public static void SetHover(bool h) { if (btn != null) { btn.hover = h; btn.Invalidate(); } }
    }

    // ================= icones no modo claro: devolve os originais que o GeneXus guardou na versao Next =================
    public static partial class IconSwap
    {
        public static volatile bool Reverse;
        sealed class RevRec { public IconFamily Fam; public object Orig; public int Base, Resize, IlSize; public ColorDepth Depth; }
        static readonly RevRec NoRev = new RevRec();
        static Dictionary<long, RevRec> revMap;
        static Dictionary<string, object> origByKey;
        static int revBuiltSeeds = -1;
        static ConditionalWeakTable<Image, RevRec> revKnown = new ConditionalWeakTable<Image, RevRec>();
        static readonly Dictionary<object, Dictionary<string, Bitmap>> revCache = new Dictionary<object, Dictionary<string, Bitmap>>(RefEq.I);
        static readonly Dictionary<UIElement, Image> revSwapped = new Dictionary<UIElement, Image>();
        static readonly Dictionary<ToolStripItem, Image> revItems = new Dictionary<ToolStripItem, Image>();
        static readonly Dictionary<ImageList, ListRec> revLists = new Dictionary<ImageList, ListRec>();
        static readonly Dictionary<object, KeyValuePair<PropertyInfo, object>> revPages = new Dictionary<object, KeyValuePair<PropertyInfo, object>>();
        public static int RevCount { get { return revMap == null ? 0 : revMap.Count; } }
        public static int RevHits;
        public static long RevBuildMs;

        static Bitmap BaseOf(object o, int b)
        {
            Icon ic = o as Icon;
            if (ic != null)
            {
                if (b <= 0) return ic.ToBitmap();
                using (Icon f = new Icon(ic, b, b)) return f.ToBitmap();
            }
            Image im = o as Image;
            if (im == null) return null;
            Bitmap r = new Bitmap(im.Width, im.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(r)) { g.CompositingMode = CompositingMode.SourceCopy; g.DrawImage(im, new Rectangle(0, 0, im.Width, im.Height)); }
            return r;
        }

        static Bitmap Degrade(Bitmap b, int size, ColorDepth d)
        {
            using (ImageList il = new ImageList())
            {
                il.ColorDepth = d; il.ImageSize = new Size(size, size);
                il.Images.Add(b);
                return (Bitmap)il.Images[0];
            }
        }

        static void AddRev(Bitmap b, IconFamily fam, object orig, int bs, int rs, int il, ColorDepth d)
        {
            int w, h;
            int[] px = Pixels(b, out w, out h);
            if (px == null) return;
            short[] s = Sig(px);
            int opaque = 0; foreach (short v in s) if (v != 0) opaque++;
            if (opaque < 6) return;
            long k = Hash(s);
            if (!revMap.ContainsKey(k)) revMap[k] = new RevRec { Fam = fam, Orig = orig, Base = bs, Resize = rs, IlSize = il, Depth = d };
        }

        static void BuildReverse()
        {
            lock (sync)
            {
                if (revMap != null && revBuiltSeeds == seeds.Count) return;
                Stopwatch sw = Stopwatch.StartNew();
                revMap = new Dictionary<long, RevRec>();
                origByKey = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                HashSet<object> doneRepl = new HashSet<object>(RefEq.I);
                int d16 = DpiScale(16);
                foreach (SeedRec s in seeds)
                {
                    if (s.Repl == null || s.Orig == null) continue;
                    if (!origByKey.ContainsKey(s.Key)) origByKey[s.Key] = s.Orig;
                    if (!doneRepl.Add(s.Repl)) continue;
                    List<int> bases = new List<int> { 0 };
                    if (s.Repl is Icon) foreach (int z in new int[] { 16, d16 }) if (!bases.Contains(z)) bases.Add(z);
                    foreach (int bs in bases)
                    {
                        Bitmap b = null;
                        try
                        {
                            b = BaseOf(s.Repl, bs);
                            if (b == null) continue;
                            AddRev(b, s.Fam, s.Orig, bs, 0, 0, 0);
                            List<int> rs = new List<int>();
                            foreach (int z in new int[] { 16, d16, DpiScale(b.Width) }) if (z != b.Width && !rs.Contains(z)) rs.Add(z);
                            foreach (int z in rs) using (Bitmap r = GxResize(b, z)) AddRev(r, s.Fam, s.Orig, bs, z, 0, 0);
                            if (b.Width == 16 || b.Width == d16)
                                foreach (ColorDepth d in new ColorDepth[] { ColorDepth.Depth8Bit, ColorDepth.Depth24Bit })
                                {
                                    try { using (Bitmap r = Degrade(b, b.Width, d)) AddRev(r, s.Fam, s.Orig, bs, 0, b.Width, d); } catch { }
                                }
                        }
                        catch { }
                        finally { if (b != null) b.Dispose(); }
                    }
                }
                revBuiltSeeds = seeds.Count;
                revKnown = new ConditionalWeakTable<Image, RevRec>();
                RevBuildMs = sw.ElapsedMilliseconds;
                DarkEngine.Log("Icones (modo claro): mapa Next->original com " + revMap.Count + " impressoes de " + doneRepl.Count + " icones em " + sw.ElapsedMilliseconds + " ms");
            }
        }

        static RevRec RevLookup(Image img)
        {
            if (img == null || revMap == null || IsMonoResult(img)) return null;
            RevRec r;
            if (revKnown.TryGetValue(img, out r)) return object.ReferenceEquals(r, NoRev) ? null : r;
            r = null;
            try
            {
                int w, h;
                int[] px = Pixels(img, out w, out h);
                if (px != null) { RevRec x; if (revMap.TryGetValue(Hash(Sig(px)), out x)) r = x; }
            }
            catch { }
            try { revKnown.Add(img, r ?? NoRev); } catch { }
            return r;
        }

        // aplica no original o mesmo caminho (base/redimensionar/lista) que o GeneXus aplicou na versao Next
        static Bitmap RevRender(RevRec r, object orig, int w, int h, bool forList)
        {
            if (orig == null) orig = r.Orig;
            string key = r.Base + "|" + r.Resize + "|" + (forList ? 0 : r.IlSize) + "|" + (int)r.Depth + "|" + w + "x" + h;
            Dictionary<string, Bitmap> d;
            lock (sync)
            {
                if (!revCache.TryGetValue(orig, out d)) { d = new Dictionary<string, Bitmap>(); revCache[orig] = d; }
                Bitmap c;
                if (d.TryGetValue(key, out c)) return c;
            }
            Bitmap b = BaseOf(orig, r.Base);
            if (b == null) return null;
            if (r.Resize > 0 && b.Width != r.Resize) { Bitmap x = GxResize(b, r.Resize); b.Dispose(); b = x; }
            if (!forList && r.IlSize > 0) { Bitmap x = Degrade(b, r.IlSize, r.Depth); b.Dispose(); b = x; }
            if ((b.Width != w || b.Height != h) && w == h && w > 0) { Bitmap x = GxResize(b, w); b.Dispose(); b = x; }
            lock (sync) d[key] = b;
            return b;
        }

        static object OrigForTool(string tk, IconFamily fam)
        {
            if (string.IsNullOrEmpty(tk) || origByKey == null) return null;
            try { FamilyForCommand(tk); } catch { }
            lock (sync)
            {
                if (cmdImage == null) return null;
                foreach (string k in KeyCandidates(tk))
                {
                    string im; IconFamily f; object o;
                    if (cmdImage.TryGetValue(k, out im) && famByKey.TryGetValue(im, out f) && f == fam && origByKey.TryGetValue(im, out o)) return o;
                }
            }
            return null;
        }

        static readonly ColorMatrix grayM = new ColorMatrix(new float[][] {
            new float[] { 0.3f, 0.3f, 0.3f, 0, 0 }, new float[] { 0.59f, 0.59f, 0.59f, 0, 0 }, new float[] { 0.11f, 0.11f, 0.11f, 0, 0 },
            new float[] { 0, 0, 0, 0.55f, 0 }, new float[] { 0.12f, 0.12f, 0.12f, 0, 1 } });

        internal static bool BeforeReverse(ref UIElementDrawParams p, bool toolbar)
        {
            UIElement e = p.Element;
            ImageUIElementBase ie = e as ImageUIElementBase;
            if (ie != null)
            {
                Image img = ie.Image;
                if (img == null) return false;
                RevRec r = RevLookup(img);
                if (r == null) return false;
                object orig = toolbar ? OrigForTool(ToolKey(e), r.Fam) : null;
                Bitmap v = RevRender(r, orig, img.Width, img.Height, false);
                if (v == null) return false;
                RevHits++;
                if (e is ImageUIElement)
                {
                    revSwapped[e] = img;
                    ((ImageUIElement)e).Image = v;
                    return false;
                }
                Rectangle rr = ie.RectInsideBorders;
                Rectangle tr;
                if (ie.Scaled) tr = rr;
                else
                {
                    tr = new Rectangle(rr.X, rr.Y, img.Width, img.Height);
                    if (ie.CenterImage) { if (img.Width < rr.Width) tr.X += (rr.Width - img.Width) / 2; if (img.Height < rr.Height) tr.Y += (rr.Height - img.Height) / 2; }
                }
                Region old = p.Graphics.Clip;
                try
                {
                    p.Graphics.SetClip(rr, CombineMode.Intersect);
                    if (ie.Enabled) p.Graphics.DrawImage(v, tr);
                    else using (ImageAttributes ia = new ImageAttributes()) { ia.SetColorMatrix(grayM); p.Graphics.DrawImage(v, tr, 0, 0, v.Width, v.Height, GraphicsUnit.Pixel, ia); }
                }
                finally { p.Graphics.Clip = old; }
                return true;
            }
            Image ai = p.AppearanceData.Image as Image;
            if (ai != null)
            {
                RevRec r = RevLookup(ai);
                if (r != null) { Bitmap v = RevRender(r, null, ai.Width, ai.Height, false); if (v != null) { p.AppearanceData.Image = v; RevHits++; } }
            }
            return false;
        }

        internal static bool AfterReverse(UIElement e)
        {
            Image o;
            if (e == null || !revSwapped.TryGetValue(e, out o)) return false;
            revSwapped.Remove(e);
            try { ((ImageUIElement)e).Image = o; } catch { }
            return true;
        }

        static void EnsureProps(Type t)
        {
            if (mgrProps.ContainsKey(t)) return;
            List<PropertyInfo> l = new List<PropertyInfo>(), il = new List<PropertyInfo>();
            foreach (PropertyInfo pi in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (pi.GetIndexParameters().Length > 0 || !pi.CanRead) continue;
                if (typeof(UltraComponentControlManagerBase).IsAssignableFrom(pi.PropertyType)) l.Add(pi);
                else if (typeof(ImageList).IsAssignableFrom(pi.PropertyType) || (pi.PropertyType == typeof(object) && pi.Name.IndexOf("ImageList", StringComparison.Ordinal) >= 0)) il.Add(pi);
            }
            mgrProps[t] = l.ToArray(); listProps[t] = il.ToArray();
        }

        public static void LightVisit(Control c, bool full)
        {
            if (!Reverse || c == null) return;
            try
            {
                UltraControlBase uc = c as UltraControlBase;
                if (uc != null) EnsureFilter(uc, IsToolbarHost(uc));
                if (!full) return;
                Type t = c.GetType();
                EnsureProps(t);
                foreach (PropertyInfo pi in mgrProps[t])
                {
                    object m = null;
                    try { m = pi.GetValue(c, null); } catch { }
                    if (m != null) EnsureFilter(m, IsToolbarHost(m));
                }
                Form f = c as Form;
                if (f != null && !formsScanned.Contains(f)) { formsScanned.Add(f); ScanFormFields(f); }
                if (uc == null)
                    foreach (PropertyInfo pi in listProps[t])
                    {
                        ImageList il = null;
                        try { il = pi.GetValue(c, null) as ImageList; } catch { }
                        if (il != null) RevList(il);
                    }
                TreeView tv = c as TreeView;
                if (tv != null && tv.StateImageList != null && RevList(tv.StateImageList) && tv.IsHandleCreated)
                {
                    try
                    {
                        MethodInfo upd = typeof(TreeView).GetMethod("UpdateNativeStateImageList", BindingFlags.Instance | BindingFlags.NonPublic);
                        if (upd != null) upd.Invoke(tv, null);
                        tv.Invalidate();
                    }
                    catch { }
                }
                ToolStrip ts = c as ToolStrip;
                if (ts != null) foreach (ToolStripItem it in ts.Items) RevItem(it, 0);
                if (t.FullName.StartsWith("Crownwood.Magic.Controls.TabControl", StringComparison.Ordinal)) RevPages(c);
            }
            catch (Exception ex) { Fail("passada (modo claro) " + c.GetType().Name, ex); }
        }

        static bool RevList(ImageList il)
        {
            int n = il.Images.Count;
            ListRec r;
            revLists.TryGetValue(il, out r);
            if (r != null && r.Count == n) return false;
            if (n == 0 || n > 2000) return false;
            Size sz = il.ImageSize;
            int start = (r != null && r.Changed) ? r.Count : 0;
            Image[] imgs = new Image[n], repl = new Image[n];
            bool any = false;
            for (int i = start; i < n; i++)
            {
                Image im = il.Images[i];
                imgs[i] = im;
                RevRec x = RevLookup(im);
                if (x != null) { repl[i] = RevRender(x, null, sz.Width, sz.Height, true); any = repl[i] != null || any; }
            }
            if (r == null) { r = new ListRec { Depth = il.ColorDepth }; revLists[il] = r; }
            if (!any) { r.Count = n; return false; }
            if (!r.Changed)
            {
                for (int i = 0; i < n; i++) { r.Imgs.Add(imgs[i]); r.Keys.Add(i < il.Images.Keys.Count ? il.Images.Keys[i] : ""); }
                r.Changed = true;
            }
            for (int i = start; i < n; i++) if (repl[i] != null) il.Images[i] = repl[i];
            r.Count = n;
            DarkEngine.LogOnce("revlist:" + sz.Width, "Icones (modo claro): lista de imagens " + sz.Width + "x" + sz.Height + " com icones Next guardados devolvida aos originais");
            return true;
        }

        static void RevItem(ToolStripItem it, int depth)
        {
            if (it == null || depth > 6 || it is ToolStripControlHost) return;
            try
            {
                Image img = it.Image;
                if (img != null && it.ImageIndex < 0 && string.IsNullOrEmpty(it.ImageKey))
                {
                    RevRec r = RevLookup(img);
                    if (r != null)
                    {
                        Bitmap v = RevRender(r, null, img.Width, img.Height, false);
                        if (v != null) { if (!revItems.ContainsKey(it)) revItems[it] = img; it.Image = v; }
                    }
                }
            }
            catch (Exception ex) { Fail("ToolStripItem (modo claro)", ex); }
            ToolStripDropDownItem dd = it as ToolStripDropDownItem;
            if (dd != null && dd.HasDropDownItems) foreach (ToolStripItem k in dd.DropDownItems) RevItem(k, depth + 1);
        }

        static void RevPages(Control c)
        {
            PropertyInfo pp = c.GetType().GetProperty("TabPages");
            IEnumerable pages = pp == null ? null : pp.GetValue(c, null) as IEnumerable;
            if (pages == null) return;
            foreach (object pg in pages)
            {
                if (pg == null || revPages.ContainsKey(pg)) continue;
                PropertyInfo pi = pg.GetType().GetProperty("Icon", typeof(Icon));
                if (pi != null && pi.CanWrite)
                {
                    Icon ic = pi.GetValue(pg, null) as Icon;
                    if (ic == null) continue;
                    RevRec r;
                    using (Bitmap b = ic.ToBitmap()) r = RevLookup(b);
                    Icon oi = r == null ? null : r.Orig as Icon;
                    if (oi == null) continue;
                    revPages[pg] = new KeyValuePair<PropertyInfo, object>(pi, ic);
                    pi.SetValue(pg, new Icon(oi, ic.Size), null);
                    continue;
                }
                pi = pg.GetType().GetProperty("Image", typeof(Image));
                if (pi != null && pi.CanWrite)
                {
                    Image im = pi.GetValue(pg, null) as Image;
                    RevRec r = RevLookup(im);
                    if (r == null) continue;
                    Bitmap v = RevRender(r, null, im.Width, im.Height, false);
                    if (v == null) continue;
                    revPages[pg] = new KeyValuePair<PropertyInfo, object>(pi, im);
                    pi.SetValue(pg, v, null);
                }
            }
        }

        public static void SetLight()
        {
            Active = false;
            int n = 0;
            lock (sync)
            {
                foreach (SeedRec s in seeds) { try { if (s.Orig != null) { s.Set.Invoke(s.Cache, new object[] { s.Key, s.Cul, s.Orig }); n++; } } catch { } }
                n += RestoreObjects();
            }
            BuildReverse();
            lock (sync) { formsScanned.Clear(); swapped.Clear(); revSwapped.Clear(); }
            Reverse = revMap != null && revMap.Count > 0 && errors <= 200;
            DarkEngine.Log("Icones (modo claro): " + n + " originais devolvidos (cache de recursos e objetos trocados)");
        }

        public static void SetDark()
        {
            Reverse = false;
            int n = 0;
            lock (sync)
            {
                foreach (KeyValuePair<ToolStripItem, Image> kv in revItems) { try { if (!kv.Key.IsDisposed) { kv.Key.Image = kv.Value; n++; } } catch { } }
                revItems.Clear();
                foreach (KeyValuePair<ImageList, ListRec> kv in revLists)
                {
                    if (!kv.Value.Changed) continue;
                    try
                    {
                        ImageList il = kv.Key;
                        for (int i = 0; i < kv.Value.Imgs.Count && i < il.Images.Count; i++) il.Images[i] = kv.Value.Imgs[i];
                        n++;
                    }
                    catch { }
                }
                revLists.Clear();
                foreach (KeyValuePair<object, KeyValuePair<PropertyInfo, object>> kv in revPages) { try { kv.Value.Key.SetValue(kv.Key, kv.Value.Value, null); n++; } catch { } }
                revPages.Clear();
                revSwapped.Clear();
                foreach (SeedRec s in seeds) { try { if (s.Repl != null) { s.Set.Invoke(s.Cache, new object[] { s.Key, s.Cul, s.Repl }); n++; } } catch { } }
                formsScanned.Clear();
            }
            Active = errors <= 200;
            DarkEngine.Log("Icones Next religados (" + n + " itens)");
        }

        static void PurgeReverse()
        {
            List<ToolStripItem> dead = new List<ToolStripItem>();
            foreach (ToolStripItem it in revItems.Keys) if (it.IsDisposed) dead.Add(it);
            foreach (ToolStripItem it in dead) revItems.Remove(it);
            revSwapped.Clear();
        }
    }
}
