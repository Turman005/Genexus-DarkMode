// Fotografa o menu de contexto IGContextMenu em 3 estilos, com o modo escuro de menus
// nativos da Xpz.Dark ligado e a tabela global Office2007/2010 em Black.
using System; using System.Drawing; using System.IO; using System.Reflection; using System.Runtime.InteropServices; using System.Windows.Forms;
using Infragistics.Win.IGControls;
static class T
{
    [DllImport("user32.dll")] static extern IntPtr FindWindow(string c, string t);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool EndMenu();
    struct RECT { public int L, T, R, B; }
    [DllImport("uxtheme.dll", EntryPoint = "#135")] static extern int SetPreferredAppMode(int m);
    [DllImport("uxtheme.dll", EntryPoint = "#104")] static extern void RefreshImmersiveColorPolicyState();
    [DllImport("uxtheme.dll", EntryPoint = "#133")] static extern bool AllowDarkModeForWindow(IntPtr h, bool a);
    [DllImport("uxtheme.dll", EntryPoint = "#136")] static extern void FlushMenuThemes();
    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] static extern int SetWindowTheme(IntPtr h, string a, string b);
    [STAThread] static int Main(string[] a)
    {
        const string GX = @"C:\Program Files (x86)\GeneXus\GeneXus18-U10";
        AppDomain.CurrentDomain.AssemblyResolve += (s, e) => { string p = Path.Combine(GX, new AssemblyName(e.Name).Name + ".dll"); return File.Exists(p) ? Assembly.LoadFrom(p) : null; };
        return Rodar(a[0], a[1]);
    }
    static int Rodar(string dll, string saida)
    {
        Assembly dark = Assembly.LoadFrom(dll);
        dark.GetType("Xpz.Dark.Fase2").GetMethod("MenusNativosEscuros", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        Infragistics.Win.Office2007ColorTable.ColorScheme = Infragistics.Win.Office2007ColorScheme.Black;
        Infragistics.Win.Office2010ColorTable.ColorScheme = Infragistics.Win.Office2010ColorScheme.Black;
        var f = new Form { Text = "teste menu", Size = new Size(400, 300), StartPosition = FormStartPosition.CenterScreen, TopMost = true };
        string modo = Environment.GetEnvironmentVariable("MODO") ?? "";
        if (modo.Contains("r")) { SetPreferredAppMode(2); RefreshImmersiveColorPolicyState(); }
        f.Show(); Application.DoEvents();
        if (modo.Contains("w")) { AllowDarkModeForWindow(f.Handle, true); }
        if (modo.Contains("t")) { SetWindowTheme(f.Handle, "DarkMode_Explorer", null); }
        if (modo.Contains("f")) { FlushMenuThemes(); }
        Console.WriteLine("modo=" + modo);
        foreach (var estilo in new[] { MenuStyle.OfficeXP, MenuStyle.System, MenuStyle.Office2007, MenuStyle.Office2010 })
        {
            var m = new IGContextMenu { Style = estilo };
            foreach (var t in new[] { "Close", "Close All But This", "-", "Open Containing Folder", "Properties" }) m.MenuItems.Add(new IGMenuItem(t));
            m.MenuItems[4].Enabled = false;
            var tm = new Timer { Interval = 500 };
            tm.Tick += delegate
            {
                tm.Stop();
                IntPtr h = FindWindow("#32768", null); RECT r; GetWindowRect(h, out r);
                using (var b = new Bitmap(Math.Max(1, r.R - r.L), Math.Max(1, r.B - r.T)))
                {
                    using (var g = Graphics.FromImage(b)) g.CopyFromScreen(r.L, r.T, 0, 0, b.Size);
                    b.Save(Path.Combine(saida, "menu-" + estilo + ".png"));
                    Color c = b.GetPixel(b.Width / 2, b.Height - 6);
                    Console.WriteLine("{0,-12} {1}x{2} fundo perto da base = {3},{4},{5}", estilo, b.Width, b.Height, c.R, c.G, c.B);
                }
                EndMenu();
            };
            tm.Start();
            m.Show(f, new Point(40, 40));
            Application.DoEvents();
        }
        f.Close();
        return 0;
    }
}
