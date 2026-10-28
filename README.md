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

Arquivo pra executar: Genexus18 - Ativar Dark Mode.cmd 

Faz uma copia da pasta dentro do PC e roda o cmd. Após isso vai ser 
criada uma extensão no Gx com o nome GxDarkTheme, quando quiser deixar
 branco é só desativar e aplicar.
