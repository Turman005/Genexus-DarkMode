// Renderiza cada pagina do Setup-GxDarkTheme.exe fora da tela (Mono WinForms + Xvfb no Linux, ou .NET Framework no Windows)
// nas escalas 100/125/150/200% e procura texto cortado. NAO instala nada: so monta as paginas (o trabalho e simulado).
//   mcs -r:System.Windows.Forms -r:System.Drawing -r:Setup-GxDarkTheme.exe Render.cs && xvfb-run mono Render.exe <saida>
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using GxDarkTheme.Instalador;

static class Render
{
    static string saida; static int problemas;
    const BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static object Call(object o, string m, params object[] a) { return o.GetType().GetMethod(m, BF).Invoke(o, a); }
    static void SetF(object o, string f, object v) { o.GetType().GetField(f, BF).SetValue(o, v); }
    static object GetF(object o, string f) { return o.GetType().GetField(f, BF).GetValue(o); }
    static void Pump() { for (int i = 0; i < 6; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(15); } }

    static void Snap(Form f, string nome)
    {
        Pump(); f.PerformLayout(); Pump();
        f.Activate(); f.Refresh(); Pump();
        Point o = f.PointToScreen(Point.Empty);
        using (Bitmap b = new Bitmap(f.ClientSize.Width, f.ClientSize.Height))
        {
            // copia da tela (Xvfb sem gerenciador de janelas = so a area cliente; a moldura do Windows nao aparece)
            using (Graphics g = Graphics.FromImage(b)) g.CopyFromScreen(o, Point.Empty, b.Size);
            b.Save(Path.Combine(saida, nome + ".png"));
        }
        Verificar(f, f, nome);
    }

    // texto cortado: label/checkbox/radio/botao menor que o tamanho preferido, ou controle fora da area visivel do pai
    static void Verificar(Form f, Control c, string nome)
    {
        foreach (Control k in c.Controls)
        {
            if (!k.Visible) continue;
            Rectangle visivel = k.Parent.ClientRectangle;
            if (!(k.Parent is ScrollableControl && ((ScrollableControl)k.Parent).AutoScroll))
                if (k.Right > visivel.Right + 1 || k.Bottom > visivel.Bottom + 1 || k.Left < -1 || k.Top < -1)
                    Falha(nome, k, "fora da area do pai " + k.Bounds + " em " + visivel);
            if ((k is Label || k is ButtonBase) && !string.IsNullOrEmpty(k.Text) && k.Width > 0)
            {
                Size pref = k.GetPreferredSize(new Size(k.Width, 0));
                bool quebra = k is Label;
                if (quebra ? pref.Height > k.Height + 1 : (pref.Width > k.Width + 1 || pref.Height > k.Height + 1))
                    Falha(nome, k, "texto nao cabe: tamanho " + k.Size + " preferido " + pref);
                if (k is Label && TextRenderer.MeasureText(LongestWord(k.Text), k.Font).Width > k.Width + 2)
                    Falha(nome, k, "palavra maior que a largura (" + LongestWord(k.Text) + ")");
            }
            Verificar(f, k, nome);
        }
    }
    static string LongestWord(string s) { string m = ""; foreach (string w in s.Split(' ', '\r', '\n')) if (w.Length > m.Length) m = w; return m; }
    static void Falha(string nome, Control k, string msg) { problemas++; Console.WriteLine("  CORTADO [" + nome + "] " + k.GetType().Name + " \"" + Trim(k.Text) + "\": " + msg); }
    static string Trim(string s) { s = (s ?? "").Replace("\r", " ").Replace("\n", " "); return s.Length > 40 ? s.Substring(0, 40) + "..." : s; }

    static Assistente Novo(bool un, float esc, Size? tamanho)
    {
        Assistente f = new Assistente(un);
        f.StartPosition = FormStartPosition.Manual; f.Location = new Point(0, 0);
        f.Show(); Pump();
        if (Math.Abs(esc - 1f) > 0.01f) f.AplicarEscala(esc);
        if (tamanho.HasValue) f.Size = tamanho.Value;
        Pump();
        return f;
    }

