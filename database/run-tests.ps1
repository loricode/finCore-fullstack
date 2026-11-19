<#
.SYNOPSIS
    Ejecuta las pruebas T-SQL de tests/ contra una base ya desplegada.

.DESCRIPTION
    Aplica tests/_framework.sql (esquema test: afirmaciones y fixtures) y ejecuta cada tests/test_*.sql.
    Cada prueba trabaja dentro de una transacción que revierte al final: no deja datos.
    Termina con código 1 si alguna falla (útil en CI).

.EXAMPLE
    .\deploy.ps1 -Recreate; .\run-tests.ps1
    .\run-tests.ps1 -Filter reconciliation
#>
[CmdletBinding()]
param(
    [string]$Server = 'localhost',
    [string]$Database = 'FinCore',
    [string]$User,
    [string]$Password,
    [switch]$TrustServerCertificate,
    [string]$Filter = '*'
)

$ErrorActionPreference = 'Stop'
. (Join-Path (Join-Path $PSScriptRoot '_deploy') 'SqlCmd.ps1')

$conn = New-SqlCmdArgs -Server $Server -User $User -Password $Password -TrustServerCertificate:$TrustServerCertificate
$testsDir = Join-Path $PSScriptRoot 'tests'

Write-Host "FinCore · pruebas de base de datos  [$Server / $Database]" -ForegroundColor Cyan

$fw = Invoke-SqlFile $conn $Database (Join-Path $testsDir '_framework.sql')
if ($fw.ExitCode -ne 0) { Write-Host $fw.Output -ForegroundColor Red; throw 'No se pudo instalar el framework de pruebas.' }

$failed = @()
$tests = Get-ChildItem -Path $testsDir -Filter "test_*$Filter*.sql" -File | Sort-Object Name
foreach ($test in $tests) {
    Write-Host $test.BaseName -ForegroundColor White
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $r = Invoke-SqlFile $conn $Database $test.FullName
    $watch.Stop()
    # Solo se muestran los mensajes de la prueba (PRINT) y los errores, no los resultados de los procedimientos.
    $r.Output -split "`r?`n" | Where-Object { $_ -match '^\s+ok\s' -or $_ -match 'Msg \d+|HResult|FALLA|Sqlcmd: Error|clash' } |
        ForEach-Object { if ($_ -notmatch '^\s+ok\s') { Write-Host $_ -ForegroundColor Red } else { Write-Host $_ -ForegroundColor Green } }
    if ($r.ExitCode -ne 0) {
        $failed += $test.BaseName
        Write-Host "  FALLÓ ($([int]$watch.ElapsedMilliseconds) ms)" -ForegroundColor Red
    } else {
        Write-Host "  PASÓ ($([int]$watch.ElapsedMilliseconds) ms)" -ForegroundColor DarkGray
    }
}

Write-Host ''
if ($failed.Count -gt 0) {
    Write-Host "$($failed.Count) de $($tests.Count) archivo(s) fallaron: $($failed -join ', ')" -ForegroundColor Red
    exit 1
}
Write-Host "Todas las pruebas pasaron ($($tests.Count) archivos)." -ForegroundColor Green
