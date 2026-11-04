// Renderiza a tela de configuracoes (src/Interface/Configuracao.cs) fora do GeneXus, em 100/125/150/200%, e procura texto cortado.
// O motor real precisa do GeneXus; aqui um motor falso so com o que a tela usa, que pinta como o motor (paleta do tema ativo).
//   mcs -r:System.Windows.Forms -r:System.Drawing -out:RenderConfig.exe RenderConfig.cs ../../src/Interface/Configuracao.cs ../../src/Nucleo/Tema.cs ../../src/Nucleo/Produto.cs
//   xvfb-run mono RenderConfig.exe <saida> [tema]
using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace GxDarkTheme
{
    public static class DarkEngine
    {
        public static Color Panel, Input, Button, Fg, FgBright, FgDim, Border;
        public static Tema TemaAtual = Temas.Next;
        public static bool On = true;
        public static event EventHandler TemaMudou;
        static string icone = "ambar";
        public static void Usar(string id)
        {
            TemaAtual = Temas.Achar(id);
            Panel = TemaAtual.M(Color.FromArgb(24, 31, 42)); Input = TemaAtual.M(Color.FromArgb(31, 39, 52)); Button = TemaAtual.M(Color.FromArgb(44, 60, 80));
            Fg = TemaAtual.M(Color.FromArgb(220, 226, 235)); FgBright = TemaAtual.M(Color.FromArgb(240, 244, 250)); FgDim = TemaAtual.M(Color.FromArgb(138, 144, 155));
            Border = TemaAtual.M(Color.FromArgb(61, 73, 87));
            if (TemaMudou != null) TemaMudou(null, EventArgs.Empty);
        }
        public static void SetTheme(string id, bool p) { Usar(id); }
        public static void SetMode(bool d, bool p) { On = d; }
        public static void SettingsWrite(string k, string v) { if (k == "icone") icone = v; }
        public static string SettingsValue(string k) { return null; }
        public static string IconOption() { return icone; }
        public static string SettingsPath() { return "/tmp/GxDarkTheme.settings"; }
        // o que o motor faz com os controles de um formulario (resumo de DarkEngine.Theme)
        public static void Pintar(Control c)
        {
            foreach (Control k in c.Controls)
            {
                if (k is ComboBox) { k.BackColor = Input; k.ForeColor = Fg; ((ComboBox)k).FlatStyle = FlatStyle.Flat; }
                else if (k is Button) { Button b = (Button)k; b.BackColor = Button; b.ForeColor = FgBright; b.FlatAppearance.BorderColor = Border; }
                else if (k is Label && k.Tag == null) k.ForeColor = Fg;
                Pintar(k);
            }
        }
    }
    public static class ToggleButton { public static void RefreshAll() { } }

    static class RenderConfig
    {
        static int problemas;
        static void Pump() { for (int i = 0; i < 6; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(15); } }

        static void Verificar(Control c, string nome)
        {
            foreach (Control k in c.Controls)
            {
                if (!k.Visible) continue;
                Rectangle v = k.Parent.ClientRectangle;
                bool rola = k.Parent is ScrollableControl && ((ScrollableControl)k.Parent).AutoScroll;
                if (!rola && (k.Right > v.Right + 1 || k.Bottom > v.Bottom + 1 || k.Left < -1 || k.Top < -1)) Falha(nome, k, "fora do pai " + k.Bounds + " em " + v);
                if (rola && k.Right > v.Right + 1) Falha(nome, k, "passa da largura " + k.Bounds + " em " + v);
                if ((k is Label || k is ButtonBase) && !string.IsNullOrEmpty(k.Text) && k.Width > 0)
                {
                    Size p = k.GetPreferredSize(new Size(k.Width, 0));
                    if (k is Label ? p.Height > k.Height + 1 : (p.Width > k.Width + 1 || p.Height > k.Height + 1)) Falha(nome, k, "texto nao cabe: " + k.Size + " preferido " + p);
                    string w = ""; foreach (string x in k.Text.Split(' ', '\r', '\n')) if (x.Length > w.Length) w = x;
                    if (k is Label && TextRenderer.MeasureText(w, k.Font).Width > k.Width + 2) Falha(nome, k, "palavra maior que a largura (" + w + ")");
                }
                if (k is ComboBox && k.Height < k.Font.Height + 4) Falha(nome, k, "combo baixo demais: altura " + k.Height + ", fonte " + k.Font.Height);
                if (k is ComboBox && k.Width < TextRenderer.MeasureText(k.Text, k.Font).Width + k.Height) Falha(nome, k, "combo estreito demais " + k.Size);
                Verificar(k, nome);
            }
        }
        static void Falha(string n, Control k, string m) { problemas++; Console.WriteLine("  CORTADO [" + n + "] " + k.GetType().Name + " \"" + k.Text + "\": " + m); }

        static void Snap(Form f, string arq, string nome)
        {
            f.Refresh(); Pump(); f.PerformLayout(); Pump();
            Point o = f.PointToScreen(Point.Empty);
            using (Bitmap b = new Bitmap(f.ClientSize.Width, f.ClientSize.Height))
            {
                using (Graphics g = Graphics.FromImage(b)) g.CopyFromScreen(o, Point.Empty, b.Size);
                b.Save(arq);
            }
            Verificar(f, nome);
            Console.WriteLine("  " + nome + ": " + f.ClientSize.Width + "x" + f.ClientSize.Height + " (minimo " + f.MinimumSize.Width + "x" + f.MinimumSize.Height + ")");
        }

        [STAThread]
        static int Main(string[] a)
        {
            string saida = a.Length > 0 ? a[0] : "."; Directory.CreateDirectory(saida);
            DarkEngine.Usar(a.Length > 1 ? a[1] : "ambar");
            Application.EnableVisualStyles();
            foreach (float e in new float[] { 1f, 1.25f, 1.5f, 2f })
            {
                string s = ((int)(e * 100)).ToString();
                Configuracao.EscalaFonte = e;
                Form f = (Form)Activator.CreateInstance(typeof(Configuracao), true);
                f.StartPosition = FormStartPosition.Manual; f.Location = Point.Empty;
                DarkEngine.Pintar(f);
                f.Show(); Pump();
                Snap(f, Path.Combine(saida, "config-" + s + ".png"), "config-" + s);
                Size normal = f.Size;
                f.Size = f.MinimumSize; Pump();
                Snap(f, Path.Combine(saida, "config-minimo-" + s + ".png"), "config-minimo-" + s);
                f.Size = new Size(f.MinimumSize.Width * 2, normal.Height); Pump();
                Snap(f, Path.Combine(saida, "config-largo-" + s + ".png"), "config-largo-" + s);
                f.Dispose();
            }
            Console.WriteLine(problemas == 0 ? "NENHUM TEXTO CORTADO" : problemas + " PROBLEMA(S)");
            return problemas == 0 ? 0 : 1;
        }
    }
}
