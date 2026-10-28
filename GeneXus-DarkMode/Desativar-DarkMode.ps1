# GeneXus 18 - Dark mode (desfazer): remove a extensao e garante que nada do tema ficou no Windows/editor
$ErrorActionPreference = 'Stop'
$cfgDir = Join-Path $env:APPDATA 'GeneXus\GeneXus\18'
$gxDir  = 'C:\Program Files (x86)\GeneXus\GeneXus18'
$here   = Split-Path -Parent $MyInvocation.MyCommand.Path
$pkgDll = Join-Path $gxDir 'Packages\GxDarkTheme.dll'
$pkgIsl = Join-Path $gxDir 'Packages\GxDarkTheme.isl'

if (Get-Process -Name GeneXus -ErrorAction SilentlyContinue) {
    Write-Host 'Feche o GeneXus antes de desfazer e rode de novo.' -ForegroundColor Yellow
    Read-Host 'Enter para sair'; exit 1
}
# Remove a extensao GxDarkTheme (precisa de administrador)
if ((Test-Path $pkgDll) -or (Test-Path $pkgIsl)) {
    Write-Host 'O Windows vai pedir permissao de administrador (UAC) para remover a extensao do GeneXus. Clique em SIM.' -ForegroundColor Yellow
    try {
        Start-Process -FilePath 'powershell.exe' -Verb RunAs -Wait -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', ('"' + (Join-Path $here 'ext\Extensao-Admin.ps1') + '"'), 'remover')
    } catch { Write-Host 'Permissao negada: a extensao continua instalada. Rode de novo e clique em SIM.' -ForegroundColor Red }
    if (Test-Path $pkgDll) { Write-Host 'A extensao ainda esta na pasta Packages.' -ForegroundColor Red }
    else { Write-Host 'Extensao GxDarkTheme removida.' -ForegroundColor Green }
}
# Sobras da versao antiga do Ativar (cores do editor, cores do Windows, tema/barra de titulo)
. (Join-Path $here 'ext\Restaurar-Original.ps1')
$bkDir = Join-Path $cfgDir 'DarkModeBackup'
if (Test-Path $bkDir) {
    [void](Restore-GxOriginal -BackupDir $bkDir)
    Remove-Item $bkDir -Recurse -Force -ErrorAction SilentlyContinue
}
Remove-Item (Join-Path $cfgDir 'GxDarkTheme.settings') -Force -ErrorAction SilentlyContinue
Write-Host 'Pronto. Abra o GeneXus normalmente.'
Read-Host 'Enter para sair'
