// Xpz.Dark - ROBUSTEZ: o que impede a extensao de atrapalhar o GeneXus.
//
//   ModoSeguro       segurar Shift ao abrir o GeneXus, ou a abertura anterior nao ter
//                    terminado (o GeneXus fechou no meio da subida), faz a extensao
//                    NAO agir nesta sessao. So o menu/tela de configuracao ficam.
//   Disjuntor        cada PARTE (abas, grades, editor...) que falhar repetidas vezes
//                    se desliga sozinha nesta sessao, com registro no log - em vez de
//                    insistir em cada desenho.
//   Compatibilidade  na abertura, confere cada detalhe interno de que a extensao
//                    depende (tipos, campos privados, eventos, arquivos, funcoes do
//                    Windows). O que faltar desliga SO a parte afetada e aparece na tela
//                    de configuracao. Numa versao nova do GeneXus, e aqui que se ve o
//                    que mudou.
//
// Tudo isto e a prova de falha: um erro aqui dentro nunca derruba o GeneXus.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Xpz.Dark
{
    /// <summary>Nomes das partes (as mesmas chaves no Disjuntor e na Compatibilidade).</summary>
    internal static class Parte
    {
        public const string Moldura = "moldura", Icones = "icones", Menu = "menu", MenuAbas = "menu das abas",
            Janelas = "janelas", Abas = "abas das partes", Grades = "grades", Editor = "editor", Web = "paginas web",
            BarraStatus = "barra de status", Combo = "combos", Rolagem = "rolagens", Gancho = "janela antes de mostrar",
            MenusNativos = "menus nativos", Auditoria = "auditoria", Temas = "troca de tema", Foco = "contorno de foco",
            Comparador = "comparador de versoes";
    }

    // =====================================================================
    //  Modo seguro
    // =====================================================================
    internal static class ModoSeguro
    {
        public static bool Ativo { get; private set; }
        public static string Motivo { get; private set; }

        private static string Marca { get { return Path.Combine(Config.Pasta(), "abrindo.marca"); } }

        /// <summary>
        /// Chamado no inicio do Initialize. A marca e gravada antes de qualquer coisa e
        /// apagada quando a abertura termina (SubidaConcluida) ou o GeneXus fecha
        /// normalmente. Marca sobrando = a abertura anterior morreu no meio.
        /// </summary>
        public static void Avaliar()
        {
            try
            {
                string outro = OutroTemaEscuro();
                if (outro != null)
                    Ligar("outro tema escuro instalado nesta versao do GeneXus (" + outro + "). Os dois brigariam pelas mesmas cores; use um so");
                else if ((Control.ModifierKeys & Keys.Shift) == Keys.Shift)
                    Ligar("Shift pressionado ao abrir o GeneXus");
                else if (File.Exists(Marca))
                    Ligar("a abertura anterior não terminou (o GeneXus fechou durante a subida em " +
                          File.GetLastWriteTime(Marca).ToString("dd/MM HH:mm") + ")");

                if (Ativo) { Apagar(); return; }
                File.WriteAllText(Marca, DateTime.Now.ToString("o"));
                Application.ApplicationExit += delegate { Apagar(); };
            }
            catch (Exception ex) { Log.Linha("modo seguro: avaliacao FALHOU: " + Log.Curto(ex)); }
        }

        /// <summary>Outra extensao de tema escuro na pasta Packages desta instalacao (ex.: a
        /// GxDarkTheme do repositorio Genexus-DarkMode). null = nenhuma.</summary>
        internal static string OutroTemaEscuro()
        {
            try
            {
                string pacotes = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Packages");
                foreach (string nome in new[] { "GxDarkTheme.dll" })
                    if (File.Exists(Path.Combine(pacotes, nome))) return nome;
            }
            catch { }
            return null;
        }

        /// <summary>A abertura chegou ao fim sem problema: a proxima abre normal.</summary>
        public static void SubidaConcluida()
        {
            if (!Ativo && File.Exists(Marca)) { Apagar(); Log.Linha("abertura concluida (marca de seguranca apagada)"); }
        }

        private static void Ligar(string motivo)
        {
            Ativo = true;
            Motivo = motivo;
            Log.Linha("MODO SEGURO: " + motivo + " - a extensao nao vai agir nesta sessao (menu e configuracao continuam)");
        }

        private static void Apagar() { try { if (File.Exists(Marca)) File.Delete(Marca); } catch { } }
    }

    // =====================================================================
    //  Disjuntor por parte
    // =====================================================================
    internal static class Disjuntor
    {
        // Pintar janelas tolera mais: um controle isolado que recuse uma cor nao
        // justifica desligar o tema das janelas todas.
        private static readonly Dictionary<string, int> Limites = new Dictionary<string, int> { { Parte.Janelas, 50 } };
        private const int LimitePadrao = 5;

        private static readonly Dictionary<string, int> _falhas = new Dictionary<string, int>();
        private static readonly Dictionary<string, string> _desligadas = new Dictionary<string, string>();

        public static bool Ligada(string parte) { return !_desligadas.ContainsKey(parte); }

        public static void Falhou(string parte, Exception ex)
        {
            if (!Ligada(parte)) return;
            int n; _falhas.TryGetValue(parte, out n); _falhas[parte] = ++n;
            if (n == 1) Log.Linha(parte + ": falha (" + Log.Curto(ex) + ")");
            int limite; if (!Limites.TryGetValue(parte, out limite)) limite = LimitePadrao;
            if (n >= limite) Desligar(parte, n + " falhas; a ultima: " + Log.Curto(ex));
        }

        public static void Desligar(string parte, string motivo)
        {
            if (!Ligada(parte)) return;
            _desligadas[parte] = motivo;
            Log.Linha("PARTE DESLIGADA nesta sessao: " + parte + " - " + motivo);
        }

        /// <summary>Executa protegido. false = parte desligada ou falhou agora.</summary>
        public static bool Executar(string parte, Action a)
        {
            if (!Ligada(parte)) return false;
            try { a(); return true; }
            catch (Exception ex) { Falhou(parte, ex); return false; }
        }

        public static IEnumerable<KeyValuePair<string, string>> Desligadas { get { return _desligadas.ToList(); } }
    }

    // =====================================================================
    //  Checagem de compatibilidade
    // =====================================================================
    internal static class Compatibilidade
    {
        internal sealed class Item { public string Parte, Descricao; public Func<bool> Teste; public bool Ok; }

        public static readonly List<Item> Itens = new List<Item>();
        public static int Total { get { return Itens.Count; } }
        public static int Aprovados { get { return Itens.Count(i => i.Ok); } }

        private const BindingFlags Inst = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags Est = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi)] private static extern IntPtr LoadLibrary(string nome);
        [DllImport("kernel32.dll")] private static extern IntPtr GetProcAddress(IntPtr mod, IntPtr ordinal);

        [StructLayout(LayoutKind.Sequential)]
        private struct OSVERSIONINFOEX { public int Tamanho, Major, Minor, Build, Plataforma; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Servico; public ushort SpMaior, SpMenor, Suite; public byte Produto, Reservado; }
        [DllImport("ntdll.dll")] private static extern int RtlGetVersion(ref OSVERSIONINFOEX v);

        /// <summary>Build REAL do Windows. Environment.OSVersion mente ("Windows 8", build
        /// 9200) para programas sem manifesto de compatibilidade com o Windows 10 - pego
        /// pelo TesteRobustez em 28/10. RtlGetVersion nao passa por essa camada.</summary>
        internal static int BuildDoWindows()
        {
            try
            {
                var v = new OSVERSIONINFOEX { Tamanho = Marshal.SizeOf(typeof(OSVERSIONINFOEX)) };
                if (RtlGetVersion(ref v) == 0) return v.Build;
            }
            catch { }
            return Environment.OSVersion.Version.Build;
        }

        private static Type T(string tipo, string assembly)
        {
            return Type.GetType(tipo + ", " + assembly, false);
        }

        private static bool Campo(string tipo, string asm, string nome, bool estatico = false)
        {
            Type t = T(tipo, asm);
            return t != null && t.GetField(nome, estatico ? Est : Inst) != null;
        }

        private static bool Prop(string tipo, string asm, string nome)
        {
            Type t = T(tipo, asm);
            return t != null && t.GetProperty(nome, Inst) != null;
        }

        private static bool Evento(string tipo, string asm, string nome)
        {
            Type t = T(tipo, asm);
            return t != null && t.GetEvent(nome) != null;
        }

        private static bool Ordinal(string dll, int ordinal)
        {
            IntPtr m = LoadLibrary(dll);
            return m != IntPtr.Zero && GetProcAddress(m, (IntPtr)ordinal) != IntPtr.Zero;
        }

        private static string Gx(string arquivo) { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, arquivo); }

        private static void A(string parte, string desc, Func<bool> teste) { Itens.Add(new Item { Parte = parte, Descricao = desc, Teste = teste }); }

        private static void Montar()
        {
            const string IgWin = "Infragistics2.Win.v11.1", IgTb = "Infragistics2.Win.UltraWinToolbars.v11.1", IgMdi = "Infragistics2.Win.UltraWinTabbedMdi.v11.1",
                         IgGrid = "Infragistics2.Win.UltraWinGrid.v11.1", Magic = "MagicLocalLibrary", Acti = "ActiproSoftware.SyntaxEditor.Net20",
                         ActiUi = "ActiproSoftware.WinUICore.Net20", Spg = "SmartPropertyGrid", DxTree = "DevExpress.XtraTreeList.v7.3", Fw = "Artech.FrameworkDE";

            A(Parte.Moldura, ".isl original do GeneXus (GenexusOffice2010Silver.isl)", () => File.Exists(Gx("GenexusOffice2010Silver.isl")));
            A(Parte.Moldura, "Infragistics StyleManager.Load(Stream)", () => { Type t = T("Infragistics.Win.AppStyling.StyleManager", IgWin); return t != null && t.GetMethod("Load", new[] { typeof(Stream) }) != null; });
            A(Parte.Moldura, "tabela de cor global Office2010", () => { Type t = T("Infragistics.Win.Office2010ColorTable", IgWin); return t != null && t.GetProperty("ColorScheme", Est) != null; });
            A(Parte.Icones, "icones da moldura (gerenciador de recursos de Images)", () => { Type t = T("Artech.FrameworkDE.Resources.Images", Fw); return t != null && Icones.CampoDoGerenciador(t) != null; });
            A(Parte.Menu, "menu Tools (UltraToolbarsManager.ToolClick)", () => Evento("Infragistics.Win.UltraWinToolbars.UltraToolbarsManager", IgTb, "ToolClick"));
            A(Parte.Menu, "itens com marca (StateButtonTool)", () => T("Infragistics.Win.UltraWinToolbars.StateButtonTool", IgTb) != null);
            A(Parte.MenuAbas, "menu das abas (InitializeContextMenu)", () => Evento("Infragistics.Win.UltraWinTabbedMdi.UltraTabbedMdiManager", IgMdi, "InitializeContextMenu"));
            A(Parte.MenuAbas, "estilo do menu (IGContextMenu.Style)", () => Prop("Infragistics.Win.IGControls.IGContextMenu", IgWin, "Style"));
            foreach (string f in new[] { "_backLightLight", "_backLight", "_backDark", "_backDarkDark", "_backIDE", "_tabRects", "_positionAtTop" })
            {
                string campo = f;
                A(Parte.Abas, "abas Crownwood: campo " + campo, () => Campo("Crownwood.Magic.Controls.TabControl", Magic, campo));
            }
            A(Parte.Abas, "abas Crownwood: TabPage.ColorTab", () => Prop("Crownwood.Magic.Controls.TabPage", Magic, "ColorTab"));
            A(Parte.Editor, "editor Actipro: renderizador VS2005", () => { Type t = T("ActiproSoftware.SyntaxEditor.VisualStudio2005SyntaxEditorRenderer", Acti); return t != null && t.GetProperty("TextAreaBackgroundFill") != null; });
            A(Parte.Editor, "editor Actipro: barras de rolagem sobrescreviveis", () => { Type t = T("ActiproSoftware.SyntaxEditor.VisualStudio2005SyntaxEditorRenderer", Acti); var m = t == null ? null : t.GetMethod("DrawScrollBarThumb"); return m != null && m.IsVirtual; });
            A(Parte.Editor, "barra de rolagem Actipro (ScrollBar.Renderer)", () => Prop("ActiproSoftware.WinUICore.ScrollBar", ActiUi, "Renderer"));
            A(Parte.Grades, "Properties (SmartPropertyGrid.GridBackColor)", () => Prop("VisualHint.SmartPropertyGrid.PropertyGrid", Spg, "GridBackColor"));
            A(Parte.Grades, "faixa de categoria (SetCategoryBackgroundColors)", () => { Type t = T("VisualHint.SmartPropertyGrid.LightColorDrawManager", Spg); return t != null && t.GetMethod("SetCategoryBackgroundColors") != null; });
            A(Parte.Grades, "Variables/Structure (DevExpress TreeList.Appearance)", () => Prop("DevExpress.XtraTreeList.TreeList", DxTree, "Appearance"));
            A(Parte.Grades, "grade de objetos (UltraGrid.DisplayLayout)", () => Prop("Infragistics.Win.UltraWinGrid.UltraGrid", IgGrid, "DisplayLayout"));
            A(Parte.Grades, "rolagem Infragistics (ScrollBarLook.ViewStyle)", () => Prop("Infragistics.Win.UltraWinScrollBar.ScrollBarLook", IgWin, "ViewStyle"));
            A(Parte.Web, "Output: tema do Monaco (OutputPane.html)", () => { string f = Gx(@"Monaco\OutputPane.html"); return File.Exists(f) && File.ReadAllText(f).Contains("monaco.editor.defineTheme"); });
            A(Parte.Web, "paginas Chromium (ExecuteJavaScriptAsync)", () => { Type t = T("ChromiumBrowser.ChromiumBaseBrowser", "ChromiumBrowser"); return t != null && t.GetMethod("ExecuteJavaScriptAsync", new[] { typeof(string) }) != null; });
            A(Parte.Comparador, "comparador: arvore de diferencas (lista, linhas e 3 desenhos)", () => { Type t = T("Artech.FrameworkDE.Comparer.StructDiffPane", Fw); return t != null && Comparador.Falta(t) == null; });
            A(Parte.MenusNativos, "Windows 10 1903+ (modo escuro de menus)", () => BuildDoWindows() >= 18362);
            A(Parte.MenusNativos, "uxtheme: SetPreferredAppMode/Refresh/Flush (#135/#104/#136)", () => Ordinal("uxtheme.dll", 135) && Ordinal("uxtheme.dll", 104) && Ordinal("uxtheme.dll", 136));
        }

        /// <summary>Roda tudo; o que falhar desliga a parte. Devolve o resumo para o log.</summary>
        public static string Verificar()
        {
            try
            {
                if (Itens.Count == 0) Montar();
                foreach (Item i in Itens)
                {
                    try { i.Ok = i.Teste(); } catch { i.Ok = false; }
                    if (!i.Ok) Disjuntor.Desligar(i.Parte, "compatibilidade: " + i.Descricao + " nao encontrado nesta versao");
                }
                string r = "compatibilidade: " + Aprovados + "/" + Total + " pontos ok";
                var falhas = Itens.Where(i => !i.Ok).Select(i => i.Descricao).ToArray();
                if (falhas.Length > 0) r += "; FALTANDO: " + string.Join("; ", falhas);
                Log.Linha(r);
                return r;
            }
            catch (Exception ex) { Log.Linha("compatibilidade: checagem FALHOU: " + Log.Curto(ex)); return "compatibilidade: erro na checagem"; }
        }
    }

    // =====================================================================
    //  Log com tamanho limitado
    // =====================================================================
    internal static class LogRotacao
    {
        /// <summary>Na abertura: dark.log acima de 1 MB vira dark.log.old (so o anterior fica).</summary>
        public static void Girar()
        {
            try
            {
                string log = Path.Combine(Config.Pasta(), "dark.log"), velho = log + ".old";
                if (File.Exists(log) && new FileInfo(log).Length > 1024 * 1024)
                {
                    if (File.Exists(velho)) File.Delete(velho);
                    File.Move(log, velho);
                }
            }
            catch { }
        }
    }
}
