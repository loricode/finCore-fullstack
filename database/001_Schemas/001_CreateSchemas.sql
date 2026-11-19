/*
    Esquemas de FinCore. Cada esquema corresponde a un módulo del monolito modular:
    permite conceder permisos por módulo y ubicar rápido cada objeto.

      sec          Seguridad: empresas, sucursales, usuarios, roles y permisos
      fin          Cuentas y operaciones financieras
      rec          Conciliación bancaria: extractos, ejecuciones y resultados
      stg          Staging: datos crudos que carga SSIS antes de validarlos
      etl          Historial de ejecuciones ETL y errores por registro
      integration  Eventos recibidos de proveedores externos (webhooks vía Node.js)
      audit        Auditoría de acciones de usuario (solo inserción)
      rpt          Procedimientos de lectura para reportes (API y SSRS)
*/
SET NOCOUNT ON;

IF SCHEMA_ID(N'sec')         IS NULL EXEC (N'CREATE SCHEMA sec AUTHORIZATION dbo;');
IF SCHEMA_ID(N'fin')         IS NULL EXEC (N'CREATE SCHEMA fin AUTHORIZATION dbo;');
IF SCHEMA_ID(N'rec')         IS NULL EXEC (N'CREATE SCHEMA rec AUTHORIZATION dbo;');
IF SCHEMA_ID(N'stg')         IS NULL EXEC (N'CREATE SCHEMA stg AUTHORIZATION dbo;');
IF SCHEMA_ID(N'etl')         IS NULL EXEC (N'CREATE SCHEMA etl AUTHORIZATION dbo;');
IF SCHEMA_ID(N'integration') IS NULL EXEC (N'CREATE SCHEMA integration AUTHORIZATION dbo;');
IF SCHEMA_ID(N'audit')       IS NULL EXEC (N'CREATE SCHEMA audit AUTHORIZATION dbo;');
IF SCHEMA_ID(N'rpt')         IS NULL EXEC (N'CREATE SCHEMA rpt AUTHORIZATION dbo;');
