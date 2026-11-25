<#
  Compila TaskbarMin y genera el instalador en local.
  Uso:  .\scripts\build.ps1 -Version 1.0.0
  Requisitos: .NET SDK 10. Opcional: Inno Setup 6 (para el instalador).
#>
param([string]$Version = "1.0.0")

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

Write-Host "==> Publicando TaskbarMin $Version" -ForegroundColor Cyan
dotnet publish "$root\src\TaskbarMin\TaskbarMin.csproj" -c Release -o "$root\publish" -p:Version=$Version
if ($LASTEXITCODE -ne 0) { throw "dotnet publish ha fallado" }

$iscc = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
if (Test-Path $iscc) {
    Write-Host "==> Generando instalador" -ForegroundColor Cyan
    & $iscc "/DAppVersion=$Version" "$root\installer\TaskbarMin.iss"
    if ($LASTEXITCODE -ne 0) { throw "ISCC ha fallado" }
    Write-Host "Instalador listo en: $root\dist" -ForegroundColor Green
} else {
    Write-Warning "Inno Setup 6 no encontrado: solo se ha generado el .exe en $root\publish"
}
