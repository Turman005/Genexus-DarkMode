// Gera em memoria o style library Infragistics escuro a partir do GenexusOffice2010Silver.isl instalado com o GeneXus.
// Porte fiel do make_isl.py (Gabriel) + os ajustes finos do GxDarkTheme.isl distribuido ate a v1 (texto desabilitado,
// abas inativas e setas de scrollbar mais legiveis). Assim o .isl (derivado do arquivo da GeneXus) nao e mais distribuido.
// Tema != Next: todas as cores do resultado passam pela funcao M() do tema (luminancia preservada).
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;

namespace GxDarkTheme
{
    public static class GeradorIsl
    {
        static string C(int r, int g, int b) { return r + ", " + g + ", " + b; }
        static readonly string EDITOR = C(17, 21, 28), PANEL = C(24, 31, 42), CHROME = C(36, 45, 60), MENU = C(36, 45, 60), INPUT = C(31, 39, 52),
            HOVER = C(52, 72, 96), HOTBD = C(78, 104, 136), MENUHOT = C(31, 78, 121), PRESS = C(31, 78, 121), BUTTON = C(51, 61, 71),
            BUTTONH = C(61, 78, 100), BORDER = C(51, 61, 71), ACCENT = C(44, 141, 206), SEL = C(44, 60, 80), SELINACT = C(40, 52, 70),
            FG = C(220, 226, 235), FGB = C(240, 244, 250), FGDIM = C(138, 144, 155), FGDIS = C(110, 122, 138), WHITE = C(255, 255, 255),
            THUMB = C(61, 73, 87), THUMBH = C(86, 103, 122), THUMBP = C(110, 122, 138), LINK = C(91, 167, 255);

        static readonly Dictionary<string, int[]> NAMED = new Dictionary<string, int[]> {
            { "white", new[] { 255, 255, 255 } }, { "whitesmoke", new[] { 245, 245, 245 } }, { "silver", new[] { 192, 192, 192 } },
            { "gainsboro", new[] { 220, 220, 220 } }, { "black", new[] { 0, 0, 0 } }, { "gray", new[] { 128, 128, 128 } },
            { "darkgray", new[] { 169, 169, 169 } }, { "lightgray", new[] { 211, 211, 211 } }, { "peru", new[] { 205, 133, 63 } },
            { "brown", new[] { 165, 42, 42 } }, { "darkorange", new[] { 255, 140, 0 } }, { "gradientactivecaption", new[] { 185, 209, 234 } } };

        // null = nao e cor; {-1} = Transparent; {a,r,g,b}
        static int[] Parse(string v)
        {
            v = v.Trim();
            string lo = v.ToLowerInvariant();
            if (lo == "transparent") return new[] { -1 };
            int[] n;
            if (NAMED.TryGetValue(lo, out n)) return new[] { 255, n[0], n[1], n[2] };
            string[] p = v.Split(',');
            List<int> x = new List<int>();
            foreach (string s in p) { int i; if (!int.TryParse(s.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out i)) return null; x.Add(i); }
            if (x.Count == 3) return new[] { 255, x[0], x[1], x[2] };
            if (x.Count == 4) return x.ToArray();
            return null;
        }

        // colorsys.rgb_to_hls
        static void Hls(int r8, int g8, int b8, out double h, out double l, out double s)
        {
            double r = r8 / 255.0, g = g8 / 255.0, b = b8 / 255.0;
            double mx = Math.Max(r, Math.Max(g, b)), mn = Math.Min(r, Math.Min(g, b));
            l = (mn + mx) / 2.0;
            if (mn == mx) { h = 0; s = 0; return; }
            s = l <= 0.5 ? (mx - mn) / (mx + mn) : (mx - mn) / (2.0 - mx - mn);
            double rc = (mx - r) / (mx - mn), gc = (mx - g) / (mx - mn), bc = (mx - b) / (mx - mn);
            if (r == mx) h = bc - gc; else if (g == mx) h = 2.0 + rc - bc; else h = 4.0 + gc - rc;
            h = h / 6.0; h = h - Math.Floor(h);
        }

        static string Kind(int[] c, out double l)
        {
            double h, s; Hls(c[1], c[2], c[3], out h, out l, out s);
            if (s > 0.25 && 0.15 < l && l < 0.97)
            {
                if (0.50 < h && h < 0.70) return "blue";
                return "warm";
            }
            return "neutral";
        }

        static readonly Regex CONTENT = new Regex(@"^(Grid(Row|Cell|ControlArea|NoRowsMessage|NoVisibleRowsMessage|NoDataSourceMessage|CellProxy|RowEditTemplatePanel)|Tree(ControlArea|Node|Cell)|ListView(ControlArea|Item|GroupItemArea)|ValueList|ComboValueListDropDown|DropDownControlArea|TabClientArea|MdiTabClientArea|Dock.*ContentArea|DockManagerPane$|MessageBox$|MessageBoxContentArea|CalendarCombo|DayView|Schedule|MonthView|TimelineView|GanttView|PrintPreview|EmptyTileArea|UltraTilePanel|LiveTile|ExplorerBar(ControlArea|GroupItemArea)|PopupGallery$)");
        static readonly Regex EDITORS = new Regex(@"^(EditorControl|Ultra(Text|Combo|Masked|Numeric|Currency|DateTime|FontName|TimeSpan|TimeZone|FormattedText|Calculator)?Edit(or|Portion)?$|UltraComboEditPortion|UltraColorPicker|ToolbarEditArea.*|ToolbarItem(ComboBox|TextBox|Editor|MaskedEdit|FontList)|RibbonGroupItem(TextBox|MaskedEdit))");
        static readonly HashSet<string> SELSTATES = new HashSet<string> { "Selected", "Active", "Pressed", "Checked", "HotTrackSelected", "EditMode", "Expanded" };

