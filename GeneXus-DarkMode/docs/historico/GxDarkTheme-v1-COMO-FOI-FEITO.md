# Como foi feito o Dark Mode do GeneXus 18 (GxDarkTheme)

Este documento explica **o que foi usado** e **como** o tema escuro do GeneXus 18 foi construído.
Tudo aqui vem dos arquivos reais do projeto: `ext\GxDarkTheme.cs`, `make_isl.py`, `Build-GxDarkTheme.ps1`,
`Extensao-Admin.ps1`, `Restaurar-Original.ps1`, os scripts Ativar/Desativar e os testes offline.

---

## 1. Objetivo e requisitos

- **Toda a IDE escura com as cores do GeneXus Next.** São tons azulados escuros: editor, grades, Properties, Output, abas, menus, barras, status, molduras e diálogos.
- **Exceção:** a pré-visualização/Layout do **WorkWithPlus** e o **Web Layout** ficam com as cores originais.
- **Afeta só o GeneXus.** Nenhuma cor global do Windows é alterada, e outros programas (ex.: a janela Executar) ficam como estão.
- **Reversível.** Com o tema desligado ou removido, o GeneXus abre exatamente com as cores originais, sem sobras.
- **Nenhum binário alterado.** Não há patch em DLL/EXE do GeneXus nem de terceiros (Infragistics, DevExpress, Actipro...).
- **Nada gravado na KB.** O tema não salva estilos em objetos, documentação ou relatórios.
- **Seguro.** Todo o código é protegido por `try/catch`, e se uma parte falha, só aquela parte é desligada (com registro no log), sem derrubar a IDE.

---

## 2. Arquitetura

### 2.1 Pacote de extensão do GeneXus (in-process)

O tema é uma **DLL de extensão do GeneXus** (`GxDarkTheme.dll`) que roda **dentro do processo** `GeneXus.exe`:

```csharp
[assembly: Artech.Architecture.Common.Packages.Package(typeof(GxDarkTheme.DarkThemePackage), IsUIPackage = true, ...)]
[assembly: Artech.Architecture.Common.Packages.PackageCompatibility(Version = 143920)]

public class DarkThemePackage : Artech.Architecture.UI.Framework.Packages.AbstractPackageUI
{
    public override void Initialize(IGxServiceProvider services) { base.Initialize(services); DarkEngine.Start(); }
}
```

- A DLL e o `.isl` são copiados para `C:\Program Files (x86)\GeneXus\GeneXus18\Packages`.
- O catálogo de pacotes é reconstruído com **`GeneXus.exe /install`** (feito pelo `Extensao-Admin.ps1`, como administrador).
- Na abertura, o GeneXus chama `Initialize`, e o `DarkEngine.Start()`:
  1. verifica se o tema está desligado (variável `GXDARK_DISABLE=1` ou arquivo de configuração);
  2. ativa o modo escuro do processo (menus nativos/scrollbars);
  3. carrega o estilo Infragistics escuro (`.isl`);
  4. troca o renderer dos `ToolStrip`;
  5. inicia um **timer de 700 ms** (mais passadas rápidas no `Application.Idle` nos primeiros instantes, para evitar o "flash" branco na abertura).
- **A cada passada**, o motor percorre os formulários abertos (`Application.OpenForms`) e seus controles, e escurece o que ainda não foi tratado.
- **Tudo acontece na thread de UI.** Formulários de outra thread são ignorados.

### 2.2 Arquivo `.isl` (Infragistics AppStylist)

- A maior parte da IDE (janela principal, docking, abas MDI, árvores, grades, menus/ribbon/toolbars) usa **Infragistics NetAdvantage v11.1**, que aceita uma *style library* (`.isl`).
- O `GxDarkTheme.isl` foi **gerado por um script Python** (`make_isl.py`) a partir do `.isl` original do GeneXus (`GenexusOffice2010Silver.isl`), trocando as cores pelas da paleta Next.
- Ele é carregado **em memória** com `Infragistics.Win.AppStyling.StyleManager.Load(stream)`. O arquivo original do GeneXus não é alterado.
- Antes de carregar, o C# ainda:
  - aplica o mesmo ajuste de DPI que o GeneXus faz (tamanho mínimo do thumb da scrollbar);
  - injeta um **glyph de checkbox escuro** gerado em memória.

---

## 3. Ferramentas e tecnologias usadas

| Item | Uso |
|---|---|
| **C# / .NET Framework 4.x** | Código da extensão (`GxDarkTheme.cs`, ~3.300 linhas), escrito em **C# 5**. |
| **csc.exe** (`C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe`) | Compilador usado (não foi usado msbuild/Visual Studio). Script: `Build-GxDarkTheme.ps1`, `/target:library /platform:anycpu /optimize+`. |
| Referências de compilação | `Artech.*.dll` do GeneXus, `DevExpress.*v7.3.dll`, `Infragistics2.Win/Shared v11.1`, `ActiproSoftware.SyntaxEditor/Shared/WinUICore`, `System.*`. |
| **Python 3** (`xml.etree.ElementTree`, `colorsys`, `re`) | `make_isl.py`: gera o `.isl` escuro a partir do Silver, mapeando fundos/textos/bordas por papel (*role*) e estado, e no fim gera um relatório de fundos claros remanescentes. |
| **PowerShell** | Ativar/Desativar, instalação como admin (`Extensao-Admin.ps1`), restauração de sobras antigas (`Restaurar-Original.ps1`), build e **testes offline** (carregam a DLL real num formulário de teste fora da tela, sem abrir o GeneXus, e capturam a imagem via `PrintWindow`). |
| **Reflexão .NET** | Ler e ajustar propriedades e campos internos **em memória**, sem modificar nenhum assembly. Os assemblies do GeneXus e de terceiros foram **decompilados só para leitura** (para entender quem pinta o quê). |
| **Win32 / DWM** | `DwmSetWindowAttribute`: barra de título escura (atributos 20/19), cor do título #242D3C (35) e do texto (36). |
| **Tema escuro do Windows por janela** | `SetWindowTheme(hwnd, "DarkMode_Explorer" / "DarkMode_CFD" / "DarkMode_ItemsView")` e as funções do `uxtheme.dll` por ordinal: 135 `SetPreferredAppMode`, 104 `RefreshImmersiveColorPolicyState`, 133 `AllowDarkModeForWindow`, 136 `FlushMenuThemes`. Isso vale **só para o processo do GeneXus**. |
| **Subclasse de janela** (`NativeWindow`) | Pintura própria de molduras finas, StatusBar, combos Flat, cabeçalho de ListView (`NM_CUSTOMDRAW`) e o filtro de "tinta" do designer de relatório. |
| **Timers / eventos** | `System.Windows.Forms.Timer` (700 ms), `Application.Idle`, eventos `Paint`, `ControlAdded`, `HandleCreated`, `BackColorChanged` e eventos das grades DevExpress. |
| **CefSharp (Chromium) / Monaco** | Painéis web (Output, GxWebControl etc.): injeção de JavaScript/CSS via `ExecuteScriptAsync` em cada carregamento (`FrameLoadEnd`). Nos editores Monaco, o tema é redefinido para `vs-dark` com as cores Next (fundo, gutter, **minimap**, seleção, widgets, scrollbars) e as cores de tokens são remapeadas. Um `MutationObserver` escurece o conteúdo que aparece depois. |
| **MSHTML (IE) – Documentation** | Na aba Documentation (wiki), o documento é obtido pela janela `Internet Explorer_Server` (`WM_HTML_GETOBJECT` + `ObjectFromLresult`), e as cores vão em **`runtimeStyle`**, um estilo **só de exibição, que não é serializado no HTML**, ou seja, não vai para a KB. |
| **DevExpress v7.3** | Grades em árvore (abas Structure/Variables): `TreeList.Appearance` + eventos de desenho customizado. Designer de relatório (aba Layout da Procedure, XtraReports): tabela de cores e estilo de pintura próprios. |
| **Actipro SyntaxEditor** | Editor de código: cores dos tokens e do renderer **em memória** (estilo VS Code Dark+). O `TextEditorConfig_Default.xml` **não** é gravado. Popups IntelliPrompt (QuickInfo, parâmetros, lista) também são tratados. |
| **Infragistics** | `.isl` escuro + `Office2007/Office2010ColorTable.ColorScheme = Black` + ajustes nas tabelas de cores Office2010. |
| **VisualHint SmartPropertyGrid** | Painel Properties: faixas de categoria (que vinham com gradiente até branco) e links (`Color.Blue` fixo) recoloridos. |
| **Crownwood Magic TabControl** | Abas de alguns diálogos pintadas no tema escuro. |
| **WinForms padrão** | `ToolStripProfessionalRenderer` com `DarkColorTable`, `DataGridView`, `PropertyGrid`, `TabControl`, CheckBox, GroupBox, botões/labels desabilitados, caixas somente leitura. |
| **Regras de contraste (WCAG)** | Função `Readable()`: qualquer texto com contraste < 4,5:1 sobre o fundo escuro vira o azul de link do Next ou um texto claro. |

