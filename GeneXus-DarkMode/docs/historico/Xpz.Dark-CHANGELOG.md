# Changelog — Xpz.Dark

## 0.13.6

- Menu do botão direito no título dos painéis (Properties, Output: Dockable, Hide...) escuro,
  desenhado pelo Windows no modo escuro.
- Lista de barras (botão direito na barra de ferramentas) escura: o menu e os itens dela têm
  papéis próprios no `.isl` (`ToolbarListMenu`, `MenuItemToolbarList`) que o original não declara.
- Instalador: a cópia de reserva do `.isl` passou a ser opcional; o tema é sempre gerado na
  máquina, a partir do `.isl` da instalação.

## 0.13.5

- Moldura do Properties (e da tela *Tools > Options*) na cor do tema: a borda simples pedida
  ao Windows saía branca.

## 0.13.4

- History: a grade não pisca mais branca ao receber os dados (a `GxUltraGrid` repõe a
  aparência clara no `InitializeLayout`; a extensão reaplica no mesmo instante).
- Corrigido: menu do botão direito nas abas com itens pretos que ficavam cinza após o mouse
  passar (regressão da 0.13.3; cor totalmente transparente no `.isl` volta a ser opaca).

## 0.13.3

- History: linhas selecionadas visíveis (Ctrl+clique em duas revisões para comparar). O `.isl`
  original usa cores semitransparentes nas grades e a transparência passou a ser preservada.

## 0.13.2

- Letra legível no item de menu com o mouse em cima e no item marcado (tema atual).
- Corrigidos os ícones da moldura (fechar/alfinete) que não eram mais clareados.

## 0.13.1

- Compatível com o GeneXus 18 U14 (assemblies ofuscados): os membros internos passaram a ser
  achados pelo tipo e pela assinatura, não pelo nome.

## 0.13.0

- Comparador de versões (*History > Compare*) escuro: árvore de diferenças, editores de
  diferença e seletor de revisão, com o tom das cores de *Tools > Options > Comparer*.

## 0.12.x

- Corrigido o erro "Cross-thread operation not valid" em *Tools > Options* (a subclasse de
  janela era solta cedo demais).
- Primeiras versões públicas: 7 temas, tela de configuração, modo seguro, disjuntor por parte,
  checagem de compatibilidade e testes fora do GeneXus.