        static string MapBack(string role, string state, string v)
        {
            int[] c = Parse(v);
            if (c == null || c[0] == -1) return v;
            int a = c[0];
            if (a == 0) return v;
            double l; string k = Kind(c, out l);
            if (a < 255)
            {
                if (l <= 0.5) return v;
                return state == "Normal" ? "Transparent" : Math.Min(a, 30) + ", 255, 255, 255";
            }
            if (k == "warm" || k == "blue")
            {
                if (state == "HotTracked") return HOVER;
                if (role.StartsWith("DockPaneCaption", StringComparison.Ordinal)) return ACCENT;
                if (state == "Pressed") return ACCENT;
                return SEL;
            }
            if (state == "HotTracked") return HOVER;
            if (EDITORS.IsMatch(role)) return l >= 0.7 ? INPUT : HOVER;
            string bas;
            if (l >= 0.93) bas = CONTENT.IsMatch(role) ? PANEL : CHROME;
            else if (l >= 0.84) bas = CHROME;
            else if (l >= 0.62) bas = BORDER;
            else if (l >= 0.45) bas = THUMB;
            else bas = state != "Normal" ? HOVER : EDITOR;
            if (state != null && SELSTATES.Contains(state) && (bas == PANEL || bas == CHROME || bas == EDITOR)) bas = SELINACT;
            return bas;
        }

        static string MapFore(string role, string state, string v)
        {
            int[] c = Parse(v);
            if (c == null || c[0] == -1) return v;
            double l; string k = Kind(c, out l);
            if (k != "neutral") return l < 0.6 ? LINK : v;
            if (l >= 0.97) return WHITE;
            if (l >= 0.6) return FGDIM;
            return (state != null && SELSTATES.Contains(state)) || state == "HotTracked" ? FGB : FG;
        }

        static string MapBorder(string role, string state, string v)
        {
            int[] c = Parse(v);
            if (c == null || c[0] == -1) return v;
            if (c[0] == 0) return v;
            double l; string k = Kind(c, out l);
            if (k != "neutral") return ACCENT;
            return BORDER;
        }

        static void MapAttrs(string role, string state, XmlElement el)
        {
            List<string> names = new List<string>();
            foreach (XmlAttribute a in el.Attributes) names.Add(a.Name);
            foreach (string a in names)
            {
                string v = el.GetAttribute(a);
                if (a == "backColor" || a == "backColor2") el.SetAttribute(a, MapBack(role, state, v));
                else if (a == "foreColor") el.SetAttribute(a, MapFore(role, state, v));
                else if (a.StartsWith("borderColor", StringComparison.Ordinal)) el.SetAttribute(a, MapBorder(role, state, v));
            }
            if (el.HasAttribute("backGradientStyle")) el.SetAttribute("backGradientStyle", "None");
            if (el.HasAttribute("backColor") && el.HasAttribute("backColor2")) el.SetAttribute("backColor2", el.GetAttribute("backColor"));
            string bc = el.HasAttribute("backColor") ? el.GetAttribute("backColor") : null;
            if (!string.IsNullOrEmpty(bc) && !el.HasAttribute("borderColor3DBase"))
            {
                int[] p = Parse(bc);
                if (p != null && p[0] == 255) el.SetAttribute("borderColor3DBase", bc);
            }
            foreach (XmlElement ib in Children(el, "imageBackground")) el.RemoveChild(ib);
            if (el.HasAttribute("imageBackgroundStyle")) el.RemoveAttribute("imageBackgroundStyle");
        }

        sealed class Attrs : List<KeyValuePair<string, string>>
        {
            public bool Has(string k) { foreach (KeyValuePair<string, string> p in this) if (p.Key == k) return true; return false; }
            public string Get(string k) { foreach (KeyValuePair<string, string> p in this) if (p.Key == k) return p.Value; return null; }
            public void Def(string k, string v) { if (!Has(k)) Add(new KeyValuePair<string, string>(k, v)); }
        }

        // st(**kw) do Python: pares nome/valor + defaults
        static Attrs St(params string[] kv)
        {
            Attrs d = new Attrs();
            for (int i = 0; i < kv.Length; i += 2)
            {
                bool rep = false;
                for (int j = 0; j < d.Count; j++) if (d[j].Key == kv[i]) { d[j] = new KeyValuePair<string, string>(kv[i], kv[i + 1]); rep = true; }
                if (!rep) d.Add(new KeyValuePair<string, string>(kv[i], kv[i + 1]));
            }
            if (d.Has("backColor")) { d.Def("backGradientStyle", "None"); d.Def("backHatchStyle", "None"); }
            if (d.Has("borderColor"))
            {
                d.Def("borderColor2", d.Get("borderColor"));
                string bc = d.Get("borderColor");
                string bk = d.Get("backColor") ?? "Transparent";
                d.Def("borderColor3DBase", bc != "Transparent" ? bc : (bk != "Transparent" ? d.Get("backColor") : CHROME));
            }
            else if (d.Has("backColor") && d.Get("backColor") != "Transparent") d.Def("borderColor3DBase", d.Get("backColor"));
            return d;
        }

        sealed class States : List<KeyValuePair<string, Attrs>>
        {
            public States S(string n, Attrs a) { Add(new KeyValuePair<string, Attrs>(n, a)); return this; }
            public Attrs Get(string n) { foreach (KeyValuePair<string, Attrs> p in this) if (p.Key == n) return p.Value; return null; }
            public States Copy() { States s = new States(); s.AddRange(this); return s; }
            public void Set(string n, Attrs a) { for (int i = 0; i < Count; i++) if (this[i].Key == n) { this[i] = new KeyValuePair<string, Attrs>(n, a); return; } Add(new KeyValuePair<string, Attrs>(n, a)); }
        }

