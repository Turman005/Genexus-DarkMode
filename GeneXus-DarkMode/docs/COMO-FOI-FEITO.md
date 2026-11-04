# Como foi feito: GxDarkTheme 2.0 (unificação com o Xpz.Dark)

## Ponto de partida

| | GxDarkTheme v1 (Turman005) | Xpz.Dark 1.x (itsFilipe) |
|---|---|---|
| Temas | 1 (cores do GeneXus Next) | 7 temas com sintaxe própria |
| Reversão | diário exato (claro = original, 0 diferenças) | parcial |
| Ícones | troca Mercury/Next | ícones originais |
| `.isl` | arquivo gerado no build, gravado em Packages | gerado em memória a partir do Silver |
| Instalação | scripts PowerShell (Ativar/Desativar) | Install-Dark.ps1 + catálogo de pacotes |

Os dois projetos eram carregados como **dois pacotes** pelo GeneXus e brigavam pelos mesmos controles. A v2 mantém o motor da v1 e traz o que o Xpz.Dark tinha de melhor.

## 1. Um tema = uma função de cor

`Nucleo/Tema.cs` define cada tema como uma função `M(Color) -> Color`. A paleta do Next é a referência.

**No tema Next, `M` é a identidade.** Por isso o Next continua idêntico à v1: as tabelas, o `.isl` e os ícones passam por `M` sem mudar nada.

**Nos outros temas**, `M` faz o seguinte:
- troca a matiz (hue) e a saturação pela do tema, separando o fundo e o acento;
- **preserva a luminância relativa (WCAG)** de cada cor.

Assim o contraste entre texto e fundo fica igual ao do Next. Os testes medem um contraste mínimo de 3,15 a 3,18 em todos os temas, e nenhum fundo fica claro.

A **sintaxe** do editor vem das tabelas de tokens do Xpz.Dark (`Temas.TokenDe(linguagem, chave)`). `EditorTheme.Rebuild()` monta a tabela do tema atual.

## 2. `.isl` em memória

`Nucleo/GeradorIsl.cs` lê o `GenexusOffice2010Silver.isl` da instalação e aplica o mapa `resources/roles.txt`, que diz qual papel de cor cada valor tem. Depois passa o resultado por `M`.

- **Teste:** no tema Next, o `.isl` gerado é **byte a byte igual** ao `GxDarkTheme.isl` da v1 (103 papéis).
- **Resultado:** nenhum arquivo `.isl` vai mais para a pasta Packages, e o instalador apaga o antigo.

## 3. Troca de tema ao vivo

`Motor/TrocaDeTema.cs` faz a troca em quatro passos. A janela fica congelada (um único Freeze) durante toda a troca:

1. volta ao claro completo usando o diário, ou seja, ao estado original exato;
2. troca as cores: paleta, tabelas do editor e tingimento dos ícones;
3. reaplica o escuro: `.isl`, controles e páginas web (CEF);
4. descongela a janela.

Como cada troca passa pelo claro original, o diário nunca acumula cor sobre cor. O `Test23` com `T23_TEMAS=1` verifica isso: ele passa pelos 7 temas a cada ciclo e espera 0 diferenças.

**Custo:** uma troca leva o tempo de um ciclo claro + escuro.

## 4. Ícones em todos os temas

- `Icones/Icones.cs` guarda os pixels do Next ("mestres") e tinge **cópias** no lugar com `M`.
- As impressões digitais (fingerprints) que identificam os ícones continuam calculadas sobre os mestres, então a troca de tema não confunde a identificação.
- **No tema Next nada é tingido.** A cópia é igual ao mestre, ou seja, os mesmos pixels da v1.

## 5. Menu, botão e configurações

- **Menu Ferramentas > Tema escuro** (`Interface/MenuTema.cs`):
  - inclui itens no `UltraToolbarsManager` da janela principal;
  - **remove esses itens quando a janela fecha**, antes de o GeneXus salvar o layout das barras, para que o layout salvo nunca tenha itens nossos;
  - se a janela não for encontrada, o menu desiste depois de algumas tentativas; o sol/lua continua funcionando.
- **Botão sol/lua:** o clique esquerdo alterna claro/escuro; o direito abre o mesmo menu.
- **Configurações** (`Interface/Configuracao.cs`): o tema e o modo valem na hora; o resto vale ao reabrir o GeneXus.

## 6. Robustez

- **Shift ao abrir:** abre sem tema naquela sessão (modo seguro).
- **Contador `aberturas_incompletas`:** sobe ao iniciar e zera 45 s depois. Se chegar a 3, a próxima abertura é no claro.
- **Cada etapa do `Start()` roda isolada** (`Safe(...)`): um erro numa parte não derruba o GeneXus nem as outras partes.

## 7. Instalador

`installer/` contém um assistente WinForms próprio, em .NET Framework 4.8, que já vem no Windows 10/11. Ele não depende de Inno Setup nem de NSIS, então o build é reproduzível a partir do código.

`Operacoes.cs` faz o trabalho:
- **detecta** as pastas `GeneXus18*`;
- **copia** a DLL;
- **limpa os restos** do Xpz.Dark e o `.isl` da v1;
- **registra** o pacote com `GeneXus.exe /install`;
- **reserva:** se esse passo falhar, insere a entrada no `packages.<N>.xml` (o mesmo formato do Install-Dark.ps1), preservando o BOM e guardando uma cópia `*.antes-gxdarktheme.bak` antes da primeira alteração;
- **desfaz o antigo "Ativar Dark Mode"**, com o porte do `Restaurar-Original.ps1`: só volta valores que ainda são os escuros antigos.

`testes/instalador` executa instalar, reinstalar e desinstalar numa árvore falsa do GeneXus.

## O que ainda falta portar do Xpz.Dark

- `Comparador.cs` com cobertura pelo diário.
- Desligamento automático por recurso, quando uma parte específica falha repetidamente.
- Busca de assinatura para o U14.
- Correções de restos claros:
  - piscada da grade do History;
  - menus de painéis acopláveis e da lista de barras;
  - escurecer antes de mostrar a janela;
  - moldura do Properties.
- Harness de testes fora do processo.
