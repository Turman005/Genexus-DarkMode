<#
.SYNOPSIS
    Instala, desliga ou remove o Xpz.Dark (tema escuro do IDE do GeneXus 18).

.DESCRIPTION
    O que faz:
      1. copia Xpz.Dark.dll  ->  <GeneXus18-Uxx>\Packages\   (em cada versao escolhida)
      2. copia tema.isl      ->  %LOCALAPPDATA%\Xpz.Dark\     (reserva opcional: o tema e gerado na hora)
      3. copia dark.ini      ->  %LOCALAPPDATA%\Xpz.Dark\     (so se ainda nao existir)
      4. ACRESCENTA a entrada do Xpz.Dark ao catalogo de packages

    VERSOES. O padrao e so a U10. Outras versoes precisam ser pedidas por nome
    (-Versao U14) ou com -Todas. A mesma DLL serve para todas as GeneXus 18: as
    bibliotecas de que ela depende tem a mesma versao (conferido U10 x U14 em 28/10);
    o que mudar por dentro de uma versao nova a propria extensao detecta na abertura
    (Tools > Tema escuro > Configuracoes > Diagnostico > Compatibilidade).

    CATALOGO. C:\ProgramData\GeneXus\GeneXus\18\packages.<n>.xml e COMPARTILHADO
    entre as versoes instaladas. Por isso o instalador ACRESCENTA uma entrada em vez
    de apagar o catalogo (quem reconstroi define o conteudo: a U10 reconstruiria sem os
    packages que so existem na U14). A entrada vale para todas as versoes; numa versao
    sem a DLL na Packages ela nao tem efeito.

    SEGURANCA PARA QUEM USA. A extensao nao abre nem grava KB - so muda cores. Se algo
    der errado ao abrir o GeneXus: segure Shift ao abrir (modo seguro), ou rode -Off.

.PARAMETER Versao
    Versoes do GeneXus 18 onde instalar, pelo sufixo da pasta: U10, U14... Padrao: U10.

.PARAMETER Todas
    Instala em todas as GeneXus 18 encontradas em "C:\Program Files (x86)\GeneXus".

.PARAMETER Off
    Interruptor: apaga o dark.ini. A extensao continua instalada e nao faz nada.
    NAO precisa de admin.

.PARAMETER ResetConfig
    Sobrescreve o dark.ini com o do pacote (por padrao o seu e preservado).

.PARAMETER Encerrar
    Remove a DLL de TODAS as versoes, a configuracao e a NOSSA entrada do catalogo - so ela.

.EXAMPLE
    .\Install-Dark.ps1                  # so a U10
.EXAMPLE
    .\Install-Dark.ps1 -Versao U10,U14
.EXAMPLE
    .\Install-Dark.ps1 -Encerrar
#>
[CmdletBinding()]
param(
    [string[]] $Versao = @('U10'),
    [switch] $Todas,
    [string] $Raiz = 'C:\Program Files (x86)\GeneXus',
    [switch] $ResetConfig,
    [switch] $Off,
    [switch] $Encerrar
)

$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
# Com "pwsh -File", "-Versao U10,U14" chega como UM texto ("U10,U14"): separa aqui.
$Versao = @($Versao | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })

function Ok($m)    { Write-Host "  [ok] $m"  -ForegroundColor DarkGreen }
function Info($m)  { Write-Host "  ---- $m"  -ForegroundColor Gray }
function Aviso($m) { Write-Host "  [!!] $m"  -ForegroundColor Yellow }
function Parar($m) { Write-Host ""; Write-Host "ABORTADO: $m" -ForegroundColor Red; exit 1 }

Write-Host ""
Write-Host "Xpz.Dark - tema escuro do IDE do GeneXus 18" -ForegroundColor Cyan

