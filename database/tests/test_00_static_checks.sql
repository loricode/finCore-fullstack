/*
    Verificaciones estáticas del esquema.
    SQL Server compila un procedimiento aunque referencie una tabla que no existe (resolución diferida):
    esta prueba detecta esas referencias rotas antes de que fallen en producción.
*/
SET NOCOUNT ON;

DECLARE @Broken NVARCHAR(4000) =
(
    SELECT STRING_AGG(CONCAT(OBJECT_SCHEMA_NAME(d.referencing_id), N'.', OBJECT_NAME(d.referencing_id),
                             N' -> ', d.referenced_schema_name, N'.', d.referenced_entity_name), N'; ')
    FROM sys.sql_expression_dependencies d
    WHERE d.referenced_id IS NULL
      AND d.referenced_schema_name IS NOT NULL            -- excluye tablas temporales y variables
      AND d.referenced_database_name IS NULL
      AND OBJECT_SCHEMA_NAME(d.referencing_id) IN (N'sec', N'fin', N'rec', N'stg', N'etl', N'integration', N'audit', N'rpt', N'dbo')
      AND d.referenced_entity_name NOT IN (N'sp_set_session_context')
);
EXEC test.usp_AssertEquals NULL, @Broken, N'referencias a objetos inexistentes';
EXEC test.usp_Pass N'sin referencias rotas en módulos';

-- Toda tabla de negocio tiene llave primaria.
DECLARE @NoPk NVARCHAR(4000) =
(
    SELECT STRING_AGG(CONCAT(SCHEMA_NAME(t.schema_id), N'.', t.name), N', ')
    FROM sys.tables t
    WHERE SCHEMA_NAME(t.schema_id) IN (N'sec', N'fin', N'rec', N'stg', N'etl', N'integration', N'audit')
      AND NOT EXISTS (SELECT 1 FROM sys.indexes i WHERE i.object_id = t.object_id AND i.is_primary_key = 1)
);
EXEC test.usp_AssertEquals NULL, @NoPk, N'tablas sin llave primaria';
EXEC test.usp_Pass N'todas las tablas tienen llave primaria';

-- Toda llave foránea está habilitada y confiable (WITH CHECK): el optimizador puede usarlas.
DECLARE @Untrusted INT = (SELECT COUNT(*) FROM sys.foreign_keys WHERE is_disabled = 1 OR is_not_trusted = 1);
EXEC test.usp_AssertEquals 0, @Untrusted, N'llaves foráneas deshabilitadas o no confiables';
EXEC test.usp_Pass N'llaves foráneas confiables';

-- Los catálogos coinciden con los enums del dominio.
DECLARE @Types INT = (SELECT COUNT(*) FROM fin.TransactionTypes), @Status INT = (SELECT COUNT(*) FROM fin.TransactionStatus),
        @Results INT = (SELECT COUNT(*) FROM rec.ReconciliationResultTypes);
EXEC test.usp_AssertEquals 5, @Types, N'tipos de operación';
EXEC test.usp_AssertEquals 5, @Status, N'estados de operación';
EXEC test.usp_AssertEquals 7, @Results, N'tipos de resultado de conciliación';
EXEC test.usp_Pass N'catálogos sembrados';