        static States TAB, TABAREA, CLIENT, PANELS, CHROMES, SPLIT, TOOLITEM, MENUHEAD, EDIT, BTN, GLYPHBTN, HEADER, ITEM, MENUITEM, SCROLLTRACK, THUMBS, ARROWS, STATUS, STATUSPANEL;
        static readonly List<string> ORoles = new List<string>();
        static readonly Dictionary<string, States> O = new Dictionary<string, States>();
        static readonly HashSet<string> TabRoles = new HashSet<string>();

        static void Put(string names, States st)
        {
            foreach (string n in names.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (!O.ContainsKey(n)) ORoles.Add(n);
                O[n] = st;
            }
        }

        static GeradorIsl()
        {
            TAB = new States()
                .S("Normal", St("backColor", CHROME, "foreColor", FGDIM, "borderColor", CHROME, "borderColor2", CHROME, "borderColor3DBase", CHROME))
                .S("HotTracked", St("backColor", HOVER, "foreColor", WHITE, "borderColor", HOVER, "borderColor2", HOVER))
                .S("Selected", St("backColor", SELINACT, "foreColor", FGB, "borderColor", SELINACT, "borderColor2", SELINACT))
                .S("Active", St("backColor", EDITOR, "foreColor", WHITE, "borderColor", EDITOR, "borderColor2", EDITOR, "borderColor3DBase", EDITOR));
            TABAREA = new States().S("Normal", St("backColor", CHROME, "foreColor", FG, "borderColor", CHROME, "borderColor2", CHROME, "borderColor3DBase", CHROME));
            CLIENT = new States().S("Normal", St("backColor", EDITOR, "foreColor", FG, "borderColor", BORDER, "borderColor2", BORDER, "borderColor3DBase", BORDER));
            PANELS = new States().S("Normal", St("backColor", PANEL, "foreColor", FG, "borderColor", BORDER, "borderColor2", BORDER, "borderColor3DBase", BORDER, "backColorDisabled", PANEL, "backColorDisabled2", PANEL, "foreColorDisabled", FGDIM));
            CHROMES = new States().S("Normal", St("backColor", CHROME, "foreColor", FG, "borderColor", CHROME, "borderColor2", CHROME, "borderColor3DBase", CHROME));
            SPLIT = new States().S("Normal", St("backColor", CHROME, "borderColor", CHROME, "borderColor2", CHROME, "borderColor3DBase", CHROME));
            TOOLITEM = new States()
                .S("Normal", St("foreColor", FG))
                .S("HotTracked", St("backColor", HOVER, "foreColor", WHITE, "borderColor", HOTBD, "borderColor2", HOTBD, "borderColor3DBase", HOTBD))
                .S("Pressed", St("backColor", PRESS, "foreColor", WHITE, "borderColor", ACCENT, "borderColor2", ACCENT, "borderColor3DBase", ACCENT))
                .S("Checked", St("backColor", SEL, "foreColor", WHITE, "borderColor", ACCENT, "borderColor2", ACCENT, "borderColor3DBase", ACCENT))
                .S("Active", St("backColor", HOVER, "foreColor", WHITE, "borderColor", HOTBD, "borderColor2", HOTBD));
            MENUHEAD = new States()
                .S("Normal", St("foreColor", FGB))
                .S("HotTracked", St("backColor", HOVER, "foreColor", WHITE, "borderColor", HOTBD, "borderColor2", HOTBD, "borderColor3DBase", HOTBD))
                .S("Pressed", St("backColor", MENU, "foreColor", WHITE, "borderColor", HOTBD, "borderColor2", HOTBD, "borderColor3DBase", HOTBD))
                .S("Active", St("backColor", HOVER, "foreColor", WHITE, "borderColor", HOTBD, "borderColor2", HOTBD))
                .S("Checked", St("backColor", SEL, "foreColor", WHITE, "borderColor", ACCENT, "borderColor2", ACCENT));
            EDIT = new States()
                .S("Normal", St("backColor", INPUT, "foreColor", FG, "borderColor", BORDER, "borderColor2", BORDER, "borderColor3DBase", BORDER, "backColorDisabled", PANEL, "backColorDisabled2", PANEL, "foreColorDisabled", FGDIM))
                .S("HotTracked", St("backColor", INPUT, "foreColor", FGB, "borderColor", ACCENT, "borderColor2", ACCENT))
                .S("Active", St("backColor", INPUT, "foreColor", FGB, "borderColor", ACCENT, "borderColor2", ACCENT));
            string b2 = C(61, 73, 87), dis = C(40, 48, 60);
            BTN = new States()
                .S("Normal", St("backColor", BUTTON, "foreColor", FGB, "borderColor", b2, "borderColor2", b2, "borderColor3DBase", b2, "backColorDisabled", dis, "backColorDisabled2", dis, "foreColorDisabled", FGDIM))
                .S("HotTracked", St("backColor", BUTTONH, "foreColor", WHITE, "borderColor", ACCENT, "borderColor2", ACCENT))
                .S("Pressed", St("backColor", ACCENT, "foreColor", WHITE, "borderColor", ACCENT, "borderColor2", ACCENT));
            GLYPHBTN = new States()
                .S("Normal", St("backColor", "Transparent", "foreColor", FG, "borderColor", "Transparent", "borderColor2", "Transparent", "borderColor3DBase", "Transparent"))
                .S("HotTracked", St("backColor", HOVER, "foreColor", WHITE, "borderColor", HOVER, "borderColor2", HOVER))
                .S("Pressed", St("backColor", ACCENT, "foreColor", WHITE, "borderColor", ACCENT, "borderColor2", ACCENT));
            HEADER = new States()
                .S("Normal", St("backColor", CHROME, "foreColor", FG, "borderColor", BORDER, "borderColor2", BORDER, "borderColor3DBase", BORDER))
                .S("HotTracked", St("backColor", HOVER, "foreColor", WHITE, "borderColor", HOTBD, "borderColor2", HOTBD))
                .S("Pressed", St("backColor", PRESS, "foreColor", WHITE));
            ITEM = new States()
                .S("Normal", St("foreColor", FG))
                .S("HotTracked", St("backColor", HOVER, "foreColor", WHITE))
                .S("Selected", St("backColor", MENUHOT, "foreColor", WHITE, "borderColor", MENUHOT, "borderColor2", MENUHOT))
                .S("Active", St("foreColor", WHITE))
                .S("HotTrackSelected", St("backColor", C(38, 92, 140), "foreColor", WHITE))
                .S("AlternateItem", St("backColor", PANEL, "foreColor", FG));
            MENUITEM = new States()
                .S("Normal", St("foreColor", FGB))
                .S("HotTracked", St("backColor", MENUHOT, "foreColor", WHITE, "borderColor", MENUHOT, "borderColor2", MENUHOT, "borderColor3DBase", MENUHOT))
                .S("Pressed", St("backColor", MENUHOT, "foreColor", WHITE, "borderColor", ACCENT, "borderColor2", ACCENT))
                .S("Active", St("backColor", MENUHOT, "foreColor", WHITE, "borderColor", MENUHOT, "borderColor2", MENUHOT))
                .S("Checked", St("backColor", SEL, "foreColor", WHITE, "borderColor", ACCENT, "borderColor2", ACCENT));
            SCROLLTRACK = new States().S("Normal", St("backColor", EDITOR, "borderColor", EDITOR, "borderColor2", EDITOR, "borderColor3DBase", EDITOR));
            THUMBS = new States()
                .S("Normal", St("backColor", THUMB, "borderColor", THUMB, "borderColor2", THUMB, "borderColor3DBase", THUMB))
                .S("HotTracked", St("backColor", THUMBH, "borderColor", THUMBH, "borderColor2", THUMBH))
                .S("Pressed", St("backColor", THUMBP, "borderColor", THUMBP, "borderColor2", THUMBP));
            ARROWS = new States()
                .S("Normal", St("backColor", EDITOR, "foreColor", FGDIM, "borderColor", EDITOR, "borderColor2", EDITOR, "borderColor3DBase", EDITOR))
                .S("HotTracked", St("backColor", HOVER, "foreColor", WHITE, "borderColor", HOVER, "borderColor2", HOVER))
                .S("Pressed", St("backColor", ACCENT, "foreColor", WHITE, "borderColor", ACCENT, "borderColor2", ACCENT));
            STATUS = new States().S("Normal", St("backColor", ACCENT, "foreColor", WHITE, "borderColor", ACCENT, "borderColor2", ACCENT, "borderColor3DBase", ACCENT));
            STATUSPANEL = new States().S("Normal", St("backColor", ACCENT, "foreColor", WHITE, "borderColor", ACCENT, "borderColor2", ACCENT, "borderColor3DBase", ACCENT));

            string tabs = "MdiTabItemHorizontalTop MdiTabItemHorizontalBottom MdiTabItemVerticalLeft MdiTabItemVerticalRight " +
                "TabItem TabItemHorizontalTop TabItemHorizontalBottom TabItemVerticalLeft TabItemVerticalRight " +
                "DockManagerTabItemHorizontalBottom DockManagerTabItemVerticalRight GroupPaneTabItemHorizontalBottom " +
                "UnpinnedTabItemHorizontalBottom UnpinnedTabItemHorizontalTop UnpinnedTabItemVerticalLeft UnpinnedTabItemVerticalRight " +
                "DockMgrTabItemHorzBottom DockMgrTabItemHorzTop DockMgrTabItemVertLeft DockMgrTabItemVertRight TabItemHorzTop TabItemHorzBottom " +
                "TabItemVertLeft TabItemVertRight TabItemHorizontal TabItemVertical UnpinnedTabItemHorzBottom UnpinnedTabItemHorzTop " +
                "UnpinnedTabItemVertLeft UnpinnedTabItemVertRight TabControlTabItemHorizontalTop TabControlTabItemHorizontalBottom " +
                "TabControlTabItemVerticalLeft TabControlTabItemVerticalRight";
            foreach (string t in tabs.Split(' ')) TabRoles.Add(t);
            Put(tabs, TAB);
            Put("MdiTabItemAreaHorizontalTop MdiTabItemAreaHorizontalBottom MdiTabItemAreaVerticalLeft MdiTabItemAreaVerticalRight " +
                "TabItemArea TabItemAreaHorizontalTop TabItemAreaHorizontalBottom TabItemAreaVerticalLeft TabItemAreaVerticalRight " +
                "UnpinnedTabArea UnpinnedTabAreaBottom UnpinnedTabAreaTop UnpinnedTabAreaLeft UnpinnedTabAreaRight " +
                "UnpinnedTabItemAreaHorizontalBottom UnpinnedTabItemAreaHorizontalTop UnpinnedTabItemAreaVerticalLeft UnpinnedTabItemAreaVerticalRight " +
                "UltraTabControl TabControlTabItemAreaHorizontalTop TabControlTabItemAreaHorizontalBottom TabControlTabItemAreaVerticalLeft " +
                "TabControlTabItemAreaVerticalRight TabItemAreaHorzTop TabItemAreaHorzBottom TabItemAreaVertLeft TabItemAreaVertRight " +
                "TabItemAreaHorizontal TabItemAreaVertical UnpinnedTabAreaHorz UnpinnedTabAreaVert UnpinnedTabAreaHorizontal UnpinnedTabAreaVertical " +
                "UnpinnedTabItemAreaHorzBottom UnpinnedTabItemAreaHorzTop UnpinnedTabItemAreaVertLeft UnpinnedTabItemAreaVertRight", TABAREA);
            Put("TabClientArea MdiTabClientAreaHorizontal MdiTabClientAreaVertical", CLIENT);
            Put("DockManagerPane DockManagerPaneContentArea DockControlPaneContentArea DockGroupPaneContentArea DockAreaPaneContentArea " +
                "TreeControlArea GridControlArea ListViewControlArea ExplorerBarControlArea UltraGroupBox UltraGroupBoxContentArea " +
                "ComboValueListDropDown DropDownControlArea ValueList MessageBox MessageBoxContentArea UltraTilePanel EmptyTileArea " +
                "PopupGallery TaskPane", PANELS);
            Put("ToolbarDockAreaTop ToolbarDockAreaBottom ToolbarDockAreaLeft ToolbarDockAreaRight Toolbar ToolbarBase ToolbarHorizontal " +
                "ToolbarVertical MainMenubarHorizontal QuickAccessToolbarTop NavigationToolbar TaskPaneToolbar UltraFormDockAreaTop " +
                "UltraFormDockAreaBottom UltraFormDockAreaLeft UltraFormDockAreaRight MessageBoxButtonArea MessageBoxFooter MessageBoxHeader " +
                "GridGroupByBox GridCaption GridAddNewBox GridSummaryFooter GridSummaryFooterCaption GridCardCaption GridRowSelector GridSpecialRowSeparator " +
                "ExplorerBarGroupHeader DockSlidingGroupHeader DockSlidingGroupHeaderHorizontal DockSlidingGroupHeaderVertical " +
                "DockFloatingWindowCaption DockFloatingWindowCaptionHorizontal DockFloatingWindowCaptionVertical UltraFormCaptionArea " +
                "RibbonTabPage Ribbon RibbonGroup RibbonGroupCaption", CHROMES);
            Put("DockManagerSplitterBar DockAreaSplitterBarHorizontal DockAreaSplitterBarVertical MdiSplitterBarHorizontal MdiSplitterBarVertical " +
                "UltraSplitter GridColScrollRegionSplitterBar GridRowScrollRegionSplitterBar ExplorerBarNavigationSplitter", SPLIT);
            Put("ToolbarItemPopupMenu ToolbarItemPopupMenuVertical", MENUHEAD);
            Put("ToolbarItem ToolbarItemButton ToolbarItemButtonVertical ToolbarItemStateButton " +
                "ToolbarItemPopupControlContainer ToolbarItemPopupColorPicker ToolbarItemQuickCustomize ToolbarItemLabel ToolbarItemControlContainer " +
                "ToolbarGrabHandleHorizontal ToolbarGrabHandleVertical QatItemQuickCustomize RibbonGroupItemButton RibbonGroupItemStateButton " +
                "RibbonGroupItemPopupMenu NavigationToolbarBackButton NavigationToolbarForwardButton NavigationToolbarRecentHistoryButton", TOOLITEM);
            Put("EditorControl UltraTextEditor UltraComboEditor UltraComboEditPortion UltraMaskedEdit UltraNumericEditor UltraCurrencyEditor " +
                "UltraDateTimeEditor UltraFontNameEditor UltraTimeSpanEditor UltraTimeZoneEditor UltraFormattedTextEditor UltraColorPicker " +
                "ToolbarEditArea ToolbarEditAreaComboBox ToolbarEditAreaTextBox ToolbarEditAreaMaskedEdit ToolbarEditAreaFontList " +
                "ToolbarItemComboBox ToolbarItemTextBox ToolbarItemEditor ToolbarItemMaskedEdit ToolbarItemFontList " +
                "RibbonGroupItemTextBox RibbonGroupItemMaskedEdit CalendarComboControlArea UltraCalculatorEditArea", EDIT);
            Put("UltraButton UltraButtonBase UltraButtonDefault UltraDropDownButton UltraDropDownButtonMainButton UltraDropDownButtonSplitButton " +
                "MessageBoxButton Button UltraCalculatorDropDownHostOkButton UltraCalculatorDropDownHostCancelButton", BTN);
            Put("ComboDropDownButton DropDownButton DropDownEditorButton EditorButton SpinButton SpinEditorButton StateEditorButton " +
                "AutoRepeatEditorButton CalendarComboDropDownButton DockPaneCaptionButton DockPaneCloseButton DockPanePinButton DockPaneUnpinButton " +
                "DockPaneMaximizeButton DockPaneMinimizeButton DockPaneRestoreButton DockPaneMenuButton MdiCloseButton MdiTabItemCloseButton " +
                "MdiSelectedTabItemCloseButton MdiTabListButton TabListButton TabItemCloseButton TabControlTabItemCloseButton " +
                "TabControlSelectedTabItemCloseButton TabControlHeaderAreaCloseButton TabScrollButton ToolbarCloseButton " +
                "UltraFormCaptionCloseButton UltraFormCaptionMinimizeButton UltraFormCaptionMaximizeButton UltraFormCaptionRestoreButton " +
                "UltraFormCaptionContextHelpButton FilterProviderActionButton GridFilterClearButton GridColumnChooserButton GridAddNewBoxButton", GLYPHBTN);
            Put("GridColumnHeader GridHeader GridBandHeader GridGroupHeader GridRowSelectorHeader TreeColumnHeader ListViewColumnHeader " +
                "GridGroupByRow ListViewGroupHeader", HEADER);
            States GRIDROW = ITEM.Copy();
            GRIDROW.Set("Normal", St("backColor", PANEL, "foreColor", FG, "borderColor", BORDER, "borderColor2", BORDER, "backColorDisabled", PANEL, "backColorDisabled2", PANEL, "foreColorDisabled", FGDIM));
            GRIDROW.Set("Active", St("backColor", MENUHOT, "foreColor", WHITE));
            Put("GridRow GridCell", GRIDROW);
            Put("TreeNode TreeCell ListViewItem ValueListItem OptionSetEditorItem ExplorerBarItem NavigationBarLocationListItem", ITEM);
            Put("MenuItem MenuItemButton MenuItemPopupMenu MenuItemStateButton MenuItemAddRemoveToolPopup MenuItemCustomize MenuItemListItem " +
                "MenuItemResetToolbar MenuItemCustomizeQAT MenuItemLabel MenuItemControlContainer MenuItemPopupControlContainer MenuItemComboBox " +
                "MenuItemTextBox MenuItemEditor MenuItemMdiWindowListItem", MENUITEM);
            Put("PopupMenu ToolbarPickerMenu NavigationToolbarRecentHistoryMenu ToolbarItemNavigationRecentHistoryMenu",
                new States().S("Normal", St("backColor", MENU, "foreColor", FGB, "borderColor", BORDER, "borderColor2", BORDER, "borderColor3DBase", BORDER)));
            Put("MenuSideStrip", new States().S("Normal", St("backColor", MENU, "borderColor", MENU, "borderColor2", MENU)));
            Put("MenuItemLabelHeader", new States().S("Normal", St("backColor", CHROME, "foreColor", FGB, "borderColor", CHROME, "borderColor2", CHROME)));
            Put("MenuCheckMark", new States().S("Normal", St("foreColor", FGB)).S("Checked", St("backColor", SEL, "foreColor", WHITE, "borderColor", ACCENT, "borderColor2", ACCENT)));
            Put("ScrollBarTrack ScrollBarTrackHorizontal ScrollBarTrackVertical ScrollBarTrackSection ScrollBarTrackSectionBottom " +
                "ScrollBarTrackSectionTop ScrollBarTrackSectionLeft ScrollBarTrackSectionRight ScrollBarTrackSectionHorizontal " +
                "ScrollBarTrackSectionVertical", SCROLLTRACK);
            Put("ScrollBarThumbHorizontal ScrollBarThumbVertical", THUMBS);
            Put("ScrollBarArrowDown ScrollBarArrowUp ScrollBarArrowLeft ScrollBarArrowRight GridColScrollRegionSplitBox GridRowScrollRegionSplitBox", ARROWS);
            Put("UltraStatusBar", STATUS);
            Put("StatusBarTextPanel StatusBarAutoStatusTextPanel StatusBarDatePanel StatusBarKeyStatePanel StatusBarCharacterPositionPanel " +
                "StatusBarCursorPositionPanel StatusBarMarqueePanel StatusBarPanel StatusBarPanelStateButton", STATUSPANEL);
            Put("DockPaneCaption DockPaneCaptionHorizontal DockPaneCaptionVertical", new States()
                .S("Normal", St("backColor", CHROME, "foreColor", FG, "borderColor", CHROME, "borderColor2", CHROME, "borderColor3DBase", CHROME))
                .S("Active", St("backColor", ACCENT, "foreColor", WHITE, "borderColor", ACCENT, "borderColor2", ACCENT, "borderColor3DBase", ACCENT)));
            Put("ToolTip", new States().S("Normal", St("backColor", CHROME, "foreColor", FG, "borderColor", BORDER, "borderColor2", BORDER, "borderColor3DBase", BORDER)));
            Put("Base", new States().S("Normal", St("foreColor", FG)).S("Selected", St("borderColor", ACCENT)));
            Put("UltraLabel UltraFormattedLinkLabel CheckEditor UltraCheckEditor UltraOptionSet GridSummaryValue GridAddNewBoxPrompt GridGroupByBoxPrompt " +
                "GridNoRowsMessage GridNoVisibleRowsMessage GridNoDataSourceMessage TreeNodeExpansionIndicator UltraGroupBoxHeaderHorizontalTop " +
                "MaskLiteralChar MaskPromptChar UltraPictureBox", new States().S("Normal", St("foreColor", FG)));
            Put("ProgressBarFill UltraProgressBar StatusBarProgressBar", new States().S("Normal", St("backColor", ACCENT, "foreColor", WHITE, "borderColor", BORDER)));
        }

