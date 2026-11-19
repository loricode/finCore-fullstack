<#
.SYNOPSIS
    Despliega la base de datos de FinCore desde los scripts versionados de esta carpeta.

.DESCRIPTION
    Recorre las carpetas NNN_* en orden y ejecuta cada script .sql en orden de nombre:

      VERSIONED   001_Schemas, 002_Tables, 003_Indexes, 009_Demo
                  Se ejecutan UNA vez. Si un script aplicado cambia, el despliegue se detiene:
                  un cambio a una tabla existente es un script NUEVO (p. ej. 025_fin_Transactions_AddChannel.sql).
      REPEATABLE  004_Functions, 005_Views, 006_Procedures, 007_Triggers, 008_Seed, 010_Security
                  CREATE OR ALTER / MERGE. Se vuelven a ejecutar cuando cambia su checksum.

    La bitácora queda en dbo.SchemaVersions (script, tipo, checksum SHA-256, fecha, usuario, duración).
    009_Demo solo se aplica con -IncludeDemo.

.EXAMPLE
    .\deploy.ps1                                   # localhost, autenticación de Windows, base FinCore
    .\deploy.ps1 -Recreate -IncludeDemo            # base desde cero con datos de demostración
    .\deploy.ps1 -Server "sql01,1433" -User fincore_deploy -Password $env:SQL_PASSWORD -TrustServerCertificate
    .\deploy.ps1 -DryRun                           # muestra qué se ejecutaría
#>
[CmdletBinding()]
param(
    [string]$Server = 'localhost',
    [string]$Database = 'FinCore',
    [string]$User,
    [string]$Password,
    [switch]$TrustServerCertificate,
    [switch]$IncludeDemo,
    [switch]$Recreate,
    [switch]$DryRun,
    # Login de la aplicación (miembro de fincore_app_role). Se usa en Docker y servidores.
    [string]$AppLogin,
    [string]$AppPassword,
    # Login de Reporting Services (miembro de fincore_reports_role: solo EXECUTE en rpt).
    [string]$ReportsLogin,
    [string]$ReportsPassword
)

$ErrorActionPreference = 'Stop'
# Join-Path anidado: funciona igual en Windows PowerShell 5.1 y en PowerShell 7 sobre Linux (contenedor).
$deployDir = Join-Path $PSScriptRoot '_deploy'
. (Join-Path $deployDir 'SqlCmd.ps1')

if (-not (Get-Command sqlcmd -ErrorAction SilentlyContinue)) {
    throw 'No se encontró sqlcmd en el PATH. Instálalo: https://learn.microsoft.com/sql/tools/sqlcmd/sqlcmd-utility'
}
if ($Database -notmatch '^[A-Za-z][A-Za-z0-9_]{0,63}$') { throw "Nombre de base de datos inválido: $Database" }

$conn = New-SqlCmdArgs -Server $Server -User $User -Password $Password -TrustServerCertificate:$TrustServerCertificate
$repeatableFolders = @('004_Functions', '005_Views', '006_Procedures', '007_Triggers', '008_Seed', '010_Security')
if ($AppLogin -and -not $AppPassword) { throw 'Con -AppLogin se requiere -AppPassword.' }
if ($AppLogin -and $AppLogin -notmatch '^[A-Za-z][A-Za-z0-9_]{0,63}$') { throw "Nombre de login inválido: $AppLogin" }
if ($ReportsLogin -and -not $ReportsPassword) { throw 'Con -ReportsLogin se requiere -ReportsPassword.' }
if ($ReportsLogin -and $ReportsLogin -notmatch '^[A-Za-z][A-Za-z0-9_]{0,63}$') { throw "Nombre de login inválido: $ReportsLogin" }

Write-Host "FinCore · despliegue de base de datos  [$Server / $Database]" -ForegroundColor Cyan

# ------------------------------------------------------------------ base de datos
if ($Recreate -and -not $DryRun) {
    Write-Host "  Eliminando la base $Database (-Recreate)..." -ForegroundColor Yellow
    Invoke-SqlQuery $conn 'master' "IF DB_ID(N'$Database') IS NOT NULL BEGIN ALTER DATABASE [$Database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$Database]; END" | Out-Null
}

$exists = (Invoke-SqlQuery $conn 'master' "SELECT CASE WHEN DB_ID(N'$Database') IS NULL THEN 0 ELSE 1 END") -eq '1'
if (-not $exists) {
    if ($DryRun) { Write-Host "  [dry-run] Se crearía la base $Database" }
    else {
        Write-Host "  Creando la base $Database..."
        Invoke-SqlQuery $conn 'master' "CREATE DATABASE [$Database]; ALTER DATABASE [$Database] SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;" | Out-Null
    }
}

