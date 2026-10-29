// Xpz.Dark - registro das grades com cores proprias (VisualHint, Infragistics, DevExpress).
// Cada biblioteca tem a sua parte em src/Adaptadores/<biblioteca>/.

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
    // =====================================================================
    //  Grades com cores proprias
    // =====================================================================
    internal static partial class Grades
    {
        private static readonly List<WeakReference> _vivas = new List<WeakReference>();

        public static bool EhSmart(Control c) { return Familia.Deriva(c.GetType(), "VisualHint.SmartPropertyGrid.PropertyGrid"); }
        public static bool EhTreeList(Control c) { return Familia.Deriva(c.GetType(), "DevExpress.XtraTreeList.TreeList"); }
        public static bool EhUltraGrid(Control c) { return Familia.Deriva(c.GetType(), "Infragistics.Win.UltraWinGrid.UltraGrid"); }

        /// <summary>Controle Infragistics com barra de rolagem propria (UltraTree do KB
        /// Explorer, UltraListView...). A UltraGrid tem a sua dentro do DisplayLayout.</summary>
        public static bool TemRolagemIg(Control c)
        {
            return !EhUltraGrid(c) && Familia.DerivaDePrefixo(c.GetType(), "Infragistics.") && c.GetType().GetProperty("ScrollBarLook") != null;
        }

        public static void Registrar(Control c)
        {
            if (!Disjuntor.Ligada(Parte.Grades)) return;
            _vivas.Add(new WeakReference(c));
            Aplicar(c);
            c.VisibleChanged += delegate { if (c.Visible) Aplicar(c); };
            if (EhUltraGrid(c)) AoReceberDados(c);
        }

        /// <summary>
        /// A GxUltraGrid poe a aparencia BRANCA de novo quando recebe os dados (SetDefaultStyle
        /// no InitializeLayout, depois de a Xpz.Dark ja ter passado): o History abria branco
        /// e escurecia so no proximo tick (~1 s). O InitializeLayout dela foi assinado antes,
        /// no construtor - o nosso roda depois, antes do primeiro desenho com os dados.
        /// </summary>
        private static void AoReceberDados(Control c)
        {
            EventInfo ev = c.GetType().GetEvent("InitializeLayout");
            if (ev == null) return;
            EventHandler h = delegate { Aplicar(c); };
            ev.AddEventHandler(c, Delegate.CreateDelegate(ev.EventHandlerType, h.Target, h.Method));
        }

        /// <summary>O GeneXus pode repor as cores (ao trocar o objeto selecionado):
        /// confere de tempos em tempos; so grava o que mudou, sem repintar a toa.</summary>
        public static void Revisitar()
        {
            for (int i = _vivas.Count - 1; i >= 0; i--)
            {
                var c = _vivas[i].Target as Control;
                if (c == null || c.IsDisposed) { _vivas.RemoveAt(i); continue; }
                if (c.Visible) Aplicar(c);
            }
        }

        private static void Aplicar(Control c)
        {
            if (!Disjuntor.Ligada(Parte.Grades)) return;
            try
            {
                bool mudou = EhSmart(c) ? Smart(c) : EhUltraGrid(c) ? UltraGrid(c) : TemRolagemIg(c) ? Rolagem(((dynamic)c).ScrollBarLook) :
                             EhCaixaDx(c) ? CaixaDx(c) : EhPainelDx(c) ? PainelDx(c) : TreeList(c);
                if (mudou) c.Invalidate(true);
            }
            catch (Exception ex) { Disjuntor.Falhou(Parte.Grades, ex); }
        }

        private static bool Cor(object alvo, string prop, Color v)
        {
            PropertyInfo p = alvo.GetType().GetProperty(prop, BindingFlags.Public | BindingFlags.Instance);
            if (p == null || !p.CanWrite || p.PropertyType != typeof(Color)) return false;
            if (Familia.Igual((Color)p.GetValue(alvo, null), v)) return false;
            p.SetValue(alvo, v, null);
            return true;
        }

    }
}
