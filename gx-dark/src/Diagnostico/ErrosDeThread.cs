// Xpz.Dark - registro dos erros "Cross-thread operation not valid" (diagnostico).

using System;
using System.Diagnostics;
using System.Runtime.ExceptionServices;

namespace Xpz.Dark
{
    /// <summary>
    /// O GeneXus as vezes mostra "Cross-thread operation not valid: Control '' accessed from a
    /// thread other than the thread it was created on" (relato de 28/10). Para saber SE a
    /// Xpz.Dark esta envolvida, grava no dark.log a pilha de chamadas no momento em que a
    /// excecao nasce - antes de qualquer try/catch, do GeneXus ou nosso - e diz se ha codigo
    /// da Xpz.Dark nela. So le: nao muda o tratamento da excecao.
    /// </summary>
    internal static class ErrosDeThread
    {
        private const int Limite = 10;   // por sessao: o suficiente para diagnosticar, sem inundar o log
        private static int _gravados;
        [ThreadStatic] private static bool _dentro;

        public static void Ligar()
        {
            try { AppDomain.CurrentDomain.FirstChanceException += AoNascer; }
            catch (Exception ex) { Log.Linha("erros de thread: registro NAO ligado: " + Log.Curto(ex)); }
        }

        private static void AoNascer(object sender, FirstChanceExceptionEventArgs e)
        {
            if (_dentro || _gravados >= Limite) return;
            var ex = e.Exception as InvalidOperationException;
            if (ex == null) return;
            _dentro = true;
            try
            {
                // A mensagem e traduzida pelo .NET; a origem nao: Control.get_Handle /
                // CheckForIllegalCrossThreadCalls.
                string pilha = new StackTrace(1, false).ToString();
                if (pilha.IndexOf("System.Windows.Forms.Control.get_Handle", StringComparison.Ordinal) < 0 &&
                    ex.Message.IndexOf("thread", StringComparison.OrdinalIgnoreCase) < 0) return;
                _gravados++;
                bool nossa = pilha.IndexOf("Xpz.Dark.", StringComparison.Ordinal) >= 0;
                Log.Linha(string.Format("ERRO DE THREAD {0}/{1} (thread {2}{3}) - {4}: {5}\r\n{6}",
                    _gravados, Limite, System.Threading.Thread.CurrentThread.ManagedThreadId,
                    System.Windows.Forms.Application.MessageLoop ? ", com laco de mensagens" : ", SEM laco de mensagens",
                    nossa ? "A PILHA PASSA PELA XPZ.DARK" : "a pilha NAO passa pela Xpz.Dark",
                    ex.Message, pilha));
            }
            catch { }
            finally { _dentro = false; }
        }
    }
}
