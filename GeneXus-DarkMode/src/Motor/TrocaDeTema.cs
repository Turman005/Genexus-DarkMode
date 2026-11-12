// GxDarkTheme - troca de tema ao vivo (GeneXus Next + 7 temas de itsFilipe), migracao do Xpz.Dark e modo seguro.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace GxDarkTheme
{
    public static partial class DarkEngine
    {
        public static event EventHandler TemaMudou;

        // le o tema gravado (tema=) antes de qualquer cor ser aplicada
        static void CarregarTema()
        {
            CarregarPersonalizado();
            string id = SettingsValue("tema");
            Tema t = Temas.Achar(id) ?? Temas.Next;
            if (id != null && Temas.Achar(id) == null) Log("Tema desconhecido no arquivo de configuracao (tema=" + id + "); usando GeneXus Next");
            TemaAtual = t;
            AplicarPaleta();
            Log("Tema: " + t.Nome + (t.IsNext ? " (cores originais do GeneXus Next)" : ""));
        }

        // troca o tema com a IDE aberta: tudo vai ao claro (diario devolve os originais exatos), as cores sao
        // recalculadas e o escuro e reaplicado - dentro de um unico congelamento de pintura (sem piscar)
        public static void SetTheme(string id, bool persist)
        {
            Tema t = Temas.Achar(id);
            if (t == null) { Log("SetTheme: tema desconhecido " + id); return; }
            if (persist) SettingsWrite("tema", t.Id);
            if (t == TemaAtual) { RefreshThemeUi(); return; }
            Trocar(t, null);
        }

        // ---- "Meu tema": cores do usuario (chaves meu_* do GxDarkTheme.settings) ----
        public const string ChaveFundo = "meu_fundo", ChaveDestaque = "meu_destaque", PrefixoSintaxe = "meu_";

        static void CarregarPersonalizado()
        {
            try
            {
                Dictionary<string, Color> s = new Dictionary<string, Color>();
                foreach (string k in Temas.Tokens) { Color c; if (LerCor(SettingsValue(PrefixoSintaxe + k), out c)) s[k] = c; }
                Color f, d;
                if (!LerCor(SettingsValue(ChaveFundo), out f)) f = Temas.FundoPadrao;
                if (!LerCor(SettingsValue(ChaveDestaque), out d)) d = Temas.DestaquePadrao;
                Temas.ConfigurarPersonalizado(f, d, s);
            }
            catch (Exception ex) { Log("Meu tema (carregar): " + ex.Message); }
        }

        public static bool LerCor(string v, out Color c)
        {
            c = Color.Empty;
            if (string.IsNullOrEmpty(v)) return false;
            v = v.Trim().TrimStart('#');
            int x;
            if (v.Length != 6 || !int.TryParse(v, System.Globalization.NumberStyles.HexNumber, null, out x)) return false;
            c = Color.FromArgb(255, (x >> 16) & 255, (x >> 8) & 255, x & 255);
            return true;
        }

        public static string CorTexto(Color c) { return "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2"); }

        // grava as cores do "Meu tema" e, se ativar=true ou se ele ja for o tema em uso, aplica na IDE na hora
        public static void SalvarPersonalizado(Color fundo, Color destaque, Dictionary<string, Color> sintaxe, bool ativar)
        {
            SettingsWrite(ChaveFundo, CorTexto(fundo));
            SettingsWrite(ChaveDestaque, CorTexto(destaque));
            foreach (string k in Temas.Tokens) if (sintaxe.ContainsKey(k)) SettingsWrite(PrefixoSintaxe + k, CorTexto(sintaxe[k]));
            Tema t = Temas.Personalizado;
            MethodInvoker cfg = delegate { Temas.ConfigurarPersonalizado(fundo, destaque, sintaxe); };
            if (ativar) SettingsWrite("tema", t.Id);
            if (!ativar && TemaAtual != t) { cfg(); RefreshThemeUi(); return; }
            Trocar(t, cfg);
            Log("Meu tema salvo: fundo " + CorTexto(fundo) + ", destaque " + CorTexto(destaque));
        }

        // troca para o tema t; antes (opcional) muda os parametros do tema DEPOIS de a IDE voltar ao claro e ANTES de recalcular as cores
        static void Trocar(Tema t, MethodInvoker antes)
        {
            if (!started || toggling) { if (antes != null) antes(); TrocarCores(t); RefreshThemeUi(); return; }
            if (!On) { if (antes != null) antes(); TrocarCores(t); Log("Tema " + t.Nome + " escolhido (modo claro agora; vale ao ligar o escuro)"); RefreshThemeUi(); return; }
            toggling = true;
            Stopwatch sw = Stopwatch.StartNew();
            List<IntPtr> frozen = Freeze();
            try
            {
                lightSeen.Clear();
                On = false;
                RestoreLight();
                lightFullAt = passCount + 1;
                bool wasIn = inPass; inPass = false;
                try { LightPass(true); } finally { inPass = wasIn; }

                if (antes != null) antes();
                TrocarCores(t);

                lightSeen.Clear();
                ApplyGlobals(false);
                browserUrls.Clear();
                wasIn = inPass; inPass = false;
                try { Pass(); } finally { inPass = wasIn; }
                Safe("Navegadores (sub-frames)", delegate { CefAllFrames(DarkJs, false); });
            }
            catch (Exception ex) { Log("Trocar tema: " + ex.GetType().Name + ": " + ex.Message); }
            finally { Unfreeze(frozen); toggling = false; }
            ThemeSwitches++;
            LastToggleMs = sw.ElapsedMilliseconds;
            RefreshThemeUi();
            Log("Tema trocado ao vivo para " + t.Nome + " em " + sw.ElapsedMilliseconds + " ms (propriedades devolvidas=" + Journal.Restored + ")");
        }

        public static int ThemeSwitches;

        static void TrocarCores(Tema t)
        {
            TemaAtual = t;
            AplicarPaleta();
            Safe("Sintaxe do editor", EditorTheme.Rebuild);
            if (iconsInit) Safe("Icones (tema)", delegate { IconSwap.TrocarTema(t); });
        }

        static void RefreshThemeUi()
        {
            try { ToggleButton.RefreshAll(); } catch { }
            try { EventHandler h = TemaMudou; if (h != null) h(null, EventArgs.Empty); } catch { }
        }

        // ---- migracao unica do Xpz.Dark (extensao do itsFilipe): %LOCALAPPDATA%\Xpz.Dark\dark.ini ----
        static void MigrarXpzDark()
        {
            try
            {
                if (SettingsValue("migrado_xpz") != null) return;
                string ini = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Xpz.Dark\dark.ini");
                if (!File.Exists(ini)) return;
                bool novo = !File.Exists(SettingsPath());
                string tema = null, ativo = null;
                foreach (string raw in File.ReadAllLines(ini))
                {
                    string l = raw.Trim();
                    if (l.Length == 0 || l[0] == ';' || l[0] == '#' || l[0] == '[') continue;
                    int i = l.IndexOf('=');
                    if (i <= 0) continue;
                    string k = l.Substring(0, i).Trim(), v = l.Substring(i + 1).Trim();
                    int c = v.IndexOf(';'); if (c >= 0) v = v.Substring(0, c).Trim();
                    if (k.Equals("Tema", StringComparison.OrdinalIgnoreCase)) tema = v.ToLowerInvariant();
                    else if (k.Equals("Ativo", StringComparison.OrdinalIgnoreCase)) ativo = v.ToLowerInvariant();
                }
                if (SettingsValue("tema") == null && Temas.Achar(tema) != null) SettingsWrite("tema", tema);
                if (novo && ativo == "nao") SettingsWrite("enabled", "false");
                SettingsWrite("migrado_xpz", DateTime.Now.ToString("yyyy-MM-dd"));
                Log("Configuracao do Xpz.Dark migrada (" + ini + "): tema=" + (tema ?? "-") + ", ativo=" + (ativo ?? "-") + ". O dark.ini nao foi alterado.");
            }
            catch (Exception ex) { Log("Migracao do dark.ini: " + ex.Message); }
        }

        // ---- modo seguro: Shift segurado na abertura, ou 3 aberturas seguidas que nao chegaram ao fim ----
        static bool ModoSeguro()
        {
            try
            {
                if ((Control.ModifierKeys & Keys.Shift) == Keys.Shift)
                {
                    Log("MODO SEGURO: Shift segurado na abertura do GeneXus; o tema escuro nao foi aplicado nesta sessao (nada foi gravado)");
                    return true;
                }
                int n;
                int.TryParse(SettingsValue("aberturas_incompletas") ?? "0", out n);
                if (n >= 3)
                {
                    SettingsWrite("aberturas_incompletas", "0");
                    Log("MODO SEGURO: as ultimas " + n + " aberturas do GeneXus nao completaram com o tema; esta sessao abre no modo CLARO (o botao sol/lua religa)");
                    forcarClaro = true;
                    return false;
                }
                SettingsWrite("aberturas_incompletas", (n + 1).ToString());
                Timer ok = new Timer { Interval = 45000 };
                ok.Tick += delegate { ok.Stop(); ok.Dispose(); SettingsWrite("aberturas_incompletas", "0"); };
                ok.Start();
            }
            catch { }
            return false;
        }
        static bool forcarClaro;
    }
}
