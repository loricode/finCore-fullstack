<#
.SYNOPSIS
    Publica los informes de FinCore en SQL Server Reporting Services (API REST v2.0).

.DESCRIPTION
    Idempotente: crea /FinCore y /FinCore/Datos si faltan, (re)crea el origen de datos compartido con las
    credenciales del login de reportes (solo EXECUTE sobre el esquema rpt) y vuelve a subir cada .rdl
    de ./Reports, enlazándolo al origen compartido. Usa la identidad de Windows de quien lo ejecuta, que
    debe tener el rol "Administrador de contenido" en la carpeta raíz de SSRS.

.EXAMPLE
    node build-rdl.mjs
    ./deploy-reports.ps1
    ./deploy-reports.ps1 -PortalUrl http://reportes01/Reports -SqlServer sql01,1433 -ReportsPassword (Read-Host)
    ./deploy-reports.ps1 -Browsers 'EMPRESA\Contabilidad', 'EMPRESA\Auditoria'   # además da lectura en /FinCore

.NOTES
    -Browsers otorga el rol de lectura de SSRS ("Browser" / "Explorador") en /FinCore a cuentas o grupos de Windows,
    conservando los permisos que ya tenga la carpeta. Entrar a la carpeta no basta para ver datos: la cuenta debe estar
    asociada a un usuario de FinCore con reports.read (pantalla Usuarios), o rpt.usp_ReportUsers rechaza el informe (50305).
#>
[CmdletBinding()]
param(
    [string]$PortalUrl = 'http://localhost/Reports',
    [string]$SqlServer = 'localhost,14333',
    [string]$Database = 'FinCore',
    [string]$ReportsLogin = 'fincore_reports',
    [string]$ReportsPassword = 'Dev#Informes_2026',
    [string]$RootFolder = 'FinCore',
    [string[]]$Browsers = @()
)

$ErrorActionPreference = 'Stop'
$api = "$($PortalUrl.TrimEnd('/'))/api/v2.0"
$root = "/$RootFolder"
$dataFolder = "$root/Datos"
$dataSourcePath = "$dataFolder/FinCore"

function Invoke-Rs([string]$Method, [string]$Resource, $Body) {
    $params = @{ Method = $Method; Uri = "$api/$Resource"; UseDefaultCredentials = $true; ContentType = 'application/json; charset=utf-8' }
    if ($null -ne $Body) { $params.Body = [Text.Encoding]::UTF8.GetBytes(($Body | ConvertTo-Json -Depth 6)) }
    try { Invoke-RestMethod @params }
    catch {
        $detail = $_.ErrorDetails.Message
        throw "SSRS $Method $Resource -> $($_.Exception.Message) $detail"
    }
}

function Get-RsItem([string]$Path) {
    try { Invoke-RestMethod -UseDefaultCredentials -Uri "$api/CatalogItems(Path='$([Uri]::EscapeDataString($Path))')" }
    catch { if ($_.Exception.Response.StatusCode.value__ -eq 404) { $null } else { throw } }
}

function Confirm-Folder([string]$Parent, [string]$Name) {
    $path = if ($Parent -eq '/') { "/$Name" } else { "$Parent/$Name" }
    if (-not (Get-RsItem $path)) {
        Invoke-Rs POST 'Folders' @{ '@odata.type' = '#Model.Folder'; Name = $Name; Path = $path } | Out-Null
        Write-Host "  + carpeta $path"
    }
}

Write-Host "Publicando informes en $PortalUrl$root" -ForegroundColor Cyan
Confirm-Folder '/' $RootFolder
Confirm-Folder $root 'Datos'

