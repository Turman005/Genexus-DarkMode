// Teste por foto: compara testes\saida\*.png com testes\referencia\*.png.
//   ComparaFotos <referencia> <saida>               compara (sai com o numero de fotos diferentes)
//   ComparaFotos <referencia> <saida> referencia    grava as fotos atuais como nova referencia
// Pixel "diferente" = algum canal mudou mais que 24 (anti-aliasing e cursor piscando nao
// contam); foto "diferente" = mais de 0,5% dos pixels diferentes ou tamanho mudou.
using System; using System.Drawing; using System.Drawing.Imaging; using System.IO; using System.Runtime.InteropServices;

static class ComparaFotos
{
    static int Main(string[] a)
    {
        string refDir = a[0], saida = a[1];
        if (a.Length > 2 && a[2] == "referencia")
        {
            Directory.CreateDirectory(refDir);
            foreach (string f in Directory.GetFiles(saida, "*.png")) File.Copy(f, Path.Combine(refDir, Path.GetFileName(f)), true);
            Console.WriteLine("  referencia gravada: " + Directory.GetFiles(refDir, "*.png").Length + " fotos");
            return 0;
        }
        if (!Directory.Exists(refDir)) { Console.WriteLine("  sem referencia (rode com -NovaReferencia)"); return 0; }
        int ruins = 0;
        foreach (string r in Directory.GetFiles(refDir, "*.png"))
        {
            string nome = Path.GetFileName(r), s = Path.Combine(saida, nome);
            if (!File.Exists(s)) { Console.WriteLine("  FALHA {0,-30} sem foto nova", nome); ruins++; continue; }
            using (var b1 = new Bitmap(r)) using (var b2 = new Bitmap(s))
            {
                if (b1.Size != b2.Size) { Console.WriteLine("  FALHA {0,-30} tamanho {1} -> {2}", nome, b1.Size, b2.Size); ruins++; continue; }
                double pct = Diferenca(b1, b2);
                bool ok = pct <= 0.5;
                Console.WriteLine("  {0} {1,-30} {2:0.00}% diferente", ok ? "[ok] " : "FALHA", nome, pct);
                if (!ok) ruins++;
            }
        }
        return ruins;
    }

    static double Diferenca(Bitmap a, Bitmap b)
    {
        var r = new Rectangle(0, 0, a.Width, a.Height);
        BitmapData da = a.LockBits(r, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb), db = b.LockBits(r, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            int n = da.Stride * a.Height;
            byte[] x = new byte[n], y = new byte[n];
            Marshal.Copy(da.Scan0, x, 0, n); Marshal.Copy(db.Scan0, y, 0, n);
            long dif = 0;
            for (int i = 0; i < n; i += 4)
                if (Math.Abs(x[i] - y[i]) > 24 || Math.Abs(x[i + 1] - y[i + 1]) > 24 || Math.Abs(x[i + 2] - y[i + 2]) > 24) dif++;
            return dif * 100.0 / (a.Width * (double)a.Height);
        }
        finally { a.UnlockBits(da); b.UnlockBits(db); }
    }
}