        static List<XmlElement> Children(XmlElement e, string name)
        {
            List<XmlElement> r = new List<XmlElement>();
            foreach (XmlNode n in e.ChildNodes) { XmlElement x = n as XmlElement; if (x != null && x.Name == name) r.Add(x); }
            return r;
        }
        static XmlElement Child(XmlElement e, string name) { foreach (XmlNode n in e.ChildNodes) { XmlElement x = n as XmlElement; if (x != null && x.Name == name) return x; } return null; }
        static List<XmlElement> Desc(XmlElement e, string name) { List<XmlElement> r = new List<XmlElement>(); foreach (XmlNode n in e.GetElementsByTagName(name)) r.Add((XmlElement)n); return r; }
        static XmlElement Sub(XmlElement parent, string name) { XmlElement x = parent.OwnerDocument.CreateElement(name); parent.AppendChild(x); return x; }

        static void ApplyOverride(XmlElement style, States states)
        {
            XmlElement sts = Child(style, "states");
            if (sts == null) sts = Sub(style, "states");
            Dictionary<string, XmlElement> have = new Dictionary<string, XmlElement>();
            foreach (XmlElement s in Children(sts, "state")) have[s.GetAttribute("name")] = s;
            foreach (KeyValuePair<string, Attrs> kv in states)
            {
                XmlElement s;
                if (!have.TryGetValue(kv.Key, out s)) { s = Sub(sts, "state"); s.SetAttribute("name", kv.Key); }
                foreach (XmlElement r in Children(s, "resources")) s.RemoveChild(r);
                foreach (XmlElement r in Children(s, "imageBackground")) s.RemoveChild(r);
                foreach (string k in new[] { "backColor2", "imageBackgroundStyle" }) if (s.HasAttribute(k)) s.RemoveAttribute(k);
                foreach (KeyValuePair<string, string> a in kv.Value) s.SetAttribute(a.Key, a.Value);
            }
        }

