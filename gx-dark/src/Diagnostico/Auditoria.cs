// Xpz.Dark - AUDITORIA: acha sozinho o que ainda esta claro.
//
// Em vez de depender de print do usuario: fotografa cada janela visivel do
// GeneXus (inclusive menus de contexto, dicas e dialogos, que sao janelas de topo
// proprias), amostra os pixels em grade e, para cada ponto CLARO, pergunta ao
// Windows qual janela/controle esta ali (WindowFromPoint). Agrupa por controle e
// grava em dark-auditoria.log quem tem area clara - com tipo, pais e cores.
//
// So LE a tela. Roda: 25s apos subir, a cada 2 min com o GeneXus em primeiro
// plano, quando surge janela de topo nova (menu de contexto, dialogo) e pelo
// botao "Varrer agora" da tela de configuracao.
//
// O designer de tela (Web Layout) e as paginas web ficam de fora: claro ali e o
// desenho do usuario ou conteudo, nao tema.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace Xpz.Dark
{
    internal static class Auditoria
    {
        private const int Passo = 10;              // px entre amostras
        private const double Claro = 0.70;         // luminancia de ponto "claro"

        private static readonly HashSet<IntPtr> _topoConhecidas = new HashSet<IntPtr>();
        private static readonly Dictionary<IntPtr, int> _pendentes = new Dictionary<IntPtr, int>();
        private static readonly HashSet<string> _jaRelatado = new HashSet<string>();
        private static int _segundos, _varreduras;
        public static bool Ligada = true;

        // --------------------------------------------------------------- Win32
        private delegate bool EnumProc(IntPtr h, IntPtr l);
        [DllImport("user32.dll")] private static extern bool EnumThreadWindows(uint tid, EnumProc cb, IntPtr l);
        [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr h, out RECT r);
        [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(POINT p);
        [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr h, uint flags);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr h, StringBuilder sb, int n);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr h, StringBuilder sb, int n);
        [DllImport("user32.dll")] private static extern IntPtr GetParent(IntPtr h);
        [StructLayout(LayoutKind.Sequential)] private struct RECT { public int L, T, R, B; }
        [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X, Y; public POINT(int x, int y) { X = x; Y = y; } }

        private static string Classe(IntPtr h) { var sb = new StringBuilder(128); GetClassName(h, sb, 128); return sb.ToString(); }
        private static string Titulo(IntPtr h) { var sb = new StringBuilder(128); GetWindowText(h, sb, 128); return sb.ToString(); }

        private static List<IntPtr> JanelasDeTopo()
        {
            var l = new List<IntPtr>();
            EnumThreadWindows(GetCurrentThreadId(), (h, p) =>
            {
                RECT r;
                if (IsWindowVisible(h) && GetWindowRect(h, out r) && r.R - r.L > 8 && r.B - r.T > 8) l.Add(h);
                return true;
            }, IntPtr.Zero);
            return l;
        }

        private static Timer _rapido;
        private static int _ticksRapidos;

        /// <summary>Chamado pelo tick de 1s da fase 2: varredura periodica.</summary>
        public static void Tick()
        {
            if (!Ligada || !Disjuntor.Ligada(Parte.Auditoria)) return;
            _segundos++;
            if (_rapido == null)
            {
                // Menu suspenso fica aberto poucos segundos: janelas novas sao vistas a cada
                // 300 ms (so a lista de janelas - barato) e fotografadas no tick seguinte.
                _rapido = new Timer { Interval = 300 };
                _rapido.Tick += delegate { Disjuntor.Executar(Parte.Auditoria, JanelasNovas); };
                _rapido.Start();
            }
            try
            {
                var agora = JanelasDeTopo();
                bool frente = agora.Contains(GetAncestor(GetForegroundWindow(), 2));
                if (_segundos == 25 || (_segundos % 120 == 0 && frente)) Varrer(agora, "periodica");
            }
            catch (Exception ex) { Disjuntor.Falhou(Parte.Auditoria, ex); }
        }

        private static void JanelasNovas()
        {
            _ticksRapidos++;
            var agora = JanelasDeTopo();
            foreach (IntPtr h in agora)
            {
                if (!_topoConhecidas.Add(h)) continue;
                // Dica (tooltip) nativa do Windows: o tema escuro do Explorer a escurece
                // da proxima vez que aparecer.
                if (Classe(h).Contains("tooltips_class32")) Fase2.TemaNativo(h, "DarkMode_Explorer");
                if (_segundos > 20) _pendentes[h] = 1;   // nova: varrer no proximo tick (ja desenhada)
            }
            foreach (IntPtr h in _pendentes.Keys.ToList())
            {
                if (_pendentes[h]-- > 0) continue;
                _pendentes.Remove(h);
                if (agora.Contains(h)) Varrer(new List<IntPtr> { h }, "janela nova");
            }
            _topoConhecidas.IntersectWith(agora);
        }

        public static void VarrerAgora()
        {
            _jaRelatado.Clear();
            Varrer(JanelasDeTopo(), "pedida pelo usuario");
        }

        private sealed class Achado
        {
            public string Chave, Descricao;
            public int Claros, Total;
            public Rectangle Area;
            public int Esquerda;                                                    // x da janela na tela
            public readonly Dictionary<int, int[]> Faixas = new Dictionary<int, int[]>();   // coluna de 16 px -> [claros, total]
        }

        private static void Varrer(List<IntPtr> janelas, string motivo)
        {
            var sw = Stopwatch.StartNew();
            var achados = new Dictionary<string, Achado>();
            var porJanela = new Dictionary<IntPtr, Achado>();   // cache: WindowFromPoint repete muito
            int pontos = 0;
            foreach (IntPtr topo in janelas)
            {
                RECT r;
                if (!GetWindowRect(topo, out r)) continue;
                var tela = SystemInformation.VirtualScreen;
                var area = Rectangle.Intersect(new Rectangle(r.L, r.T, r.R - r.L, r.B - r.T), tela);
                if (area.Width < 8 || area.Height < 8) continue;
                using (var bmp = new Bitmap(area.Width, area.Height))
                {
                    using (Graphics g = Graphics.FromImage(bmp)) g.CopyFromScreen(area.Location, Point.Empty, area.Size);
                    for (int y = Passo / 2; y < area.Height; y += Passo)
                        for (int x = Passo / 2; x < area.Width; x += Passo)
                        {
                            var pt = new POINT(area.X + x, area.Y + y);
                            IntPtr h = WindowFromPoint(pt);
                            if (h == IntPtr.Zero || GetAncestor(h, 2) != topo) continue;   // coberto por outra janela
                            pontos++;
                            Achado a;
                            if (!porJanela.TryGetValue(h, out a))
                            {
                                string chave = ChaveDe(h);
                                if (chave != null && !achados.TryGetValue(chave, out a))
                                {
                                    RECT rh; GetWindowRect(h, out rh);
                                    achados[chave] = a = new Achado { Chave = chave, Descricao = Descrever(h, topo), Esquerda = rh.L };
                                }
                                porJanela[h] = a;   // null = ignorar
                            }
                            if (a == null) continue;
                            Color c = bmp.GetPixel(x, y);
                            a.Total++;
                            bool claro = Paleta.Lum(c) >= Claro;
                            if (claro) a.Claros++;
                            int[] fx; int col = (area.X + x - a.Esquerda) / 16;
                            if (!a.Faixas.TryGetValue(col, out fx)) a.Faixas[col] = fx = new int[2];
                            fx[1]++; if (claro) fx[0]++;
                        }
                }
            }

            // Area clara de verdade: muitos pontos e boa parte deles claros. Texto claro
            // em fundo escuro fica abaixo de ~30%.
            var claros = achados.Values.Where(a => a.Claros >= 8 && a.Claros * 100 / Math.Max(1, a.Total) >= 35)
                                       .OrderByDescending(a => a.Claros).ToList();
            // FAIXA clara dentro de uma janela escura (a lateral de icones de um menu, uma
            // coluna de grade): a janela inteira fica abaixo dos 35%, a faixa nao.
            foreach (Achado a in achados.Values.Where(x => !claros.Contains(x)))
            {
                var faixa = a.Faixas.Where(kv => kv.Value[1] >= 6 && kv.Value[0] * 100 / kv.Value[1] >= 70).OrderBy(kv => kv.Key).ToList();
                if (faixa.Count == 0) continue;
                a.Descricao = string.Format("[faixa clara a {0}-{1} px da esquerda] ", faixa.First().Key * 16, (faixa.Last().Key + 1) * 16) + a.Descricao;
                a.Claros = faixa.Sum(kv => kv.Value[0]);
                a.Total = Math.Max(a.Total, 1);
                claros.Add(a);
            }
            var novos = claros.Where(a => _jaRelatado.Add(a.Chave)).ToList();
            sw.Stop();
            _varreduras++;
            Log.Linha(string.Format("auditoria ({0}): {1} janela(s), {2} pontos, {3} area(s) clara(s), {4} nova(s), {5:0} ms",
                motivo, janelas.Count, pontos, claros.Count, novos.Count, sw.Elapsed.TotalMilliseconds));
            if (novos.Count == 0) return;
            try
            {
                using (var w = new StreamWriter(Path.Combine(Config.Pasta(), "dark-auditoria.log"), _varreduras > 1))
                {
                    w.WriteLine();
                    w.WriteLine("== " + DateTime.Now.ToString("dd/MM HH:mm:ss") + "  varredura " + motivo);
                    foreach (Achado a in novos)
                        w.WriteLine(string.Format("  {0,3}% claro ({1,4} pts)  {2}", a.Claros * 100 / a.Total, a.Claros, a.Descricao));
                }
            }
            catch { }
        }

        /// <summary>null = ignorar (designer de tela, pagina web).</summary>
        private static string ChaveDe(IntPtr h)
        {
            Control c = Control.FromChildHandle(h);
            if (c == null) return "nativo:" + Classe(h) + "@" + h.ToInt64();
            for (Control p = c; p != null; p = p.Parent)
            {
                string n = p.GetType().FullName ?? "";
                if (Designer.Eh(p.GetType()) && !(n.EndsWith("ActionBar") || n.EndsWith("LayoutMainPanel") || n.EndsWith("DesignerFrame"))) return null;
                if (Web.Eh(p)) return null;
            }
            return c.GetType().FullName + "@" + c.Handle.ToInt64();
        }

        private static string Descrever(IntPtr h, IntPtr topo)
        {
            Control c = Control.FromChildHandle(h);
            var sb = new StringBuilder();
            if (c == null)
            {
                sb.Append("[janela nativa] classe=").Append(Classe(h));
                IntPtr pai = GetParent(h);
                if (pai != IntPtr.Zero) sb.Append(" pai=").Append(Classe(pai));
                Control dono = Control.FromChildHandle(pai != IntPtr.Zero ? pai : topo);
                if (dono != null) sb.Append(" dono=").Append(dono.GetType().FullName);
                sb.Append(" titulo=\"").Append(Titulo(topo)).Append("\"");
                return sb.ToString();
            }
            sb.Append(c.GetType().FullName).Append(" ").Append(Censo.Familia(c.GetType()));
            sb.Append(string.Format(" [{0}x{1}] fundo={2} texto={3}", c.Width, c.Height, Censo.Cor(c.BackColor), Censo.Cor(c.ForeColor)));
            int n = 0;
            for (Control p = c.Parent; p != null && n < 4; p = p.Parent, n++) sb.Append("  < ").Append(p.GetType().Name);
            Form f = c.FindForm();
            if (f != null) sb.Append("  {janela ").Append(f.GetType().Name).Append(" \"").Append(f.Text).Append("\"}");
            return sb.ToString();
        }
    }
}
