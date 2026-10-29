using System; using System.Collections.Generic; using System.Drawing; using System.IO; using System.Reflection; using System.Windows.Forms;
using Infragistics.Win.UltraWinTree; using Crownwood.Magic.Common;
static class T
{
    const string GX = @"C:\Program Files (x86)\GeneXus\GeneXus18-U10";
    [STAThread] static int Main(string[] a)
    {
        AppDomain.CurrentDomain.AssemblyResolve += (s, e) => { string p = Path.Combine(GX, new AssemblyName(e.Name).Name + ".dll"); return File.Exists(p) ? Assembly.LoadFrom(p) : null; };
        return Rodar(a[0], a[1]);
    }
    sealed class ContaTema : NativeWindow
    {
        public int Vezes;
        public ContaTema(IntPtr h) { AssignHandle(h); }
        protected override void WndProc(ref Message m) { if (m.Msg == 0x031A) Vezes++; base.WndProc(ref m); }
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
    static int Rodar(string dll, string saida)
    {
        Assembly dark = Assembly.LoadFrom(dll);
        BindingFlags E = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        var cfg = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { { "Tema", "ardosia" } };
        dark.GetType("Xpz.Dark.Paleta").GetMethod("Carregar", E).Invoke(null, new object[] { cfg });
        dark.GetType("Xpz.Dark.Cores").GetMethod("CarregarIsl", E).Invoke(null, new object[] { "teste" });

        var f = new Form { Text = "rolagens", Size = new Size(900, 420), StartPosition = FormStartPosition.Manual, Location = new Point(60, 60), TopMost = true };
        var arvore = new UltraTree { Left = 10, Top = 10, Width = 200, Height = 360 };
        for (int i = 0; i < 100; i++) arvore.Nodes.Add("objeto " + i);
        f.Controls.Add(arvore);
        var tc = new TabControl { Left = 220, Top = 10, Width = 200, Height = 120 }; tc.TabPages.Add("General"); f.Controls.Add(tc);
        var vs = new VScrollBar { Left = 430, Top = 10, Height = 200 }; f.Controls.Add(vs);
        // barra do DevExpress (a das arvores Variables/Structure): clara de fabrica
        Type tDx = Assembly.LoadFrom(Path.Combine(GX, "DevExpress.Utils.v7.3.dll")).GetType("DevExpress.XtraEditors.VScrollBar", true);
        var dx = (Control)Activator.CreateInstance(tDx);
        dx.Left = 460; dx.Top = 10; dx.Height = 200;
        tDx.GetProperty("Maximum").SetValue(dx, 100, null); tDx.GetProperty("LargeChange").SetValue(dx, 20, null); tDx.GetProperty("Value").SetValue(dx, 30, null);
        f.Controls.Add(dx);
        var abas = new Crownwood.Magic.Controls.TabControl { Style = VisualStyle.Plain, Appearance = Crownwood.Magic.Controls.TabControl.VisualAppearance.MultiDocument, Left = 220, Top = 220, Width = 640, Height = 60 };
        foreach (var n in new[] { "Web Layout", "Rules", "Events", "Conditions", "Variables" }) abas.TabPages.Add(new Crownwood.Magic.Controls.TabPage(n));
        abas.SelectedIndex = 2; f.Controls.Add(abas);
        dark.GetType("Xpz.Dark.Fase2").GetMethod("Iniciar", E).Invoke(null, new object[] { cfg });
        f.Show(); f.Activate(); Application.DoEvents();
        dark.GetType("Xpz.Dark.Fase2").GetMethod("Processar", E).Invoke(null, new object[] { f });
        Application.DoEvents();
        abas.BackColor = SystemColors.Control;   // o GeneXus repoe o fundo
        Application.DoEvents(); System.Threading.Thread.Sleep(400); Application.DoEvents();
        // so a area interna: barra de titulo (ativa/inativa) e o que esta atras da janela variam
        Rectangle area = f.RectangleToScreen(f.ClientRectangle);
        using (var b = new Bitmap(area.Width, area.Height)) { using (var g = Graphics.FromImage(b)) g.CopyFromScreen(area.Location, Point.Empty, area.Size); b.Save(Path.Combine(saida, "rolagens.png")); }
        int falhas = 0;
        // mouse por cima do polegar e clique na seta de baixo: o DevExpress redesenha
        IntPtr meio = (IntPtr)((70 << 16) | 8), seta = (IntPtr)(((dx.Height - 5) << 16) | 8);
        SendMessage(dx.Handle, 0x0200, IntPtr.Zero, meio); Application.DoEvents();
        SendMessage(dx.Handle, 0x0201, (IntPtr)1, seta); SendMessage(dx.Handle, 0x0202, IntPtr.Zero, seta);
        SendMessage(dx.Handle, 0x0200, IntPtr.Zero, seta);
        for (int i = 0; i < 5; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(40); }
        Console.WriteLine("  valor depois do clique na seta: " + tDx.GetProperty("Value").GetValue(dx, null));
        using (var b = new Bitmap(dx.Width, dx.Height))
        {
            Rectangle rd = dx.RectangleToScreen(dx.ClientRectangle);
            using (var g = Graphics.FromImage(b)) g.CopyFromScreen(rd.Location, Point.Empty, rd.Size);
            int claros = 0;
            for (int y = 0; y < b.Height; y += 2) for (int x = 0; x < b.Width; x += 2) { Color c = b.GetPixel(x, y); if (c.R + c.G + c.B > 3 * 170) claros++; }
            bool ok = claros == 0;
            if (!ok) falhas++;
            Console.WriteLine("  {0} {1,-56} {2}", ok ? "[ok] " : "FALHA", "barra de rolagem DevExpress escura", claros + " pontos claros");
        }
        // tema escuro de rolagem: UMA vez por janela, por mais que a revisita de 3 s peca
        // (janela criada sem ser mostrada: mostrada, o gancho de janela nova ja aplicaria o
        // tema antes do contador - a conta daria 0)
        var painel = new Panel { Size = new Size(120, 120), AutoScroll = true };
        painel.Controls.Add(new Label { Width = 400, Height = 400 });
        IntPtr hp = painel.Handle;
        var conta = new ContaTema(painel.Handle);
        MethodInfo rol = dark.GetType("Xpz.Dark.Fase2").GetMethod("RolagemEscuraQuandoPuder", E);
        for (int i = 0; i < 5; i++) { rol.Invoke(null, new object[] { painel }); Application.DoEvents(); }
        bool umaVez = conta.Vezes == 1;
        if (!umaVez) falhas++;
        Console.WriteLine("  {0} {1,-56} {2}", umaVez ? "[ok] " : "FALHA", "tema de rolagem aplicado uma vez (5 revisitas)", conta.Vezes + " WM_THEMECHANGED");
        conta.ReleaseHandle();
        painel.Dispose();
        f.Close();
        return falhas;
    }
}