    static void Progresso(Assistente f, bool un)
    {
        Call(f, "Cabecalho", un ? "Desinstalando o GxDarkTheme" : "Instalando o GxDarkTheme", "Aguarde enquanto o " + (un ? "desinstalador" : "instalador") + " trabalha");
        Call(f, "MontarProgresso");
        SetF(f, "trabalhando", true);
        Call(f, "NovaEtapa", "Copiando arquivos para GeneXus18U14 (2 de 2)...", 0.45, 0.49, false);
        foreach (string l in new string[] { "Pasta do programa: C:\\Program Files\\GxDarkTheme", "", "== GeneXus18 (C:\\Program Files (x86)\\GeneXus\\GeneXus18)",
            "  removido C:\\Program Files (x86)\\GeneXus\\GeneXus18\\Packages\\Xpz.Dark.dll",
            "  copiado C:\\Program Files (x86)\\GeneXus\\GeneXus18\\Packages\\GxDarkTheme.dll",
            "  GeneXus.exe /install (atualiza a lista de pacotes; pode levar 1-2 minutos)...", "  /install terminou (codigo 0)",
            "  catalogo de pacotes OK (GxDarkTheme.dll registrado)", "", "== GeneXus18U14 (C:\\Program Files (x86)\\GeneXus\\GeneXus18U14)",
            "  copiado C:\\Program Files (x86)\\GeneXus\\GeneXus18U14\\Packages\\GxDarkTheme.dll" })
            Call(f, "NovaLinha", l);
        Call(f, "NovaEtapa", un ? "Atualizando o GeneXus: GeneXus18U14 (2 de 2)... (pode levar 1-2 minutos)" : "Registrando no GeneXus: GeneXus18U14 (2 de 2)... (pode levar 1-2 minutos)", 0.52, 0.81, true);
        Call(f, "NovaLinha", "  GeneXus.exe /install (atualiza a lista de pacotes; pode levar 1-2 minutos)...");
        SetF(f, "inicioEtapa", DateTime.Now.AddSeconds(-40));
        Call(f, "Avancar_Relogio");
    }

    static void Escala(float esc, string sufixo)
    {
        Console.WriteLine("escala " + sufixo);
        // ---------- instalador
        Assistente f = Novo(false, esc, null);
        Snap(f, "instalador-1-boas-vindas-" + sufixo);
        Call(f, "Ir", 1); Snap(f, "instalador-2-onde-instalar-" + sufixo);
        SetF(f, "instalacoesMarcadas", Call(f, "Marcadas"));
        Call(f, "Ir", 2); Snap(f, "instalador-3-tema-" + sufixo);
        int marcados = 0; foreach (RadioButton r in (RadioButton[])GetF(f, "radios")) if (r.Checked) { marcados++; Console.WriteLine("  tema marcado: " + r.Text); } if (marcados != 1) { problemas++; Console.WriteLine("  ERRO: " + marcados + " temas marcados"); }
        Call(f, "Ir", 3); Snap(f, "instalador-4-resumo-" + sufixo);
        Progresso(f, false); Snap(f, "instalador-5-progresso-" + sufixo);
        Call(f, "AlternarDetalhes"); Snap(f, "instalador-6-progresso-detalhes-" + sufixo);
        SetF(f, "trabalhando", false); SetF(f, "terminou", true);
        Call(f, "MontarConcluido", true, @"C:\Users\fulano\AppData\Local\Temp\GxDarkTheme-instalacao.log"); Snap(f, "instalador-7-concluido-" + sufixo);
        Call(f, "MontarConcluido", false, @"C:\Users\fulano\AppData\Local\Temp\GxDarkTheme-instalacao.log"); Snap(f, "instalador-8-concluido-avisos-" + sufixo);
        f.Dispose();
        // tamanho minimo
        f = Novo(false, esc, null); f.Size = f.MinimumSize; Pump();
        Call(f, "Ir", 2); Snap(f, "instalador-9-tema-minimo-" + sufixo);
        Progresso(f, false); Call(f, "AlternarDetalhes"); Snap(f, "instalador-10-progresso-minimo-" + sufixo);
        SetF(f, "trabalhando", false); f.Dispose();
        // ---------- desinstalador
        f = Novo(true, esc, null);
        Snap(f, "desinstalador-1-confirmar-" + sufixo);
        Progresso(f, true); Snap(f, "desinstalador-2-progresso-" + sufixo);
        SetF(f, "trabalhando", false); SetF(f, "terminou", true);
        Call(f, "MontarConcluido", true, @"C:\Users\fulano\AppData\Local\Temp\GxDarkTheme-desinstalacao.log"); Snap(f, "desinstalador-3-concluido-" + sufixo);
        f.Dispose();
    }

    [STAThread]
    static int Main(string[] a)
    {
        saida = a.Length > 0 ? a[0] : "."; Directory.CreateDirectory(saida);
        Application.EnableVisualStyles();
        foreach (float e in new float[] { 1f, 1.25f, 1.5f, 2f }) Escala(e, ((int)(e * 100)).ToString());
        Console.WriteLine(problemas == 0 ? "NENHUM TEXTO CORTADO" : problemas + " PROBLEMA(S)");
        return problemas == 0 ? 0 : 1;
    }
}