$cfgDir  = Join-Path $env:LOCALAPPDATA 'Xpz.Dark'
$cfgIni  = Join-Path $cfgDir 'dark.ini'
$cfgIsl  = Join-Path $cfgDir 'tema.isl'
$cfgLog  = Join-Path $cfgDir 'dark.log'
$catDir  = Join-Path $env:ProgramData 'GeneXus\GeneXus\18'
$ASM     = 'Xpz.Dark.dll'
$TIPO    = 'Xpz.Dark.DarkPackage'

# ---------------------------------------------------------------- interruptor
if ($Off) {
    if (Test-Path $cfgIni) { Remove-Item $cfgIni -Force; Ok "desligado (dark.ini removido)" }
    else { Info "ja estava desligado" }
    Info "a extensao continua instalada, mas nao aplica nada (o menu Tools > Tema escuro religa)"
    Write-Host ""
    exit 0
}

# ------------------------------------------------------------ versoes do GeneXus
function Get-Instalacoes {
    if (-not (Test-Path $Raiz)) { return @() }
    @(Get-ChildItem $Raiz -Directory -Filter 'GeneXus18*' |
      Where-Object { Test-Path (Join-Path $_.FullName 'GeneXus.exe') } |
      ForEach-Object {
          $suf = if ($_.Name -match '(U\d+)$') { $Matches[1].ToUpper() } else { $_.Name }
          [pscustomobject]@{ Nome = $suf; Pasta = $_.FullName
                             Versao = (Get-Item (Join-Path $_.FullName 'GeneXus.exe')).VersionInfo.FileVersion }
      })
}

# ------------------------------------------------------------ catalogo: util
function Get-Catalogo {
    $f = Get-ChildItem $catDir -File -ErrorAction SilentlyContinue |
         Where-Object { $_.Name -match '^packages\.(\d+)\.xml$' } | Select-Object -First 1
    if (-not $f) { Parar "catalogo de packages nao encontrado em $catDir (abra o GeneXus uma vez antes)" }
    $bytes  = [System.IO.File]::ReadAllBytes($f.FullName)
    $temBom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    $texto  = (New-Object System.Text.UTF8Encoding($false)).GetString($bytes)
    if ($temBom) { $texto = $texto.Substring(1) }
    [pscustomobject]@{ Caminho = $f.FullName; Nome = $f.Name; Texto = $texto; TemBom = $temBom
                       Compat = [int]([regex]::Match($f.Name, '\d+').Value) }
}

function Save-Catalogo($cat, [string] $texto) {
    [System.IO.File]::WriteAllText($cat.Caminho, $texto, (New-Object System.Text.UTF8Encoding($cat.TemBom)))
}

function Get-Retrato([string] $texto) {
    $ents = [regex]::Matches($texto, '(?s)<InstalledPackage>.*?</InstalledPackage>')
    $ui = 0
    foreach ($e in $ents) {
        if ($e.Value -match '<IsUIPackage>true</IsUIPackage>' -and $e.Value -match '<Enabled>true</Enabled>') { $ui++ }
    }
    [pscustomobject]@{
        Total   = $ents.Count
        UiAtivo = $ui
        GXquery = $texto -match 'GXquery'
        Nosso   = $texto -match [regex]::Escape("<Assembly>$ASM</Assembly>")
    }
}

function Write-Retrato($rotulo, $r) {
    Info ("{0,-8} {1} packages, {2} de UI ativos, GXquery {3}, Xpz.Dark {4}" -f $rotulo, $r.Total, $r.UiAtivo,
        $(if ($r.GXquery) { 'presente' } else { 'AUSENTE' }), $(if ($r.Nosso) { 'presente' } else { 'ausente' }))
}

function Exigir-Admin {
    $me = [Security.Principal.WindowsIdentity]::GetCurrent()
    if (-not (New-Object Security.Principal.WindowsPrincipal($me)).IsInRole(
            [Security.Principal.WindowsBuiltInRole]::Administrator)) {
        Parar "precisa de admin (Packages fica em Program Files e o catalogo foi gravado por processo elevado)."
    }
    Ok "sessao elevada"
}
function Exigir-IdeFechado {
    $ide = @(Get-Process -Name 'GeneXus' -ErrorAction SilentlyContinue)
    if ($ide.Count -gt 0) { Parar "GeneXus esta rodando (PID $($ide[0].Id)). Feche todos os GeneXus." }
    Ok "nenhum GeneXus aberto"
}

