# GeneXus 18 — Dark Mode

Duas extensões **independentes** de tema escuro para o IDE do GeneXus 18. Cada uma tem a sua
pasta e a sua documentação. **Instale só uma delas**: as duas pintam os mesmos controles.

> Projetos independentes, **sem vínculo com a GeneXus S.A.** GeneXus e Mercury são marcas da
> GeneXus S.A.

| | **GxDarkTheme** | **Xpz.Dark** |
|---|---|---|
| Pasta | [`GeneXus-DarkMode/`](GeneXus-DarkMode/) | [`gx-dark/`](gx-dark/) |
| Visual | Paleta do GeneXus Next, com os **ícones do Next** (Mercury) | **7 temas** trocáveis na hora (Ardósia, Grafite, Meia-noite, Ametista, Brasa, Âmbar, Oliva) ou cores personalizadas |
| Instalar | Rode `GeneXus18 - Ativar Dark Mode.cmd` | `Install-Dark.ps1 -Versao U10,U14` |
| Desligar | `GeneXus18 - Desativar Dark Mode.cmd`, ou `enabled=false` no arquivo de configuração | Segurar **Shift** ao abrir o GeneXus (modo seguro), `Install-Dark.ps1 -Off` ou `-Encerrar` |
| Áreas a mais | Aba Documentation, designer de relatório, cabeçalho de ListView | Comparador de versões, tela de configuração, checagem de compatibilidade da versão |
| Documentação | [Como foi feito](GeneXus-DarkMode/COMO-FOI-FEITO-DarkMode.md) · [Readme](GeneXus-DarkMode/Readme.md) | [README](gx-dark/README.md) · [CHANGELOG](gx-dark/CHANGELOG.md) |

Em comum: as duas rodam **dentro do GeneXus**, em memória. Não gravam nada na KB, não alteram
DLL/EXE do GeneXus nem de terceiros e não mudam cores globais do Windows.

## Licença

- Código: **MIT** ([LICENSE](LICENSE)).
- Arquivos de terceiros (ex.: ícones do `@genexus/mercury`, Apache-2.0): continuam sob as
  licenças originais, listadas em [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
