// Xpz.Dark - EXPERIMENTO de dark mode para o IDE do GeneXus 18. So na U10.
//
// FASE 1 - a moldura (docking, abas, barras de titulo dos paineis).
//
// O que a varredura do IL (27/10) mostrou e que este codigo explora:
//
//   1. O DockManager do GeneXus forca WindowStyle = Office2007 no construtor. O
//      estilo Office2007 do Infragistics tira as cores de uma tabela GLOBAL e
//      estatica: Infragistics.Win.Office2007ColorTable.ColorScheme (idem 2010).
//      Em 20/08 o .isl com office2007ColorScheme="Black" foi carregado e a moldura
//      nao mudou - a tabela global nunca foi acionada direto. Aqui e.
//
//   2. Os icones da moldura (alfinete, fechar, setas e lista de abas) sao
//      desenhados A MAO pelos filtros de desenho do DockManager e do
//      TabbedMdiManager, e sao os de traco escuro que somem no fundo escuro. Eles
//      vem de dois lugares: campos estaticos (DockManager.PanePinned...) e de
//      Artech.FrameworkDE.Resources.Images, que pede cada icone ao ResourceManager
//      a cada desenho. O campo do ResourceManager e gravavel e so e criado se
//      estiver vazio - pomos o nosso no lugar, que devolve versoes claras.
//
//   3. Opcional: o .isl escuro de agosto, que escurecia parte dos paineis.
//
// Cada alavanca liga e desliga no dark.ini - comparar sem reinstalar.
//
// SEGURANCA
//   - Nao toca em KB. Nao abre, nao le, nao grava. So cor e icone de interface.
//   - Tudo em try/catch: isto roda na subida do IDE, e uma excecao aqui
//     derrubaria o GeneXus inteiro.
//   - DESLIGAR SEM DESINSTALAR: apagar %LOCALAPPDATA%\Xpz.Dark\dark.ini.
//   - O ResourceManager substituto devolve o ORIGINAL em qualquer falha propria.

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

using Artech.Architecture.Common.Packages;
using Artech.Architecture.Common.Services;
using Artech.Architecture.UI.Framework.Packages;
using Artech.Common.Framework.Commands;
using Artech.Common.Localization;

[assembly: Package(typeof(Xpz.Dark.DarkPackage))]
// Versao da extensao (aparece no log e na tela de configuracao).
[assembly: System.Reflection.AssemblyVersion("0.13.6.0")]
[assembly: System.Reflection.AssemblyFileVersion("0.13.6.0")]

namespace Xpz.Dark
{
    public class DarkPackage : AbstractPackageUI
    {
        public override string Name { get { return "Xpz.Dark"; } }

        /// <summary>Tem que bater com o id do manifesto GeneXusPackage.package.</summary>
        public static readonly Guid PackageGuid = new Guid("5d8c2e71-9a43-4f06-b2e8-1c7f3a90d4b6");

        private Timer _timer;
        private int _tick;
        private Dictionary<string, string> _cfg;