$instalacoes = Get-Instalacoes
if ($instalacoes.Count -eq 0) { Parar "nenhuma GeneXus 18 encontrada em $Raiz" }

# ------------------------------------------------------------------- encerrar
if ($Encerrar) {
    Write-Host ""
    Write-Host "Removendo o Xpz.Dark" -ForegroundColor Cyan
    Exigir-Admin
    Exigir-IdeFechado

    foreach ($i in $instalacoes) {
        $dll = Join-Path $i.Pasta "Packages\$ASM"
        if (Test-Path $dll) { Remove-Item $dll -Force; Ok "extensao removida da $($i.Nome)" }
    }
    foreach ($a in $cfgIni, $cfgIsl) { if (Test-Path $a) { Remove-Item $a -Force } }
    Ok "configuracao removida (os logs ficam, sao so registro)"

    $cat = Get-Catalogo
    $antes = Get-Retrato $cat.Texto
    $padrao = '(?s)<InstalledPackage>(?:(?!</InstalledPackage>).)*?<Assembly>' + [regex]::Escape($ASM) + '</Assembly>.*?</InstalledPackage>'
    $novo = [regex]::Replace($cat.Texto, $padrao, '')
    if ($novo -ne $cat.Texto) { Save-Catalogo $cat $novo; Ok "entrada do Xpz.Dark tirada do catalogo" }
    else { Info "o catalogo nao tinha a entrada do Xpz.Dark" }
    $depois = Get-Retrato (Get-Catalogo).Texto
    Write-Retrato 'antes:' $antes
    Write-Retrato 'depois:' $depois

    Write-Host ""
    Write-Host "Pronto. O GeneXus volta ao visual original." -ForegroundColor Cyan
    Write-Host ""
    exit 0
}

# ------------------------------------------------------------- alvos
if ($Todas) { $alvos = $instalacoes }
else {
    $alvos = @()
    foreach ($v in $Versao) {
        $achou = $instalacoes | Where-Object { $_.Nome -eq $v.ToUpper() }
        if (-not $achou) { Parar "GeneXus 18 $v nao encontrada. Disponiveis: $(($instalacoes | ForEach-Object Nome) -join ', ')" }
        $alvos += $achou
    }
}
foreach ($a in $alvos) {
    Ok "alvo: $($a.Nome)  $($a.Pasta)  (v$($a.Versao))"
    # Duas extensoes de tema escuro brigam pelas mesmas cores. A Xpz.Dark sai do caminho
    # sozinha (modo seguro), mas e melhor avisar ja na instalacao.
    if (Test-Path (Join-Path $a.Pasta 'Packages\GxDarkTheme.dll')) {
        Aviso "a $($a.Nome) tem a GxDarkTheme (outro tema escuro) instalada: com as duas, a Xpz.Dark fica desligada (modo seguro). Use uma so."
    }
}
$fora = @($instalacoes | Where-Object { $alvos.Nome -notcontains $_.Nome })
foreach ($f in $fora) { Info "nao instalado em: $($f.Nome) (use -Versao $($f.Nome) ou -Todas)" }
Exigir-Admin
Exigir-IdeFechado

# ------------------------------------------------------------------ arquivos
$dll = Join-Path $here $ASM
# Reserva OPCIONAL: o tema e gerado na abertura do GeneXus, em memoria, a partir do .isl
# da propria instalacao. Este .isl fixo so e usado se a geracao falhar - e nao vai no
# repositorio publico (e derivado do .isl da GeneXus).
$isl = Join-Path $here 'tema-escuro.isl'
$ini = Join-Path $here 'dark.ini'
foreach ($a in $dll, $ini) { if (-not (Test-Path $a)) { Parar "$a nao existe. Rode .\Build-Dark.ps1 antes." } }
$versaoDll = [Reflection.AssemblyName]::GetAssemblyName($dll).Version
Ok "pacote: Xpz.Dark $versaoDll"

