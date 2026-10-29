<#
.SYNOPSIS
    Compila e roda os testes da Xpz.Dark fora do GeneXus.

.DESCRIPTION
    Cada teste e um .exe pequeno (testes\*.cs) que carrega a Xpz.Dark.dll com as
    bibliotecas da instalacao do GeneXus e confere um pedaco: cores das janelas, menu,
    temas, .isl, abas, rolagens, robustez...

    Testes de FOTO gravam imagens em testes\saida\ para conferir a olho; os de
    VERIFICACAO saem com codigo 0 so se tudo passou.

    Alguns testes mostram janelas na tela por ~1 s (fotografam a tela de verdade).
    O TesteMenu mexe no dark.ini do usuario e o restaura no fim.

.PARAMETER GxDir
    Instalacao do GeneXus cujas bibliotecas os testes usam.

.PARAMETER Somente
    Nomes de testes (sem .cs) para rodar so alguns.

.PARAMETER NovaReferencia
    Grava as fotos desta rodada como a nova referencia do teste por foto (depois de
    conferir a olho que a mudanca visual e a desejada).
#>
[CmdletBinding()]
param(
    [string] $GxDir = 'C:\Program Files (x86)\GeneXus\GeneXus18-U10',
    [string[]] $Somente,
    [switch] $NovaReferencia,
    [string] $Csc = "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
$here   = Split-Path -Parent $MyInvocation.MyCommand.Path
$fontes = Join-Path $here 'testes'
$bin    = Join-Path $fontes 'bin'
$saida  = Join-Path $fontes 'saida'
$dll    = Join-Path $here 'Xpz.Dark.dll'
foreach ($d in $bin, $saida) { if (-not (Test-Path $d)) { New-Item -ItemType Directory -Path $d | Out-Null } }
if (-not (Test-Path $dll)) { throw "Xpz.Dark.dll nao existe. Rode .\Build-Dark.ps1 antes." }

function Gx([string[]] $nomes) { $nomes | ForEach-Object { "-r:" + (Join-Path $GxDir "$_.dll") } }
$ig   = 'Infragistics2.Win.v11.1', 'Infragistics2.Shared.v11.1'
$base = '-r:System.Windows.Forms.dll', '-r:System.Drawing.dll', '-r:System.Core.dll', '-r:System.Xml.dll', '-r:Microsoft.CSharp.dll'

# nome -> referencias extras, argumentos, tipo (verificacao | foto)
$testes = [ordered]@{
    TesteRobustez  = @{ Refs = @();                                                    Args = @($dll);          Tipo = 'verificacao' }
    TesteIsl       = @{ Refs = @();                                                    Args = @($dll);          Tipo = 'verificacao' }
    TesteAdaptadores = @{ Refs = @();                                                  Args = @($dll);          Tipo = 'verificacao' }
    TesteEstados   = @{ Refs = @();                                                    Args = @($dll);          Tipo = 'verificacao' }
    TesteTokens    = @{ Refs = @();                                                    Args = @($dll);          Tipo = 'verificacao' }
    TesteFase2     = @{ Refs = @();                                                    Args = @($dll, $saida);  Tipo = 'verificacao' }
    TesteVisual    = @{ Refs = (Gx ($ig + 'Infragistics2.Win.UltraWinGrid.v11.1', 'ActiproSoftware.WinUICore.Net20', 'ActiproSoftware.Shared.Net20')); Args = @($dll, $saida); Tipo = 'verificacao' }
    TesteMenu      = @{ Refs = (Gx ($ig + 'Infragistics2.Win.UltraWinToolbars.v11.1')); Args = @($dll);          Tipo = 'verificacao' }
    TesteMenusDx   = @{ Refs = (Gx 'DevExpress.Utils.v7.3');                         Args = @($dll, $saida);  Tipo = 'verificacao' }
    TesteIcones    = @{ Refs = @();                                                    Args = @($dll, $GxDir);  Tipo = 'verificacao' }
    TesteThreads   = @{ Refs = @();                                                    Args = @($dll);          Tipo = 'verificacao' }
    TesteAuditoria = @{ Refs = @();                                                    Args = @($dll);          Tipo = 'verificacao' }
    TesteTemas     = @{ Refs = (Gx ($ig + 'Infragistics2.Win.UltraWinToolbars.v11.1', 'Infragistics2.Win.UltraWinTree.v11.1', 'Infragistics2.Win.UltraWinTabControl.v11.1')); Args = @($dll, $saida); Tipo = 'foto' }
    TestePrevia    = @{ Refs = @();                                                    Args = @($dll, $saida);  Tipo = 'foto' }
    TesteAbas      = @{ Refs = (Gx 'MagicLocalLibrary');                               Args = @($dll, $saida);  Tipo = 'foto' }
    TesteRolagens  = @{ Refs = (Gx ($ig + 'Infragistics2.Win.UltraWinTree.v11.1', 'MagicLocalLibrary')); Args = @($dll, $saida); Tipo = 'foto' }
    TesteSugestoes = @{ Refs = (Gx 'ActiproSoftware.SyntaxEditor.Net20', 'ActiproSoftware.WinUICore.Net20');              Args = @($dll, $saida);  Tipo = 'foto' }
    TesteComparador = @{ Refs = (Gx 'Artech.Architecture.Common', 'Artech.Architecture.UI.Framework', 'Artech.Architecture.Interfaces', 'Artech.Udm.Framework', 'Artech.Common.Properties', 'ActiproSoftware.SyntaxEditor.Net20', 'ActiproSoftware.Shared.Net20'); Args = @($dll, $saida); Tipo = 'foto' }
    TesteSelecao   = @{ Refs = (Gx ($ig + 'Infragistics2.Win.UltraWinGrid.v11.1')) + '-r:System.Data.dll'; Args = @($dll, $saida); Tipo = 'foto' }
    TestePopups    = @{ Refs = (Gx ($ig + 'Infragistics2.Win.UltraWinToolbars.v11.1')); Args = @($dll, $saida); Tipo = 'foto' }
}

# Perfil isolado: os testes nao leem nem gravam o dark.ini/logs do usuario (o tema dele
# mudava a foto da tela de configuracao). Recriado vazio a cada rodada.
$perfil = Join-Path $bin 'perfil'
if (Test-Path $perfil) { Remove-Item -Recurse -Force $perfil }
New-Item -ItemType Directory -Force $perfil | Out-Null
# o mesmo que o instalador entrega (o dark.ini do pacote); sem ele a extensao fica desligada
Copy-Item (Join-Path $PSScriptRoot 'dark.ini') (Join-Path $perfil 'dark.ini')
$env:XPZDARK_PASTA = $perfil

# A maioria dos testes fotografa a tela. Com a sessao bloqueada (ou a tela desligada) a
# foto sai da tela de bloqueio e as verificacoes de cor dao resultado FALSO - inclusive
# passando (28/10). Confere com uma janela magenta antes de comecar.
function Test-TelaDisponivel {
    $f = New-Object System.Windows.Forms.Form
    $f.BackColor = [System.Drawing.Color]::FromArgb(255, 0, 255); $f.FormBorderStyle = 'None'; $f.TopMost = $true
    $f.StartPosition = 'Manual'; $f.Location = New-Object System.Drawing.Point(200, 200); $f.Size = New-Object System.Drawing.Size(50, 50); $f.ShowInTaskbar = $false
    try {
        $f.Show()
        # Ate 2 s: logo apos o Show a captura pode falhar ("identificador invalido") ou vir sem a janela.
        $relogio = [Diagnostics.Stopwatch]::StartNew()
        while ($relogio.ElapsedMilliseconds -lt 2000) {
            [System.Windows.Forms.Application]::DoEvents(); [System.Threading.Thread]::Sleep(100)
            try {
                $b = New-Object System.Drawing.Bitmap(1, 1)
                $g = [System.Drawing.Graphics]::FromImage($b); $g.CopyFromScreen(225, 225, 0, 0, (New-Object System.Drawing.Size(1, 1))); $g.Dispose()
                $c = $b.GetPixel(0, 0); $b.Dispose()
                if ($c.R -eq 255 -and $c.G -eq 0 -and $c.B -eq 255) { return $true }
            } catch { }
        }
        return $false
    } finally { $f.Close(); $f.Dispose() }
}
if (-not (Test-TelaDisponivel)) {
    # Causas vistas em 28/10: sessao bloqueada; e janela maximizada de outro programa por cima
    # (o Windows nem sempre aplica o TopMost a janela de processo em segundo plano).
    Write-Host '  FALHA tela indisponivel (sessao bloqueada, tela desligada ou outra janela por cima): as fotos nao valem - minimize as janelas (Win+D) e rode de novo' -ForegroundColor Red
    exit 2
}

$resultado = @()
foreach ($nome in $testes.Keys) {
    if ($Somente -and $Somente -notcontains $nome) { continue }
    $t   = $testes[$nome]
    $exe = Join-Path $bin "$nome.exe"
    $cs  = Join-Path $fontes "$nome.cs"
    $saidaCsc = & $Csc -nologo -platform:x86 "-out:$exe" $base $t.Refs $cs 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  FALHA $nome nao compilou" -ForegroundColor Red
        $saidaCsc | Select-Object -First 5 | ForEach-Object { Write-Host "        $_" }
        $resultado += [pscustomobject]@{ Teste = $nome; Tipo = $t.Tipo; Ok = $false; Detalhe = 'nao compilou' }
        continue
    }
    # Ponteiro do mouse longe das janelas de teste: hover muda a foto.
    [System.Windows.Forms.Cursor]::Position = New-Object System.Drawing.Point(([System.Windows.Forms.SystemInformation]::VirtualScreen.Right - 2), 2)
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $env:MODO = 'testar'
    $texto = & $exe @($t.Args) 2>&1 | Out-String
    $codigo = $LASTEXITCODE
    $sw.Stop()
    $falhasTexto = ([regex]::Matches($texto, '(?m)^\s*FALHA ')).Count
    $ok = $codigo -eq 0 -and $falhasTexto -eq 0
    $cor = if ($ok) { 'DarkGreen' } else { 'Red' }
    Write-Host ("  {0} {1,-16} {2,-12} {3,5:0.0} s" -f $(if ($ok) { '[ok] ' } else { 'FALHA' }), $nome, $t.Tipo, $sw.Elapsed.TotalSeconds) -ForegroundColor $cor
    if (-not $ok) { $texto -split "`n" | Where-Object { $_ -match 'FALHA|Exce|Exception' } | Select-Object -First 8 | ForEach-Object { Write-Host "        $($_.Trim())" } }
    $resultado += [pscustomobject]@{ Teste = $nome; Tipo = $t.Tipo; Ok = $ok; Detalhe = "codigo $codigo" }
}

