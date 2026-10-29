# GxDarkTheme: como ficou cada parte do GeneXus 18 no Dark Mode

Este documento mostra **visualmente** como cada parte da IDE do GeneXus 18 fica com o **GxDarkTheme**, o tema escuro com as cores do GeneXus Next. Os detalhes técnicos completos estão em [`COMO-FOI-FEITO-DarkMode.md`](COMO-FOI-FEITO-DarkMode.md), e os arquivos do tema ficam nesta mesma pasta (a extensão em `ext/`).

> **Sobre as imagens:** as marcadas como **captura real** foram tiradas do GeneXus 18 rodando com o tema. As marcadas como **renderização de teste** saíram dos testes offline, que carregam a `GxDarkTheme.dll` real em janelas de teste fora da tela (125% de DPI), sem abrir o GeneXus. Nomes de ambiente e de KB foram ocultados nas capturas.

![GeneXus 18 com o GxDarkTheme: editor de código, abas, Properties, Output e o botão sol no canto superior direito](docs/style/ide-completa-escuro.png)

*Captura real: a IDE inteira no escuro (editor de Events, abas do objeto, Properties, Output). O quadrado vermelho marca o botão sol/lua de alternar o tema.*

---

## Resumo: o que foi feito e como

- **O que é:** uma **extensão do GeneXus** (`GxDarkTheme.dll` + `GxDarkTheme.isl`) que roda dentro do `GeneXus.exe` e escurece a IDE inteira com a paleta do GeneXus Next.
- **Como funciona:** na abertura, a extensão carrega um estilo Infragistics escuro (`.isl`, gerado a partir do estilo Silver original do GeneXus), troca o renderer de menus e barras e, a cada ~700 ms, percorre as janelas abertas e escurece o que ainda está claro. Cada tecnologia da IDE é tratada do seu jeito: Infragistics (docking, abas, árvores, menus), DevExpress (Structure/Variables e o designer de relatório), Actipro SyntaxEditor (editor de código), CefSharp/Monaco (Output e painéis web), MSHTML (Documentation) e WinForms padrão.
- **Tudo em memória e reversível:** nenhuma DLL/EXE do GeneXus ou de terceiros é alterada, nada é gravado na KB e nenhuma cor global do Windows muda. Só o processo do GeneXus fica escuro.
- **Seguro:** cada parte é protegida por `try/catch`. Se algo falhar, só aquela parte volta ao original e a falha vai para o log (`%TEMP%\GxDarkTheme.log`).
- **Contraste:** textos com contraste abaixo de 4,5:1 (WCAG) são clareados automaticamente, mantendo o tom (avisos continuam laranja, erros continuam vermelhos).
- **Exceções de propósito:** a pré-visualização do WorkWithPlus e o designer do Web Layout ficam com as cores originais.
- **Ícones:** os ícones foram trocados pelos do GeneXus Next (Mercury), também só em memória.
- **Alternador:** dá para trocar entre claro e escuro **ao vivo**, sem reiniciar, pelo botão sol/lua ou `Ctrl+Shift+F12`.
- **Instalar:** feche o GeneXus, rode `GeneXus18 - Ativar Dark Mode.cmd` e aceite o UAC. O GeneXus abre sozinho (sem o parâmetro `/IDEStyle`).

---

## Paleta GeneXus Next

![Paleta de cores do GxDarkTheme](docs/style/paleta-genexus-next.png)

Todas as cores da IDE saem desta paleta: editor `#11151C`, tool windows `#181F2A`, barras `#242D3C`, selecionado `#2C3C50`, bordas `#333D47`, destaque `#2C8DCE`, links `#5BA7FF`, textos `#DCE2EB` / `#F0F4FA` / `#8A909B`, campos de edição `#1F2734` e erro `#E35861`. O editor de código usa tokens no estilo VS Code Dark+.

---

## Ícones Next (Mercury)

![Ícones da barra, menus e janelas: GeneXus 18 x GeneXus Next](docs/style/icones-next-barra-janelas.png)

