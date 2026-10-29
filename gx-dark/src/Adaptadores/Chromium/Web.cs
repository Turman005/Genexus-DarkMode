// Xpz.Dark - paginas Chromium: Output (Monaco), Start Page, Launchpad.

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
    //  Paginas Chromium: Output (Monaco), Start Page, Launchpad
    // =====================================================================
    internal static class Web
    {
        public static bool Output = true, Paginas = true;
        private static readonly List<WeakReference> _navegadores = new List<WeakReference>();
        private static string _script;

        public static bool Eh(Control c) { return Familia.Deriva(c.GetType(), "ChromiumBrowser.ChromiumBaseBrowser"); }

        public static void Registrar(Control c)
        {
            if ((!Output && !Paginas) || !Disjuntor.Ligada(Parte.Web)) return;
            _navegadores.Add(new WeakReference(c));
            try
            {
                EventInfo ev = c.GetType().GetEvent("DocumentCompleted");
                if (ev != null && ev.EventHandlerType == typeof(EventHandler))
                    ev.AddEventHandler(c, (EventHandler)delegate { Injetar(c); });
            }
            catch { }
            c.VisibleChanged += delegate { if (c.Visible) Injetar(c); };
            Injetar(c);
        }

        /// <summary>A pagina pode recarregar, e o Monaco termina de carregar depois
        /// do DocumentCompleted: reinjeta de tempos em tempos (o script e idempotente).</summary>
        public static void TrocarTema() { _script = null; Revisitar(); }

        public static void Revisitar()
        {
            for (int i = _navegadores.Count - 1; i >= 0; i--)
            {
                var c = _navegadores[i].Target as Control;
                if (c == null || c.IsDisposed) { _navegadores.RemoveAt(i); continue; }
                if (c.Visible) Injetar(c);
            }
        }

        private static void Injetar(Control c)
        {
            try
            {
                MethodInfo m = c.GetType().GetMethod("ExecuteJavaScriptAsync", new[] { typeof(string) });
                if (m != null) m.Invoke(c, new object[] { Script() });
            }
            catch { }   // navegador ainda nao inicializado: a proxima visita resolve
        }

        private static string Hex(Color c) { return string.Format("{0:x2}{1:x2}{2:x2}", c.R, c.G, c.B); }

        private static string Script()
        {
            if (_script != null) return _script;
            string fundo = "#" + Hex(Paleta.FundoCodigo);
            string monaco = !Output ? "" :
                "if (window.monaco && monaco.editor && monaco.editor.defineTheme) {" +
                "  if (window.__xpzDark !== '" + Paleta.TemaAtual.Id + fundo + "') {" +
                "    monaco.editor.defineTheme('xpzDark', { base: 'vs-dark', inherit: true, rules: [" +
                "      { token: 'custom-info', foreground: '9d9d9d' }," +
                "      { token: 'custom-error', foreground: 'f48771', fontStyle: 'bold' }," +
                "      { token: 'custom-warning', foreground: 'e2c08d' }," +
                "      { token: 'custom-infomsg', foreground: '89d185' }," +
                "      { token: 'start-section', foreground: '6a9955' } ]," +
                "      colors: { 'editor.background': '" + fundo + "', 'editorGutter.background': '" + fundo + "'," +
                "                'minimap.background': '" + fundo + "', 'editor.foreground': '#" + Hex(Paleta.Texto) + "' } });" +
                "    window.__xpzDark = '" + Paleta.TemaAtual.Id + fundo + "'; }" +
                "  monaco.editor.setTheme('xpzDark');" +
                "  document.documentElement.style.background = '" + fundo + "';" +
                "  if (document.body) document.body.style.background = '" + fundo + "';" +
                "  return; }";
            // Pagina comum (Start Page, Launchpad): inverte a luminosidade da pagina
            // inteira e devolve imagens/videos ao normal. Sem mexer no HTML do GeneXus.
            string paginas = !Paginas ? "" :
                "if (!document.getElementById('xpz-dark') && document.documentElement) {" +
                "  var s = document.createElement('style'); s.id = 'xpz-dark';" +
                "  s.textContent = 'html{background:#fff !important;filter:invert(.88) hue-rotate(180deg)}' +" +
                "    'img,svg image,video,picture,canvas,iframe,[style*=\"background-image\"]{filter:invert(1) hue-rotate(180deg)}';" +
                "  (document.head || document.documentElement).appendChild(s); }";
            return _script = "(function(){try{" + monaco + paginas + "}catch(e){}})();";
        }
    }
}
