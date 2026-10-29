// Xpz.Dark - barra de status antiga (System.Windows.Forms.StatusBar).

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
    //  Barra de status (System.Windows.Forms.StatusBar, a antiga)
    // =====================================================================
    /// <summary>
    /// GxStatusBar deriva da StatusBar antiga: controle nativo, ignora BackColor. Sem o
    /// tema visual do Windows ela aceita SB_SETBKCOLOR; o texto dos paineis passa a ser
    /// desenhado por nos (OwnerDraw), porque o nativo usa a cor de texto do sistema.
    /// </summary>
    internal static class BarraStatus
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
        [System.Runtime.InteropServices.DllImport("uxtheme.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr h, string a, string b);
        private const int SB_SETBKCOLOR = 0x2001;

        private static readonly List<WeakReference> _barras = new List<WeakReference>();

        public static void TrocarTema()
        {
            foreach (WeakReference w in _barras)
            {
                var sb = w.Target as StatusBar;
                if (sb == null || sb.IsDisposed || !sb.IsHandleCreated) continue;
                sb.BackColor = Paleta.FundoPainel;
                SendMessage(sb.Handle, SB_SETBKCOLOR, IntPtr.Zero, (IntPtr)ColorTranslator.ToWin32(Paleta.FundoPainel));
                sb.Invalidate();
            }
        }

        public static void Escurecer(StatusBar sb)
        {
            if (!Disjuntor.Ligada(Parte.BarraStatus)) return;
            _barras.Add(new WeakReference(sb));
            try
            {
                sb.BackColor = Paleta.FundoPainel;
                sb.ForeColor = Paleta.Texto;
                // A alca de redimensionar e desenhada clara pelo Windows; a janela continua
                // redimensionavel pelas bordas.
                sb.SizingGrip = false;
                Paineis(sb);
                sb.DrawItem += Desenhar;
                // Painel que o GeneXus acrescentar depois: confere ao redimensionar/repintar.
                sb.Resize += delegate { Paineis(sb); };
                EventHandler fundo = delegate
                {
                    SetWindowTheme(sb.Handle, "", "");
                    SendMessage(sb.Handle, SB_SETBKCOLOR, IntPtr.Zero, (IntPtr)ColorTranslator.ToWin32(Paleta.FundoPainel));
                    sb.Invalidate();
                };
                if (sb.IsHandleCreated) fundo(sb, EventArgs.Empty);
                sb.HandleCreated += fundo;
                Log.Linha("barra de status escurecida (" + sb.Panels.Count + " painel(is), ShowPanels=" + sb.ShowPanels + ")");
            }
            catch (Exception ex) { Log.Linha("barra de status FALHOU: " + Log.Curto(ex)); }
        }

        private static void Paineis(StatusBar sb)
        {
            foreach (StatusBarPanel p in sb.Panels)
            {
                if (p.Style != StatusBarPanelStyle.OwnerDraw) p.Style = StatusBarPanelStyle.OwnerDraw;
                if (p.BorderStyle != StatusBarPanelBorderStyle.None) p.BorderStyle = StatusBarPanelBorderStyle.None;
            }
        }

        private static void Desenhar(object sender, StatusBarDrawItemEventArgs e)
        {
            if (!Disjuntor.Ligada(Parte.BarraStatus))
            {
                // parte desligada: os paineis continuam OwnerDraw - texto simples do sistema
                TextRenderer.DrawText(e.Graphics, e.Panel.Text, e.Font, e.Bounds, SystemColors.ControlText, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
                return;
            }
            try
            {
                Rectangle r = e.Bounds;
                using (var b = new SolidBrush(Paleta.FundoPainel)) e.Graphics.FillRectangle(b, r);
                int x = r.X + 2;
                if (e.Panel.Icon != null)
                {
                    int lado = Math.Min(16, r.Height - 2);
                    e.Graphics.DrawIcon(e.Panel.Icon, new Rectangle(x, r.Y + (r.Height - lado) / 2, lado, lado));
                    x += lado + 3;
                }
                var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
                if (e.Panel.Alignment == HorizontalAlignment.Right) flags |= TextFormatFlags.Right;
                else if (e.Panel.Alignment == HorizontalAlignment.Center) flags |= TextFormatFlags.HorizontalCenter;
                TextRenderer.DrawText(e.Graphics, e.Panel.Text, e.Font, new Rectangle(x, r.Y, r.Right - x - 2, r.Height), Paleta.Texto, flags);
                // separador discreto entre paineis
                using (var p = new Pen(Paleta.Borda)) e.Graphics.DrawLine(p, r.Right - 1, r.Y + 3, r.Right - 1, r.Bottom - 3);
            }
            catch (Exception ex) { Disjuntor.Falhou(Parte.BarraStatus, ex); }
        }
    }
}
