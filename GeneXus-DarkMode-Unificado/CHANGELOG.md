# Changelog

## 2.0.0, tela de configurações revisada (DLL e Setup 2.0.2.0, 2026-10-29)

Só a tela Ferramentas > Tema escuro > Configurações mudou. O resto da extensão e o instalador ficaram iguais.

- **Sem textos cortados:**
  - Os textos quebram linha: a descrição do tema, as opções e o rodapé.
  - As opções agora têm uma caixa de marcar ao lado de um texto que quebra linha. Clicar no texto também marca e desmarca.
  - Os botões ficam do tamanho do texto: "Abrir o log", "Pasta das configurações" e "Fechar".
- **Janela redimensionável:** o tamanho inicial e o mínimo são calculados pela fonte, então nada fica cortado em 100, 125, 150 e 200%. Se a janela ficar estreita demais, aparece rolagem.
- **Autores:** nova seção "Autores", com um nome por linha: Gabriel Turman e Filipe Castro.
- Cores, comportamento e opções iguais. A tela continua seguindo o tema ativo.

## 2.0.0, instalador revisado (Setup 2.0.1.0, 2026-10-29)

O instalador ganhou layout responsivo e uma página de progresso padrão. A extensão (GxDarkTheme.dll) e a lógica de instalação não mudaram.

**Layout**
- A janela pode ser redimensionada e maximizada, com tamanho mínimo.
- Os campos se ajustam à largura e os textos quebram linha em vez de ser cortados.
- A lista de instalações dá à coluna Pasta o espaço que sobra.
- Escala por monitor (PerMonitorV2): sem texto cortado em 100, 125, 150 e 200%.

**Progresso**
- Página de progresso padrão:
  - linha de status ("Copiando arquivos...", "Registrando no GeneXus...");
  - barra de progresso real por etapas, com barra animada durante o `GeneXus.exe /install`;
  - porcentagem;
  - registro escondido atrás de "Mostrar detalhes".
- O Cancelar só fica ativo entre as instalações do GeneXus. Ele para depois da instalação em andamento, sem deixar nada pela metade.
- Página "Concluído" com o botão Concluir. O mesmo vale para o desinstalador.

**Correção**
- O tema já configurado volta marcado na página "Tema padrão", mesmo quando a configuração tem um valor vazio.

## 2.0.0 (2026-10-29): GxDarkTheme + Xpz.Dark unificados

### Novidades

**Temas**
- Os **8 temas** ficam numa extensão só: o GeneXus Next (padrão) e os 7 temas do Xpz.Dark.
  - Para escolher: menu Ferramentas > Tema escuro, ou botão direito no sol/lua.
  - **A troca é ao vivo**, sem reiniciar o GeneXus.
- O `.isl` (estilo das barras e menus) agora é **gerado em memória** a partir do `GenexusOffice2010Silver.isl` da instalação.
  - Nenhum `.isl` é gravado na pasta Packages.
  - No tema Next, o resultado é idêntico ao `.isl` da v1.

**Ícones**
- Os ícones do GeneXus Next aparecem em **todos** os temas, tingidos com a matiz do tema.
- O tema Next mantém os pixels da v1.
- As cores do editor (sintaxe), do Output, das páginas web, dos relatórios etc. seguem a paleta do tema, sem inversão de cores.

**Configuração**
- Nova janela de **Configurações**: tema, modo escuro, ícone do botão sol/lua, troca de ícones e acesso ao log.

**Robustez**
- **Modo seguro**: segure Shift ao abrir o GeneXus.
- Desligamento automático: se o GeneXus não terminar de abrir 3 vezes seguidas, a extensão abre no claro.

**Migração**
- A configuração do Xpz.Dark (`%LOCALAPPDATA%\Xpz.Dark\dark.ini`) é migrada na primeira execução. O tema e o "Ativo" são aproveitados.

**Instalador**
- Novo instalador **Setup-GxDarkTheme.exe**, um assistente em pt-BR:
  - encontra todas as instalações GeneXus18*;
  - não instala com o GeneXus aberto e nunca o fecha;
  - remove o Xpz.Dark, o `GxDarkTheme.isl` antigo e as cores gravadas pelo antigo "Ativar Dark Mode";
  - registra o pacote com `GeneXus.exe /install`; se esse passo falhar, acrescenta a entrada no catálogo, guardando uma cópia antes;
  - fica em "Aplicativos instalados" com um desinstalador.

**Créditos**
- Filipe Castro (itsFilipe) é coautor.

### Mantido da v1 (GxDarkTheme)
- O diário de reversão exata: o modo claro volta ao original, com 0 diferenças nos testes de 20 ciclos.
- O botão sol/lua (Ctrl+Shift+F12) à direita da barra de menus.
- O hover plano.
- A prévia de formulário do WorkWithPlus continua com as cores originais.

### Histórico anterior
Veja `docs/historico/` (GxDarkTheme v1 e Xpz.Dark 1.x).
