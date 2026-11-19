#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Demostración: los usuarios de ejemplo de FinCore abren los informes de SSRS con su propia cuenta de Windows.

.DESCRIPTION
    SSRS autentica con Windows, y los usuarios de ejemplo (Laura Supervisora, Marta Auditora) solo existen en FinCore.
    Este script, en un equipo de desarrollo:
      1. Crea las cuentas locales fc.supervisora y fc.auditora (grupo Usuarios, la clave no caduca).
      2. Las asocia, por la API y con auditoría, a los usuarios supervisor y auditor. Asocia también tu propia cuenta
         de Windows al usuario admin (que tiene reports.viewas: puede "ver como" cualquiera).
      3. Les da el rol de lectura de SSRS en /FinCore (deploy-reports.ps1 -Browsers).
      4. Comprueba con las credenciales de cada cuenta qué informes puede abrir.

    En producción no se crean cuentas: se asocian las cuentas del dominio desde la pantalla Usuarios y se da lectura
    en SSRS a un grupo del dominio.

.EXAMPLE
    # PowerShell como administrador, con docker compose arriba:
    ./demo-windows-users.ps1
    ./demo-windows-users.ps1 -Remove      # deshace todo: quita las asociaciones y borra las cuentas locales
#>
[CmdletBinding()]
param(
    [string]$WindowsPassword = 'FinCore*2026',
    [string]$ApiUrl = 'http://localhost:4300/api/v1',
    [string]$AdminUser = 'admin',
    [string]$AdminPassword = 'FinCore*2026',
    [string]$ReportServerUrl = 'http://localhost/ReportServer',
    [switch]$Remove
)

$ErrorActionPreference = 'Stop'
$machine = $env:COMPUTERNAME
$me = [Security.Principal.WindowsIdentity]::GetCurrent().Name
$demo = [ordered]@{
    'fc.supervisora' = @{ FinCoreUser = 'supervisor'; FullName = 'Laura Supervisora (FinCore demo)' }
    'fc.auditora'    = @{ FinCoreUser = 'auditor';    FullName = 'Marta Auditora (FinCore demo)' }
}

# ── API de FinCore como administrador ──
$login = @{ userName = $AdminUser; password = $AdminPassword } | ConvertTo-Json
$token = (Invoke-RestMethod -Method Post "$ApiUrl/auth/login" -ContentType 'application/json' -Body $login).accessToken
$headers = @{ Authorization = "Bearer $token" }
$users = Invoke-RestMethod "$ApiUrl/users" -Headers $headers

function Set-FinCoreWindowsAccount([string]$UserName, [string]$Account) {
    $user = $users | Where-Object { $_.userName -eq $UserName }
    if (-not $user) { throw "No existe el usuario $UserName en FinCore." }
    $body = @{ windowsAccount = $Account } | ConvertTo-Json
    Invoke-RestMethod -Method Put "$ApiUrl/users/$($user.id)/windows-account" -Headers $headers -ContentType 'application/json' -Body $body | Out-Null
    # Una cuenta solo puede pertenecer a un usuario: si otro la tenía, la API responde 409 y el script se detiene.
}

if ($Remove) {
    foreach ($name in $demo.Keys) {
        Set-FinCoreWindowsAccount $demo[$name].FinCoreUser $null
        if (Get-LocalUser -Name $name -ErrorAction SilentlyContinue) { Remove-LocalUser -Name $name; Write-Host "  - cuenta local $name" }
    }
    Write-Host 'Listo: asociaciones quitadas y cuentas locales borradas (tu cuenta sigue asociada a admin).' -ForegroundColor Green
    return
}

Write-Host "Usuarios de ejemplo en Reporting Services ($machine)" -ForegroundColor Cyan
$secure = ConvertTo-SecureString $WindowsPassword -AsPlainText -Force
foreach ($name in $demo.Keys) {
    if (-not (Get-LocalUser -Name $name -ErrorAction SilentlyContinue)) {
        New-LocalUser -Name $name -Password $secure -FullName $demo[$name].FullName -Description 'FinCore: demostración de SSRS' `
            -PasswordNeverExpires -UserMayNotChangePassword | Out-Null
        Add-LocalGroupMember -SID 'S-1-5-32-545' -Member $name   # grupo Usuarios (el nombre cambia con el idioma)
        Write-Host "  + cuenta local $machine\$name"
    }
}

# Tu cuenta → admin (si ya está asociada a admin no cambia nada). Las cuentas demo → supervisor y auditor.
$adminUser = $users | Where-Object { $_.userName -eq $AdminUser }
if ($adminUser.windowsAccount -ne $me) { Set-FinCoreWindowsAccount $AdminUser $me }
Write-Host "  = $me -> $AdminUser"
foreach ($name in $demo.Keys) {
    Set-FinCoreWindowsAccount $demo[$name].FinCoreUser "$machine\$name"
    Write-Host "  = $machine\$name -> $($demo[$name].FinCoreUser)"
}

& (Join-Path $PSScriptRoot 'deploy-reports.ps1') -Browsers ($demo.Keys | ForEach-Object { "$machine\$_" }) | Out-Null
Write-Host '  + rol de lectura en /FinCore'

# ── Comprobación con las credenciales de cada cuenta ──
$period = 'DateFrom=2026-11-01&DateTo=2026-11-30'
Write-Host "`nQué puede abrir cada uno:" -ForegroundColor Cyan
foreach ($name in $demo.Keys) {
    $cred = New-Object System.Management.Automation.PSCredential("$machine\$name", $secure)
    foreach ($report in 'ResumenPorSucursal', 'ActividadPorUsuario') {
        try {
            Invoke-WebRequest -UseBasicParsing -Credential $cred "$ReportServerUrl`?/FinCore/$report&rs:Format=CSV&$period" | Out-Null
            Write-Host ("  {0,-16} {1,-22} sí" -f $name, $report) -ForegroundColor Green
        } catch {
            Write-Host ("  {0,-16} {1,-22} no ({2})" -f $name, $report, $_.Exception.Response.StatusCode.value__) -ForegroundColor DarkYellow
        }
    }
}
Write-Host "`nEsperado: la supervisora abre el resumen pero no la actividad (no tiene audit.read); la auditora abre ambos." -ForegroundColor Gray
Write-Host 'Para entrar al portal como una de ellas: Edge en modo InPrivate, http://localhost/Reports, usuario EQUIPO\fc.auditora.' -ForegroundColor Gray
