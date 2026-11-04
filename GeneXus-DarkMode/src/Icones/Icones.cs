// GxDarkTheme - icones GeneXus Next (Mercury) em memoria + desenho de icones desabilitados.
// Fonte dos icones: @genexus/mercury 1.5.0 (Apache-2.0) - ver recurso GxIcons.NOTICE.txt.
// Nada e gravado em disco; nenhum binario do GeneXus/terceiros e alterado. Tudo reversivel (RestoreAll).
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Infragistics.Win;

namespace GxDarkTheme
{
    // Um icone em todos os estados/tamanhos. state: 0 normal, 1 hover, 2 desabilitado
    internal sealed class IconFamily
    {
        public string Name, Label;
        public bool IsNext, Mono;
        readonly Dictionary<int, Bitmap>[] frames = { new Dictionary<int, Bitmap>(), new Dictionary<int, Bitmap>(), new Dictionary<int, Bitmap>() };
        readonly Dictionary<long, Icon> icons = new Dictionary<long, Icon>();

        // ---- temas: cada quadro guarda a versao "mestra" (cores do GeneXus Next); o quadro entregue ao GeneXus
        // e recolorido no lugar (mesmo objeto Bitmap) quando o tema muda, entao nada precisa ser trocado de novo.
        static readonly List<IconFamily> todas = new List<IconFamily>();
        readonly Dictionary<Bitmap, Bitmap> masters = new Dictionary<Bitmap, Bitmap>(RefEq.I);
        public IconFamily() { lock (todas) todas.Add(this); }

        public Bitmap MasterOf(Bitmap b) { Bitmap m; return b != null && masters.TryGetValue(b, out m) ? m : b; }

        public void Put(int state, Bitmap b)
        {
            frames[state][b.Width] = b;
            try
            {
                if (!masters.ContainsKey(b))
                {
                    masters[b] = b.Clone(new Rectangle(0, 0, b.Width, b.Height), PixelFormat.Format32bppArgb);
                    if (!DarkEngine.TemaAtual.IsNext) Tingir(b, masters[b], DarkEngine.TemaAtual);
                }
            }
            catch { }
            IconSwap.Register(b, this, state);
        }

        static void Tingir(Bitmap alvo, Bitmap mestre, Tema t)
        {
            int w, h;
            int[] px = IconSwap.Pixels(mestre, out w, out h);
            if (px == null || alvo.Width != w || alvo.Height != h || alvo.PixelFormat != PixelFormat.Format32bppArgb) return;
            t.MapPixels(px);
            BitmapData d = alvo.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try { for (int y = 0; y < h; y++) System.Runtime.InteropServices.Marshal.Copy(px, y * w, new IntPtr(d.Scan0.ToInt64() + (long)y * d.Stride), w); }
            finally { alvo.UnlockBits(d); }
        }

        // recolore todos os quadros de todas as familias para o tema t (chamado com a IDE no modo claro, durante a troca de tema)
        public static int RetingirTodas(Tema t)
        {
            int n = 0;
            List<IconFamily> l;
            lock (todas) l = new List<IconFamily>(todas);
            foreach (IconFamily f in l)
            {
                foreach (KeyValuePair<Bitmap, Bitmap> kv in f.masters) { try { Tingir(kv.Key, kv.Value, t); n++; } catch { } }
                f.icons.Clear();   // os Icon (HICON) sao refeitos sob demanda com as cores novas
            }
            return n;
        }
        public bool Has(int state, int size) { return frames[state].ContainsKey(size); }
        public IEnumerable<Bitmap> All(int state) { return frames[state].Values; }