if (-not (Test-Path $cfgDir)) { New-Item -ItemType Directory -Path $cfgDir -Force | Out-Null }
# o log anterior vira .old: a nova sessao comeca limpa, sem perder o registro de antes
if (Test-Path $cfgLog) { Move-Item $cfgLog "$cfgLog.old" -Force }

if (Test-Path $isl) { Copy-Item $isl $cfgIsl -Force }
else { Info "sem .isl de reserva: o tema e gerado na abertura a partir do .isl do GeneXus" }
if ((Test-Path $cfgIni) -and -not $ResetConfig) { Info "dark.ini preservado (use -ResetConfig para sobrescrever)" }
else { Copy-Item $ini $cfgIni -Force; Ok "dark.ini copiado" }

foreach ($a in $alvos) {
    Copy-Item $dll (Join-Path $a.Pasta "Packages\$ASM") -Force
    Ok "extensao copiada para a $($a.Nome)"
}

# ------------------------------------------------- catalogo: ACRESCENTAR entrada
$cat   = Get-Catalogo
$antes = Get-Retrato $cat.Texto

# Copia intocada do catalogo de antes da primeira instalacao. Feita UMA vez: uma
# copia refeita a cada instalacao ja conteria a nossa entrada e nao desfaria nada.
$pristino = Join-Path $here 'catalogo-antes-do-dark.xml'
if (-not (Test-Path $pristino)) { Copy-Item $cat.Caminho $pristino -Force; Ok "catalogo original guardado: catalogo-antes-do-dark.xml" }

if ($antes.Nosso) {
    Info "o catalogo ja tem a entrada do Xpz.Dark"
} else {
    if ($cat.Texto -notmatch '</Packages>') { Parar "catalogo sem </Packages> - formato inesperado, nada foi alterado no catalogo" }
    # Mesmo formato da entrada que o proprio GeneXus grava para as extensoes.
    $entrada = "<InstalledPackage><IsUIPackage>true</IsUIPackage><Assembly>$ASM</Assembly>" +
               "<Type>$TIPO</Type><Enabled>true</Enabled><ManualDisabled>false</ManualDisabled>" +
               "<CompatibilityVersion>$($cat.Compat)</CompatibilityVersion></InstalledPackage>"
    $idx  = $cat.Texto.LastIndexOf('</Packages>')
    $novo = $cat.Texto.Substring(0, $idx) + $entrada + $cat.Texto.Substring($idx)
    Save-Catalogo $cat $novo
    Ok "entrada do Xpz.Dark acrescentada ao catalogo"
}

$depois = Get-Retrato (Get-Catalogo).Texto
Write-Retrato 'antes:' $antes
Write-Retrato 'depois:' $depois

# Conferencia: so pode ter mudado a nossa entrada.
$esperadoTotal = $antes.Total + $(if ($antes.Nosso) { 0 } else { 1 })
if ($depois.Total -ne $esperadoTotal -or -not $depois.Nosso -or ($antes.GXquery -and -not $depois.GXquery)) {
    Aviso "o catalogo nao ficou como esperado - restaurando a copia de antes"
    Copy-Item $pristino $cat.Caminho -Force
    Parar "catalogo restaurado. A extensao nao esta ativa."
}
Ok "catalogo conferido: so a nossa entrada mudou"

Write-Host ""
Write-Host "Pronto. Abra o GeneXus ($(($alvos | ForEach-Object Nome) -join ', '))." -ForegroundColor Cyan
Write-Host "  Tema e configuracoes:   menu Tools > Tema escuro"
Write-Host "  Se algo ficar estranho: segure Shift ao abrir o GeneXus (modo seguro)"
Write-Host "  Desligar sem admin:     .\Install-Dark.ps1 -Off"
Write-Host "  Remover tudo:           .\Install-Dark.ps1 -Encerrar"
Write-Host "  Registro:               $cfgLog"
Write-Host ""
