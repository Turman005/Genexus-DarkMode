// Xpz.Dark - recursos do Windows: menus nativos escuros, barra de titulo, tema escuro
// por janela, e o gancho que escurece a janela antes de ela aparecer.

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
        // ------------------------------------------------- recursos do Windows
        // Menus de contexto NATIVOS do Windows (Win32) so ficam escuros se o processo
        // pedir: uxtheme ordinal 135 = SetPreferredAppMode (2 = ForceDark), 136 =
        // FlushMenuThemes. Sem nome exportado; existe desde o Windows 10 1903.
        // Medido 27/10 (TesteMenuNativo): sem o #104 = RefreshImmersiveColorPolicyState
        // o pedido nao pega e o menu continua branco; com ele, fica escuro.
        [DllImport("uxtheme.dll", EntryPoint = "#135")] private static extern int SetPreferredAppMode(int modo);
        [DllImport("uxtheme.dll", EntryPoint = "#104")] private static extern void RefreshImmersiveColorPolicyState();
        [DllImport("uxtheme.dll", EntryPoint = "#136")] private static extern void FlushMenuThemes();

        private static bool _menusNativos;
        internal static bool MenusNativos { get { return _menusNativos; } }

        private static void MenusNativosEscuros()
        {
            try
            {
                if (!Disjuntor.Ligada(Parte.MenusNativos)) return;   // a compatibilidade ja conferiu Windows e funcoes
                SetPreferredAppMode(2);
                RefreshImmersiveColorPolicyState();
                FlushMenuThemes();
                _menusNativos = true;
                Log.Linha("menus nativos do Windows: modo escuro pedido");
            }
            catch (Exception ex) { Log.Linha("menus nativos FALHOU: " + Log.Curto(ex)); }
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int valor, int tamanho);

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hwnd, string app, string lista);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr h, IntPtr depois, int x, int y, int cx, int cy, uint flags);

        private static void QuandoTiverHandle(Control c, Action<Control> acao)
        {
            if (c.IsHandleCreated) { try { acao(c); } catch { } return; }
            EventHandler h = null;
            h = delegate { c.HandleCreated -= h; try { acao(c); } catch { } };
            c.HandleCreated += h;
        }

        /// <summary>Barra de titulo escura (Windows 10 2004+ e 11). Atributo 20; 19 nos builds antigos.</summary>
        private static void TituloEscuro(Control c)
        {
            int v = 1;
            if (DwmSetWindowAttribute(c.Handle, 20, ref v, 4) != 0) DwmSetWindowAttribute(c.Handle, 19, ref v, 4);
            CorDoTitulo(c);
            // forca o Windows a redesenhar a moldura (SWP_NOMOVE|NOSIZE|NOZORDER|NOACTIVATE|FRAMECHANGED)
            SetWindowPos(c.Handle, IntPtr.Zero, 0, 0, 0, 0, 0x0002 | 0x0001 | 0x0004 | 0x0010 | 0x0020);
        }

        /// <summary>Windows 11: a barra de titulo na cor do tema (atributos 35 = fundo,
        /// 36 = texto). No Windows 10 o pedido e ignorado e fica o escuro padrao.</summary>
        private static void CorDoTitulo(Control c)
        {
            int fundo = ColorTranslator.ToWin32(Paleta.FundoEntrada), texto = ColorTranslator.ToWin32(Paleta.Texto);
            DwmSetWindowAttribute(c.Handle, 35, ref fundo, 4);
            DwmSetWindowAttribute(c.Handle, 36, ref texto, 4);
        }

        [DllImport("user32.dll", EntryPoint = "RedrawWindow")] private static extern bool RedesenharJanela(IntPtr h, IntPtr r, IntPtr rgn, uint flags);
        private const uint RDW_INVALIDATE = 0x1, RDW_FRAME = 0x400;

        // Janela (handle) que ja recebeu o tema escuro de rolagem, por controle. A revisita de
        // 3 s das grades pedia o tema de novo a cada passagem (WM_THEMECHANGED a toa) e, sem
        // handle, empilhava um HandleCreated novo por passagem (28/10).
        private static readonly ConditionalWeakTable<Control, StrongBox<IntPtr>> _rolagemFeita = new ConditionalWeakTable<Control, StrongBox<IntPtr>>();

        /// <summary>Barras de rolagem escuras (tema "DarkMode_Explorer" do proprio Windows):
        /// uma vez por janela; de novo se o GeneXus recriar a janela.</summary>
        internal static void RolagemEscuraQuandoPuder(Control c)
        {
            if (!_rolagem) return;
            StrongBox<IntPtr> feita;
            if (!_rolagemFeita.TryGetValue(c, out feita))
            {
                feita = new StrongBox<IntPtr>(IntPtr.Zero);
                _rolagemFeita.Add(c, feita);
                c.HandleCreated += delegate { RolagemEscura(c, feita); };
            }
            if (c.IsHandleCreated) RolagemEscura(c, feita);
        }

        private static void RolagemEscura(Control c, StrongBox<IntPtr> feita)
        {
            try
            {
                IntPtr h = c.Handle;
                if (feita.Value == h) return;
                feita.Value = h;
                SetWindowTheme(h, "DarkMode_Explorer", null);
                // Janela ja desenhada: a borda (onde a barra mora) nao se redesenha sozinha -
                // a barra do Properties do Work With Devices ficava clara (auditoria 28/10).
                RedesenharJanela(h, IntPtr.Zero, IntPtr.Zero, RDW_INVALIDATE | RDW_FRAME);
            }
            catch { }
        }

        internal static void TemaNativo(IntPtr h, string tema)
        {
            try { SetWindowTheme(h, tema, null); } catch { }
        }
    }

    // =====================================================================
    //  Janela nova escurecida antes do primeiro desenho
    // =====================================================================
    /// <summary>
    /// O timer de 1s achava a janela nova depois de ela ja aparecer branca (o
    /// "pisca branco" relatado em 27/10). Um gancho CBT da propria thread avisa no
    /// momento em que a janela e ATIVADA - antes do primeiro WM_PAINT. So olha
    /// janelas desta thread; custo: uma chamada por ativacao de janela.
    /// </summary>
    internal static class Gancho
    {
        private delegate IntPtr CbtProc(int codigo, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] private static extern IntPtr SetWindowsHookEx(int tipo, CbtProc proc, IntPtr mod, uint thread);
        [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr h, int codigo, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr h);
        [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();

        private const int WH_CBT = 5, HCBT_ACTIVATE = 5;
        private static CbtProc _proc;      // referencia viva: o GC nao pode recolher o delegate
        private static IntPtr _h;

        public static void Ligar()
        {
            try
            {
                _proc = Proc;
                _h = SetWindowsHookEx(WH_CBT, _proc, IntPtr.Zero, GetCurrentThreadId());
                Log.Linha(_h == IntPtr.Zero ? "gancho de janela nova NAO instalado" : "gancho de janela nova instalado (escurece antes de mostrar)");
                // O gancho TEM de sair antes de o .NET encerrar: uma ativacao de janela durante
                // o fechamento chamaria codigo gerenciado ja desmontado e o processo cairia
                // (0xC0020001 - pego pelo TesteMenusDx em 28/10, com um menu ainda aberto).
                Application.ApplicationExit += delegate { Desligar(); };
                AppDomain.CurrentDomain.ProcessExit += delegate { Desligar(); };
                AppDomain.CurrentDomain.DomainUnload += delegate { Desligar(); };
            }
            catch (Exception ex) { Log.Linha("gancho FALHOU: " + Log.Curto(ex)); }
        }

        public static void Desligar()
        {
            IntPtr h = _h;
            _h = IntPtr.Zero;
            if (h != IntPtr.Zero) { try { UnhookWindowsHookEx(h); } catch { } }
        }

        private static IntPtr Proc(int codigo, IntPtr w, IntPtr l)
        {
            if (Environment.HasShutdownStarted) return CallNextHookEx(IntPtr.Zero, codigo, w, l);
            if (codigo == HCBT_ACTIVATE && Disjuntor.Ligada(Parte.Gancho))
            {
                try
                {
                    var f = Control.FromHandle(w) as Form;
                    if (f != null && !Fase2.JaVisto(f)) Fase2.ProcessarJanela(f, "antes de mostrar");
                }
                catch (Exception ex) { Disjuntor.Falhou(Parte.Gancho, ex); }
            }
            return CallNextHookEx(_h, codigo, w, l);
        }
    }
}
