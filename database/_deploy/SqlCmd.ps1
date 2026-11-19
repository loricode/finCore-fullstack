# Funciones compartidas por deploy.ps1 y run-tests.ps1. Compatible con Windows PowerShell 5.1 y PowerShell 7.

function New-SqlCmdArgs {
    param(
        [string]$Server,
        [string]$User,
        [string]$Password,
        [switch]$TrustServerCertificate
    )
    # -b aborta ante error · -I QUOTED_IDENTIFIER ON (índices filtrados y columnas calculadas) · -f 65001 UTF-8
    $list = @('-S', $Server, '-b', '-I', '-f', '65001')
    if ($User) { $list += @('-U', $User, '-P', $Password) } else { $list += '-E' }
    if ($TrustServerCertificate) { $list += '-C' }
    return , $list
}

function Invoke-SqlQuery {
    # Ejecuta una consulta y devuelve las líneas de salida no vacías, sin encabezados.
    param([string[]]$ConnArgs, [string]$Database, [string]$Query)
    $output = & sqlcmd @ConnArgs -d $Database -h -1 -W -Q "SET NOCOUNT ON; $Query"
    if ($LASTEXITCODE -ne 0) { throw "sqlcmd falló ($LASTEXITCODE): $($output -join [Environment]::NewLine)" }
    return @($output | Where-Object { $_ -and $_.ToString().Trim() -ne '' } | ForEach-Object { $_.ToString().Trim() })
}

function Invoke-SqlFile {
    # sqlcmd escribe los errores de SQL Server en la salida estándar, así que basta con capturarla.
    param([string[]]$ConnArgs, [string]$Database, [string]$Path, [string[]]$Extra = @())
    $output = & sqlcmd @ConnArgs -d $Database -i $Path @Extra
    $code = $LASTEXITCODE
    return [pscustomobject]@{ ExitCode = $code; Output = ($output -join [Environment]::NewLine) }
}

function Get-ScriptChecksum {
    # SHA-256 del contenido con saltos de línea normalizados: el checksum no cambia por core.autocrlf de Git.
    param([string]$Path)
    $text = [IO.File]::ReadAllText($Path, [Text.Encoding]::UTF8).Replace("`r`n", "`n")
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        $bytes = $sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($text))
        return -join ($bytes | ForEach-Object { $_.ToString('x2') })
    } finally { $sha.Dispose() }
}
