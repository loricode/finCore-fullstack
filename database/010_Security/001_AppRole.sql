/*
    Rol de base de datos de la aplicación (FinCore.Api y FinCore.Worker). Script REPETIBLE.

    Mínimo privilegio:
      - Lee y escribe en las tablas de negocio, pero NO tiene DELETE: las operaciones, la auditoría y
        el historial nunca se borran, y los tokens se revocan en lugar de eliminarse.
        Excepción: DELETE en sec.UserRoles, sec.UserBranches y sec.UserWindowsAccounts (filas de relación).
      - Ejecuta los procedimientos de conciliación, ETL y reportes.
      - Sin permisos de DDL: no puede crear ni alterar objetos (el esquema lo despliega deploy.ps1).
    El login y el usuario que pertenecen al rol los crea _deploy/100_AppLogin.sql (solo con -AppLogin).
*/
SET NOCOUNT ON;

IF DATABASE_PRINCIPAL_ID(N'fincore_app_role') IS NULL
    CREATE ROLE fincore_app_role AUTHORIZATION dbo;

GRANT SELECT, INSERT, UPDATE ON SCHEMA::sec         TO fincore_app_role;
GRANT SELECT, INSERT, UPDATE ON SCHEMA::fin         TO fincore_app_role;
GRANT SELECT, INSERT, UPDATE ON SCHEMA::rec         TO fincore_app_role;
GRANT SELECT, INSERT, UPDATE ON SCHEMA::etl         TO fincore_app_role;
GRANT SELECT, INSERT         ON SCHEMA::stg         TO fincore_app_role;
GRANT SELECT, INSERT, UPDATE ON SCHEMA::integration TO fincore_app_role;
GRANT SELECT, INSERT         ON SCHEMA::audit       TO fincore_app_role;

-- Única excepción al "sin DELETE": las tablas de asignación (HU-02, HU-04). Cambiar los roles o las sucursales
-- de un usuario quita filas de relación; el usuario y su historial no se borran nunca.
GRANT DELETE ON OBJECT::sec.UserRoles    TO fincore_app_role;
GRANT DELETE ON OBJECT::sec.UserBranches TO fincore_app_role;
GRANT DELETE ON OBJECT::sec.UserWindowsAccounts TO fincore_app_role;  -- quitar la cuenta de Windows de SSRS (Etapa 6)

GRANT EXECUTE ON SCHEMA::rec TO fincore_app_role;
GRANT EXECUTE ON SCHEMA::stg TO fincore_app_role;
GRANT EXECUTE ON SCHEMA::etl TO fincore_app_role;
GRANT EXECUTE ON SCHEMA::rpt TO fincore_app_role;