        public Bitmap Get(int state, int w, int h)
        {
            if (state < 0 || state > 2) state = 0;
            Dictionary<int, Bitmap> d = frames[state];
            if (d.Count == 0) { d = frames[0]; if (d.Count == 0) return null; }
            Bitmap b;
            int s = Math.Max(w, h);
            if (w == h && d.TryGetValue(s, out b)) return b;
            // tamanho diferente (ex.: 20 px com 125% de DPI): reduz do maior quadro disponivel
            Bitmap src = null;
            foreach (KeyValuePair<int, Bitmap> kv in d)
            {
                if (src == null) { src = kv.Value; continue; }
                bool okNew = kv.Key >= s, okOld = src.Width >= s;
                if ((okNew && (!okOld || kv.Key < src.Width)) || (!okNew && !okOld && kv.Key > src.Width)) src = kv.Value;
            }
            Bitmap r = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(r))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.PixelOffsetMode = PixelOffsetMode.HighQuality; g.CompositingQuality = CompositingQuality.HighQuality;
                g.DrawImage(MasterOf(src), new Rectangle(0, 0, w, h));
            }
            if (w == h) Put(state, r); else IconSwap.Register(r, this, state);
            return r;
        }

        public Icon GetIcon(Size size)
        {
            long k = ((long)size.Width << 16) | (uint)size.Height;
            Icon ic;
            if (icons.TryGetValue(k, out ic)) return ic;
            List<Bitmap> fr = new List<Bitmap>();
            foreach (int s in new int[] { 16, 24, 32 }) fr.Add(Get(0, s, s));
            if (size.Width == size.Height && size.Width != 16 && size.Width != 24 && size.Width != 32 && size.Width > 0 && size.Width <= 128) fr.Add(Get(0, size.Width, size.Width));
            ic = new Icon(new MemoryStream(IconSwap.Ico(fr)), size);
            icons[k] = ic;
            return ic;
        }
    }

    public sealed class IconHit { internal IconFamily F; public int State; }

    // Filtro de desenho Infragistics: troca o icone na hora de desenhar (sem alterar o objeto do GeneXus)
    // e desenha a versao desabilitada propria. Encadeia o filtro que ja existia no controle.
    public sealed class IconDrawFilter : IUIElementDrawFilter
    {
        public readonly IUIElementDrawFilter Inner;
        readonly bool toolbar;
        public IconDrawFilter(IUIElementDrawFilter inner, bool toolbar) { Inner = inner; this.toolbar = toolbar; }

        public DrawPhase GetPhasesToFilter(ref UIElementDrawParams p)
        {
            DrawPhase r = DrawPhase.None;
            if (Inner != null) { try { r = Inner.GetPhasesToFilter(ref p); } catch { } }
            if (IconSwap.Active || IconSwap.Reverse)
            {
                UIElement e = p.Element;
                if (e is ImageUIElement) r |= DrawPhase.BeforeDrawImage | DrawPhase.AfterDrawImage;
                else r |= DrawPhase.BeforeDrawImage;
            }
            if (FlatButtons.On) { try { r |= FlatButtons.Phases(p.Element); } catch { } }
            if (LineGuard.On) { try { r |= LineGuard.Phases(p.Element); } catch { } }
            return r;
        }

        public bool DrawElement(DrawPhase phase, ref UIElementDrawParams p)
        {
            if (Inner != null)
            {
                DrawPhase ip = DrawPhase.None;
                try { ip = Inner.GetPhasesToFilter(ref p); } catch { }
                if ((ip & phase) != 0 && Inner.DrawElement(phase, ref p)) return true;
            }
            if ((phase == DrawPhase.BeforeDrawBackColor || phase == DrawPhase.BeforeDrawBorders) && FlatButtons.On)
            {
                try { if (FlatButtons.Draw(phase, ref p)) return true; } catch (Exception ex) { FlatButtons.Fail(ex); }
            }
            if (LineGuard.On)
            {
                try { if (LineGuard.Draw(phase, ref p)) return true; } catch (Exception ex) { LineGuard.Fail(ex); }
            }
            if (IconSwap.Reverse)
            {
                try
                {
                    if (phase == DrawPhase.BeforeDrawImage) return IconSwap.BeforeReverse(ref p, toolbar);
                    if (phase == DrawPhase.AfterDrawImage) IconSwap.AfterReverse(p.Element);
                }
                catch (Exception ex) { IconSwap.Fail("desenho (claro)", ex); }
                return false;
            }
            if (!IconSwap.Active) return false;
            try
            {
                if (phase == DrawPhase.BeforeDrawImage) return IconSwap.BeforeImage(ref p, toolbar);
                if (phase == DrawPhase.AfterDrawImage) IconSwap.AfterImage(ref p);
            }
            catch (Exception ex) { IconSwap.Fail("desenho", ex); }
            return false;
        }
    }

    // ---------- Linhas claras e seta de personalizacao das barras (Infragistics) ----------
    // Rede de seguranca no filtro de desenho (so em memoria): borda cinza clara (sublinhados, molduras, separadores) -> #333D47;
    // elemento fino (divisor) com fundo cinza claro -> #2A3442; seta "personalizar/mais botoes" (magenta no bitmap do
    // Infragistics) desenhada #8A909B (hover #DCE2EB) sem o realce ciano/branco. Cada tipo corrigido vai uma vez ao log.
    public static class LineGuard
    {
        public static volatile bool On = true;
        static int errors;
        static readonly HashSet<string> logged = new HashSet<string>();
        static Color Soft { get { return DarkEngine.T(51, 61, 71); } }
        static Color SplitBg { get { return DarkEngine.T(42, 52, 66); } }
        static Color ArrowN { get { return DarkEngine.T(138, 144, 155); } }
        static Color ArrowH { get { return DarkEngine.T(220, 226, 235); } }
        static readonly Dictionary<Image, Bitmap[]> arrows = new Dictionary<Image, Bitmap[]>();
        internal static void LimparCache() { lock (arrows) arrows.Clear(); }

        internal static bool IsQc(UIElement e)
        {
            return e is ImageUIElementBase && e.GetType().Name.StartsWith("QuickCustomize", StringComparison.Ordinal);
        }

        internal static DrawPhase Phases(UIElement e)
        {
            DrawPhase r = DrawPhase.BeforeDrawBorders | DrawPhase.BeforeDrawBackColor;
            if (IsQc(e)) r |= DrawPhase.BeforeDrawImage;
            return r;
        }

        static bool LightGrey(Color c)
        {
            if (c.IsEmpty || c.A < 40) return false;
            int sat = Math.Max(c.R, Math.Max(c.G, c.B)) - Math.Min(c.R, Math.Min(c.G, c.B));
            return sat < 40 && (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) > 110;
        }

        static void Note(string what, UIElement e, Color c)
        {
            string k = what + e.GetType().FullName;
            lock (logged) { if (logged.Count > 300 || !logged.Add(k)) return; }
            DarkEngine.Log("LINHA CLARA corrigida (" + what + "): " + e.GetType().FullName + " #" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2"));
        }

        internal static bool Draw(DrawPhase phase, ref UIElementDrawParams p)
        {
            UIElement e = p.Element;
            if (e == null) return false;
            if (phase == DrawPhase.BeforeDrawBorders)
            {
                if (DarkEngine.IsLight(p.AppearanceData.BackColor) && p.AppearanceData.BackColor.A == 255) return false; // area clara de proposito
                Color c = p.AppearanceData.BorderColor;
                if (LightGrey(c)) { Note("borda", e, c); p.AppearanceData.BorderColor = Soft; }
                c = p.AppearanceData.BorderColor2;
                if (LightGrey(c)) { Note("borda2", e, c); p.AppearanceData.BorderColor2 = Soft; }
                c = p.AppearanceData.BorderColor3DBase;
                if (LightGrey(c)) { Note("borda3D", e, c); p.AppearanceData.BorderColor3DBase = Soft; }
                return false;
            }
            if (phase == DrawPhase.BeforeDrawBackColor)
            {
                Rectangle r = e.Rect;
                int thin = Math.Min(r.Width, r.Height), big = Math.Max(r.Width, r.Height);
                if (thin <= 0 || thin > 6 || big < 12) return false;
                Color c = p.AppearanceData.BackColor;
                if (!LightGrey(c) || c.A < 255) return false;
                Note("divisor", e, c);
                p.AppearanceData.BackColor = SplitBg;
                p.AppearanceData.BackColor2 = SplitBg;
                p.AppearanceData.BackGradientStyle = GradientStyle.None;
                return false;
            }
            if (phase == DrawPhase.BeforeDrawImage && IsQc(e)) return DrawArrow(ref p);
            return false;
        }

        static bool DrawArrow(ref UIElementDrawParams p)
        {
            ImageUIElementBase ie = (ImageUIElementBase)p.Element;
            Image img = ie.Image;
            if (img == null) return false;
            Bitmap[] v;
            lock (arrows)
            {
                if (!arrows.TryGetValue(img, out v))
                {
                    if (arrows.Count > 64) arrows.Clear();
                    v = new Bitmap[] { Remap(img, ArrowN), Remap(img, ArrowH) };
                    arrows[img] = v;
                }
            }
            bool hot = false;
            try { hot = IconSwap.Hot(ie) || (ie.Parent != null && IconSwap.Hot(ie.Parent)); } catch { }
            Bitmap b = v[hot ? 1 : 0];
            Rectangle rr = ie.RectInsideBorders;
            Rectangle tr = new Rectangle(rr.X, rr.Y, b.Width, b.Height);
            if (b.Width < rr.Width) tr.X += (rr.Width - b.Width) / 2;
            if (b.Height < rr.Height) tr.Y += (rr.Height - b.Height) / 2;
            Region old = p.Graphics.Clip;
            try { p.Graphics.SetClip(rr, CombineMode.Intersect); p.Graphics.DrawImage(b, tr.X, tr.Y, b.Width, b.Height); }
            finally { p.Graphics.Clip = old; }
            Note("seta personalizar", ie, hot ? ArrowH : ArrowN);
            return true;
        }

        // bitmap da seta: magenta = desenho (vira a cor pedida), ciano = realce branco (some), demais pixels mantidos
        static Bitmap Remap(Image src, Color fg)
        {
            Bitmap b = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(b)) g.DrawImage(src, 0, 0, src.Width, src.Height);
            for (int y = 0; y < b.Height; y++)
                for (int x = 0; x < b.Width; x++)
                {
                    Color c = b.GetPixel(x, y);
                    if (c.A == 0) continue;
                    if (c.R > 200 && c.G < 60 && c.B > 200) b.SetPixel(x, y, Color.FromArgb(c.A, fg));
                    else if (c.R < 60 && c.G > 200 && c.B > 200) b.SetPixel(x, y, Color.Transparent);
                    else if (c.R > 200 && c.G > 200 && c.B > 200) b.SetPixel(x, y, Color.Transparent);
                }
            return b;
        }

        internal static void Fail(Exception ex)
        {
            if (++errors > 20) { On = false; DarkEngine.Log("Linhas claras (filtro) desligado: " + ex.GetType().Name + ": " + ex.Message); }
        }
    }

    // ---------- Hover/pressionado/marcado PLANO (estilo GeneXus Next) nos botoes Infragistics ----------
    // O estilo Office2010Button do Infragistics ignora a cor do ISL no hover: desenha brilho (glow) amarelado e bordas
    // arredondadas claras da tabela Office2010ColorTable. Aqui, no filtro de desenho (so em memoria), os botoes de
    // barras/menus (UltraToolbarsManager e Artech.FrameworkDE.ToolBarManager), split/drop-down, botoes de legenda das
    // janelas, fechar das abas e UltraButton dos dialogos sao pintados planos: hover #344860 + borda #4E6888,
    // pressionado #1F4E79, marcado #2C3C50 + borda #2C8DCE. Estado normal: desenho original (nada muda).
    public static class FlatButtons
    {
        public static volatile bool On = true;
        static int errors;
        static readonly Dictionary<Type, int> kinds = new Dictionary<Type, int>();
        static readonly Dictionary<Type, PropertyInfo> bsProp = new Dictionary<Type, PropertyInfo>();
        static PropertyInfo pDrawAsActive, pIsOpen;
        // 0 = nao trata; 1 = botao de ferramenta; 2 = conjunto split (botao + seta); 3 = seta do split; 4 = outro botao (legenda, fechar aba, UltraButton)
        internal static int Kind(UIElement e)
        {
            if (e == null) return 0;
            Type t = e.GetType();
            int k;
            lock (kinds)
            {
                if (kinds.TryGetValue(t, out k)) return k;
                k = 0;
                for (Type x = t; x != null && x != typeof(UIElement); x = x.BaseType)
                {
                    string n = x.FullName ?? "";
                    if (n == "Infragistics.Win.UltraWinToolbars.ButtonToolUIElement") { k = 1; break; }
                    if (n == "Infragistics.Win.UltraWinToolbars.PopupToolUIElement") { k = 2; break; }
                    if (n == "Infragistics.Win.UltraWinToolbars.PopupToolDropDownButtonUIElement") { k = 3; break; }
                }
                if (k == 0 && e is ButtonUIElementBase)
                {
                    string ns = t.Namespace ?? "";
                    if (ns.StartsWith("Infragistics.Win.UltraWinToolbars", StringComparison.Ordinal) || ns.StartsWith("Infragistics.Win.UltraWinDock", StringComparison.Ordinal) ||
                        ns.StartsWith("Infragistics.Win.UltraWinTabbedMdi", StringComparison.Ordinal) || ns.StartsWith("Infragistics.Win.UltraWinTabs", StringComparison.Ordinal) ||
                        ns.StartsWith("Infragistics.Win.Misc", StringComparison.Ordinal)) k = 4;
                    else if (t.FullName == "Infragistics.Win.ButtonUIElement" || t.FullName == "Infragistics.Win.ImageAndTextButtonUIElement") k = 5;   // depende do controle (UltraButton)
                }
                kinds[t] = k;
            }
            return k;
        }

        internal static DrawPhase Phases(UIElement e)
        {
            int k = Kind(e);
            if (k == 0) return DrawPhase.None;
            if (k == 5 && !IsUltraButtonControl(e)) return DrawPhase.None;
            return DrawPhase.BeforeDrawBackColor | DrawPhase.BeforeDrawBorders;
        }

        static bool IsUltraButtonControl(UIElement e)
        {
            Control c = null;
            try { c = e.Control; } catch { }
            if (c == null) return false;
            for (Type t = c.GetType(); t != null; t = t.BaseType)
            {
                string n = t.FullName;
                if (n == "Infragistics.Win.Misc.UltraButtonBase" || n == "Infragistics.Win.Misc.UltraButton" || n == "Infragistics.Win.Misc.UltraDropDownButton") return true;
            }
            return false;
        }

        static UIElementButtonState State(UIElement e)
        {
            ButtonUIElementBase bb = e as ButtonUIElementBase;
            if (bb != null) return bb.ButtonState;
            Type t = e.GetType();
            PropertyInfo pi;
            lock (bsProp)
            {
                if (!bsProp.TryGetValue(t, out pi))
                {
                    for (Type x = t; x != null && pi == null; x = x.BaseType)
                        pi = x.GetProperty("ButtonState", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                    bsProp[t] = pi;
                }
            }
            if (pi == null) return 0;
            try { return (UIElementButtonState)pi.GetValue(e, null); } catch { return 0; }
        }

        static object ToolOf(UIElement e)
        {
            try
            {
                for (UIElement x = e; x != null; x = x.Parent)
                {
                    object c = x.GetContext();
                    if (c != null && IsToolBase(c.GetType())) return c;
                }
            }
            catch { }
            return null;
        }

        static bool IsToolBase(Type t)
        {
            for (; t != null; t = t.BaseType) if (t.FullName == "Infragistics.Win.UltraWinToolbars.ToolBase") return true;
            return false;
        }

        static bool ToolFlag(object tool, string name, ref PropertyInfo cache)
        {
            if (tool == null) return false;
            try
            {
                if (cache == null)
                    for (Type x = tool.GetType(); x != null && cache == null; x = x.BaseType)
                        if (x.FullName == "Infragistics.Win.UltraWinToolbars.ToolBase" || x.FullName == "Infragistics.Win.UltraWinToolbars.PopupToolBase")
                            cache = x.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (cache == null || !cache.DeclaringType.IsInstanceOfType(tool)) return false;
                return (bool)cache.GetValue(tool, null);
            }
            catch { return false; }
        }

        // 0 nada (desenho original), 1 hover, 2 pressionado, 3 marcado, 4 marcado+hover
        internal static int VisualState(UIElement e, int kind)
        {
            UIElementButtonState bs = State(e);
            bool over = (bs & UIElementButtonState.MouseOver) != 0, down = (bs & UIElementButtonState.MouseDown) != 0, chk = (bs & UIElementButtonState.StateButtonDown) != 0;
            bool enabled = true;
            try { enabled = e.Enabled; } catch { }
            if (!enabled) return 0;
            if (kind >= 1 && kind <= 3)
            {
                object tool = ToolOf(e);
                bool active = ToolFlag(tool, "DrawAsActive", ref pDrawAsActive);
                bool open = ToolFlag(tool, "IsOpen", ref pIsOpen);
                if (open) return 2;
                if (kind == 2) { if (chk) return active ? 4 : 3; return active ? 1 : 0; }
                if (down && (over || active)) return 2;
                if (chk) return (over || active) ? 4 : 3;
                return (over || active) ? 1 : 0;
            }
            if (down && over) return 2;
            if (chk) return over ? 4 : 3;
            return over ? 1 : 0;
        }

        internal static bool Draw(DrawPhase phase, ref UIElementDrawParams p)
        {
            UIElement e = p.Element;
            int kind = Kind(e);
            if (kind == 0 || (kind == 5 && !IsUltraButtonControl(e))) return false;
            bool inSplit = (kind == 1 || kind == 3) && e.Parent != null && Kind(e.Parent) == 2;
            int vs = VisualState(e, kind);
            if (inSplit)
            {
                // partes do split: o conjunto (pai) ja pintou hover/borda; a parte so marca pressionado e a divisoria
                int pvs = VisualState(e.Parent, 2);
                if (vs == 0 && pvs == 0) return false;
                if (phase == DrawPhase.BeforeDrawBackColor)
                {
                    Rectangle r = e.Rect;
                    if (vs == 2) Paint(p.Graphics, r, DarkEngine.MenuHot, Color.Empty);
                    else if (vs == 3 || vs == 4) Paint(p.Graphics, r, DarkEngine.Sel, Color.Empty);
                    if (kind == 3)
                        using (Pen pen = new Pen(DarkEngine.HotBorder)) p.Graphics.DrawLine(pen, r.Left, r.Top + 2, r.Left, r.Bottom - 3);
                }
                return true;
            }
            if (vs == 0) return false;
            if (phase == DrawPhase.BeforeDrawBackColor)
            {
                Color bg, bd;
                switch (vs)
                {
                    case 2: bg = DarkEngine.MenuHot; bd = DarkEngine.HotBorder; break;
                    case 3: bg = DarkEngine.Sel; bd = DarkEngine.Accent; break;
                    case 4: bg = DarkEngine.HotBg; bd = DarkEngine.Accent; break;
                    default: bg = DarkEngine.HotBg; bd = DarkEngine.HotBorder; break;
                }
                Paint(p.Graphics, e.Rect, bg, bd);
            }
            return true;   // sem brilho/gradiente nem borda clara do Office2010
        }

        // pintura em pixels inteiros (sem anti-alias): borda de 1px nitida em qualquer DPI/PixelOffsetMode,
        // cantos levemente arredondados (pixel do canto omitido + pixel interno em meio-tom)
        static void Paint(Graphics g, Rectangle r, Color bg, Color bd)
        {
            if (r.Width < 4 || r.Height < 4) return;
            SmoothingMode sm = g.SmoothingMode;
            PixelOffsetMode pom = g.PixelOffsetMode;
            try
            {
                g.SmoothingMode = SmoothingMode.None;
                g.PixelOffsetMode = PixelOffsetMode.None;
                int x = r.X, y = r.Y, w = r.Width, h = r.Height;
                using (SolidBrush b = new SolidBrush(bg))
                {
                    g.FillRectangle(b, x + 1, y + 1, w - 2, h - 2);
                    if (bd.IsEmpty) { g.FillRectangle(b, x + 2, y, w - 4, 1); g.FillRectangle(b, x + 2, y + h - 1, w - 4, 1); g.FillRectangle(b, x, y + 2, 1, h - 4); g.FillRectangle(b, x + w - 1, y + 2, 1, h - 4); }
                }
                if (!bd.IsEmpty)
                {
                    using (SolidBrush b = new SolidBrush(bd))
                    {
                        g.FillRectangle(b, x + 2, y, w - 4, 1); g.FillRectangle(b, x + 2, y + h - 1, w - 4, 1);
                        g.FillRectangle(b, x, y + 2, 1, h - 4); g.FillRectangle(b, x + w - 1, y + 2, 1, h - 4);
                    }
                    Color mid = Color.FromArgb((bd.R + bg.R) / 2, (bd.G + bg.G) / 2, (bd.B + bg.B) / 2);
                    using (SolidBrush b = new SolidBrush(mid))
                    {
                        g.FillRectangle(b, x + 1, y + 1, 1, 1); g.FillRectangle(b, x + w - 2, y + 1, 1, 1);
                        g.FillRectangle(b, x + 1, y + h - 2, 1, 1); g.FillRectangle(b, x + w - 2, y + h - 2, 1, 1);
                    }
                }
            }
            finally { g.SmoothingMode = sm; g.PixelOffsetMode = pom; }
        }

        internal static void Fail(Exception ex)
        {
            DarkEngine.LogOnce("flatbtn:" + ex.GetType().Name, "Hover plano: erro (mantido o desenho original; registrado uma vez): " + ex.GetType().Name + ": " + ex.Message);
            if (++errors > 100) { On = false; DarkEngine.Log("Hover plano: muitos erros, desligado nesta sessao."); }
        }
    }

    // Renderizador escuro das ToolStrips: icone desabilitado e hover no estilo Next
    public class DarkTsRenderer : ToolStripProfessionalRenderer
    {
        public DarkTsRenderer() : base(new DarkColorTable()) { RoundedEdges = false; }
        protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e)
        {
            try
            {
                if (IconSwap.Active && e.Image != null && e.Item != null)
                {
                    IconHit h = IconSwap.Lookup(e.Image);
                    if (h != null)
                    {
                        int st = !e.Item.Enabled ? 2 : (e.Item.Selected || e.Item.Pressed) ? 1 : 0;
                        Rectangle r = e.ImageRectangle;
                        Bitmap v = h.F.Get(st, r.Width > 0 ? r.Width : e.Image.Width, r.Height > 0 ? r.Height : e.Image.Height);
                        if (v != null) { e.Graphics.DrawImage(v, r); return; }
                    }
                }
                if (e.Image != null && e.Item != null)
                {
                    Bitmap mv = IconSwap.MonoLift(e.Image, !e.Item.Enabled);
                    if (mv != null) { Rectangle r = e.ImageRectangle; e.Graphics.DrawImage(mv, r.Width > 0 ? r : new Rectangle(r.Location, mv.Size)); return; }
                }
            }
            catch (Exception ex) { IconSwap.Fail("ToolStrip", ex); }
            base.OnRenderItemImage(e);
        }
    }

    public static partial class IconSwap
    {
        public static volatile bool Active;
        static readonly object sync = new object();
        static readonly ConditionalWeakTable<Image, IconHit> known = new ConditionalWeakTable<Image, IconHit>();
        static readonly IconHit NoHit = new IconHit();
        static readonly List<IconFamily> nextFams = new List<IconFamily>();
        static readonly Dictionary<long, IconFamily> exact = new Dictionary<long, IconFamily>();
        static readonly Dictionary<int, List<KeyValuePair<short[], IconFamily>>> refs = new Dictionary<int, List<KeyValuePair<short[], IconFamily>>>();
        static readonly Dictionary<long, IconFamily> fuzzyCache = new Dictionary<long, IconFamily>();
        static readonly List<string[]> pending = new List<string[]>();   // linhas do mapa cujo assembly ainda nao carregou
        static readonly Dictionary<string, IconFamily> rowFam = new Dictionary<string, IconFamily>();
        static int errors, seeded, refCount, matched;
        public static int Swaps { get { return matched; } }
        public static int Seeded { get { return seeded; } }

        // ---- registros para restaurar ----
        sealed class SeedRec { public object Cache; public MethodInfo Set; public string Key; public CultureInfo Cul; public object Orig; public object Repl; public IconFamily Fam; }
        static readonly List<SeedRec> seeds = new List<SeedRec>();
        static readonly Dictionary<object, IUIElementDrawFilter> filters = new Dictionary<object, IUIElementDrawFilter>();
        sealed class ListRec { public ColorDepth Depth; public List<Image> Imgs = new List<Image>(); public List<string> Keys = new List<string>(); public int Count; public bool Changed; public IntPtr Dup; }
        static readonly Dictionary<ImageList, ListRec> lists = new Dictionary<ImageList, ListRec>();
        static readonly Dictionary<ToolStripItem, Image> items = new Dictionary<ToolStripItem, Image>();
        static readonly Dictionary<object, KeyValuePair<PropertyInfo, object>> pageIcons = new Dictionary<object, KeyValuePair<PropertyInfo, object>>();

        internal static void Fail(string where, Exception ex)
        {
            DarkEngine.LogOnce("icone:" + where + ":" + ex.GetType().Name, "Icones Next: erro em " + where + " (mantido o icone original; registrado uma vez): " + ex.GetType().Name + ": " + ex.Message);
            if (++errors > 200) { Active = false; Reverse = false; DarkEngine.Log("Icones Next: muitos erros, troca de icones desligada nesta sessao (icones originais)."); }
        }

        // ================= inicializacao =================
        public static void Init()
        {
            lock (sync)
            {
                Assembly me = typeof(IconSwap).Assembly;
                int[] sizes = { 16, 24, 32 };
                string[] states = { "enabled", "hover", "disabled" };
                List<string> lines = new List<string>();
                using (Stream s = me.GetManifestResourceStream("GxIcons.icons.txt"))
                using (StreamReader r = new StreamReader(s)) { string l; while ((l = r.ReadLine()) != null) if (l.Length > 0 && l[0] != '#') lines.Add(l); }
                foreach (string l in lines)
                {
                    string[] p = l.Split('|');
                    if (p[0] == "I") nextFams.Add(new IconFamily { Name = p[2], IsNext = true, Mono = p[3] == "mono", Label = p[2] });
                }
                for (int st = 0; st < 3; st++)
                    foreach (int sz in sizes)
                        using (Stream s = me.GetManifestResourceStream("GxIcons.next_" + states[st] + "_" + sz + ".png"))
                        using (Bitmap sheet = new Bitmap(s))
                            for (int i = 0; i < nextFams.Count; i++)
                            {
                                Bitmap b = sheet.Clone(new Rectangle(i * sz, 0, sz, sz), PixelFormat.Format32bppArgb);
                                nextFams[i].Put(st, b);
                            }
                foreach (IconFamily f in nextFams) for (int st = 0; st < 2; st++) foreach (Bitmap b in f.All(st)) AddRef(f.MasterOf(b), f);
                foreach (string l in lines) { string[] p = l.Split('|'); if (p[0] == "M") pending.Add(p); }
                Active = true;
                DarkEngine.Log("Icones Next: " + nextFams.Count + " icones Mercury carregados (16/24/32 px), " + pending.Count + " entradas no mapa. Fonte: @genexus/mercury 1.5.0 (Apache-2.0).");
                try
                {
                    DarkEngine.Log("CachingResourceManager: DefaultImageSize=" + Artech.Common.Localization.CachingResourceManager.DefaultImageSize +
                        " resizeFunction=" + (Artech.Common.Localization.CachingResourceManager.resizeFunction != null) +
                        " invalidateFunction=" + (Artech.Common.Localization.CachingResourceManager.invalidateFunction != null));
                }
                catch (Exception ex) { DarkEngine.Log("CachingResourceManager indisponivel: " + ex.Message); }
            }
            Tick();
        }

        // processa linhas do mapa cujos assemblies ja estao carregados (chamado nas passadas)
        public static void Tick()
        {
            if (!Active) return;
            try { FlushUnmatched(); } catch { }
            lock (sync)
            {
                if (!wwpDone) { try { WwpRequired(); } catch (Exception ex) { wwpDone = true; Fail("WorkWithPlus obrigatorio", ex); } }
                if (pending.Count == 0) return;
                Dictionary<string, Assembly> loaded = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);
                foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies()) { try { string n = a.GetName().Name; if (n.StartsWith("Artech.", StringComparison.Ordinal) && !loaded.ContainsKey(n)) loaded[n] = a; } catch { } }
                int before = seeded, done = 0;
                for (int i = pending.Count - 1; i >= 0; i--)
                {
                    string[] p = pending[i];
                    Assembly asm;
                    if (!loaded.TryGetValue(p[1], out asm)) continue;
                    pending.RemoveAt(i); done++;
                    try { MapRow(asm, p); } catch (Exception ex) { Fail("mapa " + p[3], ex); }
                }
                if (done > 0) { try { PrepareDpiSizes(); } catch (Exception ex) { Fail("dpi", ex); } }
                if (done > 0) DarkEngine.Log("Icones Next: " + done + " entradas processadas (" + (seeded - before) + " trocadas direto no cache de recursos do GeneXus), referencias=" + refCount + ", pendentes=" + pending.Count);
            }
        }

        static readonly Dictionary<string, ResourceManager> rms = new Dictionary<string, ResourceManager>();
        static readonly Dictionary<string, ResourceManager> freshRms = new Dictionary<string, ResourceManager>();

        static void MapRow(Assembly asm, string[] p)
        {
            string baseName = p[2], key = p[3];
            int idx = int.Parse(p[4], CultureInfo.InvariantCulture);
            string rk = p[1] + "|" + baseName;
            ResourceManager fresh;
            if (!freshRms.TryGetValue(rk, out fresh)) { fresh = new ResourceManager(baseName, asm); freshRms[rk] = fresh; }
            object orig = null;
            try { orig = fresh.GetObject(key); } catch { }
            if (orig == null) { DarkEngine.LogOnce("icmiss:" + key, "Icones Next: recurso nao encontrado " + baseName + "/" + key); return; }
            string fk = rk + "|" + key;
            IconFamily fam;
            if (!rowFam.TryGetValue(fk, out fam))
            {
                if (idx >= 0) fam = nextFams[idx];
                else fam = DarkFriendlyFamily(orig, p[6]);
                rowFam[fk] = fam;
            }
            if (fam == null) return;
            if (!famByKey.ContainsKey(key)) famByKey[key] = fam;
            origs.Add(new KeyValuePair<object, IconFamily>(orig, fam));
            foreach (int bs in builtSizes) AddScaled(orig, fam, bs);
            // referencias (impressao digital) do original em varios tamanhos
            Icon oi = orig as Icon;
            if (oi != null)
            {
                foreach (int s in new int[] { 16, 20, 24, 32 })
                {
                    try { using (Icon f = new Icon(oi, s, s)) using (Bitmap b = f.ToBitmap()) AddRef(b, fam); } catch { }
                }
                try { using (Bitmap b = oi.ToBitmap()) AddRef(b, fam); } catch { }
                try { using (Icon f = new Icon(oi, 16, 16)) { AddDegraded(f, fam); using (Bitmap b = f.ToBitmap()) AddDegraded(b, fam); } } catch { }
            }
            else if (orig is Image) { AddRef((Image)orig, fam); if (((Image)orig).Width == 16) AddDegraded(orig, fam); }
            // gancho antecipado: troca a entrada no cache do CachingResourceManager do GeneXus
            Seed(asm, baseName, key, fam);
        }

        static ResourceManager FindRm(Assembly asm, string baseName)
        {
            string k = asm.FullName + "|" + baseName;
            ResourceManager rm;
            if (rms.TryGetValue(k, out rm)) return rm;
            rm = null;
            Type[] types;
            try { types = asm.GetTypes(); } catch (ReflectionTypeLoadException ex) { types = ex.Types; }
            foreach (Type t in types)
            {
                if (t == null || !t.IsClass) continue;
                PropertyInfo pi = t.GetProperty("ResourceManager", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (pi == null || !typeof(ResourceManager).IsAssignableFrom(pi.PropertyType)) continue;
                try
                {
                    ResourceManager r = pi.GetValue(null, null) as ResourceManager;
                    if (r != null && r.BaseName == baseName) { rm = r; break; }
                }
                catch { }
            }
            rms[k] = rm;
            return rm;
        }

        static FieldInfo fCache;
        static void Seed(Assembly asm, string baseName, string key, IconFamily fam)
        {
            Artech.Common.Localization.CachingResourceManager crm = FindRm(asm, baseName) as Artech.Common.Localization.CachingResourceManager;
            if (crm == null) return;
            if (fCache == null) fCache = typeof(Artech.Common.Localization.CachingResourceManager).GetField("m_ObjectCache", BindingFlags.Instance | BindingFlags.NonPublic);
            if (fCache == null) return;
            object cache = fCache.GetValue(crm);
            MethodInfo set = cache.GetType().GetMethod("Set");
            CultureInfo cul = Artech.Common.Localization.CachingResourceManager.CurrentUICulture ?? CultureInfo.CurrentUICulture;
            object cur = crm.GetObject(key, cul);   // carrega (e redimensiona, se o GeneXus quiser) o original
            object repl = null;
            Icon ci = cur as Icon;
            if (ci != null)
            {
                Icon ni = fam.GetIcon(ci.Size);
                Func<Icon, bool> inv = Artech.Common.Localization.CachingResourceManager.invalidateFunction;
                if (inv != null) { try { if (inv(ni)) { DarkEngine.LogOnce("icinv", "Icones Next: invalidateFunction do GeneXus rejeita icones novos; usando so a troca na hora do desenho."); return; } } catch { } }
                repl = ni;
                try { using (Bitmap b = ci.ToBitmap()) { AddRef(b, fam); int t = DpiScale(b.Width); if (t != b.Width) using (Bitmap r = GxResize(b, t)) AddRef(r, fam); } } catch { }
            }
            else
            {
                Bitmap cb = cur as Bitmap;
                if (cb != null) { AddRef(cb, fam); repl = fam.Get(0, cb.Width, cb.Height); }
            }
            if (repl == null) return;
            set.Invoke(cache, new object[] { key, cul, repl });
            seeds.Add(new SeedRec { Cache = cache, Set = set, Key = key, Cul = cul, Orig = cur, Repl = repl, Fam = fam });
            seeded++;
        }

        // ================= impressao digital =================
        internal static void Register(Image b, IconFamily f, int state)
        {
            try { IconHit old; if (!known.TryGetValue(b, out old)) known.Add(b, new IconHit { F = f, State = state }); } catch { }
        }

        internal static int[] Pixels(Image img, out int w, out int h)
        {
            w = img.Width; h = img.Height;
            if (w != h || w < 10 || w > 64) return null;
            Bitmap b = img as Bitmap, tmp = null;
            try
            {
                if (b == null || b.PixelFormat != PixelFormat.Format32bppArgb)
                {
                    tmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
                    using (Graphics g = Graphics.FromImage(tmp))
                    {
                        g.Clear(Color.Transparent); g.InterpolationMode = InterpolationMode.NearestNeighbor; g.PixelOffsetMode = PixelOffsetMode.Half;
                        g.DrawImage(img, new Rectangle(0, 0, w, h));
                    }
                    b = tmp;
                }
                BitmapData d = b.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                int[] px = new int[w * h];
                try { for (int y = 0; y < h; y++) Marshal.Copy(new IntPtr(d.Scan0.ToInt64() + (long)y * d.Stride), px, y * w, w); }
                finally { b.UnlockBits(d); }
                return px;
            }
            finally { if (tmp != null) tmp.Dispose(); }
        }

        static short[] Sig(int[] px)
        {
            short[] s = new short[px.Length];
            for (int i = 0; i < px.Length; i++)
            {
                int c = px[i], a = (c >> 24) & 255;
                if (a < 100) continue;
                int r = (c >> 16) & 255, g = (c >> 8) & 255, b = c & 255;
                s[i] = (short)(1 + (((r >> 5) << 6) | ((g >> 5) << 3) | (b >> 5)));
            }
            return s;
        }

        static long Hash(short[] s)
        {
            ulong h = 1469598103934665603UL ^ (ulong)s.Length;
            foreach (short v in s) { h ^= (ushort)v; h *= 1099511628211UL; }
            return (long)h;
        }

        static void AddRef(Image img, IconFamily f)
        {
            int w, h;
            int[] px = Pixels(img, out w, out h);
            if (px == null) return;
            short[] s = Sig(px);
            int opaque = 0; foreach (short v in s) if (v != 0) opaque++;
            if (opaque < 6) return;
            long k = Hash(s);
            if (!exact.ContainsKey(k)) exact[k] = f;
            List<KeyValuePair<short[], IconFamily>> l;
            if (!refs.TryGetValue(w, out l)) { l = new List<KeyValuePair<short[], IconFamily>>(); refs[w] = l; }
            l.Add(new KeyValuePair<short[], IconFamily>(s, f));
            refCount++;
        }

        // ImageLists de 8/24 bits degradam o icone (sem alfa, paleta reduzida): guarda tambem essas versoes como referencia
        static void AddDegraded(object src, IconFamily fam)
        {
            foreach (ColorDepth d in new ColorDepth[] { ColorDepth.Depth8Bit, ColorDepth.Depth24Bit, ColorDepth.Depth32Bit })
            {
                try
                {
                    using (ImageList il = new ImageList())
                    {
                        il.ColorDepth = d; il.ImageSize = new Size(16, 16);
                        if (src is Icon) il.Images.Add((Icon)src); else il.Images.Add((Image)src);
                        using (Image b = il.Images[0]) AddRef(b, fam);
                    }
                }
                catch { }
            }
        }

        static int Dist(short[] a, short[] b, int stop)
        {
            int d = 0;
            for (int i = 0; i < a.Length; i++)
            {
                int x = a[i], y = b[i];
                if (x == y) continue;
                if (x == 0 || y == 0) d++;
                else
                {
                    x--; y--;
                    if (Math.Abs((x >> 6) - (y >> 6)) > 1 || Math.Abs(((x >> 3) & 7) - ((y >> 3) & 7)) > 1 || Math.Abs((x & 7) - (y & 7)) > 1) d++;
                }
                if (d > stop) return d;
            }
            return d;
        }

        // ---- tamanhos com DPI: o GeneXus redimensiona os icones de comando DUAS vezes (ex.: 125% -> 16 vira 20 no cache e 25 na barra) ----
        static readonly List<KeyValuePair<object, IconFamily>> origs = new List<KeyValuePair<object, IconFamily>>();
        static readonly List<int> builtSizes = new List<int>();
        static readonly Dictionary<string, IconFamily> famByKey = new Dictionary<string, IconFamily>(StringComparer.OrdinalIgnoreCase);

        internal static int DpiScale(int v)
        {
            int d = 16;
            try { d = Artech.Common.Localization.CachingResourceManager.DefaultImageSize.Width; } catch { }
            if (d <= 0) d = 16;
            return (int)Math.Round(v * d / 16.0);
        }

        // copia fiel de Artech.Common.Helpers.GDI.GdiHelper.ResizeImage (o que o GeneXus usa nos icones de menu/barra)
        internal static Bitmap GxResize(Image image, int size)
        {
            Bitmap r = new Bitmap(size, size, PixelFormat.Format32bppPArgb);
            try { r.SetResolution(image.HorizontalResolution, image.VerticalResolution); } catch { }
            using (Graphics g = Graphics.FromImage(r))
            using (ImageAttributes ia = new ImageAttributes())
            {
                g.CompositingMode = CompositingMode.SourceCopy; g.CompositingQuality = CompositingQuality.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.SmoothingMode = SmoothingMode.HighQuality; g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                ia.SetWrapMode(WrapMode.TileFlipXY);
                g.DrawImage(image, new Rectangle(0, 0, size, size), 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, ia);
            }
            return r;
        }

        static void AddScaled(object orig, IconFamily fam, int size)
        {
            try
            {
                Icon oi = orig as Icon;
                if (oi != null)
                {
                    foreach (int b in new int[] { 16, 20, 24, 32 })
                    {
                        if (b == size) continue;
                        using (Icon f = new Icon(oi, b, b)) using (Bitmap bb = f.ToBitmap()) using (Bitmap r = GxResize(bb, size)) AddRef(r, fam);
                    }
                    using (Icon f = new Icon(oi, size, size)) using (Bitmap bb = f.ToBitmap()) { if (bb.Width != size) using (Bitmap r = GxResize(bb, size)) AddRef(r, fam); }
                }
                else
                {
                    Image im = orig as Image;
                    if (im != null && im.Width != size) using (Bitmap r = GxResize(im, size)) AddRef(r, fam);
                }
            }
            catch { }
        }

        // cria referencias para um tamanho ainda nao visto (qualquer DPI); chamado com sync travado
        static void EnsureSize(int w)
        {
            if (w < 12 || w > 64 || builtSizes.Contains(w)) return;
            builtSizes.Add(w);
            foreach (KeyValuePair<object, IconFamily> kv in origs) AddScaled(kv.Key, kv.Value, w);
            fuzzyCache.Clear();
        }

        internal static void PrepareDpiSizes()
        {
            lock (sync)
            {
                int d = DpiScale(16);
                EnsureSize(d); EnsureSize(DpiScale(d));
            }
        }

        // ---- mapa comando da barra -> imagem declarada nos .package do GeneXus (CommandDefinition id/image) ----
        static Dictionary<string, string> cmdImage;
        static int cmdAsmCount;
        static IconFamily FamilyForCommand(string toolKey)
        {
            if (string.IsNullOrEmpty(toolKey)) return null;
            lock (sync)
            {
                Assembly[] all = AppDomain.CurrentDomain.GetAssemblies();
                if (cmdImage == null || all.Length != cmdAsmCount)
                {
                    if (cmdImage == null) cmdImage = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    cmdAsmCount = all.Length;
                    foreach (Assembly a in all)
                    {
                        try
                        {
                            if (a.IsDynamic) continue;
                            string n = a.GetName().Name;
                            if (!n.StartsWith("Artech.", StringComparison.Ordinal) && !n.StartsWith("GeneXus", StringComparison.OrdinalIgnoreCase) && !n.StartsWith("Genexus", StringComparison.Ordinal)) continue;
                            foreach (string rn in a.GetManifestResourceNames())
                            {
                                if (!rn.EndsWith(".package", StringComparison.OrdinalIgnoreCase)) continue;
                                using (Stream st = a.GetManifestResourceStream(rn))
                                using (System.Xml.XmlReader xr = System.Xml.XmlReader.Create(st, new System.Xml.XmlReaderSettings { DtdProcessing = System.Xml.DtdProcessing.Ignore, XmlResolver = null }))
                                    while (xr.Read())
                                        if (xr.NodeType == System.Xml.XmlNodeType.Element && xr.LocalName == "CommandDefinition")
                                        {
                                            string id = xr.GetAttribute("id"), im = xr.GetAttribute("image");
                                            if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(im) && !cmdImage.ContainsKey(id)) cmdImage[id] = im;
                                        }
                            }
                        }
                        catch { }
                    }
                }
                foreach (string k in KeyCandidates(toolKey))
                {
                    string im; IconFamily f;
                    if (cmdImage.TryGetValue(k, out im) && famByKey.TryGetValue(im, out f)) return f;
                }
            }
            return null;
        }

        static IEnumerable<string> KeyCandidates(string k)
        {
            yield return k;
            int i = k.LastIndexOfAny(new char[] { '.', ':', '|', '/', '\\' });
            if (i >= 0 && i < k.Length - 1) yield return k.Substring(i + 1);
            int j = k.IndexOfAny(new char[] { '#', '@', '_' });
            if (j > 0) yield return k.Substring(0, j);
        }

        static readonly Dictionary<Type, PropertyInfo> toolKeyProp = new Dictionary<Type, PropertyInfo>();
        // chave da ferramenta (ToolBase.Key) dona do elemento, sem referenciar o assembly de toolbars
        internal static string ToolKey(UIElement e)
        {
            for (UIElement x = e; x != null; x = x.Parent)
            {
                object c = null;
                try { c = x.GetContext(); } catch { }
                if (c == null) continue;
                Type t = c.GetType();
                PropertyInfo pk;
                lock (toolKeyProp)
                {
                    if (!toolKeyProp.TryGetValue(t, out pk))
                    {
                        pk = null;
                        for (Type b = t; b != null; b = b.BaseType) if (b.FullName == "Infragistics.Win.UltraWinToolbars.ToolBase") { pk = t.GetProperty("Key", BindingFlags.Public | BindingFlags.Instance, null, typeof(string), Type.EmptyTypes, null); break; }
                        toolKeyProp[t] = pk;
                    }
                }
                if (pk != null) { try { return pk.GetValue(c, null) as string; } catch { return null; } }
            }
            return null;
        }

        static readonly HashSet<string> unmatchedSeen = new HashSet<string>();
        static readonly List<string> unmatchedList = new List<string>();
        static DateTime unmatchedLogged = DateTime.MinValue;
        internal static void NoteUnmatched(string where, Image img)
        {
            if (img.Width < 10 || img.Height < 10) return;
            string k = where + " " + img.Width + "x" + img.Height;
            lock (unmatchedSeen)
            {
                if (unmatchedSeen.Count > 400 || !unmatchedSeen.Add(k)) return;
                unmatchedList.Add(k);
            }
        }
        public static string[] Unmatched { get { lock (unmatchedSeen) return unmatchedList.ToArray(); } }
        internal static void FlushUnmatched()
        {
            string[] l;
            lock (unmatchedSeen)
            {
                if (unmatchedList.Count == 0 || (DateTime.Now - unmatchedLogged).TotalSeconds < 30) return;
                l = unmatchedList.ToArray(); unmatchedList.Clear(); unmatchedLogged = DateTime.Now;
            }
            DarkEngine.Log("Icones Next: " + l.Length + " icone(s) da barra/menus SEM equivalente Next (mantido original): " + string.Join("; ", l));
        }

        static IconFamily Match(Image img)
        {
            int w, h;
            int[] px;
            try { px = Pixels(img, out w, out h); } catch { return null; }   // imagem em uso por outro desenho: tenta depois
            if (px == null) return null;
            short[] s = Sig(px);
            long k = Hash(s);
            IconFamily f;
            lock (sync)
            {
                if (exact.TryGetValue(k, out f)) return f;
                if (!builtSizes.Contains(w) && origs.Count > 0) { EnsureSize(w); if (exact.TryGetValue(k, out f)) return f; }
                if (fuzzyCache.TryGetValue(k, out f)) return f;
                List<KeyValuePair<short[], IconFamily>> l;
                f = null;
                if (refs.TryGetValue(w, out l))
                {
                    int tol = Math.Max(2, s.Length / 40), best = int.MaxValue, second = int.MaxValue;
                    IconFamily bf = null;
                    foreach (KeyValuePair<short[], IconFamily> kv in l)
                    {
                        int d = Dist(s, kv.Key, Math.Min(second, tol + 3));
                        if (d < best) { if (bf != kv.Value) second = best; best = d; bf = kv.Value; }
                        else if (d < second && kv.Value != bf) second = d;
                    }
                    if (best <= tol && second - best >= 2) f = bf;
                }
                if (fuzzyCache.Count > 20000) fuzzyCache.Clear();
                fuzzyCache[k] = f;
            }
            return f;
        }

        static void Remember(Image img, IconHit h)
        {
            try { known.Remove(img); known.Add(img, h); matched++; } catch { }
        }

        public static IconHit Lookup(Image img)
        {
            if (img == null || !Active) return null;
            IconHit h;
            if (known.TryGetValue(img, out h)) return h.F == null ? null : h;
            int w = 0;
            try { w = img.Width; } catch { return null; }
            IconFamily f = Match(img);
            h = f == null ? NoHit : new IconHit { F = f, State = -1 };
            try { known.Add(img, h); } catch { }
            if (f != null) matched++;
            return f == null ? null : h;
        }

        // usado pelos testes/diagnostico
        public static Bitmap Variant(Image img, int state) { IconHit h = Lookup(img); return h == null ? null : h.F.Get(state, img.Width, img.Height); }
        public static string FamilyName(Image img) { IconHit h = Lookup(img); return h == null ? null : h.F.Name; }

        // ================= icones sem equivalente no Next: versao "amigavel ao escuro" =================
        static IconFamily DarkFriendlyFamily(object orig, string label)
        {
            IconFamily f = new IconFamily { Name = "original:" + label, Label = label };
            Icon oi = orig as Icon;
            foreach (int s in new int[] { 16, 24, 32 })
            {
                Bitmap src = null;
                try
                {
                    if (oi != null) { using (Icon fi = new Icon(oi, s, s)) src = fi.ToBitmap(); }
                    else if (orig is Image && ((Image)orig).Width == s) src = new Bitmap((Image)orig);
                    if (src == null || src.Width != s) continue;
                    Bitmap en = DarkFriendly(src);
                    f.Put(0, en); f.Put(1, en); f.Put(2, Disabled(f.MasterOf(en)));
                }
                finally { if (src != null) src.Dispose(); }
            }
            if (!f.Has(0, 16) && orig is Image)
            {
                using (Bitmap src = new Bitmap((Image)orig)) { Bitmap en = DarkFriendly(src); f.Put(0, en); f.Put(1, en); f.Put(2, Disabled(f.MasterOf(en))); }
            }
            return f;
        }

        static double L(int r, int g, int b) { return 0.299 * r + 0.587 * g + 0.114 * b; }

        // clareia contornos escuros (ligados a transparencia) e limpa franjas brancas semitransparentes
        internal static Bitmap DarkFriendly(Bitmap src)
        {
            int w = src.Width, h = src.Height;
            int ww, hh;
            int[] px = Pixels(src, out ww, out hh);
            if (px == null) return new Bitmap(src);
            bool[] outline = new bool[px.Length];
            Queue<int> q = new Queue<int>();
            Func<int, bool> dark = delegate(int i) { int c = px[i]; return ((c >> 24) & 255) >= 40 && L((c >> 16) & 255, (c >> 8) & 255, c & 255) < 95; };
            Func<int, bool> clear = delegate(int i) { return ((px[i] >> 24) & 255) < 40; };
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (!dark(i)) continue;
                    bool edge = x == 0 || y == 0 || x == w - 1 || y == h - 1 || clear(i - 1) || clear(i + 1) || clear(i - w) || clear(i + w);
                    if (edge) { outline[i] = true; q.Enqueue(i); }
                }
            while (q.Count > 0)
            {
                int i = q.Dequeue(), x = i % w, y = i / w;
                int[] nb = { x > 0 ? i - 1 : -1, x < w - 1 ? i + 1 : -1, y > 0 ? i - w : -1, y < h - 1 ? i + w : -1 };
                foreach (int j in nb) if (j >= 0 && !outline[j] && dark(j)) { outline[j] = true; q.Enqueue(j); }
            }
            Color panel = Color.FromArgb(24, 31, 42);   // painel do Next: o resultado e "mestre" e o tema e aplicado em IconFamily.Put
            for (int i = 0; i < px.Length; i++)
            {
                int c = px[i], a = (c >> 24) & 255;
                if (a == 0) continue;
                int r = (c >> 16) & 255, g = (c >> 8) & 255, b = c & 255;
                int mx = Math.Max(r, Math.Max(g, b)), mn = Math.Min(r, Math.Min(g, b));
                double l = L(r, g, b);
                if (outline[i])
                {
                    if (mx - mn < 48) { r = 200; g = 208; b = 220; }
                    else
                    {
                        for (double t = 0.1; t <= 0.9; t += 0.1)
                        {
                            int r2 = (int)(r + (255 - r) * t), g2 = (int)(g + (255 - g) * t), b2 = (int)(b + (255 - b) * t);
                            if (DarkEngine.Contrast(Color.FromArgb(r2, g2, b2), panel) >= 3.5 || t >= 0.85) { r = r2; g = g2; b = b2; break; }
                        }
                    }
                }
                else if (a < 210 && l > 200 && mx - mn < 40) a = (int)(a * 0.35);   // franja clara
                px[i] = (a << 24) | (r << 16) | (g << 8) | b;
            }
            return FromPixels(px, w, h);
        }

        // ---- glifo monocromatico preto/cinza escuro sem familia conhecida: pixels escuros sem cor -> #DCE2EB (alfa mantido),
        // pixels coloridos intactos. So quando >= 70% dos pixels visiveis sao escuros e sem cor (icone "de linha" preto).
        sealed class MonoBox { public Bitmap B, D; }
        static ConditionalWeakTable<Image, MonoBox> monos = new ConditionalWeakTable<Image, MonoBox>();
        internal static void LimparMonos() { lock (monoLock) monos = new ConditionalWeakTable<Image, MonoBox>(); }
        static readonly object monoLock = new object();
        static readonly HashSet<Image> monoOut = new HashSet<Image>();
        static int monoCount;
        internal static bool IsMonoResult(Image img) { lock (monoOut) return img != null && monoOut.Contains(img); }
        internal static Bitmap MonoLift(Image img, bool disabled)
        {
            if (img == null || img.Width > 96 || img.Height > 96 || img.Width < 4 || img.Height < 4) return null;
            MonoBox mb;
            lock (monoLock)
            {
                if (!monos.TryGetValue(img, out mb))
                {
                    mb = new MonoBox();
                    try
                    {
                        lock (monoOut) if (monoOut.Contains(img)) { monos.Add(img, mb); return null; }
                        int w, h;
                        int[] px = Pixels(img, out w, out h);
                        if (px != null)
                        {
                            int n = 0, d = 0;
                            for (int i = 0; i < px.Length; i++)
                            {
                                int c = px[i], a = (c >> 24) & 255;
                                if (a < 40) continue;
                                int r = (c >> 16) & 255, g = (c >> 8) & 255, b = c & 255;
                                int sat = Math.Max(r, Math.Max(g, b)) - Math.Min(r, Math.Min(g, b));
                                n++;
                                if (sat < 48 && L(r, g, b) < 95) d++;
                            }
                            if (n >= 6 && d >= n * 0.7)
                            {
                                Color f = DarkEngine.Fg;
                                for (int i = 0; i < px.Length; i++)
                                {
                                    int c = px[i], a = (c >> 24) & 255;
                                    if (a == 0) continue;
                                    int r = (c >> 16) & 255, g = (c >> 8) & 255, b = c & 255;
                                    int sat = Math.Max(r, Math.Max(g, b)) - Math.Min(r, Math.Min(g, b));
                                    if (sat < 48 && L(r, g, b) < 170) px[i] = (a << 24) | (f.R << 16) | (f.G << 8) | f.B;
                                }
                                mb.B = FromPixels(px, w, h);
                                mb.D = Disabled(mb.B);
                                lock (monoOut) { monoOut.Add(mb.B); monoOut.Add(mb.D); }
                                if (++monoCount <= 25) DarkEngine.Log("Glifo escuro clareado (#DCE2EB): " + w + "x" + h + " (" + d + "/" + n + " px escuros)");
                            }
                        }
                    }
                    catch { }
                    try { monos.Add(img, mb); } catch { }
                }
            }
            return disabled ? mb.D : mb.B;
        }

        internal static Bitmap Disabled(Bitmap en)
        {
            int w, h;
            int[] px = Pixels(en, out w, out h);
            if (px == null) return new Bitmap(en);
            for (int i = 0; i < px.Length; i++)
            {
                int c = px[i], a = (c >> 24) & 255;
                if (a == 0) continue;
                int l = (int)L((c >> 16) & 255, (c >> 8) & 255, c & 255);
                l = (int)(150 + (l / 255.0) * 80);
                px[i] = ((int)(a * 0.55) << 24) | (l << 16) | (l << 8) | Math.Min(255, l + 8);
            }
            return FromPixels(px, w, h);
        }

        internal static Bitmap FromPixels(int[] px, int w, int h)
        {
            Bitmap b = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            BitmapData d = b.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try { for (int y = 0; y < h; y++) Marshal.Copy(px, y * w, new IntPtr(d.Scan0.ToInt64() + (long)y * d.Stride), w); }
            finally { b.UnlockBits(d); }
            return b;
        }

        // .ico em memoria com quadros BMP 32 bits (alfa), para entregar Icon ao GeneXus
        internal static byte[] Ico(List<Bitmap> frames)
        {
            MemoryStream ms = new MemoryStream();
            BinaryWriter bw = new BinaryWriter(ms);
            List<byte[]> data = new List<byte[]>();
            foreach (Bitmap f in frames)
            {
                int w, h;
                int[] px = Pixels(f, out w, out h);
                int maskRow = ((w + 31) / 32) * 4;
                MemoryStream fm = new MemoryStream();
                BinaryWriter fw = new BinaryWriter(fm);
                fw.Write(40); fw.Write(w); fw.Write(h * 2); fw.Write((short)1); fw.Write((short)32); fw.Write(0); fw.Write(w * h * 4 + maskRow * h); fw.Write(0); fw.Write(0); fw.Write(0); fw.Write(0);
                for (int y = h - 1; y >= 0; y--) for (int x = 0; x < w; x++) fw.Write(px[y * w + x]);
                for (int y = h - 1; y >= 0; y--)
                {
                    byte[] row = new byte[maskRow];
                    for (int x = 0; x < w; x++) if (((px[y * w + x] >> 24) & 255) == 0) row[x >> 3] |= (byte)(0x80 >> (x & 7));
                    fw.Write(row);
                }
                fw.Flush();
                data.Add(fm.ToArray());
            }
            bw.Write((short)0); bw.Write((short)1); bw.Write((short)frames.Count);
            int off = 6 + 16 * frames.Count;
            for (int i = 0; i < frames.Count; i++)
            {
                int w = frames[i].Width, h = frames[i].Height;
                bw.Write((byte)(w >= 256 ? 0 : w)); bw.Write((byte)(h >= 256 ? 0 : h)); bw.Write((byte)0); bw.Write((byte)0);
                bw.Write((short)1); bw.Write((short)32); bw.Write(data[i].Length); bw.Write(off);
                off += data[i].Length;
            }
            foreach (byte[] d in data) bw.Write(d);
            bw.Flush();
            return ms.ToArray();
        }

        // ================= WorkWithPlus: icone de atributo/variavel OBRIGATORIO =================
        // O WWP desenha o obrigatorio (attributeReq/variableReq) como glifo vermelho sobre QUADRADO BRANCO.
        // Aqui (so em memoria, sem alterar o pacote) ele vira o MESMO glifo do atributo/variavel normal,
        // recolorido no vermelho de erro do GeneXus Next (Mercury dark --color-icon-error-default = red-600 #E35861),
        // com fundo 100% transparente. Os demais icones do WWP (TableContent, Panel, Group, ...) nao sao tocados.
        internal static readonly Color NextErrorRed = Color.FromArgb(0xE3, 0x58, 0x61);
        static bool wwpDone;
        static void WwpRequired()
        {
            Assembly wwp = null, gxr = null;
            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                string n; try { n = a.GetName().Name; } catch { continue; }
                if (n == "DVelop.Patterns.WorkWithPlus") wwp = a; else if (n == "Artech.Genexus.UI.Resources") gxr = a;
            }
            if (wwp == null || gxr == null) return;
            wwpDone = true;
            ResourceManager wr = new ResourceManager("DVelop.Patterns.WorkWithPlus.Resources.Icons", wwp);
            ResourceManager gr = new ResourceManager("Artech.Genexus.UI.Resources.Images", gxr);   // instancia propria: icone ORIGINAL do GeneXus
            int a1 = WwpOne(wr, gr, new string[] { "attributeReq", "attributeReqEv3" }, "ObjectAttribute");
            int a2 = WwpOne(wr, gr, new string[] { "variableReq", "variableReqEv3" }, "Variable");
            fuzzyCache.Clear();
            DarkEngine.Log("WorkWithPlus: icone de atributo obrigatorio = glifo do atributo normal em vermelho Next #E35861, fundo transparente (referencias=" + a1 + "); variavel obrigatoria (referencias=" + a2 + ")");
        }

        static int WwpOne(ResourceManager wr, ResourceManager gr, string[] reqKeys, string normalKey)
        {
            Icon normal = null;
            try { normal = gr.GetObject(normalKey) as Icon; } catch { }
            if (normal == null) { DarkEngine.Log("WorkWithPlus: icone normal nao encontrado: " + normalKey); return 0; }
            IconFamily f = new IconFamily { Name = "wwp-obrigatorio:" + normalKey, Label = "WWP obrigatorio " + normalKey };
            foreach (int s in new int[] { 16, 20, 24, 32 })
            {
                try
                {
                    using (Icon fi = new Icon(normal, s, s)) using (Bitmap b = fi.ToBitmap())
                    {
                        if (b.Width != s) continue;
                        Bitmap r = RecolorTo(b, NextErrorRed);
                        f.Put(0, r); f.Put(1, r); f.Put(2, Disabled(r));
                    }
                }
                catch { }
            }
            int before = refCount;
            foreach (string k in reqKeys)
            {
                object o = null;
                try { o = wr.GetObject(k); } catch { }
                if (o == null) { DarkEngine.Log("WorkWithPlus: recurso nao encontrado: " + k); continue; }
                origs.Add(new KeyValuePair<object, IconFamily>(o, f));
                foreach (int bs in builtSizes) AddScaled(o, f, bs);
                Icon oi = o as Icon;
                List<Bitmap> frames = new List<Bitmap>();
                if (oi != null)
                {
                    foreach (int s in new int[] { 16, 20, 24, 32, 64 }) { try { using (Icon fi = new Icon(oi, s, s)) frames.Add(fi.ToBitmap()); } catch { } }
                    try { frames.Add(oi.ToBitmap()); } catch { }
                }
                else if (o is Image) frames.Add(new Bitmap((Image)o));
                foreach (Bitmap b in frames)
                {
                    AddRef(b, f);
                    foreach (int t in new int[] { DpiScale(b.Width), DpiScale(16), DpiScale(DpiScale(16)), 16, 20 })
                        if (t != b.Width && t >= 12 && t <= 64) using (Bitmap r = GxResize(b, t)) AddRef(r, f);
                    if (b.Width == 16 || b.Width == DpiScale(16)) AddDegradedSize(b, f, b.Width);
                }
                if (oi != null) foreach (int s in new int[] { 16, 20, 24, DpiScale(16), DpiScale(24) })
                {
                    try { using (Icon fi = new Icon(oi, s, s)) AddDegradedSize(fi, f, s); } catch { }
                    try { AddDegradedSize(oi, f, s); } catch { }   // ImageList escolhe o quadro do icone original
                }
                foreach (Bitmap b in frames) b.Dispose();
            }
            return refCount - before;
        }

        static void AddDegradedSize(object src, IconFamily fam, int size)
        {
            foreach (ColorDepth d in new ColorDepth[] { ColorDepth.Depth4Bit, ColorDepth.Depth8Bit, ColorDepth.Depth24Bit, ColorDepth.Depth32Bit })
            {
                try
                {
                    using (ImageList il = new ImageList())
                    {
                        il.ColorDepth = d; il.ImageSize = new Size(size, size);
                        if (src is Icon) il.Images.Add((Icon)src); else il.Images.Add((Image)src);
                        using (Image b = il.Images[0]) AddRef(b, fam);
                    }
                }
                catch { }
            }
        }

        // recolore um glifo mantendo forma, alfa e o sombreado relativo (sem fundo, sem franja clara)
        internal static Bitmap RecolorTo(Bitmap src, Color target)
        {
            int w, h;
            int[] px = Pixels(src, out w, out h);
            if (px == null) return new Bitmap(src);
            double sum = 0, wsum = 0;
            for (int i = 0; i < px.Length; i++)
            {
                int c = px[i], a = (c >> 24) & 255;
                if (a < 100) continue;
                sum += L((c >> 16) & 255, (c >> 8) & 255, c & 255) * a; wsum += a;
            }
            double avg = wsum > 0 ? sum / wsum : 128;
            if (avg < 1) avg = 1;
            for (int i = 0; i < px.Length; i++)
            {
                int c = px[i], a = (c >> 24) & 255;
                if (a == 0) { px[i] = 0; continue; }
                double k = L((c >> 16) & 255, (c >> 8) & 255, c & 255) / avg;
                if (k < 0.7) k = 0.7; if (k > 1.25) k = 1.25;
                int r, g, b;
                if (k <= 1) { r = (int)(target.R * k); g = (int)(target.G * k); b = (int)(target.B * k); }
                else { double t = (k - 1) * 0.8; r = (int)(target.R + (255 - target.R) * t); g = (int)(target.G + (255 - target.G) * t); b = (int)(target.B + (255 - target.B) * t); }
                px[i] = (a << 24) | (r << 16) | (g << 8) | b;
            }
            return FromPixels(px, w, h);
        }

        // ================= Infragistics: troca na hora do desenho =================
        static readonly Dictionary<UIElement, Image> swapped = new Dictionary<UIElement, Image>();
        static PropertyInfo pGray;

        internal static bool BeforeImage(ref UIElementDrawParams p, bool toolbar)
        {
            UIElement e = p.Element;
            ImageUIElementBase ie = e as ImageUIElementBase;
            if (ie != null)
            {
                Image img = ie.Image;
                if (img == null) return false;
                IconHit hit = Lookup(img);
                if (hit == null && toolbar)
                {
                    string tk = ToolKey(e);
                    IconFamily cf = FamilyForCommand(tk);
                    if (cf != null) { hit = new IconHit { F = cf, State = -1 }; Remember(img, hit); }
                    else if (tk != null) NoteUnmatched(tk, img);
                }
                bool en = ie.Enabled;
                int st = !en ? 2 : (toolbar && Hot(ie)) ? 1 : 0;
                Bitmap v;
                if (hit == null)
                {
                    v = MonoLift(img, st == 2);
                    if (v == null) return false;
                }
                else v = hit.F.Get(st, img.Width, img.Height);
                if (v == null || (v == img && st != 2)) return false;
                if (e is ImageUIElement)
                {
                    // troca temporaria; o Infragistics posiciona o icone; AfterDrawImage devolve o original
                    swapped[e] = img;
                    ((ImageUIElement)e).Image = v;
                    if (st == 2) SetGray(ie, false);
                    return false;
                }
                // elementos cujo Image nao pode ser trocado: desenho proprio na mesma posicao
                Rectangle rr = ie.RectInsideBorders;
                Rectangle tr;
                if (ie.Scaled) tr = rr;
                else
                {
                    tr = new Rectangle(rr.X, rr.Y, img.Width, img.Height);
                    if (ie.CenterImage) { if (img.Width < rr.Width) tr.X += (rr.Width - img.Width) / 2; if (img.Height < rr.Height) tr.Y += (rr.Height - img.Height) / 2; }
                }
                Region old = p.Graphics.Clip;
                try { p.Graphics.SetClip(rr, CombineMode.Intersect); p.Graphics.DrawImage(v, tr); }
                finally { p.Graphics.Clip = old; }
                return true;
            }
            Image ai = p.AppearanceData.Image as Image;
            if (ai != null)
            {
                IconHit hit = Lookup(ai);
                Bitmap v = hit == null ? MonoLift(ai, !e.Enabled) : hit.F.Get(e.Enabled ? 0 : 2, ai.Width, ai.Height);
                if (v != null) p.AppearanceData.Image = v;
            }
            return false;
        }

        internal static void AfterImage(ref UIElementDrawParams p)
        {
            UIElement e = p.Element;
            Image orig;
            if (e != null && swapped.TryGetValue(e, out orig))
            {
                swapped.Remove(e);
                ((ImageUIElement)e).Image = orig;
                SetGray((ImageUIElementBase)e, true);
            }
        }

        static void SetGray(ImageUIElementBase e, bool v)
        {
            if (pGray == null) pGray = typeof(ImageUIElementBase).GetProperty("RenderGrayScaleDisabledImages", BindingFlags.Instance | BindingFlags.NonPublic);
            if (pGray != null) pGray.SetValue(e, v, null);
        }

        internal static bool Hot(UIElement e)
        {
            try
            {
                if (Control.MouseButtons != MouseButtons.None) return false;
                Control c = e.Control;
                if (c == null || !c.IsHandleCreated) return false;
                Point m = c.PointToClient(Control.MousePosition);
                if (!c.ClientRectangle.Contains(m)) return false;
                UIElement par = e.Parent ?? e;
                return par.Rect.Contains(m);
            }
            catch { return false; }
        }

        // barra de ferramentas Infragistics (inclui Artech.FrameworkDE.ToolBarManager, cujo nome nao contem "Toolbars")
        static bool IsToolbarHost(object o)
        {
            for (Type t = o.GetType(); t != null; t = t.BaseType)
            {
                string n = t.FullName;
                if (n == null) continue;
                if (n.StartsWith("Infragistics.Win.UltraWinToolbars.", StringComparison.Ordinal)) return true;
                if (n.IndexOf("Toolbar", StringComparison.OrdinalIgnoreCase) >= 0 && n.StartsWith("Infragistics.", StringComparison.Ordinal)) return true;
            }
            return false;
        }

        static void EnsureFilter(object host, bool toolbar)
        {
            UltraControlBase uc = host as UltraControlBase;
            UltraComponentControlManagerBase mg = host as UltraComponentControlManagerBase;
            if (uc == null && mg == null) return;
            IUIElementDrawFilter cur = uc != null ? uc.DrawFilter : mg.DrawFilter;
            if (cur is IconDrawFilter) return;
            IconDrawFilter nf = new IconDrawFilter(cur, toolbar);
            if (uc != null) uc.DrawFilter = nf; else mg.DrawFilter = nf;
            lock (sync) { if (!filters.ContainsKey(host)) filters[host] = cur; }
            if (cur == null) DarkEngine.LogOnce("icfiltn:" + host.GetType().FullName, "Icones Next: filtro de desenho instalado em " + host.GetType().FullName + (toolbar ? " (barra de ferramentas/menus)" : ""));
            if (cur != null) DarkEngine.LogOnce("icfilt:" + host.GetType().FullName, "Icones Next: filtro de desenho encadeado ao existente em " + host.GetType().FullName + " (" + cur.GetType().FullName + ")");
        }

        // ================= passada pelos controles =================
        static readonly Dictionary<Type, PropertyInfo[]> mgrProps = new Dictionary<Type, PropertyInfo[]>();
        static readonly Dictionary<Type, PropertyInfo[]> listProps = new Dictionary<Type, PropertyInfo[]>();
        static readonly HashSet<Form> formsScanned = new HashSet<Form>();

        public static void Visit(Control c, bool full)
        {
            if (!Active || c == null) return;
            try
            {
                UltraControlBase uc = c as UltraControlBase;
                if (uc != null) EnsureFilter(uc, IsToolbarHost(uc));
                TreeView tvs = c as TreeView;
                if (tvs != null) { try { StateFix(tvs); } catch (Exception ex) { Fail("lista de estados " + c.GetType().Name, ex); } }
                if (c.GetType().FullName.StartsWith("Crownwood.Magic.Controls.TabControl", StringComparison.Ordinal)) { try { PartIcons(c); } catch (Exception ex) { Fail("icone da parte default", ex); } }
                if (!full) return;
                Type t = c.GetType();
                PropertyInfo[] mp;
                if (!mgrProps.TryGetValue(t, out mp))
                {
                    List<PropertyInfo> l = new List<PropertyInfo>(), il = new List<PropertyInfo>();
                    foreach (PropertyInfo pi in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                    {
                        if (pi.GetIndexParameters().Length > 0 || !pi.CanRead) continue;
                        if (typeof(UltraComponentControlManagerBase).IsAssignableFrom(pi.PropertyType)) l.Add(pi);
                        else if (typeof(ImageList).IsAssignableFrom(pi.PropertyType) || (pi.PropertyType == typeof(object) && pi.Name.IndexOf("ImageList", StringComparison.Ordinal) >= 0)) il.Add(pi);
                    }
                    mp = l.ToArray(); mgrProps[t] = mp; listProps[t] = il.ToArray();
                }
                foreach (PropertyInfo pi in mp)
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
                        if (il != null) SwapList(il);
                    }
                ToolStrip ts = c as ToolStrip;
                if (ts != null) foreach (ToolStripItem it in ts.Items) SwapItem(it, 0);
                if (t.FullName.StartsWith("Crownwood.Magic.Controls.TabControl", StringComparison.Ordinal)) Crownwood(c);
            }
            catch (Exception ex) { Fail("passada " + c.GetType().Name, ex); }
        }

        static void ScanFormFields(Form f)
        {
            for (Type t = f.GetType(); t != null && t != typeof(Form); t = t.BaseType)
                foreach (FieldInfo fi in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    object v = null;
                    try
                    {
                        if (typeof(UltraComponentControlManagerBase).IsAssignableFrom(fi.FieldType)) v = fi.GetValue(f);
                        else if (typeof(IContainer).IsAssignableFrom(fi.FieldType))
                        {
                            IContainer ct = fi.GetValue(f) as IContainer;
                            if (ct != null) foreach (IComponent comp in ct.Components) if (comp is UltraComponentControlManagerBase) EnsureFilter(comp, IsToolbarHost(comp));
                        }
                    }
                    catch { }
                    if (v != null) EnsureFilter(v, IsToolbarHost(v));
                }
        }

        static void SwapItem(ToolStripItem it, int depth)
        {
            if (it == null || depth > 6 || it is ToolStripControlHost) return;
            try
            {
                Image img = it.Image;
                if (img != null && it.ImageIndex < 0 && string.IsNullOrEmpty(it.ImageKey))
                {
                    IconHit h = Lookup(img);
                    if (h != null && h.State == -1)
                    {
                        Bitmap v = h.F.Get(0, img.Width, img.Height);
                        if (v != null) { lock (sync) { if (!items.ContainsKey(it)) items[it] = img; } it.Image = v; }
                    }
                }
            }
            catch (Exception ex) { Fail("ToolStripItem", ex); }
            ToolStripDropDownItem dd = it as ToolStripDropDownItem;
            if (dd != null && dd.HasDropDownItems) foreach (ToolStripItem k in dd.DropDownItems) SwapItem(k, depth + 1);
        }

        static void SwapList(ImageList il)
        {
            ListRec r;
            lists.TryGetValue(il, out r);
            int n = il.Images.Count;
            if (r != null && r.Count == n) return;
            if (n == 0 || n > 2000) return;
            Size sz = il.ImageSize;
            Image[] imgs = new Image[n], repl = new Image[n];
            bool any = false;
            int start = (r != null && r.Changed) ? r.Count : 0;
            for (int i = start; i < n; i++)
            {
                Image im = il.Images[i];
                imgs[i] = im;
                IconHit h = Lookup(im);
                if (h != null && h.State == -1) { repl[i] = h.F.Get(0, sz.Width, sz.Height); any = repl[i] != null || any; }
            }
            if (r == null) { r = new ListRec { Depth = il.ColorDepth }; lists[il] = r; }
            if (!any) { r.Count = n; return; }
            if (!r.Changed)
            {
                for (int i = 0; i < n; i++) { r.Imgs.Add(imgs[i]); r.Keys.Add(i < il.Images.Keys.Count ? il.Images.Keys[i] : ""); }
                r.Changed = true;
                if (il.ColorDepth != ColorDepth.Depth32Bit)
                {
                    // copia nativa exata (pixels + mascara) para devolver sem requantizar no modo claro
                    try { if (il.HandleCreated) r.Dup = ImageList_Duplicate(il.Handle); } catch { r.Dup = IntPtr.Zero; }
                    il.Images.Clear();
                    il.ColorDepth = ColorDepth.Depth32Bit;
                    for (int i = 0; i < n; i++)
                    {
                        Image im = repl[i] ?? imgs[i];
                        if (string.IsNullOrEmpty(r.Keys[i])) il.Images.Add(im); else il.Images.Add(r.Keys[i], im);
                    }
                    r.Count = il.Images.Count;
                    return;
                }
            }
            for (int i = start; i < n; i++) if (repl[i] != null) il.Images[i] = repl[i];
            r.Count = n;
        }

        // ================= lista de estados nativa do TreeView (status da Navigation View / Impact Analysis) =================
        // TreeView.StateImageList do WinForms NAO usa a lista que o GeneXus entrega: UpdateNativeStateImageList copia as
        // imagens para um ImageList interno novo (ColorDepth padrao = 8 bits, imagem 0 duplicada) e manda esse para o
        // controle nativo (TVM_SETIMAGELIST/TVSIL_STATE). Em 8 bits o alfa vira mascara 1 bit e a borda suavizada dos
        // icones de status vira um anel claro. Aqui: lista propria em 32 bits (com os icones Next, ou o original limpo),
        // so neste controle; o handle interno do WinForms fica guardado e volta no RestoreAll.
        const int TVM_GETIMAGELIST = 0x1108, TVM_SETIMAGELIST = 0x1109, TVSIL_STATE = 2;
        [DllImport("user32.dll", EntryPoint = "SendMessageW")] static extern IntPtr SendMsg(IntPtr h, int m, IntPtr w, IntPtr l);
        [DllImport("comctl32.dll")] static extern IntPtr ImageList_Duplicate(IntPtr himl);
        [DllImport("comctl32.dll")] static extern bool ImageList_Destroy(IntPtr himl);
        [DllImport("comctl32.dll")] static extern int ImageList_GetImageCount(IntPtr himl);
        [DllImport("comctl32.dll")] static extern IntPtr ImageList_GetIcon(IntPtr himl, int i, int flags);
        [DllImport("comctl32.dll")] static extern int ImageList_ReplaceIcon(IntPtr himl, int i, IntPtr hicon);
        [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr h);
        sealed class StateRec { public ImageList Ours, Src; public IntPtr Orig, Hwnd; public int Count; }
        static readonly Dictionary<TreeView, StateRec> stateLists = new Dictionary<TreeView, StateRec>();
        // chaves usadas pelo GeneXus (SpecificationResult/ImpactAnalysisResult.CreateStatusImages) -> familia Next
        static readonly Dictionary<string, string> statusKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "OK", "gx-test/result-ok" }, { "genreq", "gx-test/result-ok" },
            { "Warning", "gx-test/result-warning" }, { "genreqw", "gx-test/result-warning" },
            { "Error", "derived/status-error" }, { "specfailed", "derived/status-error" },
            { "nogenreq", "derived/status-equal" }, { "nogenspc", "derived/status-equal-warning" }, { "specreq", "derived/status-notfound" },
        };
        static IconFamily FamByName(string n) { foreach (IconFamily f in nextFams) if (f.Name == n) return f; return null; }

        static void StateFix(TreeView tv)
        {
            if (tv.IsDisposed || !tv.IsHandleCreated || tv.CheckBoxes) return;
            ImageList src = tv.StateImageList;
            if (src == null) return;
            int n = src.Images.Count;
            if (n == 0 || n > 256) return;
            IntPtr h = tv.Handle;
            IntPtr cur = SendMsg(h, TVM_GETIMAGELIST, (IntPtr)TVSIL_STATE, IntPtr.Zero);
            if (cur == IntPtr.Zero) return;                       // o WinForms ainda nao montou a lista nativa
            StateRec r;
            lock (sync) stateLists.TryGetValue(tv, out r);
            if (r != null && r.Ours != null && cur == r.Ours.Handle && r.Src == src && r.Count == n && r.Hwnd == h) return;
            Size sz = src.ImageSize;
            ImageList ours = new ImageList { ColorDepth = ColorDepth.Depth32Bit, ImageSize = sz };
            int next = 0, clean = 0;
            Image[] imgs = new Image[n + 1];
            for (int i = 0; i < n; i++)
            {
                Image im = src.Images[i];
                string key = i < src.Images.Keys.Count ? src.Images.Keys[i] : null;
                Bitmap v = null;
                IconHit hit = Lookup(im);
                if (hit != null && hit.F != null && hit.F.IsNext) v = hit.F.Get(0, sz.Width, sz.Height);
                if (v == null && !string.IsNullOrEmpty(key))
                {
                    string fn; IconFamily f;
                    if (statusKeys.TryGetValue(key, out fn) && (f = FamByName(fn)) != null) v = f.Get(0, sz.Width, sz.Height);
                }
                if (v != null) next++; else { v = DeMatte(im, sz); clean++; }
                imgs[i + 1] = v;
            }
            imgs[0] = imgs[1];                                      // igual ao WinForms: indice 0 = "sem estado"
            ours.Images.AddRange(imgs);
            SendMsg(h, TVM_SETIMAGELIST, (IntPtr)TVSIL_STATE, ours.Handle);
            lock (sync)
            {
                if (r == null) { r = new StateRec(); stateLists[tv] = r; tv.Disposed += (o, e) => { lock (sync) { StateRec x; if (stateLists.TryGetValue((TreeView)o, out x)) { stateLists.Remove((TreeView)o); if (x.Ours != null) x.Ours.Dispose(); } } }; }
                ImageList old = r.Ours;
                if (old == null || cur != old.Handle) r.Orig = cur;      // handle do ImageList interno atual do WinForms
                r.Ours = ours; r.Src = src; r.Count = n; r.Hwnd = h;
                if (old != null) old.Dispose();
            }
            tv.Invalidate();
            DarkEngine.LogOnce("tvstate:" + tv.GetType().FullName + ":" + n, "Icones de status: lista de estados nativa do " + tv.GetType().Name + " trocada por uma de 32 bits " + sz.Width + "x" + sz.Height + " (" + next + " Next, " + clean + " originais limpos)");
        }

        // Remove o "anel" claro: pixels de borda (vizinhos de transparente) quase brancos, que sao a cor do icone
        // misturada com fundo branco. Recalcula alfa e cor a partir da cor interna vizinha (de-matte contra branco).
        internal static Bitmap DeMatte(Image src, Size sz)
        {
            Bitmap b = new Bitmap(sz.Width, sz.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(b))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.PixelOffsetMode = PixelOffsetMode.HighQuality; g.CompositingMode = CompositingMode.SourceCopy;
                if (src.Width == sz.Width && src.Height == sz.Height) g.DrawImageUnscaled(src, 0, 0); else g.DrawImage(src, new Rectangle(0, 0, sz.Width, sz.Height));
            }
            int w = sz.Width, hgt = sz.Height;
            BitmapData bd = b.LockBits(new Rectangle(0, 0, w, hgt), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            int[] px = new int[w * hgt];
            Marshal.Copy(bd.Scan0, px, 0, px.Length);
            for (int pass = 0; pass < 2; pass++)
            {
                int[] o = (int[])px.Clone();
                for (int y = 0; y < hgt; y++)
                    for (int x = 0; x < w; x++)
                    {
                        int c = o[y * w + x], a = (c >> 24) & 255;
                        if (a == 0) continue;
                        int R = (c >> 16) & 255, G = (c >> 8) & 255, B = c & 255;
                        int mn = Math.Min(R, Math.Min(G, B)), mx = Math.Max(R, Math.Max(G, B));
                        if (mn < 170 || mx - mn > 60) continue;           // so pixels claros e pouco saturados
                        bool edge = false;
                        int sr = 0, sg = 0, sb = 0, cnt = 0;
                        for (int dy = -1; dy <= 1; dy++)
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                if (dx == 0 && dy == 0) continue;
                                int xx = x + dx, yy = y + dy;
                                if (xx < 0 || yy < 0 || xx >= w || yy >= hgt) { if (dx == 0 || dy == 0) edge = true; continue; }
                                int k = o[yy * w + xx], ka = (k >> 24) & 255;
                                if (ka == 0) { if (dx == 0 || dy == 0) edge = true; continue; }
                                int kr = (k >> 16) & 255, kg = (k >> 8) & 255, kb = k & 255;
                                int kmn = Math.Min(kr, Math.Min(kg, kb)), kmx = Math.Max(kr, Math.Max(kg, kb));
                                if (ka == 255 && (kmn < 170 || kmx - kmn > 60)) { sr += kr; sg += kg; sb += kb; cnt++; }
                            }
                        if (!edge || cnt == 0) continue;
                        int cr = sr / cnt, cg = sg / cnt, cb = sb / cnt;
                        // p = f*C + (1-f)*255  ->  f = (255-p)/(255-C), usa o canal com mais contraste
                        double f = -1;
                        int[] pc = { R, G, B }, cc = { cr, cg, cb };
                        int best = 0;
                        for (int i = 0; i < 3; i++) if (255 - cc[i] > best) { best = 255 - cc[i]; f = (255.0 - pc[i]) / (255.0 - cc[i]); }
                        if (best < 40 || f < 0) continue;
                        if (f > 1) f = 1;
                        int na = (int)Math.Round(a * f);
                        px[y * w + x] = (na << 24) | (cr << 16) | (cg << 8) | cb;
                    }
            }
            Marshal.Copy(px, 0, bd.Scan0, px.Length);
            b.UnlockBits(bd);
            return b;
        }

        // ================= icone de aba de parte "default" (DocumentPartFrame.RefreshIcon: Web Layout etc.) =================
        // O GeneXus monta em tempo de execucao Images.Default (raio 32 px) + DefaultEnabledOverlay/DefaultDisabledOverlay,
        // reduz com DrawIcon e guarda num ImageList de 8 bits (alfa vira mascara de 1 bit: borda serrilhada e franja clara).
        // Aqui: a aba recebe um ImageList proprio de 32 bits com o icone Mercury do estado atual
        // (patterns-default-associated: -synchronized = parte default, -modified = parte editada). A lista do GeneXus
        // (campo ImageList da pagina) nao e alterada; quando o RefreshIcon roda de novo (troca de estado) ele religa a
        // lista dele e a proxima passada troca de novo. RestoreAll devolve a lista original.
        sealed class PartRec { public ImageList Ours, Orig; public bool State; }
        static readonly Dictionary<object, PartRec> partPages = new Dictionary<object, PartRec>();
        sealed class PartType { public FieldInfo State, List; public PropertyInfo PList, PIndex; }
        static readonly Dictionary<Type, PartType> partTypes = new Dictionary<Type, PartType>();

        static PartType PartTypeOf(Type t)
        {
            PartType r;
            if (partTypes.TryGetValue(t, out r)) return r;
            r = null;
            FieldInfo st = null, li = null;
            for (Type b = t; b != null && st == null; b = b.BaseType)
                foreach (FieldInfo fi in b.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (fi.FieldType == typeof(bool?) && fi.Name.IndexOf("IsDefault", StringComparison.OrdinalIgnoreCase) >= 0) st = fi;
                    else if (fi.FieldType == typeof(ImageList)) li = fi;
                }
            if (st != null && li != null && st.DeclaringType == li.DeclaringType)
            {
                PropertyInfo pl = t.GetProperty("ImageList", typeof(ImageList)), px = t.GetProperty("ImageIndex", typeof(int));
                if (pl != null && pl.CanWrite && px != null && px.CanWrite) r = new PartType { State = st, List = li, PList = pl, PIndex = px };
            }
            partTypes[t] = r;
            if (r != null) DarkEngine.LogOnce("parttype:" + t.FullName, "Icone de parte default: tipo reconhecido " + t.FullName + " (" + st.Name + ", " + li.Name + ")");
            return r;
        }

        static void PartIcons(Control tabs)
        {
            PropertyInfo pp = tabs.GetType().GetProperty("TabPages");
            IEnumerable pages = pp == null ? null : pp.GetValue(tabs, null) as IEnumerable;
            if (pages == null) return;
            foreach (object pg in pages)
            {
                if (pg == null) continue;
                PartType pt = PartTypeOf(pg.GetType());
                if (pt == null) continue;
                bool? st = (bool?)pt.State.GetValue(pg);
                if (!st.HasValue) continue;                                  // parte sem provedor default: sem icone
                ImageList cur = pt.PList.GetValue(pg, null) as ImageList, gxl = pt.List.GetValue(pg) as ImageList;
                PartRec r;
                lock (sync) partPages.TryGetValue(pg, out r);
                if (r != null && cur == r.Ours && r.State == st.Value) continue;
                if (gxl == null || cur != gxl) continue;                      // lista que nao e a do RefreshIcon: nao mexe
                IconFamily f = FamByName(st.Value ? "patterns-default-associated/default-associated-synchronized" : "patterns-default-associated/default-associated-modified");
                if (f == null) return;
                Size sz = gxl.ImageSize;
                if (sz.Width <= 0) sz = new Size(DpiScale(16), DpiScale(16));
                Bitmap b = f.Get(0, sz.Width, sz.Height);
                if (b == null) continue;
                ImageList ours = new ImageList { ColorDepth = ColorDepth.Depth32Bit, ImageSize = sz };
                ours.Images.Add(b);
                pt.PList.SetValue(pg, ours, null);
                pt.PIndex.SetValue(pg, 0, null);
                lock (sync)
                {
                    if (r == null)
                    {
                        r = new PartRec(); partPages[pg] = r;
                        Control pc = pg as Control;
                        if (pc != null) pc.Disposed += (o, e) => { lock (sync) { PartRec x; if (partPages.TryGetValue(o, out x)) { partPages.Remove(o); if (x.Ours != null) x.Ours.Dispose(); } } };
                    }
                    ImageList old = r.Ours;
                    r.Ours = ours; r.Orig = gxl; r.State = st.Value;
                    if (old != null && old != ours) old.Dispose();
                }
                DarkEngine.LogOnce("particon:" + st.Value + ":" + sz.Width, "Icone de parte default: aba com icone Mercury " + f.Name + " " + sz.Width + "x" + sz.Height + " (lista de 32 bits)");
            }
        }

        static void Crownwood(Control c)
        {
            PropertyInfo pp = c.GetType().GetProperty("TabPages");
            IEnumerable pages = pp == null ? null : pp.GetValue(c, null) as IEnumerable;
            if (pages == null) return;
            foreach (object pg in pages)
            {
                if (pg == null || pageIcons.ContainsKey(pg)) continue;
                PropertyInfo pi = pg.GetType().GetProperty("Icon", typeof(Icon));
                if (pi != null && pi.CanWrite)
                {
                    Icon ic = pi.GetValue(pg, null) as Icon;
                    if (ic == null) continue;
                    IconFamily f;
                    using (Bitmap b = ic.ToBitmap()) f = Match(b);
                    if (f == null) continue;
                    pageIcons[pg] = new KeyValuePair<PropertyInfo, object>(pi, ic);
                    pi.SetValue(pg, f.GetIcon(ic.Size), null);
                    continue;
                }
                pi = pg.GetType().GetProperty("Image", typeof(Image));
                if (pi != null && pi.CanWrite)
                {
                    Image im = pi.GetValue(pg, null) as Image;
                    IconHit h = Lookup(im);
                    if (h == null || h.State != -1) continue;
                    pageIcons[pg] = new KeyValuePair<PropertyInfo, object>(pi, im);
                    pi.SetValue(pg, h.F.Get(0, im.Width, im.Height), null);
                }
            }
        }

        // ================= restaurar tudo (desliga a troca em tempo de execucao) =================
        public static int RestoreAll()
        {
            int n = 0;
            Active = false;
            lock (sync)
            {
                foreach (SeedRec s in seeds) { try { if (s.Orig != null) { s.Set.Invoke(s.Cache, new object[] { s.Key, s.Cul, s.Orig }); n++; } } catch { } }
                seeds.Clear();
                foreach (KeyValuePair<object, IUIElementDrawFilter> kv in filters)
                {
                    try
                    {
                        UltraControlBase uc = kv.Key as UltraControlBase;
                        UltraComponentControlManagerBase mg = kv.Key as UltraComponentControlManagerBase;
                        if (uc != null && !uc.IsDisposed && uc.DrawFilter is IconDrawFilter) { uc.DrawFilter = ((IconDrawFilter)uc.DrawFilter).Inner; n++; }
                        else if (mg != null && mg.DrawFilter is IconDrawFilter) { mg.DrawFilter = ((IconDrawFilter)mg.DrawFilter).Inner; n++; }
                    }
                    catch { }
                }
                filters.Clear();
                n += RestoreObjects();
                Reverse = false; revItems.Clear(); revLists.Clear(); revPages.Clear(); revSwapped.Clear();
            }
            DarkEngine.Log("Icones Next: originais restaurados (" + n + " itens).");
            return n;
        }

        static int nativeBack;
        // devolve a lista nativa original (copia exata feita antes da troca): sem requantizar as imagens de 8 bits
        static bool AdoptNative(ImageList il, IntPtr dup)
        {
            try
            {
                IntPtr cur = il.Handle;
                if (ImageList_GetImageCount(dup) != il.Images.Count) return false;
                FieldInfo nf = typeof(ImageList).GetField("nativeImageList", BindingFlags.Instance | BindingFlags.NonPublic);
                if (nf == null) return false;
                ConstructorInfo ci = nf.FieldType.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, new Type[] { typeof(IntPtr) }, null);
                if (ci == null) return false;
                object old = nf.GetValue(il);
                nf.SetValue(il, ci.Invoke(new object[] { dup }));
                IDisposable d = old as IDisposable;
                if (d != null) d.Dispose();
                MethodInfo rh = typeof(ImageList).GetMethod("OnRecreateHandle", BindingFlags.Instance | BindingFlags.NonPublic);
                if (rh != null) rh.Invoke(il, new object[] { EventArgs.Empty });
                return true;
            }
            catch (Exception ex) { DarkEngine.Log("ImageList nativa: " + ex.Message); return false; }
        }

        // objetos trocados (itens, ImageLists, paginas, StateImageList, partes); chamador segura o lock
        static int RestoreObjects()
        {
            int n = 0;
                foreach (KeyValuePair<ToolStripItem, Image> kv in items) { try { if (!kv.Key.IsDisposed) { kv.Key.Image = kv.Value; n++; } } catch { } }
                items.Clear();
                foreach (KeyValuePair<ImageList, ListRec> kv in lists)
                {
                    if (!kv.Value.Changed) continue;
                    try
                    {
                        ImageList il = kv.Key;
                        il.Images.Clear();
                        il.ColorDepth = kv.Value.Depth;
                        for (int i = 0; i < kv.Value.Imgs.Count; i++) { if (string.IsNullOrEmpty(kv.Value.Keys[i])) il.Images.Add(kv.Value.Imgs[i]); else il.Images.Add(kv.Value.Keys[i], kv.Value.Imgs[i]); }
                        IntPtr dup = kv.Value.Dup;
                        if (dup != IntPtr.Zero && AdoptNative(il, dup)) { kv.Value.Dup = IntPtr.Zero; dup = IntPtr.Zero; nativeBack++; }
                        if (dup != IntPtr.Zero)
                        {
                            int m = Math.Min(ImageList_GetImageCount(dup), il.Images.Count);
                            IntPtr h = il.Handle;
                            for (int i = 0; i < m; i++)
                            {
                                IntPtr hi = ImageList_GetIcon(dup, i, 0);
                                if (hi != IntPtr.Zero) { ImageList_ReplaceIcon(h, i, hi); DestroyIcon(hi); }
                            }
                        }
                        n++;
                    }
                    catch { }
                    finally { if (kv.Value.Dup != IntPtr.Zero) { try { ImageList_Destroy(kv.Value.Dup); } catch { } kv.Value.Dup = IntPtr.Zero; } }
                }
                lists.Clear();
                if (nativeBack > 0) { DarkEngine.Log("ImageLists de 8 bits devolvidas com a copia nativa original: " + nativeBack); nativeBack = 0; }
                foreach (KeyValuePair<object, KeyValuePair<PropertyInfo, object>> kv in pageIcons) { try { kv.Value.Key.SetValue(kv.Key, kv.Value.Value, null); n++; } catch { } }
                pageIcons.Clear();
                foreach (KeyValuePair<TreeView, StateRec> kv in stateLists)
                {
                    try
                    {
                        TreeView tv = kv.Key;
                        if (!tv.IsDisposed && tv.IsHandleCreated)
                        {
                            MethodInfo upd = typeof(TreeView).GetMethod("UpdateNativeStateImageList", BindingFlags.Instance | BindingFlags.NonPublic);
                            if (upd != null) upd.Invoke(tv, null); else SendMsg(tv.Handle, TVM_SETIMAGELIST, (IntPtr)TVSIL_STATE, kv.Value.Orig);
                            tv.Invalidate(); n++;
                        }
                        if (kv.Value.Ours != null) kv.Value.Ours.Dispose();
                    }
                    catch { }
                }
                stateLists.Clear();
                foreach (KeyValuePair<object, PartRec> kv in partPages)
                {
                    try
                    {
                        PartType pt = PartTypeOf(kv.Key.GetType());
                        Control pc = kv.Key as Control;
                        if (pt != null && (pc == null || !pc.IsDisposed) && pt.PList.GetValue(kv.Key, null) == kv.Value.Ours)
                        { pt.PList.SetValue(kv.Key, pt.List.GetValue(kv.Key) as ImageList, null); pt.PIndex.SetValue(kv.Key, 0, null); n++; }
                        if (kv.Value.Ours != null) kv.Value.Ours.Dispose();
                    }
                    catch { }
                }
                partPages.Clear();
            return n;
        }

        public static void Purge()
        {
            lock (sync)
            {
                PurgeReverse();
                List<ToolStripItem> dead = new List<ToolStripItem>();
                foreach (ToolStripItem it in items.Keys) if (it.IsDisposed) dead.Add(it);
                foreach (ToolStripItem it in dead) items.Remove(it);
                List<object> df = new List<object>();
                foreach (object o in filters.Keys) { Control c = o as Control; if (c != null && c.IsDisposed) df.Add(o); }
                foreach (object o in df) filters.Remove(o);
                formsScanned.RemoveWhere(delegate(Form f) { return f.IsDisposed; });
                swapped.Clear();
            }
        }
    }
}

namespace GxDarkTheme
{
    public static partial class IconSwap
    {
        // troca de tema (chamada com a IDE ja no modo claro): recolore os quadros no lugar e refaz os Icon semeados
        internal static void TrocarTema(Tema t)
        {
            int n = 0, ic = 0;
            lock (sync)
            {
                n = IconFamily.RetingirTodas(t);
                foreach (SeedRec s in seeds)
                {
                    try
                    {
                        Icon oi = s.Orig as Icon;
                        if (oi != null && s.Fam != null && s.Repl is Icon) { s.Repl = s.Fam.GetIcon(oi.Size); ic++; }
                    }
                    catch { }
                }
            }
            LimparMonos();
            LineGuard.LimparCache();
            DarkEngine.Log("Icones: " + n + " quadros recoloridos para o tema " + t.Nome + " (" + ic + " icones refeitos)");
        }
    }
}
