param([string]$GamePath)
$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot 'Operacao.ps1') -Acao Restaurar -GamePath $GamePath
