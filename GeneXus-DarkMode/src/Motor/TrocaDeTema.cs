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
        // chaves: meu_fundo, meu_destaque, meu_<token de sintaxe>, meu_fixa_<area> (vazia = automatica), meu_out_<linha do Output> (vazia = padrao)
        public const string ChaveFundo = "meu_fundo", ChaveDestaque = "meu_destaque", PrefixoSintaxe = "meu_", PrefixoFixa = "meu_fixa_", PrefixoOutput = "meu_out_";

        public delegate string LeitorChave(string chave);

        // monta a escolha a partir de uma fonte de chaves (o settings ou um arquivo exportado)
        public static Escolha LerEscolha(LeitorChave ler)
        {
            Escolha e = new Escolha();
            Color c;
            e.Fundo = LerCor(ler(ChaveFundo), out c) ? c : Temas.FundoPadrao;
            e.Destaque = LerCor(ler(ChaveDestaque), out c) ? c : Temas.DestaquePadrao;
            foreach (string k in Temas.Tokens) if (LerCor(ler(PrefixoSintaxe + k), out c)) e.Sintaxe[k] = c;
            foreach (string[] a in Temas.Areas) if (LerCor(ler(PrefixoFixa + a[0]), out c) && CorFixaValida(a[3], c)) e.Fixas[a[0]] = c;
            foreach (string[] o in Temas.OutputTokens) if (LerCor(ler(PrefixoOutput + o[0]), out c)) e.Output[o[0]] = c;
            return e;
        }

        // todas as chaves da escolha (as automaticas/padrao saem vazias, para apagar o que havia antes)
        public static List<KeyValuePair<string, string>> ChavesDaEscolha(Escolha e)
        {
            List<KeyValuePair<string, string>> l = new List<KeyValuePair<string, string>>();
            l.Add(new KeyValuePair<string, string>(ChaveFundo, CorTexto(e.Fundo)));
            l.Add(new KeyValuePair<string, string>(ChaveDestaque, CorTexto(e.Destaque)));
            foreach (string k in Temas.Tokens) if (e.Sintaxe.ContainsKey(k)) l.Add(new KeyValuePair<string, string>(PrefixoSintaxe + k, CorTexto(e.Sintaxe[k])));
            foreach (string[] a in Temas.Areas) l.Add(new KeyValuePair<string, string>(PrefixoFixa + a[0], e.Fixas.ContainsKey(a[0]) ? CorTexto(e.Fixas[a[0]]) : ""));
            foreach (string[] o in Temas.OutputTokens) l.Add(new KeyValuePair<string, string>(PrefixoOutput + o[0], e.Output.ContainsKey(o[0]) ? CorTexto(e.Output[o[0]]) : ""));
            return l;
        }

        // fundo fixo precisa ser escuro e texto fixo precisa ser claro: o motor decide "claro x escuro" pela luminosidade,
        // e um fundo claro seria tratado como resto claro a escurecer de novo
        public static bool CorFixaValida(string tipo, Color c) { return tipo == "t" ? Lum(c) >= 150 : Lum(c) <= 100; }

        static void CarregarPersonalizado()
        {
            try { Temas.ConfigurarPersonalizado(LerEscolha(SettingsValue)); }
            catch (Exception ex) { Log("Meu tema (carregar): " + ex.Message); }
        }

        // exportar/importar: arquivo texto simples "chave=valor" (as mesmas chaves meu_* do settings)
        public static void ExportarEscolha(Escolha e, string arquivo)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("; " + Produto.NomeExibicao + " - Meu tema (importe em Montar meu tema > Importar)");
            foreach (KeyValuePair<string, string> kv in ChavesDaEscolha(e)) if (kv.Value.Length > 0) sb.AppendLine(kv.Key + "=" + kv.Value);
            File.WriteAllText(arquivo, sb.ToString(), new System.Text.UTF8Encoding(false));
        }

        public static Escolha ImportarEscolha(string arquivo)
        {
            Dictionary<string, string> d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string raw in File.ReadAllLines(arquivo))
            {
                string l = raw.Trim();
                if (l.Length == 0 || l[0] == ';' || l[0] == '#') continue;
                int i = l.IndexOf('=');
                if (i > 0) d[l.Substring(0, i).Trim()] = l.Substring(i + 1).Trim();
            }
            if (!d.ContainsKey(ChaveFundo)) throw new InvalidDataException("o arquivo não tem as cores de um Meu tema (falta " + ChaveFundo + ")");
            return LerEscolha(delegate(string k) { string v; return d.TryGetValue(k, out v) ? v : null; });
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
        public static void SalvarPersonalizado(Escolha escolha, bool ativar)
        {
            Escolha e = escolha.Copia();
            foreach (KeyValuePair<string, string> kv in ChavesDaEscolha(e)) SettingsWrite(kv.Key, kv.Value);
            Tema t = Temas.Personalizado;
            MethodInvoker cfg = delegate { Temas.ConfigurarPersonalizado(e); };
            if (ativar) SettingsWrite("tema", t.Id);
            if (!ativar && TemaAtual != t) { cfg(); RefreshThemeUi(); return; }
            Trocar(t, cfg);
            Log("Meu tema salvo: fundo " + CorTexto(e.Fundo) + ", destaque " + CorTexto(e.Destaque) + ", " + e.Fixas.Count + " cor(es) exata(s), " + e.Output.Count + " cor(es) do Output");
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
