// Previa da aba "Comparer" (composicao offscreen com as replicas; o desenho da arvore, da faixa e da
// visao geral e o do motor). Cabecalho/barra/Swap usam as cores que o motor ja aplica nesses controles.
using System; using System.Drawing; using System.Drawing.Drawing2D; using System.Drawing.Imaging; using System.Windows.Forms;
using ActiproSoftware.SyntaxEditor; using Artech.FrameworkDE.Comparer; using GxDarkTheme;

static class Preview
{
    public static Bitmap Draw(StructDiffPane L, StructDiffPane R, Document doc, bool dark, string label, bool before = false)
    {
        int W = 1000, H = 560;
        Color chrome = dark ? DarkEngine.Chrome : SystemColors.Control, panel = dark ? DarkEngine.Panel : Color.White, fg = dark ? DarkEngine.Fg : Color.Black,
              border = dark ? DarkEngine.Border : Color.FromArgb(0xC8, 0xC8, 0xC8), btn = dark ? DarkEngine.Button : Color.FromArgb(0xE1, 0xE1, 0xE1), dim = dark ? DarkEngine.FgSoft : Color.FromArgb(0x55, 0x55, 0x55);
        var bmp = new Bitmap(W, H, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        using (var f = new Font("DejaVu Sans", 8.5f)) using (var fb = new Font("DejaVu Sans", 8.5f, FontStyle.Bold))
        {
            g.Clear(panel);
            // abas
            using (var b = new SolidBrush(chrome)) g.FillRectangle(b, 0, 0, W, 26);
            using (var b = new SolidBrush(panel)) g.FillRectangle(b, 6, 4, 150, 22);
            using (var b = new SolidBrush(dark ? DarkEngine.Accent : Color.FromArgb(0, 120, 215))) g.FillRectangle(b, 6, 4, 150, 2);
            TextRenderer.DrawText(g, "Comparer", fb, new Rectangle(14, 6, 140, 20), fg, TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, "Web Layout - CadastroUsuarioParceiro   [" + label + "]", f, new Rectangle(170, 4, 600, 22), dim, TextFormatFlags.VerticalCenter);
            // barra: Swap + Ln
            using (var b = new SolidBrush(chrome)) g.FillRectangle(b, 0, 26, W, 28);
            using (var b = new SolidBrush(btn)) g.FillRectangle(b, 8, 30, 64, 20);
            using (var p = new Pen(border)) g.DrawRectangle(p, 8, 30, 64, 20);
            TextRenderer.DrawText(g, "⇄ Swap", f, new Rectangle(8, 30, 64, 20), fg, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, "Ln 57", f, new Rectangle(W - 80, 30, 70, 20), dim, TextFormatFlags.VerticalCenter);
            // cabecalhos
            int top = 56, gut = 20, pw = (W - gut - 6) / 2;
            using (var b = new SolidBrush(chrome)) g.FillRectangle(b, 0, top, W, 22);
            TextRenderer.DrawText(g, "Version 12 (2026-10-20)", fb, new Rectangle(gut + 6, top, pw, 22), fg, TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, "Current version", fb, new Rectangle(gut + pw + 12, top, pw, 22), fg, TextFormatFlags.VerticalCenter);
            top += 23;
            int th = 330;
            // visao geral
            var ov = new DiffOverview(L.Lines) { Size = new Size(gut - 4, th) };
            using (Bitmap ob = dark && !before ? ComparerTheme.OverviewFilter.Paint(ov) : RawPaint(ov)) g.DrawImageUnscaled(ob, 2, top);
            // arvores
            foreach (var pn in new[] { L, R })
            {
                pn.Tree.Row.ForeColor = dark ? DarkEngine.Fg : Color.Black;
                bool on = DarkEngine.On; if (before) DarkEngine.On = false;
                using (var area = new Bitmap(pw, th))
                using (var ga = Graphics.FromImage(area))
                {
                    ga.Clear(dark && !before ? DarkEngine.Input : Color.White);
                    pn.Tree.Render(ga, new Rectangle(0, 0, pw, th));
                    g.DrawImageUnscaled(area, pn == L ? gut + 2 : gut + pw + 6, top);
                }
                DarkEngine.On = on;
            }
            using (var p = new Pen(border)) { g.DrawRectangle(p, gut + 2, top, pw - 1, th - 1); g.DrawRectangle(p, gut + pw + 6, top, pw - 1, th - 1); }
            // faixa de linha de baixo (DetailDiffPane: duas linhas do documento)
            int y = top + th + 8;
            using (var b = new SolidBrush(chrome)) g.FillRectangle(b, 0, y, W, 20);
            TextRenderer.DrawText(g, "Line differences", fb, new Rectangle(8, y, 300, 20), fg, TextFormatFlags.VerticalCenter);
            y += 22;
            Color code = dark ? DarkEngine.Editor : Color.White;
            using (var b = new SolidBrush(code)) g.FillRectangle(b, 0, y, W, H - y);
            using (var mono = new Font("DejaVu Sans Mono", 9f))
                for (int i = 0; i < doc.Lines.Count; i++)
                {
                    Color bc = doc.Lines[i].BackColor;
                    var r = new Rectangle(0, y + i * 18, W, 18);
                    if (!bc.IsEmpty) using (var b = new SolidBrush(bc)) g.FillRectangle(b, r);
                    TextRenderer.DrawText(g, (i + 1).ToString().PadLeft(3) + "  " + doc.Lines[i].Text, mono, r, dark ? DarkEngine.Fg : Color.Black, TextFormatFlags.VerticalCenter);
                }
        }
        return bmp;
    }

    static Bitmap RawPaint(Control c)
    {
        var b = new Bitmap(c.Width, c.Height);
        using (var g = Graphics.FromImage(b)) using (var pe = new PaintEventArgs(g, c.ClientRectangle))
        {
            var t = typeof(Control);
            t.GetMethod("OnPaintBackground", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(c, new object[] { pe });
            t.GetMethod("OnPaint", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(c, new object[] { pe });
        }
        return b;
    }
}
