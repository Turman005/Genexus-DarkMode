// GxDarkTheme - VS Code Dark+ para a IDE do GeneXus 18
// Pacote de UI: carrega um style library Infragistics escuro e escurece controles WinForms restantes.
// Tudo protegido por try/catch: nunca deve derrubar a IDE.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using DevExpress.Utils;
using DevExpress.XtraTreeList;
using DevExpress.XtraTreeList.Nodes;
using DevExpress.XtraEditors.Controls;
using System.Runtime.CompilerServices;

[assembly: Artech.Architecture.Common.Packages.Package(typeof(GxDarkTheme.DarkThemePackage), IsUIPackage = true, Author = "Turman005 e itsFilipe", Description = "Tema escuro (GeneXus Next + 7 temas) para a IDE do GeneXus 18")]
[assembly: Artech.Architecture.Common.Packages.PackageCompatibility(Version = GxDarkTheme.Compat.Versao)]
[assembly: AssemblyTitle(GxDarkTheme.Produto.Nome)]
[assembly: AssemblyVersion("2.0.0.0")]
[assembly: AssemblyFileVersion("2.0.2.0")]
[assembly: AssemblyCompany("Turman005 e itsFilipe")]
[assembly: AssemblyCopyright("Copyright (c) 2026 Gabriel Turman (Turman005) e Filipe Castro (itsFilipe) - MIT")]

namespace GxDarkTheme
{
    [Guid(Produto.PacoteGuid)]
    public class DarkThemePackage : Artech.Architecture.UI.Framework.Packages.AbstractPackageUI
    {
        public override string Name { get { return Produto.Nome; } }

        public override void Initialize(Artech.Architecture.Common.Services.IGxServiceProvider services)
        {
            base.Initialize(services);
            try { DarkEngine.Start(); }
            catch (Exception ex) { DarkEngine.Log("Initialize falhou: " + ex); }
        }
    }

    public static partial class DarkEngine
    {
        // ---------- Paleta (GeneXus Next; outros temas = M(Next), ver Nucleo/Tema.cs) ----------
        public static Color Editor = Color.FromArgb(17, 21, 28);      // #11151C (GeneXus Next: editor/documento)
        public static Color Panel = Color.FromArgb(24, 31, 42);       // #181F2A (paineis, arvores, abas)
        public static Color Chrome = Color.FromArgb(36, 45, 60);      // #242D3C (titulo, menus, barras, status, faixas)
        public static Color Menu = Color.FromArgb(24, 31, 42);
        public static Color Input = Color.FromArgb(31, 39, 52);
        public static Color Hover = Color.FromArgb(52, 72, 96);
        public static Color Button = Color.FromArgb(44, 60, 80);
        public static Color Border = Color.FromArgb(51, 61, 71);      // #333D47
        public static Color Accent = Color.FromArgb(44, 141, 206);    // #2C8DCE
        public static Color Sel = Color.FromArgb(44, 60, 80);         // #2C3C50 (linha selecionada)
        public static Color Fg = Color.FromArgb(220, 226, 235);
        public static Color FgBright = Color.FromArgb(240, 244, 250); // #F0F4FA
        public static Color FgDim = Color.FromArgb(138, 144, 155);
        public static Color FgSoft = Color.FromArgb(190, 198, 210);  // #BEC6D2 abas inativas / textos secundarios (contraste ~8:1)
        public static Color FgDisabled = Color.FromArgb(150, 157, 168); // #969DA8 textos desabilitados (~4:1)
        public static Color Link = Color.FromArgb(91, 167, 255);      // #5BA7FF
        public static Color HotBg = Color.FromArgb(52, 72, 96);       // #344860 hover (barras, abas, listas)
        public static Color HotBorder = Color.FromArgb(78, 104, 136); // #4E6888 borda do hover / menu aberto
        public static Color MenuHot = Color.FromArgb(31, 78, 121);    // #1F4E79 item de menu sob o mouse / pressionado

        // valores Next de referencia (os campos acima recebem M(base) na troca de tema)
        static readonly Color[] paletaNext = { Editor, Panel, Chrome, Menu, Input, Hover, Button, Border, Accent, Sel, Fg, FgBright, FgDim, FgSoft, FgDisabled, Link, HotBg, HotBorder, MenuHot };
        public static Tema TemaAtual = Temas.Next;

        // cor de saida do tema atual a partir da cor do GeneXus Next (Next: identidade)
        public static Color T(int r, int g, int b) { return TemaAtual.M(Color.FromArgb(r, g, b)); }
        public static Color T(Color c) { return TemaAtual.M(c); }
        public static string TH(string css) { return TemaAtual.MapCss(css); }

        static void AplicarPaleta()
        {
            Color[] p = new Color[paletaNext.Length];
            for (int i = 0; i < p.Length; i++) p[i] = TemaAtual.M(paletaNext[i]);
            Editor = p[0]; Panel = p[1]; Chrome = p[2]; Menu = p[3]; Input = p[4]; Hover = p[5]; Button = p[6]; Border = p[7]; Accent = p[8]; Sel = p[9];
            Fg = p[10]; FgBright = p[11]; FgDim = p[12]; FgSoft = p[13]; FgDisabled = p[14]; Link = p[15]; HotBg = p[16]; HotBorder = p[17]; MenuHot = p[18];
            darkJsCache = null;
        }

        static bool started;
        static Timer timer;
        static readonly HashSet<IntPtr> darkTitled = new HashSet<IntPtr>();
        static readonly Dictionary<Control, bool> seen = new Dictionary<Control, bool>();
        static readonly HashSet<string> stillLight = new HashSet<string>();
        static readonly HashSet<string> typesSeen = new HashSet<string>();
        static readonly Dictionary<Type, PropertyInfo[]> colorProps = new Dictionary<Type, PropertyInfo[]>();
        static readonly Dictionary<object, string> browserUrls = new Dictionary<object, string>();
        static readonly HashSet<object> browserHooked = new HashSet<object>();
        static int themedCount, passCount;
        static string logPath;
        static readonly object logLock = new object();
        static bool inPass;

        public static void Log(string msg)
        {
            try
            {
                lock (logLock)
                {
                    if (logPath == null) logPath = Path.Combine(Path.GetTempPath(), "GxDarkTheme.log");
                    File.AppendAllText(logPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "  " + msg + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch { }
        }

        static readonly HashSet<string> loggedOnce = new HashSet<string>();
        internal static void LogOnce(string key, string msg)
        {
            lock (loggedOnce) { if (!loggedOnce.Add(key)) return; }
            Log(msg);
        }

        // dialogos de erro/excecao/feedback: nunca recolorir
        static bool IsExcludedForm(Form f)
        {
            string n = f.GetType().FullName ?? "";
            return n == "Artech.Core.UI.Dialogs.FeedbackDialog" || f is ThreadExceptionDialog ||
                   n.IndexOf("Feedback", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Exception", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   n.IndexOf("ErrorDialog", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("ErrorReport", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   n.IndexOf("Crash", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static bool IsExcludedControl(Control c)
        {
            try
            {
                if (c.InvokeRequired) return true;
                Form f = c as Form ?? c.FindForm();
                return f != null && IsExcludedForm(f);
            }
            catch { return true; }
        }

        public static void Start()
        {
            if (started) return;
            started = true;
            try { if (Environment.GetEnvironmentVariable("GXDARK_DISABLE") == "1") { Log("Desativado por GXDARK_DISABLE=1"); return; } } catch { }
            try
            {
                logPath = Path.Combine(Path.GetTempPath(), "GxDarkTheme.log");
                if (File.Exists(logPath) && new FileInfo(logPath).Length > 2000000) File.Delete(logPath);
            }
            catch { }
            Log("==== GxDarkTheme iniciando (processo " + System.Diagnostics.Process.GetCurrentProcess().ProcessName + ", " + Environment.OSVersion + ") ====");
            Safe("Migracao Xpz.Dark", MigrarXpzDark);
            if (!SettingsEnabled()) { Log("Desativado pelo arquivo " + SettingsPath() + " (enabled=false)"); return; }
            if (ModoSeguro()) return;
            Safe("Tema", CarregarTema);
            Safe("CheckLegacy", CheckLegacyConfig);
            StartToggle();   // modo escuro ou claro (mode= no arquivo), atalho Ctrl+Shift+F12
            Safe("Timer", delegate
            {
                timer = new Timer();
                timer.Interval = 700;
                timer.Tick += delegate { Pass(); };
                timer.Start();
            });
            Safe("Idle", delegate { Application.Idle += OnIdle; });
            Safe("Menu Tema escuro", MenuTema.Iniciar);
        }

        static int idleTicks;
        static void OnIdle(object s, EventArgs e)
        {
            // primeiras passadas rapidas para evitar "flash" branco na abertura
            if (idleTicks++ < 40) Pass();
            else Application.Idle -= OnIdle;
        }

        delegate void Act();
        static void Safe(string what, Act a)
        {
            try { a(); }
            catch (Exception ex) { Log("ERRO em " + what + ": " + ex.GetType().Name + ": " + ex.Message); }
        }

        // ---------- Liga/desliga sem desinstalar ----------
        // %APPDATA%\GeneXus\GeneXus\18\GxDarkTheme.settings com a linha "enabled=false" -> a extensao nao faz nada
        // (o GeneXus abre exatamente como sem a extensao). Sem arquivo ou "enabled=true" -> tema ligado.
        public static string SettingsPath()
        {
            try { string ov = Environment.GetEnvironmentVariable("GXDARK_SETTINGS"); if (!string.IsNullOrEmpty(ov)) return ov; } catch { }   // so para testes
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"GeneXus\GeneXus\18\GxDarkTheme.settings");
        }

        public static bool SettingsEnabled()
        {
            try
            {
                string p = SettingsPath();
                if (!File.Exists(p)) return true;
                foreach (string raw in File.ReadAllLines(p))
                {
                    string l = raw.Replace(" ", "").Trim().ToLowerInvariant();
                    if (l == "enabled=false" || l == "enabled=0" || l == "off") return false;
                }
            }
            catch { }
            return true;
        }

        // linha "icons=false" no mesmo arquivo: mantem o tema escuro mas usa os icones originais do GeneXus
        public static bool SettingsIcons()
        {
            try
            {
                string p = SettingsPath();
                if (!File.Exists(p)) return true;
                foreach (string raw in File.ReadAllLines(p))
                {
                    string l = raw.Replace(" ", "").Trim().ToLowerInvariant();
                    if (l == "icons=false" || l == "icons=0" || l == "icones=false") return false;
                }
            }
            catch { }
            return true;
        }

        // O tema NAO grava nada no TextEditorConfig_Default.xml (as cores do editor sao aplicadas so em memoria).
        // Se o arquivo ainda tiver as cores escuras da versao antiga do Ativar, so registra no log (Ativar/Desativar restauram).
        static void CheckLegacyConfig()
        {
            string cfg = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"GeneXus\GeneXus\18\TextEditorConfig_Default.xml");
            if (!File.Exists(cfg)) return;
            string x = File.ReadAllText(cfg);
            if (x.IndexOf("<ForeColor>-11100970</ForeColor>", StringComparison.Ordinal) >= 0)
                Log("AVISO: TextEditorConfig_Default.xml ainda tem as cores escuras gravadas pela versao antiga do Ativar. Rode o Ativar novo (ou o Desativar) com o GeneXus fechado para restaurar o original.");
        }

        // ---------- Infragistics ----------
        public static bool LoadIsl(string path)
        {
            XmlDocument doc;
            if (path == null)
            {
                // v2: o estilo escuro e gerado em memoria a partir do GenexusOffice2010Silver.isl do proprio GeneXus
                // (nenhum arquivo .isl e gravado). GXDARK_ISL aponta para um .isl pronto (testes/diagnostico).
                string ov = null;
                try { ov = Environment.GetEnvironmentVariable("GXDARK_ISL"); } catch { }
                if (!string.IsNullOrEmpty(ov) && File.Exists(ov)) { path = ov; doc = new XmlDocument(); doc.Load(path); }
                else
                {
                    string silver = LightIslPath();
                    if (silver == null) { Log("ISL: GenexusOffice2010Silver.isl nao encontrado na pasta do GeneXus; menus/barras ficam no estilo original"); return false; }
                    if (GeradorIsl.RolesConhecidos == null)
                    {
                        HashSet<string> roles = new HashSet<string>(StringComparer.Ordinal);
                        using (Stream rs = typeof(DarkEngine).Assembly.GetManifestResourceStream("GxDarkTheme.roles.txt"))
                        using (StreamReader rr = new StreamReader(rs)) { string l; while ((l = rr.ReadLine()) != null) { l = l.Trim(); if (l.Length > 0) roles.Add(l); } }
                        GeradorIsl.RolesConhecidos = roles;
                    }
                    System.Diagnostics.Stopwatch gsw = System.Diagnostics.Stopwatch.StartNew();
                    doc = GeradorIsl.Gerar(File.ReadAllText(silver), TemaAtual);
                    path = "(memoria, tema " + TemaAtual.Nome + ", " + GeradorIsl.Adicionados + " papeis adicionados, " + gsw.ElapsedMilliseconds + " ms)";
                }
            }
            else
            {
                if (!File.Exists(path)) { Log("ISL nao encontrado: " + path); return false; }
                doc = new XmlDocument();
                doc.Load(path);
            }
            // mesmo ajuste de DPI que o GeneXus faz no ISL original (tamanho minimo do thumb da scrollbar)
            int scaled = 16;
            try { using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) scaled = (int)Math.Round(16 * g.DpiY / 96f); } catch { }
            foreach (XmlNode n in doc.SelectNodes("//sharedObject[@name='ScrollBar']/properties/property"))
            {
                int dummy;
                if (n.FirstChild != null && int.TryParse(n.FirstChild.Value, out dummy)) n.FirstChild.Value = scaled.ToString();
            }
            try { InjectCheckGlyph(doc); } catch (Exception ex) { Log("Caixa de selecao escura (glyph): " + ex.GetType().Name + ": " + ex.Message); }
            try { IslPopups(doc); } catch (Exception ex) { Log("Popup de personalizacao (ISL): " + ex.GetType().Name + ": " + ex.Message); }
            try { IslContrast(doc); } catch (Exception ex) { Log("Contraste do ISL: " + ex.GetType().Name + ": " + ex.Message); }
            using (MemoryStream ms = new MemoryStream())
            {
                doc.Save(ms);
                ms.Position = 0;
                Infragistics.Win.AppStyling.StyleManager.Load(ms);
            }
            try { Infragistics.Win.Office2007ColorTable.ColorScheme = Infragistics.Win.Office2007ColorScheme.Black; } catch (Exception ex) { Log("Office2007 scheme: " + ex.Message); }
            try { Infragistics.Win.Office2010ColorTable.ColorScheme = Infragistics.Win.Office2010ColorScheme.Black; } catch (Exception ex) { Log("Office2010 scheme: " + ex.Message); }
            PatchIgColorTables();
            Log("ISL carregado OK: " + path);
            return true;
        }

        // ---------- Caixas de selecao (checkbox) das grades/listas Infragistics ----------
        // O glyph padrao e desenhado claro (quadrado branco). Gera um glyph escuro em memoria e o injeta no ISL
        // antes de carregar (nao altera nenhum arquivo).
        static readonly string[] CheckGlyphComponents = { "UltraGrid", "UltraCombo", "UltraDropDown", "UltraGridColumnChooser", "UltraTree",
            "UltraListView", "UltraCheckEditor", "UltraTimeZoneEditor", "UltraExplorerBar", "UltraWinTree" };

        static void InjectCheckGlyph(XmlDocument doc)
        {
            string ser = CheckGlyphString();
            if (ser == null) return;
            int n = 0;
            foreach (XmlNode set in doc.SelectNodes("//styleSet"))
            {
                XmlNode cs = set.SelectSingleNode("componentStyles");
                if (cs == null) continue;
                bool isBase = ((XmlElement)set).GetAttribute("basedOn").Length == 0;
                foreach (string name in CheckGlyphComponents)
                {
                    XmlElement comp = (XmlElement)cs.SelectSingleNode("componentStyle[@name='" + name + "']");
                    if (comp == null)
                    {
                        if (!isBase) continue;
                        comp = doc.CreateElement("componentStyle");
                        comp.SetAttribute("name", name);
                        comp.SetAttribute("useOsThemes", "False");
                        comp.SetAttribute("resolutionOrder", "ApplicationThenControl");
                        cs.AppendChild(comp);
                    }
                    XmlNode old = comp.SelectSingleNode("checkBoxGlyphInfo");
                    if (old != null) comp.RemoveChild(old);
                    XmlElement g = doc.CreateElement("checkBoxGlyphInfo");
                    g.InnerText = ser;
                    comp.InsertBefore(g, comp.FirstChild);
                    n++;
                }
            }
            Log("Caixas de selecao escuras aplicadas em " + n + " componentes Infragistics");
        }

        public static string CheckGlyphString()
        {
            Size sz = new Size(13, 13);
            Color boxBg = Panel, boxBd = DarkEngine.T(90, 101, 119);            // #181F2A / #5A6577
            Color hotBg = Input, hotBd = Link;                                     // hover: borda azul clara
            Color prBg = MenuHot, prBd = Link;                                     // pressionado
            Color disBg = DarkEngine.T(26, 33, 44), disBd = DarkEngine.T(58, 68, 82);
            Color chk = Color.White, chkFill = Accent, chkFillHot = DarkEngine.T(58, 155, 220), chkDis = FgDim;
            Bitmap[] imgs = new Bitmap[]
            {
                Glyph(sz, disBg, disBd, 0, chkDis), Glyph(sz, DarkEngine.T(46, 58, 74), disBd, 1, chkDis), Glyph(sz, disBg, disBd, 2, DarkEngine.T(74, 85, 104)),
                Glyph(sz, boxBg, boxBd, 0, chk), Glyph(sz, chkFill, chkFill, 1, chk), Glyph(sz, boxBg, boxBd, 2, Link),
                Glyph(sz, hotBg, hotBd, 0, chk), Glyph(sz, chkFillHot, hotBd, 1, chk), Glyph(sz, hotBg, hotBd, 2, Link),
                Glyph(sz, prBg, prBd, 0, chk), Glyph(sz, prBg, prBd, 1, chk), Glyph(sz, prBg, prBd, 2, Link),
                Glyph(sz, prBg, prBd, 0, chk), Glyph(sz, prBg, prBd, 1, chk), Glyph(sz, prBg, prBd, 2, Link),
            };
            Infragistics.Win.CheckBoxImageGlyphInfo gi = new Infragistics.Win.CheckBoxImageGlyphInfo(sz,
                imgs[0], imgs[1], imgs[2], imgs[3], imgs[4], imgs[5], imgs[6], imgs[7], imgs[8], imgs[9], imgs[10], imgs[11], imgs[12], imgs[13], imgs[14],
                "GxDarkTheme check box glyphs");
            Type su = typeof(Infragistics.Win.Appearance).Assembly.GetType("Infragistics.Win.AppStyling.StyleUtilities");
            MethodInfo mi = su == null ? null : su.GetMethod("ConvertGlyphInfoToString", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (mi == null) { Log("Glyph: StyleUtilities.ConvertGlyphInfoToString nao encontrado"); return null; }
            return (string)mi.Invoke(null, new object[] { gi });
        }

        // CheckBox WinForms: o Windows desenha o quadradinho branco; pinta por cima um quadradinho escuro do mesmo tamanho.
        static void OnCheckBoxPaint(object sender, PaintEventArgs e)
        { if (!On) return;
            try
            {
                CheckBox cb = sender as CheckBox;
                if (cb == null || cb.Appearance != Appearance.Normal || cb.RightToLeft == RightToLeft.Yes) return;
                ContentAlignment a = cb.CheckAlign;
                if (a != ContentAlignment.MiddleLeft && a != ContentAlignment.TopLeft && a != ContentAlignment.BottomLeft) return;
                if (cb.FlatStyle == FlatStyle.System) return;
                Size gs;
                try { gs = System.Windows.Forms.CheckBoxRenderer.GetGlyphSize(e.Graphics, System.Windows.Forms.VisualStyles.CheckBoxState.UncheckedNormal); }
                catch { gs = new Size(13, 13); }
                if (gs.Width < 10 || gs.Width > 40) gs = new Size(13, 13);
                int pad = cb.Padding.Left;
                int y;
                if (a == ContentAlignment.TopLeft) y = cb.Padding.Top + 1;
                else if (a == ContentAlignment.BottomLeft) y = cb.ClientSize.Height - cb.Padding.Bottom - gs.Height - 1;
                else y = (cb.ClientSize.Height - gs.Height) / 2;
                if (cb.FlatStyle != FlatStyle.Standard) return; // Flat/Popup ja usam BackColor
                Point mp = cb.PointToClient(Control.MousePosition);
                bool hot = cb.ClientRectangle.Contains(mp);
                int kind = cb.CheckState == CheckState.Checked ? 1 : cb.CheckState == CheckState.Indeterminate ? 2 : 0;
                Color bg, bd, mark;
                if (!cb.Enabled) { bg = kind == 1 ? DarkEngine.T(46, 58, 74) : DarkEngine.T(26, 33, 44); bd = DarkEngine.T(58, 68, 82); mark = kind == 2 ? DarkEngine.T(74, 85, 104) : FgDim; }
                else if (kind == 1) { bg = hot ? DarkEngine.T(58, 155, 220) : Accent; bd = hot ? Link : Accent; mark = Color.White; }
                else { bg = hot ? Input : Panel; bd = hot ? Link : DarkEngine.T(90, 101, 119); mark = Link; }
                using (Bitmap b = Glyph(gs, bg, bd, kind, mark))
                    e.Graphics.DrawImageUnscaled(b, pad, y);
            }
            catch { }
        }


        // ---------- Textos desabilitados / GroupBox / paineis web ----------
        static Color DisBtnBg { get { return DarkEngine.T(40, 48, 60); } }
        static Color DisBtnBd { get { return DarkEngine.T(58, 68, 82); } }

        static TextFormatFlags AlignFlags(ContentAlignment a)
        {
            TextFormatFlags f = 0;
            switch (a)
            {
                case ContentAlignment.TopLeft: case ContentAlignment.TopCenter: case ContentAlignment.TopRight: f |= TextFormatFlags.Top; break;
                case ContentAlignment.BottomLeft: case ContentAlignment.BottomCenter: case ContentAlignment.BottomRight: f |= TextFormatFlags.Bottom; break;
                default: f |= TextFormatFlags.VerticalCenter; break;
            }
            switch (a)
            {
                case ContentAlignment.TopCenter: case ContentAlignment.MiddleCenter: case ContentAlignment.BottomCenter: f |= TextFormatFlags.HorizontalCenter; break;
                case ContentAlignment.TopRight: case ContentAlignment.MiddleRight: case ContentAlignment.BottomRight: f |= TextFormatFlags.Right; break;
                default: f |= TextFormatFlags.Left; break;
            }
            return f;
        }

        // Botao desabilitado: o WinForms usa ControlPaint.Dark(BackColor) -> texto quase invisivel. Repinta fundo, borda e texto (#969DA8).
        static void OnDisabledButtonPaint(object sender, PaintEventArgs e)
        { if (!On) return;
            try
            {
                Button b = sender as Button;
                if (b == null || b.Enabled || b.Image != null || b.BackgroundImage != null || b.FlatStyle == FlatStyle.System) return;
                if (b.RightToLeft == RightToLeft.Yes) return;
                Rectangle r = b.ClientRectangle;
                using (SolidBrush br = new SolidBrush(DisBtnBg)) e.Graphics.FillRectangle(br, r);
                using (Pen p = new Pen(DisBtnBd)) e.Graphics.DrawRectangle(p, 0, 0, r.Width - 1, r.Height - 1);
                Rectangle tr = Rectangle.FromLTRB(r.Left + b.Padding.Left + 3, r.Top + b.Padding.Top + 2, r.Right - b.Padding.Right - 3, r.Bottom - b.Padding.Bottom - 2);
                TextFormatFlags fl = AlignFlags(b.TextAlign) | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | (b.UseMnemonic ? TextFormatFlags.HidePrefix : TextFormatFlags.NoPrefix);
                TextRenderer.DrawText(e.Graphics, b.Text ?? "", b.Font, tr, FgDisabled, fl);
            }
            catch { }
        }

        // Label desabilitado: mesmo problema (ControlPaint.DrawStringDisabled). So quando o fundo e opaco e nao ha imagem.
        static void OnDisabledLabelPaint(object sender, PaintEventArgs e)
        { if (!On) return;
            try
            {
                Label l = sender as Label;
                if (l == null || l.Enabled || l.Image != null || l.BackgroundImage != null || string.IsNullOrEmpty(l.Text)) return;
                if (l.FlatStyle == FlatStyle.System || l.RightToLeft == RightToLeft.Yes) return;
                Color bg = l.BackColor;
                if (bg.A < 255) { Control p = l.Parent; if (p == null || p.BackColor.A < 255 || p.BackgroundImage != null) return; bg = p.BackColor; }
                if (IsLight(bg)) return;
                Rectangle r = l.ClientRectangle;
                using (SolidBrush br = new SolidBrush(bg)) e.Graphics.FillRectangle(br, r);
                Rectangle tr = Rectangle.FromLTRB(r.Left + l.Padding.Left, r.Top + l.Padding.Top, r.Right - l.Padding.Right, r.Bottom - l.Padding.Bottom);
                TextFormatFlags fl = AlignFlags(l.TextAlign) | (l.AutoEllipsis ? TextFormatFlags.EndEllipsis : TextFormatFlags.WordBreak) | (l.UseMnemonic ? TextFormatFlags.HidePrefix : TextFormatFlags.NoPrefix);
                TextRenderer.DrawText(e.Graphics, l.Text, l.Font, tr, FgDisabled, fl);
            }
            catch { }
        }

        // GroupBox: o tema do Windows desenha a moldura clara; troca por #333D47 e redesenha o titulo.
        static void OnGroupBoxPaint(object sender, PaintEventArgs e)
        { if (!On) return;
            try
            {
                GroupBox gb = sender as GroupBox;
                if (gb == null || gb.FlatStyle == FlatStyle.System || gb.RightToLeft == RightToLeft.Yes || gb.BackgroundImage != null) return;
                Color bg = gb.BackColor;
                if (bg.A < 255) { Control p = gb.Parent; bg = p != null ? p.BackColor : Panel; }
                if (bg.A < 255 || IsLight(bg)) return;
                Graphics g = e.Graphics;
                int w = gb.Width, hgt = gb.Height;
                int fh = gb.Font.Height, top = fh / 2;
                using (SolidBrush br = new SolidBrush(bg))
                {
                    g.FillRectangle(br, 0, Math.Max(0, top - 2), w, 5);       // linha de cima
                    g.FillRectangle(br, 0, top, 3, hgt - top);                // esquerda
                    g.FillRectangle(br, w - 3, top, 3, hgt - top);            // direita
                    g.FillRectangle(br, 0, hgt - 3, w, 3);                    // baixo
                }
                using (Pen p = new Pen(Border)) g.DrawRectangle(p, 0, top, w - 1, hgt - top - 1);
                string txt = gb.Text;
                if (!string.IsNullOrEmpty(txt))
                {
                    TextFormatFlags fl = TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.SingleLine | TextFormatFlags.HidePrefix;
                    Size ts = TextRenderer.MeasureText(g, txt, gb.Font, new Size(Math.Max(1, w - 16), fh), fl);
                    using (SolidBrush br = new SolidBrush(bg)) g.FillRectangle(br, 4, 0, Math.Min(w - 8, ts.Width + 8), fh + 1);
                    Color fc = gb.Enabled ? (IsDarkText(gb.ForeColor) ? Fg : gb.ForeColor) : FgDisabled;
                    TextRenderer.DrawText(g, txt, gb.Font, new Rectangle(8, 0, Math.Min(w - 16, ts.Width + 2), fh + 1), fc, fl);
                }
            }
            catch { }
        }

        // Paineis que hospedam navegador (Output/Monaco, GxWebControl): fundo escuro sem "flash" branco.
        static readonly HashSet<Control> paintedPanes = new HashSet<Control>();

        static bool IsWebPaneType(Type t)
        {
            for (Type b = t; b != null && b != typeof(Control); b = b.BaseType)
            {
                string n = b.FullName ?? "";
                if (n.EndsWith(".MonacoPane", StringComparison.Ordinal) || n.EndsWith("MonacoOutputControl", StringComparison.Ordinal) || n.EndsWith(".GxWebControl", StringComparison.Ordinal)) return true;
            }
            return false;
        }

        static void WebPane(Control c)
        {
            if ((paintedPanes.Contains(c) && paneDark.Contains(c)) || !IsWebPaneType(c.GetType())) return;
            HookPanePaint(c);
        }

        static void HookPanePaint(Control c)
        {
            if (paneDark.Add(c)) { try { Journal.Snap(c); if (IsLight(c.BackColor)) c.BackColor = Editor; } catch { } }
            if (!paintedPanes.Add(c)) return;
            try
            {
                c.Paint += OnPanePaint;
                c.Disposed += delegate { paintedPanes.Remove(c); };
                c.Invalidate();
                LogOnce("pane:" + c.GetType().FullName, "Fundo escuro pintado em " + c.GetType().FullName);
            }
            catch { }
        }

        static void OnPanePaint(object sender, PaintEventArgs e)
        { if (!On) return;
            try
            {
                Control c = sender as Control;
                if (c == null || !IsLight(c.BackColor)) return; // BackColor ja escuro: nada a fazer
                using (SolidBrush br = new SolidBrush(Editor)) e.Graphics.FillRectangle(br, c.ClientRectangle);
            }
            catch { }
        }

        static bool UnderOutputPane(Control c)
        {
            for (Control p = c.Parent; p != null; p = p.Parent)
            {
                string n = p.GetType().FullName ?? "";
                if (n.IndexOf("Monaco", StringComparison.Ordinal) >= 0 || n.IndexOf("Output", StringComparison.Ordinal) >= 0) return true;
            }
            return false;
        }

        // CefSharp: cor de fundo do navegador (antes de ele ser criado) + fundo do controle WinForms
        static void CefBackground(Control c, Type t)
        {
            HookPanePaint(c);
            try
            {
                bool init = false;
                try { init = (bool)t.GetProperty("IsBrowserInitialized").GetValue(c, null); } catch { }
                if (init || !UnderOutputPane(c)) return;
                PropertyInfo ps = t.GetProperty("BrowserSettings");
                object bs = ps == null ? null : ps.GetValue(c, null);
                if (bs == null) return;
                PropertyInfo bc = bs.GetType().GetProperty("BackgroundColor");
                if (bc == null || !bc.CanWrite) return;
                object cur = bc.GetValue(bs, null);
                if (cur is uint && (uint)cur != 0) return; // o GeneXus ja definiu uma cor
                bc.SetValue(bs, (uint)0xFF11151C, null);
                Log("Navegador do Output: fundo escuro definido antes da criacao");
            }
            catch (Exception ex) { LogOnce("cefbg", "Cef fundo: " + ex.Message); }
        }

        // kind: 0 = desmarcado, 1 = marcado, 2 = indeterminado
        internal static Bitmap Glyph(Size sz, Color bg, Color bd, int kind, Color mark)
        {
            Bitmap b = new Bitmap(sz.Width, sz.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(b))
            {
                g.Clear(Color.Transparent);
                using (SolidBrush br = new SolidBrush(bg)) g.FillRectangle(br, 0, 0, sz.Width, sz.Height);
                using (Pen p = new Pen(bd)) g.DrawRectangle(p, 0, 0, sz.Width - 1, sz.Height - 1);
                if (kind == 1)
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    using (Pen p = new Pen(mark, 1.8f * sz.Width / 13f))
                    {
                        p.StartCap = System.Drawing.Drawing2D.LineCap.Round; p.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                        p.LineJoin = System.Drawing.Drawing2D.LineJoin.Round;
                        float k = sz.Width / 13f;
                        g.DrawLines(p, new PointF[] { new PointF(3f * k, 6.5f * k), new PointF(5.5f * k, 9f * k), new PointF(10f * k, 3.5f * k) });
                    }
                }
                else if (kind == 2)
                {
                    int m = Math.Max(3, sz.Width / 4);
                    using (SolidBrush br = new SolidBrush(mark)) g.FillRectangle(br, m, m, sz.Width - 2 * m, sz.Height - 2 * m);
                }
            }
            return b;
        }

        // ---------- Win32 ----------
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] static extern int SetWindowTheme(IntPtr hwnd, string app, string idList);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern IntPtr LoadLibrary(string name);
        [DllImport("kernel32.dll")] static extern IntPtr GetProcAddress(IntPtr h, IntPtr ordinal);
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] static extern bool RedrawWindow(IntPtr h, IntPtr r, IntPtr rgn, int flags);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate int SetPreferredAppModeFn(int mode);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate void VoidFn();
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate bool AllowDarkFn(IntPtr hwnd, bool allow);
        static AllowDarkFn allowDarkForWindow;

        static void EnableAppDarkMode()
        {
            if (Environment.OSVersion.Version.Major < 10) return;
            IntPtr ux = LoadLibrary("uxtheme.dll");
            if (ux == IntPtr.Zero) return;
            IntPtr p135 = GetProcAddress(ux, (IntPtr)135);
            IntPtr p104 = GetProcAddress(ux, (IntPtr)104);
            IntPtr p133 = GetProcAddress(ux, (IntPtr)133);
            IntPtr p136 = GetProcAddress(ux, (IntPtr)136);
            if (p133 != IntPtr.Zero) allowDarkForWindow = (AllowDarkFn)Marshal.GetDelegateForFunctionPointer(p133, typeof(AllowDarkFn));
            if (p135 != IntPtr.Zero) ((SetPreferredAppModeFn)Marshal.GetDelegateForFunctionPointer(p135, typeof(SetPreferredAppModeFn)))(2);
            if (p104 != IntPtr.Zero) ((VoidFn)Marshal.GetDelegateForFunctionPointer(p104, typeof(VoidFn)))();
            if (p136 != IntPtr.Zero) ((VoidFn)Marshal.GetDelegateForFunctionPointer(p136, typeof(VoidFn)))();
            Log("Modo escuro do processo (menus nativos/scrollbars) ativado");
        }

