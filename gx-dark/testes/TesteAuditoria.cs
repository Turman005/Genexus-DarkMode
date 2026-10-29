// Mostra uma janela escura com um painel BRANCO e uma lista escura com texto; a
// auditoria tem que apontar o painel branco e NAO a lista.
using System; using System.Drawing; using System.IO; using System.Reflection; using System.Windows.Forms;
namespace Teste
{
    class PainelBrancoProposital : Panel { }
    class ListaEscura : ListBox { }
    // como um menu com a lateral de icones clara: escuro, com faixa branca de 20 px a esquerda
    class MenuComLateral : Control
    {
        public MenuComLateral() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Color.FromArgb(30, 30, 30));
            e.Graphics.FillRectangle(Brushes.White, 0, 0, 20, Height);
            for (int y = 8; y < Height; y += 22) TextRenderer.DrawText(e.Graphics, "Item de menu " + y, Font, new Point(28, y), Color.Gainsboro);
        }
    }
}
static class T
{
    [STAThread] static int Main(string[] a)
    {
        const string GX = @"C:\Program Files (x86)\GeneXus\GeneXus18-U10";
        AppDomain.CurrentDomain.AssemblyResolve += (s, e) => { string p = Path.Combine(GX, new AssemblyName(e.Name).Name + ".dll"); return File.Exists(p) ? Assembly.LoadFrom(p) : null; };
        var f = new Form { Text = "teste auditoria", BackColor = Color.FromArgb(37, 37, 38), Size = new Size(760, 300), TopMost = true, StartPosition = FormStartPosition.CenterScreen };
        var branco = new Teste.PainelBrancoProposital { BackColor = Color.White, Bounds = new Rectangle(10, 10, 200, 200) };
        var lista = new Teste.ListaEscura { BackColor = Color.FromArgb(30, 30, 30), ForeColor = Color.FromArgb(222, 222, 222), Bounds = new Rectangle(230, 10, 240, 200) };
        for (int i = 0; i < 8; i++) lista.Items.Add("item de texto claro numero " + i);
        var menu = new Teste.MenuComLateral { Bounds = new Rectangle(490, 10, 240, 200) };
        f.Controls.Add(branco); f.Controls.Add(lista); f.Controls.Add(menu);
        f.Show(); f.Activate(); Application.DoEvents(); System.Threading.Thread.Sleep(400); Application.DoEvents();
        Assembly dark = Assembly.LoadFrom(a[0]);
        dark.GetType("Xpz.Dark.Auditoria", true).GetMethod("VarrerAgora", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Invoke(null, null);
        f.Close();
        string log = File.ReadAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Xpz.Dark\dark-auditoria.log"));
        bool achou = log.Contains("PainelBrancoProposital"), falso = log.Contains("ListaEscura");
        bool faixa = log.Contains("faixa clara a 0-") && log.Contains("MenuComLateral");
        Console.WriteLine("  {0} painel branco apontado", achou ? "[ok] " : "FALHA");
        Console.WriteLine("  {0} lista escura com texto claro NAO apontada", !falso ? "[ok] " : "FALHA");
        Console.WriteLine("  {0} faixa clara a esquerda de um controle escuro apontada", faixa ? "[ok] " : "FALHA");
        Console.WriteLine(log.Trim());
        return achou && !falso && faixa ? 0 : 1;
    }
}
