/*
    Login de SQL Server para Reporting Services, miembro de fincore_reports_role (solo EXECUTE en rpt).
    Lo ejecuta deploy.ps1 con -ReportsLogin y -ReportsPassword (variables de sqlcmd). Idempotente.
*/
SET NOCOUNT ON;

IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'$(ReportsLogin)')
    CREATE LOGIN [$(ReportsLogin)] WITH PASSWORD = N'$(ReportsPassword)', CHECK_POLICY = ON, DEFAULT_DATABASE = [$(DatabaseName)];
ELSE
    ALTER LOGIN [$(ReportsLogin)] WITH PASSWORD = N'$(ReportsPassword)';

IF DATABASE_PRINCIPAL_ID(N'$(ReportsLogin)') IS NULL
    CREATE USER [$(ReportsLogin)] FOR LOGIN [$(ReportsLogin)] WITH DEFAULT_SCHEMA = rpt;

IF IS_ROLEMEMBER(N'fincore_reports_role', N'$(ReportsLogin)') = 0
    ALTER ROLE fincore_reports_role ADD MEMBER [$(ReportsLogin)];

PRINT N'Usuario de reportes $(ReportsLogin) listo (rol fincore_reports_role).';