*Renderização de teste: barra, menus e janelas. Colunas: ícone original do GeneXus 18, ícone Next em 1x/2x, na barra, em hover e desabilitado.*

![Ícones dos objetos do KB Explorer e das abas do objeto](docs/style/icones-next-kb-explorer.png)

*Renderização de teste: objetos do KB Explorer e abas do objeto, com a simulação da árvore antes/depois.*

![Ícones de Structure/Variables, Team Development e Output](docs/style/icones-next-structure-teamdev-output.png)

*Renderização de teste: Structure/Variables, Team Development e Output.*

![Ícones antes, depois, hover, restaurado e desligado](docs/style/icones-next-antes-depois.png)

*Renderização de teste: original, Next, hover, restaurado em execução, `icons=false` e tema desligado.*

Os ícones vêm do pacote público `@genexus/mercury` (design system do GeneXus Next), em 16/24/32 px com variantes normal, hover e desabilitado. Cada ícone original é reconhecido pela "impressão digital" dos pixels e trocado só na hora do desenho; para manter o tema escuro com os ícones originais, use `icons=false` no arquivo de configuração.

---

## Barra principal e hover

![Barra principal: antes, depois e restaurado](docs/style/barra-principal.png)

*Renderização de teste com a barra real do GeneXus (`ToolBarManager`) a 125% de DPI: antes, depois com os ícones Next e restaurado.*

![Hover, pressionado e marcado nos botões](docs/style/hover-botoes.png)

*Renderização de teste: o brilho claro do Office 2010 virou hover plano no estilo Next.*

A 125% de DPI o GeneXus redimensiona os ícones da barra duas vezes, e por isso eles não eram reconhecidos; agora qualquer tamanho é reconhecido. O hover ficou plano: `#344860` com borda `#4E6888`, pressionado `#1F4E79` e marcado `#2C3C50` com borda `#2C8DCE`.

---

## Linhas e textos

![Linhas claras suavizadas e textos escuros clareados](docs/style/linhas-e-textos.png)

*Renderização de teste: barra principal, popup "Add or Remove Buttons", Values Editor e ListView comum, antes/depois.*

Linhas brancas das barras, popups com fundo de sistema `#F0F0F0`, cabeçalhos e grades claras de listas viraram `#333D47` / `#242D3C`. Textos acinzentados sobem até um contraste mínimo, e glifos pretos ficam `#DCE2EB`.

---

## Ícones de status (Navigation View / Impact Analysis)

![Ícones de status antes e depois](docs/style/icones-de-status.png)

*Renderização de teste (zoom 3x): Navigation View e Impact Analysis, antes/depois.*

Os ícones de status (OK, aviso, erro, "=", "?") tinham um anel branco porque o WinForms copiava a lista para 8 bits. Agora cada árvore recebe uma lista própria de 32 bits com os ícones coloridos do Mercury.

---

## Relatório do Navigation View

![Relatório do Navigation View antes e depois](docs/style/relatorio-navigation-view.png)

*Renderização de teste (Chrome offscreen, com o `genexus.css` real): à esquerda o escurecimento genérico, à direita a folha de estilo Next.*

O relatório deixou de parecer um "negativo": fundo `#11151C`, objetos e links em `#5BA7FF`, erros em `#E35861`, avisos em laranja e palavras-chave em lilás. É só exibição; nada é gravado.

---

## Layout de procedure

![Aba Layout da procedure: original, build anterior, novo e falha simulada](docs/style/layout-procedure.png)

*Renderização de teste: original, versão anterior (quebrada), novo, printBlock selecionado, falha simulada e zoom.*

O designer de relatório segue o GeneXus Next: página `#11151C`, régua `#242D3C`, cabeçalho do printBlock `#2C3C50` e seleção `#2C8DCE`, com textos nítidos. Se algo falhar, ele volta sozinho às cores originais (nunca fica preto), e o relatório da KB não é alterado.

---

## Aba Web Layout

![Ícone da aba Web Layout](docs/style/aba-web-layout.png)

*Renderização de teste: ícone da aba Web Layout nos estados default e editado, e restaurado.*