        static void DarkTitle(IntPtr h)
        {
            if (h == IntPtr.Zero || darkTitled.Contains(h)) return;
            darkTitled.Add(h);
            try
            {
                int on = 1;
                if (DwmSetWindowAttribute(h, 20, ref on, 4) != 0) DwmSetWindowAttribute(h, 19, ref on, 4);
                int caption = Chrome.R | (Chrome.G << 8) | (Chrome.B << 16); // COLORREF 0x00BBGGRR (Next: #242D3C)
                DwmSetWindowAttribute(h, 35, ref caption, 4);
                int text = FgBright.R | (FgBright.G << 8) | (FgBright.B << 16);
                DwmSetWindowAttribute(h, 36, ref text, 4);
            }
            catch { }
        }

        static void DarkScroll(Control c, string theme)
        {
            try
            {
                if (!c.IsHandleCreated) return;
                if (allowDarkForWindow != null) allowDarkForWindow(c.Handle, true);
                ListView lv = c as ListView;
                // ListView: corpo com DarkMode_Explorer (barras de rolagem escuras), cabecalho DarkMode_ItemsView
                SetWindowTheme(c.Handle, theme, null);
                TrackThemed(c, IntPtr.Zero);
                if (lv != null)
                {
                    IntPtr hdr = SendMessage(lv.Handle, 0x101F /*LVM_GETHEADER*/, IntPtr.Zero, IntPtr.Zero);
                    if (hdr != IntPtr.Zero) { if (allowDarkForWindow != null) allowDarkForWindow(hdr, true); SetWindowTheme(hdr, "DarkMode_ItemsView", null); TrackThemed(c, hdr); }
                    try { SetWindowPos(c.Handle, IntPtr.Zero, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0004 | 0x0010 | 0x0020); } catch { } // SWP_FRAMECHANGED
                }
            }
            catch { }
        }

        // ---------- Cores ----------
        public static double Lum(Color c) { return 0.299 * c.R + 0.587 * c.G + 0.114 * c.B; }
        public static bool IsLight(Color c) { return !c.IsEmpty && c.A > 0 && c != Color.Transparent && Lum(c) > 160; }
        public static bool IsDarkText(Color c) { return !c.IsEmpty && c.A > 0 && Lum(c) < 115; }

        // ---------- Passada pelos formularios ----------
        static void Pass()
        {
            if (inPass) return;
            try { ToggleButton.Ensure(); Housekeeping(); } catch { }
            if (!On) { LightPass(false); return; }
            inPass = true;
            int before = themedCount;
            try
            {
                passCount++;
                List<Form> forms = new List<Form>();
                foreach (Form f in Application.OpenForms) forms.Add(f);
                foreach (Form f in forms)
                {
                    try
                    {
                        if (f.IsDisposed || !f.IsHandleCreated) continue;
                        // nunca tocar em formularios de outra thread (ex.: dialogo de erro/feedback do GeneXus)
                        if (f.InvokeRequired) { LogOnce("thread:" + f.GetType().FullName, "Ignorado (outra thread): " + f.GetType().FullName); continue; }
                        if (IsExcludedForm(f)) { LogOnce("excl:" + f.GetType().FullName, "Ignorado (dialogo de erro/feedback): " + f.GetType().FullName); continue; }
                        DarkTitle(f.Handle);
                        Walk(f, 0);
                    }
                    catch (Exception ex) { LogOnce("err:" + f.GetType().FullName + ":" + ex.GetType().Name, "Erro no form " + f.GetType().FullName + ": " + ex.Message + " (registrado so uma vez)"); }
                }
                if (dxThemeOk && (dxSeen || (passCount % 10 == 0 && (dxSeen = DxLoaded()))))
                { try { DxThemes(); } catch (Exception ex) { dxThemeOk = false; Log("Temas DevExpress desligados: " + ex.Message); } }
                if (wikiOk && passCount % 2 == 0) { try { WikiTheme.Scan(forms); } catch (Exception ex) { wikiOk = false; Log("Documentacao desligada: " + ex.Message); } }
                if (IconSwap.Active && passCount % 10 == 0) { try { IconSwap.Tick(); } catch (Exception ex) { Log("Icones Next (mapa): " + ex.Message); } }
                if (suggestOk) { try { AutoSuggestTheme.Scan(); } catch (Exception ex) { suggestOk = false; Log("Autocompletar desligado: " + ex.Message); } }
                if (passCount % 40 == 0)
                {
                    // limpa referencias a controles descartados
                    List<Control> dead = new List<Control>();
                    foreach (Control c in seen.Keys) if (c.IsDisposed) dead.Add(c);
                    foreach (Control c in dead) seen.Remove(c);
                    List<IntPtr> deadH = new List<IntPtr>();
                    foreach (KeyValuePair<IntPtr, Control> kv in hDone) { try { if (kv.Value.IsDisposed || !kv.Value.IsHandleCreated || kv.Value.Handle != kv.Key) deadH.Add(kv.Key); } catch { deadH.Add(kv.Key); } }
                    foreach (IntPtr h in deadH) hDone.Remove(h);
                    frameDone.Clear(); // reavaliado na proxima passada (handles recriados)
                    try { IconSwap.Purge(); } catch { }
                    spgBorder.RemoveWhere(delegate(Control x) { return x.IsDisposed; });
                    List<object> deadT = new List<object>();
                    foreach (object o in crownMine.Keys) { Control oc = o as Control; if (oc == null || oc.IsDisposed) deadT.Add(o); }
                    foreach (object o in deadT) crownMine.Remove(o);
                    List<object> deadB = new List<object>();
                    foreach (object b in browserUrls.Keys) { Control bc = b as Control; if (bc != null && bc.IsDisposed) deadB.Add(b); }
                    foreach (object b in deadB) { browserUrls.Remove(b); browserHooked.Remove(b); }
                }
                if (passCount == 15 || passCount == 90 || passCount % 600 == 0) { ReportLight(); PatchIgColorTables(); try { LowContrastScan(forms); } catch (Exception ex) { Log("Verificacao de contraste: " + ex.Message); } }
            }
            catch (Exception ex) { Log("Erro na passada: " + ex.Message); }
            finally { inPass = false; }
            if (themedCount != before && passCount < 3000 && (themedCount - before) > 0 && passCount % 10 == 1)
                Log("Passada " + passCount + ": controles tratados ate agora = " + themedCount);
        }

        static bool IsDesignHosted(Control c)
        {
            try { return c.Site != null && c.Site.DesignMode; } catch { return false; }
        }

        static void Walk(Control c, int depth)
        {
            if (c == null || c.IsDisposed || depth > 80 || !On || c is ToggleButton) return;
            if (IsDesignHosted(c)) return; // nunca mexer em controles de designers (layout de objetos da KB)
            if (IsPreviewControl(c)) return; // pre-visualizacoes de patterns/formularios ficam com as cores originais
            bool known;
            if (!seen.TryGetValue(c, out known))
            {
                seen[c] = true;
                try { CrownOrig.Take(c); Journal.Snap(c); foreach (Control k in c.Controls) { CrownOrig.Take(k); Journal.Snap(k); } } catch { }   // valores originais (modo claro ao vivo)
                try { Theme(c); } catch (Exception ex) { Log("Erro tema " + c.GetType().FullName + ": " + ex.Message); }
                try
                {
                    c.ControlAdded -= OnControlAdded; c.ControlAdded += OnControlAdded;
                    c.HandleCreated -= OnHandleCreated; c.HandleCreated += OnHandleCreated;
                }
                catch { }
            }
            else
            {
                // controle ja tratado: so verifica se alguem voltou a cor para clara
                try { Recheck(c); } catch { }
            }
            try { SmartGrid(c); } catch { }
            if (c is ListView) { try { LvDark.Attach((ListView)c); } catch (Exception ex) { LogOnce("lvdark", "ListView escura: " + ex.GetType().Name + ": " + ex.Message); } }
            try { Extra(c); } catch { }
            if (IconSwap.Active) { try { IconSwap.Visit(c, !known || passCount % 15 == 0); } catch { } }
            if (!known || passCount % 5 == 0) { try { ContrastFix(c); } catch { } try { GlyphFix(c); } catch { } }
            string tn = c.GetType().FullName;
            if (dxTreeOk && IsDxTree(c.GetType())) { try { DxTree(c); } catch (Exception ex) { dxTreeOk = false; Log("Grade DevExpress desligada: " + ex.GetType().Name + ": " + ex.Message); } }
            if (reportOk && tn.StartsWith("DevExpress.XtraReports.", StringComparison.Ordinal)) { try { Report(c); } catch (Exception ex) { reportOk = false; Log("Designer de relatorio desligado: " + ex.GetType().Name + ": " + ex.Message); } }
            if (tn.StartsWith("CefSharp.")) { try { Browser(c); } catch (Exception ex) { Log("Cef: " + ex.Message); } return; }
            if (c is WebBrowser) { try { IeBrowser((WebBrowser)c); } catch { } return; }
            Control[] kids;
            try { kids = new Control[c.Controls.Count]; c.Controls.CopyTo(kids, 0); } catch { return; }
            foreach (Control k in kids) Walk(k, depth + 1);
        }

