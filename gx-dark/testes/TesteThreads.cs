// Janela criada em OUTRA thread (com laco de mensagens proprio): a Xpz.Dark nao pode tocar
// nela - o erro "Cross-thread operation not valid" relatado em 28/10. E o registro de
// diagnostico grava a pilha desse erro no dark.log, dizendo se passa pela Xpz.Dark.
using System; using System.Collections.Generic; using System.Drawing; using System.IO; using System.Reflection; using System.Threading; using System.Windows.Forms;

static class T
{
    const string GX = @"C:\Program Files (x86)\GeneXus\GeneXus18-U10";
    static int falhas;
    static void Check(string n, bool ok, string d) { Console.WriteLine("  {0} {1,-58} {2}", ok ? "[ok] " : "FALHA", n, d); if (!ok) falhas++; }

    [STAThread] static int Main(string[] a)
    {
        AppDomain.CurrentDomain.AssemblyResolve += (s, e) => { string p = Path.Combine(GX, new AssemblyName(e.Name).Name + ".dll"); return File.Exists(p) ? Assembly.LoadFrom(p) : null; };
        return Rodar(a[0]);
    }

    static int Rodar(string dll)
    {
        Assembly dark = Assembly.LoadFrom(dll);
        BindingFlags E = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        Control.CheckForIllegalCrossThreadCalls = true;   // como no GeneXus (o erro aparece la)
        // So no perfil isolado: rodado direto, gravaria no dark.log do usuario (aconteceu em 28/10).
        string pasta = Environment.GetEnvironmentVariable("XPZDARK_PASTA");
        if (string.IsNullOrEmpty(pasta)) { Console.WriteLine("  FALHA XPZDARK_PASTA ausente: rode pelo Testar-Dark.ps1"); return 1; }
        string log = Path.Combine(pasta, "dark.log");
        long inicio = File.Exists(log) ? new FileInfo(log).Length : 0;

        dark.GetType("Xpz.Dark.ErrosDeThread", true).GetMethod("Ligar", E).Invoke(null, null);
        var cfg = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { { "Tema", "ardosia" } };
        dark.GetType("Xpz.Dark.Fase2").GetMethod("Iniciar", E).Invoke(null, new object[] { cfg });

        // janela clara numa thread propria
        Form outra = null; var pronta = new ManualResetEvent(false);
        var th = new Thread(() =>
        {
            outra = new Form { Text = "outra thread", BackColor = Color.White, Size = new Size(200, 120), StartPosition = FormStartPosition.Manual, Location = new Point(-32000, -32000) };
            outra.Controls.Add(new Button { Text = "ok", BackColor = SystemColors.Control });
            outra.Shown += delegate { pronta.Set(); };
            Application.Run(outra);
        });
        th.SetApartmentState(ApartmentState.STA); th.IsBackground = true; th.Start();
        pronta.WaitOne(5000);

        // a fase 2 e o seu tick enxergam a janela em Application.OpenForms
        Exception erro = null;
        try
        {
            dark.GetType("Xpz.Dark.Fase2").GetMethod("Processar", E).Invoke(null, new object[] { outra });
            dark.GetType("Xpz.Dark.Fase2").GetMethod("Tick", E).Invoke(null, null);
            dark.GetType("Xpz.Dark.Repintar").GetMethod("Tudo", E).Invoke(null, new object[] { "teste" });
        }
        catch (Exception ex) { erro = ex.InnerException ?? ex; }
        Check("janela de outra thread: fase 2 nao da erro", erro == null, erro == null ? "" : erro.GetType().Name + ": " + erro.Message);
        Check("janela de outra thread: fica como esta (sem pintar)", outra.BackColor.ToArgb() == Color.White.ToArgb(), outra.BackColor.ToString());

        // o erro de thread do PROPRIO programa (nao da Xpz.Dark) fica registrado com a pilha
        try { IntPtr h = outra.Handle; } catch (InvalidOperationException) { }
        outra.BeginInvoke((MethodInvoker)delegate { outra.Close(); });
        th.Join(3000);
        string novo = "";
        if (File.Exists(log)) using (var fs = new FileStream(log, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) { fs.Seek(inicio, SeekOrigin.Begin); novo = new StreamReader(fs).ReadToEnd(); }
        Check("janela de outra thread anotada no log", novo.Contains("janela de outra thread deixada como esta"), "");
        Check("erro de thread registrado com a pilha", novo.Contains("ERRO DE THREAD") && novo.Contains("Rodar"), "");
        Check("pilha classificada: NAO passa pela Xpz.Dark", novo.Contains("a pilha NAO passa pela Xpz.Dark") && !novo.Contains("A PILHA PASSA PELA XPZ.DARK"), "");

        // A CAUSA REAL do erro de 28/10 (Tools > Options): caixa de filtro numa faixa de
        // ferramentas, com sugestoes automaticas ligadas DEPOIS de a caixa existir; o dialogo
        // fecha e o GeneXus descarta a pagina. Com a subclasse da Xpz.Dark soltando cedo, a caixa
        // ficava com handle morto e o Dispose estourava. Tres aberturas, como no GeneXus.
        for (int rodada = 1; rodada <= 3; rodada++)
        {
            var dlg = new Form { Text = "Options", StartPosition = FormStartPosition.Manual, Location = new Point(100, 100), Size = new Size(600, 400), ShowInTaskbar = false };
            var painel = new Panel { Dock = DockStyle.Fill }; dlg.Controls.Add(painel);
            var pagina = new UserControl { Dock = DockStyle.Fill };
            pagina.Controls.Add(new PropertyGrid { Dock = DockStyle.Fill });
            var faixa = new ToolStrip { Dock = DockStyle.Top };
            var filtro = new ToolStripTextBox(); faixa.Items.Add(filtro); pagina.Controls.Add(faixa);
            painel.Controls.Add(pagina);
            var fechar = new System.Windows.Forms.Timer { Interval = 300 }; fechar.Tick += delegate { fechar.Stop(); dlg.Close(); };
            dlg.Shown += delegate
            {
                // como no GeneXus: a Xpz.Dark ja tratou a caixa quando as sugestoes sao ligadas
                dark.GetType("Xpz.Dark.Fase2").GetMethod("Processar", E).Invoke(null, new object[] { dlg });
                filtro.AutoCompleteMode = AutoCompleteMode.SuggestAppend; filtro.AutoCompleteSource = AutoCompleteSource.CustomSource;
                filtro.AutoCompleteCustomSource.AddRange(new[] { "Name", "Description" });
                fechar.Start();
            };
            dlg.ShowDialog();
            Check("Options " + rodada + ": caixa tratada pela Xpz.Dark", filtro.TextBox.BorderStyle == BorderStyle.FixedSingle, filtro.TextBox.BorderStyle.ToString());
            Check("Options " + rodada + ": caixa sem handle morto apos fechar", !filtro.TextBox.IsHandleCreated, "");
            Exception descarte = null;
            try { pagina.Dispose(); } catch (Exception ex) { descarte = ex; }
            Check("Options " + rodada + ": descartar a pagina nao da erro", descarte == null, descarte == null ? "" : descarte.GetType().Name + ": " + descarte.Message);
            dlg.Dispose();
        }
        Console.WriteLine(falhas == 0 ? "TODOS OS TESTES PASSARAM" : falhas + " FALHA(S)");
        return falhas;
    }
}
