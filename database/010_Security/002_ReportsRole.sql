/*
    Rol de solo lectura para SQL Server Reporting Services. Script REPETIBLE.

    Solo EXECUTE en el esquema rpt: los informes no pueden leer tablas ni vistas directamente; ven exactamente lo que
    devuelven los procedimientos rpt.*, que validan el permiso reports.read y el alcance por sucursal del usuario.
    (Funciona por encadenamiento de propiedad: rpt, sec, fin, rec y etl pertenecen a dbo.)
    El login lo crea _deploy/101_ReportsLogin.sql (deploy.ps1 -ReportsLogin).
*/
SET NOCOUNT ON;

IF DATABASE_PRINCIPAL_ID(N'fincore_reports_role') IS NULL
    CREATE ROLE fincore_reports_role AUTHORIZATION dbo;

GRANT EXECUTE ON SCHEMA::rpt TO fincore_reports_role;
