<#
.SYNOPSIS
    Compila Xpz.Dark.dll - o experimento de dark mode do IDE.

.DESCRIPTION
    Mesmo esquema das outras extensoes: DLL x86, .NET Framework, manifesto
    embutido como recurso <Namespace>.GeneXusPackage.package, e
    PackageCompatibility lida do catalogo (numero errado = a extensao e ignorada
    em silencio).

    Referencia Artech.Common porque o gerenciador de recursos que clareia os
    icones HERDA do CachingResourceManager do GeneXus - precisa do tipo em tempo
    de compilacao. O Infragistics NAO e referenciado: as tabelas de cor e o
    StyleManager sao acessados por reflexao, para a DLL nao ficar presa a uma
    versao da biblioteca.

    NAO instala. A instalacao exige admin: use .\Install-Dark.ps1.
#>
[CmdletBinding()]
param(
    [string] $GxDir = 'C:\Program Files (x86)\GeneXus\GeneXus18-U10',
    [string] $Csc   = 'C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe'
)

$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path

if (-not (Test-Path $GxDir)) { throw "instalacao GeneXus nao encontrada: $GxDir" }
if (-not (Test-Path $Csc))   { throw "csc.exe nao encontrado: $Csc" }

# A Actipro (editor de codigo, fase 3) E referenciada: e a mesma DLL que o GeneXus
# carrega da pasta dele, e o codigo que a usa fica isolado em Editor.cs.
$gxRefs = @(
    'Artech.Architecture.Common.dll',
    'Artech.Architecture.UI.Framework.dll',
    'Artech.Common.Framework.dll',
    'Artech.Common.dll',
    'ActiproSoftware.SyntaxEditor.Net20.dll',
    'ActiproSoftware.Shared.Net20.dll',
    'ActiproSoftware.WinUICore.Net20.dll',
    'DevExpress.Utils.v7.3.dll'
) | ForEach-Object {
    $p = Join-Path $GxDir $_
    if (-not (Test-Path $p)) { throw "referencia ausente: $p" }
    "/r:$p"
}

# ---------------------------------------------------- versao de compatibilidade
# O nome do catalogo de packages E o numero: packages.143920.xml
$compat = 0
$catDir = Join-Path $env:ProgramData 'GeneXus\GeneXus\18'
if (Test-Path $catDir) {
    $f = Get-ChildItem $catDir -File -ErrorAction SilentlyContinue |
         Where-Object { $_.Name -match '^packages\.(\d+)\.xml$' } | Select-Object -First 1
    if ($f -and $f.Name -match '^packages\.(\d+)\.xml$') { $compat = [int] $Matches[1] }
}
if ($compat -le 0) { throw "nao consegui descobrir a CompatibilityVersion (catalogo ausente?)" }
Write-Host "  compatibilidade: $compat" -ForegroundColor DarkGray

$gen = Join-Path $here 'CompatibilityVersion.g.cs'
@"
// GERADO por Build-Dark.ps1 - nao edite.
[assembly: Artech.Architecture.Common.Packages.PackageCompatibility(Version = $compat)]
"@ | Set-Content -Path $gen -Encoding UTF8

$out      = Join-Path $here 'Xpz.Dark.dll'
$manifest = Join-Path $here 'GeneXusPackage.package'

# ---------------------------------------------------- legenda do item de menu
# O GeneXus tira o texto do menu do resource de strings, pelo id do comando.
# Precisa do Windows PowerShell 5.1: o ResourceWriter do .NET Core escreve um
# formato que o ResourceManager do .NET Framework nao le.
$res = Join-Path $here 'Xpz.Dark.Resources.resources'
$ps1 = @'
param($out)
$w = New-Object System.Resources.ResourceWriter($out)
$w.AddResource('XpzDarkConfig', [string][char]0x43 + 'onfigura' + [char]0xE7 + [char]0xF5 + 'es do tema...')
$w.Close()
'@
$tmp = Join-Path $env:TEMP 'xpz-dark-gen-res.ps1'
Set-Content -Path $tmp -Value $ps1 -Encoding ASCII
& (Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe') -NoProfile -ExecutionPolicy Bypass -File $tmp $res
if ($LASTEXITCODE -ne 0 -or -not (Test-Path $res)) { throw "falha ao gerar $res" }
Remove-Item $tmp -ErrorAction SilentlyContinue

# Todo o codigo fica em src\ (Nucleo, Adaptadores\<biblioteca>, Diagnostico, Interface):
# arquivo novo entra sozinho no build.
$fontes = @(Get-ChildItem (Join-Path $here 'src') -Recurse -Filter '*.cs' | ForEach-Object FullName)
Write-Host "compilando $out (x86, library, $($fontes.Count) arquivos)..." -ForegroundColor Cyan
& $Csc -nologo -target:library -platform:x86 -optimize+ -warn:3 -codepage:65001 "-out:$out" `
       "/resource:$manifest,Xpz.Dark.GeneXusPackage.package" `
       "/resource:$res,Xpz.Dark.Resources.resources" `
       $gxRefs "/r:System.dll" "/r:System.Core.dll" "/r:Microsoft.CSharp.dll" "/r:System.Windows.Forms.dll" "/r:System.Drawing.dll" `
       $gen $fontes
if ($LASTEXITCODE -ne 0) { throw "falha na compilacao (exit $LASTEXITCODE)" }

Write-Host "  [ok] $out" -ForegroundColor DarkGreen
