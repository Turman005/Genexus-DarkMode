// Replicas do comparador do GeneXus 18 com as MESMAS formas que o motor procura por reflexao
// (tipos, campos e assinaturas tirados do IL de Artech.FrameworkDE.Comparer / Artech.Architecture.Common).
// O desenho "do GeneXus" aqui imita o original: cores claras das opcoes, lado vazio tracejado cinza.
using System; using System.Collections.Generic; using System.Drawing; using System.Drawing.Drawing2D; using System.Windows.Forms;
using ActiproSoftware.SyntaxEditor;

namespace Artech.Architecture.Common.Comparer
{
    public enum NodeType { Equal, Added, Deleted, Modified }
    public class DiffViewLine
    {
        public int Number { get; private set; } public string Text { get; private set; } public NodeType LineType { get; set; }
        public DiffViewLine(string t, int n, NodeType lt) { Text = t; Number = n; LineType = lt; }
    }
    public sealed class DiffViewLines
    {
        readonly List<DiffViewLine> l = new List<DiffViewLine>();
        public DiffViewLine this[int i] { get { return l[i]; } }
        public int Count { get { if (Explode) throw new InvalidOperationException("falha simulada"); return l.Count; } }
        public void AddLine(string t, int n, NodeType lt) { l.Add(new DiffViewLine(t, n, lt)); }
        public bool Explode;   // teste de falha
    }
}

namespace Fake.DX
{
    public class AppearanceObject { public Font Font = SystemFonts.DefaultFont; public Color ForeColor = Color.Black; public Font GetFont() { return Font; } }
    public class TreeListNode { public int Id { get; set; } public object Tag { get; set; } public int Level; public string Text; }
    public class NodeArgs : EventArgs
    {
        public TreeListNode Node { get; set; } public Rectangle Bounds { get; set; } public Graphics Graphics { get; set; } public bool Handled { get; set; }
        public AppearanceObject Appearance { get; set; }
    }
    public class CustomDrawNodeCellEventArgs : NodeArgs { public string CellText { get; set; } }
    public class CustomDrawNodeIndentEventArgs : NodeArgs { }
    public class CustomDrawNodeImagesEventArgs : NodeArgs { }
    public delegate void CustomDrawNodeCellEventHandler(object s, CustomDrawNodeCellEventArgs e);
    public delegate void CustomDrawNodeIndentEventHandler(object s, CustomDrawNodeIndentEventArgs e);
    public delegate void CustomDrawNodeImagesEventHandler(object s, CustomDrawNodeImagesEventArgs e);

    // imita DevExpress.XtraTreeList.TreeList (o motor so olha o NOME do tipo base e os eventos)
    public class TreeList : Control
    {
        public event CustomDrawNodeCellEventHandler CustomDrawNodeCell;
        public event CustomDrawNodeIndentEventHandler CustomDrawNodeIndent;
        public event CustomDrawNodeImagesEventHandler CustomDrawNodeImages;
        public readonly List<TreeListNode> Rows = new List<TreeListNode>();
        public TreeListNode FocusedNode { get; set; }
        public AppearanceObject Row = new AppearanceObject();
        public int RowH = 20;
        public int HandlerCount { get { return (CustomDrawNodeCell == null ? 0 : CustomDrawNodeCell.GetInvocationList().Length) + (CustomDrawNodeIndent == null ? 0 : CustomDrawNodeIndent.GetInvocationList().Length) + (CustomDrawNodeImages == null ? 0 : CustomDrawNodeImages.GetInvocationList().Length); } }
        public Delegate[] Handlers(string ev)
        {
            Delegate d = ev == "cell" ? (Delegate)CustomDrawNodeCell : ev == "indent" ? (Delegate)CustomDrawNodeIndent : CustomDrawNodeImages;
            return d == null ? new Delegate[0] : d.GetInvocationList();
        }
        public void Render(Graphics g, Rectangle area)
        {
            for (int i = 0; i < Rows.Count; i++)
            {
                TreeListNode n = Rows[i];
                int y = area.Y + i * RowH, ind = 4 + n.Level * 16;
                var ia = new CustomDrawNodeIndentEventArgs { Node = n, Graphics = g, Bounds = new Rectangle(area.X, y, ind, RowH), Appearance = Row };
                if (CustomDrawNodeIndent != null) CustomDrawNodeIndent(this, ia);
                if (!ia.Handled) g.FillRectangle(Brushes.White, ia.Bounds);
                var im = new CustomDrawNodeImagesEventArgs { Node = n, Graphics = g, Bounds = new Rectangle(area.X + ind, y, 18, RowH), Appearance = Row };
                if (CustomDrawNodeImages != null) CustomDrawNodeImages(this, im);
                if (!im.Handled) g.FillRectangle(Brushes.White, im.Bounds);
                var ce = new CustomDrawNodeCellEventArgs { Node = n, Graphics = g, Bounds = new Rectangle(area.X + ind + 18, y, area.Width - ind - 18, RowH), Appearance = Row, CellText = n.Text };
                if (CustomDrawNodeCell != null) CustomDrawNodeCell(this, ce);
                if (!ce.Handled) { g.FillRectangle(Brushes.White, ce.Bounds); TextRenderer.DrawText(g, n.Text, Row.Font, ce.Bounds, Row.ForeColor, TextFormatFlags.VerticalCenter); }
            }
        }
    }
    public class CustomTreeList : TreeList { }
}

