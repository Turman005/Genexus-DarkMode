# GxDarkTheme: tema escuro para a IDE do GeneXus 18

Esta é uma extensão (pacote de interface) do GeneXus 18 que deixa a IDE escura, sem alterar nenhum arquivo do GeneXus.

A versão 2.0 **une dois projetos**:
- **GxDarkTheme**, de Gabriel Turman ([@Turman005](https://github.com/Turman005)): motor, ícones do GeneXus Next, botão sol/lua e diário de reversão.
- **Xpz.Dark**, de Filipe Castro ([@itsFilipe](https://github.com/itsFilipe)): os 7 temas, o `.isl` gerado e a migração da configuração.

| | |
|---|---|
| Temas | **GeneXus Next** (padrão), Ardósia, Grafite, Meia-noite, Ametista, Brasa, Âmbar, Oliva |
| Trocar tema | Ferramentas > **Tema escuro**, ou botão direito no sol/lua. A troca é ao vivo, sem reiniciar o GeneXus. |
| Claro/escuro | Botão sol/lua à direita da barra de menus, ou **Ctrl+Shift+F12**. O modo claro volta exatamente ao original. |
| Ícones | Ícones do GeneXus Next (Mercury) em todos os temas. Nos outros temas, eles recebem a matiz do tema. |
| Segurança | Segure **Shift** ao abrir o GeneXus para entrar no modo seguro. Se o GeneXus não terminar de abrir 3 vezes seguidas, a extensão abre no claro. |

Instruções para o usuário: veja **[LEIA-ME.md](LEIA-ME.md)**.

## Estrutura

```
src/            código da extensão (GxDarkTheme.dll, .NET Framework 4.8, C# 5)
  Nucleo/       Produto (nome/GUID), Compat, Tema (8 temas + função de cor), GeradorIsl (.isl em memória)
  Motor/        DarkEngine (aplica/reverte com diário), Alternar (sol/lua), TrocaDeTema (troca ao vivo, migração, modo seguro)
  Icones/       troca de ícones Mercury/Next + tingimento por tema
  Interface/    menu Ferramentas > Tema escuro e a janela de Configurações
resources/      ícones (Mercury, Apache-2.0) e roles.txt (mapa de cores do .isl)
installer/      Setup-GxDarkTheme.exe (assistente em pt-BR): Setup.cs (interface) + Operacoes.cs (instalar/desinstalar)
scripts/        Build.ps1 (Windows) e build.sh (Linux/macOS)
testes/box/     testes que rodam sem GeneXus (temas, .isl, contraste)
testes/instalador/  testes do instalador numa árvore falsa do GeneXus
testes/ide/     Test23 (20 ciclos claro/escuro, também com troca de tema) e Test24 (editores); abrem janelas
tools/previa.py prévias PNG de cada tema
docs/           COMO-FOI-FEITO.md e o histórico dos dois projetos
```

## Compilar

**No Windows**, com o GeneXus 18 instalado (as DLLs do GeneXus servem só de referência e não são copiadas):

```powershell
powershell -ExecutionPolicy Bypass -File scripts\Build.ps1
# opções: -GeneXus "C:\Program Files (x86)\GeneXus\GeneXus18"  -Compat 143920  -UsarCsc  -SemInstalador
```

O resultado fica em `dist\`: `GxDarkTheme.dll`, `Setup-GxDarkTheme.exe` e `SHA256SUMS.txt`.

- O build usa o `dotnet` (SDK 6 ou mais novo) se ele estiver instalado. Se não estiver, usa o `csc.exe` do próprio .NET Framework.
- Com `dotnet`, o build é determinístico: o mesmo código e as mesmas referências geram os mesmos bytes.

**No Linux/macOS:** `GXREFS=/pasta/com/as/dlls/do/GeneXus scripts/build.sh`

## Testes

```bash
# sem GeneXus (Linux/Windows, .NET 8): temas, identidade do .isl Next, contraste
cd testes/box && dotnet run -c Release -- <GenexusOffice2010Silver.isl> <GxDarkTheme.isl v1 de referência> ../../resources/roles.txt <saída>
cd testes/instalador && dotnet run -c Release      # instalar/reinstalar/desinstalar numa árvore falsa
```

```powershell
# com GeneXus 18 (Windows). Abre janelas de teste; não mexe na instalação. Copie bin\GxDarkTheme.dll para testes\ide antes.
powershell -ExecutionPolicy Bypass -File testes\ide\Test23-Alternar.ps1          # 20 ciclos: esperado 0 diferenças
$env:T23_TEMAS='1'; powershell -ExecutionPolicy Bypass -File testes\ide\Test23-Alternar.ps1   # idem, passando pelos 7 temas
powershell -ExecutionPolicy Bypass -File testes\ide\Test24-Editores.ps1
```

## Renomear o produto

O nome, o GUID do pacote, o arquivo de configuração e a chave do desinstalador ficam em `src/Nucleo/Produto.cs`.

Para trocar o nome da DLL, mude também `<AssemblyName>` em `src/GxDarkTheme.csproj`.

> **Atenção:** se o GUID (`PacoteGuid`) mudar, o GeneXus passa a tratar a extensão como outro pacote.

## Regras do projeto

- Nunca alterar binários do GeneXus ou de terceiros, nem as KBs.
- Tudo acontece em memória e é reversível. O diário devolve cada valor original.
- Nenhuma cor global do Windows é alterada.
- As cores do GeneXus Next aparecem só no tema Next.
- Nos temas escuros não fica nada branco ou claro. A exceção é a prévia de formulário do WorkWithPlus, que mantém as cores originais.

## Licença e créditos

O código é MIT (`LICENSE`), © 2026 Turman005 e Filipe Castro.

Os ícones vêm de [@genexus/mercury](https://www.npmjs.com/package/@genexus/mercury) 1.5.0, sob a licença Apache-2.0, com modificações; veja [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

Este é um projeto independente, sem vínculo com a GeneXus S.A.
