# Executado como administrador pelo Ativar/Desativar: instala ou remove a extensao GxDarkTheme
# na pasta Packages do GeneXus e reconstroi o catalogo de pacotes (GeneXus.exe /install).
param([string]$Acao = 'instalar')
$ErrorActionPreference = 'Stop'
$here  = Split-Path -Parent $MyInvocation.MyCommand.Path
$gxDir = 'C:\Program Files (x86)\GeneXus\GeneXus18'
$pkg   = Join-Path $gxDir 'Packages'
$files = @('GxDarkTheme.dll', 'GxDarkTheme.isl')
try {
    if ($Acao -eq 'instalar') {
        foreach ($f in $files) { Copy-Item (Join-Path $here $f) (Join-Path $pkg $f) -Force }
        Write-Host 'Extensao copiada para a pasta Packages do GeneXus.' -ForegroundColor Green
    } else {
        foreach ($f in $files) { Remove-Item (Join-Path $pkg $f) -Force -ErrorAction SilentlyContinue }
        Write-Host 'Extensao removida da pasta Packages do GeneXus.' -ForegroundColor Green
    }
    Write-Host 'Atualizando o catalogo de pacotes do GeneXus (pode levar 1 ou 2 minutos)...'
    Start-Process -FilePath (Join-Path $gxDir 'GeneXus.exe') -ArgumentList '/install' -Wait
    Write-Host 'Catalogo atualizado.' -ForegroundColor Green
} catch {
    Write-Host ('ERRO: ' + $_.Exception.Message) -ForegroundColor Red
    Read-Host 'Enter para sair'
    exit 1
}