        static bool dxTreeOk = true, reportOk = true, dxThemeOk = true, wikiOk = true, suggestOk = true;
        [MethodImpl(MethodImplOptions.NoInlining)]
        static void DxTree(Control c)
        {
            if (c is DevExpress.XtraTreeList.TreeList) DxTreeTheme.Attach(c);
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        static void Report(Control c) { ReportTheme.Attach(c); }
        [MethodImpl(MethodImplOptions.NoInlining)]
        static void DxThemes() { DxTreeTheme.EnsureDxThemes(); }
        static bool DxLoaded()
        {
            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
                if (a.FullName.StartsWith("DevExpress.Utils.v7.3,", StringComparison.Ordinal)) return true;
            return false;
        }
        static bool dxSeen;
        static readonly Dictionary<Type, bool> dxTreeTypes = new Dictionary<Type, bool>();
        static bool IsDxTree(Type t)
        {
            bool r;
            if (!dxTreeTypes.TryGetValue(t, out r)) { r = TypeIs(t, "DevExpress.XtraTreeList.TreeList"); dxTreeTypes[t] = r; }
            return r;
        }

        static void OnControlAdded(object s, ControlEventArgs e)
        {
            try { if (!On || IsExcludedControl(e.Control)) return; Walk(e.Control, 0); } catch { }
        }

        static void OnHandleCreated(object s, EventArgs e)
        {
            try
            {
                Control c = (Control)s;
                if (!On || c.InvokeRequired) return;
                Form f = c as Form;
                if (f != null) { if (IsExcludedForm(f)) return; DarkTitle(f.Handle); }
                ApplyScrollTheme(c);
            }
            catch { }
        }

        static void ApplyScrollTheme(Control c)
        {
            if (c is ComboBox) { DarkScroll(c, "DarkMode_CFD"); return; }
            if (c is TreeView || c is ListView || c is ListBox || c is TextBoxBase || c is DataGridView || c is ScrollableControl && ((ScrollableControl)c).AutoScroll || c is ScrollBar || c is PropertyGrid)
                DarkScroll(c, "DarkMode_Explorer");
            else if (!(c is Form) && c.GetType().FullName.IndexOf("Infragistics", StringComparison.Ordinal) < 0 && c.GetType().FullName.IndexOf("CefSharp", StringComparison.Ordinal) < 0)
            {
                // controles de terceiros com scrollbar nativa (ex.: editores de texto)
                try { if (c.IsHandleCreated && HasScrollStyle(c.Handle)) DarkScroll(c, "DarkMode_Explorer"); } catch { }
            }
        }

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")] static extern int GetWindowLong32(IntPtr h, int idx);
        static bool HasScrollStyle(IntPtr h)
        {
            int style = GetWindowLong32(h, -16);
            return (style & 0x00200000) != 0 || (style & 0x00100000) != 0; // WS_VSCROLL | WS_HSCROLL
        }

        static bool IsInfragistics(Control c)
        {
            return c.GetType().FullName.StartsWith("Infragistics.", StringComparison.Ordinal);
        }

        static Color BackFor(Control c)
        {
            if (c is TextBoxBase)
            {
                TextBoxBase tb = (TextBoxBase)c;
                // editor de uma linha "multiline" (ex.: edicao no lugar em grades/listas) fica com a cor de campo
                bool oneRow = false;
                try { oneRow = tb.Multiline && !(c is RichTextBox) && c.Height <= c.Font.Height * 2 + 6; } catch { }
                return (c is RichTextBox || tb.Multiline) && !oneRow ? Editor : Input;
            }
            if (c is ComboBox || c is NumericUpDown || c is DomainUpDown || c is UpDownBase) return Input;
            if (c is ListBox || c is ListView || c is TreeView || c is CheckedListBox) return Panel;
            if (c is ButtonBase && !(c is CheckBox) && !(c is RadioButton)) return Button;
            if (c is ToolStrip) return (c is ToolStripDropDown) ? Menu : Chrome;
            if (c is StatusStrip) return Chrome;
            if (c is TabPage) return Panel;
            if (c is DateTimePicker || c is MonthCalendar) return Input;
            return Panel;
        }

        // campo que volta para Window/Control (claro) a cada edicao: escurece na hora (sem esperar a proxima passada)
        static readonly HashSet<Control> textHooked = new HashSet<Control>();
        static bool textFixing;
        static void HookTextBack(TextBoxBase tb)
        {
            if (!textHooked.Add(tb)) return;
            tb.Disposed += delegate { textHooked.Remove(tb); };
            tb.BackColorChanged += delegate
            {
                if (!On || textFixing || tb.IsDisposed || tb.ReadOnly && !tb.Enabled) return;
                Color b = tb.BackColor;
                if (!IsLight(b) || !b.IsSystemColor && !b.IsKnownColor) return;
                textFixing = true;
                try
                {
                    tb.BackColor = BackFor(tb);
                    if (IsDarkText(tb.ForeColor) || tb.ForeColor.IsSystemColor) tb.ForeColor = Fg;
                    LogOnce("txback:" + tb.GetType().FullName + ":" + tb.Name, "Campo voltou a cor clara do sistema (" + b.Name + "): escurecido na hora - " + tb.GetType().FullName + " '" + tb.Name + "'");
                }
                catch { }
                finally { textFixing = false; }
            };
        }

        static void SetIfDiff(Control c, Color back, Color fore)
        {
            try { if (!back.IsEmpty && c.BackColor.ToArgb() != back.ToArgb()) c.BackColor = back; } catch { }
            try { if (!fore.IsEmpty && c.ForeColor.ToArgb() != fore.ToArgb()) c.ForeColor = fore; } catch { }
        }

        static void Theme(Control c)
        {
            Type t = c.GetType();
            typesSeen.Add(t.FullName);
            Form f = c as Form;
            if (f != null && f.IsHandleCreated) DarkTitle(f.Handle);
            if (IsInfragistics(c)) { themedCount++; return; } // coberto pelo ISL (resolutionOrder ApplicationThenControl)
            if (t.FullName.StartsWith("CefSharp.", StringComparison.Ordinal)) return;

            Color back = Color.Empty, fore = Color.Empty;
            bool backLight = false;
            try { backLight = IsLight(c.BackColor); } catch { }
            if (backLight || c is TextBoxBase || c is ListBox || c is TreeView || c is ListView || c is ComboBox || c is ToolStrip || c is ButtonBase && !(c is CheckBox) && !(c is RadioButton))
            {
                back = BackFor(c);
                if (!backLight && !(c is ToolStrip) && !(c is ButtonBase)) { try { if (!IsLight(c.BackColor) && Lum(c.BackColor) < 90) back = Color.Empty; } catch { } }
            }
            bool darkBg = !back.IsEmpty || !IsLight(SafeBack(c));
            if (darkBg)
            {
                try { if (IsDarkText(c.ForeColor) || c.ForeColor == SystemColors.ControlText || c.ForeColor == SystemColors.WindowText) fore = (c is ButtonBase && !(c is CheckBox) && !(c is RadioButton)) ? FgBright : Fg; } catch { }
            }
            if (c is StatusStrip) fore = FgBright;
            if (c is LinkLabel) { LinkLabel ll = (LinkLabel)c; ll.LinkColor = Link; ll.ActiveLinkColor = DarkEngine.T(130, 190, 255); ll.VisitedLinkColor = Link; }
            SetIfDiff(c, back, fore);
            try { FixSystemColors(c); } catch { }
            if (c is TextBoxBase) { try { HookTextBack((TextBoxBase)c); } catch { } }

            ButtonBase bb = c as ButtonBase;
            if (bb != null && !(c is CheckBox) && !(c is RadioButton))
            {
                try
                {
                    Journal.SnapAll(bb.FlatAppearance, c);
                    Button btn = c as Button;
                    if (btn != null) btn.UseVisualStyleBackColor = false;
                    if (bb.FlatStyle == FlatStyle.Standard || bb.FlatStyle == FlatStyle.System) bb.FlatStyle = FlatStyle.Flat;
                    bb.FlatAppearance.BorderColor = DarkEngine.T(61, 73, 87);
                    bb.FlatAppearance.MouseOverBackColor = DarkEngine.T(62, 84, 112);   // #3E5470 hover visivel sobre #2C3C50
                    bb.FlatAppearance.MouseDownBackColor = MenuHot;
                }
                catch { }
            }
            else if (c is CheckBox || c is RadioButton)
            {
                try { ButtonBase cb = (ButtonBase)c; if (cb.FlatStyle == FlatStyle.System) cb.FlatStyle = FlatStyle.Standard; } catch { }
                CheckBox chk = c as CheckBox;
                if (chk != null) { try { chk.Paint -= OnCheckBoxPaint; chk.Paint += OnCheckBoxPaint; chk.Invalidate(); } catch { } }
            }

            ComboBox cbx = c as ComboBox;
            if (cbx != null && c.GetType().FullName.IndexOf("ToolStrip", StringComparison.Ordinal) < 0)
            {
                // estilo Flat: o WinForms pinta borda/botao/fundo com BackColor (o estilo visual do Windows pintava branco)
                try { if (cbx.FlatStyle == FlatStyle.Standard || cbx.FlatStyle == FlatStyle.System) cbx.FlatStyle = FlatStyle.Flat; } catch { }
                try { if (!IsLight(cbx.BackColor) && cbx.BackColor.ToArgb() != Input.ToArgb()) cbx.BackColor = Input; } catch { }
                try { if (IsDarkText(cbx.ForeColor) || cbx.ForeColor.ToArgb() != Fg.ToArgb()) cbx.ForeColor = Fg; } catch { }
            }

            ToolStrip ts = c as ToolStrip;
            if (ts != null) ThemeToolStrip(ts);

            DataGridView dgv = c as DataGridView;
            if (dgv != null) ThemeGrid(dgv);

            PropertyGrid pg = c as PropertyGrid;
            if (pg != null) ThemePropertyGrid(pg);

            TabControl tc = c as TabControl;
            if (tc != null) ThemeTabControl(tc);

            TreeView tv = c as TreeView;
            if (tv != null) { try { if (IsLight(tv.LineColor) || tv.LineColor == Color.Black) tv.LineColor = FgDim; } catch { } }

            // textos desabilitados legiveis (o WinForms escurece a partir do BackColor) e molduras de GroupBox escuras
            if (c is Button) { try { c.Paint -= OnDisabledButtonPaint; c.Paint += OnDisabledButtonPaint; } catch { } }
            else if (c is Label && !(c is LinkLabel)) { try { c.Paint -= OnDisabledLabelPaint; c.Paint += OnDisabledLabelPaint; } catch { } }
            else if (c is GroupBox) { try { if (((GroupBox)c).FlatStyle != FlatStyle.System) { c.Paint -= OnGroupBoxPaint; c.Paint += OnGroupBoxPaint; c.Invalidate(); } } catch { } }
            WebPane(c);

            if (c.ContextMenuStrip != null) ThemeToolStrip(c.ContextMenuStrip);

            // propriedades Color declaradas por controles de terceiros (DevExpress, Actipro, GeneXus, ...)
            GenericColors(c);

            ApplyScrollTheme(c);
            themedCount++;
        }

        static Color SafeBack(Control c) { try { return c.BackColor; } catch { return Color.Empty; } }

        // Cores "de sistema" (SystemColors.Window etc.) sao pintadas pelo Windows com a paleta ATUAL do sistema;
        // se a paleta escura for desfeita (ex.: bloqueio/desbloqueio, troca de tema) elas voltam a ficar brancas
        // mesmo que o .NET ainda informe a cor escura. Aqui elas viram cores fixas (explicitas).
        // Caixas de texto somente leitura/desabilitadas: fundo #181F2A e texto #DCE2EB.
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<TextBoxBase, object> RoBoxes = new System.Runtime.CompilerServices.ConditionalWeakTable<TextBoxBase, object>();

        static void FixSystemColors(Control c)
        {
            if (c is ToolStrip || c is Form) return;
            TextBoxBase tb = c as TextBoxBase;
            if (tb != null)
            {
                bool ro = tb.ReadOnly || !tb.Enabled;
                Color b = tb.BackColor;
                Color want = ro ? Panel : BackFor(c);
                object mark;
                bool wasRo = RoBoxes.TryGetValue(tb, out mark);
                if (b.IsSystemColor || IsLight(b) || (ro && b.ToArgb() == Input.ToArgb()) || (!ro && wasRo && b.ToArgb() == Panel.ToArgb()))
                {
                    if (b.ToArgb() != want.ToArgb() || b.IsSystemColor) tb.BackColor = want;
                    if (ro && !wasRo) RoBoxes.Add(tb, null);
                    else if (!ro && wasRo) RoBoxes.Remove(tb);
                }
                Color f = tb.ForeColor;
                if (f.IsSystemColor || IsDarkText(f)) tb.ForeColor = Fg;
                return;
            }
            Control p = c.Parent;
            Color bc = c.BackColor;
            if (bc.IsSystemColor && (p == null || p.BackColor != bc))
                c.BackColor = IsLight(bc) ? BackFor(c) : Color.FromArgb(bc.ToArgb());
            Color fc = c.ForeColor;
            if (fc.IsSystemColor && (p == null || p.ForeColor != fc))
                c.ForeColor = IsDarkText(fc) ? Fg : Color.FromArgb(fc.ToArgb());
        }

        static void Recheck(Control c)
        {
            if (IsInfragistics(c)) return;
            if (c.GetType().FullName.StartsWith("CefSharp.", StringComparison.Ordinal)) return;
            Color b = c.BackColor;
            if (IsLight(b))
            {
                SetIfDiff(c, BackFor(c), IsDarkText(c.ForeColor) ? Fg : Color.Empty);
                GenericColors(c);
            }
            else if (IsDarkText(c.ForeColor) && !(c is ToolStrip)) { try { c.ForeColor = Fg; } catch { } }
            try { FixSystemColors(c); } catch { }
            ToolStrip ts = c as ToolStrip;
            if (ts != null) ThemeToolStrip(ts);
            DataGridView dgv = c as DataGridView;
            if (dgv != null && IsLight(dgv.BackgroundColor)) ThemeGrid(dgv);
        }

        static void ThemeToolStrip(ToolStrip ts)
        {
            try
            {
                Journal.Snap(ts);
                if (!(ts.Renderer is ToolStripProfessionalRenderer && ((ToolStripProfessionalRenderer)ts.Renderer).ColorTable is DarkColorTable))
                {
                    if (ts.RenderMode != ToolStripRenderMode.ManagerRenderMode || !(ToolStripManager.Renderer is ToolStripProfessionalRenderer))
                        ts.Renderer = new DarkTsRenderer();
                }
                Color back = (ts is ToolStripDropDown) ? Menu : Chrome;
                if (ts.BackColor.ToArgb() != back.ToArgb()) ts.BackColor = back;
                Color fore = FgBright;
                if (ts.ForeColor.ToArgb() != fore.ToArgb()) ts.ForeColor = fore;
                foreach (ToolStripItem it in ts.Items) ThemeItem(it, fore);
            }
            catch (Exception ex) { Log("ToolStrip: " + ex.Message); }
        }

        static void ThemeItem(ToolStripItem it, Color fore)
        {
            try
            {
                Journal.Snap(it);
                if (IsDarkText(it.ForeColor) || it.ForeColor == SystemColors.ControlText || it.ForeColor == SystemColors.MenuText) it.ForeColor = fore;
                if (IsLight(it.BackColor) && it.BackColor != SystemColors.Control && it.BackColor != SystemColors.Menu) it.BackColor = Color.Transparent;
                ToolStripControlHost host = it as ToolStripControlHost;
                if (host != null && host.Control != null) Walk(host.Control, 0);
                ToolStripDropDownItem dd = it as ToolStripDropDownItem;
                if (dd != null && dd.HasDropDownItems)
                {
                    ToolStripDropDown d = dd.DropDown;
                    if (d != null && !seen.ContainsKey(d)) { seen[d] = true; ThemeToolStrip(d); if (openingHooked.Add(d)) d.Opening += delegate { if (On) ThemeToolStrip(d); }; }
                }
            }
            catch { }
        }

        static void ThemeGrid(DataGridView g)
        {
            try
            {
                Journal.SnapAll(g.DefaultCellStyle, g); Journal.SnapAll(g.AlternatingRowsDefaultCellStyle, g); Journal.SnapAll(g.ColumnHeadersDefaultCellStyle, g);
                Journal.SnapAll(g.RowHeadersDefaultCellStyle, g); Journal.SnapAll(g.RowsDefaultCellStyle, g);
                foreach (DataGridViewColumn sc in g.Columns) if (sc.DefaultCellStyle != null) Journal.SnapAll(sc.DefaultCellStyle, g);
                g.EnableHeadersVisualStyles = false;
                g.BackgroundColor = Panel;
                g.GridColor = Border;
                g.BorderStyle = BorderStyle.None;
                DataGridViewCellStyle d = g.DefaultCellStyle;
                d.BackColor = Panel; d.ForeColor = Fg; d.SelectionBackColor = Sel; d.SelectionForeColor = Color.White;
                g.AlternatingRowsDefaultCellStyle.BackColor = DarkEngine.T(28, 36, 48);
                g.AlternatingRowsDefaultCellStyle.ForeColor = Fg;
                g.ColumnHeadersDefaultCellStyle.BackColor = Chrome; g.ColumnHeadersDefaultCellStyle.ForeColor = Fg;
                g.ColumnHeadersDefaultCellStyle.SelectionBackColor = Chrome;
                g.RowHeadersDefaultCellStyle.BackColor = Chrome; g.RowHeadersDefaultCellStyle.ForeColor = Fg;
                g.RowHeadersDefaultCellStyle.SelectionBackColor = Sel;
                g.RowsDefaultCellStyle.BackColor = Panel; g.RowsDefaultCellStyle.ForeColor = Fg;
                foreach (DataGridViewColumn col in g.Columns)
                {
                    if (col.DefaultCellStyle != null && IsLight(col.DefaultCellStyle.BackColor)) col.DefaultCellStyle.BackColor = Panel;
                    if (col.DefaultCellStyle != null && IsDarkText(col.DefaultCellStyle.ForeColor)) col.DefaultCellStyle.ForeColor = Fg;
                }
            }
            catch (Exception ex) { Log("DataGridView: " + ex.Message); }
        }

        static void ThemePropertyGrid(PropertyGrid p)
        {
            try
            {
                p.BackColor = Chrome;
                p.ViewBackColor = Panel;
                p.ViewForeColor = Fg;
                p.HelpBackColor = Chrome;
                p.HelpForeColor = Fg;
                p.LineColor = Chrome;
                p.CategoryForeColor = FgBright;
                p.CommandsBackColor = Chrome;
                p.CommandsForeColor = Fg;
                SetColorProp(p, "CategorySplitterColor", Chrome);
                SetColorProp(p, "SelectedItemWithFocusBackColor", Sel);
                SetColorProp(p, "SelectedItemWithFocusForeColor", Color.White);
                SetColorProp(p, "ViewBorderColor", Border);
                SetColorProp(p, "HelpBorderColor", Border);
                SetColorProp(p, "CommandsBorderColor", Border);
                SetColorProp(p, "DisabledItemForeColor", DarkEngine.T(110, 122, 138));
                SetColorProp(p, "CommandsLinkColor", Link);
                SetColorProp(p, "CommandsActiveLinkColor", Link);
                foreach (Control k in p.Controls) { ToolStrip ts = k as ToolStrip; if (ts != null) ThemeToolStrip(ts); }
            }
            catch (Exception ex) { Log("PropertyGrid: " + ex.Message); }
        }

        static void SetColorProp(object o, string name, Color v)
        {
            try
            {
                PropertyInfo pi = o.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                if (pi != null && pi.CanWrite && pi.PropertyType == typeof(Color)) pi.SetValue(o, v, null);
            }
            catch { }
        }

        static void ThemeTabControl(TabControl tc)
        {
            try
            {
                if (tc.DrawMode == TabDrawMode.Normal)
                {
                    tc.DrawMode = TabDrawMode.OwnerDrawFixed;
                    tc.DrawItem -= DrawTab; tc.DrawItem += DrawTab;
                    if (tabPaintHooked.Add(tc)) tc.Paint += delegate { };
                }
                foreach (TabPage tp in tc.TabPages) { tp.UseVisualStyleBackColor = false; if (IsLight(tp.BackColor)) tp.BackColor = Panel; if (IsDarkText(tp.ForeColor)) tp.ForeColor = Fg; }
                DarkScroll(tc, "DarkMode_Explorer");
            }
            catch (Exception ex) { Log("TabControl: " + ex.Message); }
        }

        static void DrawTab(object sender, DrawItemEventArgs e)
        { if (!On) return;
            try
            {
                TabControl tc = (TabControl)sender;
                TabPage tp = tc.TabPages[e.Index];
                bool sel = e.Index == tc.SelectedIndex;
                Rectangle r = e.Bounds;
                using (SolidBrush b = new SolidBrush(sel ? Editor : Chrome)) e.Graphics.FillRectangle(b, r);
                if (sel) using (SolidBrush a = new SolidBrush(Accent)) e.Graphics.FillRectangle(a, r.X, r.Y, r.Width, 2);
                TextRenderer.DrawText(e.Graphics, tp.Text, tc.Font, r, sel ? Color.White : FgSoft, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            }
            catch { }
        }

        // ---------- Propriedades Color genericas ----------
        static PropertyInfo[] GetColorProps(Type t)
        {
            PropertyInfo[] arr;
            if (colorProps.TryGetValue(t, out arr)) return arr;
            List<PropertyInfo> list = new List<PropertyInfo>();
            try
            {
                foreach (PropertyInfo pi in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (pi.PropertyType != typeof(Color) || !pi.CanRead || !pi.CanWrite) continue;
                    if (pi.GetIndexParameters().Length > 0) continue;
                    Type decl = pi.DeclaringType;
                    if (decl == null || decl.Namespace == null) continue;
                    if (decl.Namespace.StartsWith("System.", StringComparison.Ordinal) || decl.Namespace == "System") continue;
                    string n = pi.Name;
                    if (n.IndexOf("Transparent", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Mask", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        n.IndexOf("Key", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Image", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        n.IndexOf("Error", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Warning", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        n.IndexOf("Bookmark", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Breakpoint", StringComparison.OrdinalIgnoreCase) >= 0)
                        continue;
                    try { BrowsableAttribute ba = (BrowsableAttribute)Attribute.GetCustomAttribute(pi, typeof(BrowsableAttribute)); } catch { }
                    list.Add(pi);
                }
            }
            catch { }
            arr = list.ToArray();
            colorProps[t] = arr;
            return arr;
        }

        static void GenericColors(Control c)
        {
            foreach (PropertyInfo pi in GetColorProps(c.GetType()))
            {
                try
                {
                    Color v = (Color)pi.GetValue(c, null);
                    if (v.IsEmpty || v.A == 0) continue;
                    string n = pi.Name;
                    bool sel = n.IndexOf("Select", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Highlight", StringComparison.OrdinalIgnoreCase) >= 0;
                    Color nv = Color.Empty;
                    if (n.IndexOf("Back", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Fill", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Gutter", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Margin", StringComparison.OrdinalIgnoreCase) >= 0)
                    { if (IsLight(v)) nv = sel ? Sel : (n.IndexOf("Gutter", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Margin", StringComparison.OrdinalIgnoreCase) >= 0 ? Editor : Panel); }
                    else if (n.IndexOf("Fore", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Text", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Font", StringComparison.OrdinalIgnoreCase) >= 0)
                    { if (IsDarkText(v)) nv = sel ? Color.White : Fg; }
                    else if (n.IndexOf("Line", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Border", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Grid", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Separator", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Splitter", StringComparison.OrdinalIgnoreCase) >= 0)
                    { if (IsLight(v)) nv = Border; }
                    else if (n.IndexOf("Link", StringComparison.OrdinalIgnoreCase) >= 0)
                    { if (IsDarkText(v)) nv = Link; }
                    if (!nv.IsEmpty && nv.ToArgb() != v.ToArgb()) pi.SetValue(c, nv, null);
                }
                catch { }
            }
        }


        // ---------- VisualHint SmartPropertyGrid (painel Properties do GeneXus) ----------
        // As faixas de categoria/subcategoria sao pintadas pelo DrawManager com gradiente ate Color.White,
        // e as propriedades HyperLink (ex.: "Help") usam Color.Blue fixo.
        static readonly Dictionary<Type, PropertyInfo> spgTypes = new Dictionary<Type, PropertyInfo>();
        static readonly Dictionary<object, bool> spgDm = new Dictionary<object, bool>();

        static readonly HashSet<Control> spgBorder = new HashSet<Control>();
        static void SmartGrid(Control c)
        {
            Type t = c.GetType();
            PropertyInfo pdm;
            if (!spgTypes.TryGetValue(t, out pdm))
            {
                pdm = null;
                try
                {
                    PropertyInfo p = t.GetProperty("DrawManager", BindingFlags.Public | BindingFlags.Instance);
                    if (p != null && t.GetProperty("FirstProperty", BindingFlags.Public | BindingFlags.Instance) != null) pdm = p;
                }
                catch { }
                spgTypes[t] = pdm;
                if (pdm != null) Log("SmartPropertyGrid encontrado: " + t.FullName);
            }
            if (pdm == null) return;
            bool changed = false;
            object dm = null;
            try { dm = pdm.GetValue(c, null); } catch { }
            if (dm != null && !spgDm.ContainsKey(dm))
            {
                spgDm[dm] = true;
                try
                {
                    Journal.SnapFields(dm);
                    MethodInfo m1 = dm.GetType().GetMethod("SetCategoryBackgroundColors", new Type[] { typeof(Color), typeof(Color) });
                    MethodInfo m2 = dm.GetType().GetMethod("SetSubCategoryBackgroundColors", new Type[] { typeof(Color), typeof(Color) });
                    if (m1 != null) { m1.Invoke(dm, new object[] { Chrome, Chrome }); changed = true; }
                    if (m2 != null) { m2.Invoke(dm, new object[] { Chrome, Chrome }); changed = true; }
                    Log("SmartPropertyGrid: faixas de categoria escurecidas (" + dm.GetType().Name + ")");
                }
                catch (Exception ex) { Log("SmartPropertyGrid DrawManager: " + ex.Message); }
            }
            SetColorProp(c, "RootCategoryTextColor", FgBright);
            SetColorProp(c, "SubCategoryTextColor", FgBright);
            // moldura de 1px clara da grade de Properties: sem borda (so em memoria)
            if (!spgBorder.Contains(c))
            {
                spgBorder.Add(c);
                try
                {
                    PropertyInfo pb = t.GetProperty("BorderStyle", BindingFlags.Public | BindingFlags.Instance);
                    if (pb != null && pb.CanWrite && pb.PropertyType == typeof(BorderStyle) && (BorderStyle)pb.GetValue(c, null) != BorderStyle.None)
                    {
                        pb.SetValue(c, BorderStyle.None, null);
                        LogOnce("spgborder", "SmartPropertyGrid: moldura clara removida (BorderStyle=None em memoria)");
                    }
                }
                catch (Exception ex) { LogOnce("spgborder-err", "SmartPropertyGrid borda: " + ex.Message); }
            }
            // propriedades HyperLink com azul escuro fixo
            if (passCount % 3 == 0) try
            {
                object e = t.GetProperty("FirstProperty").GetValue(c, null);
                if (e != null)
                {
                    object de = e.GetType().GetMethod("GetDeepEnumerator", Type.EmptyTypes).Invoke(e, null);
                    Type et = de.GetType();
                    PropertyInfo rb = et.GetProperty("RightBound");
                    PropertyInfo pp = et.GetProperty("Property");
                    MethodInfo next = et.GetMethod("MoveNext", Type.EmptyTypes);
                    MethodInfo eq = null;
                    for (Type bt = et; bt != null && eq == null; bt = bt.BaseType)
                        eq = bt.GetMethod("op_Equality", BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
                    int guard = 0;
                    while (de != null && guard++ < 5000)
                    {
                        object right = rb.GetValue(de, null);
                        if (eq == null || (bool)eq.Invoke(null, new object[] { de, right })) break;
                        object prop = pp.GetValue(de, null);
                        if (prop != null && prop.GetType().Name.IndexOf("HyperLink", StringComparison.Ordinal) >= 0)
                        {
                            PropertyInfo fc = prop.GetType().GetProperty("ForeColor");
                            if (fc != null && fc.CanWrite)
                            {
                                Color v = (Color)fc.GetValue(prop, null);
                                if (IsDarkText(v) || (v.B > 150 && v.R < 60 && v.G < 60)) { Journal.SnapNamed(prop, "ForeColor"); fc.SetValue(prop, Link, null); changed = true; }
                            }
                        }
                        de = next.Invoke(de, null);
                    }
                }
            }
            catch { }
            if (changed) { try { c.Invalidate(true); } catch { } }
        }


        // ---------- Casos especiais (scrollbars Actipro, StatusBar nativa, abas Crownwood, scrollbars tardias) ----------
        static readonly Dictionary<IntPtr, Control> hDone = new Dictionary<IntPtr, Control>();
        static readonly Dictionary<object, bool> crownMine = new Dictionary<object, bool>();
        static readonly Dictionary<object, bool> crownHooked = new Dictionary<object, bool>();

        static bool TypeIs(Type t, string fullName)
        {
            for (; t != null; t = t.BaseType) if (t.FullName == fullName) return true;
            return false;
        }

        public static bool IsPreviewUrl(string u)
        {
            if (string.IsNullOrEmpty(u)) return false;
            u = u.ToLowerInvariant().Replace('\\', '/');
            return u.Contains("/previewimages/") || u.Contains("_preview.html") || u.Contains("/preview/");
        }

        static readonly Dictionary<Type, bool> previewTypes = new Dictionary<Type, bool>();
        static bool IsPreviewControl(Control c)
        {
            Type t = c.GetType();
            bool r;
            if (previewTypes.TryGetValue(t, out r)) return r;
            r = false;
            try
            {
                string n = t.Name, full = t.FullName ?? "";
                bool pattern = full.StartsWith("Artech.Patterns.", StringComparison.Ordinal) || full.StartsWith("Artech.Packages.Patterns.", StringComparison.Ordinal) ||
                               full.IndexOf("WorkWith", StringComparison.OrdinalIgnoreCase) >= 0 || full.IndexOf("DVelop", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               full.IndexOf("Form", StringComparison.Ordinal) >= 0 || full.IndexOf("Layout", StringComparison.Ordinal) >= 0 || full.IndexOf("Panel", StringComparison.Ordinal) >= 0;
                r = pattern && (n.EndsWith("Preview", StringComparison.Ordinal) || n.IndexOf("PreviewControl", StringComparison.Ordinal) >= 0 || n.IndexOf("PreviewPanel", StringComparison.Ordinal) >= 0 || n.IndexOf("PreviewHost", StringComparison.Ordinal) >= 0);
            }
            catch { }
            previewTypes[t] = r;
            if (r) Log("Pre-visualizacao mantida com cores originais: " + t.FullName);
            return r;
        }

        // ---------- Tabelas de cores Office2010 do Infragistics (borda do menu aberto, cabecalho aberto) ----------
        static bool igLogged;
        static void PatchIgColorTables()
        {
            try
            {
                Type t = typeof(Infragistics.Win.Appearance).Assembly.GetType("Infragistics.Win.Office2010ColorTable");
                if (t == null) return;
                int n = 0;
                n += SetIgArr(t, "popupMenuBorder", HotBorder);
                n += SetIgArr(t, "popupMenuToolOpenGradientDark", Chrome);
                n += SetIgArr(t, "popupMenuToolOpenGradientLight", Chrome);
                n += SetIgArr(t, "popupMenuToolOpenForecolor", Color.White);
                n += SetIgArr(t, "menuDroppedBackColor", Chrome);
                n += SetIgArr(t, "menuIconAreaGradientDark", Chrome);
                n += SetIgArr(t, "menuIconAreaGradientLight", Chrome);
                n += SetIgArr(t, "menuIconAreaExpandedGradientDark", Chrome);
                n += SetIgArr(t, "menuIconAreaExpandedGradientLight", Chrome);
                n += SetIgArr(t, "menuIconAreaBorderColor", Chrome);
                n += SetIgArr(t, "pressedGradientDark", MenuHot);
                n += SetIgArr(t, "pressedGradientLight", MenuHot);
                int nb = FlatButtonColors(t);
                Type t7 = typeof(Infragistics.Win.Appearance).Assembly.GetType("Infragistics.Win.Office2007ColorTable");
                if (t7 != null) nb += FlatButtonColors(t7);
                int nl = LineColors(t);
                if (t7 != null) nl += LineColors(t7);
                Type t3 = typeof(Infragistics.Win.Appearance).Assembly.GetType("Infragistics.Win.Office2003ColorTable");
                if (t3 != null) nl += LineColors(t3);
                if (!igLogged) { igLogged = true; Log("Infragistics Office2010ColorTable: " + n + " cores de menu/hover ajustadas; botoes planos (hover/pressionado/marcado, sem brilho) Office2010/2007: " + nb + " cores; linhas/cantos/alca das barras suavizados: " + nl + " cores"); }
            }
            catch (Exception ex) { LogOnce("igct", "Office2010ColorTable: " + ex.Message); }
        }

        // Hover/pressionado/marcado dos botoes no estilo Next (plano): sem gradiente amarelado nem brilho (glow) do Office.
        // Rede de seguranca para o que o filtro de desenho (FlatButtons) nao alcanca; so em memoria, no processo do GeneXus.
        static int FlatButtonColors(Type t)
        {
            int n = 0;
            foreach (string f in new string[] { "buttonHighlightGradientDark", "buttonHighlightGradientLight", "buttonHighlightBorderInner", "buttonHighlightLightenColor",
                "highlightNotPressedGradientDark", "highlightNotPressedGradientLight", "editorButtonActiveGradientDark", "editorButtonActiveGradientLight",
                "pagerButtonHotTrackedGradientDark", "pagerButtonHotTrackedGradientLight", "popupMenuScrollItemHotTrackedGradientDark", "popupMenuScrollItemHotTrackedGradientLight",
                "highlightQuickCustomizeGradientDark", "highlightQuickCustomizeGradientLight", "buttonHighlightGlow",
                "buttonHighlightCheckedGradientDark", "buttonHighlightCheckedGradientLight", "buttonHighlightCheckedBorderInner", "buttonHighlightCheckedGlow", "pagerButtonHotTrackBorderInner" })
                n += SetIgArr(t, f, HotBg);
            foreach (string f in new string[] { "buttonHighlightBorderColor", "toolBorderHighlight", "editorButtonActiveBorder", "pagerButtonHotTrackedBorderColor", "popupMenuScrollItemHotTrackedBorder",
                "buttonPressedBorderColor", "editorButtonPressedBorder", "pagerButtonPressedBorderColor", "popupMenuScrollItemPressedBorder" })
                n += SetIgArr(t, f, HotBorder);
            foreach (string f in new string[] { "buttonPressedGradientDark", "buttonPressedGradientLight", "buttonPressedBorderInner", "buttonPressedGlow",
                "highlightPressedGradientDark", "highlightPressedGradientLight", "highlightMouseDownGradientDark", "highlightMouseDownGradientLight",
                "editorButtonPressedGradientDark", "editorButtonPressedGradientLight", "editorButtonPressedBorderInner", "pagerButtonPressedGradientDark", "pagerButtonPressedGradientLight",
                "popupMenuScrollItemPressedGradientDark", "popupMenuScrollItemPressedGradientLight" })
                n += SetIgArr(t, f, MenuHot);
            foreach (string f in new string[] { "buttonCheckedGradientDark", "buttonCheckedGradientLight", "buttonCheckedGlow" })
                n += SetIgArr(t, f, Sel);
            foreach (string f in new string[] { "buttonCheckedBorderColor", "buttonHighlightCheckedBorderColor" })
                n += SetIgArr(t, f, Accent);
            foreach (string f in new string[] { "toolHighlightForecolor", "toolHighlightPressedForecolor", "mainMenuToolHighlightForecolor", "mainMenuToolHighlightPressedForecolor" })
                n += SetIgArr(t, f, FgBright);
            return n;
        }

        // Linhas claras das barras (sublinhado da faixa #ACAFB4, sombras de canto #E5E9EE/#D9DCE2) e alca de arrastar (quadrados #1B1B1B/#595959):
        // passam a linhas suaves #333D47 e alca discreta; so em memoria.
        static int LineColors(Type t)
        {
            int n = 0;
            n += SetIgArr(t, "toolbarUnderline", Border);
            n += SetIgArr(t, "toolbarCornerShadowDark", Border);
            n += SetIgArr(t, "toolbarCornerShadowMedium", DarkEngine.T(46, 55, 66));
            n += SetIgArr(t, "toolbarCornerShadowLight", DarkEngine.T(42, 51, 63));
            n += SetIgArr(t, "grabHandleDarkSquare", DarkEngine.T(106, 114, 126));
            n += SetIgArr(t, "grabHandleLightSquare", Chrome);
            return n;
        }

        static int SetIgArr(Type t, string name, Color c)
        {
            FieldInfo f = t.GetField(name, BindingFlags.NonPublic | BindingFlags.Static);
            if (f == null) return 0;
            Color[] a = f.GetValue(null) as Color[];
            if (a == null) return 0;
            if (!igOrig.ContainsKey(f)) igOrig[f] = (Color[])a.Clone();   // original para o modo claro
            for (int i = 0; i < a.Length; i++) a[i] = c;
            return 1;
        }

        // ---------- Popups do editor (Actipro IntelliPrompt: QuickInfo, parametros, lista) ----------
        static readonly Dictionary<object, bool> markupHooked = new Dictionary<object, bool>();
        static readonly Dictionary<object, bool> popupHooked = new Dictionary<object, bool>();

        static void ActiproPopup(Control c, Type t)
        {
            if (TypeIs(t, "ActiproSoftware.MarkupLabel.MarkupLabel"))
            {
                if (!markupHooked.ContainsKey(c))
                {
                    markupHooked[c] = true;
                    c.TextChanged += OnMarkupChanged;
                    c.VisibleChanged += OnMarkupChanged;
                    c.Disposed += delegate(object s, EventArgs e) { markupHooked.Remove(s); };
                }
                RecolorMarkup(c);
            }
            else if (c is Form && TypeIs(t, "ActiproSoftware.WinUICore.PopupControl"))
            {
                if (!popupHooked.ContainsKey(c))
                {
                    popupHooked[c] = true;
                    c.Paint += OnPopupPaint;
                    c.Disposed += delegate(object s, EventArgs e) { popupHooked.Remove(s); };
                    c.Invalidate();
                }
            }
        }

        static void OnPopupPaint(object s, PaintEventArgs e)
        { if (!On) return;
            try
            {
                Control c = (Control)s;
                Rectangle r = c.ClientRectangle;
                using (Pen p = new Pen(Border)) e.Graphics.DrawRectangle(p, r.Left, r.Top, r.Width - 1, r.Height - 1);
            }
            catch { }
        }

        static void OnMarkupChanged(object s, EventArgs e)
        { if (!On) return;
            Control c = s as Control;
            if (c == null || c.IsDisposed || c.InvokeRequired) return;
            // o Actipro re-interpreta o markup logo depois de TextChanged: recolorir depois disso (antes do WM_PAINT)
            try { if (c.IsHandleCreated) c.BeginInvoke((MethodInvoker)delegate { RecolorMarkup(c); }); } catch { }
        }

        static bool IsActiproPopup(Control c)
        {
            for (Control p = c; p != null; p = p.Parent)
                if (TypeIs(p.GetType(), "ActiproSoftware.WinUICore.PopupControl")) return true;
            return false;
        }

        public static void RecolorMarkup(Control c)
        {
            try
            {
                if (c.IsDisposed || c.InvokeRequired) return;
                if (IsActiproPopup(c))
                {
                    if (c.BackColor != Chrome) c.BackColor = Chrome;
                    if (c.ForeColor != Fg) c.ForeColor = Fg;
                }
                Color bg = c.BackColor;
                if (bg.IsEmpty || bg.A < 255) bg = Chrome;
                if (IsLight(bg)) return;
                PropertyInfo pr = c.GetType().GetProperty("RootElement");
                if (pr == null) return;
                bool changed = false;
                FixMarkupElement(pr.GetValue(c, null), bg, ref changed, 0);
                if (changed) c.Invalidate(true);
            }
            catch (Exception ex) { LogOnce("markup:" + ex.GetType().Name, "Markup (QuickInfo): " + ex.Message); }
        }

        static void FixMarkupElement(object el, Color bg, ref bool changed, int depth)
        {
            if (el == null || depth > 60) return;
            Type t = el.GetType();
            PropertyInfo pcss = t.GetProperty("CssData");
            object css = pcss != null ? pcss.GetValue(el, null) : null;
            Color back = bg;
            if (css != null)
            {
                PropertyInfo pc = css.GetType().GetProperty("Color"), pb = css.GetType().GetProperty("BackgroundColor");
                if (pb != null)
                {
                    Color b = (Color)pb.GetValue(css, null);
                    if (!b.IsEmpty && b.A > 0)
                    {
                        if (Lum(b) > 90) { pb.SetValue(css, Color.Empty, null); changed = true; }
                        else back = b;
                    }
                }
                if (pc != null)
                {
                    Color f = (Color)pc.GetValue(css, null);
                    if (!f.IsEmpty && f.A > 0)
                    {
                        Color nf = Readable(f, back);
                        if (nf.ToArgb() != f.ToArgb()) { pc.SetValue(css, nf, null); changed = true; }
                    }
                    else if (t.Name == "MarkupLabelBoldElement" || t.Name == "MarkupLabelStrongElement")
                    {
                        pc.SetValue(css, FgBright, null); changed = true;   // titulo em destaque
                    }
                }
            }
            PropertyInfo pch = t.GetProperty("Children");
            System.Collections.IList ch = pch != null ? pch.GetValue(el, null) as System.Collections.IList : null;
            if (ch != null)
            {
                object[] arr = new object[ch.Count];
                ch.CopyTo(arr, 0);
                foreach (object o in arr) FixMarkupElement(o, back, ref changed, depth + 1);
            }
        }

        // ---------- Contraste (WCAG) ----------
        static double RelLum(Color c)
        {
            Func<int, double> ch = delegate(int v) { double x = v / 255.0; return x <= 0.03928 ? x / 12.92 : Math.Pow((x + 0.055) / 1.055, 2.4); };
            return 0.2126 * ch(c.R) + 0.7152 * ch(c.G) + 0.0722 * ch(c.B);
        }
        public static double Contrast(Color a, Color b)
        {
            double la = RelLum(a), lb = RelLum(b);
            return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
        }
        // cor legivel (>= 4.5:1) sobre fundo escuro: azuis viram o azul de link do Next, cinzas/pretos viram texto claro
        public static Color Readable(Color f, Color bg)
        {
            if (Contrast(f, bg) >= 4.5) return f;
            int mx = Math.Max(f.R, Math.Max(f.G, f.B)), mn = Math.Min(f.R, Math.Min(f.G, f.B));
            bool gray = mx - mn < 40;
            if (!gray && f.B >= f.R && f.B >= f.G) return Link;
            if (gray) return Lum(f) < 60 ? Fg : DarkEngine.T(170, 178, 190);
            Color c = f;
            for (int i = 1; i <= 10 && Contrast(c, bg) < 4.5; i++)
                c = Color.FromArgb(f.R + (255 - f.R) * i / 10, f.G + (255 - f.G) * i / 10, f.B + (255 - f.B) * i / 10);
            return c;
        }

        static void Extra(Control c)
        {
            if (!c.IsHandleCreated) return;
            Type t = c.GetType();
            try { ActiproPopup(c, t); } catch { }
            if (TypeIs(t, "ActiproSoftware.SyntaxEditor.IntelliPromptMemberListBox"))
            {
                try { MemberListPainter.Attach((ListBox)c); } catch (Exception ex) { LogOnce("mlp", "Lista IntelliPrompt: " + ex.Message); }
            }
            if (editorOk)
            {
                try { EditorHook(c); }
                catch (Exception ex) { editorOk = false; Log("Editor (cores em memoria) desativado: " + ex.GetType().Name + ": " + ex.Message); }
            }
            IntPtr h = c.Handle;
            if (!hDone.ContainsKey(h))
            {
                if (TypeIs(t, "ActiproSoftware.WinUICore.ScrollBar"))
                {
                    // o renderer do Actipro abre o tema "Scrollbar" na janela da propria barra: DarkMode_Explorer -> barra escura
                    DarkScroll(c, "DarkMode_Explorer"); c.Invalidate(); hDone[h] = c;
                }
                else if (c is StatusBar)
                {
                    ThemeStatusBar(c); hDone[h] = c;
                }
                else if (!(c is Form) && !IsInfragistics(c) && !t.FullName.StartsWith("CefSharp.", StringComparison.Ordinal) && HasScrollStyle(h))
                {
                    // barras de rolagem nativas que aparecem depois (ex.: grade de Properties ao listar um objeto)
                    DarkScroll(c, c is ComboBox ? "DarkMode_CFD" : "DarkMode_Explorer");
                    try { SetWindowPos(h, IntPtr.Zero, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0004 | 0x0010 | 0x0020); } catch { } // SWP_FRAMECHANGED
                    hDone[h] = c;
                }
            }
            if (TypeIs(t, "Crownwood.Magic.Controls.TabControl")) CrownTabs(c);
            if (frameOk && !(c is Form) && !frameDone.Contains(h) && !t.FullName.StartsWith("CefSharp.", StringComparison.Ordinal))
            {
                frameDone.Add(h);
                try { FrameHook.Attach(c); }
                catch (Exception ex) { frameOk = false; Log("Molduras (pintura propria) desligadas: " + ex.Message); }
            }
        }

        static bool frameOk = true;
        static readonly HashSet<IntPtr> frameDone = new HashSet<IntPtr>();

        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);

        static bool editorOk = true;
        // metodo separado: se o assembly do Actipro nao carregar, so esta parte para (o resto do tema continua)
        [MethodImpl(MethodImplOptions.NoInlining)]
        static void EditorHook(Control c)
        {
            ActiproSoftware.SyntaxEditor.SyntaxEditor ed = c as ActiproSoftware.SyntaxEditor.SyntaxEditor;
            if (ed != null) EditorTheme.Apply(ed);
        }

        static void ThemeStatusBar(Control c)
        {
            try
            {
                IntPtr h = c.Handle;
                sbThemed[h] = c;
                SetWindowTheme(h, "", "");                                   // desenho classico (sem o tema claro do Windows)
                int cr = Chrome.R | (Chrome.G << 8) | (Chrome.B << 16);
                SendMessage(h, 0x2001 /*SB_SETBKCOLOR*/, IntPtr.Zero, new IntPtr(cr));
                if (IsLight(c.BackColor)) c.BackColor = Chrome;
                if (IsDarkText(c.ForeColor)) c.ForeColor = FgBright;
                // o texto classico usa a cor de sistema (preta): pintamos a barra nos mesmos (fundo + textos claros)
                StatusBar sb = c as StatusBar;
                if (sb != null) StatusBarPainter.Attach(sb);
                c.Invalidate();
                Log("StatusBar escurecida: " + c.GetType().FullName);
            }
            catch (Exception ex) { Log("StatusBar: " + ex.Message); }
        }

        static void CrownTabs(Control tc)
        {
            Type t = tc.GetType();
            if (crownColored.Add(tc))
            {
                Journal.SnapNamed(tc, "HotTextColor", "TextInactiveColor", "TextColor");
                SetColorProp(tc, "HotTextColor", FgBright);
                SetColorProp(tc, "TextInactiveColor", FgSoft);
                SetColorProp(tc, "TextColor", FgBright);
            }
            if (!crownHooked.ContainsKey(tc))
            {
                crownHooked[tc] = true;
                try
                {
                    EventInfo ei = t.GetEvent("SelectionChanged");
                    if (ei != null && ei.EventHandlerType == typeof(EventHandler))
                        ei.AddEventHandler(tc, new EventHandler(delegate(object s, EventArgs e) { try { if (On) CrownPages((Control)s); } catch { } }));
                }
                catch { }
            }
            // faixa das abas: sempre Panel no escuro (antes so era trocada se estivesse clara; um BackColor herdado/cinza ficava)
            try { if (tc.BackColor.ToArgb() != Panel.ToArgb()) { Journal.SnapNamed(tc, "BackColor"); tc.BackColor = Panel; } } catch { }
            CrownPages(tc);
            // botoes de seta/fechar: o Crownwood os recolore com _backIDE (cinza #808080 em fundo escuro) quando recalcula
            foreach (Control k in tc.Controls)
                if (k.GetType().Name == "InertButton" && k.BackColor.ToArgb() != Panel.ToArgb()) { Journal.Snap(k); k.BackColor = Panel; }
        }

        static void CrownPages(Control tc)
        {
            object pages = null;
            try { pages = tc.GetType().GetProperty("TabPages").GetValue(tc, null); } catch { }
            System.Collections.IEnumerable en = pages as System.Collections.IEnumerable;
            if (en == null) return;
            foreach (object pg in en)
            {
                try
                {
                    Type pt = pg.GetType();
                    PropertyInfo pc = pt.GetProperty("ColorTab"), ps = pt.GetProperty("Selected");
                    if (pc == null || ps == null) continue;
                    Journal.SnapNamed(pg, "ColorTab");
                    Color col = (Color)pc.GetValue(pg, null);
                    bool sel = (bool)ps.GetValue(pg, null);
                    if (sel)
                    {
                        // aba selecionada: o GeneXus usa ColorTab = White (pintada como retangulo branco)
                        if (IsLight(col)) { pc.SetValue(pg, Editor, null); crownMine[pg] = true; }
                    }
                    else if (crownMine.ContainsKey(pg) && col.ToArgb() == Editor.ToArgb())
                    {
                        pc.SetValue(pg, Color.White, null); // White = "sem cor": a aba usa o BackColor (escuro) do controle
                        crownMine.Remove(pg);
                    }
                    else if (IsLight(col) && col.ToArgb() != Color.White.ToArgb())
                    {
                        pc.SetValue(pg, Color.FromArgb((col.R + Chrome.R * 3) / 4, (col.G + Chrome.G * 3) / 4, (col.B + Chrome.B * 3) / 4), null);
                    }
                }
                catch { }
            }
        }

        // ---------- Navegadores embutidos ----------
        static string darkJsCache;
        static string DarkJs { get { string j = darkJsCache; if (j == null) darkJsCache = j = TH(DarkJsNext); return j; } }
        const string DarkJsNext =
            "(function(){try{" +
            "var u=(location.href||'').toLowerCase();" +
            "if(/previewimages\\/|_preview\\.html|\\/preview\\//.test(u)){return;}" +
            // editores DSO (Design System Object: Styles/Tokens) sao paginas Monaco empacotadas (sem window.monaco): ramo proprio abaixo
            "var gxDsoPage=/designstyle|designtoken|monaco[\\/\\\\]design/.test(u)&&!/preview/.test(u);" +
            "if(!gxDsoPage&&(document.designMode==='on'||document.querySelector('[contenteditable=true],[contenteditable=\"\"]')||/design|editor|layout/.test(u))){return;}" +
            "window.__gxDefs=window.__gxDefs||{};window.__gxMonUndo=function(){try{if(window.__gxMonTimer){clearInterval(window.__gxMonTimer);window.__gxMonTimer=0;}if(!window.__gxMonacoDark||!window.monaco)return;var od=window.__gxOD,os=window.__gxOS;monaco.editor.defineTheme=od;monaco.editor.setTheme=os;var D=window.__gxDefs;for(var k in D){try{od.call(monaco.editor,k,D[k]);}catch(e){}}window.__gxMonacoDark=0;var c=document.getElementById('gxMonCss');if(c&&c.parentNode)c.parentNode.removeChild(c);os.call(monaco.editor,window.__gxLast||(/outputpane/.test(u)?'outputTheme':'vs'));}catch(e){}};" +
            "function gxMon(){try{if(!window.monaco||!monaco.editor||!monaco.editor.defineTheme)return false;if(window.__gxMonacoDark)return true;window.__gxMonacoDark=1;" +
            "var C={'editor.background':'#11151C','editor.foreground':'#DCE2EB','editorGutter.background':'#11151C','minimap.background':'#11151C','editor.lineHighlightBackground':'#1B2433','editor.lineHighlightBorder':'#1B2433','editor.selectionBackground':'#264F78','editor.inactiveSelectionBackground':'#2C3C50','editorWidget.background':'#181F2A','editorWidget.border':'#333D47','input.background':'#1F2734','scrollbarSlider.background':'#79797966','scrollbarSlider.hoverBackground':'#646464B3','scrollbarSlider.activeBackground':'#BFBFBF66','editorOverviewRuler.border':'#11151C','editorCursor.foreground':'#F0F4FA','editorLink.activeForeground':'#5BA7FF'};" +
            "var M={'008800':'6A9955','ff0000':'F48771','000000':'DCE2EB','000080':'569CD6','0000ff':'569CD6','800000':'CE9178','a31515':'CE9178','808080':'A0A7B2','808000':'D7BA7D','ffa500':'FFA500'};" +
            "function dk(d){var o={base:'vs-dark',inherit:true,rules:[],colors:{}};d=d||{};var k;for(k in C)o.colors[k]=C[k];" +
            "(d.rules||[]).forEach(function(r){var n={};for(var q in r)n[q]=r[q];if(n.foreground){var f=String(n.foreground).replace('#','').toLowerCase();if(M[f])n.foreground=M[f];}if(n.background){var b=String(n.background).replace('#','');if(b.length>=6){var R=parseInt(b.substr(0,2),16),G=parseInt(b.substr(2,2),16),B=parseInt(b.substr(4,2),16);if(0.299*R+0.587*G+0.114*B>150)delete n.background;}}o.rules.push(n);});return o;}" +
            "var od=monaco.editor.defineTheme,os=monaco.editor.setTheme;window.__gxOD=od;window.__gxOS=os;var D=window.__gxDefs;for(var dn in D){try{od.call(monaco.editor,dn,dk(D[dn]));}catch(e){}}" +
            "monaco.editor.defineTheme=function(n,d){window.__gxDefs[n]=d;try{return od.call(monaco.editor,n,dk(d));}catch(e){return od.call(monaco.editor,n,d);}};" +
            "monaco.editor.setTheme=function(n){window.__gxLast=n;if(n==='vs')n='vs-dark';return os.call(monaco.editor,n);};" +
            "if(/outputpane/.test(u)){monaco.editor.defineTheme('outputTheme',{base:'vs',inherit:false,rules:[{token:'custom-info',foreground:'808080'},{token:'custom-error',foreground:'ff0000',fontStyle:'bold'},{token:'custom-warning',foreground:'FFA500'},{token:'custom-infomsg',foreground:'A5FF00'},{token:'start-section',foreground:'008800'}]});monaco.editor.setTheme('outputTheme');}" +
            "else{os.call(monaco.editor,'vs-dark');}" +
            "if(!document.getElementById('gxMonCss')){var st2=document.createElement('style');st2.id='gxMonCss';st2.textContent='.monaco-editor .minimap{border-left-color:#333D47 !important}';(document.head||document.documentElement).appendChild(st2);}" +
            "return true;}catch(e){return false;}}" +
            "if(!gxMon()&&!window.__gxMonTimer){var gxn=0;window.__gxMonTimer=setInterval(function(){if(gxMon()||++gxn>60){clearInterval(window.__gxMonTimer);window.__gxMonTimer=0;}},500);}" +
            // relatorios HTML do ReportView (Navigation View, Impact Analysis, List Database, Import): folha de estilo Next desenhada,
            // so de exibicao (um <style id=gxNextReport> no documento em memoria; nada e gravado; sem a extensao o relatorio volta ao original)
            "var gxl=document.getElementsByTagName('link'),gxrep=false;for(var gxk=0;gxk<gxl.length;gxk++){if(/genexus\\.css$/i.test(gxl[gxk].getAttribute('href')||''))gxrep=true;}" +
            "if(gxrep){if(!document.getElementById('gxNextReport')){var gxs=document.createElement('style');gxs.id='gxNextReport';gxs.textContent=':root{color-scheme:dark}html,body{background:#11151C !important;color:#DCE2EB !important}html{scrollbar-color:#3a4552 #11151c}::-webkit-scrollbar{width:12px;height:12px;background:#11151c}::-webkit-scrollbar-track{background:#11151c}::-webkit-scrollbar-thumb{background:#333d47;border:2px solid #11151c;border-radius:6px}::-webkit-scrollbar-thumb:hover{background:#56677a}::-webkit-scrollbar-corner{background:#11151c}::selection{background:#264F78;color:#F0F4FA}.MainHeaderTable{background:#181F2A !important;color:#F0F4FA !important;border:1px solid #333D47 !important;border-bottom:2px solid #2C8DCE !important}.MainHeaderTable td{color:#F0F4FA !important}.MainContentTable{background:#181F2A !important;border-color:#333D47 !important}.HeaderTable,.HeaderTableRight{background:#1B2330 !important;border-color:#333D47 !important}.HeaderTable td,.HeaderTableRight td{color:#F0F4FA !important;font-weight:600}.TableLevelInfo .HeaderTable,.HeaderTableRight,.AdvancedInfoCell .TableContainer{border-left-color:#333D47 !important}.MainInfoCell tr+tr,.AdvancedInfoCell tr+tr,[style*=\"6C6C6C\"],[style*=\"6c6c6c\"]{border-color:#2A333F !important}td,th,div,span,li,p{border-color:#333D47}.LabelText,.NavigationFilter,.subTitulosTabla,th{color:#8A909B !important}.gxobj,.gxlink,a[href]{color:#5BA7FF !important;text-decoration:none !important}.gxobj:hover,.gxlink:hover,a[href]:hover{color:#8CC2FF !important;text-decoration:underline !important}.gxobjnotexist,.ErrorText,.gxremoved{color:#E35861 !important}.ResultText,.NavigationWarning,.WarningText{color:#E5A54B !important}.CommandText,.NavigationWarning[data-gxn=kw]{color:#C792EA !important}.gxsource,.gxevent,.MessageText,.gxstatus{color:#DCE2EB !important}.gxnew{color:#A1DAA5 !important}';(document.head||document.documentElement).appendChild(gxs);}" +
            "var gxw=document.querySelectorAll('.NavigationWarning');for(var gxj=0;gxj<gxw.length;gxj++){var gxt=(gxw[gxj].textContent||'').replace(/^\\s+|\\s+$/g,'').toUpperCase();if(gxt==='WHEN'||gxt==='OTHERWISE')gxw[gxj].setAttribute('data-gxn','kw');}return;}" +
            "if(window.__gxdark){window.__gxdark();return;}" +
            "var G=window.__gxG=window.__gxG||1;window.__gxT=window.__gxT||[];function sp(el,p,v,i){if(!el.__gxp){el.__gxp={};window.__gxT.push(el);}if(!(p in el.__gxp))el.__gxp[p]=[el.style.getPropertyValue(p),el.style.getPropertyPriority(p)];el.style.setProperty(p,v,i||'');}" +
            "function rgb(s){var m=s&&s.match(/rgba?\\(([^)]+)\\)/);if(!m)return null;var p=m[1].split(',').map(parseFloat);return {r:p[0],g:p[1],b:p[2],a:p.length>3?p[3]:1};}" +
            "function L(c){return 0.299*c.r+0.587*c.g+0.114*c.b;}" +
            "function fix(el){if(!el||el.nodeType!==1||el.__gxd===G)return;if(el.closest&&el.closest('.monaco-editor')&&gxMon())return;el.__gxd=G;var t=el.tagName;if(t==='IMG'||t==='SVG'||t==='VIDEO'||t==='CANVAS'||t==='IFRAME')return;" +
            "var cs=getComputedStyle(el);var b=rgb(cs.backgroundColor);" +
            "if(b&&b.a>0.1&&L(b)>150){var tag=t;var col=(tag==='INPUT'||tag==='TEXTAREA'||tag==='SELECT'||tag==='BUTTON')?'#1f2734':(L(b)>235?'#11151c':'#181F2A');sp(el,'background-color',col,'important');}" +
            "if(cs.backgroundImage&&cs.backgroundImage.indexOf('gradient')>=0){sp(el,'background-image','none','important');}" +
            "var f=rgb(cs.color);if(f&&L(f)<130){sp(el,'color',t==='A'?'#5ba7ff':'#dce2eb','important');}" +
            "['Top','Right','Bottom','Left'].forEach(function(s){var bc=rgb(cs['border'+s+'Color']);if(bc&&parseFloat(cs['border'+s+'Width'])>0&&L(bc)>150)sp(el,'border-'+s.toLowerCase()+'-color','#333d47','important');});}" +
            "function all(){sp(document.documentElement,'background-color','#11151c','important');if(document.body){sp(document.body,'color','#dce2eb');}" +
            "var els=document.querySelectorAll('*');for(var i=0;i<els.length;i++)fix(els[i]);}" +
            "window.__gxUndo=function(){try{if(window.__gxObs){window.__gxObs.disconnect();window.__gxObs=null;}var c=document.getElementById('gxDarkCss');if(c&&c.parentNode)c.parentNode.removeChild(c);var T=window.__gxT||[];for(var i=0;i<T.length;i++){var e=T[i],o=e.__gxp;if(!o)continue;for(var p in o){if(o[p][0])e.style.setProperty(p,o[p][0],o[p][1]);else e.style.removeProperty(p);}e.__gxp=null;}window.__gxT=[];window.__gxdark=null;window.__gxG=(window.__gxG||1)+1;}catch(e){}};" +
            // DSO: folha de estilo escura no documento e nas shadow roots, cores de token (.mtkN) remapeadas para a paleta Next,
            // SetColorScheme('dark') da propria pagina quando existe; window.__gxDsoUndo (modo claro) remove tudo e volta ao esquema original
            "if(gxDsoPage){" +
            "window.__gxDsoUndo=function(){try{if(window.__gxDsoTimer){clearInterval(window.__gxDsoTimer);window.__gxDsoTimer=0;}var S=window.__gxDsoSt||[];for(var i=0;i<S.length;i++){try{if(S[i].parentNode)S[i].parentNode.removeChild(S[i]);}catch(e){}}window.__gxDsoSt=[];if(window.__gxDsoSch==='dark'&&typeof window.SetColorScheme==='function'){try{window.SetColorScheme('light');}catch(e){}try{document.documentElement.className=window.__gxDsoCls||'';}catch(e){}}window.__gxDsoSch='';window.__gxDsoOn=0;}catch(e){}};" +
            "var DM={'000000':'DCE2EB','000080':'569CD6','0000ff':'569CD6','0043b1':'569CD6','5b5bff':'9CDCFE','001080':'9CDCFE','0070c1':'4FC1FF','e50000':'9CDCFE','ff0000':'F48771','cd3131':'F48771','800000':'CE9178','a31515':'CE9178','008000':'6A9955','008800':'6A9955','13a432':'6A9955','098658':'B5CEA8','09885a':'B5CEA8','267f99':'4EC9B0','795e26':'DCDCAA','af00db':'C586C0','800080':'C586C0','808080':'A0A7B2','908880':'A0A7B2','a1a1a1':'A0A7B2','1818db':'569CD6','811f3f':'D7BA7D','3030c0':'569CD6','0451a5':'4FC1FF','fffffe':'11151C','ffffff':'DCE2EB'};" +
            "function dsoLum(h){var R=parseInt(h.substr(0,2),16),G=parseInt(h.substr(2,2),16),B=parseInt(h.substr(4,2),16);return 0.299*R+0.587*G+0.114*B;}" +
            "function dsoCol(h){h=h.toLowerCase();if(DM[h])return DM[h];if(dsoLum(h)>=130)return h;var R=parseInt(h.substr(0,2),16),G=parseInt(h.substr(2,2),16),B=parseInt(h.substr(4,2),16);for(var k=1;k<=10;k++){var r=R+(255-R)*k/10,g=G+(255-G)*k/10,b=B+(255-B)*k/10;if(0.299*r+0.587*g+0.114*b>=140){R=r;G=g;B=b;break;}}function x(v){v=Math.round(v).toString(16);return v.length<2?'0'+v:v;}return x(R)+x(G)+x(B);}" +
            "var DSOCSS=':root{color-scheme:dark}html,body{background:#11151C !important;color:#DCE2EB}'+" +
            "'.monaco-editor,.monaco-editor-background,.monaco-editor .margin,.monaco-editor .inputarea.ime-input,.monaco-editor .monaco-editor-background{background-color:#11151C !important}'+" +
            "'.monaco-editor .line-numbers{color:#8A909B !important}.monaco-editor .current-line ~ .line-numbers,.monaco-editor .line-numbers.active-line-number{color:#DCE2EB !important}'+" +
            "'.monaco-editor .view-overlays .current-line,.monaco-editor .margin-view-overlays .current-line-margin{border-color:#1B2433 !important;background-color:#1B2433 !important}'+" +
            "'.monaco-editor .cursor{background-color:#F0F4FA !important;border-color:#F0F4FA !important;color:#11151C !important}'+" +
            "'.monaco-editor .selected-text{background-color:#2C3C50 !important}.monaco-editor.focused .selected-text{background-color:#264F78 !important}'+" +
            "'.monaco-editor .minimap,.monaco-editor .minimap-shadow-visible{border-left-color:#333D47 !important}.minimap{border-left:1px solid #333D47 !important}'+" +
            "'.monaco-editor .suggest-widget,.monaco-editor .monaco-hover,.monaco-editor .parameter-hints-widget,.monaco-editor .find-widget,.monaco-editor .zone-widget,.monaco-menu .monaco-action-bar.vertical,.context-view .monaco-menu{background-color:#1F2734 !important;color:#DCE2EB !important;border-color:#333D47 !important}'+" +
            "'.monaco-editor .suggest-widget .monaco-list .monaco-list-row.focused,.monaco-editor .suggest-widget .monaco-list .monaco-list-row.focused .monaco-highlighted-label{background-color:#2C3C50 !important;color:#F0F4FA !important;outline:1px solid #2C8DCE !important}'+" +
            "'.monaco-editor .suggest-widget .monaco-list .monaco-list-row:hover{background-color:#344860 !important}.monaco-editor .suggest-widget .monaco-list .monaco-list-row .monaco-highlighted-label .highlight{color:#5BA7FF !important}'+" +
            "'.monaco-editor .suggest-widget .details,.monaco-editor .suggest-widget .details .header,.monaco-editor .monaco-hover code{background-color:#1F2734 !important;color:#DCE2EB !important}'+" +
            "'.monaco-editor .find-widget .monaco-inputbox,.monaco-inputbox{background-color:#11151C !important;color:#DCE2EB !important;border-color:#333D47 !important}'+" +
            "'.monaco-menu .monaco-action-bar.vertical .action-label,.monaco-menu .monaco-action-bar.vertical .keybinding{color:#DCE2EB !important}.monaco-menu .monaco-action-bar.vertical .action-item.focused>.action-label,.monaco-menu .monaco-action-bar.vertical .action-item.focused>a{background-color:#1F4E79 !important;color:#F0F4FA !important}'+" +
            "'.monaco-scrollable-element>.scrollbar>.slider{background:rgba(121,121,121,.4) !important}.monaco-editor .decorationsOverviewRuler{opacity:.6}'+" +
            "'::-webkit-scrollbar{width:12px;height:12px;background:#11151c}::-webkit-scrollbar-thumb{background:#333d47;border:2px solid #11151c;border-radius:6px}::-webkit-scrollbar-corner{background:#11151c}';" +
            "function dsoRoots(){var R=[document],q=[document];while(q.length){var r=q.shift();var els=r.querySelectorAll?r.querySelectorAll('*'):[];for(var i=0;i<els.length;i++){var sr=els[i].shadowRoot;if(sr&&R.indexOf(sr)<0){R.push(sr);q.push(sr);}}}return R;}" +
            "function dsoMtk(root){var css='',map={},ss=root.querySelectorAll('style');for(var i=0;i<ss.length;i++){var s=ss[i];if(s.id==='gxDsoCss'||s.id==='gxDsoMtk')continue;var t=s.textContent||'';if(t.indexOf('.mtk')<0)continue;var re=/\\.mtk(\\d+)\\s*\\{\\s*color:\\s*#([0-9a-fA-F]{6})\\s*;?\\s*\\}/g,m;while((m=re.exec(t))){map[m[1]]=m[2];}}for(var n in map){css+='.monaco-editor .mtk'+n+'{color:#'+dsoCol(map[n])+' !important}';}return css;}" +
            "function dsoPut(root,id,txt){var host=root===document?(document.head||document.documentElement):root;var el=root.getElementById?root.getElementById(id):root.querySelector('#'+id);if(!el){el=document.createElement('style');el.id=id;host.appendChild(el);(window.__gxDsoSt=window.__gxDsoSt||[]).push(el);}else if(el.parentNode===host&&host.lastChild!==el){host.appendChild(el);}if(el.textContent!==txt)el.textContent=txt;}" +
            "function dsoSkip(el){var st0=(el.getAttribute&&el.getAttribute('style'))||'',cl0=(typeof el.className==='string'?el.className:'').toLowerCase();if(/background/i.test(st0)||cl0.indexOf('swatch')>=0||cl0.indexOf('color-box')>=0)return true;for(var p=el;p&&p.nodeType===1;p=p.parentNode||p.host){var c=((p.id||'')+' '+(typeof p.className==='string'?p.className:'')+' '+p.tagName).toLowerCase();if(c.indexOf('monaco')>=0||c.indexOf('preview')>=0||p.tagName==='IFRAME')return true;if(!p.parentNode&&p.host)p=p.host;}return false;}" +
            "function gxDso(){try{window.__gxDsoOn=1;var R=dsoRoots(),ed=false;for(var i=0;i<R.length;i++){var r=R[i];var has=r.querySelector&&r.querySelector('.monaco-editor');if(has)ed=true;if(r===document||has||(r.querySelector&&r.querySelector('style.monaco-colors,style.gx-monaco-colors'))){dsoPut(r,'gxDsoCss',DSOCSS);dsoPut(r,'gxDsoMtk',dsoMtk(r));}if(r!==document&&r.querySelectorAll){var q=r.querySelectorAll('*');for(var j=0;j<q.length;j++){if(!dsoSkip(q[j]))fix(q[j]);}}}" +
            "if(ed&&typeof window.SetColorScheme==='function'&&window.__gxDsoSch!=='dark'){if(window.__gxDsoSeen){window.__gxDsoCls=document.documentElement.className;try{window.SetColorScheme('dark');window.__gxDsoSch='dark';}catch(e){window.__gxDsoSch='err';}}else window.__gxDsoSeen=1;}" +
            "}catch(e){}}" +
            "window.__gxdark=function(){gxDso();};" +
            "sp(document.documentElement,'background-color','#11151c','important');" +
            "var bq=document.querySelectorAll('body *');for(var bi=0;bi<bq.length;bi++){if(!dsoSkip(bq[bi]))fix(bq[bi]);}" +
            "gxDso();if(!window.__gxDsoTimer){var gdn=0;window.__gxDsoTimer=setInterval(function(){gdn++;if(gdn<=120||gdn%3===0)gxDso();},1000);}" +
            "return;}" +
            "window.__gxdark=all;all();" +
            "var st=document.getElementById('gxDarkCss');if(!st){st=document.createElement('style');st.id='gxDarkCss';st.textContent=':root{color-scheme:dark}html{scrollbar-color:#3a4552 #11151c}::-webkit-scrollbar{width:12px;height:12px;background:#11151c}::-webkit-scrollbar-track{background:#11151c}::-webkit-scrollbar-thumb{background:#333d47;border:2px solid #11151c;border-radius:6px}::-webkit-scrollbar-thumb:hover{background:#56677a}::-webkit-scrollbar-corner{background:#11151c}::placeholder{color:#A0A7B2 !important;opacity:1}';(document.head||document.documentElement).appendChild(st);}" +
            "try{var de=document.documentElement,ov=de.style.overflow;de.style.overflow='hidden';void de.offsetHeight;de.style.overflow=ov;void de.offsetHeight;}catch(e){}" +
            "if(window.__gxObs)window.__gxObs.disconnect();window.__gxObs=new MutationObserver(function(ms){ms.forEach(function(m){m.addedNodes&&m.addedNodes.forEach(function(n){if(n.nodeType===1){fix(n);var q=n.querySelectorAll?n.querySelectorAll('*'):[];for(var i=0;i<q.length;i++)fix(q[i]);}});});});window.__gxObs.observe(document.documentElement,{childList:true,subtree:true});" +
            "}catch(e){}})();";

        static MethodInfo cefExec;
        static bool cefExecSearched;

        static void Browser(Control c)
        {
            Type t = c.GetType();
            if (!t.FullName.EndsWith("ChromiumWebBrowser", StringComparison.Ordinal)) return;
            if (!browserHooked.Contains(c))
            {
                browserHooked.Add(c);
                CefBackground(c, t);
                try
                {
                    EventInfo ev = t.GetEvent("FrameLoadEnd");
                    if (ev != null)
                    {
                        MethodInfo h = typeof(DarkEngine).GetMethod("OnFrameLoadEnd", BindingFlags.NonPublic | BindingFlags.Static);
                        Delegate d = Delegate.CreateDelegate(ev.EventHandlerType, h);
                        ev.AddEventHandler(c, d);
                    }
                }
                catch (Exception ex) { Log("Cef hook: " + ex.Message); }
            }
            string addr = null;
            try { PropertyInfo pa = t.GetProperty("Address"); if (pa != null) addr = pa.GetValue(c, null) as string; } catch { }
            bool init = false, loading = true;
            try { init = (bool)t.GetProperty("IsBrowserInitialized").GetValue(c, null); } catch { }
            try { loading = (bool)t.GetProperty("IsLoading").GetValue(c, null); } catch { }
            if (!init || loading || addr == null) return;
            string last;
            if (browserUrls.TryGetValue(c, out last) && last == addr) return;
            browserUrls[c] = addr;
            if (IsPreviewUrl(addr)) { Log("Pre-visualizacao mantida com cores originais: " + addr); return; }
            RunCef(c);
            Log("Navegador escurecido (CSS): " + addr);
        }

        static void OnFrameLoadEnd(object sender, EventArgs e)
        { if (!On) return;
            try
            {
                bool main = true;
                string url = null;
                try { url = e.GetType().GetProperty("Url").GetValue(e, null) as string; } catch { }
                try
                {
                    object frame = e.GetType().GetProperty("Frame").GetValue(e, null);
                    main = (bool)frame.GetType().GetProperty("IsMain").GetValue(frame, null);
                    if (url == null) url = frame.GetType().GetProperty("Url").GetValue(frame, null) as string;
                }
                catch { }
                if (IsPreviewUrl(url)) return;
                if (main) RunCef(sender);
                else
                {
                    // sub-frames (iframes da Start Page etc.): mesmo script, executado no proprio frame
                    try
                    {
                        object frame = e.GetType().GetProperty("Frame").GetValue(e, null);
                        MethodInfo ex = frame.GetType().GetMethod("ExecuteJavaScriptAsync", new Type[] { typeof(string), typeof(string), typeof(int) });
                        if (ex != null && !string.IsNullOrEmpty(url) && !url.StartsWith("about:", StringComparison.OrdinalIgnoreCase))
                        {
                            ex.Invoke(frame, new object[] { DarkJs, url, 1 });
                            LogOnce("cefsub:" + url, "Navegador: sub-frame escurecido (CSS): " + url);
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        static void RunCef(object browser) { if (On) RunCefJs(browser, DarkJs); }

        static bool RunCefJs(object browser, string js)
        {
            try
            {
                if (!cefExecSearched)
                {
                    cefExecSearched = true;
                    foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        if (!a.GetName().Name.Equals("CefSharp", StringComparison.OrdinalIgnoreCase)) continue;
                        Type we = a.GetType("CefSharp.WebBrowserExtensions");
                        if (we == null) continue;
                        foreach (MethodInfo m in we.GetMethods(BindingFlags.Public | BindingFlags.Static))
                        {
                            if (m.Name != "ExecuteScriptAsync") continue;
                            ParameterInfo[] ps = m.GetParameters();
                            if (ps.Length == 2 && ps[1].ParameterType == typeof(string) && ps[0].ParameterType.IsInterface && ps[0].ParameterType.Name.Contains("WebBrowser"))
                            { cefExec = m; break; }
                        }
                    }
                    Log(cefExec != null ? "CefSharp ExecuteScriptAsync encontrado: " + cefExec.GetParameters()[0].ParameterType.FullName : "CefSharp ExecuteScriptAsync NAO encontrado");
                }
                if (cefExec != null && cefExec.GetParameters()[0].ParameterType.IsInstanceOfType(browser))
                { cefExec.Invoke(null, new object[] { browser, js }); return true; }
            }
            catch (Exception ex) { Log("Cef script: " + ex.Message); }
            return false;
        }

        static void IeBrowser(WebBrowser wb)
        {
            if (wb.ReadyState != WebBrowserReadyState.Complete || wb.Document == null) return;
            string addr = wb.Url != null ? wb.Url.ToString() : "about:blank";
            string last;
            if (browserUrls.TryGetValue(wb, out last) && last == addr) return;
            browserUrls[wb] = addr;
            if (IsPreviewUrl(addr)) { Log("Pre-visualizacao mantida com cores originais: " + addr); return; }
            try { wb.Document.InvokeScript("eval", new object[] { DarkJs }); Log("Navegador IE escurecido: " + addr); }
            catch (Exception ex) { Log("IE script: " + ex.Message); }
        }

        // ---------- Diagnostico ----------
        // ---------- Contraste de texto (WCAG >= 4.5:1) ----------
        // clareia a cor mantendo o matiz (avisos continuam laranja, erros vermelhos etc.)
        public static Color Lift(Color f, Color bg, double target)
        {
            if (Contrast(f, bg) >= target) return f;
            for (double t = 0.06; t <= 1.0; t += 0.06)
            {
                Color c = Color.FromArgb(f.A, (int)(f.R + (255 - f.R) * t), (int)(f.G + (255 - f.G) * t), (int)(f.B + (255 - f.B) * t));
                if (Contrast(c, bg) >= target) return c;
            }
            // inalcancavel (ex.: texto claro sobre o azul de destaque #2C8DCE, maximo 3.6:1 com branco): fica a cor de maior contraste
            Color best = f;
            foreach (Color c in new Color[] { Fg, Color.FromArgb(f.A, 255, 255, 255) }) if (Contrast(c, bg) > Contrast(best, bg)) best = c;
            return best;
        }

        static Color EffectiveBack(Control c)
        {
            for (Control p = c; p != null; p = p.Parent)
            {
                Color b = p.BackColor;
                if (b.A == 255 && !b.IsEmpty) return b;
            }
            return Panel;
        }

        static readonly HashSet<string> lowLogged = new HashSet<string>();
        static int contrastFixed;
        // textos de controles WinForms: sobe para >= 4.5:1 sobre o fundo real
        static void ContrastFix(Control c)
        {
            if (!(c is Label || c is ButtonBase || c is GroupBox || c is TabPage || c is StatusStrip || c is ToolStrip || c is LinkLabel
                || c is ComboBox || c is ListBox || c is TreeView || c is ListView || c is TextBoxBase || c is UpDownBase || c is StatusBar)) return;
            if (!c.Enabled || c.BackgroundImage != null) return;
            Color bg = EffectiveBack(c);
            if (IsLight(bg)) return; // area clara de proposito (nao escurecida): nao mexe
            Color f = c.ForeColor;
            Color n = TextFloor(f, bg, false);
            if (n.ToArgb() != f.ToArgb())
            {
                c.ForeColor = n; contrastFixed++;
                LogOnce("cfix:" + c.GetType().FullName + ":" + c.Name, "Contraste corrigido: " + c.GetType().FullName + " '" + c.Name + "' " + Hex(f) + " -> " + Hex(n) + " sobre " + Hex(bg));
            }
            LinkLabel ll = c as LinkLabel;
            if (ll != null && Contrast(ll.LinkColor, bg) < 4.5) ll.LinkColor = Lift(ll.LinkColor, bg, 4.6);
            ToolStrip ts = c as ToolStrip;
            if (ts != null)
                foreach (ToolStripItem it in ts.Items)
                {
                    if (!it.Enabled || it is ToolStripControlHost) continue;
                    Color ib = it.BackColor.A == 255 && !it.BackColor.IsEmpty ? it.BackColor : bg;
                    if (IsLight(ib)) continue;
                    Color itn = TextFloor(it.ForeColor, ib, false);
                    if (itn.ToArgb() != it.ForeColor.ToArgb()) { Color n2 = itn; LogOnce("cfixi:" + ts.Name + ":" + it.Name, "Contraste corrigido (item): " + it.GetType().Name + " '" + it.Name + "' " + Hex(it.ForeColor) + " -> " + Hex(n2)); it.ForeColor = n2; contrastFixed++; }
                }
        }

        // glifo preto/cinza escuro (sem familia de icones conhecida) em botao/rotulo/imagem sobre fundo escuro -> versao clara em memoria
        static readonly Dictionary<Control, Image> glyphOrig = new Dictionary<Control, Image>();
        static void GlyphFix(Control c)
        {
            ButtonBase bb = c as ButtonBase; Label lb = c as Label; PictureBox pb = c as PictureBox;
            if (bb == null && lb == null && pb == null) return;
            Image img = bb != null ? bb.Image : lb != null ? lb.Image : pb.Image;
            if (img == null || IconSwap.IsMonoResult(img) || IconSwap.Lookup(img) != null) return;
            if (IsLight(EffectiveBack(c))) return;
            Bitmap mv = IconSwap.MonoLift(img, false);
            if (mv == null) return;
            glyphOrig[c] = img;
            if (bb != null) bb.Image = mv; else if (lb != null) lb.Image = mv; else pb.Image = mv;
            LogOnce("glyph:" + c.GetType().FullName + ":" + c.Name, "Glifo escuro clareado em " + c.GetType().FullName + " '" + c.Name + "'");
        }

        // texto claro o bastante: >= 4.5:1 e, se for cinza/preto, no minimo ~#9AA3AE (desabilitado ~#7F8894); cores vivas so pelo contraste
        public static Color TextFloor(Color f, Color bg, bool disabled)
        {
            if (f.IsEmpty || f.A == 0 || IsLight(bg)) return f;
            Color n = Contrast(f, bg) < 4.5 ? Lift(f, bg, 4.6) : f;
            int sat = Math.Max(n.R, Math.Max(n.G, n.B)) - Math.Min(n.R, Math.Min(n.G, n.B));
            double floor = disabled ? 134 : 161;
            if (sat < 40 && Lum(n) < floor)
            {
                Color tgt = disabled ? DarkEngine.T(127, 136, 148) : DarkEngine.T(154, 163, 174);
                if (Lum(n) < 60) n = Color.FromArgb(n.A, tgt);   // preto/cinza muito escuro: vira o cinza claro da paleta
                else
                    for (double t = 0.05; t <= 1.0 && Lum(n) < floor; t += 0.05)
                        n = Color.FromArgb(f.A, (int)(f.R + (255 - f.R) * t), (int)(f.G + (255 - f.G) * t), (int)(f.B + (255 - f.B) * t));
            }
            return n;
        }

        static string Hex(Color c) { return "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2"); }

        // varredura: registra no log o que ainda tem contraste < 4.5:1 (uma vez por controle/cor)
        static void LowContrastScan(List<Form> forms)
        {
            int found = 0;
            foreach (Form f in forms)
            {
                if (f.IsDisposed || f.InvokeRequired || IsExcludedForm(f)) continue;
                Stack<Control> st = new Stack<Control>();
                st.Push(f);
                while (st.Count > 0)
                {
                    Control c = st.Pop();
                    if (c.IsDisposed || IsDesignHosted(c) || IsPreviewControl(c)) continue;
                    try
                    {
                        if (c.Visible && c.Enabled && !string.IsNullOrEmpty(c.Text) && !(c is Form) && !(c is TextBoxBase) && c.BackgroundImage == null && !c.GetType().FullName.StartsWith("CefSharp.", StringComparison.Ordinal))
                        {
                            Color bg = EffectiveBack(c), fg = c.ForeColor;
                            double cr = Contrast(fg, bg);
                            if (cr < 4.5 && !IsLight(bg))
                            {
                                string k = c.GetType().FullName + "|" + c.Name + "|" + fg.ToArgb() + "|" + bg.ToArgb();
                                if (lowLogged.Add(k)) { found++; Log("  BAIXO CONTRASTE: " + c.GetType().FullName + " '" + c.Name + "' texto " + Hex(fg) + " sobre " + Hex(bg) + " = " + cr.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + ":1"); }
                            }
                        }
                    }
                    catch { }
                    try { foreach (Control k in c.Controls) st.Push(k); } catch { }
                }
            }
            Log("Verificacao de contraste (passada " + passCount + "): " + found + " novo(s) item(ns) BAIXO CONTRASTE; correcoes automaticas ate agora = " + contrastFixed);
        }

        // ISL em memoria: toda cor de texto (normal/desabilitada) com contraste < 4.5:1 sobre o fundo do mesmo estado e clareada
        // Popup branco da seta de personalizacao das barras ("Add or Remove Buttons"): o papel QuickCustomizeToolbar tinha fundo
        // Transparent (cai no Control #F0F0F0) e AddRemoveToolsMenu nao existia. Em memoria: fundo #1F2734, texto #DCE2EB, borda #333D47.
        static void IslPopups(XmlDocument doc)
        {
            XmlElement styles = doc.SelectSingleNode("//styleSet[not(@basedOn)]/styles") as XmlElement;
            if (styles == null) return;
            int n = 0;
            foreach (string role in new string[] { "QuickCustomizeToolbar", "QatQuickCustomizeToolbar", "AddRemoveToolsMenu" })
            {
                XmlElement st = styles.SelectSingleNode("style[@role='" + role + "']") as XmlElement;
                if (st == null) { st = doc.CreateElement("style"); st.SetAttribute("role", role); styles.AppendChild(st); }
                XmlElement states = st.SelectSingleNode("states") as XmlElement;
                if (states == null) { states = doc.CreateElement("states"); st.AppendChild(states); }
                XmlElement nrm = states.SelectSingleNode("state[@name='Normal']") as XmlElement;
                if (nrm == null) { nrm = doc.CreateElement("state"); nrm.SetAttribute("name", "Normal"); states.AppendChild(nrm); }
                nrm.SetAttribute("backColor", IslRgb(T(31, 39, 52)));
                nrm.SetAttribute("foreColor", IslRgb(T(220, 226, 235)));
                nrm.SetAttribute("borderColor", IslRgb(T(51, 61, 71)));
                nrm.SetAttribute("borderColor2", IslRgb(T(51, 61, 71)));
                nrm.SetAttribute("borderColor3DBase", IslRgb(T(51, 61, 71)));
                nrm.SetAttribute("backGradientStyle", "None");
                nrm.SetAttribute("backHatchStyle", "None");
                n++;
            }
            Log("Popup de personalizacao das barras escuro (ISL em memoria): " + n + " papeis");
        }

        static void IslContrast(XmlDocument doc)
        {
            int n = 0;
            List<string> ex = new List<string>();
            foreach (XmlElement e in doc.SelectNodes("//*[@foreColor or @foreColorDisabled]"))
            {
                Color bg = IslColor(e.GetAttribute("backColor"));
                if (bg.IsEmpty)
                {
                    // sem fundo proprio: usa o fundo do estado Normal irmao, senao a barra (#242D3C)
                    XmlElement nrm = e.ParentNode == null ? null : e.ParentNode.SelectSingleNode("*[@name='Normal']") as XmlElement;
                    bg = nrm == null ? Color.Empty : IslColor(nrm.GetAttribute("backColor"));
                    if (bg.IsEmpty) bg = Chrome;
                }
                foreach (string attr in new string[] { "foreColor", "foreColorDisabled" })
                {
                    Color f = IslColor(e.GetAttribute(attr));
                    if (f.IsEmpty) continue;
                    Color b = bg;
                    if (attr == "foreColorDisabled") { Color bd = IslColor(e.GetAttribute("backColorDisabled")); if (!bd.IsEmpty) b = bd; }
                    if (IsLight(b)) continue;
                    Color nf = TextFloor(f, b, attr == "foreColorDisabled");
                    if (nf.ToArgb() == f.ToArgb()) continue;
                    if (Contrast(nf, b) < 4.5) LogOnce("islbaixo:" + Hex(nf) + Hex(b), "  BAIXO CONTRASTE (ISL, melhor possivel): " + e.GetAttribute("name") + "." + attr + " " + Hex(nf) + " sobre " + Hex(b) + " = " + Contrast(nf, b).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + ":1");
                    if (nf.ToArgb() == f.ToArgb()) continue;
                    e.SetAttribute(attr, nf.R + ", " + nf.G + ", " + nf.B);
                    n++;
                    if (ex.Count < 12) ex.Add(e.GetAttribute("name") + "." + attr + " " + Hex(f) + "->" + Hex(nf) + "/" + Hex(b));
                }
            }
            Log("Contraste do ISL (em memoria): " + n + " cor(es) de texto clareada(s) para >= 4.5:1" + (ex.Count > 0 ? " ex.: " + string.Join("; ", ex.ToArray()) : ""));
        }

        static string IslRgb(Color c) { return c.R + ", " + c.G + ", " + c.B; }

        static Color IslColor(string v)
        {
            if (string.IsNullOrEmpty(v)) return Color.Empty;
            string[] p = v.Split(',');
            int r, g, b;
            if (p.Length == 3 && int.TryParse(p[0].Trim(), out r) && int.TryParse(p[1].Trim(), out g) && int.TryParse(p[2].Trim(), out b)) return Color.FromArgb(r, g, b);
            if (p.Length == 4 && int.TryParse(p[1].Trim(), out r) && int.TryParse(p[2].Trim(), out g) && int.TryParse(p[3].Trim(), out b)) return Color.FromArgb(r, g, b);
            return Color.Empty;
        }

        static void ReportLight()
        {
            try
            {
                List<string> now = new List<string>();
                foreach (Control c in seen.Keys)
                {
                    try
                    {
                        if (c.IsDisposed || c.InvokeRequired || !c.Visible) continue;
                        if (IsInfragistics(c)) continue;
                        if (c is StatusBar || paintedPanes.Contains(c)) continue;
                        if (IsLight(c.BackColor))
                        {
                            string k = c.GetType().FullName + " (" + c.Name + ") back=" + ColorTranslator.ToHtml(c.BackColor);
                            if (stillLight.Add(k)) now.Add(k);
                        }
                    }
                    catch { }
                }
                Log("Diagnostico: " + seen.Count + " controles conhecidos, " + themedCount + " tratados, " + typesSeen.Count + " tipos.");
                foreach (string s in now) Log("  AINDA CLARO: " + s);
                if (passCount == 15)
                {
                    List<string> ts = new List<string>(typesSeen); ts.Sort();
                    Log("Tipos de controle encontrados: " + string.Join(", ", ts.ToArray()));
                }
            }
            catch (Exception ex) { Log("ReportLight: " + ex.Message); }
        }
    }

    // ---------- ToolStrip (menus/barras WinForms) ----------
    public class DarkColorTable : ProfessionalColorTable
    {
        static Color C { get { return DarkEngine.Chrome; } }
        static Color M { get { return DarkEngine.Chrome; } }
        static Color H { get { return DarkEngine.MenuHot; } }
        static Color HB { get { return DarkEngine.HotBg; } }
        static Color HBD { get { return DarkEngine.HotBorder; } }
        static Color B { get { return DarkEngine.Border; } }
        static Color A { get { return DarkEngine.Accent; } }
        static Color S { get { return DarkEngine.Sel; } }
        public DarkColorTable() { UseSystemColors = false; }
        public override Color ToolStripBorder { get { return C; } }
        public override Color ToolStripDropDownBackground { get { return M; } }
        public override Color ToolStripGradientBegin { get { return C; } }
        public override Color ToolStripGradientMiddle { get { return C; } }
        public override Color ToolStripGradientEnd { get { return C; } }
        public override Color ToolStripContentPanelGradientBegin { get { return C; } }
        public override Color ToolStripContentPanelGradientEnd { get { return C; } }
        public override Color ToolStripPanelGradientBegin { get { return C; } }
        public override Color ToolStripPanelGradientEnd { get { return C; } }
        public override Color MenuStripGradientBegin { get { return C; } }
        public override Color MenuStripGradientEnd { get { return C; } }
        public override Color MenuBorder { get { return HBD; } }
        public override Color MenuItemBorder { get { return H; } }
        public override Color MenuItemSelected { get { return H; } }
        public override Color MenuItemSelectedGradientBegin { get { return H; } }
        public override Color MenuItemSelectedGradientEnd { get { return H; } }
        public override Color MenuItemPressedGradientBegin { get { return C; } }
        public override Color MenuItemPressedGradientMiddle { get { return C; } }
        public override Color MenuItemPressedGradientEnd { get { return C; } }
        public override Color ImageMarginGradientBegin { get { return M; } }
        public override Color ImageMarginGradientMiddle { get { return M; } }
        public override Color ImageMarginGradientEnd { get { return M; } }
        public override Color ImageMarginRevealedGradientBegin { get { return M; } }
        public override Color ImageMarginRevealedGradientMiddle { get { return M; } }
        public override Color ImageMarginRevealedGradientEnd { get { return M; } }
        public override Color SeparatorDark { get { return B; } }
        public override Color SeparatorLight { get { return B; } }
        public override Color ButtonSelectedHighlight { get { return HB; } }
        public override Color ButtonSelectedHighlightBorder { get { return HBD; } }
        public override Color ButtonSelectedBorder { get { return HBD; } }
        public override Color ButtonSelectedGradientBegin { get { return HB; } }
        public override Color ButtonSelectedGradientMiddle { get { return HB; } }
        public override Color ButtonSelectedGradientEnd { get { return HB; } }
        public override Color ButtonPressedHighlight { get { return H; } }
        public override Color ButtonPressedHighlightBorder { get { return A; } }
        public override Color ButtonPressedBorder { get { return A; } }
        public override Color ButtonPressedGradientBegin { get { return H; } }
        public override Color ButtonPressedGradientMiddle { get { return H; } }
        public override Color ButtonPressedGradientEnd { get { return H; } }
        public override Color ButtonCheckedHighlight { get { return S; } }
        public override Color ButtonCheckedHighlightBorder { get { return A; } }
        public override Color ButtonCheckedGradientBegin { get { return S; } }
        public override Color ButtonCheckedGradientMiddle { get { return S; } }
        public override Color ButtonCheckedGradientEnd { get { return S; } }
        public override Color CheckBackground { get { return S; } }
        public override Color CheckSelectedBackground { get { return S; } }
        public override Color CheckPressedBackground { get { return H; } }
        public override Color GripDark { get { return DarkEngine.T(61, 73, 87); } }
        public override Color GripLight { get { return C; } }
        public override Color OverflowButtonGradientBegin { get { return C; } }
        public override Color OverflowButtonGradientMiddle { get { return C; } }
        public override Color OverflowButtonGradientEnd { get { return C; } }
        public override Color StatusStripGradientBegin { get { return C; } }
        public override Color StatusStripGradientEnd { get { return C; } }
        public override Color RaftingContainerGradientBegin { get { return C; } }
        public override Color RaftingContainerGradientEnd { get { return C; } }
    }
    // ---------- Editor de codigo (Actipro SyntaxEditor): cores SO EM MEMORIA ----------
    // Nada e gravado no TextEditorConfig_Default.xml nem nos objetos de configuracao do GeneXus:
    // recolorimos os estilos da linguagem ja carregada (Document.Language.HighlightingStyles) e o renderer do editor.
    // Se a extensao for desativada/removida, o GeneXus abre com as cores originais do arquivo, sem sobras.
    public static class EditorTheme
    {
        static readonly Dictionary<string, Color> Fore = new Dictionary<string, Color>();
        static readonly Dictionary<string, Color> Back = new Dictionary<string, Color>();
        static readonly Dictionary<string, Color> LangFore = new Dictionary<string, Color>();
        static readonly Dictionary<object, bool> renderers = new Dictionary<object, bool>();
        static readonly Dictionary<object, bool> languagesLogged = new Dictionary<object, bool>();
        static readonly HashSet<object> snappedSt = new HashSet<object>(RefEq.I);
        static readonly List<KeyValuePair<object, ActiproSoftware.SyntaxEditor.HighlightingStyle>> addedDefaults = new List<KeyValuePair<object, ActiproSoftware.SyntaxEditor.HighlightingStyle>>();
        static Color DefaultText { get { Tema t = DarkEngine.TemaAtual; return t.Sintaxe != null ? t.Sintaxe["texto"] : H("D4D4D4"); } }
        static Color SelBack { get { return DarkEngine.T(H("264F78")); } }
        static Color Whitespace { get { return DarkEngine.T(64, 72, 84); } }

        static Color H(string hex) { return Color.FromArgb(255, Convert.ToInt32(hex.Substring(0, 2), 16), Convert.ToInt32(hex.Substring(2, 2), 16), Convert.ToInt32(hex.Substring(4, 2), 16)); }

        static EditorTheme() { Rebuild(); }

        // cores da sintaxe do tema atual: Next = VS Code Dark+ (v1); temas do Filipe = tabela de tokens do tema
        public static void Rebuild()
        {
            Fore.Clear(); Back.Clear(); LangFore.Clear(); snappedSt.Clear();
            // tokens estilo VS Code Dark+ (mesmas cores que o Ativar antigo gravava no arquivo)
            string[] f = {
                "DefaultStyle", "D4D4D4", "BracketHighlightingStyle", "FFFFFF", "CodeSnippetDependentFieldStyle", "D4D4D4", "CodeSnippetFieldStyle", "FFFFFF",
                "CollapsibleTextStyle", "808080", "CommentDefaultStyle", "6A9955", "CommentDelimiterStyle", "6A9955", "CommentURLStyle", "6A9955",
                "DefWordStyle", "C586C0", "DeprecatedWordStyle", "D7BA7D", "FcnReservedWordStyle", "DCDCAA", "FcnSomeGenReservedWordStyle", "F48771",
                "KeyWordStyle", "569CD6", "NumberStyle", "B5CEA8", "OperatorStyle", "D4D4D4", "ReservedWordStyle", "4EC9B0",
                "StringDefaultStyle", "CE9178", "StringDelimiterStyle", "CE9178", "VariableReplaceStyle", "FFFFFF", "VariableStyle", "9CDCFE" };
            for (int i = 0; i < f.Length; i += 2) Fore[f[i]] = H(f[i + 1]);
            // linguagens filhas do Actipro (HTML/XML com <script>/<style>/CDATA: HTMLDefinition, JScript, CSS, VBScript do Packages)
            string[] g = {
                "TagDelimiterStyle", "808080", "TagNameStyle", "569CD6", "TagAttributeStyle", "9CDCFE", "TagAttributeValueStyle", "CE9178",
                "EntityStyle", "D7BA7D", "PropertyNameStyle", "9CDCFE", "ProcessingInstructionDelimiterStyle", "808080", "ProcessingInstructionDefaultStyle", "C586C0",
                "DeclarationDelimiterStyle", "808080", "DeclarationDefaultStyle", "C586C0", "ASPDelimiterStyle", "DCDCAA",
                "PropertyDelimiterStyle", "D4D4D4", "PropertyDefaultStyle", "9CDCFE", "ValueDelimiterStyle", "D4D4D4", "ValueDefaultStyle", "CE9178" };
            for (int i = 0; i < g.Length; i += 2) Fore[g[i]] = H(g[i + 1]);
            // a mesma chave com outro significado conforme a linguagem (ex.: ReservedWordStyle = palavra-chave no JavaScript)
            string[] l = {
                "JScript|ReservedWordStyle", "569CD6", "JScript|NativeTypeStyle", "4EC9B0", "JScript|OperatorStyle", "D4D4D4",
                "VBScript|ReservedWordStyle", "569CD6", "VBScript|NativeTypeStyle", "4EC9B0", "VBScript|ReservedWordOperatorStyle", "569CD6",
                "VBScript|PreProcessorDirectiveKeywordStyle", "C586C0", "VBScript|PreProcessorDirectiveDefaultStyle", "D4D4D4",
                "CSS|DefaultStyle", "D7BA7D" };
            for (int i = 0; i < l.Length; i += 2) LangFore[l[i]] = H(l[i + 1]);
            Back["CodeSnippetFieldStyle"] = DarkEngine.T(H("264F78"));
            Back["VariableReplaceStyle"] = H("613214");
            Tema t = DarkEngine.TemaAtual;
            if (t.Sintaxe != null)
            {
                foreach (string k in new List<string>(Fore.Keys)) { string tok = Temas.TokenDe("", k); if (tok != null) Fore[k] = t.Sintaxe[tok]; }
                foreach (string k in new List<string>(LangFore.Keys)) { int i = k.IndexOf('|'); string tok = Temas.TokenDe(k.Substring(0, i), k.Substring(i + 1)); if (tok != null) LangFore[k] = t.Sintaxe[tok]; }
            }
        }

        public static void Apply(ActiproSoftware.SyntaxEditor.SyntaxEditor ed)
        {
            bool changed = false;
            if (darkRendererOk)
            {
                try { changed |= UseDarkRenderer(ed); }
                catch (Exception ex) { darkRendererOk = false; DarkEngine.Log("Editor: renderer proprio desligado: " + ex.Message); }
            }
            try { changed |= ThemeRenderer(ed.RendererResolved); } catch (Exception ex) { DarkEngine.Log("Editor renderer: " + ex.Message); }
            try
            {
                ActiproSoftware.SyntaxEditor.Document d = ed.Document;
                if (d != null && d.Language != null) changed |= ThemeLanguage(d.Language, 0);
            }
            catch (Exception ex) { DarkEngine.Log("Editor linguagem: " + ex.Message); }
            if (changed) { try { ed.Invalidate(true); } catch { } }
        }

        static bool darkRendererOk = true;
        static readonly Dictionary<object, DarkVs2005Renderer> darkFor = new Dictionary<object, DarkVs2005Renderer>();

        // troca (so neste editor, em memoria) o renderer VS2005 padrao por uma copia com os botoes de divisao escuros
        static bool UseDarkRenderer(ActiproSoftware.SyntaxEditor.SyntaxEditor ed)
        {
            ActiproSoftware.SyntaxEditor.ISyntaxEditorRenderer cur = ed.RendererResolved;
            if (cur == null || cur is DarkVs2005Renderer) return false;
            if (cur.GetType() != typeof(ActiproSoftware.SyntaxEditor.VisualStudio2005SyntaxEditorRenderer)) return false;
            if (ed.Renderer != null && !object.ReferenceEquals(ed.Renderer, cur)) return false;
            DarkVs2005Renderer d;
            if (!darkFor.TryGetValue(cur, out d))
            {
                d = new DarkVs2005Renderer();
                int n = 0;
                foreach (PropertyInfo p in typeof(ActiproSoftware.SyntaxEditor.VisualStudio2005SyntaxEditorRenderer).GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (!p.CanRead || !p.CanWrite || p.GetIndexParameters().Length > 0) continue;
                    try { p.SetValue(d, p.GetValue(cur, null), null); n++; } catch { }
                }
                darkFor[cur] = d;
                DarkEngine.Log("Editor: renderer escuro proprio criado (" + n + " propriedades copiadas do original)");
            }
            Journal.SnapNamed(ed, "Renderer");
            ed.Renderer = d;
            return true;
        }

        static bool Same(Color a, Color b) { return a.ToArgb() == b.ToArgb() && a.IsSystemColor == b.IsSystemColor; }

        // devolve true se mudou alguma cor (GeneXus pode reaplicar as cores do arquivo, ex.: depois de Tools > Options)
        public static bool ThemeLanguage(ActiproSoftware.SyntaxEditor.SyntaxLanguage lang, int depth)
        {
            if (lang == null || depth > 4) return false;
            bool changed = false;
            string lk = lang.Key ?? "";
            // fundo da regiao da linguagem filha (ex.: <script><![CDATA[ ... ]]> com ChildLanguageBackColor="WhiteSmoke"):
            // o Actipro pinta essa regiao com SyntaxLanguage.BackColor -> no escuro, o mesmo fundo do editor
            try
            {
                Color lb = lang.BackColor;
                if (!lb.IsEmpty && lb.ToArgb() != DarkEngine.Editor.ToArgb() && (lb.IsSystemColor || DarkEngine.IsLight(lb) || DarkEngine.Lum(lb) > 60))
                {
                    Journal.SnapNamed(lang, "BackColor");
                    lang.BackColor = DarkEngine.Editor;
                    changed = true;
                    DarkEngine.LogOnce("langback:" + lk, "Editor: fundo da regiao da linguagem filha " + lk + " (" + lb.Name + ") trocado pelo fundo do editor");
                }
            }
            catch { }
            ActiproSoftware.SyntaxEditor.HighlightingStyleCollection hs = lang.HighlightingStyles;
            for (int i = 0; i < hs.Count; i++)
            {
                ActiproSoftware.SyntaxEditor.HighlightingStyle st = hs[i];
                if (st == null) continue;
                if (DarkEngine.On && snappedSt.Add(st)) Journal.SnapNamed(st, "ForeColor", "BackColor", "BorderColor");
                string k = st.Key ?? "";
                Color fore, back;
                Color cf = st.ForeColor, cb = st.BackColor;
                if (LangFore.TryGetValue(lk + "|" + k, out fore) || Fore.TryGetValue(k, out fore))
                {
                    if (!Same(cf, fore)) { st.ForeColor = fore; changed = true; }
                    Color bc = k == "BracketHighlightingStyle" ? H("808080") : fore;
                    if (!st.BorderColor.IsEmpty && !Same(st.BorderColor, bc)) { st.BorderColor = bc; changed = true; }
                }
                else if (cf.IsSystemColor || DarkEngine.IsDarkText(cf))
                {
                    // linguagens/estilos fora da lista (ex.: XML, HTML, outras definicoes): so garante leitura no fundo escuro
                    Color nf = cf.IsEmpty ? DefaultText : DarkEngine.Readable(Color.FromArgb(cf.ToArgb()), DarkEngine.Editor);
                    if (cf.IsSystemColor && !DarkEngine.IsDarkText(cf) && !DarkEngine.IsLight(cf)) nf = Color.FromArgb(cf.ToArgb());
                    if (!Same(cf, nf)) { st.ForeColor = nf; changed = true; }
                    if (!st.BorderColor.IsEmpty && DarkEngine.IsDarkText(st.BorderColor)) { st.BorderColor = nf; changed = true; }
                }
                if (Back.TryGetValue(k, out back))
                {
                    if (!Same(cb, back)) { st.BackColor = back; changed = true; }
                }
                else if (!cb.IsEmpty && (cb.IsSystemColor || DarkEngine.IsLight(cb)))
                {
                    // "Window" do arquivo = cor de sistema (branca): troca pelo fundo do editor, explicito
                    if (!Same(cb, DarkEngine.Editor)) { st.BackColor = DarkEngine.Editor; changed = true; }
                }
            }
            // o estilo padrao do texto (identificadores) precisa existir; sem ele o Actipro usa a cor de sistema (preta)
            if (hs["DefaultStyle"] == null)
            {
                try { ActiproSoftware.SyntaxEditor.HighlightingStyle ns = new ActiproSoftware.SyntaxEditor.HighlightingStyle("DefaultStyle", "Default Text", DefaultText, Color.Empty); hs.Add(ns); addedDefaults.Add(new KeyValuePair<object, ActiproSoftware.SyntaxEditor.HighlightingStyle>(hs, ns)); changed = true; }
                catch { }
            }
            try
            {
                ActiproSoftware.SyntaxEditor.SyntaxLanguageCollection kids = lang.ChildLanguages;
                if (kids != null) for (int i = 0; i < kids.Count; i++) changed |= ThemeLanguage(kids[i], depth + 1);
            }
            catch { }
            if (changed)
            {
                bool first;
                lock (languagesLogged) { first = !languagesLogged.ContainsKey(lang); languagesLogged[lang] = true; }
                DarkEngine.Log((first ? "Editor: cores escuras aplicadas em memoria na linguagem " : "Editor: cores reaplicadas (o GeneXus recarregou as do arquivo) na linguagem ") + lang.Key);
            }
            return changed;
        }

        static void Fill(ref bool ch, ActiproSoftware.Drawing.BackgroundFill cur, Color want, Action<ActiproSoftware.Drawing.BackgroundFill> set)
        {
            ActiproSoftware.Drawing.SolidColorBackgroundFill s = cur as ActiproSoftware.Drawing.SolidColorBackgroundFill;
            if (s != null && !s.Color.IsSystemColor && s.Color.ToArgb() == want.ToArgb()) return;
            set(new ActiproSoftware.Drawing.SolidColorBackgroundFill(want));
            ch = true;
        }

        static void Col(ref bool ch, Color cur, Color want, Action<Color> set)
        {
            if (!cur.IsSystemColor && cur.ToArgb() == want.ToArgb()) return;
            set(want);
            ch = true;
        }

        public static bool ThemeRenderer(ActiproSoftware.SyntaxEditor.ISyntaxEditorRenderer r)
        {
            ActiproSoftware.SyntaxEditor.SyntaxEditorRenderer b = r as ActiproSoftware.SyntaxEditor.SyntaxEditorRenderer;
            if (b == null) { if (r != null) DarkEngine.Log("Editor: renderer nao suportado " + r.GetType().FullName); return false; }
            bool ch = false;
            if (!(b is DarkVs2005Renderer) && snappedSt.Add(b)) Journal.SnapAll(b, null);
            // sem isso, uma troca de cores do Windows faria o Actipro voltar tudo para as cores de sistema
            if (b.ResetAllPropertiesOnSystemColorChange) { b.ResetAllPropertiesOnSystemColorChange = false; ch = true; }
            Col(ref ch, b.SelectionFocusedBackColor, SelBack, delegate(Color c) { b.SelectionFocusedBackColor = c; });
            Col(ref ch, b.SelectionFocusedForeColor, Color.White, delegate(Color c) { b.SelectionFocusedForeColor = c; });
            Col(ref ch, b.SelectionUnfocusedBackColor, DarkEngine.Sel, delegate(Color c) { b.SelectionUnfocusedBackColor = c; });
            Col(ref ch, b.SelectionUnfocusedForeColor, DarkEngine.Fg, delegate(Color c) { b.SelectionUnfocusedForeColor = c; });
            Col(ref ch, b.IndentationGuideColor, DarkEngine.Border, delegate(Color c) { b.IndentationGuideColor = c; });
            Col(ref ch, b.ContentDividerColor, DarkEngine.Border, delegate(Color c) { b.ContentDividerColor = c; });
            Col(ref ch, b.OutliningIndicatorBackColor, DarkEngine.Editor, delegate(Color c) { b.OutliningIndicatorBackColor = c; });
            Col(ref ch, b.OutliningIndicatorForeColor, DarkEngine.FgDim, delegate(Color c) { b.OutliningIndicatorForeColor = c; });
            Col(ref ch, b.WhitespaceSpaceForeColor, Whitespace, delegate(Color c) { b.WhitespaceSpaceForeColor = c; });
            Col(ref ch, b.WhitespaceTabForeColor, Whitespace, delegate(Color c) { b.WhitespaceTabForeColor = c; });
            Col(ref ch, b.WordWrapGlyphColor, DarkEngine.FgDim, delegate(Color c) { b.WordWrapGlyphColor = c; });
            if (!b.CurrentLineHighlightBackColor.IsEmpty && (b.CurrentLineHighlightBackColor.IsSystemColor || DarkEngine.IsLight(b.CurrentLineHighlightBackColor)))
            { b.CurrentLineHighlightBackColor = DarkEngine.T(28, 35, 46); ch = true; }
            if (!b.CurrentLineHighlightBorderColor.IsEmpty && (b.CurrentLineHighlightBorderColor.IsSystemColor || DarkEngine.IsLight(b.CurrentLineHighlightBorderColor)))
            { b.CurrentLineHighlightBorderColor = DarkEngine.Border; ch = true; }

            ActiproSoftware.SyntaxEditor.VisualStudio2005SyntaxEditorRenderer v = r as ActiproSoftware.SyntaxEditor.VisualStudio2005SyntaxEditorRenderer;
            if (v != null)
            {
                Color ed = DarkEngine.Editor, pn = DarkEngine.Panel, ch2 = DarkEngine.Chrome;
                Fill(ref ch, v.TextAreaBackgroundFill, ed, delegate(ActiproSoftware.Drawing.BackgroundFill f) { v.TextAreaBackgroundFill = f; });
                Fill(ref ch, v.TextAreaDisabledBackgroundFill, pn, delegate(ActiproSoftware.Drawing.BackgroundFill f) { v.TextAreaDisabledBackgroundFill = f; });
                Fill(ref ch, v.LineNumberMarginBackgroundFill, ed, delegate(ActiproSoftware.Drawing.BackgroundFill f) { v.LineNumberMarginBackgroundFill = f; });
                Fill(ref ch, v.LineNumberMarginDisabledBackgroundFill, pn, delegate(ActiproSoftware.Drawing.BackgroundFill f) { v.LineNumberMarginDisabledBackgroundFill = f; });
                Fill(ref ch, v.IndicatorMarginBackgroundFill, ed, delegate(ActiproSoftware.Drawing.BackgroundFill f) { v.IndicatorMarginBackgroundFill = f; });
                Fill(ref ch, v.SelectionMarginBackgroundFill, ed, delegate(ActiproSoftware.Drawing.BackgroundFill f) { v.SelectionMarginBackgroundFill = f; });
                Fill(ref ch, v.SelectionMarginDisabledBackgroundFill, pn, delegate(ActiproSoftware.Drawing.BackgroundFill f) { v.SelectionMarginDisabledBackgroundFill = f; });
                Fill(ref ch, v.UserMarginBackgroundFill, ed, delegate(ActiproSoftware.Drawing.BackgroundFill f) { v.UserMarginBackgroundFill = f; });
                Fill(ref ch, v.UserMarginDisabledBackgroundFill, pn, delegate(ActiproSoftware.Drawing.BackgroundFill f) { v.UserMarginDisabledBackgroundFill = f; });
                Fill(ref ch, v.WordWrapMarginBackgroundFill, pn, delegate(ActiproSoftware.Drawing.BackgroundFill f) { v.WordWrapMarginBackgroundFill = f; });
                Fill(ref ch, v.SplitterBackgroundFill, ch2, delegate(ActiproSoftware.Drawing.BackgroundFill f) { v.SplitterBackgroundFill = f; });
                Fill(ref ch, v.ScrollBarBlockBackgroundFill, ch2, delegate(ActiproSoftware.Drawing.BackgroundFill f) { v.ScrollBarBlockBackgroundFill = f; });
                Col(ref ch, v.LineNumberMarginForeColor, DarkEngine.FgDim, delegate(Color c) { v.LineNumberMarginForeColor = c; });
                Col(ref ch, v.LineNumberMarginBorderColor, ed, delegate(Color c) { v.LineNumberMarginBorderColor = c; });
                Col(ref ch, v.IndicatorMarginBorderColor, ed, delegate(Color c) { v.IndicatorMarginBorderColor = c; });
                Col(ref ch, v.SelectionMarginBorderColor, ed, delegate(Color c) { v.SelectionMarginBorderColor = c; });
                Col(ref ch, v.UserMarginBorderColor, ed, delegate(Color c) { v.UserMarginBorderColor = c; });
                Col(ref ch, v.UserMarginForeColor, DarkEngine.FgDim, delegate(Color c) { v.UserMarginForeColor = c; });
                Col(ref ch, v.WordWrapMarginBorderColor, DarkEngine.Border, delegate(Color c) { v.WordWrapMarginBorderColor = c; });
                // moldura do editor (SimpleBorder ControlDark = linha clara a direita/embaixo)
                ActiproSoftware.Drawing.SimpleBorder sbd = v.Border as ActiproSoftware.Drawing.SimpleBorder;
                if (sbd != null && (sbd.Color.IsSystemColor || sbd.Color.ToArgb() != DarkEngine.Border.ToArgb()))
                { v.Border = new ActiproSoftware.Drawing.SimpleBorder(sbd.Style, DarkEngine.Border); ch = true; }
            }
            if (ch)
            {
                bool first;
                lock (renderers) { first = !renderers.ContainsKey(r); renderers[r] = true; }
                DarkEngine.Log((first ? "Editor: renderer escurecido em memoria: " : "Editor: renderer reaplicado: ") + r.GetType().FullName);
            }
            return ch;
        }
        // modo claro ao vivo (as cores ja voltaram pelo Journal): tira o estilo DefaultStyle que o tema criou
        public static void Undo()
        {
            int n = 0;
            foreach (KeyValuePair<object, ActiproSoftware.SyntaxEditor.HighlightingStyle> kv in addedDefaults)
            {
                bool removed = false;
                try
                {
                    MethodInfo rm = kv.Key.GetType().GetMethod("Remove", new Type[] { typeof(ActiproSoftware.SyntaxEditor.HighlightingStyle) });
                    if (rm != null) { rm.Invoke(kv.Key, new object[] { kv.Value }); removed = true; }
                    else { System.Collections.IList il = kv.Key as System.Collections.IList; if (il != null) { il.Remove(kv.Value); removed = true; } }
                }
                catch { }
                if (!removed) { try { kv.Value.ForeColor = SystemColors.WindowText; } catch { } }
                n++;
            }
            addedDefaults.Clear();
            snappedSt.Clear();
            lock (languagesLogged) languagesLogged.Clear();
            lock (renderers) renderers.Clear();
            if (n > 0) DarkEngine.Log("Editor (modo claro): " + n + " estilo(s) DefaultStyle criados pelo tema retirados");
        }
    }


    // ---------- Lista de auto-completar do Actipro (IntelliPromptMemberListBox) ----------
    // O OnDrawItem do Actipro desenha com SystemBrushes.Window/Highlight e SystemColors.WindowText fixos (lista branca).
    // Subclasse nativa (em memoria): no escuro intercepta o WM_DRAWITEM refletido (0x202B) e desenha o item nas cores do Next;
    // no claro (DarkEngine.On = false) a mensagem segue intacta para o desenho original do Actipro.
    public sealed class MemberListPainter : NativeWindow
    {
        static readonly Dictionary<ListBox, MemberListPainter> all = new Dictionary<ListBox, MemberListPainter>();
        static readonly Dictionary<ImageList, Bitmap[]> icons = new Dictionary<ImageList, Bitmap[]>();
        static PropertyInfo pImages, pVirtual, pImgIdx, pText;
        public static Color Back { get { return DarkEngine.Input; } }     // #1F2734
        public static Color SelBack { get { return DarkEngine.Sel; } }    // #2C3C50
        public static Color SelBorder { get { return DarkEngine.Accent; } } // #2C8DCE
        public static Color Text { get { return DarkEngine.Fg; } }        // #DCE2EB
        public static int Drawn;
        readonly ListBox lb;
        bool failed;

        [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)]
        struct DRAWITEMSTRUCT { public int CtlType; public int CtlID; public int itemID; public int itemAction; public int itemState; public IntPtr hwndItem; public IntPtr hDC; public RECT rcItem; public IntPtr itemData; }

        MemberListPainter(ListBox l) { lb = l; }

        public static void Attach(ListBox l)
        {
            if (l == null || l.IsDisposed || !l.IsHandleCreated) return;
            MemberListPainter p;
            lock (all)
            {
                if (!all.TryGetValue(l, out p))
                {
                    p = new MemberListPainter(l);
                    all[l] = p;
                    l.HandleCreated += OnHandleCreated;
                    l.HandleDestroyed += OnHandleDestroyed;
                    l.Disposed += OnDisposed;
                    DarkEngine.LogOnce("mlp-attach", "Lista IntelliPrompt (auto-completar): desenho escuro proprio ligado (subclasse em memoria)");
                }
            }
            if (p.Handle == IntPtr.Zero) p.AssignHandle(l.Handle);
            if (!DarkEngine.On) return;
            // fundo da area sem itens e moldura do popup (valores originais no Journal -> modo claro devolve exatamente)
            if (l.BackColor.ToArgb() != Back.ToArgb()) { Journal.SnapNamed(l, "BackColor", "ForeColor"); l.BackColor = Back; l.ForeColor = Text; }
            Form f = l.FindForm();
            if (f != null && f.BackColor.ToArgb() != Back.ToArgb()) { Journal.SnapNamed(f, "BackColor"); f.BackColor = Back; }
            l.Invalidate();
        }

        static MemberListPainter Of(object s) { ListBox l = s as ListBox; MemberListPainter p = null; if (l != null) lock (all) all.TryGetValue(l, out p); return p; }
        static void OnHandleCreated(object s, EventArgs e) { MemberListPainter p = Of(s); if (p != null && p.Handle == IntPtr.Zero) try { p.AssignHandle(((Control)s).Handle); } catch { } }
        static void OnHandleDestroyed(object s, EventArgs e) { MemberListPainter p = Of(s); if (p != null && p.Handle != IntPtr.Zero) try { p.ReleaseHandle(); } catch { } }
        static void OnDisposed(object s, EventArgs e)
        {
            MemberListPainter p = Of(s);
            ListBox l = s as ListBox;
            if (l != null) lock (all) all.Remove(l);
            if (p != null && p.Handle != IntPtr.Zero) try { p.ReleaseHandle(); } catch { }
        }

        public static int Count { get { lock (all) return all.Count; } }

        // modo claro: so redesenha (o desenho original volta porque WndProc deixa passar quando On = false)
        public static void Light()
        {
            List<ListBox> l; lock (all) l = new List<ListBox>(all.Keys);
            foreach (ListBox x in l) { try { if (!x.IsDisposed && x.IsHandleCreated) x.Invalidate(); } catch { } }
            lock (icons) { foreach (Bitmap[] a in icons.Values) foreach (Bitmap b in a) if (b != null) b.Dispose(); icons.Clear(); }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x202B && DarkEngine.On && !failed)
            {
                try { if (Draw(m.LParam)) { m.Result = (IntPtr)1; return; } }
                catch (Exception ex) { failed = true; DarkEngine.Log("Lista IntelliPrompt: desenho proprio desligado (" + ex.GetType().Name + ": " + ex.Message + ")"); }
            }
            base.WndProc(ref m);
        }

        static object Prop(ref PropertyInfo cache, object o, string n)
        {
            if (cache == null || !cache.DeclaringType.IsInstanceOfType(o)) cache = o.GetType().GetProperty(n, BindingFlags.Instance | BindingFlags.Public);
            return cache == null ? null : cache.GetValue(o, null);
        }

        bool Draw(IntPtr lp)
        {
            if (lp == IntPtr.Zero) return false;
            DRAWITEMSTRUCT d = (DRAWITEMSTRUCT)Marshal.PtrToStructure(lp, typeof(DRAWITEMSTRUCT));
            if (d.hDC == IntPtr.Zero) return false;
            Rectangle r = Rectangle.FromLTRB(d.rcItem.Left, d.rcItem.Top, d.rcItem.Right, d.rcItem.Bottom);
            if (r.Width <= 0 || r.Height <= 0) return true;
            ImageList il = Prop(ref pImages, lb, "ImageList") as ImageList;
            object v = Prop(ref pVirtual, lb, "IsSelectionVirtual");
            bool virt = v is bool && (bool)v;
            using (Graphics g = Graphics.FromHdc(d.hDC))
            {
                using (SolidBrush b = new SolidBrush(Back)) g.FillRectangle(b, r);
                int idx = d.itemID;
                if (idx < 0 || idx >= lb.Items.Count) return true;
                object it = lb.Items[idx];
                bool sel = (d.itemState & 1) != 0;
                Rectangle hr = il == null ? r : Rectangle.FromLTRB(r.Left + r.Height + 3, r.Top, r.Right, r.Bottom);
                if (sel)
                {
                    if (!virt) using (SolidBrush b = new SolidBrush(SelBack)) g.FillRectangle(b, hr);
                    using (Pen p = new Pen(SelBorder)) { if (virt) p.DashStyle = System.Drawing.Drawing2D.DashStyle.Dot; g.DrawRectangle(p, hr.Left, hr.Top, hr.Width - 1, hr.Height - 1); }
                }
                if (il != null)
                {
                    object io = Prop(ref pImgIdx, it, "ImageIndex");
                    int ii = io is int ? (int)io : -1;
                    Bitmap ic = Icon(il, ii);
                    if (ic != null) g.DrawImage(ic, r.Left + 1, r.Top + Math.Max(0, (r.Height - ic.Height) / 2), ic.Width, ic.Height);
                }
                object to = Prop(ref pText, it, "Text");
                string text = to as string ?? (it == null ? "" : it.ToString());
                int tx = r.Left + (il == null ? 1 : r.Height + 5);
                TextRenderer.DrawText(g, text, lb.Font, new Rectangle(tx, r.Top, Math.Max(0, r.Right - tx), r.Height), sel && !virt ? DarkEngine.FgBright : Text,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis | TextFormatFlags.PreserveGraphicsClipping);
            }
            Drawn++;
            return true;
        }

        // icone sem "fosco" branco: pixels quase brancos ligados a borda/transparencia viram transparentes (copia em memoria)
        static Bitmap Icon(ImageList il, int i)
        {
            if (il == null || i < 0 || i >= il.Images.Count) return null;
            Bitmap[] a;
            lock (icons)
            {
                if (!icons.TryGetValue(il, out a) || a.Length != il.Images.Count) { if (a != null) foreach (Bitmap x in a) if (x != null) x.Dispose(); a = new Bitmap[il.Images.Count]; icons[il] = a; }
                if (a[i] != null) return a[i];
            }
            Bitmap bm;
            using (Image src = il.Images[i]) bm = Dematte(src);
            lock (icons) { if (a[i] == null) a[i] = bm; else { bm.Dispose(); bm = a[i]; } }
            return bm;
        }

        public static Bitmap Dematte(Image src)
        {
            Bitmap b = new Bitmap(src.Width, src.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(b)) { g.Clear(Color.Transparent); g.DrawImage(src, 0, 0, src.Width, src.Height); }
            int w = b.Width, h = b.Height;
            System.Drawing.Imaging.BitmapData bd = b.LockBits(new Rectangle(0, 0, w, h), System.Drawing.Imaging.ImageLockMode.ReadWrite, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            int[] px = new int[w * h];
            Marshal.Copy(bd.Scan0, px, 0, px.Length);
            bool[] seen = new bool[px.Length];
            Stack<int> st = new Stack<int>();
            for (int x = 0; x < w; x++) { st.Push(x); st.Push((h - 1) * w + x); }
            for (int y = 0; y < h; y++) { st.Push(y * w); st.Push(y * w + w - 1); }
            while (st.Count > 0)
            {
                int k = st.Pop();
                if (k < 0 || k >= px.Length || seen[k]) continue;
                seen[k] = true;
                int c = px[k];
                int A = (c >> 24) & 255, R = (c >> 16) & 255, G = (c >> 8) & 255, B = c & 255;
                bool clear = A < 16;
                bool matte = !clear && R > 228 && G > 228 && B > 228 && Math.Max(R, Math.Max(G, B)) - Math.Min(R, Math.Min(G, B)) < 24;
                if (!clear && !matte) continue;
                if (matte) px[k] = 0;
                int x0 = k % w;
                if (x0 > 0) st.Push(k - 1);
                if (x0 < w - 1) st.Push(k + 1);
                st.Push(k - w); st.Push(k + w);
            }
            Marshal.Copy(px, 0, bd.Scan0, px.Length);
            b.UnlockBits(bd);
            return b;
        }
    }

    // ---------- StatusBar (WinForms classica): pinta fundo e textos claros sem depender das cores do Windows ----------
    public class StatusBarPainter : NativeWindow
    {
        static readonly Dictionary<StatusBar, StatusBarPainter> all = new Dictionary<StatusBar, StatusBarPainter>();
        readonly StatusBar sb;
        bool failed;

        [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)]
        struct PAINTSTRUCT
        {
            public IntPtr hdc; public int fErase; public RECT rcPaint; public int fRestore; public int fIncUpdate;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] rgbReserved;
        }
        [DllImport("user32.dll")] static extern IntPtr BeginPaint(IntPtr h, out PAINTSTRUCT ps);
        [DllImport("user32.dll")] static extern bool EndPaint(IntPtr h, ref PAINTSTRUCT ps);
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, ref RECT r);

        StatusBarPainter(StatusBar s) { sb = s; }

        public static void Attach(StatusBar s)
        {
            if (s == null || s.IsDisposed || !s.IsHandleCreated) return;
            StatusBarPainter p;
            if (!all.TryGetValue(s, out p))
            {
                p = new StatusBarPainter(s);
                all[s] = p;
                s.Disposed += delegate { try { p.ReleaseHandle(); } catch { } all.Remove(s); };
            }
            if (p.Handle != s.Handle)
            {
                if (p.Handle != IntPtr.Zero) { try { p.ReleaseHandle(); } catch { } }
                p.AssignHandle(s.Handle);
            }
            s.Invalidate();
        }

        // sempre pinta a barra inteira (inclusive paineis OwnerDraw do GeneXus, desenhados fora da tela e clareados)
        public static bool CanPaint(StatusBar s) { return true; }

        protected override void WndProc(ref Message m)
        {
            if (!failed && DarkEngine.On)
            {
                try
                {
                    if (m.Msg == 0x0014 /*WM_ERASEBKGND*/ && CanPaint(sb)) { m.Result = (IntPtr)1; return; }
                    if (m.Msg == 0x000F /*WM_PAINT*/ && m.WParam == IntPtr.Zero && CanPaint(sb))
                    {
                        PAINTSTRUCT ps;
                        IntPtr hdc = BeginPaint(m.HWnd, out ps);
                        try { using (Graphics g = Graphics.FromHdc(hdc)) Draw(g); }
                        finally { EndPaint(m.HWnd, ref ps); }
                        m.Result = IntPtr.Zero;
                        return;
                    }
                    if (m.Msg == 0x0082 /*WM_NCDESTROY*/) { base.WndProc(ref m); try { ReleaseHandle(); } catch { } return; }
                }
                catch (Exception ex) { failed = true; DarkEngine.Log("StatusBar (pintura propria) desligada: " + ex.Message); }
            }
            base.WndProc(ref m);
        }

        static MethodInfo onDrawItem;
        static bool ownerDrawFailed;

        public void Draw(Graphics g)
        {
            Rectangle cr = sb.ClientRectangle;
            using (SolidBrush bg = new SolidBrush(DarkEngine.Chrome)) g.FillRectangle(bg, cr);
            Font font = sb.Font;
            TextFormatFlags baseFlags = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
            if (sb.ShowPanels && sb.Panels.Count > 0)
            {
                Bitmap off = null;
                try
                {
                    using (Pen sep = new Pen(DarkEngine.HotBorder))
                    {
                        for (int i = 0; i < sb.Panels.Count; i++)
                        {
                            StatusBarPanel pn = sb.Panels[i];
                            RECT rr = new RECT();
                            if (SendMessage(sb.Handle, 0x040A /*SB_GETRECT*/, (IntPtr)i, ref rr) == IntPtr.Zero) continue;
                            Rectangle r = Rectangle.FromLTRB(rr.Left, rr.Top, rr.Right, rr.Bottom);
                            if (r.Width <= 0 || r.Height <= 0) continue;
                            bool drawn = false;
                            if (pn.Style == StatusBarPanelStyle.OwnerDraw && !ownerDrawFailed)
                            {
                                if (off == null) off = new Bitmap(Math.Max(1, cr.Width), Math.Max(1, cr.Height), System.Drawing.Imaging.PixelFormat.Format32bppRgb);
                                drawn = DrawOwner(g, off, i, pn, r);
                            }
                            if (!drawn)
                            {
                                int x = r.Left + 2;
                                if (pn.Icon != null)
                                {
                                    try { g.DrawIcon(pn.Icon, new Rectangle(x, r.Top + (r.Height - 16) / 2, 16, 16)); x += 19; } catch { }
                                }
                                Rectangle tr = Rectangle.FromLTRB(x, r.Top, r.Right - 1, r.Bottom);
                                // paineis curtos (Ln, Col, INS...) nunca viram "I...": sem reticencias e sem corte
                                TextFormatFlags fl = (pn.AutoSize == StatusBarPanelAutoSize.Spring ? baseFlags : (baseFlags & ~TextFormatFlags.EndEllipsis) | TextFormatFlags.NoClipping) | (pn.Alignment == HorizontalAlignment.Right ? TextFormatFlags.Right : pn.Alignment == HorizontalAlignment.Center ? TextFormatFlags.HorizontalCenter : TextFormatFlags.Left);
                                TextRenderer.DrawText(g, pn.Text ?? "", font, tr, DarkEngine.Fg, fl);
                            }
                            if (i < sb.Panels.Count - 1)
                                g.DrawLine(sep, r.Right, r.Top + 4, r.Right, r.Bottom - 4);
                        }
                    }
                }
                finally { if (off != null) off.Dispose(); }
            }
            else
            {
                Rectangle tr = new Rectangle(cr.Left + 4, cr.Top, cr.Width - 8 - (sb.SizingGrip ? 14 : 0), cr.Height);
                TextRenderer.DrawText(g, sb.Text ?? "", font, tr, DarkEngine.Fg, baseFlags | TextFormatFlags.Left);
            }
            if (sb.SizingGrip)
            {
                using (SolidBrush dot = new SolidBrush(DarkEngine.FgDim))
                {
                    int bx = cr.Right - 4, by = cr.Bottom - 4;
                    for (int row = 0; row < 3; row++)
                        for (int col = 0; col <= row; col++)
                            g.FillRectangle(dot, bx - col * 4 - 2, by - (row - col) * 4 - 2, 2, 2);
                }
            }
        }

        // Painel OwnerDraw: o proprio GeneXus desenha (OnDrawItem) num bitmap com fundo #242D3C; depois os pixels
        // escuros (texto preto) viram texto claro e um eventual fundo claro vira #242D3C. Icones coloridos ficam iguais.
        bool DrawOwner(Graphics g, Bitmap off, int index, StatusBarPanel pn, Rectangle r)
        {
            try
            {
                if (onDrawItem == null) onDrawItem = typeof(StatusBar).GetMethod("OnDrawItem", BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { typeof(StatusBarDrawItemEventArgs) }, null);
                if (onDrawItem == null) { ownerDrawFailed = true; return false; }
                Rectangle rc = Rectangle.Intersect(r, new Rectangle(0, 0, off.Width, off.Height));
                if (rc.Width <= 0 || rc.Height <= 0) return true;
                using (Graphics og = Graphics.FromImage(off))
                {
                    using (SolidBrush b = new SolidBrush(DarkEngine.Chrome)) og.FillRectangle(b, rc);
                    og.SetClip(rc);
                    StatusBarDrawItemEventArgs ev = new StatusBarDrawItemEventArgs(og, sb.Font, r, index, DrawItemState.None, pn, DarkEngine.Fg, DarkEngine.Chrome);
                    onDrawItem.Invoke(sb, new object[] { ev });
                }
                Lighten(off, rc);
                g.DrawImage(off, rc, rc, GraphicsUnit.Pixel);
                return true;
            }
            catch (Exception ex)
            {
                ownerDrawFailed = true;
                DarkEngine.Log("StatusBar OwnerDraw (desenho do GeneXus) desligado, usando texto simples: " + (ex.InnerException != null ? ex.InnerException.Message : ex.Message));
                return false;
            }
        }

        static double L(int r, int g, int b) { return 0.299 * r + 0.587 * g + 0.114 * b; }

        // ha um pixel cinza escuro (miolo do texto) a ate 2 px na horizontal? (le os valores originais: so mapeados ficam claros)
        static bool NearDarkCore(int[] px, int rowLen, int w, int h, int x, int y, double LD)
        {
            for (int dx = -2; dx <= 2; dx++)
            {
                int xx = x + dx;
                if (dx == 0 || xx < 0 || xx >= w) continue;
                int v = px[y * rowLen + xx];
                int R = (v >> 16) & 0xFF, G = (v >> 8) & 0xFF, B = v & 0xFF;
                int chroma = Math.Max(R, Math.Max(G, B)) - Math.Min(R, Math.Min(G, B));
                if (chroma <= 48 && L(R, G, B) < LD * 0.55) return true;
                // vizinho ja mapeado para o texto claro (Fg proximo) tambem conta
                if (chroma <= 24 && L(R, G, B) > 170 && B > R) return true;
            }
            return false;
        }

        static void Lighten(Bitmap bmp, Rectangle rc)
        {
            System.Drawing.Imaging.BitmapData d = bmp.LockBits(rc, System.Drawing.Imaging.ImageLockMode.ReadWrite, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            try
            {
                int n = rc.Width * rc.Height;
                int[] px = new int[d.Stride / 4 * rc.Height];
                Marshal.Copy(d.Scan0, px, 0, px.Length);
                int rowLen = d.Stride / 4;
                // cor de fundo dominante
                Dictionary<int, int> hist = new Dictionary<int, int>();
                for (int y = 0; y < rc.Height; y++)
                    for (int x = 0; x < rc.Width; x++)
                    {
                        int v = px[y * rowLen + x] | unchecked((int)0xFF000000);
                        int k; hist.TryGetValue(v, out k); hist[v] = k + 1;
                    }
                int dom = DarkEngine.Chrome.ToArgb(), best = -1;
                foreach (KeyValuePair<int, int> kv in hist) if (kv.Value > best) { best = kv.Value; dom = kv.Key; }
                Color D = Color.FromArgb(dom);
                double LD = Math.Max(1.0, L(D.R, D.G, D.B));
                Color C = DarkEngine.Chrome, F = DarkEngine.Fg;
                bool lightBg = LD > 100;
                for (int y = 0; y < rc.Height; y++)
                    for (int x = 0; x < rc.Width; x++)
                    {
                        int i = y * rowLen + x;
                        int v = px[i];
                        int a = (v >> 24) & 0xFF, R = (v >> 16) & 0xFF, G = (v >> 8) & 0xFF, B = v & 0xFF;
                        if ((v | unchecked((int)0xFF000000)) == dom) { px[i] = C.ToArgb(); continue; }
                        double l = L(R, G, B);
                        int chroma = Math.Max(R, Math.Max(G, B)) - Math.Min(R, Math.Min(G, B));
                        double t;
                        if (lightBg && chroma > 48)
                        {
                            // franja de ClearType ao lado do texto escuro: mapeia canal a canal; cor de verdade (icone/texto colorido) fica
                            if (!NearDarkCore(px, rowLen, rc.Width, rc.Height, x, y, LD)) continue;
                            double tr = (D.R - R) / Math.Max(1.0, (double)D.R), tg = (D.G - G) / Math.Max(1.0, (double)D.G), tb = (D.B - B) / Math.Max(1.0, (double)D.B);
                            tr = Math.Max(0, Math.Min(1, tr)); tg = Math.Max(0, Math.Min(1, tg)); tb = Math.Max(0, Math.Min(1, tb));
                            px[i] = unchecked((int)0xFF000000) | ((int)(C.R + (F.R - C.R) * tr) << 16) | ((int)(C.G + (F.G - C.G) * tg) << 8) | (int)(C.B + (F.B - C.B) * tb);
                            continue;
                        }
                        if (lightBg) { t = (LD - l) / LD; }
                        else { if (l >= LD - 3) continue; t = (LD - l) / LD; }
                        if (t < 0) t = 0; if (t > 1) t = 1;
                        int nr = (int)(C.R + (F.R - C.R) * t), ng = (int)(C.G + (F.G - C.G) * t), nb = (int)(C.B + (F.B - C.B) * t);
                        px[i] = unchecked((int)0xFF000000) | (nr << 16) | (ng << 8) | nb;
                    }
                Marshal.Copy(px, 0, d.Scan0, px.Length);
            }
            finally { bmp.UnlockBits(d); }
        }
    }

    // ---------- Molduras finas (borda 3D/clara do Windows), combos Flat e cabecalho de ListView ----------
    // Tudo por subclasse de janela (NativeWindow) em memoria: nenhuma cor de sistema e alterada.
    public class FrameHook : NativeWindow
    {
        static readonly Dictionary<Control, FrameHook> all = new Dictionary<Control, FrameHook>();
        readonly Control c;
        bool failed;
        const int NC = 1, COMBO = 2, LVHDR = 4, LVGRID = 8;
        int kind;

        [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] struct NMHDR { public IntPtr hwndFrom; public IntPtr idFrom; public int code; }
        [StructLayout(LayoutKind.Sequential)]
        struct NMCUSTOMDRAW { public NMHDR hdr; public int dwDrawStage; public IntPtr hdc; public RECT rc; public IntPtr dwItemSpec; public int uItemState; public IntPtr lItemlParam; }

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")] static extern int GetWindowLong32(IntPtr h, int idx);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
        [DllImport("user32.dll")] static extern bool ClientToScreen(IntPtr h, ref POINT p);
        [DllImport("user32.dll")] static extern IntPtr GetWindowDC(IntPtr h);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr h, IntPtr dc);
        [DllImport("user32.dll")] static extern bool RedrawWindow(IntPtr h, IntPtr r, IntPtr rgn, int flags);
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
        [DllImport("gdi32.dll")] static extern int SetTextColor(IntPtr hdc, int color);
        [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr h, out RECT r);
        [DllImport("user32.dll", EntryPoint = "SendMessage")] static extern IntPtr SendMessageRect(IntPtr h, int msg, IntPtr w, ref RECT r);

        FrameHook(Control ctl) { c = ctl; }

        public static void Attach(Control ctl)
        {
            if (ctl == null || ctl.IsDisposed || !ctl.IsHandleCreated || ctl is Form || ctl is StatusBar) return;
            IntPtr h = ctl.Handle;
            int st = GetWindowLong32(h, -16), ex = GetWindowLong32(h, -20);
            int k = 0;
            bool child = (st & 0x40000000) != 0;
            if (child && ((ex & 0x200) != 0 /*CLIENTEDGE*/ || (ex & 0x20000) != 0 /*STATICEDGE*/ || (st & 0x00C00000) == 0x00800000 /*so WS_BORDER*/)) k |= NC;
            ComboBox cb = ctl as ComboBox;
            if (cb != null && cb.FlatStyle == FlatStyle.Flat && ctl.GetType().FullName.IndexOf("ToolStrip", StringComparison.Ordinal) < 0) k |= COMBO;
            ListView lv = ctl as ListView;
            if (lv != null) { k |= LVHDR; if (LvDark.HasGrid(lv)) k |= LVGRID; }
            if (k == 0) return;
            FrameHook p;
            if (!all.TryGetValue(ctl, out p))
            {
                p = new FrameHook(ctl);
                all[ctl] = p;
                ctl.Disposed += delegate { try { if (p.Handle != IntPtr.Zero) p.ReleaseHandle(); } catch { } all.Remove(ctl); };
            }
            p.kind = k;
            if (p.Handle != h)
            {
                if (p.Handle != IntPtr.Zero) { try { p.ReleaseHandle(); } catch { } }
                p.AssignHandle(h);
                if ((k & NC) != 0) RedrawWindow(h, IntPtr.Zero, IntPtr.Zero, 0x0400 /*FRAME*/ | 0x0001 /*INVALIDATE*/);
                if ((k & (COMBO | LVHDR)) != 0) ctl.Invalidate(true);
            }
        }

        protected override void WndProc(ref Message m)
        {
            bool passed = false;
            if (!failed && DarkEngine.On)
            {
                try
                {
                    switch (m.Msg)
                    {
                        case 0x0085: // WM_NCPAINT
                            passed = true;
                            base.WndProc(ref m);
                            if ((kind & NC) != 0) PaintFrame(m.HWnd);
                            return;
                        case 0x000F: // WM_PAINT
                            passed = true;
                            base.WndProc(ref m);
                            if ((kind & COMBO) != 0) PaintCombo();
                            if ((kind & LVGRID) != 0) LvDark.PaintGrid(c as ListView);
                            return;
                        case 0x004E: // WM_NOTIFY
                            if ((kind & LVHDR) != 0 && HeaderDraw(ref m)) return;
                            break;
                        case 0x204E: // WM_REFLECT + WM_NOTIFY: desenho dos itens da propria ListView (nao OwnerDraw)
                            if ((kind & LVHDR) != 0 && !((ListView)c).OwnerDraw)
                            {
                                passed = true;
                                base.WndProc(ref m);
                                ItemSel(ref m);
                                return;
                            }
                            break;
                        case 0x0082: // WM_NCDESTROY
                            passed = true;
                            base.WndProc(ref m);
                            try { ReleaseHandle(); } catch { }
                            return;
                    }
                }
                catch (Exception ex)
                {
                    failed = true;
                    DarkEngine.Log("Moldura/combos (pintura propria) desligada para " + c.GetType().FullName + ": " + ex.Message);
                    if (!passed) base.WndProc(ref m);
                    return;
                }
            }
            base.WndProc(ref m);
        }

        Color InnerColor()
        {
            Color b = Color.Empty;
            try { b = c.BackColor; } catch { }
            if (b.IsEmpty || b.A < 255 || DarkEngine.IsLight(b)) b = DarkEngine.Panel;
            return b;
        }

        // pinta a borda nao-cliente: 1px #333D47 por fora + o resto da espessura com o fundo do proprio controle
        void PaintFrame(IntPtr h)
        {
            RECT wr;
            if (!GetWindowRect(h, out wr)) return;
            POINT o = new POINT();
            ClientToScreen(h, ref o);
            int n = Math.Min(o.X - wr.Left, o.Y - wr.Top);
            if (n <= 0 || n > 4) return;
            int w = wr.Right - wr.Left, hh = wr.Bottom - wr.Top;
            if (w < 3 || hh < 3) return;
            IntPtr dc = GetWindowDC(h);
            if (dc == IntPtr.Zero) return;
            try
            {
                using (Graphics g = Graphics.FromHdc(dc))
                {
                    if (n > 1)
                    {
                        using (SolidBrush br = new SolidBrush(InnerColor()))
                        {
                            int k = n - 1;
                            g.FillRectangle(br, 1, 1, w - 2, k);
                            g.FillRectangle(br, 1, hh - n, w - 2, k);
                            g.FillRectangle(br, 1, 1, k, hh - 2);
                            g.FillRectangle(br, w - n, 1, k, hh - 2);
                        }
                    }
                    using (Pen p = new Pen(DarkEngine.Border)) g.DrawRectangle(p, 0, 0, w - 1, hh - 1);
                }
            }
            finally { ReleaseDC(h, dc); }
        }

        // ComboBox Flat: o WinForms pinta o botao com SystemColors.Control (claro) e a borda com ControlDark
        void PaintCombo()
        {
            ComboBox cb = c as ComboBox;
            if (cb == null || cb.FlatStyle != FlatStyle.Flat || cb.RightToLeft == RightToLeft.Yes) return;
            Rectangle r = cb.ClientRectangle;
            if (r.Width < 20 || r.Height < 8) return;
            int bw = SystemInformation.HorizontalScrollBarArrowWidth;
            using (Graphics g = cb.CreateGraphics())
            {
                bool hot = false;
                try { hot = cb.Enabled && r.Contains(cb.PointToClient(Control.MousePosition)); } catch { }
                Rectangle btn = Rectangle.FromLTRB(r.Right - bw - 1, r.Top + 1, r.Right - 1, r.Bottom - 1);
                using (SolidBrush br = new SolidBrush(hot ? DarkEngine.HotBg : DarkEngine.Chrome)) g.FillRectangle(br, btn);
                using (Pen p = new Pen(DarkEngine.Border)) g.DrawLine(p, btn.Left, btn.Top, btn.Left, btn.Bottom - 1);
                int mx = btn.Left + (btn.Width + 1) / 2, my = btn.Top + btn.Height / 2;
                using (SolidBrush ar = new SolidBrush(cb.Enabled ? DarkEngine.Fg : DarkEngine.FgDim))
                    g.FillPolygon(ar, new Point[] { new Point(mx - 4, my - 2), new Point(mx + 4, my - 2), new Point(mx, my + 2) });
                using (Pen p = new Pen(hot || cb.Focused ? DarkEngine.HotBorder : DarkEngine.Border)) g.DrawRectangle(p, 0, 0, r.Width - 1, r.Height - 1);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        struct NMLVCUSTOMDRAW { public NMCUSTOMDRAW nmcd; public int clrText; public int clrTextBk; public int iSubItem; }

        // linha selecionada da ListView com o tema do Windows: realce claro (#F0F0F0 quando sem foco) -> #2C3C50 com texto claro
        void ItemSel(ref Message m)
        {
            if (m.LParam == IntPtr.Zero) return;
            NMHDR nh = (NMHDR)Marshal.PtrToStructure(m.LParam, typeof(NMHDR));
            if (nh.code != -12 /*NM_CUSTOMDRAW*/ || nh.hwndFrom != c.Handle) return;
            NMLVCUSTOMDRAW cd = (NMLVCUSTOMDRAW)Marshal.PtrToStructure(m.LParam, typeof(NMLVCUSTOMDRAW));
            int st = cd.nmcd.dwDrawStage;
            if (st != 0x10001 /*ITEMPREPAINT*/ && st != 0x30001 /*SUBITEM|ITEMPREPAINT*/) return;
            int idx = (int)cd.nmcd.dwItemSpec.ToInt64();
            bool sel = (SendMessage(c.Handle, 0x102C /*LVM_GETITEMSTATE*/, (IntPtr)idx, (IntPtr)0x0002 /*LVIS_SELECTED*/).ToInt64() & 2) != 0;
            if (!sel) return;
            ListView lv = (ListView)c;
            if (st == 0x30001 && !lv.FullRowSelect && cd.iSubItem > 0) return;
            if (!lv.Focused && lv.HideSelection) return;
            Color bg = DarkEngine.T(44, 60, 80), fg = DarkEngine.FgBright;
            cd.nmcd.uItemState &= ~(0x0001 /*CDIS_SELECTED*/ | 0x0010 /*CDIS_FOCUS*/);
            cd.clrText = fg.R | (fg.G << 8) | (fg.B << 16);
            cd.clrTextBk = bg.R | (bg.G << 8) | (bg.B << 16);
            Marshal.StructureToPtr(cd, m.LParam, false);
            m.Result = (IntPtr)(m.Result.ToInt64() | 0x02 /*CDRF_NEWFONT*/);
        }

        // cabecalho da ListView (tema escuro do Windows): texto claro via NM_CUSTOMDRAW
        bool HeaderDraw(ref Message m)
        {
            if (m.LParam == IntPtr.Zero) return false;
            NMHDR nh = (NMHDR)Marshal.PtrToStructure(m.LParam, typeof(NMHDR));
            if (nh.code != -12 /*NM_CUSTOMDRAW*/) return false;
            ListView lv = c as ListView;
            if (lv == null) return false;
            IntPtr hdr = SendMessage(m.HWnd, 0x101F /*LVM_GETHEADER*/, IntPtr.Zero, IntPtr.Zero);
            if (hdr == IntPtr.Zero || nh.hwndFrom != hdr) return false;
            NMCUSTOMDRAW cd = (NMCUSTOMDRAW)Marshal.PtrToStructure(m.LParam, typeof(NMCUSTOMDRAW));
            if (lv.OwnerDraw)
            {
                // a propria ListView (DrawColumnHeader) desenha os itens; aqui so a area vazia a direita (pos-pintura)
                if (cd.dwDrawStage == 0x0001)
                {
                    base.WndProc(ref m);
                    m.Result = (IntPtr)(m.Result.ToInt64() | 0x10);
                    return true;
                }
                if (cd.dwDrawStage != 0x0002) return false;
            }
            if (cd.dwDrawStage == 0x0001 /*CDDS_PREPAINT*/) { m.Result = (IntPtr)(0x20 | 0x10) /*CDRF_NOTIFYITEMDRAW|NOTIFYPOSTPAINT*/; return true; }
            if (cd.dwDrawStage == 0x10001 /*CDDS_ITEMPREPAINT*/)
            {
                // desenha o item inteiro (fundo escuro, linha fina, texto claro): independe do tema do Windows
                int idx = (int)cd.dwItemSpec;
                Rectangle r = Rectangle.FromLTRB(cd.rc.Left, cd.rc.Top, cd.rc.Right, cd.rc.Bottom);
                if (idx < 0 || idx >= lv.Columns.Count || r.Width <= 0 || r.Height <= 0) { m.Result = IntPtr.Zero; return true; }
                using (Graphics g = Graphics.FromHdc(cd.hdc))
                {
                    bool hot = (cd.uItemState & 0x40 /*CDIS_HOT*/) != 0, pressed = (cd.uItemState & 0x01 /*CDIS_SELECTED*/) != 0;
                    using (SolidBrush b = new SolidBrush(pressed ? DarkEngine.MenuHot : hot ? DarkEngine.HotBg : DarkEngine.Chrome)) g.FillRectangle(b, r);
                    using (Pen p = new Pen(DarkEngine.Border))
                    {
                        g.DrawLine(p, r.Right - 1, r.Top + 3, r.Right - 1, r.Bottom - 4);
                        g.DrawLine(p, r.Left, r.Bottom - 1, r.Right - 1, r.Bottom - 1);
                    }
                    ColumnHeader ch = lv.Columns[idx];
                    HorizontalAlignment al = ch.TextAlign;
                    TextFormatFlags fl = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix
                        | (al == HorizontalAlignment.Right ? TextFormatFlags.Right : al == HorizontalAlignment.Center ? TextFormatFlags.HorizontalCenter : TextFormatFlags.Left);
                    Rectangle tr = Rectangle.FromLTRB(r.Left + 6, r.Top, r.Right - 6, r.Bottom - 1);
                    TextRenderer.DrawText(g, ch.Text ?? "", lv.Font, tr, lv.Enabled ? DarkEngine.Fg : DarkEngine.FgDisabled, fl);
                }
                m.Result = (IntPtr)0x04; // CDRF_SKIPDEFAULT
                return true;
            }
            if (cd.dwDrawStage == 0x0002 /*CDDS_POSTPAINT*/)
            {
                // area vazia a direita da ultima coluna
                RECT hc; GetClientRect(hdr, out hc);
                int right = 0;
                for (int i = 0; i < lv.Columns.Count; i++)
                {
                    RECT ir = new RECT();
                    if (SendMessageRect(hdr, 0x1207 /*HDM_GETITEMRECT*/, (IntPtr)i, ref ir) != IntPtr.Zero && ir.Right > right) right = ir.Right;
                }
                if (hc.Right > right && hc.Bottom > hc.Top)
                {
                    using (Graphics g = Graphics.FromHdc(cd.hdc))
                    {
                        using (SolidBrush b = new SolidBrush(DarkEngine.Chrome)) g.FillRectangle(b, right, hc.Top, hc.Right - right, hc.Bottom - hc.Top);
                        using (Pen p = new Pen(DarkEngine.Border)) g.DrawLine(p, right, hc.Bottom - 1, hc.Right - 1, hc.Bottom - 1);
                    }
                }
                m.Result = IntPtr.Zero;
                return true;
            }
            return false;
        }
    }

    // ---------- ListView escura (grades simples do Windows: Values Editor de dominios/atributos e outros dialogos) ----------
    // Em memoria: linhas de grade #333D47 desenhadas por cima (as nativas sao claras), e nas ListViews OwnerDraw que pedem o
    // desenho padrao do sistema (DrawDefault = true, ex.: EnumValuesEditorControl) o cabecalho e as celulas sao pintados escuros.
    // O manipulador do GeneXus continua rodando primeiro; o nosso so age quando ele deixou o desenho para o sistema.
    public static class LvDark
    {
        static readonly HashSet<ListView> done = new HashSet<ListView>();
        static readonly HashSet<ListView> grids = new HashSet<ListView>();
        static readonly HashSet<ListView> hookedLv = new HashSet<ListView>();
        public static void Reset() { done.Clear(); }   // modo claro: o proximo escuro reaplica
        static readonly Dictionary<ImageList, Dictionary<int, Bitmap>> rowImgs = new Dictionary<ImageList, Dictionary<int, Bitmap>>();
        static Color GridLine { get { return DarkEngine.Border; } }
        static Color Sel { get { return DarkEngine.T(44, 60, 80); } }

        [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
        [DllImport("user32.dll", EntryPoint = "SendMessage")] static extern IntPtr SendMessageRect(IntPtr h, int msg, IntPtr w, ref RECT r);
        [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr h, out RECT r);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
        [DllImport("user32.dll")] static extern bool ScreenToClient(IntPtr h, ref POINT p);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);

        public static bool HasGrid(ListView lv) { return lv != null && grids.Contains(lv); }

        public static void Attach(ListView lv)
        {
            if (lv == null || lv.IsDisposed) return;
            if (done.Contains(lv))
            {
                if (lv.GridLines && grids.Contains(lv)) { lv.GridLines = false; lv.Invalidate(); }
                return;
            }
            if (DarkEngine.IsLight(lv.BackColor)) return; // lista deixada clara de proposito
            done.Add(lv);
            bool hook = hookedLv.Add(lv);
            if (hook) lv.Disposed += delegate { done.Remove(lv); grids.Remove(lv); hookedLv.Remove(lv); };
            bool g = false;
            if (lv.GridLines) { grids.Add(lv); lv.GridLines = false; g = true; }
            if (lv.OwnerDraw)
            {
                lv.DrawColumnHeader -= OnHeader; lv.DrawColumnHeader += OnHeader;
                lv.DrawSubItem -= OnSubItem; lv.DrawSubItem += OnSubItem;
            }
            if (lv.IsHandleCreated) { FrameHook.Attach(lv); lv.Invalidate(true); }
            else if (hook) lv.HandleCreated += delegate { try { if (DarkEngine.On) FrameHook.Attach(lv); } catch { } };
            DarkEngine.LogOnce("lvdark:" + lv.GetType().FullName + ":" + lv.Name, "ListView escura: " + lv.GetType().FullName + " '" + lv.Name + "'" + (g ? " (linhas de grade #333D47)" : "") + (lv.OwnerDraw ? " (cabecalho/celulas do desenho padrao pintados escuros)" : ""));
        }

        static void OnHeader(object sender, DrawListViewColumnHeaderEventArgs e)
        {
            if (!e.DrawDefault || !DarkEngine.On) return;
            ListView lv = sender as ListView;
            if (lv == null || e.Header == null) return;
            try
            {
                e.DrawDefault = false;
                Graphics g = e.Graphics; Rectangle r = e.Bounds;
                bool pressed = (e.State & ListViewItemStates.Selected) != 0, hot = (e.State & ListViewItemStates.Hot) != 0;
                using (SolidBrush b = new SolidBrush(pressed ? DarkEngine.MenuHot : hot ? DarkEngine.HotBg : DarkEngine.Chrome)) g.FillRectangle(b, r);
                using (Pen p = new Pen(GridLine))
                {
                    g.DrawLine(p, r.Right - 1, r.Top + 3, r.Right - 1, r.Bottom - 4);
                    g.DrawLine(p, r.Left, r.Bottom - 1, r.Right - 1, r.Bottom - 1);
                }
                HorizontalAlignment al = e.Header.TextAlign;
                TextFormatFlags fl = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix
                    | (al == HorizontalAlignment.Right ? TextFormatFlags.Right : al == HorizontalAlignment.Center ? TextFormatFlags.HorizontalCenter : TextFormatFlags.Left);
                TextRenderer.DrawText(g, e.Header.Text ?? "", e.Font ?? lv.Font, Rectangle.FromLTRB(r.Left + 6, r.Top, r.Right - 6, r.Bottom - 1), lv.Enabled ? DarkEngine.Fg : DarkEngine.FgDisabled, fl);
            }
            catch { e.DrawDefault = true; }
        }

        static void OnSubItem(object sender, DrawListViewSubItemEventArgs e)
        {
            if (!e.DrawDefault || !DarkEngine.On) return;
            ListView lv = sender as ListView;
            if (lv == null || e.Item == null || lv.View != View.Details) return;
            if (lv.CheckBoxes && e.ColumnIndex == 0) return; // caixa de selecao nativa: fica com o sistema
            try
            {
                e.DrawDefault = false;
                Graphics g = e.Graphics;
                Rectangle r = e.Bounds;
                if (e.ColumnIndex == 0 && e.ColumnIndex < lv.Columns.Count && r.Width > lv.Columns[0].Width) r.Width = lv.Columns[0].Width;
                bool sel = e.Item.Selected && (lv.FullRowSelect || e.ColumnIndex == 0) && (lv.Focused || !lv.HideSelection || lv.ContainsFocus);
                ListViewItem.ListViewSubItem si = e.SubItem;
                bool itemStyle = e.ColumnIndex == 0 || e.Item.UseItemStyleForSubItems || si == null;
                Color back = itemStyle ? e.Item.BackColor : si.BackColor;
                if (back.IsEmpty || back.A < 255 || DarkEngine.IsLight(back)) back = DarkEngine.IsLight(lv.BackColor) ? DarkEngine.Panel : lv.BackColor;
                if (sel) back = Sel;
                using (SolidBrush b = new SolidBrush(back)) g.FillRectangle(b, r);
                int x = r.X + 2;
                ImageList il = lv.SmallImageList;
                if (e.ColumnIndex == 0 && il != null)
                {
                    int idx = e.Item.ImageIndex;
                    if (idx < 0 && !string.IsNullOrEmpty(e.Item.ImageKey)) idx = il.Images.IndexOfKey(e.Item.ImageKey);
                    x += e.Item.IndentCount * il.ImageSize.Width;
                    if (idx >= 0 && idx < il.Images.Count)
                    {
                        Bitmap bm = RowImage(il, idx);
                        if (bm != null)
                        {
                            Size isz = il.ImageSize;
                            g.DrawImage(bm, new Rectangle(x, r.Y + (r.Height - isz.Height) / 2, isz.Width, isz.Height));
                        }
                    }
                    x += il.ImageSize.Width + 2;
                }
                string txt = si != null ? si.Text : e.Item.Text;
                if (string.IsNullOrEmpty(txt)) return;
                Font f = itemStyle ? (e.Item.Font ?? lv.Font) : (si.Font ?? lv.Font);
                Color fc = itemStyle ? e.Item.ForeColor : si.ForeColor;
                if (sel) fc = DarkEngine.FgBright;
                else
                {
                    if (fc.IsEmpty || DarkEngine.IsDarkText(fc) || fc.IsSystemColor) fc = DarkEngine.IsDarkText(lv.ForeColor) ? DarkEngine.Fg : lv.ForeColor;
                    fc = DarkEngine.TextFloor(fc, back, !lv.Enabled);
                }
                HorizontalAlignment al = e.ColumnIndex > 0 && e.ColumnIndex < lv.Columns.Count ? lv.Columns[e.ColumnIndex].TextAlign : HorizontalAlignment.Left;
                TextFormatFlags fl = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix
                    | (al == HorizontalAlignment.Right ? TextFormatFlags.Right : al == HorizontalAlignment.Center ? TextFormatFlags.HorizontalCenter : TextFormatFlags.Left);
                Rectangle tr = Rectangle.FromLTRB(x + 2, r.Top, r.Right - 4, r.Bottom);
                if (tr.Width > 0) TextRenderer.DrawText(g, txt, f, tr, fc, fl);
            }
            catch { e.DrawDefault = true; }
        }

        // imagem de inicio de linha: retangulo branco/cinza claro "chapado" -> fundo escuro #181F2A, traco preto -> #8A909B;
        // pixels coloridos mantidos. Icone comum (sem area clara chapada) fica igual.
        static Bitmap RowImage(ImageList il, int idx)
        {
            Dictionary<int, Bitmap> d;
            if (!rowImgs.TryGetValue(il, out d)) { d = new Dictionary<int, Bitmap>(); rowImgs[il] = d; il.Disposed += delegate { rowImgs.Remove(il); }; }
            Bitmap b;
            if (d.TryGetValue(idx, out b)) return b;
            Image src = il.Images[idx];
            b = DarkRow(src) ?? (src as Bitmap) ?? new Bitmap(src);
            d[idx] = b;
            return b;
        }

        internal static Bitmap DarkRow(Image src)
        {
            int w, h;
            int[] px = IconSwap.Pixels(src, out w, out h);
            if (px == null) return null;
            int n = 0, light = 0;
            for (int i = 0; i < px.Length; i++)
            {
                int c = px[i], a = (c >> 24) & 255;
                if (a < 40) continue;
                int r = (c >> 16) & 255, g = (c >> 8) & 255, bl = c & 255;
                n++;
                if (Math.Max(r, Math.Max(g, bl)) - Math.Min(r, Math.Min(g, bl)) < 40 && 0.299 * r + 0.587 * g + 0.114 * bl > 200) light++;
            }
            if (n == 0) return null;
            if (light < n / 4) return IconSwap.MonoLift(src, false);
            Color pn = DarkEngine.Panel, gl = DarkEngine.T(138, 144, 155);
            for (int i = 0; i < px.Length; i++)
            {
                int c = px[i], a = (c >> 24) & 255;
                if (a == 0) continue;
                int r = (c >> 16) & 255, g = (c >> 8) & 255, bl = c & 255;
                int sat = Math.Max(r, Math.Max(g, bl)) - Math.Min(r, Math.Min(g, bl));
                double l = 0.299 * r + 0.587 * g + 0.114 * bl;
                if (sat < 40 && l > 200) px[i] = (a << 24) | (pn.R << 16) | (pn.G << 8) | pn.B;
                else if (sat < 48 && l < 140) px[i] = (a << 24) | (gl.R << 16) | (gl.G << 8) | gl.B;
            }
            DarkEngine.LogOnce("lvimg:" + w + "x" + h + ":" + light, "Imagem de linha clara escurecida (ListView): " + w + "x" + h + ", " + light + "/" + n + " px claros");
            return IconSwap.FromPixels(px, w, h);
        }

        // linhas de grade escuras: depois do WM_PAINT da propria ListView, nas mesmas posicoes das nativas
        public static void PaintGrid(ListView lv)
        {
            if (lv == null || lv.IsDisposed || !lv.IsHandleCreated || lv.View != View.Details || lv.Columns.Count == 0) return;
            IntPtr h = lv.Handle;
            RECT cr; GetClientRect(h, out cr);
            if (cr.Right <= 0 || cr.Bottom <= 0) return;
            int top = 0, hdrLeft = 0;
            IntPtr hdr = SendMessage(h, 0x101F /*LVM_GETHEADER*/, IntPtr.Zero, IntPtr.Zero);
            List<int> xs = new List<int>();
            if (hdr != IntPtr.Zero)
            {
                RECT wr;
                if (GetWindowRect(hdr, out wr))
                {
                    POINT o = new POINT { X = wr.Left, Y = wr.Top };
                    ScreenToClient(h, ref o);
                    hdrLeft = o.X;
                    if (IsWindowVisible(hdr)) top = Math.Max(0, o.Y + (wr.Bottom - wr.Top));
                }
                int nc = SendMessage(hdr, 0x1200 /*HDM_GETITEMCOUNT*/, IntPtr.Zero, IntPtr.Zero).ToInt32();
                for (int i = 0; i < nc && i < 256; i++)
                {
                    RECT ir = new RECT();
                    if (SendMessageRect(hdr, 0x1207 /*HDM_GETITEMRECT*/, (IntPtr)i, ref ir) != IntPtr.Zero) xs.Add(hdrLeft + ir.Right - 1);
                }
            }
            int right = cr.Right;
            if (xs.Count > 0) { int mx = int.MinValue; foreach (int x in xs) if (x > mx) mx = x; right = Math.Min(cr.Right, mx + 1); }
            using (Graphics g = Graphics.FromHwnd(h))
            {
                g.SetClip(Rectangle.FromLTRB(0, top, cr.Right, cr.Bottom));
                foreach (Control k in lv.Controls) { try { if (k.Visible) g.ExcludeClip(k.Bounds); } catch { } }
                using (Pen p = new Pen(GridLine))
                {
                    int n = SendMessage(h, 0x1004 /*LVM_GETITEMCOUNT*/, IntPtr.Zero, IntPtr.Zero).ToInt32();
                    int first = SendMessage(h, 0x1027 /*LVM_GETTOPINDEX*/, IntPtr.Zero, IntPtr.Zero).ToInt32();
                    int y = -1, ih = 0;
                    for (int i = Math.Max(0, first), k = 0; i < n && k < 400; i++, k++)
                    {
                        RECT ir = new RECT { Left = 0 /*LVIR_BOUNDS*/ };
                        if (SendMessageRect(h, 0x100E /*LVM_GETITEMRECT*/, (IntPtr)i, ref ir) == IntPtr.Zero) break;
                        if (ir.Top > cr.Bottom) break;
                        ih = ir.Bottom - ir.Top; y = ir.Bottom - 1;
                        if (y >= top) g.DrawLine(p, 0, y, right - 1, y);
                    }
                    if (ih > 2) for (y += ih; y < cr.Bottom; y += ih) if (y >= top) g.DrawLine(p, 0, y, right - 1, y); // linhas da area vazia (como as nativas)
                    foreach (int x in xs) if (x >= 0 && x < cr.Right) g.DrawLine(p, x, top, x, cr.Bottom - 1);
                }
            }
        }
    }


    // Renderer do editor (Actipro VS2005) com os botoes de divisao (acima/ao lado das barras de rolagem) escuros.
    // Usado so em memoria, por editor; sem a extensao o GeneXus usa o renderer original.
    public class DarkVs2005Renderer : ActiproSoftware.SyntaxEditor.VisualStudio2005SyntaxEditorRenderer
    {
        public override void DrawEditorViewSplitButton(PaintEventArgs e, Rectangle bounds, ActiproSoftware.SyntaxEditor.EditorView view, Orientation orientation)
        {
            try
            {
                Graphics g = e.Graphics;
                using (SolidBrush b = new SolidBrush(DarkEngine.Chrome)) g.FillRectangle(b, bounds);
                if (bounds.Width < 3 || bounds.Height < 3) return;
                using (Pen p = new Pen(DarkEngine.Border)) g.DrawRectangle(p, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
                using (Pen p = new Pen(DarkEngine.FgDim))
                {
                    int cx = bounds.X + bounds.Width / 2, cy = bounds.Y + bounds.Height / 2;
                    if (bounds.Width >= bounds.Height)
                    {
                        int hl = Math.Max(1, Math.Min(5, bounds.Width / 2 - 3));
                        g.DrawLine(p, cx - hl, cy - 1, cx + hl, cy - 1);
                        if (bounds.Height >= 6) g.DrawLine(p, cx - hl, cy + 1, cx + hl, cy + 1);
                    }
                    else
                    {
                        int hl = Math.Max(1, Math.Min(5, bounds.Height / 2 - 3));
                        g.DrawLine(p, cx - 1, cy - hl, cx - 1, cy + hl);
                        if (bounds.Width >= 6) g.DrawLine(p, cx + 1, cy - hl, cx + 1, cy + hl);
                    }
                }
            }
            catch { try { base.DrawEditorViewSplitButton(e, bounds, view, orientation); } catch { } }
        }
    }
    // ---------- Grades em arvore DevExpress (abas Structure / Variables da Transaction, Procedure, SDT, ...) ----------
    // So aparencias e eventos de desenho da propria TreeList (em memoria): nada e salvo na KB; sem a extensao volta ao original.
    public static class DxTreeTheme
    {
        static Color SelBg { get { return DarkEngine.T(44, 60, 80); } }       // #2C3C50 linha selecionada
        static Color Line { get { return DarkEngine.Border; } }                 // #333D47 linhas da grade / separadores
        static Color TreeLn { get { return DarkEngine.T(96, 108, 124); } }    // linhas de conexao da arvore (visiveis)
        static Color GrayFg { get { return DarkEngine.T(160, 167, 178); } }   // textos secundarios/desabilitados (mais claro que #8A909B)
        static readonly HashSet<TreeList> done = new HashSet<TreeList>();
        static readonly HashSet<TreeList> hookedTl = new HashSet<TreeList>();
        static readonly Dictionary<TreeList, TreeListNode> hot = new Dictionary<TreeList, TreeListNode>();
        static bool logged;

        public static bool Attach(Control c)
        {
            TreeList tl = c as TreeList;
            if (tl == null) return false;
            if (done.Contains(tl)) return true;
            done.Add(tl);
            bool hook = hookedTl.Add(tl);
            if (hook) tl.Disposed += OnDisposed;
            TreeListAppearanceCollection a = tl.Appearance;
            Color E = DarkEngine.Editor, F = DarkEngine.Fg;
            Set(a.Row, E, F); Set(a.Empty, E, F); Set(a.EvenRow, E, F); Set(a.OddRow, E, F);
            Set(a.FocusedRow, DarkEngine.MenuHot, Color.White); Set(a.FocusedCell, DarkEngine.MenuHot, Color.White);
            Set(a.SelectedRow, SelBg, DarkEngine.FgBright); Set(a.HideSelectionRow, SelBg, F);
            Set(a.TreeLine, TreeLn, TreeLn); Set(a.HorzLine, Line, Line); Set(a.VertLine, Line, Line); Set(a.FixedLine, Line, Line);
            Set(a.HeaderPanel, DarkEngine.Chrome, F); Set(a.FooterPanel, DarkEngine.Chrome, F); Set(a.GroupFooter, DarkEngine.Chrome, F);
            Set(a.Preview, E, DarkEngine.FgDim); Set(a.GroupButton, E, F);
            try { Journal.SnapNamed(tl, "BorderStyle"); tl.BorderStyle = BorderStyles.NoBorder; } catch { }
            if (hook)
            {
            tl.CustomDrawColumnHeader += OnHeader;
            tl.CustomDrawNodeButton += OnButton;
            tl.CustomDrawEmptyArea += OnEmpty;
            tl.CustomDrawNodeCell += OnCell;
            tl.NodeCellStyle += OnCellStyle;
            tl.ShownEditor += OnShownEditor;
            tl.MouseMove += OnMove;
            tl.MouseLeave += OnLeave;
            }
            try { tl.LayoutChanged(); } catch { }
            tl.Invalidate();
            if (!logged) { logged = true; DarkEngine.Log("Grade DevExpress (TreeList) escurecida: " + tl.GetType().FullName + " (" + tl.Name + ")"); }
            return true;
        }

        static void Set(AppearanceObject ap, Color b, Color f)
        {
            Journal.SnapAll(ap, null); Journal.SnapAll(ap.Options, null);
            ap.BackColor = b; ap.BackColor2 = Color.Empty; ap.Options.UseBackColor = true;
            ap.ForeColor = f; ap.Options.UseForeColor = true;
        }

        static void OnDisposed(object s, EventArgs e)
        {
            TreeList tl = s as TreeList;
            if (tl == null) return;
            done.Remove(tl); hot.Remove(tl); hookedTl.Remove(tl);
        }

        static void OnHeader(object s, CustomDrawColumnHeaderEventArgs e)
        { if (!DarkEngine.On) return;
            try
            {
                Graphics g = e.Graphics;
                Rectangle r = e.Bounds;
                Color bg = e.Pressed ? DarkEngine.MenuHot : (e.HotTrack && e.Column != null ? DarkEngine.HotBg : DarkEngine.Chrome);
                using (SolidBrush b = new SolidBrush(bg)) g.FillRectangle(b, r);
                using (Pen p = new Pen(Line))
                {
                    g.DrawLine(p, r.Left, r.Bottom - 1, r.Right - 1, r.Bottom - 1);
                    if (e.ColumnType == HitInfoType.Column || e.ColumnType == HitInfoType.ColumnButton) g.DrawLine(p, r.Right - 1, r.Top + 3, r.Right - 1, r.Bottom - 2);
                }
                if (e.Column != null && e.ColumnType == HitInfoType.Column)
                {
                    string cap = e.Column.Caption;
                    if (string.IsNullOrEmpty(cap)) cap = e.Column.FieldName;
                    Rectangle tr = e.CaptionRect;
                    if (tr.Width <= 0) tr = Rectangle.Inflate(r, -4, 0);
                    string so = e.Column.SortOrder.ToString();
                    bool sorted = so == "Ascending" || so == "Descending";
                    Rectangle sr = e.SortShapeRect;
                    if (sorted && sr.Width > 0 && sr.Left > tr.Left) tr.Width = Math.Max(0, Math.Min(tr.Right, sr.Left - 2) - tr.Left);
                    Font f = e.Appearance.Font ?? ((Control)s).Font;
                    TextFormatFlags fl = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
                    string ha = "";
                    try { if (e.Column.AppearanceHeader.Options.UseTextOptions) ha = e.Column.AppearanceHeader.HAlignment.ToString(); } catch { }
                    if (ha == "Center") fl |= TextFormatFlags.HorizontalCenter; else if (ha == "Far") fl |= TextFormatFlags.Right;
                    TextRenderer.DrawText(g, cap, f, tr, e.Pressed ? Color.White : DarkEngine.Fg, fl);
                    if (sorted && sr.Width > 0)
                    {
                        int cx = sr.Left + sr.Width / 2, cy = sr.Top + sr.Height / 2;
                        Point[] tri = so == "Ascending"
                            ? new Point[] { new Point(cx - 4, cy + 2), new Point(cx + 4, cy + 2), new Point(cx, cy - 2) }
                            : new Point[] { new Point(cx - 4, cy - 2), new Point(cx + 4, cy - 2), new Point(cx, cy + 2) };
                        using (SolidBrush b = new SolidBrush(DarkEngine.FgDim)) g.FillPolygon(b, tri);
                    }
                }
                e.Handled = true;
            }
            catch { }
        }

        static void OnButton(object s, CustomDrawNodeButtonEventArgs e)
        { if (!DarkEngine.On) return;
            try
            {
                Graphics g = e.Graphics;
                Rectangle r = e.Bounds;
                // fundo da linha (a celula vizinha usa a aparencia da linha)
                Color bg = e.Appearance.BackColor;
                if (bg.IsEmpty || DarkEngine.IsLight(bg)) bg = DarkEngine.Editor;
                using (SolidBrush b = new SolidBrush(bg)) g.FillRectangle(b, r);
                int sz = Math.Max(4, Math.Min(r.Width, r.Height) / 3);
                int cx = r.Left + r.Width / 2, cy = r.Top + r.Height / 2;
                Point[] pts = e.Expanded
                    ? new Point[] { new Point(cx - sz, cy - sz / 2), new Point(cx, cy + sz / 2 + 1), new Point(cx + sz, cy - sz / 2) }
                    : new Point[] { new Point(cx - sz / 2, cy - sz), new Point(cx + sz / 2 + 1, cy), new Point(cx - sz / 2, cy + sz) };
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (Pen p = new Pen(e.Expanded ? DarkEngine.Fg : DarkEngine.FgSoft, 1.6f))
                {
                    p.LineJoin = System.Drawing.Drawing2D.LineJoin.Round; p.StartCap = System.Drawing.Drawing2D.LineCap.Round; p.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                    g.DrawLines(p, pts);
                }
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.Default;
                e.Handled = true;
            }
            catch { }
        }

        static void OnEmpty(object s, CustomDrawEmptyAreaEventArgs e)
        { if (!DarkEngine.On) return;
            try
            {
                using (SolidBrush b = new SolidBrush(DarkEngine.Editor))
                {
                    if (e.EmptyAreaRegion != null) e.Graphics.FillRegion(b, e.EmptyAreaRegion);
                    else e.Graphics.FillRectangle(b, e.Bounds);
                }
                e.Handled = true;
            }
            catch { }
        }

        static bool IsSelected(TreeList tl, TreeListNode n)
        {
            try { return tl.Selection != null && tl.Selection.Contains(n); } catch { return false; }
        }

        static void OnCellStyle(object s, GetCustomNodeCellStyleEventArgs e)
        { if (!DarkEngine.On) return;
            try
            {
                TreeList tl = (TreeList)s;
                AppearanceObject ap = e.Appearance;
                TreeListNode n = e.Node;
                bool focused = n != null && n == tl.FocusedNode;
                bool sel = n != null && IsSelected(tl, n);
                Color f = ap.ForeColor;
                bool gray = !f.IsEmpty && (f.IsKnownColor && f.ToKnownColor() == KnownColor.GrayText || f.ToArgb() == SystemColors.GrayText.ToArgb() ||
                            (Math.Max(f.R, Math.Max(f.G, f.B)) - Math.Min(f.R, Math.Min(f.G, f.B)) < 24 && DarkEngine.Lum(f) > 90 && DarkEngine.Lum(f) < 175));
                if (focused && tl.Focused) { ap.ForeColor = gray ? DarkEngine.T(200, 214, 230) : Color.White; ap.Options.UseForeColor = true; return; }
                if (gray) { ap.ForeColor = GrayFg; ap.Options.UseForeColor = true; }
                else if (DarkEngine.IsDarkText(f))
                {
                    bool blue = f.B > 120 && f.B > f.R + 40;
                    ap.ForeColor = blue ? DarkEngine.Link : (sel ? DarkEngine.FgBright : DarkEngine.Fg); ap.Options.UseForeColor = true;
                }
                if (focused || sel) return;
                TreeListNode h;
                if (hot.TryGetValue(tl, out h) && h == n) { ap.BackColor = DarkEngine.HotBg; ap.BackColor2 = Color.Empty; ap.Options.UseBackColor = true; }
                else if (DarkEngine.IsLight(ap.BackColor)) { ap.BackColor = DarkEngine.Editor; ap.BackColor2 = Color.Empty; ap.Options.UseBackColor = true; }
            }
            catch { }
        }

        static object Prop(object o, string name)
        {
            if (o == null) return null;
            // propriedades redeclaradas com 'new' (ex.: Properties dos editores DevExpress): usa a mais derivada
            for (Type t = o.GetType(); t != null; t = t.BaseType)
            {
                PropertyInfo p = t.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (p != null && p.GetIndexParameters().Length == 0) return p.GetValue(o, null);
            }
            return null;
        }

        // caixas "Is Collection"/"Nullable" etc.: glyph escuro (o estado desabilitado que o GeneXus marca continua visivel)
        static void OnCell(object s, CustomDrawNodeCellEventArgs e)
        { if (!DarkEngine.On) return;
            try
            {
                object vi = e.EditViewInfo;
                if (vi == null || vi.GetType().Name.IndexOf("CheckEditViewInfo", StringComparison.Ordinal) < 0) return;
                object ci = Prop(vi, "CheckInfo");
                if (ci == null) return;
                object cs = Prop(ci, "CheckState");
                int state = 0;
                try { state = Convert.ToInt32(Prop(ci, "State")); } catch { }
                int kind = 0;
                if (cs is CheckState) kind = (CheckState)cs == CheckState.Checked ? 1 : (CheckState)cs == CheckState.Indeterminate ? 2 : 0;
                else { object v = e.CellValue; if (v is bool && (bool)v) kind = 1; }
                bool dis = (state & 4) != 0;
                bool hotState = (state & 1) != 0;
                Rectangle r = e.Bounds;
                Color bg = e.Appearance.BackColor;
                if (bg.IsEmpty || DarkEngine.IsLight(bg)) bg = DarkEngine.Editor;
                using (SolidBrush b = new SolidBrush(bg)) e.Graphics.FillRectangle(b, r);
                int gs = Math.Max(11, Math.Min(r.Height - 4, (int)Math.Round(13 * e.Graphics.DpiY / 96f)));
                Rectangle gr = ((Rectangle)(Prop(ci, "GlyphRect") ?? Rectangle.Empty));
                Point at = gr.Width > 0 && r.Contains(gr.Location) ? new Point(gr.Left + (gr.Width - gs) / 2, gr.Top + (gr.Height - gs) / 2) : new Point(r.Left + (r.Width - gs) / 2, r.Top + (r.Height - gs) / 2);
                Color gbg, gbd, mark;
                if (dis) { gbg = kind == 1 ? DarkEngine.T(46, 58, 74) : DarkEngine.T(26, 33, 44); gbd = DarkEngine.T(70, 80, 96); mark = kind == 2 ? DarkEngine.T(90, 102, 120) : DarkEngine.FgDim; }
                else if (kind == 1) { gbg = hotState ? DarkEngine.T(58, 155, 220) : DarkEngine.Accent; gbd = hotState ? DarkEngine.Link : DarkEngine.Accent; mark = Color.White; }
                else { gbg = hotState ? DarkEngine.Input : DarkEngine.Panel; gbd = hotState ? DarkEngine.Link : DarkEngine.T(90, 101, 119); mark = DarkEngine.Link; }
                using (Bitmap bmp = DarkEngine.Glyph(new Size(gs, gs), gbg, gbd, kind, mark)) e.Graphics.DrawImageUnscaled(bmp, at);
                e.Handled = true;
            }
            catch { }
        }

        static void OnMove(object s, MouseEventArgs e)
        { if (!DarkEngine.On) return;
            try
            {
                TreeList tl = (TreeList)s;
                TreeListHitInfo hi = tl.CalcHitInfo(e.Location);
                TreeListNode n = hi != null ? hi.Node : null;
                TreeListNode old;
                hot.TryGetValue(tl, out old);
                if (old == n) return;
                hot[tl] = n;
                if (old != null) tl.InvalidateNode(old);
                if (n != null) tl.InvalidateNode(n);
            }
            catch { }
        }

        static void OnLeave(object s, EventArgs e)
        {
            try
            {
                TreeList tl = (TreeList)s;
                TreeListNode old;
                if (hot.TryGetValue(tl, out old) && old != null) { hot[tl] = null; tl.InvalidateNode(old); }
            }
            catch { }
        }

        // editores na celula (textbox / combo do Type / autocomplete): fundo escuro, lista do combo escura
        static void OnShownEditor(object s, EventArgs e)
        { if (!DarkEngine.On) return;
            try
            {
                TreeList tl = (TreeList)s;
                Control ed = tl.ActiveEditor;
                if (ed == null) return;
                DarkEditor(ed);
            }
            catch (Exception ex) { DarkEngine.LogOnce("dxeditor", "Editor da grade DevExpress: " + ex.Message); }
        }

        public static void DarkEditor(Control ed)
        {
            Journal.Snap(ed); foreach (Control k0 in ed.Controls) Journal.Snap(k0);
            object props = Prop(ed, "Properties");
            if (props != null)
            {
                foreach (string n in new string[] { "Appearance", "AppearanceFocused", "AppearanceReadOnly", "AppearanceDropDown", "AppearanceDropDownHeader" })
                {
                    AppearanceObject ap = Prop(props, n) as AppearanceObject;
                    if (ap == null) continue;
                    Set(ap, n == "AppearanceDropDownHeader" ? DarkEngine.Chrome : DarkEngine.Input, DarkEngine.Fg);
                }
                AppearanceObject dis = Prop(props, "AppearanceDisabled") as AppearanceObject;
                if (dis != null) Set(dis, DarkEngine.Panel, DarkEngine.FgDisabled);
            }
            try { ed.BackColor = DarkEngine.Input; ed.ForeColor = DarkEngine.Fg; } catch { }
            foreach (Control k in ed.Controls)
            {
                try { if (k is TextBoxBase) { k.BackColor = DarkEngine.Input; k.ForeColor = DarkEngine.Fg; } } catch { }
            }
        }

        // DevExpress pinta barras de rolagem / botao do combo com o tema "scrollbar"/"combobox" aberto sem janela:
        // trocamos so o handle em cache do pintor DevExpress (em memoria) pelas versoes escuras do Windows.
        static IntPtr darkScrollTheme, darkComboTheme;
        static FieldInfo themesField;
        static bool themeFailed;
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] static extern IntPtr OpenThemeData(IntPtr hwnd, string cls);

        public static void EnsureDxThemes()
        {
            if (themeFailed) return;
            try
            {
                DevExpress.Utils.WXPaint.WXPPainter p = DevExpress.Utils.WXPaint.WXPPainter.Default;
                if (p == null || !p.ThemesEnabled) return;
                if (themesField == null) themesField = typeof(DevExpress.Utils.WXPaint.WXPPainter).GetField("themes", BindingFlags.NonPublic | BindingFlags.Instance);
                if (themesField == null) { themeFailed = true; DarkEngine.Log("DevExpress: cache de temas nao encontrado"); return; }
                System.Collections.Hashtable t = themesField.GetValue(p) as System.Collections.Hashtable;
                if (t == null) return;
                if (darkScrollTheme == IntPtr.Zero) darkScrollTheme = OpenThemeData(IntPtr.Zero, "DarkMode_Explorer::ScrollBar");
                if (darkComboTheme == IntPtr.Zero) darkComboTheme = OpenThemeData(IntPtr.Zero, "DarkMode_CFD::Combobox");
                if (!origThemesSaved) { origThemesSaved = true; origScroll = t.ContainsKey("scrollbar") ? t["scrollbar"] : null; origCombo = t.ContainsKey("combobox") ? t["combobox"] : null; }
                bool changed = false;
                if (darkScrollTheme != IntPtr.Zero && !(t.ContainsKey("scrollbar") && (IntPtr)t["scrollbar"] == darkScrollTheme)) { t["scrollbar"] = darkScrollTheme; changed = true; }
                if (darkComboTheme != IntPtr.Zero && !(t.ContainsKey("combobox") && (IntPtr)t["combobox"] == darkComboTheme)) { t["combobox"] = darkComboTheme; changed = true; }
                if (changed) DarkEngine.LogOnce("dxthemes", "DevExpress: barras de rolagem/combos com tema escuro do Windows (scroll=" + (darkScrollTheme != IntPtr.Zero) + ", combo=" + (darkComboTheme != IntPtr.Zero) + ")");
            }
            catch (Exception ex) { themeFailed = true; DarkEngine.Log("DevExpress temas: " + ex.Message); }
        }
        // modo claro ao vivo: devolve os temas originais do pintor DevExpress e libera a reaplicacao
        static bool origThemesSaved; static object origScroll, origCombo;
        public static void Unapply()
        {
            done.Clear(); hot.Clear();
            try
            {
                if (origThemesSaved && themesField != null)
                {
                    DevExpress.Utils.WXPaint.WXPPainter p = DevExpress.Utils.WXPaint.WXPPainter.Default;
                    System.Collections.Hashtable t = p == null ? null : themesField.GetValue(p) as System.Collections.Hashtable;
                    if (t != null)
                    {
                        if (origScroll != null) t["scrollbar"] = origScroll; else t.Remove("scrollbar");
                        if (origCombo != null) t["combobox"] = origCombo; else t.Remove("combobox");
                    }
                    origThemesSaved = false;
                }
            }
            catch (Exception ex) { DarkEngine.Log("DevExpress temas (claro): " + ex.Message); }
            foreach (TreeList tl in new List<TreeList>(hookedTl)) { try { if (!tl.IsDisposed) { tl.LayoutChanged(); tl.Invalidate(); } } catch { } }
        }
    }

    // ---------- Designer de relatorio (aba Layout da Procedure: XtraReports) ----------
    // Visual do GeneXus Next: fora da pagina #181F2A, pagina #11151C com borda #333D47, regua #242D3C com marcas #8A909B,
    // faixa do printBlock #2C3C50 com titulo #DCE2EB, faixa selecionada com destaque #2C8DCE, grade pontilhada #333D47.
    // Tudo em memoria: a tabela de cores do XtraReports e trocada e cada parte do designer pinta num bitmap proprio
    // (OnPaint do proprio controle), que e convertido para o escuro antes de ir para a tela. O relatorio da KB nao e alterado.
    // Se qualquer coisa falhar, aquela parte volta a pintar sozinha (cores originais), nunca fica preta.
    public class DarkReportColors : DevExpress.XtraReports.Design.Office2003Colors
    {
        static Color P { get { return DarkEngine.Panel; } }
        static Color C { get { return DarkEngine.Chrome; } }
        static Color B { get { return DarkEngine.Border; } }
        static Color S { get { return DarkEngine.T(44, 60, 80); } }
        static Color Acc { get { return DarkEngine.T(44, 141, 206); } }
        static Color SelFill { get { return DarkEngine.T(32, 74, 112); } }
        static Color SelFill2 { get { return DarkEngine.T(38, 86, 128); } }
        // regua e canto: cores originais (claras); a pintura propria converte branco -> #242D3C e preto -> #8A909B
        internal static DevExpress.XtraReports.Design.Office2003Colors Orig;
        public override Color ReportBackground { get { return P; } }
        public override Color RulerBackground { get { return Orig != null ? Orig.RulerBackground : Color.White; } }
        public override Color CornerPanelOuterBorder { get { return Orig != null ? Orig.CornerPanelOuterBorder : Color.Gray; } }
        public override Color CornerPanelInnerBorder { get { return Orig != null ? Orig.CornerPanelInnerBorder : Color.White; } }
        public override Color RulerRightMargin { get { return Orig != null ? Orig.RulerRightMargin : Color.LightGray; } }
        public override Color PopupFormBorder { get { return B; } }
        public override Color PopupFormBackground1 { get { return P; } }
        public override Color PopupFormBackground2 { get { return P; } }
        public override Color PopupFormCaption1 { get { return C; } }
        public override Color PopupFormCaption2 { get { return C; } }
        public override Color PopupFormText { get { return DarkEngine.Fg; } }
        public override Color ComponentTrayBackground { get { return P; } }
        public override Color SliderContentInner { get { return DarkEngine.T(70, 82, 98); } }
        public override Color SliderContentOuter { get { return DarkEngine.T(110, 122, 138); } }
        public override Color BandButtonSign { get { return DarkEngine.Fg; } }
        public override Color SmartTagBackColor { get { return C; } }
        public override Color[] BandColorsLevel0 { get { return new Color[] { S, S, S, S }; } }
        public override Color[] BandColorsLevel1 { get { return new Color[] { S, S, S, S }; } }
        public override Color[] BandColorsLevel2 { get { return new Color[] { S, S, S, S }; } }
        public override Color[] SelectedBandColors { get { return new Color[] { SelFill, SelFill, SelFill2, SelFill2 }; } }
        public override Color[] BandBorderColorsLevel0 { get { return new Color[] { B, B }; } }
        public override Color[] BandBorderColorsLevel1 { get { return new Color[] { B, B }; } }
        public override Color[] BandBorderColorsLevel2 { get { return new Color[] { B, B }; } }
        public override Color[] SelectedBandBorderColors { get { return new Color[] { Acc, Acc }; } }
        public override Color[] BandButtonBorderColorsLevel0 { get { return new Color[] { DarkEngine.HotBorder, DarkEngine.HotBorder }; } }
        public override Color[] BandButtonBorderColorsLevel1 { get { return new Color[] { DarkEngine.HotBorder, DarkEngine.HotBorder }; } }
        public override Color[] BandButtonBorderColorsLevel2 { get { return new Color[] { DarkEngine.HotBorder, DarkEngine.HotBorder }; } }
        public override Color[] SelectedBandButtonBorderColors { get { return new Color[] { Acc, Acc }; } }
    }

    // titulo das bandas (printBlock1...) em texto claro sobre a faixa
    public class DarkBandCaptionPainter : DevExpress.XtraReports.Design.BandCaptionPainterOffice2003
    {
        protected override Color GetForeColor(DevExpress.XtraReports.Design.BandCaptionViewInfo viewInfo) { return DarkEngine.Fg; }
    }

    public class DarkReportPaintStyle : DevExpress.XtraReports.Design.ReportPaintStyleOffice2003
    {
        public override DevExpress.XtraReports.Design.BandCaptionPainter CreateBandCaptionPainter(DevExpress.LookAndFeel.UserLookAndFeel lookAndFeel) { return new DarkBandCaptionPainter(); }
    }

    public static class ReportTheme
    {
        public static Color Page { get { return DarkEngine.Editor; } }      // #11151C pagina
        public static Color Outside { get { return DarkEngine.Panel; } }    // #181F2A fora da pagina
        static bool colorsDone;
        internal static volatile bool failed;
        static readonly HashSet<IntPtr> filtered = new HashSet<IntPtr>();
        static readonly HashSet<Control> backHooked = new HashSet<Control>();
        static object oldColors; static System.Collections.Hashtable oldStyles;
        static readonly List<Control> frames = new List<Control>();

        static void EnsureColors()
        {
            if (colorsDone || failed) return;
            try
            {
                Type t = typeof(DevExpress.XtraReports.Design.Office2003Colors);
                object cur = DevExpress.XtraReports.Design.Office2003Colors.Current; // fixa o tipo de tema XP atual
                FieldInfo f = t.GetField("office2003Colors", BindingFlags.NonPublic | BindingFlags.Static);
                if (f == null) { failed = true; DarkEngine.Log("Relatorio: tabela de cores nao encontrada; designer fica com as cores originais"); return; }
                FieldInfo ps = typeof(DevExpress.XtraReports.Design.ReportPaintStyles).GetField("paintStyles", BindingFlags.NonPublic | BindingFlags.Static);
                System.Collections.Hashtable styles = ps != null ? ps.GetValue(null) as System.Collections.Hashtable : null;
                oldColors = f.GetValue(null);
                DarkReportColors.Orig = (oldColors as DevExpress.XtraReports.Design.Office2003Colors) ?? (cur as DevExpress.XtraReports.Design.Office2003Colors);
                if (DarkReportColors.Orig is DarkReportColors) DarkReportColors.Orig = null;
                if (!(cur is DarkReportColors)) f.SetValue(null, new DarkReportColors());
                if (styles != null)
                {
                    oldStyles = (System.Collections.Hashtable)styles.Clone();
                    DarkReportPaintStyle dark = new DarkReportPaintStyle();
                    foreach (string k in new string[] { "Office2003", "Skin", "Flat" }) styles[k] = dark;
                }
                // fundo da pagina: o designer continua pintando branco; o filtro converte para #11151C (sem filtro = pagina branca legivel)
                colorsDone = true;
                foreach (Control k in new List<Control>(frames)) { try { MethodInfo cv = k.GetType().GetMethod("CreateViewInfos", BindingFlags.NonPublic | BindingFlags.Instance); if (cv != null) cv.Invoke(k, null); k.Invalidate(true); } catch { } }
                DarkEngine.Log("Designer de relatorio: cores escuras do GeneXus Next em memoria (regua, faixas, fora da pagina)" + (styles != null ? "" : " [estilo de pintura nao encontrado]"));
            }
            catch (Exception ex) { failed = true; DarkEngine.Log("Relatorio cores: " + ex.GetType().Name + ": " + ex.Message + " (designer fica com as cores originais)"); }
        }

        // c = qualquer controle cujo tipo e do XtraReports
        public static void Attach(Control c)
        {
            EnsureColors();
            if (failed || !c.IsHandleCreated) return;
            string n = c.GetType().FullName;
            int kind = -1;
            if (n == "DevExpress.XtraReports.Design.ReportFrame+FramePanel") kind = 0;
            else if (n.StartsWith("DevExpress.XtraReports.Design.Ruler.", StringComparison.Ordinal) || n == "DevExpress.XtraReports.Design.ReportFrame+CornerPanel") kind = 1;
            if (n == "DevExpress.XtraReports.Design.ReportFrame" || n == "DevExpress.XtraReports.Design.ReportFrame+ScrollablePanel" || kind == 0)
            {
                try
                {
                    if (c.BackColor != Outside) c.BackColor = Outside;
                    if (backHooked.Add(c))
                    {
                        // o designer reaplica a cor do estilo (UpdatePaintStyle): volta para o escuro
                        c.BackColorChanged += delegate(object s, EventArgs e) { try { Control k = (Control)s; if (!failed && DarkEngine.On && DarkEngine.IsLight(k.BackColor)) k.BackColor = Outside; } catch { } };
                        c.Disposed += delegate(object s, EventArgs e) { backHooked.Remove((Control)s); };
                    }
                }
                catch { }
            }
            if (kind < 0 || filtered.Contains(c.Handle)) return;
            filtered.Add(c.Handle);
            if (kind == 0)
            {
                try
                {
                    MethodInfo cv = c.GetType().GetMethod("CreateViewInfos", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (cv != null) cv.Invoke(c, null);
                    frames.Add(c);
                    c.Disposed += delegate(object s, EventArgs e) { frames.Remove((Control)s); };
                }
                catch (Exception ex) { DarkEngine.LogOnce("repviewinfos", "Relatorio: nao recriou as faixas: " + ex.Message); }
            }
            new ReportPaintFilter(c, kind == 0);
            c.Invalidate(true);
            DarkEngine.LogOnce("repfilter:" + n, "Designer de relatorio: pintura escura (bitmap proprio) em " + n);
            c.HandleDestroyed += delegate(object s, EventArgs e) { try { filtered.Remove(((Control)s).Handle); } catch { } };
        }

        // modo claro ao vivo: devolve a tabela de cores / estilos de pintura originais (o filtro de pintura fica inerte)
        public static void Restore()
        {
            if (!colorsDone || failed) return;
            try
            {
                FieldInfo f = typeof(DevExpress.XtraReports.Design.Office2003Colors).GetField("office2003Colors", BindingFlags.NonPublic | BindingFlags.Static);
                if (f != null && oldColors != null) f.SetValue(null, oldColors);
                FieldInfo ps = typeof(DevExpress.XtraReports.Design.ReportPaintStyles).GetField("paintStyles", BindingFlags.NonPublic | BindingFlags.Static);
                System.Collections.Hashtable styles = ps != null ? ps.GetValue(null) as System.Collections.Hashtable : null;
                if (styles != null && oldStyles != null) foreach (System.Collections.DictionaryEntry e in oldStyles) styles[e.Key] = e.Value;
            }
            catch (Exception ex) { DarkEngine.Log("Relatorio (claro): " + ex.Message); }
            colorsDone = false;
            foreach (Control k in new List<Control>(frames))
            {
                try
                {
                    MethodInfo cv = k.GetType().GetMethod("CreateViewInfos", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (cv != null) cv.Invoke(k, null);
                    k.Invalidate(true);
                }
                catch { }
            }
            foreach (Control k in new List<Control>(backHooked)) { try { k.Invalidate(true); } catch { } }
        }

        // desliga tudo e devolve as cores originais (chamado se a pintura falhar)
        internal static void Fallback(string why)
        {
            if (failed) return;
            failed = true;
            DarkEngine.Log("Designer de relatorio: tema escuro desligado, voltando as cores originais (" + why + ")");
            try
            {
                FieldInfo f = typeof(DevExpress.XtraReports.Design.Office2003Colors).GetField("office2003Colors", BindingFlags.NonPublic | BindingFlags.Static);
                if (f != null && oldColors != null) f.SetValue(null, oldColors);
                FieldInfo ps = typeof(DevExpress.XtraReports.Design.ReportPaintStyles).GetField("paintStyles", BindingFlags.NonPublic | BindingFlags.Static);
                System.Collections.Hashtable styles = ps != null ? ps.GetValue(null) as System.Collections.Hashtable : null;
                if (styles != null && oldStyles != null) foreach (System.Collections.DictionaryEntry e in oldStyles) styles[e.Key] = e.Value;
                foreach (Control k in new List<Control>(backHooked)) { try { k.ResetBackColor(); k.Invalidate(true); } catch { } }
                foreach (Control k in new List<Control>(frames))
                {
                    try
                    {
                        MethodInfo cv = k.GetType().GetMethod("CreateViewInfos", BindingFlags.NonPublic | BindingFlags.Instance);
                        if (cv != null) cv.Invoke(k, null);
                        k.Invalidate(true);
                    }
                    catch { }
                }
            }
            catch { }
        }
    }

    // Pinta a parte do designer (faixas/pagina ou regua) num bitmap usando o OnPaint do proprio controle, converte
    // tinta preta -> clara e branco -> fundo escuro (cores de verdade ficam), e copia para a tela. Nao le pixels da tela.
    public class ReportPaintFilter : NativeWindow
    {
        readonly Control owner;
        readonly bool page;
        readonly Color ink, bg;
        bool broken;
        [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] struct PAINTSTRUCT { public IntPtr hdc; public bool fErase; public RECT rc; public bool fRestore; public bool fIncUpdate; [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] r; }
        [DllImport("user32.dll")] static extern IntPtr BeginPaint(IntPtr h, out PAINTSTRUCT ps);
        [DllImport("user32.dll")] static extern bool EndPaint(IntPtr h, ref PAINTSTRUCT ps);
        static MethodInfo onPaint, onPaintBg;
        static Color GridDot { get { return DarkEngine.Border; } }
        static readonly bool testFail = Environment.GetEnvironmentVariable("GXDARK_TEST_REPORTFAIL") == "1";   // so para o teste de seguranca

        public ReportPaintFilter(Control c, bool isPage)
        {
            owner = c; page = isPage;
            ink = isPage ? DarkEngine.Fg : DarkEngine.FgDim;
            bg = isPage ? ReportTheme.Page : DarkEngine.Chrome;
            if (onPaint == null)
            {
                onPaint = typeof(Control).GetMethod("OnPaint", BindingFlags.NonPublic | BindingFlags.Instance);
                onPaintBg = typeof(Control).GetMethod("OnPaintBackground", BindingFlags.NonPublic | BindingFlags.Instance);
            }
            AssignHandle(c.Handle);
            c.HandleDestroyed += delegate { try { ReleaseHandle(); } catch { } };
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg != 0x000F || m.WParam != IntPtr.Zero || broken || ReportTheme.failed || !DarkEngine.On || onPaint == null || owner.IsDisposed) { base.WndProc(ref m); return; }
            PAINTSTRUCT ps;
            IntPtr hdc = BeginPaint(m.HWnd, out ps);
            bool ok = false;
            try
            {
                Rectangle r = Rectangle.FromLTRB(ps.rc.Left, ps.rc.Top, ps.rc.Right, ps.rc.Bottom);
                r.Intersect(owner.ClientRectangle);
                if (r.Width <= 0 || r.Height <= 0 || hdc == IntPtr.Zero) ok = true;
                else
                {
                    using (Bitmap bmp = new Bitmap(r.Width, r.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                    {
                        using (Graphics g = Graphics.FromImage(bmp))
                        {
                            g.TranslateTransform(-r.X, -r.Y);
                            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                            g.SetClip(r);
                            using (PaintEventArgs pe = new PaintEventArgs(g, r))
                            {
                                onPaintBg.Invoke(owner, new object[] { pe });
                                onPaint.Invoke(owner, new object[] { pe });
                            }
                        }
                        if (testFail) throw new InvalidOperationException("falha simulada (teste)");
                        Remap(bmp);
                        using (Graphics wg = Graphics.FromHdc(hdc))
                        {
                            wg.DrawImageUnscaled(bmp, r.X, r.Y);
                            if (page)
                            {
                                // borda da pagina (#333D47), visivel contra o fundo de fora #181F2A
                                Rectangle cr = owner.ClientRectangle;
                                using (Pen p = new Pen(DarkEngine.Border)) wg.DrawRectangle(p, cr.X, cr.Y, cr.Width - 1, cr.Height - 1);
                            }
                        }
                    }
                    ok = true;
                }
            }
            catch (Exception ex)
            {
                broken = true;
                Exception e = ex is TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex;
                DarkEngine.Log("Designer de relatorio: pintura escura falhou em " + owner.GetType().Name + " (" + e.GetType().Name + ": " + e.Message + ")");
            }
            finally
            {
                if (!ok)
                {
                    // nunca deixa a area preta/vazia: volta as cores originais e pinta ja do jeito original do DevExpress
                    ReportTheme.Fallback("erro de pintura");
                    try
                    {
                        if (hdc != IntPtr.Zero)
                            using (Graphics wg = Graphics.FromHdc(hdc))
                            using (PaintEventArgs pe = new PaintEventArgs(wg, owner.ClientRectangle))
                            {
                                onPaintBg.Invoke(owner, new object[] { pe });
                                onPaint.Invoke(owner, new object[] { pe });
                            }
                    }
                    catch { }
                }
                EndPaint(m.HWnd, ref ps);
            }
            m.Result = IntPtr.Zero;
            if (!ok) { try { Control o = owner; o.BeginInvoke((MethodInvoker)delegate { try { o.Invalidate(true); } catch { } }); } catch { } }
        }

        bool IsPalette(int R, int G, int B)
        {
            Color[] pal = { DarkEngine.Editor, DarkEngine.Panel, DarkEngine.Chrome, DarkEngine.Border, ReportTheme.Page, ReportTheme.Outside, DarkEngine.T(28, 35, 47) };
            foreach (Color c in pal) if (c.R == R && c.G == G && c.B == B) return true;
            return false;
        }

        void Remap(Bitmap bmp)
        {
            Rectangle all = new Rectangle(0, 0, bmp.Width, bmp.Height);
            System.Drawing.Imaging.BitmapData d = bmp.LockBits(all, System.Drawing.Imaging.ImageLockMode.ReadWrite, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            try
            {
                int w = bmp.Width, hh = bmp.Height, row = d.Stride / 4;
                int[] px = new int[row * hh];
                Marshal.Copy(d.Scan0, px, 0, px.Length);
                int[] src = (int[])px.Clone();
                Dictionary<int, int> memo = new Dictionary<int, int>();
                for (int y = 0; y < hh; y++)
                {
                    int o = y * row;
                    for (int x = 0; x < w; x++)
                    {
                        int v = src[o + x] | unchecked((int)0xFF000000);
                        int R = (v >> 16) & 0xFF, G = (v >> 8) & 0xFF, B = v & 0xFF;
                        int mx = Math.Max(R, Math.Max(G, B)), mn = Math.Min(R, Math.Min(G, B));
                        if (mx - mn > 26)
                        {
                            int nc;
                            if (page && NearCaption(src, row, w, x, o)) nc = v;            // faixa do printBlock (icone, botao): fica
                            else if (mx - mn > 60 && Run(src, w, x, o) >= 3)
                            {
                                // cor de verdade do relatorio (texto/fundo colorido): mantem o tom, so clareia se estiver escuro
                                double L0 = 0.299 * R + 0.587 * G + 0.114 * B;
                                if (L0 < 120) { double k = (120 - L0) / (255 - L0); nc = unchecked((int)0xFF000000) | ((int)(R + (255 - R) * k) << 16) | ((int)(G + (255 - G) * k) << 8) | (int)(B + (255 - B) * k); }
                                else nc = v;
                            }
                            else
                            {
                                // texto ClearType (franjas coloridas): inverte canal a canal -> texto claro nitido sobre o escuro
                                double tr = Math.Max(0, Math.Min(1, 1.0 - R / 245.0)), tg = Math.Max(0, Math.Min(1, 1.0 - G / 245.0)), tb = Math.Max(0, Math.Min(1, 1.0 - B / 245.0));
                                nc = unchecked((int)0xFF000000) | ((int)(bg.R + (ink.R - bg.R) * tr) << 16) | ((int)(bg.G + (ink.G - bg.G) * tg) << 8) | (int)(bg.B + (ink.B - bg.B) * tb);
                            }
                            px[o + x] = nc;
                            continue;
                        }
                        if (page && NearCaption(src, row, w, x, o)) { px[o + x] = v; continue; }   // texto/borda da faixa do printBlock
                        if (page && mx < 24 && Isolated(src, row, w, hh, x, y)) { px[o + x] = unchecked((int)0xFF000000) | (GridDot.R << 16) | (GridDot.G << 8) | GridDot.B; continue; }
                        int nv;
                        if (memo.TryGetValue(v, out nv)) { px[o + x] = nv; continue; }
                        if (IsPalette(R, G, B) || (R == ink.R && G == ink.G && B == ink.B)) nv = v;
                        else
                        {
                            // quantidade de "tinta": 0 = branco (fundo), 1 = preto (texto/linha)
                            double L = 0.299 * R + 0.587 * G + 0.114 * B;
                            double t = Math.Max(0, Math.Min(1, 1.0 - L / 245.0));
                            int nr = (int)(bg.R + (ink.R - bg.R) * t), ng = (int)(bg.G + (ink.G - bg.G) * t), nb = (int)(bg.B + (ink.B - bg.B) * t);
                            nv = unchecked((int)0xFF000000) | (nr << 16) | (ng << 8) | nb;
                        }
                        memo[v] = nv;
                        px[o + x] = nv;
                    }
                }
                Marshal.Copy(px, 0, d.Scan0, px.Length);
            }
            finally { bmp.UnlockBits(d); }
        }

        static bool Isolated(int[] src, int row, int w, int h, int x, int y)
        {
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int xx = x + dx, yy = y + dy;
                    if (xx < 0 || yy < 0 || xx >= w || yy >= h) continue;
                    int v = src[yy * row + xx];
                    int R = (v >> 16) & 0xFF, G = (v >> 8) & 0xFF, B = v & 0xFF;
                    if (Math.Max(R, Math.Max(G, B)) < 60) return false;
                }
            return true;
        }

        static int Arg(int v) { int R = (v >> 16) & 0xFF, G = (v >> 8) & 0xFF, B = v & 0xFF; return R >= G && R >= B ? 0 : (G >= B ? 1 : 2); }
        static int Chr(int v) { int R = (v >> 16) & 0xFF, G = (v >> 8) & 0xFF, B = v & 0xFF; return Math.Max(R, Math.Max(G, B)) - Math.Min(R, Math.Min(G, B)); }

        static readonly int[] captionPal = {
            DarkEngine.T(44, 60, 80).ToArgb(), DarkEngine.T(32, 74, 112).ToArgb(), DarkEngine.T(38, 86, 128).ToArgb(),
            DarkEngine.T(44, 141, 206).ToArgb(), DarkEngine.HotBorder.ToArgb() };

        // ha cor da faixa do printBlock a ate 6 px na mesma linha?
        static bool NearCaption(int[] src, int row, int w, int x, int o)
        {
            for (int dx = -6; dx <= 6; dx++)
            {
                int xx = x + dx;
                if (xx < 0 || xx >= w) continue;
                int v = src[o + xx] | unchecked((int)0xFF000000);
                for (int i = 0; i < captionPal.Length; i++) if (v == captionPal[i]) return true;
            }
            return false;
        }

        // comprimento da sequencia horizontal de pixels bem coloridos com o mesmo canal dominante (ate 3)
        static int Run(int[] src, int w, int x, int o)
        {
            int me = Arg(src[o + x]), run = 1;
            for (int d = -1; d <= 1; d += 2)
            {
                int xx = x + d;
                while (xx >= 0 && xx < w && run < 3 && Chr(src[o + xx]) > 60 && Arg(src[o + xx]) == me) { run++; xx += d; }
            }
            return run;
        }
    }

    // ---------- Aba Documentation (wiki: Edit/Design, Preview, History, Back Links) ----------
    // Usa runtimeStyle do MSHTML: estilo so de exibicao, que NAO entra no HTML (innerHTML/outerHTML/salvar) da documentacao.
    // So documentos wiki (controles Artech.*Wiki* ou paginas wikiPage*.html); Web Layout / pre-visualizacao WWP nao sao tocados.
    public static class WikiTheme
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int RegisterWindowMessage(string s);
        [DllImport("user32.dll")] static extern IntPtr SendMessageTimeout(IntPtr h, int msg, IntPtr w, IntPtr l, int flags, int timeout, out IntPtr result);
        [DllImport("oleacc.dll")] static extern int ObjectFromLresult(IntPtr lres, ref Guid riid, IntPtr w, [MarshalAs(UnmanagedType.IUnknown)] out object ppv);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder sb, int n);
        delegate bool EnumProc(IntPtr h, IntPtr l);
        [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr parent, EnumProc cb, IntPtr l);
        [DllImport("user32.dll")] static extern IntPtr GetParent(IntPtr h);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);

        static int msgGetObj;
        static Guid IID_IHTMLDocument2 = new Guid("332C4425-26CB-11D0-B483-00C04FD90119");
        static readonly Dictionary<IntPtr, string> sig = new Dictionary<IntPtr, string>();
        static readonly Dictionary<IntPtr, bool> isWiki = new Dictionary<IntPtr, bool>();
        static bool broken;
        static readonly List<object[]> undo = new List<object[]>();   // (runtimeStyle, propriedade, valor anterior) para o modo claro ao vivo
        static readonly List<object> sheets = new List<object>();
        static void SetR(object rs, string n, object v)
        {
            if (rs == null) return;
            if (undo.Count < 300000) { object old = null; try { old = Get(rs, n); } catch { } undo.Add(new object[] { rs, n, old }); }
            SetP(rs, n, v);
        }
        public static void Undo()
        {
            int n = 0;
            for (int i = undo.Count - 1; i >= 0; i--) { object[] u = undo[i]; try { SetP(u[0], (string)u[1], u[2] ?? ""); n++; } catch { } }
            undo.Clear();
            foreach (object ss in sheets) { try { SetP(ss, "cssText", ""); n++; } catch { } }
            sheets.Clear(); sig.Clear(); isWiki.Clear();
            if (n > 0) DarkEngine.Log("Documentacao (modo claro): " + n + " estilos de exibicao devolvidos");
        }
        static string Bg { get { return DarkEngine.TH("#11151C"); } }
        static string Fg { get { return DarkEngine.TH("#DCE2EB"); } }
        static string LinkC { get { return DarkEngine.TH("#5BA7FF"); } }
        static string Line { get { return DarkEngine.TH("#333D47"); } }
        static string Cell { get { return DarkEngine.TH("#181F2A"); } }

        public static void Scan(List<Form> forms)
        {
            if (broken) return;
            try
            {
                if (msgGetObj == 0) msgGetObj = RegisterWindowMessage("WM_HTML_GETOBJECT");
                List<IntPtr> ies = new List<IntPtr>();
                StringBuilder sb = new StringBuilder(64);
                foreach (Form f in forms)
                {
                    if (f.IsDisposed || !f.IsHandleCreated || f.InvokeRequired) continue;
                    EnumChildWindows(f.Handle, delegate(IntPtr h, IntPtr l)
                    {
                        sb.Length = 0; GetClassName(h, sb, 64);
                        if (sb.ToString() == "Internet Explorer_Server" && IsWindowVisible(h)) ies.Add(h);
                        return true;
                    }, IntPtr.Zero);
                }
                foreach (IntPtr h in ies)
                {
                    try { Apply(h); } catch (Exception ex) { DarkEngine.LogOnce("wiki:" + ex.GetType().Name, "Documentacao (MSHTML): " + ex.Message); }
                }
                if (sig.Count > 64) { List<IntPtr> k = new List<IntPtr>(sig.Keys); foreach (IntPtr h in k) if (!ies.Contains(h)) { sig.Remove(h); isWiki.Remove(h); } }
            }
            catch (Exception ex) { broken = true; DarkEngine.Log("Documentacao (MSHTML) desligada: " + ex.Message); }
        }

        static bool WikiHost(IntPtr ie)
        {
            bool r;
            if (isWiki.TryGetValue(ie, out r)) return r;
            r = false;
            for (IntPtr h = GetParent(ie); h != IntPtr.Zero && !r; h = GetParent(h))
            {
                Control c = Control.FromHandle(h);
                for (; c != null; c = c.Parent)
                {
                    string n = c.GetType().FullName ?? "";
                    if (n.IndexOf(".Wiki", StringComparison.Ordinal) >= 0 || n.IndexOf("WikiEngine", StringComparison.Ordinal) >= 0) { r = true; break; }
                    if (n.IndexOf("WorkWith", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf(".Patterns.", StringComparison.Ordinal) >= 0 ||
                        n.IndexOf("WebForm", StringComparison.Ordinal) >= 0 || n.IndexOf("WebLayout", StringComparison.Ordinal) >= 0) { isWiki[ie] = false; return false; }
                }
                if (c != null) break;
            }
            isWiki[ie] = r;
            return r;
        }

        static object Get(object o, string n) { return o == null ? null : o.GetType().InvokeMember(n, BindingFlags.GetProperty, null, o, null); }
        static void SetP(object o, string n, object v) { if (o != null) o.GetType().InvokeMember(n, BindingFlags.SetProperty, null, o, new object[] { v }); }

        static object Document(IntPtr h)
        {
            IntPtr res;
            SendMessageTimeout(h, msgGetObj, IntPtr.Zero, IntPtr.Zero, 2 /*SMTO_ABORTIFHUNG*/, 1000, out res);
            if (res == IntPtr.Zero) return null;
            object doc;
            if (ObjectFromLresult(res, ref IID_IHTMLDocument2, IntPtr.Zero, out doc) != 0) return null;
            return doc;
        }

        static void Apply(IntPtr h)
        {
            object doc = Document(h);
            if (doc == null) return;
            string url = "";
            try { url = Get(doc, "URL") as string ?? ""; } catch { }
            bool wikiUrl = url.IndexOf("WikiPages", StringComparison.OrdinalIgnoreCase) >= 0 || url.IndexOf("wikiPage", StringComparison.OrdinalIgnoreCase) >= 0;
            if (DarkEngine.IsPreviewUrl(url)) return;
            if (!wikiUrl && !WikiHost(h)) return;
            object all = Get(doc, "all");
            int count = all != null ? Convert.ToInt32(Get(all, "length")) : 0;
            string s = url + "|" + count;
            object body = Get(doc, "body");
            if (body == null) return;
            try { object b0 = Get(body, "runtimeStyle"); if (b0 != null && !(Get(b0, "backgroundColor") as string ?? "").Equals(Bg, StringComparison.OrdinalIgnoreCase)) s += "|reset"; } catch { }
            string last;
            if (sig.TryGetValue(h, out last) && (last == s || last == s + "|css")) return;
            sig[h] = s;
            object rt0 = null;
            try { rt0 = Get(body, "runtimeStyle"); } catch { }
            if (rt0 == null)
            {
                // modo de documento sem runtimeStyle: so documentos somente-leitura (Preview/History) recebem folha de estilo;
                // o editor nao recebe nada para nunca gravar estilo na documentacao
                string dm = ""; bool ce = false;
                try { dm = Get(doc, "designMode") as string ?? ""; } catch { }
                try { ce = Convert.ToBoolean(Get(body, "isContentEditable")); } catch { }
                if (dm.Equals("On", StringComparison.OrdinalIgnoreCase) || ce) { DarkEngine.LogOnce("wikinort:" + url, "Documentacao (edicao) sem runtimeStyle: mantida clara para nao gravar estilos (" + url + ")"); return; }
                if (!s.EndsWith("|css"))
                {
                    object ss = doc.GetType().InvokeMember("createStyleSheet", BindingFlags.InvokeMethod, null, doc, new object[] { "", 0 });
                    SetP(ss, "cssText", "html,body{background:" + Bg + " !important;color:" + Fg + " !important}a{color:" + LinkC + " !important}td,th,table,div,p,span,li{color:" + Fg + " !important;border-color:" + Line + " !important}td,th,table{background-color:" + Cell + " !important}");
                    sheets.Add(ss);
                    sig[h] = url + "|" + count + "|css";
                    DarkEngine.LogOnce("wikicss:" + url, "Documentacao (somente leitura) escurecida por folha de estilo: " + url);
                }
                return;
            }
            if (count > 6000) { DarkEngine.LogOnce("wikibig", "Documentacao muito grande para recolorir (" + count + " elementos)"); return; }
            Style(Get(doc, "documentElement"), true);
            Style(body, true);
            int n = 0;
            for (int i = 0; i < count; i++)
            {
                object el = null;
                try { el = all.GetType().InvokeMember("item", BindingFlags.InvokeMethod, null, all, new object[] { i }); } catch { }
                if (el == null) continue;
                try { if (Element(el)) n++; } catch { }
            }
            sig[h] = url + "|" + count;
            DarkEngine.LogOnce("wiki:" + url, "Documentacao escurecida (so exibicao, runtimeStyle): " + url + " (" + count + " elementos)");
        }

        static void Style(object el, bool root)
        {
            if (el == null) return;
            object rs = Get(el, "runtimeStyle");
            if (rs == null) return;
            SetR(rs, "backgroundColor", Bg);
            SetR(rs, "color", Fg);
            if (root)
            {
                foreach (string p in new string[] { "scrollbarBaseColor", "scrollbarFaceColor", "scrollbarTrackColor", "scrollbarArrowColor", "scrollbarShadowColor", "scrollbarHighlightColor", "scrollbar3dLightColor", "scrollbarDarkShadowColor" })
                {
                    string v = p == "scrollbarTrackColor" ? Bg : p == "scrollbarArrowColor" ? DarkEngine.TH("#8A909B") : p == "scrollbarFaceColor" || p == "scrollbarBaseColor" ? Line : Cell;
                    try { SetR(rs, p, v); } catch { }
                }
            }
        }

        static Color Parse(string v)
        {
            if (string.IsNullOrEmpty(v)) return Color.Empty;
            v = v.Trim();
            if (v == "transparent" || v == "inherit" || v == "auto") return Color.Empty;
            try
            {
                if (v.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
                {
                    string[] p = v.Substring(v.IndexOf('(') + 1).TrimEnd(')').Split(',');
                    return Color.FromArgb(int.Parse(p[0].Trim()), int.Parse(p[1].Trim()), int.Parse(p[2].Trim()));
                }
                return ColorTranslator.FromHtml(v);
            }
            catch { return Color.Empty; }
        }

        static bool Element(object el)
        {
            string tag = (Get(el, "tagName") as string ?? "").ToUpperInvariant();
            if (tag == "IMG" || tag == "SCRIPT" || tag == "STYLE" || tag == "HEAD" || tag == "META" || tag == "LINK" || tag == "TITLE" || tag == "!" || tag == "HTML" || tag == "BODY") return false;
            object cs = Get(el, "currentStyle");
            object rs = Get(el, "runtimeStyle");
            if (cs == null || rs == null) return false;
            bool ch = false;
            Color bg = Parse(Get(cs, "backgroundColor") as string);
            if (!bg.IsEmpty && DarkEngine.Lum(bg) > 150)
            {
                bool input = tag == "INPUT" || tag == "TEXTAREA" || tag == "SELECT" || tag == "BUTTON";
                SetR(rs, "backgroundColor", input ? DarkEngine.TH("#1F2734") : (tag == "TD" || tag == "TH" || tag == "TABLE" || tag == "TR" ? Cell : Bg)); ch = true;
            }
            if (tag == "A")
            {
                object href = null; try { href = Get(el, "href"); } catch { }
                if (href != null && href.ToString().Length > 0) { SetR(rs, "color", LinkC); ch = true; }
            }
            else
            {
                Color fg = Parse(Get(cs, "color") as string);
                if (!fg.IsEmpty && DarkEngine.Lum(fg) < 130)
                {
                    bool blue = fg.B > 120 && fg.B > fg.R + 40;
                    SetR(rs, "color", blue ? LinkC : Fg); ch = true;
                }
            }
            foreach (string side in new string[] { "Top", "Right", "Bottom", "Left" })
            {
                try
                {
                    string st = Get(cs, "border" + side + "Style") as string;
                    if (string.IsNullOrEmpty(st) || st == "none") continue;
                    Color bc = Parse(Get(cs, "border" + side + "Color") as string);
                    if (!bc.IsEmpty && DarkEngine.Lum(bc) > 150) { SetR(rs, "border" + side + "Color", Line); ch = true; }
                }
                catch { }
            }
            return ch;
        }
    }

    // ---------- Lista do autocompletar nativo (Type na grade Structure/Variables) ----------
    public static class AutoSuggestTheme
    {
        delegate bool EnumProc(IntPtr h, IntPtr l);
        [DllImport("user32.dll")] static extern bool EnumThreadWindows(int tid, EnumProc cb, IntPtr l);
        [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr parent, EnumProc cb, IntPtr l);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder sb, int n);
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] static extern int SetWindowTheme(IntPtr hwnd, string app, string idList);
        [DllImport("kernel32.dll")] static extern int GetCurrentThreadId();
        [DllImport("user32.dll")] static extern bool InvalidateRect(IntPtr h, IntPtr r, bool erase);
        static readonly HashSet<IntPtr> done = new HashSet<IntPtr>();
        static readonly Dictionary<IntPtr, IntPtr[]> origLv = new Dictionary<IntPtr, IntPtr[]>();
        [DllImport("user32.dll")] static extern bool IsWindow(IntPtr h);
        public static void Undo()
        {
            foreach (IntPtr h in done)
            {
                try
                {
                    if (!IsWindow(h)) continue;
                    SetWindowTheme(h, null, null);
                    IntPtr[] o;
                    if (origLv.TryGetValue(h, out o)) { SendMessage(h, 0x1001, IntPtr.Zero, o[0]); SendMessage(h, 0x1026, IntPtr.Zero, o[1]); SendMessage(h, 0x1024, IntPtr.Zero, o[2]); }
                    InvalidateRect(h, IntPtr.Zero, true);
                }
                catch { }
            }
            done.Clear(); origLv.Clear();
        }

        static int Cr(Color c) { return c.R | (c.G << 8) | (c.B << 16); }

        public static void Scan()
        {
            StringBuilder sb = new StringBuilder(64);
            List<IntPtr> lists = new List<IntPtr>();
            EnumThreadWindows(GetCurrentThreadId(), delegate(IntPtr h, IntPtr l)
            {
                sb.Length = 0; GetClassName(h, sb, 64);
                if (sb.ToString() == "Auto-Suggest Dropdown")
                    EnumChildWindows(h, delegate(IntPtr k, IntPtr l2)
                    {
                        sb.Length = 0; GetClassName(k, sb, 64);
                        string cn = sb.ToString();
                        if (cn == "SysListView32" || cn == "ScrollBar") lists.Add(k);
                        return true;
                    }, IntPtr.Zero);
                return true;
            }, IntPtr.Zero);
            foreach (IntPtr h in lists)
            {
                if (done.Contains(h)) continue;
                done.Add(h);
                SetWindowTheme(h, "DarkMode_Explorer", null);
                sb.Length = 0; GetClassName(h, sb, 64);
                if (sb.ToString() == "SysListView32")
                {
                    try { origLv[h] = new IntPtr[] { SendMessage(h, 0x1000, IntPtr.Zero, IntPtr.Zero), SendMessage(h, 0x1025, IntPtr.Zero, IntPtr.Zero), SendMessage(h, 0x1023, IntPtr.Zero, IntPtr.Zero) }; } catch { }
                    SendMessage(h, 0x1001 /*LVM_SETBKCOLOR*/, IntPtr.Zero, (IntPtr)Cr(DarkEngine.Input));
                    SendMessage(h, 0x1026 /*LVM_SETTEXTBKCOLOR*/, IntPtr.Zero, (IntPtr)Cr(DarkEngine.Input));
                    SendMessage(h, 0x1024 /*LVM_SETTEXTCOLOR*/, IntPtr.Zero, (IntPtr)Cr(DarkEngine.Fg));
                }
                InvalidateRect(h, IntPtr.Zero, true);
                DarkEngine.LogOnce("autosuggest", "Lista do autocompletar (Auto-Suggest Dropdown) escurecida");
            }
            if (done.Count > 200) { done.Clear(); origLv.Clear(); }
        }
    }
}