# Origen de datos compartido: credenciales guardadas en el servidor (cifradas por SSRS), nunca en el .rdl.
$existing = Get-RsItem $dataSourcePath
if ($existing) { Invoke-Rs DELETE "DataSources($($existing.Id))" | Out-Null }
Invoke-Rs POST 'DataSources' @{
    '@odata.type'      = '#Model.DataSource'
    Name               = 'FinCore'
    Path               = $dataSourcePath
    Description        = 'FinCore (SQL Server). Login de solo lectura vía procedimientos rpt.*'
    DataSourceType     = 'SQL'
    ConnectionString   = "Data Source=$SqlServer;Initial Catalog=$Database"
    # Sin esta bandera la API REST descarta ConnectionString al crear el origen (queda vacía).
    IsConnectionStringOverridden = $true
    IsEnabled          = $true
    CredentialRetrieval = 'store'
    CredentialsInServer = @{ UserName = $ReportsLogin; Password = $ReportsPassword; UseAsWindowsCredentials = $false; ImpersonateAuthenticatedUser = $false }
} | Out-Null
Write-Host "  + origen de datos $dataSourcePath ($SqlServer / $Database, login $ReportsLogin)"

$reports = Get-ChildItem (Join-Path $PSScriptRoot 'Reports') -Filter *.rdl
if (-not $reports) { throw 'No hay .rdl en ./Reports. Ejecuta primero: node build-rdl.mjs' }

foreach ($file in $reports) {
    $name = $file.BaseName
    $path = "$root/$name"
    $old = Get-RsItem $path
    if ($old) { Invoke-Rs DELETE "Reports($($old.Id))" | Out-Null }

    $report = Invoke-Rs POST 'Reports' @{
        '@odata.type' = '#Model.Report'
        Name          = $name
        Path          = $path
        Content       = [Convert]::ToBase64String([IO.File]::ReadAllBytes($file.FullName))
        ContentType   = ''
    }

    # El .rdl referencia el origen compartido por ruta absoluta; SSRS lo enlaza al publicar. Se verifica.
    $bound = (Invoke-Rs GET "Reports($($report.Id))/DataSources").value | Where-Object { $_.IsReference -and $_.Path -eq $dataSourcePath }
    if (-not $bound) { throw "El informe $path no quedó enlazado a $dataSourcePath (¿cambió DATA_SOURCE_PATH en build-rdl.mjs?)" }
    Write-Host "  + informe $path"
}

if ($Browsers.Count -gt 0) {
    # El nombre del rol depende del idioma de la instalación; se consulta por SOAP (la API REST no lista roles).
    $serverUrl = $PortalUrl.TrimEnd('/') -replace '/Reports$', '/ReportServer'
    $soap = New-WebServiceProxy -Uri "$serverUrl/ReportService2010.asmx?wsdl" -UseDefaultCredential
    $browserRole = ($soap.ListRoles('Catalog', $null) | Where-Object { $_.Name -in 'Browser', 'Explorador' } | Select-Object -First 1).Name
    if (-not $browserRole) { throw 'No se encontró el rol de lectura (Browser / Explorador) en SSRS.' }

    $folder = Get-RsItem $root
    $current = Invoke-Rs GET "CatalogItems($($folder.Id))/Policies"
    $policies = [System.Collections.ArrayList]@($current.Policies | ForEach-Object {
        @{ GroupUserName = $_.GroupUserName; Roles = @($_.Roles | ForEach-Object { @{ Name = $_.Name } }) }
    })
    foreach ($principal in $Browsers) {
        $existing = $policies | Where-Object { $_.GroupUserName -eq $principal }
        if ($existing) {
            if (-not ($existing.Roles | Where-Object { $_.Name -eq $browserRole })) { $existing.Roles += @{ Name = $browserRole } }
        } else {
            [void]$policies.Add(@{ GroupUserName = $principal; Roles = @(@{ Name = $browserRole }) })
        }
        Write-Host "  + $browserRole en $root para $principal"
    }
    Invoke-Rs PUT "CatalogItems($($folder.Id))/Policies" @{ InheritParentPolicy = $false; Policies = @($policies) } | Out-Null
}

Write-Host "Listo: $PortalUrl/browse/$RootFolder" -ForegroundColor Green