namespace Artech.Architecture.UI.Framework.Services.Comparer
{
    public class StructNode { public Icon Icon { get; set; } public Image Image { get; set; } public string Name; }
}

namespace Artech.FrameworkDE.Comparer
{
    using Artech.Architecture.Common.Comparer; using Fake.DX;

    // opcoes do GeneXus (cores claras + pincel em cache do lado vazio)
    public class ComparerColorOptions
    {
        Brush m_EmptyBackBrush = new HatchBrush(HatchStyle.BackwardDiagonal, Color.FromArgb(0xB3, 0xB3, 0xB3), SystemColors.Window);
        Brush m_Other = new SolidBrush(Color.White);
        public Brush OrphanEmptySideBackBrush { get { return m_EmptyBackBrush; } }
        public Color GetBackColorForEditType(NodeType t) { return t == NodeType.Added ? Color.FromArgb(0xDD, 0xF7, 0xBC) : t == NodeType.Deleted ? Color.FromArgb(0xFF, 0xC8, 0xC8) : t == NodeType.Modified ? Color.FromArgb(0xC8, 0xDC, 0xFF) : SystemColors.Window; }
    }

    public class StructDiffPane : UserControl
    {
        CustomTreeList Yvvw1ItIGd = new CustomTreeList();
        DiffViewLines mokwE5b1GU = new DiffViewLines();
        public static ComparerColorOptions Opts = new ComparerColorOptions();
        public CustomTreeList Tree { get { return Yvvw1ItIGd; } }
        public DiffViewLines Lines { get { return mokwE5b1GU; } }
        public StructDiffPane()
        {
            Yvvw1ItIGd.CustomDrawNodeCell += h8MwI2afgU;
            Yvvw1ItIGd.CustomDrawNodeIndent += LskwgEmw1w;
            Yvvw1ItIGd.CustomDrawNodeImages += i7Hwa3L8ub;
            Controls.Add(Yvvw1ItIGd);
        }
        public void Add(string text, int level, NodeType t, int number, Image icon)
        {
            mokwE5b1GU.AddLine(text, number, t);
            Yvvw1ItIGd.Rows.Add(new TreeListNode { Id = Yvvw1ItIGd.Rows.Count, Level = level, Text = number == -1 ? "" : text, Tag = number == -1 ? null : new Artech.Architecture.UI.Framework.Services.Comparer.StructNode { Image = icon, Name = text } });
        }
        Brush O69woOruA5(TreeListNode n)
        {
            DiffViewLine l = mokwE5b1GU[n.Id];
            if (l.Number == -1) return new HatchBrush(HatchStyle.BackwardDiagonal, Color.FromArgb(0xB3, 0xB3, 0xB3), SystemColors.Window);
            return new SolidBrush(Opts.GetBackColorForEditType(l.LineType));
        }
        void h8MwI2afgU(object s, CustomDrawNodeCellEventArgs e)
        {
            using (Brush b = O69woOruA5(e.Node)) e.Graphics.FillRectangle(b, e.Bounds);
            if (ReferenceEquals(Yvvw1ItIGd.FocusedNode, e.Node)) using (Pen p = new Pen(Color.FromArgb(0, 120, 215))) e.Graphics.DrawRectangle(p, e.Bounds.X, e.Bounds.Y, e.Bounds.Width - 1, e.Bounds.Height - 1);
            TextRenderer.DrawText(e.Graphics, e.CellText, e.Appearance.Font, e.Bounds, e.Appearance.ForeColor, TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            e.Handled = true;
        }
        void LskwgEmw1w(object s, CustomDrawNodeIndentEventArgs e)
        {
            using (Brush b = O69woOruA5(e.Node)) e.Graphics.FillRectangle(b, e.Bounds);
            using (Pen p = new Pen(Color.Gray) { DashStyle = DashStyle.Dot })
                for (int k = 0; k < e.Node.Level; k++) e.Graphics.DrawLine(p, e.Bounds.X + 12 + k * 16, e.Bounds.Y, e.Bounds.X + 12 + k * 16, e.Bounds.Bottom);
            e.Handled = true;
        }
        void i7Hwa3L8ub(object s, CustomDrawNodeImagesEventArgs e)
        {
            using (Brush b = O69woOruA5(e.Node)) e.Graphics.FillRectangle(b, e.Bounds);
            var sn = e.Node.Tag as Artech.Architecture.UI.Framework.Services.Comparer.StructNode;
            if (sn != null && sn.Image != null) e.Graphics.DrawImage(sn.Image, new Rectangle(e.Bounds.X, e.Bounds.Y + 2, 16, 16));
            e.Handled = true;
        }
    }

