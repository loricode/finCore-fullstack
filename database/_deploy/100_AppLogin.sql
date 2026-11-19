/*
    Login de SQL Server y usuario de base de datos para la aplicación, miembro de fincore_app_role.
    Lo ejecuta deploy.ps1 solo cuando recibe -AppLogin y -AppPassword (entornos Docker y servidores),
    con las variables de sqlcmd $(AppLogin) y $(AppPassword). Es idempotente: si el login existe,
    actualiza la contraseña. No se registra en dbo.SchemaVersions porque depende del entorno.
*/
SET NOCOUNT ON;

IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'$(AppLogin)')
    CREATE LOGIN [$(AppLogin)] WITH PASSWORD = N'$(AppPassword)', CHECK_POLICY = ON, DEFAULT_DATABASE = [$(DatabaseName)];
ELSE
    ALTER LOGIN [$(AppLogin)] WITH PASSWORD = N'$(AppPassword)';

IF DATABASE_PRINCIPAL_ID(N'$(AppLogin)') IS NULL
    CREATE USER [$(AppLogin)] FOR LOGIN [$(AppLogin)] WITH DEFAULT_SCHEMA = dbo;

IF IS_ROLEMEMBER(N'fincore_app_role', N'$(AppLogin)') = 0
    ALTER ROLE fincore_app_role ADD MEMBER [$(AppLogin)];

PRINT N'Usuario de aplicación $(AppLogin) listo (rol fincore_app_role).';