        static readonly Dictionary<string, string> PROPMAP = new Dictionary<string, string> {
            { "BackColor", INPUT }, { "ForeColor", FG }, { "DisabledLinkColor", FGDIS }, { "LinkColor", LINK }, { "TitleBackColor", CHROME },
            { "TitleForeColor", FG }, { "TrailingForeColor", FGDIS }, { "HelpBackColor", CHROME }, { "LineColor", CHROME }, { "ViewBackColor", PANEL },
            { "ViewForeColor", FG }, { "SelectionOverlayBorderColor", ACCENT }, { "MenuItemSeparatorColor", BORDER }, { "MenuItemSeparatorColor2", BORDER },
            { "NavigationToolSeparatorColor", BORDER }, { "NavigationToolSeparatorColor2", BORDER }, { "RibbonToolSeparatorColor", BORDER },
            { "RibbonToolSeparatorColor2", BORDER }, { "ToolSeparatorColor", BORDER }, { "ToolSeparatorColor2", BORDER },
            { "NumericGaugeToolHotTrackNeedleColor", FG }, { "NumericGaugeToolTickmarkColor", FGDIM }, { "NumericGaugeToolTrackColor2", FGDIM },
            { "NumericGaugeToolValueNeedleColor", FG } };

        // papeis (roles) que existem nas DLLs Infragistics 11.1 e podem ser adicionados quando o Silver nao os define
        public static HashSet<string> RolesConhecidos;