    // barra de visao geral: fundo branco, marcas nas cores claras, caixa azul-clara da janela visivel
    public class DiffOverview : UserControl
    {
        Func<DiffViewLine, Color> PRp1ImIhIc; DiffViewLines lines;
        public int ViewTop = 40, ViewH = 120;
        public DiffOverview(DiffViewLines l) { lines = l; PRp1ImIhIc = x => StructDiffPane.Opts.GetBackColorForEditType(x.LineType); }
        protected override void OnPaintBackground(PaintEventArgs e) { e.Graphics.FillRectangle(Brushes.White, ClientRectangle); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; int h = ClientSize.Height, w = ClientSize.Width;
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].LineType == NodeType.Equal) continue;
                int y = i * h / lines.Count, y2 = (i + 1) * h / lines.Count;
                using (var b = new SolidBrush(PRp1ImIhIc(lines[i]))) g.FillRectangle(b, 3, y, w - 6, Math.Max(2, y2 - y));
            }
            using (var b = new SolidBrush(Color.FromArgb(0xCC, 0xE4, 0xF7))) g.FillRectangle(b, 0, ViewTop, w - 1, ViewH);
            using (var p = new Pen(Color.FromArgb(0x33, 0x99, 0xFF))) g.DrawRectangle(p, 0, ViewTop, w - 1, ViewH);
        }
    }

    // painel de codigo: cor de linha ignorada e pincel do orfao incluido
    public class SourceDiffPane : UserControl
    {
        Color BHHcm6toym = Color.FromArgb(0xF0, 0xF0, 0xF0);
        Brush kL2cE1a2Ha = new HatchBrush(HatchStyle.BackwardDiagonal, Color.FromArgb(0xB3, 0xB3, 0xB3), Color.FromArgb(0xDD, 0xF7, 0xBC));
        public Color Ignore { get { return BHHcm6toym; } } public Brush Orphan { get { return kL2cE1a2Ha; } }
    }
}

namespace Artech.FrameworkDE.Comparer.Helpers
{
    public class OrphanLineIndicator : SpanIndicator
    {
        Brush cplwrQA4sm;
        public OrphanLineIndicator(Brush b) : base("orphan") { cplwrQA4sm = b; }
        public Brush B { get { return cplwrQA4sm; } }
        public override void DrawGlyph(PaintEventArgs e, Rectangle r) { }
        public override int DisplayPriority { get { return 0; } }
        public override bool HasFontChange { get { return false; } }
        public override bool HasGlyph { get { return false; } }
    }
}