---

## 4. Paleta GeneXus Next usada

| Nome no código | Cor | Uso |
|---|---|---|
| `Editor` | `#11151C` | Fundo de editor/documento, linhas de grade |
| `Panel` | `#181F2A` | Painéis, árvores, abas, superfície do relatório |
| `Chrome` | `#242D3C` | Barra de título, menus, barras, status, cabeçalhos de grade, régua |
| `Input` | `#1F2734` | Caixas de texto, combos, editores na célula |
| `Border` | `#333D47` | Bordas, separadores, linhas de grade, pontos do grid do relatório |
| `Sel` | `#2C3C50` | Linha selecionada, bandas do relatório |
| `HotBg` | `#344860` | Hover (barras, abas, listas, grades) |
| `HotBorder` | `#4E6888` | Borda do hover / menu aberto |
| `MenuHot` | `#1F4E79` | Item sob o mouse/pressionado, linha focada, banda principal do relatório |
| `Accent` | `#2C8DCE` | Destaque |
| `Fg` | `#DCE2EB` | Texto normal |
| `FgBright` | `#F0F4FA` | Texto de destaque / título |
| `FgSoft` | `#BEC6D2` | Abas inativas, textos secundários |
| `FgDisabled` | `#969DA8` | Textos desabilitados |
| `FgDim` | `#8A909B` | Textos cinza (régua, dicas) |
| `Link` | `#5BA7FF` | Links |

**Editor de código (tokens estilo VS Code Dark+):**

| Token | Cor |
|---|---|
| texto | `#D4D4D4` |
| palavra-chave | `#569CD6` |
| comentário | `#6A9955` |
| string | `#CE9178` |
| variável | `#9CDCFE` |
| reservada | `#4EC9B0` |
| número | `#B5CEA8` |
| função reservada | `#DCDCAA` |
| seleção | `#264F78` |

---

## 5. O que é tratado em cada área da IDE

- **Janela principal, docking, abas MDI, árvores (KB Explorer etc.), menus, ribbon e toolbars:** pelo `.isl` Infragistics escuro mais as tabelas de cores Office2010.
- **Barras de título:** escuras via DWM em todas as janelas do GeneXus.
- **Menus e scrollbars nativos:** modo escuro do processo + `SetWindowTheme` nos controles com barra de rolagem (inclusive as que aparecem depois).
- **Editor de código (Actipro SyntaxEditor):**
  - cores dos tokens, margem, números de linha, seleção e indicadores, tudo em memória;
  - se o GeneXus recarregar as cores (ex.: Tools > Options), elas são reaplicadas;
  - os botões de divisão do editor ganham um renderer escuro.
- **Popups do editor (QuickInfo/tooltip, parâmetros):** fundo escuro, borda `#333D47`, textos recoloridos com contraste mínimo.
- **Painel Properties (SmartPropertyGrid):** faixas de categoria escuras, links em `#5BA7FF`.
- **Output e outros painéis web (CefSharp/Monaco):** CSS/JS injetado, Monaco em `vs-dark` com cores Next e fundo do painel escuro antes de o navegador carregar.
- **StatusBar:** pintura própria. Os painéis *OwnerDraw* do GeneXus são desenhados num bitmap e o texto preto é clareado.
- **Molduras, GroupBox, combos Flat, cabeçalho de ListView:** moldura de 1 px `#333D47`, título claro.
- **Caixas somente leitura/desabilitadas, botões e labels desabilitados:** cores fixas legíveis (ex.: os campos da tela **Team Development > Update**).
- **Checkboxes:** glyph escuro nas grades/listas Infragistics e quadradinho escuro nos CheckBox WinForms.
- **Abas Structure / Variables (DevExpress TreeList):**
  - fundo `#11151C`, cabeçalho `#242D3C` com separadores `#333D47`;
  - seleção `#2C3C50`, linha focada `#1F4E79` com texto branco, hover `#344860`;
  - expansor e linhas da árvore visíveis, checkboxes escuras, texto cinza legível;
  - editor na célula escuro, scrollbars/botão de combo com o tema escuro do Windows;
  - lista do autocompletar do Type escura (`LVM_SETBKCOLOR`/`LVM_SETTEXTCOLOR`).
- **Aba Layout da Procedure (designer de relatório XtraReports):**
  - superfície `#181F2A`/`#11151C` e régua `#242D3C`;
  - a banda verde-limão (printBlock) vira `#1F4E79`/`#2C3C50` com título claro;
  - um filtro pós-pintura clareia o texto e as linhas pretas e transforma os pontos do grid em `#333D47`;
  - o relatório salvo na KB não muda.
- **Aba Documentation (wiki: edição, Preview, History):**
  - documento `#11151C`, texto `#DCE2EB`, links `#5BA7FF`, via `runtimeStyle` (não vai para o HTML salvo);
  - se o documento em edição não suportar `runtimeStyle`, ele fica claro de propósito, para não correr o risco de gravar estilos.