O ícone serrilhado da aba (raio + selo de estado em 8 bits) virou o ícone Mercury em 32 bits, e a troca acompanha o estado da parte. O designer do Web Layout em si continua com as cores originais.

---

## WorkWithPlus: atributos obrigatórios

![Atributos obrigatórios do WorkWithPlus antes e depois](docs/style/wwp-atributos-obrigatorios.png)

*Renderização de teste com os ícones reais do WorkWithPlus.*

Atributos e variáveis obrigatórios eram um glifo vermelho em quadrado branco. Agora usam o mesmo glifo do atributo normal, em vermelho `#E35861`, com fundo transparente. A pré-visualização do WorkWithPlus não é alterada.

---

## CDATA / autocompletar / CSS

![Bloco CDATA, lista de autocompletar e CSS antes e depois](docs/style/cdata-autocompletar-css.png)

*Renderização de teste com o editor real (Actipro) e as definições de linguagem do GeneXus; o editor DSO foi simulado no Chrome.*

Blocos `<script><![CDATA[` e `<style>` tinham fundo claro (WhiteSmoke) e texto apagado, e a lista de autocompletar era branca. Agora tudo fica no fundo do editor, com cores próprias para JavaScript e CSS, e a lista usa `#1F2734` com seleção `#2C3C50`.

---

## Alternador sol/lua

![Alternar claro/escuro ao vivo](docs/style/alternador-claro-escuro.png)

*Renderização de teste: original, escuro, e os dois modos depois de 20 ciclos, com o botão ampliado.*

![Botão sol/lua (opção Âmbar)](docs/style/botao-sol-lua.png)

*Prévia do botão no estilo padrão "Âmbar": sol âmbar no escuro, lua ardósia no claro.*

- **Botão:** no fim da barra de menus, alinhado ao botão Fechar da janela. No escuro mostra um sol (passa para o claro); no claro mostra uma lua (passa para o escuro).
- **Atalho:** `Ctrl+Shift+F12`. Se alguma ferramenta do GeneXus já usar essa combinação, vale só o botão.
- **Persistência:** o modo escolhido fica na linha `mode=dark` ou `mode=light` de `%APPDATA%\GeneXus\GeneXus\18\GxDarkTheme.settings`, e o GeneXus abre no último modo usado.
- **Volta exata:** antes da primeira troca, cada valor original é anotado e devolvido exatamente no claro. Se alguma aba ficar com sobras do outro modo, é só reabri-la.

---

## Como desativar / restaurar

- **Alternar só nesta sessão:** botão sol/lua ou `Ctrl+Shift+F12`.
- **Desligar sem desinstalar:** em `%APPDATA%\GeneXus\GeneXus\18\GxDarkTheme.settings`, coloque `enabled=false`. No próximo início, o GeneXus abre com as cores originais. A variável de ambiente `GXDARK_DISABLE=1` faz o mesmo.
- **Tema escuro com os ícones originais:** `icons=false` no mesmo arquivo.
- **Desinstalar:** feche o GeneXus, rode `GeneXus18 - Desativar Dark Mode.cmd` e aceite o UAC. Ele remove a extensão da pasta Packages, reconstrói o catálogo de pacotes e limpa sobras da versão antiga.

---

## Atribuição

Ícones: **GeneXus Mercury** (`@genexus/mercury` 1.5.0), © GeneXus S.A., licenciados sob a [Apache License 2.0](https://www.apache.org/licenses/LICENSE-2.0). Os PNGs foram gerados a partir dos SVGs públicos do pacote e recoloridos conforme os tokens do Mercury; três ícones de status (igual, igual com aviso, especificação pendente) e o "X" de erro foram derivados no mesmo estilo. O aviso de licença (`NOTICE`) vai embutido na `GxDarkTheme.dll`; ainda não há um arquivo `THIRD-PARTY-NOTICES` separado nesta pasta.

GeneXus, GeneXus Next e WorkWithPlus são marcas dos seus respectivos donos. O GxDarkTheme é um projeto independente.