        public override void Initialize(IGxServiceProvider services)
        {
            base.Initialize(services);
            LogRotacao.Girar();
            // Antes de tudo: Shift pressionado ou abertura anterior interrompida = modo seguro.
            ModoSeguro.Avaliar();
            // Diagnostico do "Cross-thread operation not valid" (28/10): grava a pilha. Liga
            // tambem no modo seguro - comparar com a extensao parada diz se a culpa e dela.
            ErrosDeThread.Ligar();
            // A marca de "abrindo" sai quando a abertura termina - em qualquer caso (tema
            // desligado inclusive), senao a proxima abertura entraria em modo seguro a toa.
            var fimDaAbertura = new Timer { Interval = 20000 };
            fimDaAbertura.Tick += delegate { fimDaAbertura.Stop(); fimDaAbertura.Dispose(); ModoSeguro.SubidaConcluida(); };
            fimDaAbertura.Start();

            // O menu da tela de configuracao existe SEMPRE - inclusive com o tema
            // desligado, que e justamente quando se quer religar por ela.
            try { AddCommand(new CommandKey(PackageGuid, "XpzDarkConfig"), ExecConfig); }
            catch (Exception ex) { Log.Linha("comando de configuracao FALHOU: " + Log.Curto(ex)); }
            // O menu do manifesto nao aparece na U10: o item vai direto no menu Tools.
            try { MenuTema.Iniciar(); }
            catch (Exception ex) { Log.Linha("menu FALHOU: " + Log.Curto(ex)); }

            try
            {
                Log.Linha("==================== GeneXus subiu ====================");
                _cfg = Config.Ler();
                if (_cfg == null) { Log.Linha("sem dark.ini - experimento desligado"); return; }
                if (Config.Valor(_cfg, "Ativo") != null && !Config.Sim(_cfg, "Ativo")) { Log.Linha("Ativo = nao - tema desligado"); return; }
                if (ModoSeguro.Ativo) return;
                Log.Linha("config: " + string.Join("; ", _cfg.Select(kv => kv.Key + "=" + kv.Value).ToArray()));
                Log.Linha("GeneXus em " + AppDomain.CurrentDomain.BaseDirectory + ", Xpz.Dark " + typeof(DarkPackage).Assembly.GetName().Version);
                // O que nao existir nesta versao do GeneXus desliga so a parte afetada.
                Compatibilidade.Verificar();

                Cores.Retrato("antes de tudo");

                if (Config.Sim(_cfg, "ClarearIconesMoldura")) Disjuntor.Executar(Parte.Icones, Icones.Aplicar);
                // O tema decide a moldura (.isl gerado na hora) e as janelas.
                Paleta.Carregar(_cfg);
                Log.Linha("tema: " + Paleta.TemaAtual.Nome + (Paleta.Personalizado ? " (cores personalizadas)" : ""));
                if (Config.Sim(_cfg, "CarregarIsl")) Cores.CarregarIsl("Initialize");
                Cores.Aplicar(_cfg, "Initialize");

                // Fase 2: controles comuns do Windows. Tem timer proprio, porque
                // trabalha a sessao inteira (janelas novas a cada documento aberto).
                if (Config.SimPadrao(_cfg, "Fase2")) Fase2.Iniciar(_cfg);

                // Acompanhar a subida: o GeneXus carrega o .isl Silver DELE em algum
                // momento, e isso pode devolver a tabela global para Silver. Cada
                // tick confere, reaplica se precisar, e o log conta quem venceu.
                _timer = new Timer();
                _timer.Interval = 2000;
                _timer.Tick += delegate { Tick(); };
                _timer.Start();
            }
            catch (Exception ex) { Log.Linha("Initialize FALHOU: " + Log.Curto(ex)); }
        }

        private bool ExecConfig(CommandData data)
        {
            Form dono = Form.ActiveForm;
            if (dono == null) foreach (Form f in Application.OpenForms) { if (Fase2.OutraThread(f)) continue; dono = f; break; }
            TelaConfiguracao.Exibir(dono);
            return true;
        }

        private void Tick()
        {
            try
            {
                _tick++;
                bool mudou = Cores.Aplicar(_cfg, "tick " + _tick);
                if (mudou && Config.Sim(_cfg, "CarregarIsl"))
                {
                    // Se a tabela foi revertida, o .isl provavelmente tambem foi trocado.
                    Cores.CarregarIsl("tick " + _tick);
                    Cores.Aplicar(_cfg, "tick " + _tick + " (pos-isl)");
                }
                if (mudou || _tick == 1 || _tick == 4) Repintar.Tudo("tick " + _tick);

                // Uma vez, com a interface ja montada: perguntar aos objetos vivos
                // de onde vem a cor. Ausencia da chave = ligado nesta fase.
                // Diagnosticos: so com a chave explicitamente ligada (padrao de producao = desligado).
                if (_tick == 5 && Config.Sim(_cfg, "Diagnostico")) Diagnostico.Executar();

                if (_tick >= 15)
                {
                    _timer.Stop();
                    _timer.Dispose();
                    _timer = null;
                    Cores.Retrato("fim do acompanhamento (30s)");
                }
            }
            catch (Exception ex) { Log.Linha("tick FALHOU: " + Log.Curto(ex)); }
        }
    }
}