---

## 6. Exclusões (ficam com as cores originais)

- **Pré-visualização / Layout do WorkWithPlus** e demais pré-visualizações de patterns/formulários:
  - por tipo de controle: `IsPreviewControl` (patterns, WorkWith, DVelop, Form/Layout/Panel cujo nome termina em *Preview*);
  - por URL: `IsPreviewUrl` (`/previewimages/`, `_preview.html`, `/preview/`).
- **Web Layout e designers de objetos:** controles hospedados em designer (`Site.DesignMode`) nunca são tocados.
- **Diálogos de erro/feedback:** `FeedbackDialog`, `ThreadExceptionDialog` e formulários com *Exception/ErrorDialog/ErrorReport/Crash* no nome.
- **Formulários de outra thread** (`InvokeRequired`): ignorados.
- **Diálogos do próprio Windows** (abrir/salvar arquivo, Executar etc.) e **outros programas:** não são alterados, porque o tema não muda cores globais do sistema.

---

## 7. Segurança e reversibilidade

- **Desligar sem desinstalar:** criar `%APPDATA%\GeneXus\GeneXus\18\GxDarkTheme.settings` com a linha `enabled=false` (também aceita `enabled=0` ou `off`). No próximo início, a extensão não faz nada e o GeneXus abre como original.
- **Desligar por variável de ambiente:** `GXDARK_DISABLE=1`.
- **Desinstalar:** atalho **"GeneXus18 - Desativar Dark Mode.cmd"**. Ele remove a DLL e o `.isl` da pasta Packages, roda `GeneXus.exe /install` e limpa sobras da versão antiga.
- **Nada global:**
  - não usa `SetSysColors`, não grava em `HKCU\Control Panel\Colors` e não muda tema/cor de destaque do Windows;
  - o modo escuro (DWM/uxtheme) é aplicado janela a janela, só no processo do GeneXus.
- **Nada na KB:** não grava objetos, documentação nem relatórios. As cores do editor não são salvas no `TextEditorConfig_Default.xml`.
- **Sem patch:** nenhuma DLL/EXE do GeneXus ou de terceiros é modificada. Tudo é feito em memória (propriedades, eventos, reflexão, subclasse de janela).
- **Log:** `%TEMP%\GxDarkTheme.log`, que registra:
  - o que foi escurecido;
  - o que foi mantido original (pré-visualizações, diálogos);
  - erros (cada parte com erro se desliga sozinha);
  - um diagnóstico periódico com linhas **`AINDA CLARO: <tipo> (<nome>) back=<cor>`**, que mostram controles que continuam claros e serviram para achar o que faltava tratar.

---

## 8. Estrutura da pasta e instalação

```
GeneXus-DarkMode\
├─ GeneXus18 - Ativar Dark Mode.cmd      -> chama Ativar-DarkMode.ps1
├─ GeneXus18 - Desativar Dark Mode.cmd   -> chama Desativar-DarkMode.ps1
├─ Ativar-DarkMode.ps1
├─ Desativar-DarkMode.ps1
├─ Readme.md                             (seu)
├─ COMO-FOI-FEITO-DarkMode.md            (este documento)
└─ ext\
   ├─ GxDarkTheme.dll                    a extensão
   ├─ GxDarkTheme.isl                    estilo Infragistics escuro
   ├─ Extensao-Admin.ps1                 copia/remove em Packages + GeneXus.exe /install (admin)
   └─ Restaurar-Original.ps1             desfaz sobras da versão antiga do Ativar
```

**Instalar:**
1. Feche o GeneXus.
2. Rode **"GeneXus18 - Ativar Dark Mode.cmd"**.
3. Clique **Sim** no UAC e aguarde de 1 a 2 minutos.
4. O GeneXus abre sozinho.

Abra o GeneXus **sem** o parâmetro `/IDEStyle`, porque esse parâmetro impede o carregamento do tema.

**Desinstalar:**
1. Feche o GeneXus.
2. Rode **"GeneXus18 - Desativar Dark Mode.cmd"** e clique **Sim** no UAC.

---

## 9. Histórico resumido: problemas e soluções

1. **Cores globais do Windows (abandonado).**
   - **Problema:** a primeira versão do Ativar gravava a paleta escura nas cores de sistema do Windows (`SetSysColors` + `HKCU\Control Panel\Colors`), mudava tema de apps/cor de destaque e escrevia as cores no `TextEditorConfig_Default.xml`. Isso **vazava para outros programas** (ex.: a janela Executar do Windows).
   - **Solução:** tudo passou a ser feito **só dentro da extensão, em memória**. O `Restaurar-Original.ps1` devolve o original, mas apenas os valores que ainda estão exatamente com o escuro gravado pela versão antiga (o que você mudou depois fica como está). Os controles que dependiam de cores de sistema receberam cores fixas explícitas (`FixSystemColors`).
2. **Minimap branco / Monaco claro no Output.**
   - **Problema:** o Monaco dentro do CefSharp usava o tema claro.
   - **Solução:** `defineTheme`/`setTheme` foram interceptados para `vs-dark` com as cores Next (`minimap.background`, gutter, widgets, scrollbar) e uma borda do minimap `#333D47`. As cores de tokens do Output foram remapeadas para versões legíveis.
3. **Tooltip (QuickInfo) ilegível no editor.**
   - **Problema:** textos azul-escuro/preto/vermelho-escuro sobre fundo escuro.
   - **Solução:** o `MarkupLabel` do Actipro é recolorido com a regra de contraste (links e tipos em `#5BA7FF`, cinzas e escuros clareados, fundo claro removido) e o popup ganha borda `#333D47`. Hover/pressionado de menus e toolbars ficaram azuis bem visíveis.
4. **Cross-thread (FeedbackDialog / WorkWithPlus).**
   - **Problema:** alguns diálogos (feedback/erro) e telas do WorkWithPlus rodam em outra thread, e mexer neles da thread de UI causava erro.
   - **Solução:** formulários com `InvokeRequired` passaram a ser ignorados, e os diálogos de erro/feedback foram excluídos por tipo (`IsExcludedForm`).
5. **Team Development.**
   - **Problema:** campos brancos ao lado de *Knowledge Base / Version / Last full update* (caixas somente leitura) e linhas/bordas claras nas grades *Pending Commits / Pending for Update*.
   - **Solução:** caixas somente leitura/desabilitadas com fundo `#181F2A` e texto `#DCE2EB`, e grades e cabeçalhos com borda sólida `#333D47` no `.isl`.
