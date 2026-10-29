# GxDarkTheme 2.0: tema escuro para o GeneXus 18

Esta extensão junta o **GxDarkTheme** (Turman005) e o **Xpz.Dark** (itsFilipe).

- **8 temas:** GeneXus Next (o padrão), Ardósia, Grafite, Meia-noite, Ametista, Brasa, Âmbar e Oliva.
- **Ícones do GeneXus Next** em todos os temas.
- **Troca ao vivo:** você troca o tema e alterna entre claro e escuro sem reiniciar o GeneXus.

## Instalar

1. **Feche o GeneXus.** O instalador nunca fecha o GeneXus por conta própria.
2. Dê dois cliques em **`Setup-GxDarkTheme.exe`** e clique em **Sim** na janela de administrador.
3. Escolha as instalações do GeneXus 18 (o instalador encontra todas as pastas `GeneXus18*`).
4. Escolha o tema padrão e clique em **Instalar**.
   - O passo "GeneXus.exe /install" atualiza a lista de pacotes e leva de 1 a 2 minutos por instalação.
5. Abra o GeneXus normalmente.

O instalador também faz a limpeza das versões antigas:
- remove o **Xpz.Dark** antigo e o `GxDarkTheme.isl` antigo;
- desfaz as cores que o antigo "Ativar Dark Mode" gravava no Windows;
- aproveita o tema que você usava no Xpz.Dark.

## Trocar de tema ou desligar o escuro (com o GeneXus aberto)

- **Botão sol/lua** (à direita da barra de menus), ou **Ctrl+Shift+F12**: alterna entre claro e escuro.
- **Botão direito no sol/lua**: escolhe o tema e abre as Configurações.
- **Ferramentas (Tools) > Tema escuro**: o mesmo menu, com os temas, o modo escuro e as **Configurações...**.

A escolha fica gravada em `%APPDATA%\GeneXus\GeneXus\18\GxDarkTheme.settings`.

## Se algo der errado

- **Abrir o GeneXus sem o tema nesta vez:** segure **Shift** enquanto o GeneXus abre (modo seguro).
- **Desligar de vez sem desinstalar:** use Configurações > desmarque "Extensão ativa" e reabra o GeneXus.
- **Log:** `%TEMP%\GxDarkTheme.log`. Mande esse arquivo se alguma área continuar clara.

## Desinstalar

Vá em **Configurações do Windows > Aplicativos > Aplicativos instalados > GxDarkTheme > Desinstalar**. Feche o GeneXus antes.

O desinstalador:
- remove a extensão de todas as instalações;
- atualiza a lista de pacotes do GeneXus;
- pergunta se você quer manter a configuração (o tema escolhido).

O GeneXus volta exatamente ao visual original.

## O que a extensão NÃO faz

- Não altera arquivos do GeneXus (nem de terceiros) nem as suas KBs.
- Não altera as cores do Windows.
- Tudo é feito em memória, enquanto o GeneXus está aberto.

---
Autores: Gabriel Turman (Turman005) e Filipe Castro (itsFilipe). Licença MIT.

Os ícones vêm do pacote @genexus/mercury 1.5.0 (Apache-2.0), com modificações; veja `THIRD-PARTY-NOTICES.md`.

Este é um projeto independente, sem vínculo com a GeneXus S.A.