        public static int Adicionados;

        // silverXml: conteudo do GenexusOffice2010Silver.isl; tema: Next = identico ao GxDarkTheme.isl da v1
        public static XmlDocument Gerar(string silverXml, Tema tema)
        {
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(silverXml);
            XmlElement root = doc.DocumentElement;
            root.SetAttribute("office2007ColorScheme", "Black");
            root.SetAttribute("office2010ColorScheme", "Black");
            XmlElement ann = Child(root, "annotation");
            if (ann != null) { XmlElement lm = Child(ann, "lastModified"); if (lm != null) lm.InnerText = "2026-10-27T17:30:00"; }
            HashSet<string> known = RolesConhecidos ?? new HashSet<string>();
            Adicionados = 0;
            foreach (XmlElement ss in Desc(root, "styleSet"))
            {
                foreach (XmlElement cs in Desc(ss, "componentStyle"))
                {
                    string name = cs.GetAttribute("name");
                    if (!name.StartsWith("Inbox", StringComparison.Ordinal))
                    {
                        cs.SetAttribute("useOsThemes", "False");
                        cs.SetAttribute("resolutionOrder", "ApplicationThenControl");
                        if (cs.GetAttribute("headerStyle") == "XPThemed") cs.SetAttribute("headerStyle", "Standard");
                    }
                    XmlElement props = Child(cs, "properties");
                    if (props == null) continue;
                    foreach (XmlElement p in Children(props, "property"))
                    {
                        string n = p.GetAttribute("name");
                        if (name == "Inbox Button" && n == "BackColor") { p.InnerText = BUTTON; continue; }
                        if (name == "Inbox PropertyGrid" && n == "BackColor") { p.InnerText = CHROME; continue; }
                        if (name == "Inbox MonthCalendar" && n == "BackColor") { p.InnerText = PANEL; continue; }
                        string v;
                        if (PROPMAP.TryGetValue(n, out v)) p.InnerText = v;
                        else if (n == "RowSelectorStyle") p.InnerText = "Standard";
                        else if (n == "GroupPaneTabStyle") p.InnerText = "Flat";
                    }
                }
                XmlElement styles = Child(ss, "styles");
                if (styles == null) continue;
                Dictionary<string, XmlElement> present = new Dictionary<string, XmlElement>();
                foreach (XmlElement s in Children(styles, "style"))
                {
                    string role = s.GetAttribute("role"); present[role] = s;
                    foreach (XmlElement state in Desc(s, "state")) MapAttrs(role, state.HasAttribute("name") ? state.GetAttribute("name") : null, state);
                }
                bool isBase = !ss.HasAttribute("basedOn");
                foreach (string role in ORoles)
                {
                    XmlElement el;
                    if (present.TryGetValue(role, out el)) ApplyOverride(el, O[role]);
                    else if (isBase && known.Contains(role))
                    {
                        el = Sub(styles, "style"); el.SetAttribute("role", role);
                        ApplyOverride(el, O[role]); Adicionados++;
                    }
                }
            }
            HashSet<string> solid = new HashSet<string> { "GridColumnHeader", "GridBandHeader", "GridGroupHeader", "GridRowSelectorHeader", "TreeColumnHeader", "ListViewColumnHeader" };
            XmlElement sets = Child(root, "styleSets");
            foreach (XmlElement ss in Children(sets, "styleSet"))
            {
                XmlElement styles = Child(ss, "styles");
                if (styles == null) continue;
                foreach (XmlElement s in Children(styles, "style")) if (solid.Contains(s.GetAttribute("role"))) s.SetAttribute("borderStyle", "Solid");
            }
            XmlElement first = Child(sets, "styleSet");
            XmlElement cstyles = Child(first, "componentStyles");
            HashSet<string> haveC = new HashSet<string>();
            foreach (XmlElement c in Children(cstyles, "componentStyle")) haveC.Add(c.GetAttribute("name"));
            foreach (string n in new[] { "UltraTextEditor", "UltraStatusBar", "UltraLabel", "UltraMaskedEdit", "UltraNumericEditor", "UltraExpandableGroupBox",
                "UltraPictureBox", "UltraProgressBar", "UltraCurrencyEditor", "UltraTimeSpanEditor" })
            {
                if (haveC.Contains(n)) continue;
                XmlElement c = Sub(cstyles, "componentStyle");
                c.SetAttribute("name", n); c.SetAttribute("useOsThemes", "False"); c.SetAttribute("resolutionOrder", "ApplicationThenControl");
            }
            foreach (XmlElement c in Children(cstyles, "componentStyle"))
            {
                string cn = c.GetAttribute("name");
                if (cn != "UltraTabControl" && cn != "UltraTabStripControl") continue;
                XmlElement props = Child(c, "properties");
                if (props == null) props = Sub(c, "properties");
                bool has = false;
                foreach (XmlElement q in Children(props, "property")) if (q.GetAttribute("name") == "Style") has = true;
                if (!has) { XmlElement q = Sub(props, "property"); q.SetAttribute("name", "Style"); q.SetAttribute("colorCategory", "{Default}"); q.InnerText = "Flat"; }
            }
            XmlElement resources = Child(root, "resources");
            if (resources != null)
                foreach (XmlElement res in Children(resources, "resource"))
                {
                    string name = res.GetAttribute("name");
                    string[] parts = name.Split('_');
                    string role = name.IndexOf('_') >= 0 ? parts[1] : name;
                    string state = parts[parts.Length - 1];
                    MapAttrs(role, state, res);
                    if (role == "UltraButton" || role == "RibbonGroupItemStateButton")
                    {
                        States src = role == "UltraButton" ? BTN : TOOLITEM;
                        string key = state == "HotTrackSelected" || state == "Selected" ? "Checked" : state == "Active" ? "HotTracked" : state;
                        Attrs attrs = src.Get(key) ?? src.Get("Normal");
                        if (res.HasAttribute("backColor2")) res.RemoveAttribute("backColor2");
                        foreach (KeyValuePair<string, string> a in attrs) res.SetAttribute(a.Key, a.Value);
                        if (!attrs.Has("backColor")) res.SetAttribute("backColor", "Transparent");
                    }
                    if (role == "GridColumnHeader") foreach (KeyValuePair<string, string> a in (HEADER.Get(state) ?? HEADER.Get("Normal"))) res.SetAttribute(a.Key, a.Value);
                    if (role == "ExplorerBarGroupHeader") foreach (KeyValuePair<string, string> a in CHROMES.Get("Normal")) res.SetAttribute(a.Key, a.Value);
                    if (role == "GridNoVisibleRowsMessage") { res.SetAttribute("backColor", PANEL); res.SetAttribute("foreColor", FGDIM); res.SetAttribute("borderColor", BORDER); }
                }
            AjustesFinos(root);
            if (tema != null && !tema.IsNext) Recolorir(root, tema);
            return doc;
        }