6. **Checkbox branca nas grades.** O glyph padrão era um quadrado branco. Foi gerado um glyph escuro e injetado no `.isl` em memória.
7. **StatusBar com painéis OwnerDraw.** O GeneXus desenha parte da barra com texto preto. Agora ela é pintada pela extensão (fundo `#242D3C`, texto clareado).
8. **Molduras, combos e cabeçalhos claros do Windows.** Foram resolvidos com pintura própria da borda não-cliente e custom draw do cabeçalho da ListView.
9. **Aba Structure branca (e Variables).**
   - **Problema:** são grades DevExpress (não Infragistics), por isso o `.isl` não as alcançava.
   - **Solução:** foram tratadas via `Appearance` + eventos de desenho (cabeçalho, botão de expandir, célula, checkbox, área vazia), além de trocar em memória o tema que o DevExpress usa para scrollbar/combo.
10. **Aba Layout (relatório) com banda verde-limão e fundo branco.**
    - **Solução:**
      - tabela de cores `Office2003Colors` escura;
      - estilo de pintura escuro (título da banda claro);
      - fundo das bandas `#181F2A`;
      - filtro pós-pintura para o texto e as linhas pretas dos controles e para os pontos do grid.
11. **Aba Documentation branca.**
    - **Problema:** o editor wiki é MSHTML, e mudar o HTML gravaria as cores na KB.
    - **Solução:** usar `runtimeStyle` (só exibição). No teste, o HTML antes e depois ficou idêntico e o Undo não foi acionado.

---

## 10. Limitações conhecidas

- **Designer de relatório:**
  - o filtro de clareamento atua depois de cada pintura normal; o retângulo de arrastar/redimensionar controles é desenhado fora disso e pode aparecer diferente;
  - partes em preto/cinza de imagens colocadas no relatório também são invertidas;
  - se o relatório definir fundo branco explícito, só o filtro o escurece.
- **Documentation:** se um documento em modo de edição não suportar `runtimeStyle`, ele fica claro de propósito (registrado no log). A visualização do código-fonte HTML não foi verificada visualmente.
- **Autocompletar do Type:** depende de a lista ser criada na mesma thread da IDE. Isso não foi testável offline.
- **Telas novas ou raras** que usem controles não previstos podem continuar claras. Elas aparecem no log como **`AINDA CLARO`**, que é o ponto de partida para tratá-las.
- **Telas de terceiros:** a tela *TemplateSelector* do WorkWithPlus (editor de template, não a pré-visualização) fica escura pelo CSS injetado nos painéis web.
- **Versão:** o tema foi feito e testado para **GeneXus 18 (18.0.16)** com as versões de Infragistics v11.1, DevExpress v7.3 e Actipro que vêm nele. Outra versão do GeneXus pode exigir recompilação.

---

## 11. Ícones e contraste

### 11.1 Ícones do GeneXus Next (Mercury)

- **Origem:** os ícones vêm do pacote público **`@genexus/mercury` 1.5.0**, o design system do GeneXus Next.
  - Licença **Apache-2.0**, © GeneXus S.A.
  - O aviso de licença (`NOTICE`) vai embutido na `GxDarkTheme.dll`, junto com os PNGs.
- **Mapa:** 115 entradas.
  - 97 equivalentes exatos e 11 aproximados.
  - 7 ícones sem equivalente no Next continuam os originais do GeneXus, só ajustados para o fundo escuro.
- **Cores:**
  - ícones coloridos usam as cores *enabled/hover* do Mercury;
  - ícones monocromáticos usam `#DCE2EB`, e `#F0F4FA` no hover;
  - ícones desabilitados ficam em cinza clareado com 55% de opacidade.
- **Tamanhos:** cada ícone vem em 16, 24 e 32 px, nas variantes normal, hover e desabilitado. Eles ficam como recursos da DLL (`next_*_16/24/32.png` + `icons.txt`).
- **Código:** `ext\GxIcons.cs`, compilado junto com `GxDarkTheme.cs`.
- **Como a troca funciona (só em memória):**
  1. Cada ícone original é reconhecido pela "impressão digital" dos pixels.
     - Também valem as versões degradadas de ImageList de 8 e 24 bits.
     - Por isso a troca não depende do nome do arquivo.
  2. O `CachingResourceManager` do GeneXus recebe a versão Next já no cache, então telas abertas depois também ganham o ícone novo.
  3. Em cada tipo de controle a troca é feita de um jeito:
     - **Infragistics** (KB Explorer, toolbars, árvores): um *draw filter* desenha a variante certa (normal, hover ou desabilitada);
     - **ToolStrip/menus:** o renderer escuro desenha a variante certa;
     - **ImageList:** reconstruída em 32 bits com os ícones novos.
  4. Os originais ficam guardados. Desligar o tema em execução devolve tudo, e a troca é thread-safe.
  5. Se der erro, a parte afetada fica com os ícones originais e grava uma única linha no log.
- **Exclusões:** a pré-visualização do WorkWithPlus e o Web Layout não são tocados.
- **Manter o tema escuro com os ícones originais:** no `%APPDATA%\GeneXus\GeneXus\18\GxDarkTheme.settings`, adicione a linha `icons=false`.
  - A linha `enabled=false` continua desligando o tema inteiro.

### 11.2 Rodada de contraste (WCAG ≥ 4.5:1)

Esta rodada cobre:

- abas, rótulos desabilitados e grades;
- KB Explorer, Properties, Output, Team Development e Extensions Manager;
- barra de status, títulos e atalhos de menu;
- tooltips, *placeholders*, cabeçalhos de grupo e números de linha.

Como funciona:

- **Cores de texto:** são clareadas mantendo o tom, até ≥ 4.5:1 sobre o fundo real. Avisos continuam laranja e erros continuam vermelhos.
- **Estilo Infragistics (`.isl`):** é corrigido em memória ao carregar. O arquivo em disco não muda.
- **Casos fisicamente impossíveis** ficam com a melhor cor disponível e aparecem no log como **`BAIXO CONTRASTE`**.
  - Exemplo: texto branco sobre o azul de destaque `#2C8DCE`, que chega no máximo a 3.6:1.
- **Varredura periódica:** registra no log, uma vez por controle, qualquer texto ainda abaixo de 4.5:1, também como `BAIXO CONTRASTE`.

### 11.3 Aba Layout de Procedure (designer de relatório) refeita

A versão anterior deixava a área do Layout preta ou vazia. O filtro antigo lia de volta os pixels da tela, e isso falhava no designer.

Agora ela segue o GeneXus Next:

| Elemento | Cor |
|---|---|
| Fora da página | `#181F2A` |
| Página | `#11151C`, com borda `#333D47` |
| Régua | fundo `#242D3C`, números e marcas `#8A909B` |
| Cabeçalho do printBlock | `#2C3C50`, com título claro e ícone visível |
| Banda selecionada | destaque `#2C8DCE` |
| Pontos do grid | `#333D47` |
| Texto e linhas dos controles | claros |

Como funciona:

- **Cores do próprio designer:** a tabela `Office2003Colors` e o estilo de pintura do DevExpress são trocados em memória.
- **Página e réguas:** o próprio controle pinta num bitmap separado, as cores são convertidas, e o bitmap é desenhado na janela.
  - O texto ClearType é convertido canal a canal, então fica nítido, sem franjas.
  - Cores de verdade do relatório mantêm o tom e só são clareadas se estiverem escuras.
