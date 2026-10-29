// Xpz.Dark - troca de tema ao vivo (parte da Fase2).

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
        // ------------------------------------------------- troca de tema AO VIVO
        /// <summary>
        /// Troca o tema sem reabrir o GeneXus: recarrega a moldura (.isl do tema), e nas
        /// janelas ja escurecidas troca cada cor do tema antigo pela correspondente do
        /// novo. Controle com a cor original guardada e recalculado a partir dela (exato).
        /// Editor, grades, paginas web, barra de status e menus leem a paleta nova.
        /// </summary>
        internal static void TrocarTema(Tema t)
        {
            if (!Disjuntor.Ligada(Parte.Temas)) return;
            try { TrocarTemaProtegido(t); }
            catch (Exception ex) { _pintando = false; Disjuntor.Falhou(Parte.Temas, ex); }
        }

        private static void TrocarTemaProtegido(Tema t)
        {
            var sw = Stopwatch.StartNew();
            Color[] antes = Paleta.Fotografia();
            Paleta.Personalizado = false;
            Paleta.DeTema(t);
            Color[] depois = Paleta.Fotografia();
            var mapa = new Dictionary<int, Color>();
            for (int i = 0; i < antes.Length; i++) if (!mapa.ContainsKey(antes[i].ToArgb())) mapa[antes[i].ToArgb()] = depois[i];

            Cores.CarregarIsl("troca de tema");
            int n = 0;
            _pintando = true;
            try
            {
                var janelas = new List<Form>();
                foreach (Form f in Application.OpenForms) if (!Fase2.OutraThread(f)) janelas.Add(f);
                foreach (Form f in janelas) n += Recolorir(f, mapa);
            }
            finally { _pintando = false; }

            try { Grades.Revisitar(); } catch { }
            try { Web.TrocarTema(); } catch { }
            try { BarraStatus.TrocarTema(); } catch { }
            try { Editor.TrocarTema(); } catch { }
            Repintar.Tudo("troca de tema");
            Log.Linha(string.Format("tema trocado para {0}: {1} controles recoloridos em {2} ms", t.Nome, n, sw.ElapsedMilliseconds));
        }

        private static int Recolorir(Control c, Dictionary<int, Color> mapa)
        {
            if (c == null || !JaVisto(c)) return 0;
            int n = 0;
            try
            {
                Original o;
                bool temOrig = _originais.TryGetValue(c, out o);
                Color nova;
                if (Explicito(c, true) && mapa.TryGetValue(c.BackColor.ToArgb(), out nova))
                {
                    if (temOrig && Paleta.FundoClaro(o.Fundo)) nova = Paleta.FundoEscuroPara(o.Fundo);
                    c.BackColor = nova; n = 1;
                }
                if (Explicito(c, false) && mapa.TryGetValue(c.ForeColor.ToArgb(), out nova))
                {
                    if (temOrig && Paleta.TextoEscuro(o.Texto)) nova = Paleta.TextoClaroPara(o.Texto);
                    c.ForeColor = nova; n = 1;
                }
                // casos especiais com cores proprias
                var pg = c as PropertyGrid; if (pg != null) PintarPropertyGrid(pg);
                var dgv = c as DataGridView; if (dgv != null) PintarGrade(dgv);
                var ll = c as LinkLabel; if (ll != null) { ll.LinkColor = Paleta.Link; ll.ActiveLinkColor = Paleta.Link; }
                var bt = c as ButtonBase;
                if (bt != null && bt.FlatStyle == FlatStyle.Flat) { bt.FlatAppearance.BorderColor = Paleta.Borda; bt.FlatAppearance.MouseOverBackColor = Paleta.Realce; bt.FlatAppearance.MouseDownBackColor = Paleta.Pressionado; }
                var tv = c as TreeView;
                if (tv != null && mapa.ContainsKey(tv.LineColor.ToArgb())) tv.LineColor = Paleta.Borda;
                if (c.GetType().FullName == "Crownwood.Magic.Controls.TabControl") PintarAbas(c);
                if (c is Form && _titulo && c.IsHandleCreated) CorDoTitulo(c);
                c.Invalidate();
            }
            catch { }
            Control[] filhos;
            try { filhos = new Control[c.Controls.Count]; c.Controls.CopyTo(filhos, 0); } catch { return n; }
            foreach (Control f in filhos) n += Recolorir(f, mapa);
            return n;
        }
    }
}