        // ajustes feitos a mao no GxDarkTheme.isl da v1 (contraste): texto desabilitado, abas inativas, setas da scrollbar
        static void AjustesFinos(XmlElement root)
        {
            string fgdis = C(150, 157, 168), soft = C(190, 198, 210), arrow = C(170, 178, 190);
            foreach (XmlNode n in root.SelectNodes("//*[@foreColorDisabled]"))
            {
                XmlElement e = (XmlElement)n;
                if (e.GetAttribute("foreColorDisabled") == FGDIM) e.SetAttribute("foreColorDisabled", fgdis);
            }
            foreach (XmlNode n in root.SelectNodes("//style"))
            {
                XmlElement s = (XmlElement)n;
                string role = s.GetAttribute("role");
                bool tab = TabRoles.Contains(role);
                bool arr = role.StartsWith("ScrollBarArrow", StringComparison.Ordinal) || role == "GridColScrollRegionSplitBox" || role == "GridRowScrollRegionSplitBox";
                if (!tab && !arr) continue;
                foreach (XmlNode m in s.SelectNodes("states/state[@name='Normal']"))
                {
                    XmlElement st = (XmlElement)m;
                    if (st.GetAttribute("foreColor") == FGDIM) st.SetAttribute("foreColor", tab ? soft : arrow);
                }
            }
        }

