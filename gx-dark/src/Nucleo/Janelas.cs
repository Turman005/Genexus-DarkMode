// Xpz.Dark - FASE 2: controles comuns do Windows (WinForms).
//
// A fase 1 (o .isl escuro) escureceu tudo que e Infragistics. Sobrou o que NAO e:
// PropertyGrid, caixas de texto, combos, listas, arvores WinForms, faixas de
// ferramentas (ToolStrip), dialogos e a barra de titulo das janelas.
//
// REGRA DE OURO: so troca cor que e PADRAO DO SISTEMA ou neutra clara/escura. Uma
// cor que o GeneXus pos de proposito (fundo vermelho de erro, texto azul de link)
// fica como esta - mudar isso apagaria significado.
//
// DESEMPENHO, que e requisito:
//   - cada controle e processado UMA vez (ConditionalWeakTable: marca sem segurar
//     memoria - quando o controle morre, a marca morre junto);
//   - controle novo chega por EVENTO (ControlAdded de cada conteiner), nunca por
//     varredura periodica da arvore;
//   - janela nova e achada por um timer de 1s que so compara a LISTA de janelas
//     abertas (dezenas de itens);
//   - nada de Application.Idle nem filtro de mensagem, que rodam a cada evento.
//   Tudo cronometrado: o dark.log diz quantos controles e quantos ms.
//
// SEGURANCA: so cor e aparencia. try/catch em cada controle - um controle do
// GeneXus que reclame de cor nova nao pode derrubar o IDE.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace Xpz.Dark
{

    internal static partial class Fase2
    {
        private sealed class Marca { public int Repinturas; public DateTime Desde; }
        private static readonly ConditionalWeakTable<Control, Marca> _vistos = new ConditionalWeakTable<Control, Marca>();

        // Cor que o controle mostrava ANTES de mexermos. Serve para devolver ao
        // designer de tela exatamente a cor de fabrica (ver Designer).
        private sealed class Original { public Color Fundo, Texto; }
        private static readonly ConditionalWeakTable<Control, Original> _originais = new ConditionalWeakTable<Control, Original>();

        private static bool _pintando;          // trava contra reentrada nos eventos de cor
        internal static bool Ligada { get { return _cfg != null; } }
        private static bool _fundoWebLayout;
        private static readonly HashSet<Type> _tiposVistos = new HashSet<Type>();
        private static int _tiposNovosLogados, _janelasCensoTardio;

        private static Dictionary<string, string> _cfg;
        private static Timer _timer;
        private static int _segundos;
        private static bool _censoFeito;

        // contabilidade de desempenho
        private static long _controles, _alterados, _ticksTotal, _maiorEvento;
        private static int _eventos;
        private static readonly Dictionary<string, int> _porTipo = new Dictionary<string, int>();

        private static bool _botoes, _combos, _titulo, _rolagem;
        private static RendererEscuro _renderer;

        public static void Iniciar(Dictionary<string, string> cfg)
        {
            _cfg = cfg;
            _botoes  = Config.SimPadrao(cfg, "Botoes");
            _combos  = Config.SimPadrao(cfg, "CombosPlanos");
            _titulo  = Config.SimPadrao(cfg, "TituloEscuro");
            _rolagem = Config.SimPadrao(cfg, "BarrasRolagemEscuras");
            _fundoWebLayout = Config.SimPadrao(cfg, "FundoWebLayout");
            Web.Output = Config.SimPadrao(cfg, "OutputEscuro");
            Web.Paginas = Config.SimPadrao(cfg, "PaginasWebEscuras");
            Paleta.Carregar(cfg);
            Auditoria.Ligada = Config.Sim(cfg, "Auditoria");
            if (Config.SimPadrao(cfg, "MenusNativosEscuros")) MenusNativosEscuros();
            if (Config.SimPadrao(cfg, "EditorEscuro")) Editor.Ligar(cfg);

            try
            {
                // Faixas de ferramentas e menus de contexto que usam o renderizador
                // global passam a usar o nosso - inclusive os criados depois.
                if (Config.SimPadrao(cfg, "ToolStripsEscuros"))
                {
                    _renderer = new RendererEscuro();
                    ToolStripManager.Renderer = _renderer;
                    Log.Linha("fase 2: renderizador escuro global de ToolStrip ativo");
                }
            }
            catch (Exception ex) { Log.Linha("fase 2: renderizador FALHOU: " + Log.Curto(ex)); }

            if (Config.SimPadrao(cfg, "EscurecerAntesDeMostrar")) Gancho.Ligar();

            _threadUi = System.Threading.Thread.CurrentThread.ManagedThreadId;
            _timer = new Timer();
            _timer.Interval = 1000;
            _timer.Tick += delegate { Tick(); };
            _timer.Start();
            Log.Linha(string.Format("fase 2 ligada (botoes={0} combos={1} titulo={2} rolagem={3})", _botoes, _combos, _titulo, _rolagem));
        }

        private static void Tick()
        {
            try
            {
                _segundos++;
                var janelas = new List<Form>();
                foreach (Form f in Application.OpenForms) if (!Fase2.OutraThread(f)) janelas.Add(f);   // copia: a colecao muda
                foreach (Form f in janelas)
                {
                    Marca m;
                    if (_vistos.TryGetValue(f, out m)) continue;
                    ProcessarJanela(f, "timer");
                    // Dialogo aberto depois do censo (Opcoes, propriedades...): censo dele tambem.
                    if (_censoFeito && _janelasCensoTardio < 30 && Config.Sim(_cfg, "Censo"))
                    {
                        _janelasCensoTardio++;
                        Censo.Janela(f);
                    }
                }

                // O GeneXus troca a linguagem/estilos do editor depois de cria-lo:
                // revisita os editores conhecidos (barato e idempotente).
                var swEd = Stopwatch.StartNew();
                bool algum = Editor.Revisitar();
                // Grades e paginas web: a cada 3s basta.
                if (_segundos % 3 == 0) { Grades.Revisitar(); Web.Revisitar(); algum = true; }
                if (algum) { swEd.Stop(); Contabilizar(swEd.ElapsedTicks); }

                if (!_censoFeito && _segundos >= 12 && Config.Sim(_cfg, "Censo")) { _censoFeito = true; Censo.Executar(); }
                Auditoria.Tick();
                if (_segundos == 30 || _segundos == 120 || _segundos % 600 == 0) Resumo("aos " + _segundos + "s");
            }
            catch (Exception ex) { Log.Linha("fase 2: tick FALHOU: " + Log.Curto(ex)); }
        }

        internal static void ProcessarJanela(Form f, string quem)
        {
            var sw = Stopwatch.StartNew();
            long antes = _controles;
            Processar(f);
            sw.Stop();
            Contabilizar(sw.ElapsedTicks);
            Log.Linha(string.Format("fase 2: janela nova {0} \"{1}\": {2} controles em {3:0.0} ms ({4})",
                f.GetType().Name, Curto(f.Text), _controles - antes, sw.Elapsed.TotalMilliseconds, quem));
        }

        private static void Contabilizar(long ticks)
        {
            _eventos++;
            _ticksTotal += ticks;
            if (ticks > _maiorEvento) _maiorEvento = ticks;
        }

        public static void Resumo(string quando)
        {
            double total = _ticksTotal * 1000.0 / Stopwatch.Frequency;
            double maior = _maiorEvento * 1000.0 / Stopwatch.Frequency;
            Log.Linha(string.Format("fase 2 DESEMPENHO {0}: {1} controles vistos, {2} alterados, {3} eventos, {4:0.0} ms no total, maior evento {5:0.0} ms",
                quando, _controles, _alterados, _eventos, total, maior));
            var top = _porTipo.OrderByDescending(kv => kv.Value).Take(12).Select(kv => kv.Key + "=" + kv.Value);
            Log.Linha("fase 2 alterados por tipo: " + string.Join(", ", top.ToArray()));
        }

        // ------------------------------------------------------------------
        // A Xpz.Dark so mexe em janelas da thread da interface (a que ligou a fase 2). Janela
        // que o GeneXus crie em OUTRA thread fica como esta: tocar nela daqui da o erro
        // "Cross-thread operation not valid" (relato de 28/10).
        private static int _threadUi;
        private static readonly HashSet<string> _outraThreadLogada = new HashSet<string>();

        internal static bool OutraThread(Control c)
        {
            try { return (_threadUi != 0 && System.Threading.Thread.CurrentThread.ManagedThreadId != _threadUi) || c.InvokeRequired; }
            catch { return true; }
        }

        internal static void Processar(Control c)
        {
            if (c == null) return;
            if (OutraThread(c))
            {
                string n = c.GetType().FullName;
                lock (_outraThreadLogada) if (!_outraThreadLogada.Add(n)) return;
                Log.Linha("janela de outra thread deixada como esta: " + n);
                return;
            }
            Marca m;
            if (_vistos.TryGetValue(c, out m)) return;
            _vistos.Add(c, new Marca());
            _controles++;
            TipoNovo(c);

            // Designer de tela (Web Layout): NAO e tema do IDE, e o desenho da tela
            // do usuario. Fica como o GeneXus desenha; so a moldura em volta escurece.
            if (Designer.Eh(c.GetType())) { try { Designer.Raiz(c); } catch { } return; }

            try { Pintar(c); } catch (Exception ex) { Disjuntor.Falhou(Parte.Janelas, ex); }

            // O GeneXus as vezes repoe a cor depois (ex.: combo de eventos volta a
            // preto). Reage so a cor que voltou a ser clara/escura neutra.
            if (!TemTemaProprio(c.GetType()))
            {
                try { c.BackColorChanged += AoMudarCor; c.ForeColorChanged += AoMudarCor; } catch { }
            }

            // Filho que chegar depois: por evento, sem varrer de novo.
            try { c.ControlAdded += AoAdicionar; } catch { }

            Control[] filhos;
            try { filhos = new Control[c.Controls.Count]; c.Controls.CopyTo(filhos, 0); }
            catch { return; }
            foreach (Control f in filhos) Processar(f);
        }

        private static void AoAdicionar(object sender, ControlEventArgs e)
        {
            try
            {
                var sw = Stopwatch.StartNew();
                Processar(e.Control);
                sw.Stop();
                Contabilizar(sw.ElapsedTicks);
            }
            catch { }
        }

        private static void AoMudarCor(object sender, EventArgs e)
        {
            if (_pintando) return;
            var c = sender as Control;
            Marca m;
            if (c == null || !_vistos.TryGetValue(c, out m)) return;
            bool fundo = Paleta.FundoClaro(c.BackColor), texto = Paleta.TextoEscuro(c.ForeColor);
            if (!fundo && !texto) return;           // o caso comum: cor herdada do pai, ja escura
            // Nao brigar sem fim com quem repoe a cor - mas o GeneXus repoe de novo a cada
            // objeto selecionado (Filter do Properties, 28/10): limite POR JANELA DE TEMPO.
            if ((DateTime.Now - m.Desde).TotalSeconds > 10) { m.Desde = DateTime.Now; m.Repinturas = 0; }
            if (m.Repinturas >= 25) return;
            m.Repinturas++;
            _pintando = true;
            try
            {
                if (fundo) c.BackColor = (c is Label && FundoEscuroAoRedor(c)) ? Color.Transparent : Paleta.FundoEscuroPara(c.BackColor);
                if (texto) c.ForeColor = Paleta.TextoClaroPara(c.ForeColor);
            }
            catch { }
            finally { _pintando = false; }
        }

        /// <summary>Tipo de controle visto pela primeira vez depois do censo: uma linha
        /// no log, para descobrir editores (variaveis, regras...) ainda nao mapeados.</summary>
        private static void TipoNovo(Control c)
        {
            Type t = c.GetType();
            if (!_tiposVistos.Add(t) || !_censoFeito || _tiposNovosLogados >= 300) return;
            _tiposNovosLogados++;
            string pai = c.Parent == null ? "-" : c.Parent.GetType().Name;
            Log.Linha(string.Format("tipo novo: {0} {1} (pai {2}) fundo={3} texto={4}",
                t.FullName, Censo.Familia(t), pai, Censo.Cor(c.BackColor), Censo.Cor(c.ForeColor)));
        }

        // -------------------------------------------- cor "de fabrica"
        private static readonly MethodInfo _serFundo = typeof(Control).GetMethod("ShouldSerializeBackColor", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly MethodInfo _serTexto = typeof(Control).GetMethod("ShouldSerializeForeColor", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        /// <summary>A cor foi DEFINIDA no controle (e nao herdada do pai)?</summary>
        internal static bool Explicito(Control c, bool fundo)
        {
            try
            {
                MethodInfo m = fundo ? _serFundo : _serTexto;
                if (m != null) return (bool)m.Invoke(c, null);
            }
            catch { }
            if (c.Parent == null) return true;
            return fundo ? c.BackColor.ToArgb() != c.Parent.BackColor.ToArgb() : c.ForeColor.ToArgb() != c.Parent.ForeColor.ToArgb();
        }

        internal static void GuardarOriginal(Control c)
        {
            Original o;
            if (_originais.TryGetValue(c, out o)) return;
            // Cor herdada: o que o PAI mostrava de fabrica (o pai ja pode estar escuro).
            Color fundo = Explicito(c, true) || c.Parent == null ? c.BackColor : FundoDeFabrica(c.Parent);
            Color texto = Explicito(c, false) || c.Parent == null ? c.ForeColor : TextoDeFabrica(c.Parent);
            _originais.Add(c, new Original { Fundo = fundo, Texto = texto });
        }

        /// <summary>A cor que o controle mostraria sem a Xpz.Dark.</summary>
        internal static Color FundoDeFabrica(Control c)
        {
            for (Control p = c; p != null; p = p.Parent)
            {
                Original o;
                if (_originais.TryGetValue(p, out o)) return o.Fundo;
                if (Explicito(p, true)) return p.BackColor;
            }
            return SystemColors.Control;
        }

        internal static Color TextoDeFabrica(Control c)
        {
            for (Control p = c; p != null; p = p.Parent)
            {
                Original o;
                if (_originais.TryGetValue(p, out o)) return o.Texto;
                if (Explicito(p, false)) return p.ForeColor;
            }
            return SystemColors.ControlText;
        }

        internal static void MarcarVisto(Control c)
        {
            Marca m;
            if (!_vistos.TryGetValue(c, out m)) { _vistos.Add(c, new Marca()); _controles++; }
        }

        internal static bool JaVisto(Control c) { Marca m; return _vistos.TryGetValue(c, out m); }

        internal static bool FundoWebLayout { get { return _fundoWebLayout; } }

        private static bool FundoEscuroAoRedor(Control c)
        {
            return c.Parent != null && c.Parent.BackColor.A == 255 && Paleta.Lum(c.Parent.BackColor) < 0.35;
        }

        /// <summary>Familia que tem tema proprio (o .isl, o editor) - nao mexer na cor.</summary>
        private static bool TemTemaProprio(Type t)
        {
            for (Type b = t; b != null; b = b.BaseType)
            {
                string n = b.FullName ?? "";
                if (n.StartsWith("Infragistics.", StringComparison.Ordinal) ||
                    n.StartsWith("ActiproSoftware.", StringComparison.Ordinal) ||
                    n.StartsWith("DevExpress.", StringComparison.Ordinal) ||
                    n == "System.Windows.Forms.WebBrowser")
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Escurece um controle: percorre os adaptadores (src\Nucleo\Adaptadores.cs) na ordem
        /// das etapas. O primeiro "Exclusivo" que atender trata o controle sozinho; controles
        /// de biblioteca com tema proprio param antes da regra geral (o .isl cuida deles).
        /// </summary>
        private static void Pintar(Control c)
        {
            if (!Disjuntor.Ligada(Parte.Janelas)) return;
            var lista = Adaptadores;

            foreach (IAdaptador a in lista)
                if (a.Etapa == Etapa.Exclusivo && Atende(a, c)) { if (Executar(a, c)) Contar(c); return; }
            foreach (IAdaptador a in lista)
                if (a.Etapa == Etapa.AntesDoTema && Atende(a, c)) Executar(a, c);

            if (TemTemaProprio(c.GetType())) return;
            GuardarOriginal(c);
            bool mudou = false;

            foreach (IAdaptador a in lista)
                if (a.Etapa == Etapa.Especial && Atende(a, c)) mudou |= Executar(a, c);

            // --- regra geral: so cor padrao/neutra
            if (Paleta.FundoClaro(c.BackColor)) { c.BackColor = Paleta.FundoEscuroPara(c.BackColor); mudou = true; }
            if (Paleta.TextoEscuro(c.ForeColor)) { c.ForeColor = Paleta.TextoClaroPara(c.ForeColor); mudou = true; }

            // --- o que depende do handle de janela do Windows
            foreach (IAdaptador a in lista)
                if (a.Etapa == Etapa.Janela && Atende(a, c)) mudou |= Executar(a, c);

            if (mudou) Contar(c);
        }

        private static void Contar(Control c)
        {
            _alterados++;
            string n = c.GetType().Name;
            int x; _porTipo.TryGetValue(n, out x); _porTipo[n] = x + 1;
        }

        private static string Curto(string s) { s = s ?? ""; return s.Length > 40 ? s.Substring(0, 40) + "..." : s; }
    }
}
