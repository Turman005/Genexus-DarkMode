# Xpz.Dark — tema escuro para o IDE do GeneXus 18

Extensão (package) do GeneXus 18 que deixa o IDE escuro: moldura, menus, painéis, abas,
grades, árvores, Properties, editor de código, comparador de versões e páginas web internas
(Start Page, Output).

> Projeto independente, **sem vínculo com a GeneXus S.A.** GeneXus é marca da GeneXus S.A.
> Alternativa ao **GxDarkTheme** (pasta `GeneXus-DarkMode/` deste repositório). **Instale só
> uma das duas**: elas pintam os mesmos controles. Se a Xpz.Dark encontrar a `GxDarkTheme.dll`
> na pasta `Packages`, ela fica desligada (modo seguro) e avisa na tela de configuração.

## Recursos

- **7 temas trocáveis na hora** pelo menu *Tools > Tema escuro*: Ardósia, Grafite, Meia-noite,
  Ametista, Brasa, Âmbar e Oliva, ou cores personalizadas.
- **Tela de configuração** (*Tools > Tema escuro > Configurações do tema...*) com prévia do
  editor, liga/desliga por parte e quadro de saúde da extensão.
- **Tema gerado na máquina:** a cada abertura do GeneXus, o tema da moldura é gerado **em
  memória** a partir do `GenexusOffice2010Silver.isl` da própria instalação. Nenhuma cópia
  derivada do arquivo da GeneXus é gravada ou distribuída.
- **Comparador de versões** (*History > Compare*) escuro, com o tom das cores escolhidas em
  *Tools > Options > Comparer*, sem gravar nada nas preferências.
- **Robustez:**
  - **modo seguro:** segure **Shift** ao abrir o GeneXus e a extensão não faz nada naquela
    sessão; ela também entra sozinha em modo seguro se a abertura anterior foi interrompida;
  - **disjuntor por parte:** se uma parte falhar, só ela é desligada (com registro no log);
  - **checagem de compatibilidade** na abertura (29 pontos): o que mudar por dentro de uma
    versão nova do GeneXus é detectado e a parte correspondente fica desligada.
- **Nada gravado na KB** e **nenhum binário alterado**: tudo é feito em memória (reflexão,
  eventos, subclasse de janela).

Testada no GeneXus 18 **U10** e **U14** (mesmas versões de Infragistics 11.1, DevExpress 7.3 e
Actipro), no Windows 10/11.

## Instalação

Requisitos: GeneXus 18, Windows 10 1903 ou mais novo, PowerShell 7 (`pwsh`) e permissão de
administrador (a DLL vai para a pasta `Packages` do GeneXus).

1. Baixe o zip da versão em **Releases** e extraia numa pasta (ou compile, veja abaixo).
2. **Feche o GeneXus.**
3. Na pasta extraída, rode como administrador:

   ```powershell
   pwsh -NoProfile -File .\Install-Dark.ps1 -Versao U10,U14
   ```

   - sem `-Versao`, instala só na U10; `-Todas` instala em todas as GeneXus 18 encontradas;
   - o instalador **acrescenta** a entrada da Xpz.Dark ao catálogo de packages, sem
     reconstruí-lo (o catálogo é compartilhado entre as versões instaladas).
4. Abra o GeneXus.

**Desligar ou remover:**

- `Install-Dark.ps1 -Off`: a extensão continua instalada e não faz nada (não precisa de admin);
- `Install-Dark.ps1 -Encerrar`: remove a DLL de todas as versões, a configuração e a entrada
  da Xpz.Dark no catálogo (só ela).

A configuração e os logs ficam em `%LOCALAPPDATA%\Xpz.Dark\` (`dark.ini`, `dark.log`).

## Compilação

```powershell
pwsh -NoProfile -File .\Build-Dark.ps1 -GxDir 'C:\Program Files (x86)\GeneXus\GeneXus18-U10' -Csc '<caminho do csc.exe>'
```

- Gera `Xpz.Dark.dll` (x86, .NET Framework 4.x) a partir de `src\`.
- Referencia as DLLs `Artech.*` da instalação do GeneXus indicada em `-GxDir`. Infragistics,
  DevExpress e Actipro são acessados por reflexão, sem referência de compilação.
- O `csc.exe` padrão é o do Visual Studio Build Tools; qualquer compilador C# para .NET
  Framework 4.x serve.

## Testes

```powershell
pwsh -NoProfile -File .\Testar-Dark.ps1
```

- Os testes rodam **fora do GeneXus**: carregam a DLL e os controles reais das bibliotecas
  (Infragistics, DevExpress, Actipro, VisualHint) num formulário de teste.
- Há verificações (cores, contraste WCAG de todos os temas, robustez, compatibilidade) e
  **comparação de fotos** com `testes\referencia\`.
- Os testes **usam a tela**: abrem janelas por cima, movem o mouse e fotografam. Deixe a
  tela livre (Win+D) enquanto rodam.
- `-Somente TesteX,TesteY` roda só alguns testes; `-NovaReferencia` grava as fotos atuais
  como referência. Fotos que dependem da máquina (ex.: a aba Diagnóstico, que mostra a pasta
  do perfil) não estão em `referencia\` e não são comparadas.

## Estrutura

```
gx-dark\
├─ src\
│  ├─ Nucleo\          ciclo de vida, paleta, temas e gerador do .isl, robustez
│  ├─ Adaptadores\     uma pasta por biblioteca (Infragistics, DevExpress, Actipro,
│  │                   VisualHint, Crownwood, WinForms, Win32, GeneXus, Chromium...)
│  ├─ Interface\       tela de configuração
│  └─ Diagnostico\     censo, diagnóstico e auditoria do que ficou claro
├─ testes\             testes fora do GeneXus + fotos de referência
├─ Build-Dark.ps1      compila
├─ Install-Dark.ps1    instala, desliga ou remove
├─ Testar-Dark.ps1     roda os testes
├─ GeneXusPackage.package   manifesto do package
└─ dark.ini            configuração padrão
```

## Limitações conhecidas

- Ainda claros:
  - aba **Documentation** (MSHTML) e o menu do botão direito dela;
  - designer de relatório (XtraReports), cabeçalho de ListView e `MessageBox`;
  - o menu *Add or Remove Buttons* das barras de ferramentas;
  - a faixa-resumo à esquerda do comparador;
  - as duas linhas de 1 px no título dos painéis (Properties, Output), desenhadas à mão pelo
    próprio GeneXus com cor fixa;
  - o aviso "Opening Knowledge Base", as caixas desativadas da barra de ferramentas e o
    quadradinho "Is Collection" da aba Variables.
- **De propósito**, a área de desenho do **Web Layout** e o **Themes Preview** ficam como
  estão: mostram o que o site vai exibir.

## Licença

MIT (arquivo `LICENSE` na raiz do repositório). Avisos de terceiros em `THIRD-PARTY-NOTICES.md`.