# ------------------------------------------------ teste por foto
# So na rodada completa: com -Somente, faltariam fotos dos outros testes.
if (-not $Somente) {
    $cmp = Join-Path $bin 'ComparaFotos.exe'
    & $Csc -nologo "-out:$cmp" '-r:System.Drawing.dll' (Join-Path $fontes 'ComparaFotos.cs') | Out-Null
    $referencia = Join-Path $fontes 'referencia'
    Write-Host ""
    if ($NovaReferencia) { & $cmp $referencia $saida referencia }
    else {
        $texto = & $cmp $referencia $saida | Out-String
        $codigo = $LASTEXITCODE
        $texto -split "`n" | Where-Object { $_.Trim() } | ForEach-Object { Write-Host $_ -ForegroundColor $(if ($_ -match 'FALHA') { 'Red' } else { 'DarkGray' }) }
        $resultado += [pscustomobject]@{ Teste = 'ComparaFotos'; Tipo = 'foto'; Ok = ($codigo -eq 0); Detalhe = "$codigo foto(s) diferente(s)" }
    }
}

$ruins = @($resultado | Where-Object { -not $_.Ok })
Write-Host ""
if ($ruins.Count -eq 0) { Write-Host "TODOS OS $($resultado.Count) TESTES PASSARAM  (fotos em $saida)" -ForegroundColor Cyan; exit 0 }
Write-Host "$($ruins.Count) DE $($resultado.Count) FALHARAM: $(($ruins | ForEach-Object Teste) -join ', ')" -ForegroundColor Red
exit 1