- **Segurança:**
  - qualquer erro nessa pintura devolve na hora as cores originais do DevExpress (tela clara normal, nunca preta) e grava uma linha no log;
  - isso foi testado com uma falha simulada.
- O relatório em si (KB) não é alterado; é só exibição.
- Isto substitui o item 10 da seção 9 e a primeira limitação da seção 10.

### 11.4 Barra principal (cabeçalho) e DPI acima de 100%

**Problema:** com o Windows em 125% de DPI, o GeneXus redimensiona os ícones de comando **duas vezes**: o cache de recursos já entrega o `.ico` em 20 px e o `DefaultMenuResolver.GetImage` ainda aplica `GdiHelper.ResizeImage` para `ScreenDPI.GetSize(20)` = **25 px** (bicúbico). A "impressão digital" da extensão só conhecia 16/20/24/32 px, então nenhum ícone da barra principal (novo, abrir, salvar, recortar/copiar/colar, desfazer/refazer, build, run…) era reconhecido e o original continuava sendo desenhado. Além disso, o `Artech.FrameworkDE.ToolBarManager` não tem "Toolbars" no nome e não recebia o tratamento de barra (hover).

**Correção (só em memória, reversível):**
- Referências geradas sob demanda para **qualquer tamanho** visto na hora do desenho, com uma cópia fiel do `GdiHelper.ResizeImage` do GX (16/20/24/32 → tamanho alvo). Os tamanhos do DPI atual (ex.: 20 e 25 px) são preparados na inicialização.
- `ImageList` de 32 bits também entra como referência.
- Barras/menus reconhecidos pelo tipo base (`Infragistics.Win.UltraWinToolbars.*`), não pelo nome, então o `ToolBarManager` do GX tem hover e desabilitado no estilo Next.
- Plano B por **chave do comando**: se a imagem não for reconhecida, a ferramenta (`ToolBase.Key`) é procurada nos `<CommandDefinition id=… image=…>` dos `.package` carregados, e o ícone Next é escolhido pelo nome do recurso.
- Diagnóstico: ícones da barra/menus sem equivalente Next são listados no log (`Icones Next: N icone(s) da barra/menus SEM equivalente Next …`).

**Verificação:** `Test16-Toolbar.ps1` cria o `Artech.FrameworkDE.ToolBarManager` real, obtém as imagens pelo pipeline real do GX (`GxResourceManager` + `DefaultMenuResolver.GetImage`) com o processo em 125% de DPI e compara antes/depois/restaurado (`barra-principal-verificacao.png`). Com a build anterior: 0/16 trocados. Com a nova: 16/16 (Appearance direta e compartilhada), restaurado = original.

### 11.5 Hover dos botões no estilo Next (plano, sem brilho)

**Problema:** ao passar o mouse, os botões de barras/menus mostravam um degradê/brilho claro (amarelado/branco) do Office 2010 e os ícones claros ficavam ilegíveis. O ISL já definia o hover plano, mas o estilo `Office2010Button` do Infragistics ignora essas cores: o fundo recebe um *glow* e as bordas vêm das tabelas estáticas `Office2010ColorTable`/`Office2007ColorTable`.

**Correção (só em memória, reversível):**
- No filtro de desenho já instalado nas barras (`IconDrawFilter`), o novo `FlatButtons` pinta plano (pixels inteiros, cantos levemente arredondados) os botões de `UltraToolbarsManager`/`Artech.FrameworkDE.ToolBarManager` (botões, botões de estado, split/drop-down incluindo a seta, barra de menus), botões de legenda/fechar de painéis e abas e `UltraButton` dos diálogos: **hover** `#344860` + borda 1px `#4E6888`; **pressionado/aberto** `#1F4E79`; **marcado** `#2C3C50` + borda `#2C8DCE` (marcado+hover `#344860` + borda `#2C8DCE`). No estado normal nada muda.
- Rede de segurança: as cores de hover/pressionado/marcado/brilho das tabelas `Office2010ColorTable` e `Office2007ColorTable` passam para a mesma paleta (texto em hover `#F0F4FA`). As ToolStrips já usavam a tabela plana `DarkColorTable`.

**Verificação:** `Test17-Hover.ps1` cria o `ToolBarManager` real em 125% de DPI e força hover/pressionado/marcado (`hover-verificacao.png`). Pixels claros (>200) no botão em hover: 206 → 28 (só o ícone); botão com texto: 419 → 71; marcado+hover: 200 → 12. Fundo/borda medidos exatamente `#344860`/`#4E6888`, `#1F4E79`, `#2C3C50`/`#2C8DCE`.

### 11.6 WorkWithPlus: ícone de atributo/variável obrigatório

**Problema:** na árvore da instância do WorkWithPlus os atributos obrigatórios (`attributeReq`/`attributeReqEv3`) e variáveis obrigatórias (`variableReq`/`variableReqEv3`) eram um glifo vermelho sobre **quadrado branco**, diferente dos outros atributos.