# ------------------------------------------------------------------ bitácora
$journal = @{}
if ($exists -or -not $DryRun) {
    if (-not $DryRun) {
        $r = Invoke-SqlFile $conn $Database (Join-Path $deployDir '000_SchemaVersions.sql')
        if ($r.ExitCode -ne 0) { throw "No se pudo crear dbo.SchemaVersions:`n$($r.Output)" }
    }
    $hasJournal = (Invoke-SqlQuery $conn $Database "SELECT CASE WHEN OBJECT_ID(N'dbo.SchemaVersions') IS NULL THEN 0 ELSE 1 END") -eq '1'
    if ($hasJournal) {
        foreach ($line in (Invoke-SqlQuery $conn $Database "SELECT ScriptPath + '|' + Checksum FROM dbo.SchemaVersions")) {
            $parts = $line.Split('|'); $journal[$parts[0]] = $parts[1]
        }
    }
}

# ------------------------------------------------------------------ scripts
$folders = Get-ChildItem -Path $PSScriptRoot -Directory |
    Where-Object { $_.Name -match '^\d{3}_' -and ($IncludeDemo -or $_.Name -ne '009_Demo') } |
    Sort-Object Name

$applied = 0; $skipped = 0
foreach ($folder in $folders) {
    $type = if ($repeatableFolders -contains $folder.Name) { 'REPEATABLE' } else { 'VERSIONED' }
    foreach ($file in (Get-ChildItem -Path $folder.FullName -Filter '*.sql' -File | Sort-Object Name)) {
        $key = "$($folder.Name)/$($file.Name)"
        $checksum = Get-ScriptChecksum $file.FullName

        if ($journal.ContainsKey($key)) {
            if ($journal[$key] -eq $checksum) { $skipped++; continue }
            if ($type -eq 'VERSIONED') {
                throw "El script versionado $key ya fue aplicado y su contenido cambió. No edites scripts aplicados: crea uno nuevo."
            }
        }

        if ($DryRun) { Write-Host "  [dry-run] $type $key"; $applied++; continue }

        Write-Host ("  {0,-10} {1}" -f $type, $key)
        $watch = [Diagnostics.Stopwatch]::StartNew()
        $result = Invoke-SqlFile $conn $Database $file.FullName
        $watch.Stop()
        if ($result.ExitCode -ne 0) {
            Write-Host $result.Output -ForegroundColor Red
            throw "Falló $key. El despliegue se detuvo; los scripts anteriores quedaron aplicados."
        }

        $ms = [int]$watch.ElapsedMilliseconds
        $path = $key.Replace("'", "''")
        Invoke-SqlQuery $conn $Database @"
MERGE dbo.SchemaVersions AS t
USING (SELECT N'$path' AS ScriptPath) AS s ON t.ScriptPath = s.ScriptPath
WHEN MATCHED THEN UPDATE SET Checksum = '$checksum', AppliedAtUtc = SYSUTCDATETIME(), AppliedBy = SUSER_SNAME(),
                             DurationMs = $ms, ExecutionCount = t.ExecutionCount + 1
WHEN NOT MATCHED THEN INSERT (ScriptPath, ScriptType, Checksum, DurationMs) VALUES (N'$path', '$type', '$checksum', $ms);
"@ | Out-Null
        $applied++
    }
}

# ------------------------------------------------------------------ login de la aplicación
if ($AppLogin -and -not $DryRun) {
    Write-Host "  LOGIN      $AppLogin -> fincore_app_role"
    $vars = @('-v', "AppLogin=$AppLogin", "AppPassword=$AppPassword", "DatabaseName=$Database")
    $r = Invoke-SqlFile $conn $Database (Join-Path $deployDir '100_AppLogin.sql') $vars
    if ($r.ExitCode -ne 0) { Write-Host $r.Output -ForegroundColor Red; throw "No se pudo crear el login $AppLogin." }
}

if ($ReportsLogin -and -not $DryRun) {
    Write-Host "  LOGIN      $ReportsLogin -> fincore_reports_role"
    $vars = @('-v', "ReportsLogin=$ReportsLogin", "ReportsPassword=$ReportsPassword", "DatabaseName=$Database")
    $r = Invoke-SqlFile $conn $Database (Join-Path $deployDir '101_ReportsLogin.sql') $vars
    if ($r.ExitCode -ne 0) { Write-Host $r.Output -ForegroundColor Red; throw "No se pudo crear el login $ReportsLogin." }
}

Write-Host "Listo: $applied script(s) aplicados, $skipped sin cambios." -ForegroundColor Green
