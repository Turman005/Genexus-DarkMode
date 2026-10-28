# GeneXus 18 - Dark mode (v4): o tema e aplicado SO pela extensao GxDarkTheme, em memoria, enquanto o GeneXus roda.
# Nada mais e alterado no Windows nem no TextEditorConfig_Default.xml. Desativar a extensao (Extensions Manager)
# ou rodar Desativar-DarkMode.ps1 volta o GeneXus exatamente ao original.
$ErrorActionPreference = 'Stop'
$gxDir   = 'C:\Program Files (x86)\GeneXus\GeneXus18'
$cfgDir  = Join-Path $env:APPDATA 'GeneXus\GeneXus\18'
$bkDir   = Join-Path $cfgDir 'DarkModeBackup'
$setFile = Join-Path $cfgDir 'GxDarkTheme.settings'
$here    = Split-Path -Parent $MyInvocation.MyCommand.Path
$extDir  = Join-Path $here 'ext'
$pkgDll  = Join-Path $gxDir 'Packages\GxDarkTheme.dll'
$logFile = Join-Path $env:TEMP 'GxDarkTheme.log'

if (Get-Process -Name GeneXus -ErrorAction SilentlyContinue) {
    Write-Host 'Feche o GeneXus antes de ativar o dark mode e rode de novo.' -ForegroundColor Yellow
    Read-Host 'Enter para sair'; exit 1
}
foreach ($f in @('GxDarkTheme.dll', 'GxDarkTheme.isl', 'Extensao-Admin.ps1', 'Restaurar-Original.ps1')) {
    if (-not (Test-Path (Join-Path $extDir $f))) {
        Write-Host ('Arquivo nao encontrado: ext\' + $f) -ForegroundColor Red
        Read-Host 'Enter para sair'; exit 1
    }
}

# 1) Se a versao antiga do Ativar deixou cores escuras gravadas no Windows/editor, devolve o original
. (Join-Path $extDir 'Restaurar-Original.ps1')
if (Test-Path $bkDir) {
    $n = Restore-GxOriginal -BackupDir $bkDir
    # o backup ja cumpriu o papel: guardado com outro nome, nunca mais e aplicado automaticamente
    $old = $bkDir + '.restaurado-' + (Get-Date -Format 'yyyyMMdd-HHmmss')
    Rename-Item $bkDir $old
    Write-Host ('Configuracoes originais conferidas (' + $n + ' grupo(s) restaurado(s)). Backup antigo guardado em ' + $old) -ForegroundColor Green
}

# 2) Liga o tema (se tinha sido desligado pelo arquivo de configuracao)
if (Test-Path $setFile) { Remove-Item $setFile -Force }

# 3) Extensao GxDarkTheme (unica mudanca persistente)
Write-Host ''
Write-Host 'Agora o Windows vai pedir permissao de administrador (janela do UAC).' -ForegroundColor Yellow
Write-Host 'Clique em SIM: e necessario para copiar a extensao para a pasta do GeneXus' -ForegroundColor Yellow
Write-Host 'e atualizar a lista de pacotes. Aguarde terminar (1 a 2 minutos).' -ForegroundColor Yellow
try {
    Start-Process -FilePath 'powershell.exe' -Verb RunAs -Wait -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', ('"' + (Join-Path $extDir 'Extensao-Admin.ps1') + '"'), 'instalar')
} catch {
    Write-Host 'Permissao de administrador negada: a extensao nao foi instalada. Rode este atalho de novo e clique em SIM.' -ForegroundColor Red
    Read-Host 'Enter para sair'; exit 1
}
if (Test-Path $pkgDll) { Write-Host 'Extensao GxDarkTheme instalada.' -ForegroundColor Green }
else { Write-Host 'A extensao nao foi encontrada na pasta Packages. Verifique a mensagem da janela de administrador.' -ForegroundColor Red }
Remove-Item $logFile -Force -ErrorAction SilentlyContinue

# 4) Abrir o GeneXus normalmente (sem /IDEStyle: esse parametro impede o carregamento do tema)
Write-Host 'Abrindo o GeneXus...'
Start-Process -FilePath (Join-Path $gxDir 'GeneXus.exe')
Write-Host ('Se alguma area continuar clara, envie o arquivo ' + $logFile) -ForegroundColor Cyan
Start-Sleep -Seconds 3