**Correção (só em memória, reversível):** quando o `DVelop.Patterns.WorkWithPlus` é carregado, esses ícones são lidos (somente leitura) e reconhecidos por impressão digital; no desenho viram o **mesmo glifo do atributo/variável normal do GeneXus**, recolorido no vermelho de erro do GeneXus Next (Mercury dark `--color-icon-error-default` = `red-600` **#E35861**), mantendo alfa e sombreado, **fundo 100% transparente**. TableContent, Panel, Group, Table, User Control, Error viewer, variáveis e Transaction ficam como estavam; o atributo Hidden não tem ícone próprio (é o ícone normal + "(Hidden)"). O preview do WWP não é alterado. Nenhum arquivo do pacote é modificado.

**Verificação:** `Test18-Wwp.ps1` (UltraTree com os ícones reais, imagem direta e `ImageList`, 125% de DPI; `wwp-atributos-verificacao.png`): pixels claros no ícone obrigatório 76 → 0, glifo vermelho mantido; demais nós inalterados.

### 11.7 Linhas claras suavizadas e textos escuros clareados

**Problema:** sobravam linhas brancas/cinza-claras e textos/glifos pretos ou cinza-escuros:
- **barra principal:** a linha sob as bandas (`toolbarUnderline` **#ACAFB4**), as sombras de canto e a alça de arrastar vinham fixas das tabelas `Office2010/2007/2003ColorTable`;
- **popup "Add or Remove Buttons"** (QuickCustomize): o papel do ISL era transparente, então aparecia com fundo de sistema **#F0F0F0**;
- **Values Editor e listas parecidas** (`ListView` com `DrawDefault`/`GridLines`): cabeçalho claro, linhas da grade e retângulos brancos no início da linha, além da seleção azul/clara (`#0078D7`/`#F0F0F0`) e das barras de rolagem claras;
- **glifos monocromáticos pretos** (botões do editor, `PictureBox`, `ToolStrip`): ficavam quase invisíveis no fundo escuro.

**Correção (só em memória, reversível, sem tocar em binários nem nas cores globais do Windows):**
- **Tabelas do Infragistics:** linha das bandas e sombras de canto → `#333D47` (variações `#2E3742`/`#2A333F`). Alça → `#6A727E`/`#242D3C`. Log: `linhas/cantos/alca das barras suavizados: 18 cores`.
- **ISL em memória:** os papéis `QuickCustomizeToolbar`, `QatQuickCustomizeToolbar` e `AddRemoveToolsMenu` passam a fundo `#1F2734`, texto `#DCE2EB` e borda `#333D47`. Log: `Popup de personalizacao das barras escuro … 3 papeis`.
- **Filtro de desenho `LineGuard`** (no `IconDrawFilter` existente):
  - bordas cinza-claras → `#333D47`; separadores finos claros → `#2A3442`;
  - a seta Personalizar/overflow é desenhada em `#8A909B` (hover `#DCE2EB`);
  - log `LINHA CLARA corrigida (…)`.
- **ListView genérico (`LvDark`)**, que vale para todos os diálogos modais com essas listas:
  - cabeçalho `#242D3C`/`#DCE2EB`;
  - células escuras e grade desenhada em `#333D47` (a grade nativa é desligada);
  - retângulo branco do início da linha → `#181F2A` (glifo `#8A909B`);
  - seleção `#2C3C50` com texto `#F0F4FA`, também em listas não OwnerDraw (via `NM_CUSTOMDRAW`);
  - barras de rolagem no tema escuro do Windows (só naquela janela).
- **Campos no lugar** (editor de célula de uma linha) → `#1F2734`/`#DCE2EB`. Se o controle voltar sozinho a uma cor clara de sistema, ela é corrigida na hora (log `Campo voltou a cor clara…`).
- **Piso de texto (`TextFloor`):** contraste mínimo de 4,6:1. Texto acinzentado sobe até pelo menos `#9AA3AE` (desabilitado `#7F8894`). Vale para ISL, `ToolStrip`, `ComboBox`, `ListBox`, `TreeView`, `ListView`, `TextBox`, `UpDown` e `StatusBar`.
- **Glifos pretos (`MonoLift`):** imagens ≥70% escuras e sem saturação têm os pixels escuros trocados por `#DCE2EB`, mantendo alfa (há uma versão para desabilitado). Os glifos coloridos (vermelho, verde, azul) não mudam. Log: `Glifo escuro clareado`.
- **Start Page/navegador (Cef):** `color-scheme: dark` e barra de rolagem `#333D47` sobre `#11151C`, também em sub-frames (o preview do WWP e o Web Layout continuam excluídos).

**Verificação** (`Test19-Barras.ps1` e `Test20-Paineis.ps1`, 125% de DPI, DLL 10:25 × nova; imagem `linhas-textos-verificacao.png`):

| Medição | Antes (DLL 10:25) | Depois (DLL nova) |
|---|---|---|
| Barra: pixels claros | 2047, com a linha #ACAFB4 (317 px) | 1689, sem a linha; o que sobra são textos e ícones |
| Popup QuickCustomize: pixels >200 | 5572 de 6030 | 147 (só texto e seta) |
| Values Editor: pixels de linha clara | 8389 | 434 (polegar da rolagem e borda da marca verde) |
| Cabeçalho | #F0F0F0 | #242D3C / #DCE2EB |
| Grade | #F0F0F0 | #333D47 |
| Selecionada | #0078D7 | #2C3C50 / #F0F4FA |
| Editor no lugar | #11151C | #1F2734 / #DCE2EB |
| Barra de rolagem: pixels claros | 4562 | 0 |
| ListView comum: selecionada | #F0F0F0 | #2C3C50 / #F0F4FA |
| ListView comum: rolagem | 4664 | 0 |
| Glifo preto (Button/PictureBox/ToolStrip) | #253343 / #181F2A / #242D3C | #DCE2EB |
| Glifo vermelho | 33 px vermelhos | 33 (mantido) |

Os testes de regressão `Test13`, `Test14`, `Test16`, `Test17` e `Test18` deram resultado idêntico à versão 10:25.

**Ainda não verificado no GX real:**
- as linhas dos painéis encaixados (dock) e da grade de Propriedades;
- a rolagem da Start Page dentro de sub-frames;
- o texto do botão desabilitado `<..` (ficou `#969D98`, que já é legível).

### 11.8 Ícones de status na Navigation View e na Impact Analysis

**Problema:** os ícones de status (OK, aviso, erro, "=", "=" com aviso, "?") apareciam com um anel branco. As duas janelas são o mesmo controle do GeneXus (`Artech.Core.UI.Controls.ReportView`). Os status vêm de `SpecificationResult/ImpactAnalysisResult.CreateStatusImages()`, numa lista limpa de 32 bits.

**Causa:** o `TreeView.StateImageList` do WinForms não manda essa lista para o controle nativo. Ele copia as imagens para um `ImageList` interno novo (8 bits, 16×16, imagem 0 duplicada) e envia essa cópia com `TVM_SETIMAGELIST`/`TVSIL_STATE`. Em 8 bits a borda suavizada perde o alfa e vira um anel claro.

**Correção (só em memória e reversível, sem tocar em binários nem nas cores globais do Windows):**
- **Lista de estados nativa:** em cada `TreeView` com `StateImageList`, o GxDarkTheme monta uma lista própria de 32 bits no tamanho do GeneXus (20×20 a 125%) e a envia só para aquele controle. O handle interno do WinForms fica guardado. Se o WinForms montar outra cópia (handle recriado, lista trocada), a troca é refeita na passada seguinte. O `RestoreAll` devolve a lista original (`UpdateNativeStateImageList`). Log: `Icones de status: lista de estados nativa … 32 bits`.
- **Ícones Mercury coloridos:**

  | Status | Ícone |
  |---|---|
  | OK | `gx-test/result-ok` (#A1DAA5) |
  | Aviso | `gx-test/result-warning` (#FAB454) |
  | Erro | "X" do `gx-test/result-error` na cor `--icons06` #F4608D |

  O Mercury 1.5.0 não tem "igual/sem mudança", "igual com aviso" nem "especificação pendente". Esses três foram desenhados no mesmo estilo (círculo cheio com glifo vazado, geometria do `result-ok`):
  - "=" em `--icons03` #24A19A;
  - "=" com o selo `result-warning`;
  - "?" do `system/help` em `--icons08` #FAB454.

  Eles ficam em `next/derived.py`.
- **Mapa:** os mesmos desenhos são usados em `Artech.Genexus.UI.Resources.Images` (StatusOk/Warning/Error/Equal/EqualWarning/NotFound), `Core.UI` (StatusEqual/EqualWarning/Error/Warning), `Packages.GenexusUI` (Status*_small) e na barra/menu do ReportView (`errorStatus`, `sucessStatus`, `tmiOnlyUnchanged`).
  - Como o desenho de OK/aviso/erro do Output é idêntico pixel a pixel, o Output também passou dos ícones de contorno monocromáticos para os coloridos.
  - Se a impressão digital falhar, as chaves do GeneXus (`specreq`, `genreq`, `OK`…) escolhem o ícone.
- **Rede de segurança:** uma imagem de estado sem par Next passa por um de-matte. Os pixels claros de borda são recalculados como cor interna + alfa, contra o branco.

**Verificação** (`Test21-Status.ps1`, 125% de DPI, comctl32 v6 como no GeneXus, listas reais do `CreateStatusImages()` num `TreeView` sobre #181F2A; imagem `status-icones-verificacao.png`, zoom 3x):

| Medição | Antes (DLL 11:17) | Depois (DLL nova) |
|---|---|---|
| Lista de estados nativa | cópia do WinForms, 16×16, 8 bits | lista própria 32 bits, 20×20 |
| Pixels de anel claro junto ao fundo | 416 | 30 |
| De-matte sintético | 19 px | 0 px |

- Os 30 px que sobram são o miolo claro do ícone Next de transação/procedimento, e não borda.
- Depois de `RestoreAll`, a lista volta à cópia original do WinForms (7 imagens).
- `Test16`, `Test17`, `Test18`, `Test19` e `Test20` deram o mesmo resultado da DLL 11:17. O `Test13` rodou sem erro, com a troca nas barras restaurada (5/5), e agora mostra as famílias `gx-test/result-warning` e `derived/status-error` no Output.

**Ainda não verificado no GX real:**
- o `TreeViewMS` do GeneXus, porque o teste usou `TreeView` comum: o tipo não carrega fora do GX, mas herda de `TreeView`;
- os ícones de nível, For Each e índice dentro da Navigation View (já estão em lista de 32 bits, sem a cópia de 8 bits);
- o diálogo "View last import log".

### 11.9 Relatório da Navigation View com folha de estilo Next

- **Problema:** o relatório (`ReportView` → XSL `spec.xsl` → `GXChromiumBrowser`) usa `gxxml/genexus.css`. A inversão genérica do `DarkJs` deixava tudo com cara de "negativo" e transformava os links `.gxobj` (spans azul-escuros) em texto cinza.
- **Solução:** quando a página tem um `<link>` para `genexus.css`, o `DarkJs` injeta uma vez `<style id='gxNextReport'>` e pula a passada genérica:
  - fundo #11151C, texto #DCE2EB, cabeçalho #181F2A com linha inferior #2C8DCE, tabelas #1B2330, separadores #2A333F;
  - rótulos #8A909B, links/objetos #5BA7FF (hover #8CC2FF sublinhado), erros/inexistentes #E35861, avisos #E5A54B;
  - palavras-chave WHEN/OTHERWISE em #C792EA (o JS marca `data-gxn='kw'`, porque dividem a classe `.NavigationWarning` com "No index!").
- É só exibição: nada é gravado em arquivo e desligar a extensão volta ao CSS original. `tbllist.xsl` (lista de tabelas/Impact Analysis) e `IMPORT.XSL` também incluem `include.xsl` → `genexus.css` e recebem o mesmo estilo.
- **Verificação:** `navigation-report-verificacao.png` (Chrome offscreen, 125%). Ainda não conferido dentro do GX.

### 11.10 Ícone da aba Web Layout (parte default)

- **Problema:** `DocumentPartFrame` (aba de parte com padrão, ex.: Web Layout) compõe em `RefreshIcon` o raio `Images.Default` + sobreposição Enabled/Disabled numa lista de 8 bits, com bordas serrilhadas.
- **Solução (`GxIcons.PartIcons`):** em cada `TabControl` Crownwood, qualquer página que tenha um campo `bool? *IsDefault*` e um campo `ImageList` próprio recebe uma lista nova de 32 bits com o ícone Mercury:
  - parte default → `patterns-default-associated/default-associated-synchronized`;
  - parte editada → `patterns-default-associated/default-associated-modified`.
- A lista do GeneXus (`m_ImageList`) nunca é alterada; só troca a referência `ImageList` da aba. Quando `RefreshIcon` roda de novo (troca de estado), a aba volta a apontar para a lista do GX e a próxima passada (~0,7 s) reaplica o ícone do novo estado.
- **Reversível:** `RestoreAll` devolve a lista do GX e descarta a nossa; com `icons=false` ou `enabled=false` nada é trocado. Sem patch binário.
- **Verificação:** `Test22-WebLayout.ps1` (página falsa com os mesmos campos, 125% DPI) → `web-layout-verificacao.png`: listas próprias 32 bits 20×20, troca de estado em 1 passada, restauração para a lista 8 bits do GX.
- **Ainda não verificado no GX real:** o `DocumentPartFrame` verdadeiro, a troca de estado numa KB e outras partes com padrão.

### 11.11 Atribuição

> Ícones: **GeneXus Mercury** (`@genexus/mercury` 1.5.0), © GeneXus S.A., licenciado sob a Apache License 2.0 (https://www.apache.org/licenses/LICENSE-2.0). Os PNGs foram gerados a partir dos SVGs públicos do pacote e recoloridos conforme os tokens do Mercury. Três ícones de status (igual, igual com aviso, especificação pendente) e o "X" de erro rosa são derivados no mesmo estilo, com as cores da paleta do Mercury. O aviso de licença acompanha a DLL.

---

## 12. Alternar claro/escuro ao vivo (botão sol/lua)

Desde 28/10/2026, a extensão `ext\GxDarkTheme.dll` troca entre o modo claro e o escuro **sem reiniciar o GeneXus**, como nas outras IDEs. O código fica em `GxToggle.cs`, compilado na mesma DLL. Os PNGs do botão vão embutidos como recursos, então a pasta `ext\` continua só com os 4 arquivos.

### 12.1 Onde fica e como usar

- **Botão:** fica no fim da barra de menus principal, à direita, com 24×24 px a 125% DPI.
  - No escuro mostra um **sol** (clicar passa para o claro).
  - No claro mostra uma **lua** (clicar passa para o escuro).
  - O tooltip diz "Alternar para modo claro/escuro (Ctrl+Shift+F12)".
  - Se a janela estiver estreita demais e não sobrar espaço na barra, o botão some até haver espaço. O atalho continua valendo.
- **Atalho:** `Ctrl+Shift+F12`. Na abertura, a extensão verifica se alguma ferramenta do GeneXus já usa essa combinação. Se usar, o atalho do tema fica desligado (o log registra isso) e vale só o botão.
- **Tempo:** a primeira troca leva cerca de 0,6 a 0,75 s. As seguintes levam em média uns 120 ms para o claro e uns 155 ms para o escuro.

### 12.2 Persistência (`mode=`)

- O modo escolhido é gravado na linha `mode=dark` ou `mode=light` de `%APPDATA%\GeneXus\GeneXus\18\GxDarkTheme.settings`. É a única coisa que a extensão grava.
- Na próxima abertura, o GeneXus já começa no último modo usado.
- Com `mode=light`, a extensão carrega mas não aplica o tema. Ela só guarda os originais e mostra a lua.
- `enabled=false` continua desligando tudo, e `icons=false` continua desligando os ícones Next.

### 12.3 Estilo do ícone (`icone=`)

O padrão, quando o arquivo não tem a linha `icone=`, é a **opção B "Âmbar"** (escolhida a partir da prévia `sol-lua-preview.png`):
- sol preenchido âmbar `#E5A54B` (`#F0BB6A` no hover) na barra escura;
- lua ardósia `#3D4B5C` (`#2D3A48` no hover) na barra clara.

Para trocar, acrescente uma linha ao `GxDarkTheme.settings`. O botão lê a linha na abertura do GeneXus.

| Linha | Visual |
|---|---|
| *(nenhuma)* ou `icone=ambar` (também `âmbar` ou `b`) | B: sol âmbar / lua ardósia (padrão) |
| `icone=contorno` (também `a`) | A: sol e lua só em contorno, `#DCE2EB` no escuro e `#2D3A48` no claro |
| `icone=azul` (também `c`) | C: sol âmbar no escuro / lua azul `#1F63C9` no claro |

A chave `icon=` também é aceita.

### 12.4 Como volta ao claro sem reiniciar

- Antes da primeira troca, cada propriedade que o tema altera é anotada num diário (`Journal`) com o valor original do GeneXus.
- Ao voltar para o claro, o valor original é devolvido. Se o próprio GeneXus mudou o valor depois, o valor dele é mantido.
- Cores derivadas que o GeneXus calcula sozinho (abas Crownwood, tabelas de cor do Infragistics, ImageLists de 8 bits) voltam por cópias guardadas na abertura.
- Tudo acontece em memória e é reversível, sem patch binário e sem mexer em arquivos do GeneXus ou da KB.

**Volta ao vivo:**
- controles WinForms (fundo e texto);
- menus, barras e o estilo ISL do Infragistics (o original é recarregado);
- renderizador das ToolStrips;
- grades DevExpress;
- ícones Next, que voltam aos originais do GeneXus;
- abas Crownwood, incluindo os botões de seta e fechar;
- listas e listas de estado;
- barras de título e barras de rolagem (tema do Windows por janela);
- janelas abertas depois da troca, que já nascem no modo atual.

**Volta ao vivo, mas ainda não verificado no GeneXus real:**
- navegadores Cef/Monaco: a página recebe um script que desfaz o CSS injetado;
- documentação (MSHTML/wiki);
- designer de relatório (Layout de Procedure);
- editor de código (Actipro SyntaxEditor);
- barra de status, moldura e property grid.

Se alguma aba ficar com sobras do outro modo, **reabra a aba**. A aba nova nasce no modo atual. Abas web (Web Layout, prévias, wiki) e editores já abertos são os candidatos mais prováveis. A prévia de formulário do WorkWithPlus continua intocada, nos dois modos.

### 12.5 Verificação

- **`Test23-Alternar.ps1`:**
  - Usa uma janela falsa com Infragistics, DevExpress, Crownwood, listas e ToolStrips, a 125% DPI, e roda 20 ciclos claro↔escuro.
  - Resultado: 0 diferenças de propriedade nos dois modos, GDI/USER com delta 0 e diário estável (60 objetos).
  - As únicas diferenças de pixel são do retângulo de foco depois que o teste abre janelas.
  - Janelas novas abertas em cada modo ficam com 0 diferenças.
  - O teste roda sem linha `icone=` e confirma o padrão `ambar`.
- **`Test22-WebLayout.ps1`:** continua OK (ícone da parte default devolvido à lista de 8 bits do GX).
- **Prévias:** `sol-lua-preview.png` (opções A, B e C) e `alternar-tema-verificacao.png`.

### 12.6 Correções de 28/10 (três pontos do Gabriel)

DLL: `GxDarkTheme.dll`, 757.760 bytes, compilada em 28/10/2026 às 16:26:43 (horário de Brasília), MD5 `5064C09C9BE8A5FDE2E33B392D637339`.

1. **Botão sol/lua desalinhado.**
   - Causa: o botão ficava a 2 px da borda da barra de menu, cerca de 15 px fora da coluna do X da janela e do painel Properties.
   - Correção:
     - O centro do botão agora fica alinhado ao centro do botão Fechar da janela (via `WM_GETTITLEBARINFOEX`).
     - Na vertical, ele fica centralizado nos textos do menu.
     - Se o X não estiver disponível (janela sem borda ou minimizada), o botão volta para a margem.
     - A posição é recalculada no Resize, no SizeChanged e no DpiChanged do formulário.
   - Medido a 125%: dX=0 e dY=0 em D1, L1 e D21. Antes, dX era 15.
2. **Barra de abas marrom ou cinza.**
   - Causa: o TabControl Crownwood guarda `BackColor = SystemColors.Control` explícito, mas `ShouldSerialize` diz "não". Ao voltar ao claro, o diário fazia `ResetBackColor`, e a barra herdava a cor do pai (no GX, #535353). Depois `DefineBackColor` congelava essa cor.
   - No escuro seguinte, #535353 não é "claro", então nunca era escurecida de novo.
   - Correção:
     - O diário agora distingue cor ambiente de cor explícita (`Properties`/`PropBackColor`) e devolve o valor exato quando a cor era explícita.
     - `CrownOrig` tira uma foto da BackColor e dos campos de cor internos do Crownwood antes do primeiro tema e devolve tudo no claro.
     - No escuro, `CrownTabs` sempre força o Panel (#181F2A).
3. **Ícones e fundos do Output depois de alternar.**
   - Causa: os itens do ToolStrip do Output têm `BackColor = Transparent` explícito, com a mesma armadilha do Serialize. Ao voltar ao claro, ficavam com o fundo do escuro (#242D3C). Assim, o X vermelho, a lupa e os demais ícones apareciam sobre quadrados escuros, e as cores pareciam perdidas.
   - Corrigido pela mesma lógica de valor exato.
   - Teste com a barra copiada do GxOutput (mesmos ícones do recurso, mesmo `CustomToolStripRenderer`): L1 e L20 são idênticos a L0.

Verificação (`Test23`, controles reais a 125%):
- **DLL antiga:** 14 diferenças de propriedade a cada claro (crown2, InertButtons, itens do Output) e 1 a cada escuro (crown2 = #535353).
- **DLL nova:** 0 diferenças de propriedade em todos os ciclos. Os pixels ficam em 0, exceto o retângulo de foco nos ciclos 5 e 6, que abrem janelas.
- **Recursos:** GDI com delta -2, USER com delta 0.
- **Test22:** OK.
- **Imagem:** `tres-pontos-verificacao.png` (antes e depois).
- **Observação:** o GxOutput real (`Activator`) trava fora do GX, então o teste usa uma cópia fiel da barra (`T23_REALOUT=1` liga o real).