        static readonly string[] AtributosCor = { "backColor", "backColor2", "foreColor", "borderColor", "borderColor2", "borderColor3DBase", "backColorDisabled", "backColorDisabled2", "foreColorDisabled", "borderColorDisabled" };

        static string MapV(string v, Tema t)
        {
            int[] c = Parse(v);
            if (c == null || c[0] == -1) return v;
            Color o = Color.FromArgb(c[0], c[1], c[2], c[3]);
            Color n = t.M(o);
            if (n.ToArgb() == o.ToArgb()) return v;
            return (c[0] < 255 ? c[0] + ", " : "") + n.R + ", " + n.G + ", " + n.B;
        }

        // aplica M() do tema em todo atributo/propriedade de cor
        static void Recolorir(XmlElement root, Tema t)
        {
            foreach (XmlNode n in root.SelectNodes("//*"))
            {
                XmlElement e = (XmlElement)n;
                List<XmlAttribute> at = new List<XmlAttribute>();
                foreach (XmlAttribute a in e.Attributes) at.Add(a);
                foreach (XmlAttribute a in at) if (a.Name.IndexOf("Color", StringComparison.OrdinalIgnoreCase) >= 0) a.Value = MapV(a.Value, t);
                if (e.Name == "property" && e.GetAttribute("name").IndexOf("Color", StringComparison.OrdinalIgnoreCase) >= 0 && e.ChildNodes.Count == 1 && e.FirstChild is XmlText)
                    e.InnerText = MapV(e.InnerText, t);
            }
        }

        public static string ParaTexto(XmlDocument doc)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            XmlWriterSettings ws = new XmlWriterSettings { Indent = true, IndentChars = "  ", Encoding = new System.Text.UTF8Encoding(false) };
            using (System.IO.StringWriter sw = new System.IO.StringWriter(sb)) using (XmlWriter w = XmlWriter.Create(sw, ws)) doc.Save(w);
            return sb.ToString();
        }
    }
}
